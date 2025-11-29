using Sandbox.Utility;

public partial class ShrimpleActiveRagdoll
{
	public TimeUntil? LerpToAnimation { get; set; } = null;
	protected Dictionary<int, Transform> LerpStartTransforms { get; set; } = new();

	protected void UpdateLerpAnimations()
	{
		if ( LerpToAnimation == null )
			return;

		foreach ( var body in Bodies )
		{
			if ( !LerpStartTransforms.TryGetValue( body.Key, out var startTransform ) )
				continue;
			if ( !Renderer.TryGetBoneTransformAnimation( GetBoneByBody( body.Value ), out var animTransform ) )
				continue;
			startTransform = Renderer.WorldTransform.ToWorld( startTransform );

			var currentTransform = startTransform.LerpTo( animTransform, Easing.ExpoInOut( LerpToAnimation.Value.Fraction ) );
			currentTransform = Renderer.WorldTransform.ToLocal( currentTransform );
			Renderer.SceneModel.SetBoneOverride( body.Key, in currentTransform );
		}

		MoveObjectsFromMesh();

		if ( LerpToAnimation.Value )
		{
			LerpToAnimation = null;
			Renderer.SceneModel.ClearBoneOverrides();
		}
	}

	[Button( "TESTLERP" )]
	public void TestLerpToAnimation()
	{
		foreach ( var body in Bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.Key ); // I'd use GetBoneLocalTransform but I can't find which transform it's local to! Not the renderer or bone object's so idk
			LerpStartTransforms[body.Key] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}

		LerpToAnimation = 1f;
	}
}
