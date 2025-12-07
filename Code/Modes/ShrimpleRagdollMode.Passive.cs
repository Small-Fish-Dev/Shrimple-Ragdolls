public class PassiveMode : IShrimpleRagdollMode<PassiveMode>
{
	public static string Name => "Passive";
	public static string Description => "Bodies will strictly match the animation.";
	public static bool PhysicsDriven => false; // We follow the renderer's animations!

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders();
		body.Component?.Enabled = true;
		body.Component?.MotionEnabled = false;
		ragdoll.GetParentJoint( body )?.Component.Enabled = false;

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( true ); // Make model absolute if we're root

		ragdoll.AddFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		ragdoll.MoveObjectFromMesh( body.GetBone( ragdoll.Model ) );
		ragdoll.Renderer.ClearPhysicsBones(); // TODO: Clear only this bone when we have ClearPhysicsBone(
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root
		body.Component?.Enabled = false;
		body.Component?.MotionEnabled = true;
		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveObjectFromMesh( body.GetBone( ragdoll.Model ) );
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		//ragdoll.MoveMeshFromObject( body );
	}

	static PassiveMode()
	{
		ShrimpleRagdollModeRegistry.Register<PassiveMode>();
	}
}
