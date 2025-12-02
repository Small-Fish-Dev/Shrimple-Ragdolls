public partial class ShrimpleRagdoll
{
	protected void CreateStatuePhysics()
	{
		if ( !Active || IsProxy )
			return;

		DestroyPhysics();

		if ( !Model.IsValid() )
			return;

		var physics = Model.Physics;
		if ( physics == null || physics.Parts.Count == 0 )
			return;
		CreateBoneObjects( physics );
		CreateStatueBodies( physics );
		//var rigidbody = Renderer.GameObject.AddComponent<Rigidbody>();
		//DisableBodies();
		//EnableBodies();
		Renderer.GetComponent<Rigidbody>( true )?.Enabled = true;
		MoveMeshFromObjects();
		if ( NetworkRefreshOnChange )
			Renderer?.Network?.Refresh();

		RagdollPhysicsWereCreated = true;
		StatuePhysicsWereCreated = true;
	}

	private void BakeIntoStatue() // TESTING, DO NOT USE
	{
		InternalSetRagdollMode( Mode, RagdollMode.Statue );
		StatuePhysicsWereCreated = true;
	}
}
