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

	/// <summary>
	/// Returns the signed angle in degrees that <paramref name="rot"/> rotates around <paramref name="axis"/>.
	/// Picks a reference vector perpendicular to the axis, rotates it, projects onto the plane, then atan2s the result.
	/// </summary>
	public static float GetSignedAngleAroundAxis( Rotation rot, Vector3 axis )
	{
		// I really don't understand this math, but my implementation was so long and crap I asked an AI to fix it and I guess this is what it's meant to look?
		axis = axis.Normal;

		// Pick a stable reference vector perpendicular to the axis
		var reference = MathF.Abs( Vector3.Dot( axis, Vector3.Up ) ) < 0.99f
			? Vector3.Cross( axis, Vector3.Up ).Normal
			: Vector3.Cross( axis, Vector3.Right ).Normal;

		// Rotate the reference, then flatten it back onto the axis-perpendicular plane
		var rotated = rot * reference;
		rotated = (rotated - axis * Vector3.Dot( rotated, axis )).Normal;

		return MathF.Atan2( Vector3.Dot( Vector3.Cross( reference, rotated ), axis ),
							Vector3.Dot( reference, rotated ) ) * (180f / MathF.PI);
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

	/// <summary>
	/// Returns the given bone and all of its descendants in the skeleton
	/// </summary>
	/// <param name="rootBone">The root bone</param>
	public IEnumerable<BoneCollection.Bone> GetDescendantBones( BoneCollection.Bone rootBone )
	{
		var included = new HashSet<int>() { rootBone.Index };

		foreach ( var bone in Renderer.Model.Bones.AllBones )
		{
			if ( bone.Parent != null && included.Contains( bone.Parent.Index ) )
				included.Add( bone.Index );

			if ( included.Contains( bone.Index ) )
				yield return bone;
		}
	}

	/// <summary>
	/// Returns the given bone and all of its descendants in the skeleton
	/// </summary>
	/// <param name="boneName">The root bone</param>
	public IEnumerable<BoneCollection.Bone> GetDescendantBones( string boneName )
		=> GetDescendantBones( Renderer?.Model?.Bones?.GetBone( boneName ) );

	/// <summary>
	/// Returns the given bone and all of its descendants in the skeleton
	/// </summary>
	/// <param name="boneIndex">The root bone</param>
	public IEnumerable<BoneCollection.Bone> GetDescendantBones( int boneIndex )
		=> GetDescendantBones( Renderer?.Model?.Bones?.AllBones[boneIndex] );

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
