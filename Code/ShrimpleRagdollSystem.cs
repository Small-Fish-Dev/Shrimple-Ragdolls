namespace ShrimpleRagdolls;

public class ShrimpleRagdollSystem : GameObjectSystem
{
	public ShrimpleRagdollSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, -10, UpdateVisuals, "Updating Ragdoll Visuals" ); // Run before the animation system (0), otherwise it will look jittery when moving a ragdoll's renderer
		Listen( Stage.PhysicsStep, 65, UpdatePhysics, "Updating Ragdoll Physics" );
	}

	void UpdateVisuals()
	{
		var allRagdolls = Scene.GetAllComponents<ShrimpleRagdoll>();

		foreach ( var ragdoll in allRagdolls )
			ragdoll.ComputeVisuals();
	}

	void UpdatePhysics()
	{
		var allRagdolls = Scene.GetAllComponents<ShrimpleRagdoll>();

		foreach ( var ragdoll in allRagdolls )
			ragdoll.ComputePhysics();
	}
}
