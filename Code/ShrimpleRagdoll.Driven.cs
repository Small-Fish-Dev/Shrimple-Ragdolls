namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// How hard the root body pulls itself back to the animation<br />
	/// Lower it and the whole ragdoll is easier to drag around by a limb
	/// </summary>
	[Property, Group( "Driven" )]
	public float RootDriveStrength { get; set; } = 16f;

	/// <summary>
	/// How hard a limb pulls itself back to the animation<br />
	/// This is the number a grab has to beat to hold a limb still
	/// </summary>
	[Property, Group( "Driven" )]
	public float BodyDriveStrength { get; set; } = 12f;

	/// <summary>
	/// How hard the root twists itself back to the animated orientation, in radians per second squared
	/// </summary>
	[Property, Group( "Driven" )]
	public float RootAngularStrength { get; set; } = 180f;

	/// <summary>
	/// Spring frequency in hz of the drive, higher tracks the animation more tightly
	/// </summary>
	[Advanced, Property, Group( "Driven" )]
	public float DriveFrequency { get; set; } = 36f;

	/// <summary>
	/// Scales the strength of every body at once
	/// </summary>
	[Property, Sync, Group( "Driven" ), Range( 0f, 1f ), Step( 0.05f )]
	public float StrengthMultiplier { get; set; } = 1f;

	/// <summary>
	/// How far the root may drift from its animation before <see cref="IsOverwhelmed"/> trips
	/// </summary>
	[Property, Group( "Driven" )]
	public float MaxPoseError { get; set; } = 48f;

	private const float DriveDamping = 1f;
	private const float StrengthBlendTime = 0.15f;
	private const float GrabLimpBlendTime = 0.12f;
	private const float AnimationVelocityTimeout = 0.1f;

	private Vector3 _poseError;
	private Vector3 _poseAngularError;

	/// <summary>
	/// How far the root has been dragged from where the animation wants it
	/// </summary>
	public float PoseErrorMagnitude => _poseError.Length;

	/// <summary>
	/// True once they've been dragged further than <see cref="MaxPoseError"/> and gives up on matching animations
	/// </summary>
	public bool IsOverwhelmed => PoseErrorMagnitude > MaxPoseError;

	/// <summary>
	/// Fires when <see cref="IsOverwhelmed"/> becomes true
	/// </summary>
	public Action Overwhelmed { get; set; }

	private bool _wasOverwhelmed;

	private struct StrengthState
	{
		public float Current;
		public float Target;
		public float Rate;
		public bool HasRestore;
		public TimeUntil Hold;
		public float RestoreRate;
	}

	private StrengthState[] _strength;
	private float[] _grabLimp;
	private float[] _grabLimpTarget;
	private Vector3[] _lastAnimPosition;
	private Rotation[] _lastAnimRotation;
	private float[] _lastAnimTime;
	private Vector3[] _animVelocity;
	private Vector3[] _animAngularVelocity;
	private bool[] _hasLastAnim;
	private int _rootBodyIndex = -1;

	private bool EnsureDrivenBuffers()
	{
		if ( !EnsureBodyCache() )
			return false;

		var count = Bodies.Count;
		if ( _strength != null && _strength.Length == count )
			return true;

		_strength = new StrengthState[count];
		_grabLimp = new float[count];
		_grabLimpTarget = new float[count];
		_lastAnimPosition = new Vector3[count];
		_lastAnimRotation = new Rotation[count];
		_lastAnimTime = new float[count];
		_animVelocity = new Vector3[count];
		_animAngularVelocity = new Vector3[count];
		_hasLastAnim = new bool[count];
		_rootBodyIndex = -1;

		for ( var i = 0; i < count; i++ )
		{
			_strength[i] = new StrengthState { Current = 1f, Target = 1f };
			_grabLimp[i] = 1f;
			_grabLimpTarget[i] = 1f;

			if ( _rootBodyIndex < 0 && IsRootBody( Bodies[i] ) )
				_rootBodyIndex = i;
		}

		return true;
	}

	/// <summary>
	/// The strength a bone is driven at, 0 is limp and 1 is holding the animation pose
	/// </summary>
	public float GetBodyStrength( int boneIndex )
	{
		var index = GetBodyIndex( boneIndex );
		return index < 0 ? 0f : GetBodyStrengthByIndex( index );
	}

	/// <summary>
	/// The strength a bone is driven at, 0 is limp and 1 is holding the animation pose
	/// </summary>
	public float GetBodyStrength( string boneName )
	{
		var bone = Renderer?.Model?.Bones?.GetBone( boneName );
		return bone == null ? 0f : GetBodyStrength( bone.Index );
	}

	private float GetBodyStrengthByIndex( int index )
		=> Math.Clamp( _strength[index].Current * _grabLimp[index] * StrengthMultiplier
			* (1f - HaulFraction * HaulMotorLimpness), 0f, 1f );

	private float GetDriveStrengthByIndex( int index )
		=> Math.Clamp( _strength[index].Current * _grabLimp[index] * StrengthMultiplier
			* (1f - HaulFraction * HaulLimpness), 0f, 1f );

	/// <summary>
	/// Ease a bone's strength toward a value and leave it there
	/// </summary>
	/// <param name="boneIndex">Which bone</param>
	/// <param name="strength">0 is limp, 1 is holding the animation pose</param>
	/// <param name="blendTime">Seconds to get there, negative for the default</param>
	public void SetBodyStrength( int boneIndex, float strength, float blendTime = -1f )
	{
		var index = GetBodyIndex( boneIndex );

		if ( index >= 0 )
			SetBodyStrengthByIndex( index, strength, blendTime );
	}

	/// <summary>
	/// Ease a bone's strength toward a value and leave it there
	/// </summary>
	public void SetBodyStrength( string boneName, float strength, float blendTime = -1f )
	{
		var bone = Renderer?.Model?.Bones?.GetBone( boneName );

		if ( bone != null )
			SetBodyStrength( bone.Index, strength, blendTime );
	}

	private void SetBodyStrengthByIndex( int index, float strength, float blendTime )
	{
		strength = Math.Clamp( strength, 0f, 1f );

		var state = _strength[index];
		state.Target = strength;
		state.Rate = RateFor( state.Current, strength, blendTime );
		state.HasRestore = false;
		_strength[index] = state;
	}

	private static float RateFor( float from, float to, float blendTime )
	{
		if ( blendTime < 0f )
			blendTime = StrengthBlendTime;

		return blendTime <= 0f ? float.MaxValue : MathF.Max( MathF.Abs( to - from ), 0.001f ) / blendTime;
	}

	/// <summary>
	/// Drop a bone's strength to <paramref name="strength"/> (0 is fully limp), hold it for <paramref name="holdTime"/>, then climb back to full over <paramref name="recoverTime"/>
	/// </summary>
	public void WeakenBody( int boneIndex, float strength, float holdTime, float recoverTime, float blendTime = -1f )
	{
		var index = GetBodyIndex( boneIndex );

		if ( index >= 0 )
			WeakenBodyByIndex( index, strength, holdTime, recoverTime, blendTime );
	}

	private void WeakenBodyByIndex( int index, float strength, float holdTime, float recoverTime, float blendTime )
	{
		strength = Math.Clamp( strength, 0f, 1f );
		var state = _strength[index];

		// A second hit while they're still limp shouldn't make them tougher
		if ( state.HasRestore && state.Target < strength )
			strength = state.Target;

		state.Target = strength;
		state.Rate = RateFor( state.Current, strength, blendTime );
		state.HasRestore = true;
		state.Hold = MathF.Max( holdTime, 0f );
		state.RestoreRate = recoverTime <= 0f ? float.MaxValue : 1f / recoverTime;
		_strength[index] = state;
	}

	/// <summary>
	/// Like <see cref="WeakenBody"/> but the limpness bleeds along the skeleton, fading with every joint it crosses, so an arm hangs off a held wrist instead of snapping straight<br />
	/// <paramref name="falloff"/> is how much limpness survives each joint and <paramref name="depth"/> how many it spreads across, negative for the whole skeleton.<br />
	/// Leave both times at 0 to hold it indefinitely
	/// </summary>
	public void WeakenChain( int boneIndex, float strength, float falloff = 0.5f, int depth = -1, bool includeChildren = true,
		float holdTime = 0f, float recoverTime = 0f, float blendTime = -1f )
	{
		if ( !EnsureDrivenBuffers() || !Renderer.IsValid() || !Renderer.Model.IsValid() )
			return;

		foreach ( var (index, chainStrength) in EnumerateChain( boneIndex, strength, falloff, depth, includeChildren ) )
		{
			if ( recoverTime > 0f || holdTime > 0f )
				WeakenBodyByIndex( index, chainStrength, holdTime, recoverTime, blendTime );
			else
				SetBodyStrengthByIndex( index, chainStrength, blendTime );
		}
	}

	/// <summary>
	/// Ease a bone back to full strength
	/// </summary>
	public void RestoreStrength( int boneIndex, float blendTime = -1f ) => SetBodyStrength( boneIndex, 1f, blendTime );

	/// <summary>
	/// Ease every bone back to full strength
	/// </summary>
	public void RestoreAllStrength( float blendTime = -1f )
	{
		if ( !EnsureDrivenBuffers() )
			return;

		for ( var i = 0; i < _strength.Length; i++ )
			SetBodyStrengthByIndex( i, 1f, blendTime );
	}

	/// <summary>
	/// Every body out from a bone and the strength that reaches it, blending back toward full by <paramref name="falloff"/> per joint crossed
	/// </summary>
	private IEnumerable<(int Index, float Strength)> EnumerateChain( int boneIndex, float strength, float falloff, int depth, bool includeChildren )
	{
		var bones = Renderer.Model.Bones.AllBones;

		if ( boneIndex < 0 || boneIndex >= bones.Count )
			yield break;

		var maxDepth = depth < 0 ? int.MaxValue : depth;
		var rootBone = bones[boneIndex];
		var bone = rootBone;
		var step = 0;

		while ( bone != null && step <= maxDepth )
		{
			if ( _bodyIndexByBone.TryGetValue( bone.Index, out var index ) )
			{
				yield return (index, ChainStrength( strength, falloff, step ));
				step++; // Only count steps that crossed an actual physics joint
			}

			bone = bone.Parent;
		}

		if ( !includeChildren )
			yield break;

		foreach ( var descendant in GetDescendantBones( rootBone ) )
		{
			if ( descendant.Index == boneIndex || !_bodyIndexByBone.TryGetValue( descendant.Index, out var index ) )
				continue;

			var childStep = BodyDepthBetween( descendant, rootBone );

			if ( childStep >= 0 && childStep <= maxDepth )
				yield return (index, ChainStrength( strength, falloff, childStep ));
		}
	}

	private static float ChainStrength( float strength, float falloff, int step )
		=> step <= 0 ? strength : float.Lerp( 1f, strength, MathF.Pow( Math.Clamp( falloff, 0f, 1f ), step ) );

	/// <summary>
	/// How many bodies sit between a bone and an ancestor, or -1 if it isn't a descendant of it
	/// </summary>
	private int BodyDepthBetween( BoneCollection.Bone bone, BoneCollection.Bone ancestor )
	{
		var steps = 0;
		var current = bone;

		while ( current != null )
		{
			if ( current.Index == ancestor.Index )
				return steps;

			if ( _bodyIndexByBone.ContainsKey( current.Index ) )
				steps++;

			current = current.Parent;
		}

		return -1;
	}

	/// <summary>
	/// Punch them. Applies an impulse and eases the strength out of the limb it landed on, so the blow travels through the skeleton<br />
	/// Bodies within <paramref name="radius"/> take a falloff share of the impulse
	/// </summary>
	public ModelPhysics.Body? ApplyRecoil( Vector3 hitPosition, Vector3 impulse, float radius = 20f, float limpness = 0.85f,
		float holdTime = 0.1f, float recoverTime = 0.35f, float chainFalloff = 0.5f )
	{
		var hitBody = GetNearestBody( hitPosition );

		return hitBody.HasValue
			? ApplyRecoil( hitBody.Value, hitPosition, impulse, radius, limpness, holdTime, recoverTime, chainFalloff )
			: null;
	}

	/// <summary>
	/// Punch a specific body. Prefer this when you traced the hit, so a blow to the knuckles weakens the hand and not whichever bone origin happened to be nearest<br />
	/// Bodies within <paramref name="radius"/> take a falloff share of the impulse
	/// </summary>
	public ModelPhysics.Body? ApplyRecoil( ModelPhysics.Body target, Vector3 hitPosition, Vector3 impulse, float radius = 20f,
		float limpness = 0.85f, float holdTime = 0.1f, float recoverTime = 0.35f, float chainFalloff = 0.5f )
	{
		if ( !EnsureDrivenBuffers() || !PhysicsWereCreated || !target.Component.IsValid() )
			return null;

		var targetPhysics = target.Component.PhysicsBody;

		if ( !targetPhysics.IsValid() )
			return null;

		WakePhysics();
		targetPhysics.ApplyImpulseAt( hitPosition, impulse );

		// Neighbours share the blow as a speed, a raw impulse that nudges a torso sends a hand into orbit
		if ( radius > 0f )
		{
			var speed = impulse.Length / MathF.Max( targetPhysics.Mass, 0.001f );
			var direction = impulse.Normal;

			foreach ( var body in Bodies )
			{
				if ( body.Bone == target.Bone || !body.Component.IsValid() )
					continue;

				var physics = body.Component.PhysicsBody;

				if ( !physics.IsValid() )
					continue;

				var away = Vector3.DistanceBetween( hitPosition, body.Component.WorldPosition );

				if ( away > radius )
					continue;

				var falloff = 1f - away / radius;
				physics.ApplyImpulse( direction * (speed * falloff * falloff * physics.Mass) );
			}
		}

		WeakenChain( target.Bone, 1f - Math.Clamp( limpness, 0f, 1f ), chainFalloff,
			holdTime: holdTime, recoverTime: MathF.Max( recoverTime, 0.001f ) );

		return target;
	}

	/// <summary>
	/// <see cref="ApplyRecoil(Vector3, Vector3, float, float, float, float, float)"/> but networked to the owner<br />
	/// Pass -1 for <paramref name="boneIndex"/> to resolve the nearest body on the owner instead
	/// </summary>
	[Rpc.Owner]
	public void NetworkRecoil( int boneIndex, Vector3 hitPosition, Vector3 impulse, float radius = 20f,
		float limpness = 0.85f, float holdTime = 0.1f, float recoverTime = 0.35f, float chainFalloff = 0.5f )
	{
		// The bone travels rather than the body, a ModelPhysics.Body is local component references
		var index = boneIndex < 0 ? -1 : GetBodyIndex( boneIndex );
		var target = index >= 0 ? Bodies[index] : GetNearestBody( hitPosition );

		if ( target is { } body )
			ApplyRecoil( body, hitPosition, impulse, radius, limpness, holdTime, recoverTime, chainFalloff );
	}

	/// <summary>
	/// Punch a body you already traced, from anywhere
	/// </summary>
	public void NetworkRecoil( ModelPhysics.Body target, Vector3 hitPosition, Vector3 impulse, float radius = 20f,
		float limpness = 0.85f, float holdTime = 0.1f, float recoverTime = 0.35f, float chainFalloff = 0.5f )
		=> NetworkRecoil( target.Bone, hitPosition, impulse, radius, limpness, holdTime, recoverTime, chainFalloff );

	/// <summary>
	/// The body closest to a world position
	/// </summary>
	public ModelPhysics.Body? GetNearestBody( Vector3 worldPosition, out float distance )
	{
		distance = float.MaxValue;
		ModelPhysics.Body? nearest = null;

		if ( Bodies is not { Count: > 0 } )
			return null;

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			var away = Vector3.DistanceBetween( worldPosition, body.Component.WorldPosition );

			if ( away >= distance )
				continue;

			distance = away;
			nearest = body;
		}

		return nearest;
	}

	/// <summary>
	/// The body closest to a world position
	/// </summary>
	public ModelPhysics.Body? GetNearestBody( Vector3 worldPosition ) => GetNearestBody( worldPosition, out _ );

	/// <summary>
	/// The body backed by a physics body, for turning a trace result into a bone<br />
	/// Prefer this over <see cref="GetNearestBody(Vector3)"/> when you have a trace
	/// </summary>
	public ModelPhysics.Body? GetBodyByPhysicsBody( PhysicsBody physicsBody )
	{
		if ( !physicsBody.IsValid() || Bodies is not { Count: > 0 } )
			return null;

		foreach ( var body in Bodies )
		{
			if ( body.Component.IsValid() && body.Component.PhysicsBody == physicsBody )
				return body;
		}

		return null;
	}

	/// <summary>
	/// How much the ragdoll is being pushed, as a velocity to apply yourself so it follows where you're dragging it.<br />
	/// Drift under <paramref name="deadZone"/> units is ignored
	/// </summary>
	public Vector3 GetPushVelocity( float responsiveness = 8f, float maxSpeed = 400f, float deadZone = 2f, bool horizontalOnly = true )
	{
		var error = horizontalOnly ? _poseError.WithZ( 0f ) : _poseError;
		var length = error.Length;

		if ( length <= deadZone )
			return Vector3.Zero;

		return (error.Normal * ((length - deadZone) * responsiveness)).ClampLength( maxSpeed );
	}

	/// <summary>
	/// A yaw speed in degrees per second for your character's facing, so twisting them by a shoulder turns them. Similar to <see cref="GetPushVelocity"/>
	/// </summary>
	public float GetTurnSpeed( float responsiveness = 6f, float maxSpeed = 360f, float deadZone = 5f )
	{
		var yaw = _poseAngularError.z.RadianToDegree();
		var magnitude = MathF.Abs( yaw );

		if ( magnitude <= deadZone )
			return 0f;

		return Math.Clamp( MathF.Sign( yaw ) * (magnitude - deadZone) * responsiveness, -maxSpeed, maxSpeed );
	}

	private void UpdateDriven()
	{
		if ( !PhysicsWereCreated || IsProxy )
			return;

		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		if ( !EnsureDrivenBuffers() )
			return;

		UpdateGrabs();
		UpdateStrengths();

		// Settling after a teleport zeroes velocity every tick, driving into that just fights it
		if ( _settleTicks > 0 )
			return;

		var anyDriven = false;
		var gravity = Scene.PhysicsWorld.Gravity;
		var gravityLength = gravity.Length;

		for ( var i = 0; i < Bodies.Count; i++ )
		{
			var body = Bodies[i];

			if ( GetEffectiveMode( body.Bone ) != RagdollMode.Driven )
			{
				_hasLastAnim[i] = false;
				continue;
			}

			anyDriven = true;
			DriveBody( body, i, gravity, gravityLength );
		}

		if ( !anyDriven )
		{
			_wasOverwhelmed = false;
			return;
		}

		foreach ( var joint in Joints )
		{
			if ( GetEffectiveMode( joint.Body2.Bone ) == RagdollMode.Driven )
				DriveJoint( joint );
		}

		var overwhelmed = IsOverwhelmed;

		if ( overwhelmed && !_wasOverwhelmed )
			Overwhelmed?.Invoke();

		_wasOverwhelmed = overwhelmed;
	}

	private void UpdateStrengths()
	{
		var delta = Time.Delta;

		for ( var i = 0; i < _strength.Length; i++ )
		{
			var state = _strength[i];

			// Waiting for the dip to land before recovering means a zero length hold still reads as a hit
			if ( state.HasRestore && state.Hold && state.Current <= state.Target + 0.001f )
			{
				state.HasRestore = false;
				state.Target = 1f;
				state.Rate = state.RestoreRate;
			}

			if ( state.Current != state.Target )
				state.Current = state.Current.Approach( state.Target, state.Rate * delta );

			_strength[i] = state;

			// Grabs get their own layer so a grab and a recoil on one limb don't overwrite each other
			_grabLimp[i] = _grabLimp[i].Approach( _grabLimpTarget[i], delta / GrabLimpBlendTime );
		}
	}

	/// <summary>
	/// Springs one body toward its animation pose within the budget its strength allows
	/// </summary>
	private void DriveBody( ModelPhysics.Body body, int index, Vector3 gravity, float gravityLength )
	{
		var rigidbody = body.Component;

		if ( !rigidbody.IsValid() || !rigidbody.PhysicsBody.IsValid() )
		{
			_hasLastAnim[index] = false;
			return;
		}

		var bone = Renderer.Model.Bones.AllBones[body.Bone];

		if ( !Renderer.TryGetBoneTransformAnimation( bone, out var target ) )
		{
			_hasLastAnim[index] = false;
			return;
		}

		var delta = Time.Delta;
		var isRoot = index == _rootBodyIndex;

		SampleAnimationVelocity( index, target );

		var toTarget = target.Position - rigidbody.WorldPosition;

		// A teleport shouldn't turn into a launch, snap instead of springing at it
		if ( toTarget.Length > TeleportSnapDistance )
		{
			rigidbody.WorldTransform = target;
			rigidbody.Velocity = Vector3.Zero;
			rigidbody.AngularVelocity = Vector3.Zero;

			if ( isRoot )
			{
				_poseError = Vector3.Zero;
				_poseAngularError = Vector3.Zero;
			}

			return;
		}

		if ( isRoot )
		{
			_poseError = -toTarget;
			_poseAngularError = ToRotationVector( rigidbody.WorldRotation * target.Rotation.Inverse );
		}

		var strength = GetDriveStrengthByIndex( index );

		if ( strength <= 0f )
			return;

		var budget = (isRoot ? RootDriveStrength : BodyDriveStrength) * gravityLength * strength;

		// Hold up their own weight, so full strength means no sag and zero means a normal fall
		var gravityScale = rigidbody.Gravity ? rigidbody.GravityScale : 0f;
		var acceleration = -gravity * (gravityScale * strength)
			+ SpringAcceleration( toTarget, rigidbody.Velocity - _animVelocity[index], DriveFrequency, DriveDamping, delta );

		if ( budget > 0f )
		{
			rigidbody.Velocity += acceleration.ClampLength( budget ) * delta;
			rigidbody.Sleeping = false;
		}

		// Children get their orientation from the joint motors, only the root twists itself upright
		if ( isRoot && RootAngularStrength > 0f )
		{
			var rotationError = ToRotationVector( target.Rotation * rigidbody.WorldRotation.Inverse );
			var angular = SpringAcceleration( rotationError, rigidbody.AngularVelocity - _animAngularVelocity[index],
				DriveFrequency, DriveDamping, delta );

			rigidbody.AngularVelocity += angular.ClampLength( RootAngularStrength * strength ) * delta;
		}
	}

	private void DriveJoint( ModelPhysics.Joint joint )
	{
		if ( !joint.Component.IsValid() )
			return;

		var index = GetBodyIndex( joint.Body2.Bone );
		var strength = index < 0 ? 0f : GetBodyStrengthByIndex( index );

		if ( strength <= 0.001f )
			SetJointMotorDisabled( joint );
		else
			MoveJointFromAnimation( joint, MotorFrequency * strength, MotorDamping );
	}

	private static void SetJointMotorDisabled( ModelPhysics.Joint joint )
	{
		if ( joint.Component is BallJoint ballJoint )
		{
			ballJoint.Motor = BallJoint.MotorMode.Disabled;
			ballJoint.Frequency = 0f;
		}
		else if ( joint.Component is HingeJoint hingeJoint )
		{
			hingeJoint.Motor = HingeJoint.MotorMode.Disabled;
			hingeJoint.Frequency = 0f;
		}
	}

	/// <summary>
	/// Tracks how fast a bone's animation is moving
	/// </summary>
	private void SampleAnimationVelocity( int index, Transform target )
	{
		if ( !_hasLastAnim[index] )
		{
			_lastAnimPosition[index] = target.Position;
			_lastAnimRotation[index] = target.Rotation;
			_lastAnimTime[index] = Time.Now;
			_animVelocity[index] = Vector3.Zero;
			_animAngularVelocity[index] = Vector3.Zero;
			_hasLastAnim[index] = true;
			return;
		}

		var elapsed = Time.Now - _lastAnimTime[index];
		var moved = target.Position - _lastAnimPosition[index];

		if ( elapsed > 0f && !moved.IsNearlyZero( 0.001f ) )
		{
			_animVelocity[index] = moved / elapsed;
			_animAngularVelocity[index] = ToRotationVector( target.Rotation * _lastAnimRotation[index].Inverse ) / elapsed;
			_lastAnimPosition[index] = target.Position;
			_lastAnimRotation[index] = target.Rotation;
			_lastAnimTime[index] = Time.Now;
		}
		else if ( elapsed > AnimationVelocityTimeout )
		{
			// Stopped
			_animVelocity[index] = Vector3.Zero;
			_animAngularVelocity[index] = Vector3.Zero;
			_lastAnimTime[index] = Time.Now;
		}
	}

	/// <summary>
	/// Damped spring acceleration, the velocity change <see cref="Vector3.SpringDamp(in Vector3, in Vector3, ref Vector3, float, float, float)"/> would make over
	/// this step. We want it as an acceleration so it can be clamped to the force budget
	/// </summary>
	private static Vector3 SpringAcceleration( Vector3 offset, Vector3 relativeVelocity, float frequency, float damping, float delta )
	{
		if ( frequency <= 0f || delta <= 0f )
			return Vector3.Zero;

		var velocity = relativeVelocity;
		Vector3.SpringDamp( -offset, Vector3.Zero, ref velocity, delta, frequency, damping );

		return (velocity - relativeVelocity) / delta;
	}

	/// <summary>
	/// Turns a rotation into the axis it spins around scaled by its angle in radians, the short way round
	/// </summary>
	private static Vector3 ToRotationVector( Rotation rotation )
	{
		var w = rotation.w;
		var axis = new Vector3( rotation.x, rotation.y, rotation.z );

		if ( w < 0f )
		{
			w = -w;
			axis = -axis;
		}

		var sine = axis.Length;

		// Under a degree the small angle form is cheaper and better conditioned than the acos
		if ( sine < 0.0001f )
			return axis * 2f;

		return axis / sine * (2f * MathF.Acos( Math.Clamp( w, -1f, 1f ) ));
	}

	/// <summary>
	/// Clears everything driven mode accumulated, called when the ragdoll leaves the mode
	/// </summary>
	private void ResetDriven()
	{
		ReleaseAllGrabs();

		if ( _strength != null )
		{
			for ( var i = 0; i < _strength.Length; i++ )
			{
				_strength[i] = new StrengthState { Current = 1f, Target = 1f };
				_grabLimp[i] = 1f;
				_grabLimpTarget[i] = 1f;
				_hasLastAnim[i] = false;
			}
		}

		_poseError = Vector3.Zero;
		_poseAngularError = Vector3.Zero;
		HaulFraction = 0f;
		_wasOverwhelmed = false;
	}
}
