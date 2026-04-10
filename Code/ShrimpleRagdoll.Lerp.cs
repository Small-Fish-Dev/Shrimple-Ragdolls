using Sandbox.Utility;

namespace ShrimpleRagdolls;

public enum LerpMode
{
	Mesh,
	Objects,
	Bodies
}

public partial class ShrimpleRagdoll
{
	private TimeUntil? _lerpToAnimation;
	private Dictionary<int, Transform> _lerpStartTransforms = new();
	private RagdollMode? _lerpTargetMode;
	private LerpMode _lerpMode;
	private HashSet<int> _lerpTargetBones;
	public bool IsLerping => _lerpToAnimation != null;

	public void UpdateLerp()
	{
		if ( !IsLerping )
			return;

		switch ( _lerpMode )
		{
			case LerpMode.Mesh:
				UpdateLerpMesh();
				break;
			case LerpMode.Objects:
				UpdateLerpObjects();
				break;
			case LerpMode.Bodies:
				UpdateLerpBodies();
				break;
		}

		if ( _lerpToAnimation.Value )
			FinishLerp();
	}

	private void UpdateLerpMesh()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var fraction = Easing.SineEaseInOut( _lerpToAnimation.Value.Fraction );

		foreach ( var body in Bodies )
		{
			if ( _lerpTargetBones != null && !_lerpTargetBones.Contains( body.Bone ) )
				continue;

			if ( !_lerpStartTransforms.TryGetValue( body.Bone, out var startTransform ) )
				continue;

			var bone = Renderer.Model.Bones.AllBones[body.Bone];
			if ( !Renderer.TryGetBoneTransformAnimation( bone, out var animTransform ) )
				continue;

			startTransform = Renderer.WorldTransform.ToWorld( startTransform );
			var currentTransform = startTransform.LerpTo( animTransform, fraction, false );
			currentTransform = Renderer.WorldTransform.ToLocal( currentTransform );
			Renderer.SceneModel.SetBoneOverride( body.Bone, in currentTransform );
		}
	}

	private void UpdateLerpObjects()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var fraction = Easing.SineEaseInOut( _lerpToAnimation.Value.Fraction );

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() )
				continue;
			if ( _lerpTargetBones != null && !_lerpTargetBones.Contains( body.Bone ) )
				continue;

			if ( !_lerpStartTransforms.TryGetValue( body.Bone, out var startTransform ) )
				continue;

			var bone = Renderer.Model.Bones.AllBones[body.Bone];
			if ( !Renderer.TryGetBoneTransformAnimation( bone, out var animTransform ) )
				continue;

			startTransform = Renderer.WorldTransform.ToWorld( startTransform );
			var currentTransform = startTransform.LerpTo( animTransform, fraction, false );
			body.Component.WorldTransform = currentTransform;
		}
	}

	private void UpdateLerpBodies()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var fraction = Easing.SineEaseInOut( _lerpToAnimation.Value.Fraction );

		foreach ( var body in Bodies )
		{
			if ( !body.Component.IsValid() || !body.Component.PhysicsBody.IsValid() )
				continue;
			if ( _lerpTargetBones != null && !_lerpTargetBones.Contains( body.Bone ) )
				continue;

			if ( !_lerpStartTransforms.TryGetValue( body.Bone, out var startTransform ) )
				continue;

			var bone = Renderer.Model.Bones.AllBones[body.Bone];
			if ( !Renderer.TryGetBoneTransformAnimation( bone, out var animTransform ) )
				continue;

			startTransform = Renderer.WorldTransform.ToWorld( startTransform );
			var currentTransform = startTransform.LerpTo( animTransform, fraction, false );
			body.Component.PhysicsBody.SmoothMove( in currentTransform, MathF.Max( Time.Delta, Time.Delta ), Time.Delta );
		}
	}

	private void FinishLerp()
	{
		Renderer?.ClearPhysicsBones();

		if ( _lerpTargetMode.HasValue )
			Mode = _lerpTargetMode.Value;

		_lerpToAnimation = null;
		_lerpStartTransforms.Clear();
		_lerpTargetBones = null;
		_lerpTargetMode = null;
	}

	public void StopLerp()
	{
		if ( !IsLerping )
			return;

		Renderer?.ClearPhysicsBones();

		_lerpToAnimation = null;
		_lerpStartTransforms.Clear();
		_lerpTargetBones = null;
		_lerpTargetMode = null;
	}

	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	private void StartLerpInternal( Dictionary<int, Transform> startTransforms, float duration, LerpMode mode, RagdollMode? targetMode = null, bool allBodies = false )
	{
		if ( startTransforms == null || startTransforms.Count == 0 )
			return;

		_lerpStartTransforms = startTransforms;
		_lerpTargetBones = allBodies ? null : new HashSet<int>( startTransforms.Keys );
		_lerpMode = mode;
		_lerpTargetMode = targetMode;
		_lerpToAnimation = MathF.Max( duration, Time.Delta );

		if ( Renderer.IsValid() && Renderer.SceneModel.IsValid() )
		{
			foreach ( var (boneIndex, transform) in startTransforms )
				Renderer.SceneModel.SetBoneOverride( boneIndex, transform );
		}
	}

	private Dictionary<int, Transform> GetStartTransformsFromMesh()
	{
		var startTransforms = new Dictionary<int, Transform>();

		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return startTransforms;

		foreach ( var body in Bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.Bone );
			startTransforms[body.Bone] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}

		return startTransforms;
	}

	private Dictionary<int, Transform> GetStartTransformsFromMesh( IEnumerable<ModelPhysics.Body> bodies )
	{
		var startTransforms = new Dictionary<int, Transform>();

		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return startTransforms;

		foreach ( var body in bodies )
		{
			var renderBonePosition = Renderer.SceneModel.GetBoneWorldTransform( body.Bone );
			startTransforms[body.Bone] = Renderer.WorldTransform.ToLocal( renderBonePosition );
		}

		return startTransforms;
	}

	public void StartLerpMeshToAnimation( float duration, RagdollMode targetMode = RagdollMode.None ) => StartLerpInternal( GetStartTransformsFromMesh(), duration, LerpMode.Mesh, targetMode, allBodies: true );
	public void StartLerpMeshToAnimation( IEnumerable<ModelPhysics.Body> bodies, float duration ) => StartLerpInternal( GetStartTransformsFromMesh( bodies ), duration, LerpMode.Mesh );
	public void StartLerpObjectsToAnimation( float duration, RagdollMode targetMode = RagdollMode.None ) => StartLerpInternal( GetStartTransformsFromMesh(), duration, LerpMode.Objects, targetMode, allBodies: true );
	public void StartLerpObjectsToAnimation( IEnumerable<ModelPhysics.Body> bodies, float duration ) => StartLerpInternal( GetStartTransformsFromMesh( bodies ), duration, LerpMode.Objects );
	public void StartLerpBodiesToAnimation( float duration, RagdollMode targetMode = RagdollMode.None ) => StartLerpInternal( GetStartTransformsFromMesh(), duration, LerpMode.Bodies, targetMode, allBodies: true );
	public void StartLerpBodiesToAnimation( IEnumerable<ModelPhysics.Body> bodies, float duration ) => StartLerpInternal( GetStartTransformsFromMesh( bodies ), duration, LerpMode.Bodies );
	public void StartLerpFromDisplacedTransforms( Dictionary<int, Transform> displacedTransforms, float duration, LerpMode mode = LerpMode.Mesh ) => StartLerpInternal( displacedTransforms, duration, mode );

	public void StartLerpInRadiusToAnimation( Vector3 worldPosition, float radius, float duration, LerpMode mode = LerpMode.Mesh )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		var affectedBodies = Bodies
			.Where( b => b.Component.IsValid() && Vector3.DistanceBetween( worldPosition, b.Component.WorldPosition ) <= radius )
			.ToList();

		if ( affectedBodies.Count > 0 )
			StartLerpInternal( GetStartTransformsFromMesh( affectedBodies ), duration, mode );
	}
}
