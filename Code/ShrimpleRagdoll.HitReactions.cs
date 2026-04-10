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
	}

	protected List<ActiveHitReaction> ActiveHitReactions { get; set; } = new();

	/// <summary>
	/// When translation ends during a hit reaction, as a fraction of the total duration.
	/// </summary>
	[Property, Group( "Hit Reaction" ), Advanced, Range( 0f, 1f ), Step( 0.05f )]
	public float HitReactionTranslationEnd { get; set; } = 0.7f;

	/// <summary>
	/// When rotation kicks in during a hit reaction, as a fraction of the total duration.
	/// </summary>
	[Property, Group( "Hit Reaction" ), Advanced, Range( 0f, 1f ), Step( 0.05f )]
	public float HitReactionRotationStart { get; set; } = 1f / 3f;

	/// <summary>
	/// Multiplier for hit reaction translation displacement.
	/// </summary>
	[Property, Group( "Hit Reaction" ), Advanced, Range( 0f, 5f ), Step( 0.1f )]
	public float HitReactionTranslationScale { get; set; } = 2f;

	/// <summary>
	/// Multiplier for hit reaction rotation displacement.
	/// </summary>
	[Property, Group( "Hit Reaction" ), Advanced, Range( 0f, 5f ), Step( 0.1f )]
	public float HitReactionRotationScale { get; set; } = 0.5f;

	public void ApplyHitReaction( Vector3 hitPosition, Vector3 force, float radius = 30f, float duration = 0.5f, float rotationStrength = 15f )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		ModelPhysics.Body? impactBody = null;
		var closestDistance = float.MaxValue;

		foreach ( var body in Bodies )
		{
			var bonePos = Renderer.SceneModel.GetBoneWorldTransform( body.Bone ).Position;
			var distance = Vector3.DistanceBetween( hitPosition, bonePos );

			if ( distance < closestDistance )
			{
				closestDistance = distance;
				impactBody = body;
			}
		}

		if ( impactBody == null )
			return;

		var targetBody = impactBody.Value;

		if ( !IsRootBody( targetBody ) )
		{
			var parent = GetParentBody( targetBody );
			while ( parent != null && !IsRootBody( parent.Value ) )
			{
				var parentPos = Renderer.SceneModel.GetBoneWorldTransform( parent.Value.Bone ).Position;
				if ( Vector3.DistanceBetween( hitPosition, parentPos ) > radius )
					break;

				targetBody = parent.Value;
				parent = GetParentBody( targetBody );
			}
		}

		var boneWorldTransform = Renderer.SceneModel.GetBoneWorldTransform( targetBody.Bone );
		var forceMagnitude = force.Length;
		var forceDir = force.Normal;

		var descendantCount = GetDescendants( targetBody ).Count();
		var totalBodies = Bodies.Count;
		var descendantRatio = totalBodies > 1 ? (float)descendantCount / (totalBodies - 1) : 0f;
		var rotationBlend = 1f - descendantRatio;

		Transform displacedWorld;

		if ( IsRootBody( targetBody ) )
		{
			displacedWorld = boneWorldTransform.WithPosition( boneWorldTransform.Position + force );
		}
		else
		{
			var displacedPosition = boneWorldTransform.Position + force * HitReactionTranslationScale;
			var displacedRotation = boneWorldTransform.Rotation;

			var leverArm = (boneWorldTransform.Position - hitPosition).Normal;
			var rotationAxis = Vector3.Cross( forceDir, leverArm ).Normal;

			if ( rotationAxis.LengthSquared < 1e-4f )
				rotationAxis = Vector3.Cross( forceDir, boneWorldTransform.Rotation.Up ).Normal;

			if ( rotationAxis.LengthSquared > 1e-4f )
				displacedRotation = Rotation.FromAxis( rotationAxis, rotationStrength * HitReactionRotationScale * forceMagnitude * rotationBlend ) * boneWorldTransform.Rotation;

			displacedWorld = new Transform( displacedPosition, displacedRotation, boneWorldTransform.Scale );
		}

		var childOriginals = new Dictionary<int, Transform>();
		foreach ( var descendant in GetDescendants( targetBody ) )
		{
			var childWorld = Renderer.SceneModel.GetBoneWorldTransform( descendant.Bone );
			childOriginals[descendant.Bone] = childWorld;
		}

		var translationOriginals = new Dictionary<int, Transform>();
		var translationOffsets = new Dictionary<int, Vector3>();

		foreach ( var body in Bodies )
		{
			if ( body.Bone == targetBody.Bone || childOriginals.ContainsKey( body.Bone ) )
				continue;

			var bodyWorldTransform = Renderer.SceneModel.GetBoneWorldTransform( body.Bone );
			var distance = Vector3.DistanceBetween( hitPosition, bodyWorldTransform.Position );

			if ( distance > radius )
				continue;

			var falloff = 1f - (distance / radius);
			falloff *= falloff;

			translationOriginals[body.Bone] = bodyWorldTransform;
			translationOffsets[body.Bone] = force * falloff;
		}

		ActiveHitReactions.Add( new ActiveHitReaction
		{
			BoneIndex = targetBody.Bone,
			DisplacedTransform = Renderer.WorldTransform.ToLocal( displacedWorld ),
			OriginalTransform = Renderer.WorldTransform.ToLocal( boneWorldTransform ),
			ChildOriginalTransforms = childOriginals,
			TranslationOriginalTransforms = translationOriginals,
			TranslationOffsets = translationOffsets,
			TimeUntilDone = duration,
			Duration = duration,
		} );
	}

	/// <summary>
	/// Update all active hit reactions, called from OnUpdate
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

			var fraction = reaction.TimeUntilDone.Fraction;

			var positionFraction = HitReactionTranslationEnd > 0f ? MathF.Min( fraction / HitReactionTranslationEnd, 1f ) : 1f;
			var positionBlend = MathF.Sin( positionFraction * MathF.PI );
			var position = Vector3.Lerp( reaction.OriginalTransform.Position, reaction.DisplacedTransform.Position, positionBlend );

			float rotationBlendAmount;
			if ( fraction < HitReactionRotationStart )
			{
				rotationBlendAmount = 0f;
			}
			else
			{
				var rotationFraction = (fraction - HitReactionRotationStart) / (1f - HitReactionRotationStart);
				rotationBlendAmount = MathF.Sin( rotationFraction * MathF.PI );
			}

			var rotation = Rotation.Slerp( reaction.OriginalTransform.Rotation, reaction.DisplacedTransform.Rotation, rotationBlendAmount );
			var currentLocal = new Transform( position, rotation, reaction.OriginalTransform.Scale );
			Renderer.SceneModel.SetBoneOverride( reaction.BoneIndex, in currentLocal );

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

			if ( reaction.TranslationOriginalTransforms != null && reaction.TranslationOffsets != null )
			{
				foreach ( var (boneIndex, originalWorld) in reaction.TranslationOriginalTransforms )
				{
					if ( !reaction.TranslationOffsets.TryGetValue( boneIndex, out var offset ) )
						continue;

					var lerpedOffset = offset * positionBlend;
					var displacedWorld = originalWorld.WithPosition( originalWorld.Position + lerpedOffset );
					var local = Renderer.WorldTransform.ToLocal( displacedWorld );
					Renderer.SceneModel.SetBoneOverride( boneIndex, in local );
				}
			}
		}
	}

	private bool IsRootBody( ModelPhysics.Body body ) => GetParentBody( body ) == null;

	private ModelPhysics.Body? GetParentBody( ModelPhysics.Body body )
	{
		var bone = Renderer.Model.Bones.AllBones[body.Bone];
		var parentBone = bone.Parent;

		while ( parentBone != null )
		{
			var parentBody = Bodies.FirstOrDefault( b => b.Bone == parentBone.Index );
			if ( parentBody.Component.IsValid() )
				return parentBody;

			parentBone = parentBone.Parent;
		}

		return null;
	}

	private IEnumerable<ModelPhysics.Body> GetDescendants( ModelPhysics.Body root )
	{
		var rootBone = Renderer.Model.Bones.AllBones[root.Bone];
		return Bodies.Where( b => b.Bone != root.Bone && IsDescendantOf( Renderer.Model.Bones.AllBones[b.Bone], rootBone ) );
	}

	private static bool IsDescendantOf( BoneCollection.Bone bone, BoneCollection.Bone ancestor )
	{
		var parent = bone.Parent;
		while ( parent != null )
		{
			if ( parent.Index == ancestor.Index )
				return true;
			parent = parent.Parent;
		}
		return false;
	}
}
