namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	protected struct ActiveHitReaction
	{
		public int BoneIndex;
		public Transform DisplacedTransform;
		public Transform OriginalTransform;
		public Dictionary<int, Transform> ChildOriginalTransforms;
		public Dictionary<int, Transform> TranslationOriginalTransforms;
		public Dictionary<int, Vector3> TranslationOffsets;
		public TimeUntil TimeUntilDone;
		public float Duration;
		public LerpEasing Easing;
	}

	protected List<ActiveHitReaction> ActiveHitReactions { get; set; } = new();

	public void ApplyHitReaction( Vector3 hitPosition, Vector3 force, float radius = 30f, float duration = 0.5f, LerpEasing easing = LerpEasing.AnticipateOvershoot, float rotationStrength = 15f )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		// Find the nearest body to the hit position
		Body? impactBody = null;
		var closestDistance = float.MaxValue;

		foreach ( var body in Bodies.Values )
		{
			var bonePos = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex ).Position;
			var distance = Vector3.DistanceBetween( hitPosition, bonePos );

			if ( distance < closestDistance )
			{
				closestDistance = distance;
				impactBody = body;
			}
		}

		if ( impactBody == null )
			return;

		// Walk up the hierarchy to find the bone to rotate based on radius
		// Small radius = rotate the impact bone, larger radius = rotate a parent further up
		var targetBody = impactBody.Value;

		if ( !targetBody.IsRootBone )
		{
			var parent = targetBody.GetParent();
			while ( parent != null && !parent.Value.IsRootBone )
			{
				var parentPos = Renderer.SceneModel.GetBoneWorldTransform( parent.Value.BoneIndex ).Position;
				if ( Vector3.DistanceBetween( hitPosition, parentPos ) > radius )
					break;

				targetBody = parent.Value;
				parent = targetBody.GetParent();
			}
		}

		var boneWorldTransform = Renderer.SceneModel.GetBoneWorldTransform( targetBody.BoneIndex );
		var forceDir = force.Normal;

		// Count descendants to blend between rotation and translation
		// Many descendants = mostly translate, few = mostly rotate
		var descendantCount = targetBody.GetHierarchy().Count() - 1;
		var rotationBlend = 1f / (1f + descendantCount * 0.5f);

		Transform displacedWorld;

		if ( targetBody.IsRootBone )
		{
			// Root bone (pelvis) only gets translation
			displacedWorld = boneWorldTransform.WithPosition( boneWorldTransform.Position + force );
		}
		else
		{
			var displacedPosition = boneWorldTransform.Position + force * (1f - rotationBlend);
			var displacedRotation = boneWorldTransform.Rotation;

			var leverArm = (boneWorldTransform.Position - hitPosition).Normal;
			var rotationAxis = Vector3.Cross( leverArm, forceDir ).Normal;

			if ( rotationAxis.LengthSquared < 1e-4f )
				rotationAxis = Vector3.Cross( forceDir, boneWorldTransform.Rotation.Up ).Normal;

			if ( rotationAxis.LengthSquared > 1e-4f )
				displacedRotation = Rotation.FromAxis( rotationAxis, rotationStrength * rotationBlend ) * boneWorldTransform.Rotation;

			displacedWorld = new Transform( displacedPosition, displacedRotation, boneWorldTransform.Scale );
		}

		// Snapshot children's original world transforms for rotation propagation
		var childOriginals = new Dictionary<int, Transform>();
		foreach ( var descendant in targetBody.GetHierarchy().Skip( 1 ) )
		{
			var childWorld = Renderer.SceneModel.GetBoneWorldTransform( descendant.BoneIndex );
			childOriginals[descendant.BoneIndex] = childWorld;
		}

		// Gather nearby bones for radius-based translation (the old splash behavior)
		var translationOriginals = new Dictionary<int, Transform>();
		var translationOffsets = new Dictionary<int, Vector3>();

		foreach ( var body in Bodies.Values )
		{
			// Skip the target bone itself and its descendants (they're handled by rotation propagation)
			if ( body.BoneIndex == targetBody.BoneIndex || childOriginals.ContainsKey( body.BoneIndex ) )
				continue;

			var bodyWorldTransform = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex );
			var distance = Vector3.DistanceBetween( hitPosition, bodyWorldTransform.Position );

			if ( distance > radius )
				continue;

			var falloff = 1f - (distance / radius);
			falloff *= falloff;

			translationOriginals[body.BoneIndex] = bodyWorldTransform;
			translationOffsets[body.BoneIndex] = force * falloff;
		}

		ActiveHitReactions.Add( new ActiveHitReaction
		{
			BoneIndex = targetBody.BoneIndex,
			DisplacedTransform = Renderer.WorldTransform.ToLocal( displacedWorld ),
			OriginalTransform = Renderer.WorldTransform.ToLocal( boneWorldTransform ),
			ChildOriginalTransforms = childOriginals,
			TranslationOriginalTransforms = translationOriginals,
			TranslationOffsets = translationOffsets,
			TimeUntilDone = duration,
			Duration = duration,
			Easing = easing
		} );
	}

	/// <summary>
	/// Apply a directional hit reaction (e.g., bullet impact)
	/// </summary>
	public void ApplyDirectionalHitReaction( Vector3 hitPosition, Vector3 direction, float forceMagnitude = 5f, float radius = 30f, float duration = 0.5f, float rotationStrength = 15f )
	{
		ApplyHitReaction( hitPosition, direction.Normal * forceMagnitude, radius, duration, rotationStrength: rotationStrength );
	}

	/// <summary>
	/// Update all active hit reactions, called from ComputeVisuals
	/// </summary>
	internal void UpdateHitReactions()
	{
		if ( ActiveHitReactions.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		for ( var i = ActiveHitReactions.Count - 1; i >= 0; i-- )
		{
			var reaction = ActiveHitReactions[i];

			if ( reaction.TimeUntilDone )
			{
				ActiveHitReactions.RemoveAt( i );
				continue;
			}

			var t = reaction.Easing.Apply( reaction.TimeUntilDone.Fraction );
			var currentLocal = reaction.DisplacedTransform.LerpTo( reaction.OriginalTransform, t, false );
			Renderer.SceneModel.SetBoneOverride( reaction.BoneIndex, in currentLocal );

			// Propagate the transform change to all children in the hierarchy
			// using snapshotted original transforms to avoid feedback loops
			if ( reaction.ChildOriginalTransforms == null || reaction.ChildOriginalTransforms.Count == 0 )
				continue;

			var originalWorld = Renderer.WorldTransform.ToWorld( reaction.OriginalTransform );
			var currentWorld = Renderer.WorldTransform.ToWorld( currentLocal );
			var deltaRotation = currentWorld.Rotation * originalWorld.Rotation.Inverse;
			var pivot = originalWorld.Position;
			var deltaPosition = currentWorld.Position - originalWorld.Position;

			foreach ( var (childBoneIndex, childOriginalWorld) in reaction.ChildOriginalTransforms )
			{
				var rotatedPosition = pivot + deltaRotation * (childOriginalWorld.Position - pivot) + deltaPosition;
				var rotatedRotation = deltaRotation * childOriginalWorld.Rotation;
				var childDisplaced = new Transform( rotatedPosition, rotatedRotation, childOriginalWorld.Scale );
				var childLocal = Renderer.WorldTransform.ToLocal( childDisplaced );
				Renderer.SceneModel.SetBoneOverride( childBoneIndex, in childLocal );
			}
		}
	}

	private struct DebugHitRay
	{
		public Vector3 Position;
		public Vector3 Direction;
		public RealTimeUntil Expiry;
	}

	private List<DebugHitRay> _debugHitRays = new();

	[Button( "Debug Hit Reaction" )]
	public void DebugHitReaction()
	{
		if ( !Renderer.IsValid() || Bodies == null || Bodies.Count == 0 )
			return;

		var random = new Random();

		// Pick a random height along the body's vertical span
		var minZ = float.MaxValue;
		var maxZ = float.MinValue;

		foreach ( var body in Bodies.Values )
		{
			var pos = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex ).Position;
			if ( pos.z < minZ ) minZ = pos.z;
			if ( pos.z > maxZ ) maxZ = pos.z;
		}

		var targetZ = random.Float( minZ, maxZ );

		Body? closestBody = null;
		var closestDist = float.MaxValue;

		foreach ( var body in Bodies.Values )
		{
			var boneZ = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex ).Position.z;
			var dist = MathF.Abs( boneZ - targetZ );
			if ( dist < closestDist )
			{
				closestDist = dist;
				closestBody = body;
			}
		}

		if ( closestBody == null )
			return;

		var bonePosition = Renderer.SceneModel.GetBoneWorldTransform( closestBody.Value.BoneIndex ).Position;
		var direction = new Vector3( random.Float( -1f, 1f ), random.Float( -1f, 1f ), random.Float( -0.5f, 0.5f ) ).Normal;

		_debugHitRays.Add( new DebugHitRay
		{
			Position = bonePosition,
			Direction = direction,
			Expiry = 1f
		} );

		ApplyHitReaction( bonePosition, direction, 1f, 0.2f );
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		for ( var i = _debugHitRays.Count - 1; i >= 0; i-- )
		{
			var ray = _debugHitRays[i];

			if ( ray.Expiry )
			{
				_debugHitRays.RemoveAt( i );
				continue;
			}

			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.SolidSphere( ray.Position, 1f );
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.Arrow( ray.Position, ray.Position + ray.Direction * 15f, 1f, 2f );
		}
	}
}
