public sealed class DisabledMode : IShrimpleRagdollMode
{
	public static string Name => "Disabled";
	public static string Description => "Disables all bodies and joints";

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.DisableColliders(); // Disable colliders
		ragdoll.GetParentJoint( body )?.Component.Enabled = false; // Disable our parent joint
		body.Component?.Enabled = false; // Disable our rigidbody

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root

		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone ); // Remove tags
		ragdoll.Renderer.SceneModel.SetBoneOverride( body.BoneIndex, Transform.Zero ); // Try to reset bone override this way, if not we'll need to call ClearBoneOverrides
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}
}
