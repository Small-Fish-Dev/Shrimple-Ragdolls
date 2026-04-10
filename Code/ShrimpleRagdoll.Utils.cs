namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Move the ragdoll without affecting its velocity or simulating collisions<br />
	/// </summary>
	/// <param name="target">The target transform, the entire ragdoll will be moved so that its root matches</param>
	public void Move( Transform target )
	{
		WakePhysics();

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			var targetTransform = target.ToWorld( Renderer.WorldTransform.ToLocal( body.Component.WorldTransform ) );
			body.Component.WorldTransform = targetTransform;
		}
	}

	/// <summary>
	/// Apply a velocity to the ragdoll as a whole rather than on every body individually
	/// </summary>
	/// <param name="velocity">The velocity applied</param>
	public void ApplyVelocity( Vector3 velocity )
	{
		WakePhysics();

		foreach ( var body in Bodies )
			if ( body.Component.IsValid() )
				body.Component.Velocity += velocity;
	}

	/// <summary>
	/// Apply an angular velocity to the ragdoll, spinning it around the mass center
	/// </summary>
	/// <param name="angularVelocity">The axis to spin around and speed in radians per second</param>
	public void ApplyAngularVelocity( Vector3 angularVelocity )
	{
		WakePhysics();

		var spinAxis = angularVelocity.Normal;
		var spinSpeed = angularVelocity.Length;
		var normalizedAngularVelocity = spinAxis * spinSpeed;
		var massCenter = GetMassCenter();

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			var bodyVelocity = Vector3.Cross( normalizedAngularVelocity, body.Component.WorldPosition - massCenter );
			body.Component.Velocity += bodyVelocity;
			body.Component.AngularVelocity += normalizedAngularVelocity;
		}
	}

	/// <summary>
	/// Apply a torque to the ragdoll, causing angular acceleration based on each body's inertia
	/// </summary>
	/// <param name="torque">The torque vector (axis and magnitude)</param>
	public void ApplyTorque( Vector3 torque )
	{
		WakePhysics();

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			body.Component.PhysicsBody.ApplyTorque( torque );
		}
	}

	/// <summary>
	/// Apply a force to the ragdoll, causing acceleration based on each body's mass
	/// </summary>
	/// <param name="force">The force vector</param>
	public void ApplyForce( Vector3 force )
	{
		WakePhysics();

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			body.Component.PhysicsBody.ApplyForce( force );
		}
	}

	/// <summary>
	/// Apply an impulse to the ragdoll, instantly changing velocity based on each body's mass
	/// </summary>
	/// <param name="impulse">The impulse vector</param>
	public void ApplyImpulse( Vector3 impulse )
	{
		WakePhysics();

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;

			body.Component.PhysicsBody.ApplyImpulse( impulse );
		}
	}

	/// <summary>
	/// Get a body by bone name
	/// </summary>
	public ModelPhysics.Body? GetBodyByBoneName( string boneName )
	{
		if ( !Renderer.IsValid() || !Renderer.Model.IsValid() )
			return null;

		return Bodies.FirstOrDefault( x => x.Bone == Renderer.Model.Bones.GetBone( boneName ).Index );
	}

	/// <summary>
	/// Get a body by bone index
	/// </summary>
	public ModelPhysics.Body? GetBodyByBoneIndex( int boneIndex )
	{
		if ( !Renderer.IsValid() || !Renderer.Model.IsValid() )
			return null;

		return Bodies[boneIndex];
	}

	/// <summary>
	/// Get a body by bone
	/// </summary>
	public ModelPhysics.Body? GetBodyByBone( BoneCollection.Bone bone )
	{
		if ( bone == null )
			return null;

		return Bodies.FirstOrDefault( x => x.Bone == bone.Index );
	}

	public static float GetSignedAngleAroundAxis( Rotation rel, Vector3 axis )
	{
		axis = axis.Normal;

		// Pick reference direction perpendicular to axis
		var refDir = Vector3.Cross( axis, Vector3.Up );
		if ( refDir.LengthSquared < 1e-4f )
			refDir = Vector3.Cross( axis, Vector3.Right );
		refDir = refDir.Normal;

		// Rotate reference by the relative rotation
		var rotatedDir = rel * refDir;

		// Project both onto plane perpendicular to axis
		refDir -= axis * Vector3.Dot( refDir, axis );
		rotatedDir -= axis * Vector3.Dot( rotatedDir, axis );

		// Safety check: ensure vectors aren't zero after projection
		var refLen = refDir.Length;
		var rotLen = rotatedDir.Length;
		if ( refLen < 1e-6f || rotLen < 1e-6f )
			return 0f; // No meaningful rotation in this plane

		refDir = refDir / refLen;
		rotatedDir = rotatedDir / rotLen;

		// Signed angle using atan2
		var cross = Vector3.Cross( refDir, rotatedDir );
		var dot = Vector3.Dot( refDir, rotatedDir );
		var angleRad = MathF.Atan2( Vector3.Dot( cross, axis ), dot );

		return angleRad * (180f / MathF.PI);
	}

	/// <summary>
	/// Get the ragdoll's ideal transform from the provided bone
	/// </summary>
	/// <param name="boneIndex">Which bone to base off of</param>
	/// <param name="mergedBoneTransforms">The final renderer's transform should match the bone's transform</param>
	/// <returns></returns>
	public Transform GetRagdollTransform( int boneIndex, bool mergedBoneTransforms = true )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return WorldTransform;

		var currentTransform = Bodies[boneIndex].Component.GameObject.WorldTransform;
		var targetTransform = currentTransform;

		if ( mergedBoneTransforms )
		{
			var localTransform = Renderer.Model.GetBoneTransform( boneIndex );
			var invRotation = localTransform.Rotation.Inverse;

			// Transform the bone's world transform back to root space
			var rotatedLocalPos = currentTransform.Rotation * (localTransform.Position * invRotation);
			targetTransform = new Transform(
				currentTransform.Position - rotatedLocalPos,
				currentTransform.Rotation * invRotation
			);
		}

		return targetTransform;
	}

	public void MultiplyJointLimits( float multiplier = 1f )
	{
		foreach ( var joint in Joints )
		{
			if ( joint.Component is BallJoint ballJoint )
			{
				ballJoint.SwingLimit *= multiplier;
				ballJoint.TwistLimit *= multiplier;
			}
			else if ( joint.Component is HingeJoint hingeJoint )
			{
				hingeJoint.MinAngle *= multiplier;
				hingeJoint.MaxAngle *= multiplier;
			}
		}

		_currentJointLimits *= multiplier;
	}
}
