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

		ragdoll.MoveObjectFromMesh( body.GetBone() );
		ragdoll.MoveMeshFromObject( body );
		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( body.IsRootBone )
		{
			ragdoll.MakeRendererAbsolute( false );
			ragdoll.Renderer.GetComponent<Rigidbody>( true )?.Enabled = false;
		}
		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.ProceduralBone );
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( !body.GameObject.Flags.HasFlag( GameObjectFlags.ProceduralBone ) ) // For some reason it's delayed when done inside of OnEnter? So we do it here even if not optimal
			ragdoll.AddFlags( body.GameObject, GameObjectFlags.ProceduralBone ); // Add procedural bone so it's ignored by the boneObjectList updates in SkinnedModelRenderer

		ragdoll.MoveMeshFromObject( body );
	}

	static StatueMode()
	{
		ShrimpleRagdollModeRegistry.Register<StatueMode>();
	}
}
