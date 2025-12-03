public interface IShrimpleRagdollMode
{
	public static string Name { get; }
	public static string Description { get; }

	public virtual void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body ) { }
	public virtual void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body ) { }
	public virtual void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body ) { }
	public virtual void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body ) { }
}

internal static class RagdollModeRegistry
{
	private static readonly Dictionary<Type, IShrimpleRagdollMode> _cache = new();

	public static T Get<T>() where T : IShrimpleRagdollMode, new()
	{
		var type = typeof( T );

		if ( !_cache.TryGetValue( type, out var mode ) )
		{
			mode = new T();
			_cache[type] = mode;
			Log.Info( "HIIII" + mode );
		}

		return (T)mode;
	}
}
