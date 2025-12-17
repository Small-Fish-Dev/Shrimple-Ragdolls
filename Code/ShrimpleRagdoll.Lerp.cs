namespace ShrimpleRagdolls;

using Sandbox.Utility;

public enum LerpEasing
{
	Linear,
	Ease,
	EaseIn,
	EaseOut,
	EaseInOut,
	BounceIn,
	BounceOut,
	BounceInOut,
	SineEaseIn,
	SineEaseOut,
	SineEaseInOut,
	AnticipateOvershoot,
}

public static class LerpEasingExtensions
{
	public static float Apply( this LerpEasing easing, float t ) =>
		easing switch
		{
			LerpEasing.Linear => t,
			LerpEasing.Ease => Easing.QuadraticInOut( t ),
			LerpEasing.EaseIn => Easing.QuadraticIn( t ),
			LerpEasing.EaseOut => Easing.QuadraticOut( t ),
			LerpEasing.EaseInOut => Easing.ExpoInOut( t ),
			LerpEasing.BounceIn => Easing.BounceIn( t ),
			LerpEasing.BounceOut => Easing.BounceOut( t ),
			LerpEasing.BounceInOut => Easing.BounceInOut( t ),
			LerpEasing.SineEaseIn => Easing.SineEaseIn( t ),
			LerpEasing.SineEaseOut => Easing.SineEaseOut( t ),
			LerpEasing.SineEaseInOut => Easing.SineEaseInOut( t ),
			LerpEasing.AnticipateOvershoot => Easing.AnticipateOvershoot( t ),
			_ => t
		};
}

public partial class ShrimpleRagdoll
{
	public TimeUntil? LerpToAnimation { get; protected set; } = null;
	public Dictionary<int, Transform> LerpStartTransforms { get; protected set; } = new();
	public string LerpToAnimationTarget { get; protected set; }
	public LerpEasing LerpToAnimationEasing { get; protected set; } = LerpEasing.EaseIn;
	protected HashSet<int> LerpTargetBodies { get; set; } = null;
	public bool IsLerpingToAnimation => LerpToAnimation != null;

	protected void UpdateLerpAnimations()
	{
		if ( !IsLerpingToAnimation )
			return;

		foreach ( var body in Bodies )
		{
			if ( LerpTargetBodies != null && !LerpTargetBodies.Contains( body.Key ) )
				continue;

			if ( !LerpStartTransforms.TryGetValue( body.Key, out var startTransform ) )
				continue;
			if ( !Renderer.TryGetBoneTransformAnimation( body.Value.GetBone(), out var animTransform ) )
				continue;

			startTransform = Renderer.WorldTransform.ToWorld( startTransform );
			var currentTransform = startTransform.LerpTo( animTransform, LerpToAnimationEasing.Apply( LerpToAnimation.Value.Fraction ), false );
			currentTransform = Renderer.WorldTransform.ToLocal( currentTransform );
			Renderer.SceneModel.SetBoneOverride( body.Key, in currentTransform );
		}

		if ( LerpToAnimation.Value )
			FinishLerp();
	}

	protected void FinishLerp()
	{
		var bodiesToUpdate = LerpTargetBodies ?? Bodies.Keys;
		foreach ( var boneIndex in bodiesToUpdate )
		{
			if ( Bodies.TryGetValue( boneIndex, out var body ) )
				RemoveBodyFlags( body, BodyFlags.NoVisualUpdate );
		}

		Renderer.ClearPhysicsBones();

		if ( LerpTargetBodies == null )
			Mode = LerpToAnimationTarget;

		LerpToAnimation = null;
		LerpStartTransforms.Clear();
		LerpTargetBodies = null;
	}

	protected void InternalStartLerp( Dictionary<int, Transform> startTransforms, float duration, LerpEasing easing, string targetMode = null, bool lerpingAllBodies = false )
	{
		if ( startTransforms == null || startTransforms.Count == 0 )
			return;

		LerpStartTransforms = startTransforms;
		LerpTargetBodies = lerpingAllBodies ? null : new HashSet<int>( startTransforms.Keys );
		LerpToAnimationEasing = easing;
		LerpToAnimationTarget = targetMode;
		LerpToAnimation = MathF.Max( duration, Time.Delta );

		foreach ( var (boneIndex, transform) in startTransforms )
		{
			if ( Bodies.TryGetValue( boneIndex, out var body ) )
			{
				AddBodyFlags( body, BodyFlags.NoVisualUpdate );
				Renderer.SceneModel.SetBoneOverride( boneIndex, transform );
			}
		}
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpToAnimation( float duration, LerpEasing easing = LerpEasing.Ease, string targetMode = "Disabled" )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		var startTransforms = new Dictionary<int, Transform>();
		foreach ( var body in Bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.Key );
			startTransforms[body.Key] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}

		InternalStartLerp( startTransforms, duration, easing, targetMode, lerpingAllBodies: true );
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpBodiesToAnimation( IEnumerable<Body> bodies, float duration, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		var startTransforms = new Dictionary<int, Transform>();
		foreach ( var body in bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex );
			startTransforms[body.BoneIndex] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}

		InternalStartLerp( startTransforms, duration, easing );
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpBodiesInRadiusToAnimation( Vector3 worldPosition, float radius, float duration, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		var affectedBodies = Bodies.Values
			.Where( b => b.Component.IsValid() && Vector3.DistanceBetween( worldPosition, b.Component.WorldPosition ) <= radius )
			.ToList();

		if ( affectedBodies.Count > 0 )
			StartLerpBodiesToAnimation( affectedBodies, duration, easing );
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpFromDisplacedTransforms( Dictionary<int, Transform> displacedTransforms, float duration, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( displacedTransforms, duration, easing );
	}

	public void StartSlerpToAnimation( float duration, string targetMode = "Disabled" )
	{
		StartLerpToAnimation( duration, LerpEasing.EaseIn, targetMode );
	}

	[Button( "Test Lerp All" )]
	public void TestLerpToAnimation()
	{
		StartLerpToAnimation( 2f, LerpEasing.EaseInOut, ShrimpleRagdollMode.Disabled );
	}
}
