using Sandbox.Utility;

public partial class ShrimpleActiveRagdoll
{
	/// <summary>
	/// Timer for lerping to the current animation pose<br />
	/// null if not lerping
	/// </summary>
	public TimeUntil? LerpToAnimation { get; protected set; } = null;

	/// <summary>
	/// Initial transforms for lerping to animation
	/// </summary>
	public Dictionary<int, Transform> LerpStartTransforms { get; protected set; } = new();

	/// <summary>
	/// Which mode to set after lerping is complete
	/// </summary>
	public RagdollMode LerpToAnimationTarget { get; protected set; }

	/// <summary>
	/// Is the ragdoll currently lerping to the animation pose?
	/// </summary>
	public bool IsLerpingToAnimation => LerpToAnimation != null;

	protected void UpdateLerpAnimations()
	{
		if ( !IsLerpingToAnimation )
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
