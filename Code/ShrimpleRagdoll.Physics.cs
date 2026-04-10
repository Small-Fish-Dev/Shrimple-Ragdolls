namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	[Property, Group( "Physics" )]
	public bool MotionEnabled
	{
		get;
		set
		{
			field = value;
			if ( ModelPhysics.IsValid() )
				ModelPhysics.MotionEnabled = value;
		}
	}

	/// <summary>
	/// Enable/Disable gravity to all bodies.
	/// </summary>
	[Property, Group( "Physics" )]
	public bool Gravity
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SetGravity( value );
		}
	} = true;

	/// <summary>
	/// Set the gravity scale to all bodies.
	/// </summary>
	[Property, Group( "Physics" )]
	public float GravityScale
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SetGravityScale( value );
		}
	} = 1f;

	/// <summary>
	/// Set the linear damping to all bodies.
	/// </summary>
	[Property, Group( "Physics" )]
	public float LinearDamping
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SetLinearDamping( value );
		}
	} = 0f;

	/// <summary>
	/// Set the angular damping to all bodies.
	/// </summary>
	[Property, Group( "Physics" )]
	public float AngularDamping
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SetAngularDamping( value );
		}
	} = 0f;

	/// <summary>
	/// Rigidbody flags applied to all bodies.
	/// </summary>
	[Property, Group( "Physics" )]
	public RigidbodyFlags RigidbodyFlags
	{
		get;
		set
		{
			field = value;
			if ( ModelPhysics.IsValid() )
				ModelPhysics.RigidbodyFlags = value;
		}
	}

	/// <summary>
	/// Rigidbody locking applied to all bodies.
	/// </summary>
	[Property, Group( "Physics" )]
	public PhysicsLock Locking
	{
		get;
		set
		{
			field = value;
			if ( ModelPhysics.IsValid() )
				ModelPhysics.Locking = value;
		}
	}

	/// <summary>
	/// Sets the mass override of this ragdoll<br />
	/// Each body part will have their proportionally changed so they combine to the desired total mass<br />
	/// Set to 0 for the default value
	/// </summary>
	[Property, Group( "Physics" )]
	public float MassOverride
	{
		get;
		set
		{
			field = value;
			SetMassOverride( value );
		}
	} = 0f;

	/// <summary>
	/// All bodies will be put to sleep on start.
	/// </summary>
	[Property, Group( "Physics" )]
	public bool StartAsleep
	{
		get;
		set
		{
			field = value;
			if ( ModelPhysics.IsValid() )
				ModelPhysics.StartAsleep = value;
		}
	}

	/// <summary>
	/// Surface to apply to all colliders<br />
	/// Set to null for the surfaces defined in the ragdoll
	/// </summary>
	[Property, Group( "Physics" )]
	public Surface Surface
	{
		get;
		set
		{
			field = value;
			SetSurface( value );
		}
	} = null;

	[Property, Group( "Physics" )]
	public ColliderFlags ColliderFlags
	{
		get;
		set
		{
			field = value;
			SetColliderFlags( value );
		}
	}

	public float Mass => (ModelPhysics?.Mass ?? 0f) == 0f ? GetModelMass() : ModelPhysics?.Mass ?? 0f;

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

		foreach ( var body in Bodies )
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
	/// Calculate the mass of the ragdoll using the model data
	/// </summary>
	/// <returns></returns>
	public float GetModelMass() => Renderer?.Model?.Physics?.Parts?.Sum( x => x.Mass ) ?? 0f;

	/// <summary>
	/// Makes sure to wake up all bodies
	/// </summary>
	public void WakePhysics()
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
			if ( body.Component.IsValid() )
				body.Component.Sleeping = false;
	}

	/// <summary>
	/// Sets all rigidbodies to sleep
	/// </summary>
	public void SleepPhysics()
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
			if ( body.Component.IsValid() )
				body.Component.Sleeping = true;
	}

	/// <summary>
	/// Sets gravity to all rigid bodies
	/// </summary>
	/// <param name="gravity"></param>
	protected void SetGravity( bool gravity )
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
			if ( body.Component.IsValid() )
				body.Component.Gravity = gravity;
	}

	/// <summary>
	/// Sets gravity scale to all rigid bodies
	/// </summary>
	/// <param name="gravityScale"></param>
	protected void SetGravityScale( float gravityScale )
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
			if ( body.Component.IsValid() )
				body.Component.GravityScale = gravityScale;
	}

	/// <summary>
	/// Sets the linear damping to all rigid bodies
	/// </summary>
	/// <param name="damping"></param>
	protected void SetLinearDamping( float damping )
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
			if ( body.Component.IsValid() )
				body.Component.LinearDamping = damping;
	}

	/// <summary>
	/// Sets the angular damping to all rigid bodies
	/// </summary>
	/// <param name="damping"></param>
	protected void SetAngularDamping( float damping )
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
			if ( body.Component.IsValid() )
				body.Component.AngularDamping = damping;
	}

	/// <summary>
	/// Apply a surface to every collider
	/// </summary>
	/// <param name="surface"></param>
	protected void SetSurface( Surface surface )
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() || !body.Component.GameObject.IsValid() )
				continue;

			foreach ( var collider in body.Component.GameObject.GetComponents<Collider>() )
			{
				if ( collider.IsValid() )
					collider.Surface = surface;
			}
		}
	}

	/// <summary>
	/// Apply collider flags to every collider
	/// </summary>
	/// <param name="flags"></param>
	protected void SetColliderFlags( ColliderFlags flags )
	{
		if ( !PhysicsWereCreated )
			return;

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() || !body.Component.GameObject.IsValid() )
				continue;

			foreach ( var collider in body.Component.GameObject.GetComponents<Collider>() )
			{
				if ( collider.IsValid() )
					collider.ColliderFlags = flags;
			}
		}
	}

	/// <summary>
	/// Sets the mass override, each body piece will have their mass proportional so that the total combines to the desired value
	/// </summary>
	/// <param name="massOverride"></param>
	protected void SetMassOverride( float massOverride )
	{
		if ( !PhysicsWereCreated )
			return;

		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.MassOverride = massOverride;

		float totalDefaultMass = 0f;
		foreach ( var body in Bodies )
		{
			if ( body.Component.IsValid() && body.Component.PhysicsBody.IsValid() )
				totalDefaultMass += body.Component.PhysicsBody.Mass;
		}

		if ( totalDefaultMass <= 0f )
			return;

		// Set proportional masses so they sum to massOverride
		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() || !body.Component.PhysicsBody.IsValid() )
				continue;

			float proportion = body.Component.PhysicsBody.Mass / totalDefaultMass;
			body.Component.MassOverride = massOverride * proportion;
		}
	}

	/// <summary>
	/// Sets up all physics related settings for colliders and rigidbodies
	/// </summary>
	public void SetupPhysics()
	{
		SetGravity( Gravity );
		SetGravityScale( GravityScale );
		SetLinearDamping( LinearDamping );
		SetAngularDamping( AngularDamping );
		SetSurface( Surface );
		SetColliderFlags( ColliderFlags );
		SetMassOverride( MassOverride );
	}
}
