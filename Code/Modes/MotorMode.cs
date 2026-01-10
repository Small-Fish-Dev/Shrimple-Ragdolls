namespace ShrimpleRagdolls;

public class MotorMode : IShrimpleRagdollMode<MotorMode>
{
	public static string Name => "Motor";
	public static string Description => "A ragdoll driven by the joint's motors, each joint tries following their parent's animation rotation.";
	public static bool PhysicsDriven => true;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		EnabledMode.OnEnter( ragdoll, body );

		var joint = body.GetParentJoint()?.Component;
		if ( !joint.IsValid() ) return;

		if ( joint is BallJoint ballJoint )
		{
			ballJoint.Motor = BallJoint.MotorMode.TargetRotation;
			ballJoint.Frequency = 30f;
			ballJoint.DampingRatio = 1f;
		}
		else if ( joint is HingeJoint hingeJoint )
		{
			hingeJoint.Motor = HingeJoint.MotorMode.TargetAngle;
			hingeJoint.Frequency = 30f;
			hingeJoint.DampingRatio = 1f;
		}
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		var joint = body.GetParentJoint();

		if ( joint != null && joint.Value.Component.IsValid() )
		{
			ragdoll.ResetJointSettings( joint.Value );
			if ( joint?.Component is BallJoint ballJoint )
			{
				ballJoint.Motor = BallJoint.MotorMode.Disabled;
				ballJoint.Frequency = 0f;
			}
			else if ( joint?.Component is HingeJoint hingeJoint )
			{
				hingeJoint.Motor = HingeJoint.MotorMode.Disabled;
				hingeJoint.Frequency = 0f;
			}
		}

		EnabledMode.OnExit( ragdoll, body );
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		var joint = body.GetParentJoint()?.Component;
		if ( !joint.IsValid() ) return;

		var parent = body.GetParent();
		if ( parent == null ) return;

		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( body.GetBone(), out var animSelfTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( parent.Value.GetBone(), out var animParentTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransformLocal( body.GetBone(), out var currentSelfTransformLocal ) )
			return;

		var animRotation = animParentTransform.ToLocal( animSelfTransform ).Rotation;
		var currentRotation = currentSelfTransformLocal.Rotation;

		if ( joint is BallJoint ballJoint )
		{
			var targetJointRotation = joint.Point1.LocalRotation.Inverse * animRotation * joint.Point2.LocalRotation;
			ballJoint.TargetRotation = targetJointRotation;
		}

		if ( joint is HingeJoint hingeJoint )
		{
			var targetJointRot = joint.Point1.LocalRotation.Inverse * animRotation * joint.Point2.LocalRotation;
			var targetAngle = ShrimpleRagdoll.GetSignedAngleAroundAxis( targetJointRot, hingeJoint.Axis );

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
