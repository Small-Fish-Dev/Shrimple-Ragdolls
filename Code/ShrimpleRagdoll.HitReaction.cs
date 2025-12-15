using Sandbox.Utility;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Apply a hit reaction by displacing bones and lerping back to animation
	/// </summary>
	public void ApplyHitReaction( Vector3 hitPosition, Vector3 force, float radius = 30f, float duration = 0.5f, Easing.Function easing = null )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		easing ??= Easing.AnticipateOvershoot;
		var displacedTransforms = new Dictionary<int, Transform>();

		foreach ( var body in Bodies.Values )
		{
			var boneWorldTransform = Renderer.SceneModel.GetBoneWorldTransform( body.BoneIndex );
			var distance = Vector3.DistanceBetween( hitPosition, boneWorldTransform.Position );

			if ( distance > radius )
				continue;

			// Quadratic falloff - closer = stronger
			var falloff = 1f - (distance / radius);
			falloff *= falloff;

			var displaced = boneWorldTransform.WithPosition( boneWorldTransform.Position + force * falloff );
			displacedTransforms[body.BoneIndex] = Renderer.WorldTransform.ToLocal( displaced );
		}

		if ( displacedTransforms.Count > 0 )
			StartLerpFromDisplacedTransforms( displacedTransforms, duration, easing );
	}

	/// <summary>
	/// Apply a directional hit reaction (e.g., bullet impact)
	/// </summary>
	public void ApplyDirectionalHitReaction( Vector3 hitPosition, Vector3 direction, float forceMagnitude = 5f, float radius = 30f, float duration = 0.5f )
	{
		ApplyHitReaction( hitPosition, direction.Normal * forceMagnitude, radius, duration );
	}

	[Button( "Test Hit - Head" )]
	public void TestHitReactionHead()
	{
		var headBody = GetBodyByBoneName( "head" );
		if ( headBody.HasValue )
		{
			ApplyDirectionalHitReaction(
				headBody.Value.Component.WorldPosition,
				WorldRotation.Backward,
				forceMagnitude: 2f,
				radius: 15f,
				duration: 0.1f
			);
		}
	}
}
