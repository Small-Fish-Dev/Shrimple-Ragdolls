namespace ShrimpleRagdolls;

public class EnabledMode : IShrimpleRagdollMode<EnabledMode>
{
	public static string Name => "Enabled";
	public static string Description => "Classic ragdoll behaviour, drop on the ground and flop around.";
	public static bool PhysicsDriven => true;

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders();
		body.EnableRigidbody(); // Make sure to enable rigidbody before joint to build the physicsbody or else the joint freaks out!
		body.EnableParentJoint();

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( true ); // Make model absolute if we're root

		ragdoll.SetFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.Bone | GameObjectFlags.PhysicsBone );
		ragdoll.MoveObjectFromMesh( body.GetBone() );
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveMeshFromObject( body );
	}

	static EnabledMode()
	{
		ShrimpleRagdollModeRegistry.Register<EnabledMode>();
	}
}
