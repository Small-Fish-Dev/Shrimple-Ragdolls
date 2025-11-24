public partial class ShrimpleActiveRagdoll
{
	[Sync]
	public NetDictionary<int, Transform> BodyTransforms { get; set; } = new();

	protected void SetupBodyTransforms()
	{
		if ( IsProxy )
			return;

		BodyTransforms.Clear();

		foreach ( var body in Bodies )
			BodyTransforms.Add( body.Key.Index, body.Value.Component.WorldTransform );
	}
	protected void SetBodyTransforms()
	{
		if ( IsProxy )
			return;

		foreach ( var body in Bodies )
			BodyTransforms[body.Key.Index] = body.Value.Component.GameObject.WorldTransform;
	}

	protected void SetProxyTransforms()
	{
		if ( !IsProxy )
			return;

		foreach ( var bodyTransform in BodyTransforms )
		{
			var body = GetBodyByBoneIndex( bodyTransform.Key );

			if ( body != null )
				body.Component.WorldTransform = bodyTransform.Value;
		}
	}
}
