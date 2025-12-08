public partial class ShrimpleRagdoll
{
	[Sync]
	public NetDictionary<int, Transform> BodyTransforms { get; set; } = new();

	protected void SetupBodyTransforms()
	{
		if ( IsProxy )
			return;

		BodyTransforms.Clear();

		foreach ( var body in Bodies )
			BodyTransforms.Add( body.Key, body.Value.Component.WorldTransform );
	}
	protected void SetBodyTransforms()
	{
		if ( IsProxy )
			return;

		foreach ( var body in Bodies )
			BodyTransforms[body.Key] = body.Value.Component.GameObject.WorldTransform;

	}

	protected void SetProxyTransforms()
	{
		if ( !IsProxy )
			return;

		foreach ( var bodyTransform in BodyTransforms )
		{
			var body = GetBodyByBoneIndex( bodyTransform.Key );

			if ( body != null && body.Value.Component.Enabled && body.Value.Component.PhysicsBody.IsValid() )
			{
				body.Value.Component.WorldTransform = bodyTransform.Value;
				body.Value.Component.PhysicsBody.Move( bodyTransform.Value, Time.Delta );
			}
		}

		MoveGameObject();
	}

	protected void OnModeChanged( string oldMode, string newMode )
	{
		if ( IsProxy )
		{
			CreateBoneObjects( Model.Physics );
			SetBodyHierarchyReferences();
		}
	}

	protected override void OnRefresh()
	{
		base.OnRefresh();

		if ( IsProxy )
		{
			CreateBoneObjects( Model.Physics );
			SetBodyHierarchyReferences();
		}
	}

	protected override async Task OnLoad()
	{
		if ( !IsProxy )
			return;

		CreateBoneObjects( Model.Physics );
		SetBodyHierarchyReferences();
	}
}
