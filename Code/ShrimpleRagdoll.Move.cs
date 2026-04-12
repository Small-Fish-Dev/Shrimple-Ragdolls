namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Teleport all body GameObjects to their animation bone transforms
	/// </summary>
	public void MoveObjectsFromAnimations()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			var bone = Renderer.Model.Bones.AllBones[body.Bone];
			if ( !Renderer.TryGetBoneTransformAnimation( bone, out var targetTransform ) )
				continue;

			body.Component.WorldTransform = targetTransform;
		}
	}

	/// <summary>
	/// Kinematically move all rigidbodies toward their animation bone transforms
	/// </summary>
	public void MoveBodiesFromAnimations()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var body in Bodies )
			MoveBodyFromAnimation( body );
	}

	private void MoveBodyFromAnimation( ModelPhysics.Body body )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		if ( !body.Component.IsValid() || !body.Component.PhysicsBody.IsValid() )
			return;

		var bone = Renderer.Model.Bones.AllBones[body.Bone];
		if ( !Renderer.TryGetBoneTransformAnimation( bone, out var targetTransform ) )
			return;

		body.Component.PhysicsBody.SmoothMove( in targetTransform, MathF.Max( ActiveLerpTime, Time.Delta ), Time.Delta );
	}

	/// <summary>
	/// Enable motors on all joints with the given frequency and damping
	/// </summary>
	public void EnableJointMotors( float frequency = 30f, float dampingRatio = 1f )
	{
		foreach ( var joint in Joints )
		{
			if ( !joint.Component.IsValid() )
				continue;

			if ( joint.Component is BallJoint ballJoint )
			{
				ballJoint.Motor = BallJoint.MotorMode.TargetRotation;
				ballJoint.Frequency = frequency;
				ballJoint.DampingRatio = dampingRatio;
			}
			else if ( joint.Component is HingeJoint hingeJoint )
			{
				hingeJoint.Motor = HingeJoint.MotorMode.TargetAngle;
				hingeJoint.Frequency = frequency;
				hingeJoint.DampingRatio = dampingRatio;
			}
		}
	}

	/// <summary>
	/// Disable and reset motors on all joints
	/// </summary>
	public void DisableJointMotors()
	{
		foreach ( var joint in Joints )
		{
			if ( !joint.Component.IsValid() )
				continue;

			if ( joint.Component is BallJoint ballJoint )
			{
				ballJoint.Motor = BallJoint.MotorMode.Disabled;
				ballJoint.Frequency = 0f;
			}
			else if ( joint.Component is HingeJoint hingeJoint )
			{
				hingeJoint.Motor = HingeJoint.MotorMode.Disabled;
				hingeJoint.Frequency = 0f;
			}
		}
	}

	/// <summary>
	/// Drive every joint toward its animation pose using joint motors
	/// </summary>
	public void MoveJointsFromAnimations( float frequency = 30f, float dampingRatio = 1f )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var joint in Joints )
			MoveJointFromAnimation( joint, frequency, dampingRatio );
	}

	private void MoveJointFromAnimation( ModelPhysics.Joint joint, float frequency = 30f, float dampingRatio = 1f )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		if ( !joint.Component.IsValid() )
			return;

		var childBone = Renderer.Model.Bones.AllBones[joint.Body2.Bone];
		var parentBone = Renderer.Model.Bones.AllBones[joint.Body1.Bone];
		if ( childBone == null || parentBone == null )
			return;

		if ( !Renderer.TryGetBoneTransformAnimation( childBone, out var animChildTransform ) ||
			!Renderer.TryGetBoneTransformAnimation( parentBone, out var animParentTransform ) )
			return;

		var animRotation = animParentTransform.ToLocal( animChildTransform ).Rotation;

		if ( joint.Component is BallJoint ballJoint )
		{
			ballJoint.Motor = BallJoint.MotorMode.TargetRotation;
			ballJoint.Frequency = frequency;
			ballJoint.DampingRatio = dampingRatio;
			ballJoint.TargetRotation = joint.Component.Point1.LocalRotation.Inverse * animRotation * joint.Component.Point2.LocalRotation;
		}
		else if ( joint.Component is HingeJoint hingeJoint )
		{
			hingeJoint.Motor = HingeJoint.MotorMode.TargetAngle;
			hingeJoint.Frequency = frequency;
			hingeJoint.DampingRatio = dampingRatio;
			var targetJointRot = joint.Component.Point1.LocalRotation.Inverse * animRotation * joint.Component.Point2.LocalRotation;
			hingeJoint.TargetAngle = GetSignedAngleAroundAxis( targetJointRot, hingeJoint.Axis );
		}
	}
}
