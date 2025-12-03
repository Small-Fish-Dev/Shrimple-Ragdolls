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
}

public static class ShrimpleRagdollMode
{
	public static string Disabled => "Disabled";
	public static string Enabled => "Enabled";
	public static string Passive => "Passive";
	public static string Active => "Active";
	public static string Statue => "Statue";
}
