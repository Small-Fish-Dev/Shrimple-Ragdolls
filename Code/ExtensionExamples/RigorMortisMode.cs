public class RigorMortisMode : IShrimpleRagdollMode<RigorMortisMode>
{
	public static string Name => "Rigor Mortis";
	public static string Description => "The ragdoll's joints will lock up and make it hard to move from the initial position.";
	public static bool PhysicsDriven => true;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		EnabledMode.OnEnter( ragdoll, body );

		var joint = body.GetParentJoint()?.Component;
		if ( !joint.IsValid() ) return;

		var parent = body.GetParentBody();
		if ( parent == null ) return;

		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( body.GetBone(), out var animSelfTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( parent.Value.GetBone(), out var animParentTransform ) )
			return;

		var animRotation = animParentTransform.ToLocal( animSelfTransform ).Rotation;
		var currentJointRot = joint.Point1.LocalRotation.Inverse * animRotation * joint.Point2.LocalRotation;

		if ( joint is BallJoint ballJoint )
		{
			ballJoint.Motor = BallJoint.MotorMode.TargetRotation;
			ballJoint.Frequency = 100f;
			ballJoint.DampingRatio = 3f;
			ballJoint.TargetRotation = currentJointRot;
		}

		if ( joint is HingeJoint hingeJoint )
		{
			hingeJoint.Motor = HingeJoint.MotorMode.TargetAngle;
			hingeJoint.Frequency = 100f;
			hingeJoint.DampingRatio = 3f;

			var currentAngle = ShrimpleRagdoll.GetSignedAngleAroundAxis( currentJointRot, hingeJoint.Axis );
			hingeJoint.TargetAngle = currentAngle;
		}
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		MotorMode.OnExit( ragdoll, body ); // Behaviour for exiting is just the same as motor mode
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		MotorMode.VisualUpdate( ragdoll, body );
	}

	static RigorMortisMode()
	{
		ShrimpleRagdollModeRegistry.Register<RigorMortisMode>();
	}
}

public static class RigorMortisExtension
{
	// This is an example if you want to add your own
	extension( ShrimpleRagdollMode target )
	{
		public static string RigorMortis => "Rigor Mortis";
	}
}
