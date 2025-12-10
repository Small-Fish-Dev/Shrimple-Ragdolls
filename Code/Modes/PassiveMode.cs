public class PassiveMode : IShrimpleRagdollMode<PassiveMode>
{
	public static string Name => "Passive";
	public static string Description => "Bodies will strictly match the animation.";
	public static bool PhysicsDriven => false;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders();
		body.Component?.Enabled = true;
		body.Component?.MotionEnabled = false;
		body.GetParentJoint()?.Component.Enabled = false;

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( true );

		ragdoll.AddFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		ragdoll.MoveObjectFromMesh( body.GetBone() );
		ragdoll.Renderer.ClearPhysicsBones(); // TODO: Clear only this bone when we have ClearPhysicsBone()
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false );
		body.Component?.Enabled = false;
		body.Component?.MotionEnabled = true;
		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveObjectFromMesh( body.GetBone() );
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
	}

	static PassiveMode()
	{
		ShrimpleRagdollModeRegistry.Register<PassiveMode>();
	}
}
