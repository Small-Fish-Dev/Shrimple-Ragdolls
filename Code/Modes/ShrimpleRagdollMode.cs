public interface IShrimpleRagdollMode<TSelf>
	where TSelf : IShrimpleRagdollMode<TSelf>
{
	static abstract string Name { get; }
	static abstract string Description { get; }

	static abstract void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
	static abstract void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
	static abstract void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
	static abstract void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
}

public readonly struct ShrimpleRagdollModeHandlers
{
	public readonly Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> OnEnter;
	public readonly Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> OnExit;
	public readonly Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> PhysicsUpdate;
	public readonly Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> VisualUpdate;
	public readonly string Description;

	public ShrimpleRagdollModeHandlers(
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> enter,
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> exit,
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> physics,
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> visual,
		string desc )
	{
		OnEnter = enter;
		OnExit = exit;
		PhysicsUpdate = physics;
		VisualUpdate = visual;
		Description = desc;
	}
}

public static class ShrimpleRagdollModeRegistry
{
	private static readonly Dictionary<string, ShrimpleRagdollModeHandlers> _modes = new();

	public static void Register<T>() where T : IShrimpleRagdollMode<T>
	{
		_modes[T.Name] = new ShrimpleRagdollModeHandlers(
			T.OnEnter,
			T.OnExit,
			T.PhysicsUpdate,
			T.VisualUpdate,
			T.Description
		);
	}

	public static bool TryGet( string name, out ShrimpleRagdollModeHandlers handlers )
		=> _modes.TryGetValue( name, out handlers );

	/// <summary>
	/// Exposed for the editor widget – gives you all the mode names + descriptions.
	/// </summary>
	public static IEnumerable<ShrimpleRagdollModeInfo> GetRegisteredModes()
	{
		foreach ( var kv in _modes )
		{
			yield return new ShrimpleRagdollModeInfo( kv.Key, kv.Value.Description );
		}
	}
}

public class ShrimpleRagdollMode
{
	public static string Disabled => "Disabled";
	public static string Enabled => "Enabled";
	public static string Passive => "Passive";
	public static string Active => "Active";
	public static string Statue => "Statue";
}

public static class ShrimpleRagdollModeExtensions
{
	// This is an example if you want to add your own
	extension( ShrimpleRagdollMode target )
	{
		public static string GmodStatue => "GmodStatue";
	}
}

public readonly struct ShrimpleRagdollModeInfo
{
	public readonly string Name;
	public readonly string Description;

	public ShrimpleRagdollModeInfo( string name, string description )
	{
		Name = name;
		Description = description;
	}
}

[Serializable]
public struct ShrimpleRagdollModeProperty
{
	/// <summary>
	/// The string key we use to look up the handlers in the registry.
	/// </summary>
	public string Name;

	public ShrimpleRagdollModeProperty( string name )
	{
		Name = name;
	}

	public override string ToString() => Name ?? string.Empty;

	public static implicit operator string( ShrimpleRagdollModeProperty value )
		=> value.Name;

	public static implicit operator ShrimpleRagdollModeProperty( string name )
		=> new ShrimpleRagdollModeProperty( name );
}
