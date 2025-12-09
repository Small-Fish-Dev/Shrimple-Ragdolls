public partial class ShrimpleRagdoll
{
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
	/// Sets rigidbody flags to every body
	/// </summary>
	/// <param name="flags"></param>
	public void SetRigidbodyFlags( RigidbodyFlags flags )
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
	public void SetLocking( PhysicsLock locking )
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
	public void SetSurface( Surface surface )
	{
		foreach ( var body in Bodies.Values )
			foreach ( var collider in body.Colliders )
				collider.Surface = surface;
	}

	/// <summary>
	/// Apply collider flags to every collider
	/// </summary>
	/// <param name="flags"></param>
	public void SetColliderFlags( ColliderFlags flags )
	{
		foreach ( var body in Bodies.Values )
			foreach ( var collider in body.Colliders )
				collider.ColliderFlags = flags;
	}
}
