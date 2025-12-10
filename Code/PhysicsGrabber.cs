[Hide]
public class PhysicsGrabber : Component
{
	private PhysicsBody GrabbedBody;
	private GameObject GrabbedObject;
	private Vector3 GrabbedBodyLocal;
	private Vector3 GrabbedObjectLocal;
	private float GrabDistance;

	[Property] public float SpringStrength { get; set; } = 150f;
	[Property] public float Damping { get; set; } = 10f;
	[Property] public float MaxForceMultiplier { get; set; } = 100f;

	protected override void OnDisabled()
	{
		base.OnDisabled();
		Clear();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Clear();
	}

	private void Clear()
	{
		GrabbedBody = null;
		GrabbedObject = null;
		GrabbedBodyLocal = default;
		GrabbedObjectLocal = default;
		GrabDistance = 0;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( GrabbedBody.IsValid() )
		{
			if ( !Input.Down( "attack1" ) )
			{
				Clear();
			}
			else
			{
				return;
			}
		}

		var tr = Scene.Trace.Ray( Scene.Camera.WorldPosition, Scene.Camera.WorldPosition + Scene.Camera.WorldRotation.Forward * 1000 )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		if ( !tr.Hit || tr.Body is null )
			return;

		if ( tr.Body.BodyType == PhysicsBodyType.Static )
			return;

		if ( Input.Down( "attack1" ) )
		{
			GrabbedBody = tr.Body;
			GrabbedObject = tr.GameObject;
			GrabbedBodyLocal = GrabbedBody.Transform.PointToLocal( tr.HitPosition );
			GrabbedObjectLocal = GrabbedObject.WorldTransform.PointToLocal( tr.HitPosition );
			GrabDistance = tr.Distance;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		if ( !GrabbedBody.IsValid() )
			return;

		var camera = Scene.Camera;
		var targetPosition = camera.WorldPosition + camera.WorldRotation.Forward * GrabDistance;
		var currentPosition = GrabbedBody.Transform.PointToWorld( GrabbedBodyLocal );

		var displacement = targetPosition - currentPosition;
		var velocity = GrabbedBody.GetVelocityAtPoint( currentPosition );

		var springForce = displacement * SpringStrength - velocity * Damping;
		var force = springForce * GrabbedBody.Mass;

		var maxForce = MaxForceMultiplier * GrabbedBody.Mass * Scene.PhysicsWorld.Gravity.Length;
		if ( force.Length > maxForce )
		{
			force = force.Normal * maxForce;
		}

		GrabbedBody.ApplyForceAt( currentPosition, force );
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( !GrabbedObject.IsValid() )
		{
			var tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), 1000.0f )
						.IgnoreGameObjectHierarchy( GameObject.Root )
						.Run();

			if ( tr.Hit )
			{
				Gizmo.Draw.Color = Color.Cyan;
				Gizmo.Draw.SolidSphere( tr.HitPosition, 1 );
			}
		}
		else
		{
			var position = GrabbedObject.WorldTransform.PointToWorld( GrabbedObjectLocal );

			Gizmo.Draw.Color = Color.Cyan;
			Gizmo.Draw.SolidSphere( position, 1 );
		}
	}
}
