namespace ShrimpleRagdolls;

public class PassiveMode : IShrimpleRagdollMode<PassiveMode>
{
	public static string Name => "Passive";
	public static string Description => "Bodies will strictly match the animation.";
	public static bool PhysicsDriven => false;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders();
		body.EnableRigidbody();
		body.Component?.MotionEnabled = false;
		body.DisableParentJoint();

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false );

		ragdoll.SetFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.Bone | GameObjectFlags.PhysicsBone );
		ragdoll.MoveObjectFromMesh( body.GetBone() );
		ragdoll.Renderer.ClearPhysicsBones(); // TODO: Clear only this bone when we have ClearPhysicsBone()
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.Component?.MotionEnabled = true;
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveObjectFromAnimation( body.GetBone() );
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveMeshFromObject( body );
	}

	static PassiveMode()
	{
		ShrimpleRagdollModeRegistry.Register<PassiveMode>();
	}
}
