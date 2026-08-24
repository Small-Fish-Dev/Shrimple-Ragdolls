namespace ShrimpleRagdolls;

public class ShrimpleRagdollSystem : GameObjectSystem
{
	private readonly List<ShrimpleRagdoll> _ragdolls = [];

	public ShrimpleRagdollSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, 0, UpdateHitReactions, "Updating Ragdoll Hit Reactions" );
	}

	void UpdateHitReactions()
	{
		_ragdolls.Clear();
		Scene.GetAll( _ragdolls );

		foreach ( var ragdoll in _ragdolls )
		{
			if ( ragdoll.IsValid() )
				ragdoll.UpdateHitReactions();
		}
	}
}
