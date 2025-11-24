public partial class ShrimpleActiveRagdoll
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
		MoveMeshFromObjects();

		foreach ( var body in Bodies.Values )
			body.Component.Enabled = true;

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
