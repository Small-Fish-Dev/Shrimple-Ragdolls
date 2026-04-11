namespace ShrimpleRagdolls;

public class ShrimpleRagdollSystem : GameObjectSystem
{
	public ShrimpleRagdollSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, 0, UpdateHitReactions, "Updating Ragdoll Hit Reactions" );
	}

	void UpdateHitReactions()
	{
		foreach ( var ragdoll in Scene.GetAllComponents<ShrimpleRagdoll>() )
		{
			if ( ragdoll.IsValid() )
				ragdoll.UpdateHitReactions();
		}
	}
}
