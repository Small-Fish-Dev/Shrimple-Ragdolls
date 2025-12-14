public class StatueMode : IShrimpleRagdollMode<StatueMode>
{
	public static string Name => "Statue";
	public static string Description => "Ragdoll is frozen in place, joints are disabled.";
	public static bool PhysicsDriven => false; // Despite being physically simulated, the renderer and the physicsbody are one thing so we don't need to respect MoveMode!

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders();
		body.DisableParentJoint();
		body.DisableRigidbody();

		if ( body.IsRootBone )
		{
			ragdoll.MakeRendererAbsolute( true );
			var rigidBody = ragdoll.Renderer.GetOrAddComponent<Rigidbody>( true );
			rigidBody.Enabled = true;
			rigidBody.Locking = ragdoll.Locking;
			rigidBody.RigidbodyFlags = ragdoll.RigidbodyFlags;
			ragdoll.SetupPhysics(); // We created a new rigidbody, so we gotta give it all the settings we have
		}

		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		ragdoll.MoveObjectFromMesh( body.GetBone() );
		ragdoll.MoveMeshFromObject( body );
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( body.IsRootBone )
		{
			ragdoll.MakeRendererAbsolute( false );
			ragdoll.Renderer.GetComponent<Rigidbody>( true )?.Enabled = false;
		}
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		ragdoll.MoveMeshFromObject( body );
	}

	static StatueMode()
	{
		ShrimpleRagdollModeRegistry.Register<StatueMode>();
	}
}
