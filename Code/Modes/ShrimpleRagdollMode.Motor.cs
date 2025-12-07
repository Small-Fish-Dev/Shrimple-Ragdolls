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
		var joint = ragdoll.GetParentJoint( body )?.Component ?? null;
		if ( !joint.IsValid() ) return;

		var parent = ragdoll.GetParentBody( body ).Value;

		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( ragdoll.GetBoneByBody( body ), out var animSelfTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransformAnimation( ragdoll.GetBoneByBody( parent ), out var animParentTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransform( ragdoll.GetBoneByBody( body ), out var currentSelfTransform ) )
			return;
		if ( !ragdoll.Renderer.TryGetBoneTransform( ragdoll.GetBoneByBody( parent ), out var currentParentTransform ) )
			return;

		var currentRotation = currentParentTransform.ToLocal( currentSelfTransform ).Rotation; // The rotation our bones are right now
		var animRotation = animParentTransform.ToLocal( animSelfTransform ).Rotation; // The rotation our animations want to be

		//Log.Info( currentRotation.Angles() );
		//Log.Info( targetJointRotation.Angles() );

		if ( joint is BallJoint ballJoint )
		{/*
			var currentJointRotation = ballJoint.LocalRotation * ballJoint.Point1.LocalRotation * ballJoint.Point2.LocalRotation.Inverse; // The rotation our joint is right now
			var targetJointRotation = ballJoint.TargetRotation * ballJoint.Point1.LocalRotation * ballJoint.Point2.LocalRotation.Inverse; // The rotation our joint wants to be
			var rotationDifference = Rotation.Difference( currentJointRotation, targetJointRotation ); // how far off we are
			ballJoint.Frequency = 2f;
			ballJoint.DampingRatio = 1f;
			ballJoint.TargetRotation = rotationDifference;
			Log.Info( currentJointRotation.Angles() + " " + targetJointRotation.Angles() + " " + rotationDifference.Angles() );*/
		}
		if ( joint is HingeJoint hingeJoint )
		{
			var currentJointRotation = Rotation.FromAxis( hingeJoint.Axis, hingeJoint.Angle ) * hingeJoint.Point1.LocalRotation * hingeJoint.Point2.LocalRotation.Inverse; // The rotation our joint is right now
			var targetJointRotation = Rotation.FromAxis( hingeJoint.Axis, hingeJoint.TargetAngle ) * hingeJoint.Point1.LocalRotation * hingeJoint.Point2.LocalRotation.Inverse; // The rotation our joint wants to be

			var rotationDifference = Rotation.Difference( currentRotation, animRotation ); // how far off we are

			hingeJoint.Frequency = 100f;
			hingeJoint.DampingRatio = 1f;
			hingeJoint.TargetAngle = 0f;
			Log.Info( body.GetBone( ragdoll.Model ).Name );
			Log.Info( rotationDifference.Angles() );
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
