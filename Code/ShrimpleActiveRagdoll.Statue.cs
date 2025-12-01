public partial class ShrimpleActiveRagdoll
{
	protected void CreateStatuePhysics()
	{
		if ( !Active || IsProxy )
			return;

		//DestroyPhysics();

		if ( !Model.IsValid() )
			return;
		/*
				var physics = Model.Physics;
				if ( physics == null || physics.Parts.Count == 0 )
					return;*/

		//CreateBoneObjects( physics );
		//CreateStatueBodies( physics );
		MoveMeshFromObjects();
		var rigidbody = Renderer.AddComponent<Rigidbody>();
		foreach ( var body in Bodies.Values )
		{
			RemoveFlags( body.GameObject, GameObjectFlags.Absolute );
			body.Component.Enabled = false;
		}
		DisableJoints();
		rigidbody.PhysicsBody.RebuildMass();
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
