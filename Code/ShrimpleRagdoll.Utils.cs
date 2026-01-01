namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Calculate the center of mass of the ragdoll in world space based on its bodies' masses and masscenters
	/// </summary>
	public Vector3 MassCenter => GetMassCenter();

	/// <summary>
	/// Calculate the center of mass of the ragdoll in world space based on its bodies' masses and masscenters
	/// </summary>
	/// <returns>World position of the combined center of mass</returns>
	public Vector3 GetMassCenter()
	{
		if ( Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			return rigidbody.WorldTransform.PointToWorld( rigidbody.MassCenter );

		var totalMass = 0f;
		var weightedCenter = Vector3.Zero;

		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() || !body.Component.Active )
				continue;

			var mass = body.Component.PhysicsBody.Mass;
			var worldMassCenter = body.Component.WorldTransform.PointToWorld( body.Component.MassCenter );
			weightedCenter += worldMassCenter * mass;
			totalMass += mass;
		}

		if ( totalMass <= 0f )
			return Renderer.IsValid() ? Renderer.WorldPosition : Vector3.Zero;

		return weightedCenter / totalMass;
	}

	/// <summary>
	/// Move the ragdoll without affecting its velocity or simulating collisions<br />
	/// </summary>
	/// <param name="target">The target transform, the entire ragdoll will be moved so that its root matches</param>
	public void Move( Transform target )
	{
		WakePhysics();

		foreach ( var bone in BoneObjects )
		{
			if ( bone.Value.Flags.Contains( GameObjectFlags.Absolute ) )
			{
				var targetTransform = target.ToWorld( Renderer.WorldTransform.ToLocal( bone.Value.WorldTransform ) );
				bone.Value.WorldTransform = targetTransform;
			}
			else if ( bone.Key.Index == 0 )
			{
				Renderer.WorldTransform = target;
			}
		}
	}

	/// <summary>
	/// Apply a velocity to the ragdoll as a whole rather than on every body individually
	/// </summary>
	/// <param name="velocity">The velocity applied</param>
	public void ApplyVelocity( Vector3 velocity )
	{
		WakePhysics();

		foreach ( var body in Bodies?.Values )
			body.Component?.Velocity += velocity;

		if ( Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.Velocity += velocity;
	}

	/// <summary>
	/// Apply a torque to the ragdoll as a whole rather than on every body individually
	/// </summary>
	/// <param name="torque">The axis to spin around and speed in radians per second</param>
	public void ApplyTorque( Vector3 torque )
	{
		WakePhysics();

		var spinAxis = torque.Normal;
		var spinSpeed = torque.Length; // radians per second
		var angularVelocity = spinAxis * spinSpeed;
		var massCenter = GetMassCenter();

		foreach ( var body in Bodies?.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			var bodyVelocity = Vector3.Cross( angularVelocity, body.Component.WorldPosition - massCenter );
			body.Component.Velocity += bodyVelocity;
			body.Component.AngularVelocity += angularVelocity;
		}

		if ( Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.AngularVelocity += angularVelocity;
	}

	public Body? GetBodyByBoneName( string boneName )
	{
		if ( !Renderer.IsValid() || !Renderer.Model.IsValid() )
			return null;

		var bone = Renderer.Model.Bones.GetBone( boneName );
		if ( bone == null )
			return null;

		if ( Bodies.TryGetValue( bone.Index, out var body ) )
			return body;
		return null;
	}

	public Body? GetBodyByBoneIndex( int boneIndex )
	{
		if ( Bodies.TryGetValue( boneIndex, out var body ) )
			return body;
		return null;
	}

	public Body? GetBodyByBone( BoneCollection.Bone bone )
	{
		if ( bone == null )
			return null;

		if ( Bodies.TryGetValue( bone.Index, out var body ) )
			return body;

		return null;
	}

	/// <summary>
	/// Finds the nearest ancestor bone that is associated with a valid body
	/// </summary>
	/// <returns>The nearest valid parent body associated with the specified bone, or null if no such body is found.</returns>
	public Body? GetNearestValidParentBody( BoneCollection.Bone bone )
	{
		while ( bone != null )
		{
			var parentBody = GetBodyByBone( bone );
			if ( parentBody != null )
				return parentBody;
			bone = bone.Parent;
		}
		return null;
	}

	/// <summary>
	/// Finds the nearest descendant bone that is associated with a valid body
	/// </summary>
	/// <returns>The nearest valid childn body associated with the specified bone, or null if no such body is found.</returns>
	public Body? GetNearestValidChildBody( BoneCollection.Bone bone )
	{
		if ( bone == null )
			return null;

		// If this bone has a body, return it
		if ( Bodies.TryGetValue( bone.Index, out var body ) )
			return body;

		// Otherwise, recursively check children
		if ( bone.Children != null )
		{
			foreach ( var childBone in bone.Children )
			{
				var childBody = GetNearestValidChildBody( childBone );
				if ( childBody != null )
					return childBody;
			}
		}
		return null;
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
}
