namespace ShrimpleRagdolls;

/// <summary>
/// Hold attack1 to drag physics objects around, attack2 to punch. Grabbing a <see cref="ShrimpleRagdoll"/>
/// in <see cref="RagdollMode.Driven"/> goes through its grab api so the limb goes limp and the body follows,
/// anything else gets a plain spring.
/// </summary>
public class PhysicsGrabber : Component
{
	[Property] public float SpringStrength { get; set; } = 150f;
	[Property] public float Damping { get; set; } = 10f;
	[Property] public float MaxForceMultiplier { get; set; } = 100f;

	/// <summary>
	/// How much mass this hand can hold up against a ragdoll's weight<br />
	/// Below their total mass you can only steer a limb, above it you can pick them up and swing them
	/// </summary>
	[Property, Group( "Ragdoll" )] public float CarryMass { get; set; } = 220f;

	/// <summary>
	/// Also hold the grabbed limb's orientation, which stops them swinging the arm you're holding
	/// </summary>
	[Property, Group( "Ragdoll" )] public bool ControlRotation { get; set; } = true;

	/// <summary>
	/// How fast a punch sends the limb it lands on. A speed rather than a force, so a hit lands the same
	/// whether you catch a wrist or the chest
	/// </summary>
	[Property, Group( "Punching" )] public float PunchSpeed { get; set; } = 1000f;

	[Property, Group( "Punching" )] public float PunchReach { get; set; } = 90f;

	/// <summary>
	/// How limp a punched limb goes, so the blow travels instead of being shrugged off by the drive
	/// </summary>
	[Property, Group( "Punching" ), Range( 0f, 1f ), Step( 0.05f )]
	public float PunchLimpness { get; set; } = 0.85f;

	[Property, Group( "Punching" )] public float PunchRecoverTime { get; set; } = 0.4f;
	[Property, Group( "Punching" )] public float PunchCooldown { get; set; } = 0.4f;

	/// <summary>
	/// Draw a dot where you're aiming, which sticks to the grabbed point once you have hold of something
	/// </summary>
	[Property] public bool ShowCrosshair { get; set; } = true;

	/// <summary>
	/// The ragdoll grab currently being held, if you grabbed a ragdoll
	/// </summary>
	public RagdollGrab Grab { get; private set; }

	private PhysicsBody GrabbedBody;
	private Vector3 GrabbedBodyLocal;
	private float GrabDistance;
	private Rotation GrabRotation = Rotation.Identity;
	private TimeSince _lastPunch;

	protected override void OnDisabled() => Clear();

	protected override void OnDestroy() => Clear();

	private void Clear()
	{
		Grab?.Release();
		Grab = null;
		GrabbedBody = null;
		GrabbedBodyLocal = default;
		GrabDistance = 0f;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( Input.Pressed( "attack2" ) && _lastPunch > PunchCooldown )
			Punch();

		if ( Input.Down( "attack1" ) )
			TryGrab();
		else
			Clear();

		if ( ShowCrosshair )
			DrawCrosshair();
	}

	private void TryGrab()
	{
		// They struggled free, or something pulled them out of reach
		if ( Grab is { IsHolding: false } )
			Clear();

		if ( GrabbedBody.IsValid() || Grab != null )
			return;

		var trace = AimTrace( 1000f );

		if ( !trace.Hit || trace.Body is null || trace.Body.BodyType == PhysicsBodyType.Static )
			return;

		GrabDistance = trace.Distance;

		var ragdoll = trace.GameObject.IsValid()
			? trace.GameObject.Components.GetInAncestorsOrSelf<ShrimpleRagdoll>()
			: null;

		if ( ragdoll.IsValid() && ragdoll.GetBodyByPhysicsBody( trace.Body ) is { } body )
		{
			// Only the owner can drive a ragdoll's bodies, so take it over before grabbing. The request
			// may not land this frame, but TryGrab runs again every frame you hold the button
			if ( ragdoll.IsProxy )
			{
				ragdoll.Network.TakeOwnership();
				return;
			}

			Grab = ragdoll.GrabBody( body, trace.HitPosition, CarryMass );

			if ( Grab != null )
			{
				Grab.ControlRotation = ControlRotation;

				// Hold the orientation it had when you grabbed it, relative to your view. Targeting the raw
				// camera rotation starts with an arbitrary error and the grab spins correcting it
				GrabRotation = Scene.Camera.WorldRotation.Inverse * body.Component.WorldRotation;
				return;
			}
		}

		GrabbedBody = trace.Body;
		GrabbedBodyLocal = GrabbedBody.Transform.PointToLocal( trace.HitPosition );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Scene.Camera.IsValid() )
			return;

		var camera = Scene.Camera;
		var targetPosition = camera.WorldPosition + camera.WorldRotation.Forward * GrabDistance;

		if ( Grab is { IsHolding: true } )
		{
			Grab.MoveTo( new Transform( targetPosition, camera.WorldRotation * GrabRotation ) );
			return;
		}

		if ( !GrabbedBody.IsValid() )
			return;

		var currentPosition = GrabbedBody.Transform.PointToWorld( GrabbedBodyLocal );
		var displacement = targetPosition - currentPosition;
		var velocity = GrabbedBody.GetVelocityAtPoint( currentPosition );
		var force = (displacement * SpringStrength - velocity * Damping) * GrabbedBody.Mass;

		var maxForce = MaxForceMultiplier * GrabbedBody.Mass * Scene.PhysicsWorld.Gravity.Length;
		GrabbedBody.ApplyForceAt( currentPosition, force.ClampLength( maxForce ) );
	}

	private void Punch()
	{
		if ( !Scene.Camera.IsValid() )
			return;

		_lastPunch = 0f;

		var forward = Scene.Camera.WorldRotation.Forward;
		var trace = AimTrace( PunchReach );

		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return;

		var ragdoll = trace.GameObject.Components.GetInAncestorsOrSelf<ShrimpleRagdoll>();

		if ( !ragdoll.IsValid() )
			return;

		var body = ragdoll.GetBodyByPhysicsBody( trace.Body ) ?? ragdoll.GetNearestBody( trace.HitPosition );

		if ( body is not { } target || !target.Component.IsValid() || !target.Component.PhysicsBody.IsValid() )
			return;

		// Networked, so you can hit an npc the host is simulating without taking it over the way a grab has to
		ragdoll.NetworkRecoil( target, trace.HitPosition, forward * (PunchSpeed * target.Component.PhysicsBody.Mass),
			limpness: PunchLimpness, recoverTime: PunchRecoverTime );
	}

	/// <summary>
	/// Sticks to the grabbed point once you have hold of something, otherwise sits where you're aiming
	/// </summary>
	private void DrawCrosshair()
	{
		if ( !Scene.Camera.IsValid() )
			return;

		Vector3 point;

		if ( Grab is { IsHolding: true } )
			point = Grab.WorldAnchor;
		else if ( GrabbedBody.IsValid() )
			point = GrabbedBody.Transform.PointToWorld( GrabbedBodyLocal );
		else
		{
			var trace = AimTrace( 1000f );

			if ( !trace.Hit )
				return;

			point = trace.HitPosition;
		}

		Scene.DebugOverlay.Sphere( new Sphere( point, 1f ), Color.Cyan, 0f, global::Transform.Zero, true );
	}

	private SceneTraceResult AimTrace( float distance )
	{
		var camera = Scene.Camera;

		return Scene.Trace
			.Ray( camera.WorldPosition, camera.WorldPosition + camera.WorldRotation.Forward * distance )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();
	}
}
