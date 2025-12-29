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

public enum LerpMode
{
	/// <summary>
	/// Lerp the mesh bone overrides to animation transforms
	/// </summary>
	Mesh,
	/// <summary>
	/// Lerp the GameObjects to animation transforms
	/// </summary>
	Objects,
	/// <summary>
	/// Lerp the rigidbodies to animation transforms using physics
	/// </summary>
	Bodies
}

public partial class ShrimpleRagdoll
{
	public TimeUntil? LerpToAnimation { get; protected set; } = null;
	public Dictionary<int, Transform> LerpStartTransforms { get; protected set; } = new();
	public string LerpToAnimationTarget { get; protected set; }
	public LerpEasing LerpToAnimationEasing { get; protected set; } = LerpEasing.EaseIn;
	public LerpMode LerpToAnimationMode { get; protected set; } = LerpMode.Mesh;
	protected HashSet<int> LerpTargetBodies { get; set; } = null;
	public bool IsLerpingToAnimation => LerpToAnimation != null;

	protected void UpdateLerpAnimations()
	{
		if ( !IsLerpingToAnimation )
			return;

		switch ( LerpToAnimationMode )
		{
			case LerpMode.Mesh:
				UpdateLerpMeshToAnimation();
				break;
			case LerpMode.Objects:
				UpdateLerpObjectsToAnimation();
				break;
			case LerpMode.Bodies:
				UpdateLerpBodiesToAnimation();
				break;
		}

		if ( LerpToAnimation.Value )
			FinishLerp();
	}

	protected void UpdateLerpMeshToAnimation()
	{
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
	}

	protected void UpdateLerpObjectsToAnimation()
	{
		foreach ( var body in Bodies )
		{
			if ( LerpTargetBodies != null && !LerpTargetBodies.Contains( body.Key ) )
				continue;

			if ( !LerpStartTransforms.TryGetValue( body.Key, out var startTransform ) )
				continue;
			if ( !Renderer.TryGetBoneTransformAnimation( body.Value.GetBone(), out var animTransform ) )
				continue;

			var boneObject = BoneObjects[body.Value.GetBone()];
			startTransform = Renderer.WorldTransform.ToWorld( startTransform );
			var currentTransform = startTransform.LerpTo( animTransform, LerpToAnimationEasing.Apply( LerpToAnimation.Value.Fraction ), false );
			boneObject.WorldTransform = currentTransform;
		}
	}

	protected void UpdateLerpBodiesToAnimation()
	{
		foreach ( var body in Bodies )
		{
			if ( LerpTargetBodies != null && !LerpTargetBodies.Contains( body.Key ) )
				continue;

			if ( !LerpStartTransforms.TryGetValue( body.Key, out var startTransform ) )
				continue;
			if ( !body.Value.Component.IsValid() || !Renderer.TryGetBoneTransformAnimation( body.Value.GetBone(), out var animTransform ) )
				continue;

			startTransform = Renderer.WorldTransform.ToWorld( startTransform );
			var currentTransform = startTransform.LerpTo( animTransform, LerpToAnimationEasing.Apply( LerpToAnimation.Value.Fraction ), false );
			body.Value.Component.SmoothMove( in currentTransform, MathF.Max( LerpTime, Time.Delta ), Time.Delta );
		}
	}

	protected void FinishLerp()
	{
		var flagToRemove = LerpToAnimationMode == LerpMode.Mesh ? BodyFlags.NoVisualUpdate : BodyFlags.NoPhysicsUpdate;

		if ( LerpTargetBodies == null )
		{
			RemoveAllBodyFlags( flagToRemove );
			Mode = LerpToAnimationTarget;
		}
		else
		{
			foreach ( var boneIndex in LerpTargetBodies )
			{
				if ( Bodies.TryGetValue( boneIndex, out var body ) )
					RemoveBodyFlags( body, flagToRemove );
			}
		}

		Renderer.ClearPhysicsBones();

		LerpToAnimation = null;
		LerpStartTransforms.Clear();
		LerpTargetBodies = null;
	}

	/// <summary>
	/// Stop the current lerp without applying the target mode
	/// </summary>
	public void StopLerp()
	{
		if ( !IsLerpingToAnimation )
			return;

		var flagToRemove = LerpToAnimationMode == LerpMode.Mesh ? BodyFlags.NoVisualUpdate : BodyFlags.NoPhysicsUpdate;

		if ( LerpTargetBodies == null )
			RemoveAllBodyFlags( flagToRemove );
		else
		{
			foreach ( var boneIndex in LerpTargetBodies )
			{
				if ( Bodies.TryGetValue( boneIndex, out var body ) )
					RemoveBodyFlags( body, flagToRemove );
			}
		}

		Renderer.ClearPhysicsBones();

		LerpToAnimation = null;
		LerpStartTransforms.Clear();
		LerpTargetBodies = null;
	}

	protected void InternalStartLerp( Dictionary<int, Transform> startTransforms, float duration, LerpEasing easing, LerpMode mode = LerpMode.Mesh, string targetMode = null, bool lerpingAllBodies = false )
	{
		if ( startTransforms == null || startTransforms.Count == 0 )
			return;

		LerpStartTransforms = startTransforms;
		LerpTargetBodies = lerpingAllBodies ? null : new HashSet<int>( startTransforms.Keys );
		LerpToAnimationEasing = easing;
		LerpToAnimationMode = mode;
		LerpToAnimationTarget = targetMode;
		LerpToAnimation = MathF.Max( duration, Time.Delta );

		var flagToAdd = mode == LerpMode.Mesh ? BodyFlags.NoVisualUpdate : BodyFlags.NoPhysicsUpdate;

		if ( lerpingAllBodies )
		{
			AddAllBodyFlags( flagToAdd );
			foreach ( var (boneIndex, transform) in startTransforms )
				Renderer.SceneModel.SetBoneOverride( boneIndex, transform );
		}
		else
		{
			foreach ( var (boneIndex, transform) in startTransforms )
			{
				if ( Bodies.TryGetValue( boneIndex, out var body ) )
				{
					AddBodyFlags( body, flagToAdd );
					Renderer.SceneModel.SetBoneOverride( boneIndex, transform );
				}
			}
		}
	}

	/// <summary>
	/// Get start transforms for all bodies from their current mesh positions
	/// </summary>
	protected Dictionary<int, Transform> GetStartTransformsFromMesh()
	{
		var startTransforms = new Dictionary<int, Transform>();
		foreach ( var body in Bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.Key );
			startTransforms[body.Key] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}
		return startTransforms;
	}

	/// <summary>
	/// Get start transforms for specific bodies from their current mesh positions
	/// </summary>
	protected Dictionary<int, Transform> GetStartTransformsFromMesh( IEnumerable<Body> bodies )
	{
		var startTransforms = new Dictionary<int, Transform>();
		foreach ( var body in bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex );
			startTransforms[body.BoneIndex] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}
		return startTransforms;
	}

	/// <summary>
	/// Lerp the mesh bone overrides to animation transforms
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpMeshToAnimation( float duration, LerpEasing easing = LerpEasing.Ease, string targetMode = "Disabled" )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( GetStartTransformsFromMesh(), duration, easing, LerpMode.Mesh, targetMode, lerpingAllBodies: true );
	}

	/// <summary>
	/// Lerp specific mesh bones to animation transforms
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpMeshToAnimation( IEnumerable<Body> bodies, float duration, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( GetStartTransformsFromMesh( bodies ), duration, easing, LerpMode.Mesh );
	}

	/// <summary>
	/// Lerp the GameObjects to animation transforms
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpObjectsToAnimation( float duration, LerpEasing easing = LerpEasing.Ease, string targetMode = "Disabled" )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( GetStartTransformsFromMesh(), duration, easing, LerpMode.Objects, targetMode, lerpingAllBodies: true );
	}

	/// <summary>
	/// Lerp specific GameObjects to animation transforms
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpObjectsToAnimation( IEnumerable<Body> bodies, float duration, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( GetStartTransformsFromMesh( bodies ), duration, easing, LerpMode.Objects );
	}

	/// <summary>
	/// Lerp the rigidbodies to animation transforms using physics
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpBodiesToAnimation( float duration, LerpEasing easing = LerpEasing.Ease, string targetMode = "Disabled" )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( GetStartTransformsFromMesh(), duration, easing, LerpMode.Bodies, targetMode, lerpingAllBodies: true );
	}

	/// <summary>
	/// Lerp specific rigidbodies to animation transforms using physics
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpBodiesToAnimation( IEnumerable<Body> bodies, float duration, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( GetStartTransformsFromMesh( bodies ), duration, easing, LerpMode.Bodies );
	}

	/// <summary>
	/// Lerp bodies in a radius to animation transforms
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpInRadiusToAnimation( Vector3 worldPosition, float radius, float duration, LerpMode mode = LerpMode.Mesh, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		var affectedBodies = Bodies.Values
			.Where( b => b.Component.IsValid() && Vector3.DistanceBetween( worldPosition, b.Component.WorldPosition ) <= radius )
			.ToList();

		if ( affectedBodies.Count > 0 )
			InternalStartLerp( GetStartTransformsFromMesh( affectedBodies ), duration, easing, mode );
	}

	/// <summary>
	/// Start lerp from displaced transforms
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void StartLerpFromDisplacedTransforms( Dictionary<int, Transform> displacedTransforms, float duration, LerpMode mode = LerpMode.Mesh, LerpEasing easing = LerpEasing.Ease )
	{
		if ( IsProxy && !(Network?.Active ?? true) )
			return;

		InternalStartLerp( displacedTransforms, duration, easing, mode );
	}
}
