public class MotorMode : IShrimpleRagdollMode<MotorMode>
{
	public static string Name => "Motor";
	public static string Description => "A ragdoll driven by the joint's motors, each joint tries following their parent's animation rotation";
	public static bool PhysicsDriven => true;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		EnabledMode.OnEnter( ragdoll, body );

		var joint = ragdoll.GetParentJoint( body )?.Component ?? null;
		if ( !joint.IsValid() ) return;

		if ( joint is BallJoint ballJoint )
			ballJoint.Motor = BallJoint.MotorMode.TargetRotation;
		else if ( joint is HingeJoint hingeJoint )
			hingeJoint.Motor = HingeJoint.MotorMode.TargetAngle;
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		var joint = ragdoll.GetParentJoint( body )?.Component ?? null;
		if ( !joint.IsValid() ) return;

		if ( joint is BallJoint ballJoint )
			ballJoint.Motor = BallJoint.MotorMode.Disabled;
		else if ( joint is HingeJoint hingeJoint )
			hingeJoint.Motor = HingeJoint.MotorMode.Disabled;

		EnabledMode.OnExit( ragdoll, body );
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		var joint = ragdoll.GetParentJoint( body )?.Component;
		if ( !joint.IsValid() ) return;

		var parent = ragdoll.GetParentBody( body ).Value;

		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( ragdoll.GetBoneByBody( body ), out var animSelfTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( ragdoll.GetBoneByBody( parent ), out var animParentTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransformLocal( ragdoll.GetBoneByBody( body ), out var currentSelfTransformLocal ) )
			return;

		var animRotation = animParentTransform.ToLocal( animSelfTransform ).Rotation;
		var currentRotation = currentSelfTransformLocal.Rotation;

		//ragdoll.DebugOverlay.Sphere( new Sphere( animSelfTransform.Position, 3f ), Color.Red, Time.Delta );

		// Ball joint logic
		if ( joint is BallJoint ballJoint )
		{
			var targetJointRotation = joint.Point1.LocalRotation.Inverse * animRotation * joint.Point2.LocalRotation; // The order we multiple for is important, from right to left we start with the child's point of reference
			var currentJointRotation = joint.Point1.LocalRotation.Inverse * currentRotation * joint.Point2.LocalRotation;

			ballJoint.Frequency = 30f; // TODO: Make this configurable
			ballJoint.DampingRatio = 1f;
			ballJoint.TargetRotation = targetJointRotation;
		}

		// Hinge joint logic
		if ( joint is HingeJoint hingeJoint )
		{
			var currentJointRot = joint.Point1.LocalRotation.Inverse * currentRotation * joint.Point2.LocalRotation;
			var targetJointRot = joint.Point1.LocalRotation.Inverse * animRotation * joint.Point2.LocalRotation;

			var currentAngle = ShrimpleRagdoll.GetSignedAngleAroundAxis( currentJointRot, hingeJoint.Axis );
			var targetAngle = ShrimpleRagdoll.GetSignedAngleAroundAxis( targetJointRot, hingeJoint.Axis );

			hingeJoint.Frequency = 30f;
			hingeJoint.DampingRatio = 1f;
			hingeJoint.TargetAngle = targetAngle;
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
