namespace ShrimpleRagdolls;

public class DisabledMode : IShrimpleRagdollMode<DisabledMode>
{
	public static string Name => "Disabled";
	public static string Description => "Disables all bodies and joints.";
	public static bool PhysicsDriven => false;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.DisableColliders();
		body.DisableParentJoint();
		body.DisableRigidbody();

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root

		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		ragdoll.Renderer.SceneModel.ClearBoneOverrides(); // TODO: Make this only ClearBoneOverride( boneIndex ) once feature request gets accepted
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

	static DisabledMode()
	{
		ShrimpleRagdollModeRegistry.Register<DisabledMode>();
	}
}
