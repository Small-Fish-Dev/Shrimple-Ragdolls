public class ShrimpleActiveRagdollSystem : GameObjectSystem
{
	public ShrimpleActiveRagdollSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, 65, UpdateVisuals, "Updating Ragdoll Visuals" );
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
