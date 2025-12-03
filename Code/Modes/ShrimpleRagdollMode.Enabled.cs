public sealed class EnabledMode : IShrimpleRagdollMode
{
	public static string Name => "Enabled";
	public static string Description => "Classic ragdoll behaviour, drop on the ground and flop around";

	public void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders(); // Enable our colliders
		ragdoll.GetParentJoint( body )?.Component.Enabled = true; // Enable our parent joint
		body.Component?.Enabled = true; // Enable our rigidbody

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( true ); // Make model absolute if we're root

		ragdoll.AddFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		ragdoll.MoveObjectFromMesh( body.GetBone( ragdoll.Model ) );
	}

	public void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root
		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
	}

	public void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
	}

	public void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveMeshFromObject( body );
	}
}
