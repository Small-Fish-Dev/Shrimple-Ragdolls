public class MotorMode : IShrimpleRagdollMode<MotorMode>
{
	public static string Name => "Motor";
	public static string Description => "A ragdoll driven by the joint's motors";
	public static bool PhysicsDriven => true;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		EnabledMode.OnEnter( ragdoll, body );
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		EnabledMode.OnExit( ragdoll, body );
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		var joint = ragdoll.GetParentJoint( body )?.Component ?? null;
		if ( !joint.IsValid() ) return;

		if ( joint is BallJoint ballJoint )
		{
			ballJoint.Motor = BallJoint.MotorMode.TargetRotation;
			ballJoint.Frequency = 100f;
			ballJoint.TargetRotation = Rotation.FromPitch( MathF.Cos( Time.Now * 10f ) * 70f );
		}
		if ( joint is HingeJoint hingeJoint )
		{
			hingeJoint.Motor = HingeJoint.MotorMode.TargetAngle;
			hingeJoint.Frequency = 10f;
			hingeJoint.TargetAngle = MathF.Cos( Time.Now * 5f ) * 20f;
		}
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		EnabledMode.VisualUpdate( ragdoll, body );
	}

	static MotorMode()
	{
		ShrimpleRagdollModeRegistry.Register<MotorMode>();
	}
}
