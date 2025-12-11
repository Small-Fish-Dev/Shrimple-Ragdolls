using Sandbox.Utility;

public partial class ShrimpleRagdoll
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
	public string LerpToAnimationTarget { get; protected set; }

	/// <summary>
	/// The easing function to use when lerping to animation
	/// </summary>
	public Easing.Function LerpToAnimationFunction { get; protected set; } = Easing.EaseIn;

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
			if ( !Renderer.TryGetBoneTransformAnimation( body.Value.GetBone(), out var animTransform ) )
				continue;
			startTransform = Renderer.WorldTransform.ToWorld( startTransform );

			var currentTransform = startTransform.LerpTo( animTransform, LerpToAnimationFunction.Invoke( LerpToAnimation.Value.Fraction ), false );
			currentTransform = Renderer.WorldTransform.ToLocal( currentTransform );
			Renderer.SceneModel.SetBoneOverride( body.Key, in currentTransform );
		}

		if ( LerpToAnimation.Value )
		{
			Renderer.ClearPhysicsBones();
			LerpToAnimation = null;
			LerpStartTransforms.Clear();
			Mode = LerpToAnimationTarget;
		}
	}
	// TODO: EASING CANT BE NETWORKED, USE STRING
	/// <summary>
	/// Start lerping the ragdoll to the current animation pose
	/// </summary>
	/// <param name="duration">How long the transition will last</param>
	/// <param name="function">Which easing function to use for the interpolation</param>
	/// <param name="targetMode">Which mode to set the ragdoll after lerping is complete</param>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpToAnimation( float duration, Easing.Function function, string targetMode = "Disabled" )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		foreach ( var body in Bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.Key ); // I'd use GetBoneLocalTransform but I can't find which transform it's local to! Not the renderer or bone object's so idk
			LerpStartTransforms[body.Key] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}

		LerpToAnimationTarget = targetMode;
		LerpToAnimationFunction = function;
		LerpToAnimation = MathF.Max( duration, Time.Delta );
	}

	/// <summary>
	/// Start lerping the ragdoll to the current animation pose
	/// </summary>
	/// <param name="duration">How long the transition will last</param>
	/// <param name="targetMode">Which mode to set the ragdoll after lerping is complete</param>
	public void StartSlerpToAnimation( float duration, string targetMode = "Disabled" )
	{
		StartLerpToAnimation( duration, Easing.EaseIn, targetMode );
	}

	[Button( "TESTLERP" )]
	public void TestLerpToAnimation()
	{
		StartLerpToAnimation( 2f, Easing.AnticipateOvershoot, ShrimpleRagdollMode.Disabled );
	}
}
