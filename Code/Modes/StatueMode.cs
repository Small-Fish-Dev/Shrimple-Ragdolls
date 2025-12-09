public class StatueMode : IShrimpleRagdollMode<StatueMode>
{
	public static string Name => "Statue";
	public static string Description => "Ragdoll is frozen in place, joints are disabled.";
	public static bool PhysicsDriven => false; // Despite being physically simulated, the renderer and the physicsbody are one thing so we don't need to respect MoveMode!

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders(); // Enable our colliders
		ragdoll.GetParentJoint( body )?.Component.Enabled = false; // Disable our parent joint
		body.Component?.Enabled = false; // Disable our rigidbody

		if ( body.IsRootBone )
		{
			ragdoll.MakeRendererAbsolute( true ); // Make model absolute if we're root
			var rigidBody = ragdoll.Renderer.GetOrAddComponent<Rigidbody>( true ); // Add rigidbody to model if we're root
			rigidBody.Enabled = true;
			rigidBody.Locking = ragdoll.Locking;
			rigidBody.RigidbodyFlags = ragdoll.RigidbodyFlags;
		}

		ragdoll.RemoveFlags( body.GameObject, GameObjectFlags.Absolute ); // Remove absolute tag from our gameobject
		ragdoll.MoveObjectFromMesh( body.GetBone( ragdoll.Model ) );
		ragdoll.MoveMeshFromObject( body );
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( body.IsRootBone )
		{
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root
			ragdoll.Renderer.GetComponent<Rigidbody>( true )?.Enabled = false; // Disable model's rigidbody if we're root
		}
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		// We're a statue, we don't need to update visuals ourselves :)
	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		// We're a statue, we don't need to update visuals ourselves :)
	}

	static StatueMode()
	{
		ShrimpleRagdollModeRegistry.Register<StatueMode>();
	}
}
