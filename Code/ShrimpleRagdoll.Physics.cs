public partial class ShrimpleRagdoll
{
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
			if ( field == value )
				return;

			field = value;
			SetRigidbodyFlags( value );
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
			SetLocking( value );
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
	public bool StartAsleep { get; set; } = false;

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

	/// <summary>
	/// Makes sure to wake up all bodies
	/// </summary>
	public void WakePhysics()
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.Sleeping = false;

		foreach ( var body in Bodies.Values )
			body.Component?.Sleeping = false;
	}

	/// <summary>
	/// Sets all rigidbodies to sleep
	/// </summary>
	public void SleepPhysics()
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.Sleeping = true;

		foreach ( var body in Bodies.Values )
			body.Component?.Sleeping = true;
	}

	/// <summary>
	/// Sets gravity to all rigid bodies
	/// </summary>
	/// <param name="gravity"></param>
	protected void SetGravity( bool gravity )
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.Gravity = gravity;

		foreach ( var body in Bodies.Values )
			body.Component?.Gravity = gravity;
	}

	/// <summary>
	/// Sets gravity scale to all rigid bodies
	/// </summary>
	/// <param name="gravityScale"></param>
	protected void SetGravityScale( float gravityScale )
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.GravityScale = gravityScale;

		foreach ( var body in Bodies.Values )
			body.Component?.GravityScale = gravityScale;
	}

	/// <summary>
	/// Sets the linear damping to all rigid bodies
	/// </summary>
	/// <param name="damping"></param>
	protected void SetLinearDamping( float damping )
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.LinearDamping = damping;

		foreach ( var body in Bodies.Values )
			body.Component?.LinearDamping = damping;
	}

	/// <summary>
	/// Sets the linear damping to all rigid bodies
	/// </summary>
	/// <param name="damping"></param>
	protected void SetAngularDamping( float damping )
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.AngularDamping = damping;

		foreach ( var body in Bodies.Values )
			body.Component?.AngularDamping = damping;
	}

	/// <summary>
	/// Sets rigidbody flags to every body
	/// </summary>
	/// <param name="flags"></param>
	protected void SetRigidbodyFlags( RigidbodyFlags flags )
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.RigidbodyFlags = flags;

		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			body.Component?.RigidbodyFlags = flags;
		}
	}

	/// <summary>
	/// Sets the physics locking to every body
	/// </summary>
	/// <param name="locking"></param>
	protected void SetLocking( PhysicsLock locking )
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.Locking = locking;

		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			body.Component?.Locking = locking;
		}
	}

	/// <summary>
	/// Apply a surface to every collider
	/// </summary>
	/// <param name="surface"></param>
	protected void SetSurface( Surface surface )
	{
		foreach ( var body in Bodies.Values )
			foreach ( var collider in body.Colliders )
				collider.Surface = surface;
	}

	/// <summary>
	/// Apply collider flags to every collider
	/// </summary>
	/// <param name="flags"></param>
	protected void SetColliderFlags( ColliderFlags flags )
	{
		foreach ( var body in Bodies.Values )
			foreach ( var collider in body.Colliders )
				collider.ColliderFlags = flags;
	}

	/// <summary>
	/// Sets the mass override, each body piece will have their mass proportional so that the total combines to the desired value
	/// </summary>
	/// <param name="massOverride"></param>
	protected void SetMassOverride( float massOverride )
	{
		if ( Renderer.IsValid() && Renderer.Components.TryGet<Rigidbody>( out var rigidbody ) && rigidbody.IsValid() && rigidbody.Active )
			rigidbody.MassOverride = massOverride;

		float totalDefaultMass = 0f;
		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() && body.Component.PhysicsBody.IsValid() )
				totalDefaultMass += body.Component.PhysicsBody.Mass;
		}

		if ( totalDefaultMass <= 0f )
			return;

		// Set proportional masses so they sum to massOverride
		foreach ( var body in Bodies.Values )
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
		if ( StartAsleep )
			SleepPhysics();

		SetGravity( Gravity );
		SetGravityScale( GravityScale );
		SetLinearDamping( LinearDamping );
		SetAngularDamping( LinearDamping );
		SetRigidbodyFlags( RigidbodyFlags );
		SetLocking( Locking );
		SetSurface( Surface );
		SetColliderFlags( ColliderFlags );
		SetMassOverride( MassOverride );
	}
}
