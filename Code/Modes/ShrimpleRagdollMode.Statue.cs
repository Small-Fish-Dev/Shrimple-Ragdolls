public class StatueMode : IShrimpleRagdollMode<StatueMode>
{
	public static string Name => "Statue";
	public static string Description => "Ragdoll is frozen in place, physics and joints are disabled.";

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders(); // Enable our colliders
		ragdoll.GetParentJoint( body )?.Component.Enabled = false; // Disable our parent joint
		body.Component?.Enabled = false; // Disable our rigidbody

		if ( body.IsRootBone )
		{
			ragdoll.MakeRendererAbsolute( true ); // Make model absolute if we're root
			ragdoll.Renderer.GetOrAddComponent<Rigidbody>( true ).Enabled = true; // Add rigidbody to model if we're root
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
		if ( body.IsRootBone )
			ragdoll.MoveGameObject();
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
