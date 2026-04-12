namespace ShrimpleRagdolls;

public enum RagdollMode
{
	/// <summary>
	/// Ragdolling is disabled completely
	/// </summary>
	None,
	/// <summary>
	/// Collisions but follows animations 1 to 1
	/// </summary>
	Passive,
	/// <summary>
	/// Physically simulated and tried to follow animations
	/// </summary>
	Active,
	/// <summary>
	/// Completely physical driven, flops around
	/// </summary>
	Enabled,
	/// <summary>
	/// Physical driven but uses joint motors to follow animations
	/// </summary>
	Motor
}

/// <summary>
/// A wrapper for modelphysics with utilities
/// </summary>
[Icon( "sports_martial_arts" )]
public partial class ShrimpleRagdoll : Component
{
	[Property, Sync]
	public RagdollMode Mode
	{
		get;
		set
		{
			if ( field == value )
				return;

			var oldValue = field;
			field = value;
			ApplyRagdollMode( oldValue );
			ModeChanged?.Invoke( oldValue, value ); // We want the new mode to already be in effect when invoking
		}
	} = RagdollMode.Enabled;

	[Property, Hide]
	public ModelPhysics ModelPhysics { get; private set; }

	[Property]
	public SkinnedModelRenderer Renderer
	{
		get;
		set
		{
			field = value;
			EnsureModelPhysics();
		}
	}

	[Property]
	public bool FollowRootPosition { get; set; } = true;

	[Property]
	public bool FollowRootRotation { get; set; } = false;

	/// <summary>
	/// How fast bodies move to the desired position in active mode
	/// </summary>
	[Advanced, Property, Group( "Settings" )]
	public float ActiveLerpTime { get; set; } = 0f;

	/// <summary>
	/// Motor joints frequency
	/// </summary>
	[Advanced, Property, Group( "Settings" )]
	public float MotorFrequency
	{
		get;
		set
		{
			if ( field == value )
				return;
			field = value;
			if ( Mode == RagdollMode.Motor )
				EnableJointMotors( value, MotorDamping );
		}
	} = 30f;

	/// <summary>
	/// Motor joints damping
	/// </summary>
	[Advanced, Property, Group( "Settings" )]
	public float MotorDamping
	{
		get;
		set
		{
			if ( field == value )
				return;
			field = value;
			if ( Mode == RagdollMode.Motor )
				EnableJointMotors( MotorFrequency, value );
		}
	} = 1f;

	public List<ModelPhysics.Body> Bodies => ModelPhysics?.Bodies;
	public List<ModelPhysics.Joint> Joints => ModelPhysics?.Joints;
	public bool PhysicsWereCreated => ModelPhysics?.PhysicsWereCreated ?? false;
	private float _currentJointLimits = 1f;

	/// <summary>
	/// Before, After
	/// </summary>
	public Action<RagdollMode, RagdollMode> ModeChanged { get; set; }

	private void EnsureModelPhysics()
	{
		if ( !Renderer.IsValid() )
			return;

		ModelPhysics = Renderer.GameObject.Components.GetOrCreate<ModelPhysics>();
		ModelPhysics.Renderer = Renderer;
		ModelPhysics.Model = Renderer.Model;
		ModelPhysics.IgnoreRoot = true;
		ModelPhysics.StartAsleep = StartAsleep;
		ModelPhysics.RigidbodyFlags = RigidbodyFlags;
		ModelPhysics.Locking = Locking;
		ModelPhysics.MotionEnabled = MotionEnabled;
		ModelPhysics.Flags |= ComponentFlags.Hidden;
	}

	private void ApplyRagdollMode( RagdollMode oldMode = RagdollMode.None )
	{
		if ( !ModelPhysics.IsValid() )
			return;

		StopLerp();

		if ( oldMode == RagdollMode.Motor )
			DisableJointMotors();
		else if ( oldMode == RagdollMode.Active )
			MultiplyJointLimits( 1f / _currentJointLimits );

		if ( Mode == RagdollMode.None )
		{
			ModelPhysics.Enabled = false;
			Renderer.ClearPhysicsBones();
		}
		else if ( Mode == RagdollMode.Passive )
		{
			ModelPhysics.Enabled = true;
			MotionEnabled = false;
			DisableJoints();

		}
		else if ( Mode == RagdollMode.Active )
		{
			ModelPhysics.Enabled = true;
			MotionEnabled = true;
			SetGravity( false ); // We don't want gravity in active mode otherwise we'll have to fight against it!
			EnableJoints();
			MultiplyJointLimits( 1.5f );
		}
		else if ( Mode == RagdollMode.Enabled )
		{
			ModelPhysics.Enabled = true;
			MotionEnabled = true;
			EnableJoints();
			SetGravity( Gravity );
		}
		else if ( Mode == RagdollMode.Motor )
		{
			ModelPhysics.Enabled = true;
			MotionEnabled = true;
			SetGravity( Gravity );
			EnableJoints();
			EnableJointMotors( MotorFrequency, MotorFrequency );
		}
	}

	protected override void OnUpdate()
	{
		if ( IsLerping && _lerpMode == LerpMode.Mesh )
			UpdateLerp();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsLerping && _lerpMode != LerpMode.Mesh )
			UpdateLerp();

		if ( IsProxy )
			return;

		UpdateRagdollMode();
		FollowRoot();
	}

	private void UpdateRagdollMode()
	{
		if ( !PhysicsWereCreated || IsProxy )
			return;

		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var body in Bodies )
		{
			var effectiveMode = _partialRagdollOverrides.TryGetValue( body.Bone, out var overrideMode ) ? overrideMode : Mode;
			if ( effectiveMode == RagdollMode.Active )
				MoveBodyFromAnimation( body );
		}

		foreach ( var joint in Joints )
		{
			var effectiveMode = _partialRagdollOverrides.TryGetValue( joint.Body2.Bone, out var overrideMode ) ? overrideMode : Mode;
			if ( effectiveMode == RagdollMode.Motor )
				MoveJointFromAnimation( joint, MotorFrequency, MotorDamping );
		}
	}

	private void FollowRoot()
	{
		if ( !FollowRootPosition && !FollowRootRotation || !Renderer.IsValid() ) return;
		if ( Mode == RagdollMode.None || Mode == RagdollMode.Passive || Mode == RagdollMode.Active ) return;

		var targetTransform = GetRagdollTransform( 0 ); // Follow root bone

		if ( FollowRootPosition )
			Renderer.WorldPosition = targetTransform.Position;
		if ( FollowRootRotation )
			Renderer.WorldRotation = targetTransform.Rotation;
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		ApplyRagdollMode( Mode );
		Renderer.ClearPhysicsBones();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		ModelPhysics?.Enabled = false;
		Renderer.ClearPhysicsBones();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ModelPhysics?.Destroy();
	}

	protected override void OnStart()
	{
		base.OnStart();
		SetupRagdoll();
		ApplyRagdollMode();
		ApplyPartialRagdollConfig();
	}

	protected void SetupRagdoll()
	{
		EnsureModelPhysics();
		SetupPhysics();
	}

	public void DisableJoints()
	{
		foreach ( var joint in Joints )
			joint.Component.Enabled = false;
	}

	public void EnableJoints()
	{
		foreach ( var joint in Joints )
			joint.Component.Enabled = true;
	}
}
