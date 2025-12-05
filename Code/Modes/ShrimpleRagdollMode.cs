public interface IShrimpleRagdollMode<TSelf>
	where TSelf : IShrimpleRagdollMode<TSelf>
{
	/// <summary>
	///  The naame of this mode, must be unique as it's used as an id
	/// </summary>
	static abstract string Name { get; }
	/// <summary>
	/// A short description of what this mode does
	/// </summary>
	static abstract string Description { get; }
	/// <summary>
	/// The renderer should follow the ragdoll's MoveMode
	/// </summary>
	static abstract bool PhysicsDriven { get; }

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
	public readonly bool PhysicsDriven;

	public ShrimpleRagdollModeHandlers(
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> enter,
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> exit,
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> physics,
		Action<ShrimpleRagdoll, ShrimpleRagdoll.Body> visual,
		string desc,
		bool physicsDriven )
	{
		OnEnter = enter;
		OnExit = exit;
		PhysicsUpdate = physics;
		VisualUpdate = visual;
		Description = desc;
		PhysicsDriven = physicsDriven;
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
			T.Description,
			T.PhysicsDriven
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
			yield return new ShrimpleRagdollModeInfo( kv.Key, kv.Value.Description, kv.Value.PhysicsDriven );
		}
	}
}

public class ShrimpleRagdollMode
{
	// Default ones VVV

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
	public readonly bool PhysicsDriven;

	public ShrimpleRagdollModeInfo( string name, string description, bool physicsDriven )
	{
		Name = name;
		Description = description;
		PhysicsDriven = physicsDriven;
	}
}

[Serializable]
public struct ShrimpleRagdollModeProperty
{
	/// <summary>
	/// The string key we use to look up the handlers in the registry.
	/// </summary>
	[Property, KeyProperty]
	public string Name { get; set; }

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
