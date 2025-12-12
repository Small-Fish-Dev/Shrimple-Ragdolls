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
	/// Set of body indices that should be lerped (null = all bodies)
	/// </summary>
	protected HashSet<int> LerpTargetBodies { get; set; } = null;

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
			// Skip if we have target bodies specified and this isn't one of them
			if ( LerpTargetBodies != null && !LerpTargetBodies.Contains( body.Key ) )
				continue;

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

			// Only change mode if we were lerping all bodies
			if ( LerpTargetBodies == null )
				Mode = LerpToAnimationTarget;

			LerpTargetBodies = null;
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

		LerpStartTransforms.Clear();
		LerpTargetBodies = null; // Clear target bodies - lerp all

		foreach ( var body in Bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.Key );
			LerpStartTransforms[body.Key] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}

		LerpToAnimationTarget = targetMode;
		LerpToAnimationFunction = function;
		LerpToAnimation = MathF.Max( duration, Time.Delta );
	}

	/// <summary>
	/// Start lerping a single body to the current animation pose
	/// </summary>
	/// <param name="body">The body to lerp</param>
	/// <param name="duration">How long the transition will last</param>
	/// <param name="function">Which easing function to use for the interpolation</param>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpBodyToAnimation( Body body, float duration, Easing.Function function )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		// Initialize or add to existing lerp
		if ( LerpToAnimation == null )
		{
			LerpStartTransforms.Clear();
			LerpTargetBodies = new HashSet<int>();
			LerpToAnimationFunction = function;
			LerpToAnimation = MathF.Max( duration, Time.Delta );
		}

		// Add this body to the lerp targets
		LerpTargetBodies ??= new HashSet<int>();
		LerpTargetBodies.Add( body.BoneIndex );

		// Store start transform
		var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex );
		LerpStartTransforms[body.BoneIndex] = Renderer.WorldTransform.ToLocal( renderBonePosition );
	}

	/// <summary>
	/// Start lerping multiple bodies to the current animation pose
	/// </summary>
	/// <param name="bodies">The bodies to lerp</param>
	/// <param name="duration">How long the transition will last</param>
	/// <param name="function">Which easing function to use for the interpolation</param>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpBodiesToAnimation( IEnumerable<Body> bodies, float duration, Easing.Function function )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		LerpStartTransforms.Clear();
		LerpTargetBodies = new HashSet<int>();
		LerpToAnimationFunction = function;
		LerpToAnimation = MathF.Max( duration, Time.Delta );

		foreach ( var body in bodies )
		{
			LerpTargetBodies.Add( body.BoneIndex );

			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex );
			LerpStartTransforms[body.BoneIndex] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}
	}

	/// <summary>
	/// Start lerping bodies within a radius of a world position to animation
	/// </summary>
	/// <param name="worldPosition">The center position</param>
	/// <param name="radius">The radius to affect bodies within</param>
	/// <param name="duration">How long the transition will last</param>
	/// <param name="function">Which easing function to use for the interpolation</param>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpBodiesInRadiusToAnimation( Vector3 worldPosition, float radius, float duration, Easing.Function function )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		var affectedBodies = new List<Body>();

		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			var distance = Vector3.DistanceBetween( worldPosition, body.Component.WorldPosition );
			if ( distance <= radius )
				affectedBodies.Add( body );
		}

		if ( affectedBodies.Count > 0 )
			StartLerpBodiesToAnimation( affectedBodies, duration, function );
	}

	/// <summary>
	/// Start lerping from custom displaced transforms back to animation
	/// Used by hit reactions to provide pre-calculated displaced positions
	/// </summary>
	/// <param name="displacedTransforms">Dictionary of bone indices to their displaced local transforms</param>
	/// <param name="duration">How long the transition will last</param>
	/// <param name="function">Which easing function to use for the interpolation</param>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpFromDisplacedTransforms( Dictionary<int, Transform> displacedTransforms, float duration, Easing.Function function )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;
		if ( displacedTransforms == null || displacedTransforms.Count == 0 )
			return;

		LerpStartTransforms.Clear();
		LerpTargetBodies = new HashSet<int>();
		LerpToAnimationFunction = function;
		LerpToAnimation = MathF.Max( duration, Time.Delta );

		// Use the provided displaced transforms as start positions
		// and immediately apply them
		foreach ( var kvp in displacedTransforms )
		{
			LerpTargetBodies.Add( kvp.Key );
			LerpStartTransforms[kvp.Key] = kvp.Value;

			// Immediately apply the displaced transform
			Renderer.SceneModel.SetBoneOverride( kvp.Key, kvp.Value );
		}
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

	[Button( "Test Lerp Single Body - Head" )]
	public void TestLerpSingleBody()
	{
		var headBody = GetBodyByBoneName( "head" );
		if ( headBody.HasValue )
			StartLerpBodyToAnimation( headBody.Value, 1f, Easing.EaseOut );
	}

	[Button( "Test Lerp Radius - Chest" )]
	public void TestLerpRadius()
	{
		var chestBody = GetBodyByBoneName( "spine_02" );
		if ( chestBody.HasValue )
			StartLerpBodiesInRadiusToAnimation( chestBody.Value.Component.WorldPosition, 50f, 1f, Easing.EaseOut );
	}
}
