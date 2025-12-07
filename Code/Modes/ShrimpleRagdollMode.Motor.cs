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
		if ( !ragdoll.Renderer.TryGetBoneTransformLocal( ragdoll.GetBoneByBody( body ), out var currentSelfTransformLocal ) )
			return;

		var animRotation = animParentTransform.ToLocal( animSelfTransform ).Rotation; // The rotation our animations want to be
		var currentRotation = currentSelfTransformLocal.Rotation; // The rotation our bones are right now

		ragdoll.DebugOverlay.Sphere( new Sphere( animSelfTransform.Position, 3f ), Color.Red, Time.Delta );
		//Log.Info( currentRotation.Angles() );
		//Log.Info( targetJointRotation.Angles() );

		if ( joint is BallJoint ballJoint )
		{
			var currentJointRotation = ballJoint.LocalRotation * ballJoint.Point1.LocalRotation * ballJoint.Point2.LocalRotation.Inverse; // The rotation our joint is right now
			var targetJointRotation = ballJoint.TargetRotation * ballJoint.Point1.LocalRotation * ballJoint.Point2.LocalRotation.Inverse; // The rotation our joint wants to be
			var rotationDifference = Rotation.Difference( currentJointRotation, targetJointRotation ); // how far off we are
			ballJoint.Frequency = 2f;
			ballJoint.DampingRatio = 1f;
			ballJoint.TargetRotation = rotationDifference;
			Log.Info( currentJointRotation.Angles() + " " + targetJointRotation.Angles() + " " + rotationDifference.Angles() );
		}
		if ( joint is HingeJoint hingeJoint )
		{
			var hingeJointRot = currentRotation * hingeJoint.Point1.LocalRotation.Inverse * hingeJoint.Point2.LocalRotation;
			var animationRot = animRotation * hingeJoint.Point1.LocalRotation.Inverse * hingeJoint.Point2.LocalRotation;

			var hingeAngle = ShrimpleRagdoll.GetSignedAngleAroundAxis( hingeJointRot, hingeJoint.Axis );
			var animationAngle = ShrimpleRagdoll.GetSignedAngleAroundAxis( animationRot, hingeJoint.Axis );

			hingeJoint.Frequency = 100f;
			hingeJoint.DampingRatio = 1f;
			hingeJoint.TargetAngle = animationAngle;

			//Log.Info( "----------" + body.GetBone( ragdoll.Model ).Name + "----------" );
			//Log.Info( hingeJoint.Point1.LocalRotation.Angles() + " | " + hingeJoint.Point2.LocalRotation.Angles() );
			//Log.Info( animRotation.Angles() + " | " + currentRotation.Angles() );
			//Log.Info( animationAngle + " | " + hingeAngle + " | " + hingeJoint.Angle );
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
