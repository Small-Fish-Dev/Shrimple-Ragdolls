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

		// Rotate the bone based on radius, the bigger the radius the further we go down
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
		var forceMagnitude = force.Length;
		var forceDir = force.Normal;

		// Bones that control a large fraction of the body should translate more and rotate less
		var descendantCount = targetBody.GetHierarchy().Count() - 1;
		var totalBodies = Bodies.Count;
		var descendantRatio = totalBodies > 1 ? (float)descendantCount / (totalBodies - 1) : 0f;
		var rotationBlend = 1f - descendantRatio;

		Transform displacedWorld;

		if ( targetBody.IsRootBone )
		{
			displacedWorld = boneWorldTransform.WithPosition( boneWorldTransform.Position + force );
		}
		else
		{
			var displacedPosition = boneWorldTransform.Position + force * (1f - rotationBlend);
			var displacedRotation = boneWorldTransform.Rotation;

			var leverArm = (boneWorldTransform.Position - hitPosition).Normal;
			var rotationAxis = Vector3.Cross( forceDir, leverArm ).Normal;

			if ( rotationAxis.LengthSquared < 1e-4f )
				rotationAxis = Vector3.Cross( forceDir, boneWorldTransform.Rotation.Up ).Normal;

			if ( rotationAxis.LengthSquared > 1e-4f )
				displacedRotation = Rotation.FromAxis( rotationAxis, rotationStrength * forceMagnitude * rotationBlend ) * boneWorldTransform.Rotation;

			displacedWorld = new Transform( displacedPosition, displacedRotation, boneWorldTransform.Scale );
		}

		var childOriginals = new Dictionary<int, Transform>();
		foreach ( var descendant in targetBody.GetHierarchy().Skip( 1 ) )
		{
			var childWorld = Renderer.SceneModel.GetBoneWorldTransform( descendant.BoneIndex );
			childOriginals[descendant.BoneIndex] = childWorld;
		}

		// Gather nearby bones for radius-based translation splash
		var translationOriginals = new Dictionary<int, Transform>();
		var translationOffsets = new Dictionary<int, Vector3>();

		foreach ( var body in Bodies.Values )
		{
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

			// Propagate the rotation/translation to children using snapshots
			if ( reaction.ChildOriginalTransforms != null && reaction.ChildOriginalTransforms.Count > 0 )
			{
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

			// Apply radius-based translation splash to nearby non-descendant bones
			if ( reaction.TranslationOriginalTransforms != null && reaction.TranslationOffsets != null )
			{
				foreach ( var (boneIndex, originalWorld) in reaction.TranslationOriginalTransforms )
				{
					if ( !reaction.TranslationOffsets.TryGetValue( boneIndex, out var offset ) )
						continue;

					var lerpedOffset = offset * (1f - t);
					var displacedWorld = originalWorld.WithPosition( originalWorld.Position + lerpedOffset );
					var local = Renderer.WorldTransform.ToLocal( displacedWorld );
					Renderer.SceneModel.SetBoneOverride( boneIndex, in local );
				}
			}
		}
	}

	/// <summary>
	/// Shoot a hit reaction from the scene camera. Bind to a key for in-game testing.
	/// </summary>
	[ConCmd( "debug_hit" )]
	public static void DebugCameraShot()
	{
		var camera = Game.ActiveScene.Camera;
		if ( !camera.IsValid() )
			return;

		var tr = Game.ActiveScene.Trace.Ray( camera.WorldPosition + camera.WorldRotation.Forward * 15f, camera.WorldPosition + camera.WorldRotation.Forward * 5000f )
			.Run();

		if ( !tr.Hit || !tr.GameObject.Root.Components.TryGet<ShrimpleRagdoll>( out var ragdoll, FindMode.EnabledInSelfAndDescendants ) )
			return;

		var direction = camera.WorldRotation.Forward;
		ragdoll.ApplyHitReaction( tr.HitPosition, direction * 2f, 10f, 0.2f );
	}
}
