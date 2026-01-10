namespace ShrimpleRagdolls;

public class ActiveMode : IShrimpleRagdollMode<ActiveMode>
{
	public static string Name => "Active";
	public static string Description => "Bodies will physically move where they're meant to be, unless blocked.";
	public static bool PhysicsDriven => false; // We follow the renderer's animations!

	public static void OnEnter( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.EnableColliders();
		body.EnableRigidbody();
		body.Component?.Gravity = false; // Gravity's hard to combat when we want it to be as close to the animations as possible while not using crazy amounts of force!
		body.EnableParentJoint();

		if ( body.IsRootBone )
			ragdoll.MakeRendererAbsolute( false ); // Make model not absolute if we're root

		ragdoll.SetFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.Bone );
		ragdoll.MoveBodyFromAnimations( body );
	}

	public static void OnExit( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		body.Component?.Gravity = true;

		var parentJoint = body.GetParentJoint();
		if ( parentJoint != null )
			ragdoll.ResetJointSettings( parentJoint.Value );
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
