/// <summary>
/// Base class for mode-specific settings components.
/// Attach these to the same GameObject as ShrimpleRagdoll to configure mode behavior.
/// </summary>
public abstract class ModeSettings : Component
{
	/// <summary>
	/// The mode name this settings component targets (e.g. "Motor", "Active")
	/// </summary>
	public abstract string TargetMode { get; }

	/// <summary>
	/// Reference to the ragdoll component
	/// </summary>
	protected ShrimpleRagdoll Ragdoll { get; private set; }

	protected override void OnAwake()
	{
		base.OnAwake();
		Ragdoll = GetComponent<ShrimpleRagdoll>();

		if ( !Ragdoll.IsValid() )
		{
			Log.Warning( $"{GetType().Name} requires a ShrimpleRagdoll component on the same GameObject!" );
		}
	}

	/// <summary>
	/// Called when a body enters this mode.
	/// Apply your mode-specific settings here.
	/// </summary>
	public abstract void ApplySettings( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );

	/// <summary>
	/// Apply settings to all bodies that are currently in this mode
	/// </summary>
	public void ApplyToAllBodiesInMode()
	{
		if ( !Ragdoll.IsValid() )
			return;

		foreach ( var body in Ragdoll.Bodies.Values )
		{
			if ( Ragdoll.BodyModes.TryGetValue( body.BoneIndex, out var modeName ) && modeName == TargetMode )
			{
				ApplySettings( Ragdoll, body );
			}
		}
	}

	/// <summary>
	/// Apply settings to a specific body if it's in this mode
	/// </summary>
	public void ApplyToBody( ShrimpleRagdoll.Body body )
	{
		if ( !Ragdoll.IsValid() )
			return;

		if ( Ragdoll.BodyModes.TryGetValue( body.BoneIndex, out var modeName ) && modeName == TargetMode )
		{
			ApplySettings( Ragdoll, body );
		}
	}
}
