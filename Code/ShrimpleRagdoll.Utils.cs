public partial class ShrimpleRagdoll
{
	[Button]
	public void TestMove()
	{
		Move( new Transform( WorldPosition + Vector3.Up * 100f, WorldRotation ) );
	}

	/// <summary>
	/// Calculate the center of mass of the ragdoll based on its bodies' masses and masscenters
	/// </summary>
	/// <returns></returns>
	public Vector3 GetMassCenter()
	{
		var masses = 0f;
		var centers = Vector3.Zero;

		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			var mass = body.Component.PhysicsBody.Mass;
			centers += body.Component.MassCenter * mass;
			masses += mass;
		}

		if ( masses <= 0f )
			return Vector3.Zero;

		return centers / masses;
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

		var rotationCenter = Renderer.TryGetBoneTransform( "pelvis", out var trasform ) ? trasform.Position : massCenter;
		var centerLinearVelocity = Vector3.Cross( angularVelocity, (massCenter - rotationCenter) );

		foreach ( var body in Bodies?.Values )
		{
			var bodyVelocity = centerLinearVelocity + Vector3.Cross( angularVelocity, body.Component.WorldPosition - massCenter );
			body.Component?.Velocity += bodyVelocity;
			body.Component?.AngularVelocity += angularVelocity;
		}
	}

	/// <summary>
	/// Makes sure to wake up all bodies
	/// </summary>
	public void WakePhysics()
	{
		if ( Mode == ShrimpleRagdollMode.Statue )
		{
			var body = Renderer.GetComponent<Rigidbody>();
			body.Sleeping = false;
		}
		else
		{
			foreach ( var body in Bodies )
				body.Value.Component.Sleeping = false;
		}
	}

	public Body? GetBodyByBoneName( string boneName )
	{
		var bone = Renderer.Model.Bones.GetBone( boneName );
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

	public BoneCollection.Bone GetBoneByBody( Body body )
	{
		if ( !body.IsValid )
			return null;

		foreach ( var pair in Bodies )
		{
			var bone = pair.Value.GetBone( Model );
			if ( bone == body.GetBone( Model ) )
				return bone;
		}
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
}
