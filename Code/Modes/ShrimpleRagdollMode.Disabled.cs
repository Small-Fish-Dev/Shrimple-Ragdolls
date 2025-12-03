public sealed class DisabledMode : IShrimpleRagdollMode
{
	public static string Name => "Disabled";
	public static string Description => "Disables all bodies and joints";

	public void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.DisableColliders(); // Disable colliders
		ragdoll.GetParentJoint( body )?.Component.Enabled = false; // Disable our parent joint
		body.Component?.Enabled = false; // Disable our rigidbody

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root

		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone ); // Remove tags
		ragdoll.Renderer.SceneModel.ClearBoneOverrides(); // TODO: Make this only ClearBoneOverride( boneIndex ) once feature request gets accepte
	}

	public void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}

	public void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}

	public void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}
}
