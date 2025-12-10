public class ActiveMode : IShrimpleRagdollMode<ActiveMode>
{
	public static string Name => "Active";
	public static string Description => "Bodies will physically move where they're meant to be, unless blocked.";
	public static bool PhysicsDriven => false; // We follow the renderer's animations!

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders();
		body.Component?.Enabled = true;
		body.Component?.Gravity = false; // Gravity's hard to combat when we want it to be as close to the animations as possible while not using crazy amounts of force!
		body.GetParentJoint()?.Component.Enabled = true;

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( true ); // Make model absolute if we're root

		ragdoll.AddFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		ragdoll.MoveObjectFromMesh( body.GetBone() );
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root
		body.Component?.Gravity = true;
		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveBodyFromAnimations( body );
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveMeshFromObject( body );
	}

	static ActiveMode()
	{
		ShrimpleRagdollModeRegistry.Register<ActiveMode>();
	}
}
