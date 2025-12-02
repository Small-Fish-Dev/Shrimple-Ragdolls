public interface IShrimpleRagdollMode
{
	public static abstract string Name { get; }
	public static abstract string Description { get; }

	public static abstract void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
	public static abstract void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
	public static abstract void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
	public static abstract void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body );
}
