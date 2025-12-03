public sealed class StatueMode : IShrimpleRagdollMode
{
	public static string Name => "Statue";
	public static string Description => "Ragdoll is frozen in place, physics and joints are disabled.";

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders(); // Enable our collides
		ragdoll.GetParentJoint( body )?.Component.Enabled = false; // Disable our parent joint
		body.Component?.Enabled = false; // Disable our rigidbody

		if ( body.GameObject.Parent == ragdoll.Renderer.GameObject )
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
		if ( body.GameObject.Parent == ragdoll.Renderer.GameObject )
		{
			ragdoll.MakeRendererAbsolute( false ); // Remove absolute from model if we're root
			ragdoll.Renderer.GetComponent<Rigidbody>( true )?.Enabled = false; // Disable model's rigidbody if we're root
		}
	}

	public static void PhysicsUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}

	public static void VisualUpdate( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{

	}
}
