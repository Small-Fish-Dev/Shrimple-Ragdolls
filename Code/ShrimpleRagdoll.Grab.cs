namespace ShrimpleRagdolls;

/// <summary>
/// A hold on one of a ragdoll's bodies. Feed it a target every frame with <see cref="MoveTo(Vector3)"/> and let go with <see cref="Release"/>.<br />
/// It springs at the grabbed point capped at <see cref="MaxForce"/>, and capping a force rather than an acceleration is what makes their weight matter.
/// </summary>
public sealed class RagdollGrab
{
	/// <summary>
	/// The ragdoll being held
	/// </summary>
	public ShrimpleRagdoll Ragdoll { get; internal set; }

	/// <summary>
	/// Bone index of the body being held
	/// </summary>
	public int Bone { get; internal set; }

	/// <summary>
	/// The rigidbody being held
	/// </summary>
	public Rigidbody Body { get; internal set; }

	/// <summary>
	/// Where on the body it's held, in the body's local space
	/// </summary>
	public Vector3 LocalAnchor { get; set; }

	/// <summary>
	/// Where the grab wants the held point. Set this every frame from your hand
	/// </summary>
	public Transform Target { get; set; }

	/// <summary>
	/// Also hold the body's orientation, which is what stops them punching with an arm you're holding
	/// </summary>
	public bool ControlRotation { get; set; }

	/// <summary>
	/// The most force the grab can pull with, set from carryMass times gravity
	/// </summary>
	public float MaxForce { get; set; } = 200f * 800f;

	/// <summary>
	/// The most the grab can angularly accelerate the body, in radians per second squared<br />
	/// Keep it modest, a saturated angular correction can't damp itself and spins
	/// </summary>
	public float MaxAngularAcceleration { get; set; } = 40f;

	/// <summary>
	/// Spring frequency of the hold in hz, higher is a tighter grip
	/// </summary>
	public float Frequency { get; set; } = 8f;

	/// <summary>
	/// Damping ratio of the hold, 1 is critically damped
	/// </summary>
	public float Damping { get; set; } = 1f;

	/// <summary>
	/// How limp the held limb goes, 1 means it stops fighting you entirely
	/// </summary>
	public float Limpness { get; set; } = 1f;

	/// <summary>
	/// How much limpness carries into each neighbouring bone, so an arm hangs off a held wrist
	/// </summary>
	public float ChainFalloff { get; set; } = 0.45f;

	/// <summary>
	/// How many joints out the limpness spreads, negative for the whole skeleton
	/// </summary>
	public int ChainDepth { get; set; } = 2;

	/// <summary>
	/// Also go limp down the chain from the held bone, not just up it
	/// </summary>
	public bool LimpChildren { get; set; } = true;

	/// <summary>
	/// The grab breaks if the held point gets this far from <see cref="Target"/>, 0 to never break<br />
	/// Be generous, a hauled limb legitimately trails a long way behind the hand
	/// </summary>
	public float BreakDistance { get; set; }

	/// <summary>
	/// How hard they've fought the grab, breaks free at 1
	/// </summary>
	public float Struggle { get; internal set; }

	/// <summary>
	/// The force the grab pulled with last tick, negated, so a heavy victim can drag the grabber around
	/// </summary>
	public Vector3 ReactionForce { get; internal set; }

	/// <summary>
	/// True once the grab has ended, whether it was let go or broken out of
	/// </summary>
	public bool Released { get; internal set; }

	/// <summary>
	/// True while the grab is still holding a valid body
	/// </summary>
	public bool IsHolding => !Released && Body.IsValid();

	/// <summary>
	/// The held point in world space
	/// </summary>
	public Vector3 WorldAnchor => Body.IsValid() ? Body.WorldTransform.PointToWorld( LocalAnchor ) : Target.Position;

	/// <summary>
	/// Move the point the grab pulls toward, keeping the current target rotation
	/// </summary>
	public void MoveTo( Vector3 position ) => Target = Target.WithPosition( position );

	/// <summary>
	/// Move the transform the grab pulls toward
	/// </summary>
	public void MoveTo( Transform target ) => Target = target;

	/// <summary>
	/// Fight the grab, breaking it once the accumulated struggle reaches 1
	/// </summary>
	public void ApplyStruggle( float amount ) => Struggle = Math.Clamp( Struggle + amount, 0f, 1f );

	/// <summary>
	/// Let go
	/// </summary>
	public void Release() => Ragdoll?.ReleaseGrab( this );

	// Which bodies this grab makes limp and by how much, walked once and reused
	internal List<(int Index, float Strength)> ChainCache;
	internal float CachedLimpness = float.NaN;
	internal float CachedFalloff;
	internal int CachedDepth;
	internal bool CachedChildren;

	internal bool ChainCacheIsStale => ChainCache == null
		|| CachedLimpness != Limpness
		|| CachedFalloff != ChainFalloff
		|| CachedDepth != ChainDepth
		|| CachedChildren != LimpChildren;
}

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// How much of their anchoring to the animated position they lose while being hauled<br />
	/// Without it, lifting someone means beating every body's drive at once, several times their bodyweight
	/// </summary>
	[Advanced, Property, Group( "Driven" ), Range( 0f, 1f ), Step( 0.05f )]
	public float HaulLimpness { get; set; } = 0.95f;

	/// <summary>
	/// How much of their articulation they lose while being hauled, kept low so they still struggle
	/// </summary>
	[Advanced, Property, Group( "Driven" ), Range( 0f, 1f ), Step( 0.05f )]
	public float HaulMotorLimpness { get; set; } = 0.35f;

	/// <summary>
	/// How fast a held ragdoll works itself free, in struggle per second at full strength. Scaled by
	/// <see cref="StrengthMultiplier"/> so a stunned npc can't wriggle out. 0 to disable
	/// </summary>
	[Advanced, Property, Group( "Driven" )]
	public float StruggleRate { get; set; }

	/// <summary>
	/// How much of their own bodyweight is currently being pulled by grabs, 0 to 1<br />
	/// Multiply your npc's locomotion by 1 minus this and being held stops them walking off
	/// </summary>
	public float HaulFraction { get; private set; }

	// Grip stretch, not their displacement. Force alone misses a light limb that follows your hand without
	// loading the spring, and displacement can never start a haul near the root
	private const float HaulStretchDistance = 16f;
	private const float HaulBlendTime = 0.25f;

	/// <summary>
	/// Every grab currently holding this ragdoll
	/// </summary>
	public IReadOnlyList<RagdollGrab> Grabs => _grabs;
	private readonly List<RagdollGrab> _grabs = new();

	/// <summary>
	/// Fires when something takes hold of this ragdoll
	/// </summary>
	public Action<RagdollGrab> Grabbed { get; set; }

	/// <summary>
	/// Fires when a grab ends. Check <see cref="RagdollGrab.Struggle"/> to tell a break from a let go
	/// </summary>
	public Action<RagdollGrab> GrabReleased { get; set; }

	/// <summary>
	/// True if anything is holding this ragdoll, synced so proxies can react too
	/// </summary>
	[Sync]
	public bool IsBeingGrabbed { get; private set; }

	/// <summary>
	/// True if this bone is being held
	/// </summary>
	public bool IsBoneGrabbed( int boneIndex )
	{
		foreach ( var grab in _grabs )
		{
			if ( grab.Bone == boneIndex )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Take hold of one of this ragdoll's bodies, owner only<br />
	/// Take ownership first if the grabber should be allowed to grab a ragdoll it doesn't own<br />
	/// <paramref name="carryMass"/> is how much mass this grip can hold up against gravity
	/// </summary>
	public RagdollGrab GrabBody( ModelPhysics.Body body, Vector3 worldPoint, float carryMass = 200f )
	{
		if ( IsProxy || !body.Component.IsValid() || !body.Component.PhysicsBody.IsValid() || !EnsureDrivenBuffers() )
			return null;

		var grab = new RagdollGrab
		{
			Ragdoll = this,
			Bone = body.Bone,
			Body = body.Component,
			LocalAnchor = body.Component.WorldTransform.PointToLocal( worldPoint ),
			Target = new Transform( worldPoint, body.Component.WorldRotation ),
			MaxForce = carryMass * Scene.PhysicsWorld.Gravity.Length,
		};

		_grabs.Add( grab );
		WakePhysics();
		Grabbed?.Invoke( grab );

		return grab;
	}

	/// <summary>
	/// Take hold of a bone by name
	/// </summary>
	public RagdollGrab GrabBone( string boneName, Vector3 worldPoint, float carryMass = 200f )
	{
		var body = GetBodyByBoneName( boneName );
		return body.HasValue ? GrabBody( body.Value, worldPoint, carryMass ) : null;
	}

	/// <summary>
	/// Take hold of whichever body is closest to a world point
	/// </summary>
	/// <param name="worldPoint">Where you grabbed</param>
	/// <param name="radius">How far the nearest body may be, 0 for no limit</param>
	/// <param name="carryMass">How much mass this grip can hold up against gravity</param>
	public RagdollGrab GrabNearest( Vector3 worldPoint, float radius = 24f, float carryMass = 200f )
	{
		var body = GetNearestBody( worldPoint, out var distance );

		if ( body is not { } target || (radius > 0f && distance > radius) )
			return null;

		return GrabBody( target, worldPoint, carryMass );
	}

	/// <summary>
	/// Let go of a grab, easing the limb's strength back
	/// </summary>
	public void ReleaseGrab( RagdollGrab grab )
	{
		if ( grab == null || grab.Released )
			return;

		grab.Released = true;
		_grabs.Remove( grab );
		GrabReleased?.Invoke( grab );
	}

	/// <summary>
	/// Let go of everything holding this ragdoll
	/// </summary>
	public void ReleaseAllGrabs()
	{
		for ( var i = _grabs.Count - 1; i >= 0; i-- )
			ReleaseGrab( _grabs[i] );
	}

	/// <summary>
	/// Drop any grabs we're still holding after losing ownership. They'd never update again, so they would
	/// never release either, and the limb would sit limp forever
	/// </summary>
	internal void ReleaseGrabsIfProxy()
	{
		if ( IsProxy && _grabs.Count > 0 )
			ReleaseAllGrabs();
	}

	private void UpdateGrabs()
	{
		// Rebuilt from scratch every tick, so letting go recovers on its own
		for ( var i = 0; i < _grabLimpTarget.Length; i++ )
			_grabLimpTarget[i] = 1f;

		// Cheap to assign every tick, a sync setter short circuits when the value hasn't changed
		IsBeingGrabbed = _grabs.Count > 0;

		if ( _grabs.Count == 0 )
		{
			HaulFraction = 0f;
			return;
		}

		UpdateHaul();

		var delta = Time.Delta;
		var struggle = StruggleRate * StrengthMultiplier * delta;

		for ( var i = _grabs.Count - 1; i >= 0; i-- )
		{
			var grab = _grabs[i];

			if ( !grab.IsHolding )
			{
				ReleaseGrab( grab );
				continue;
			}

			if ( struggle > 0f )
				grab.ApplyStruggle( struggle );

			var anchor = grab.WorldAnchor;
			var slipped = grab.BreakDistance > 0f
				&& Vector3.DistanceBetween( anchor, grab.Target.Position ) > grab.BreakDistance;

			if ( grab.Struggle >= 1f || slipped )
			{
				ReleaseGrab( grab );
				continue;
			}

			ApplyGrabLimpness( grab );
			ApplyGrabForce( grab, anchor, delta );
		}
	}

	/// <summary>
	/// Works out how much of their weight the grabs are taking and drops their anchoring to match, so
	/// lifting someone stops being a fight against every limb's drive at once
	/// </summary>
	private void UpdateHaul()
	{
		var weight = Mass * Scene.PhysicsWorld.Gravity.Length;
		var pull = 0f;
		var stretch = 0f;

		foreach ( var grab in _grabs )
		{
			if ( !grab.IsHolding )
				continue;

			pull += grab.ReactionForce.Length;
			stretch = MathF.Max( stretch, Vector3.DistanceBetween( grab.WorldAnchor, grab.Target.Position ) );
		}

		var byForce = weight > 0f ? pull / weight : 0f;
		var target = Math.Clamp( MathF.Max( byForce, stretch / HaulStretchDistance ), 0f, 1f );

		HaulFraction = HaulFraction.Approach( target, Time.Delta / HaulBlendTime );
	}

	/// <summary>
	/// Writes a grab's limpness into the grab strength layer, taking the limpest value where grabs overlap
	/// </summary>
	private void ApplyGrabLimpness( RagdollGrab grab )
	{
		if ( grab.ChainCacheIsStale )
		{
			grab.ChainCache ??= new List<(int, float)>();
			grab.ChainCache.Clear();
			grab.ChainCache.AddRange( EnumerateChain( grab.Bone, 1f - Math.Clamp( grab.Limpness, 0f, 1f ),
				grab.ChainFalloff, grab.ChainDepth, grab.LimpChildren ) );

			grab.CachedLimpness = grab.Limpness;
			grab.CachedFalloff = grab.ChainFalloff;
			grab.CachedDepth = grab.ChainDepth;
			grab.CachedChildren = grab.LimpChildren;
		}

		foreach ( var (index, chainStrength) in grab.ChainCache )
			_grabLimpTarget[index] = MathF.Min( _grabLimpTarget[index], chainStrength );
	}

	private void ApplyGrabForce( RagdollGrab grab, Vector3 anchor, float delta )
	{
		var rigidbody = grab.Body;
		var physics = rigidbody.PhysicsBody;

		if ( !physics.IsValid() )
			return;

		rigidbody.Sleeping = false;

		if ( grab.Frequency > 0f && grab.MaxForce > 0f )
		{
			var velocity = physics.GetVelocityAtPoint( anchor );
			var acceleration = SpringAcceleration( grab.Target.Position - anchor, velocity, grab.Frequency, grab.Damping, delta );
			var force = (acceleration * physics.Mass).ClampLength( grab.MaxForce );

			// At the grabbed point rather than the mass centre, so the limb swings off the grip
			physics.ApplyForceAt( anchor, force );
			grab.ReactionForce = -force;
		}
		else
		{
			grab.ReactionForce = Vector3.Zero;
		}

		if ( !grab.ControlRotation || grab.MaxAngularAcceleration <= 0f )
			return;

		var rotationError = ToRotationVector( grab.Target.Rotation * rigidbody.WorldRotation.Inverse );
		var angular = SpringAcceleration( rotationError, rigidbody.AngularVelocity, grab.Frequency, grab.Damping, delta );

		rigidbody.AngularVelocity += angular.ClampLength( grab.MaxAngularAcceleration ) * delta;
	}
}
