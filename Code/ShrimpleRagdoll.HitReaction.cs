using Sandbox.Utility;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Apply a hit reaction by displacing bones near a world position
	/// </summary>
	/// <param name="hitPosition">World position where the hit occurred</param>
	/// <param name="displacement">Direction and strength of the displacement</param>
	/// <param name="radius">Maximum distance from hit position to affect bodies</param>
	/// <param name="recoveryDuration">How long to lerp back (0 = no lerp back)</param>
	/// <param name="recoveryEasing">Easing function for recovery (defaults to AnticipateOvershoot)</param>
	public void ApplyHitReaction( Vector3 hitPosition, Vector3 displacement, float radius = 30f, float recoveryDuration = 0.5f, Easing.Function recoveryEasing = null )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		recoveryEasing ??= Easing.AnticipateOvershoot;
		var worldTransform = Renderer.WorldTransform;
		var displacedTransforms = new Dictionary<int, Transform>();

		// Calculate displaced transforms for affected bodies
		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			var bodyPosition = body.Component.WorldPosition;
			var distance = Vector3.DistanceBetween( hitPosition, bodyPosition );

			if ( distance > radius )
				continue;

			// Calculate falloff (closer = stronger displacement)
			var falloff = 1f - (distance / radius);
			falloff = MathF.Pow( falloff, 2f );

			// Get current bone transform
			if ( !Renderer.TryGetBoneTransform( body.GetBone(), out var currentWorldTransform ) )
				continue;

			// Calculate displaced transform (position + rotation)
			var displacedWorld = currentWorldTransform.Add( displacement * falloff, true );
			var displacedLocal = worldTransform.ToLocal( displacedWorld );

			displacedTransforms[body.BoneIndex] = displacedLocal;
		}

		// Let the lerp system handle the displacement and recovery
		if ( displacedTransforms.Count > 0 && recoveryDuration > 0f )
		{
			StartLerpFromDisplacedTransforms( displacedTransforms, recoveryDuration, recoveryEasing );
		}
	}

	/// <summary>
	/// Apply a directional hit reaction (e.g., bullet impact)
	/// </summary>
	public void ApplyDirectionalHitReaction( Vector3 hitPosition, Vector3 direction, float strength = 5f, float radius = 30f, float recoveryDuration = 0.5f )
	{
		var displacement = direction.Normal * strength;
		ApplyHitReaction( hitPosition, displacement, radius, recoveryDuration );
	}

	/// <summary>
	/// Apply an explosive hit reaction (pushes outward from center)
	/// </summary>
	public void ApplyExplosiveHitReaction( Vector3 explosionPosition, float strength = 10f, float radius = 100f, float recoveryDuration = 0.8f )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var worldTransform = Renderer.WorldTransform;
		var displacedTransforms = new Dictionary<int, Transform>();

		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			var bodyPosition = body.Component.WorldPosition;
			var distance = Vector3.DistanceBetween( explosionPosition, bodyPosition );

			if ( distance > radius )
				continue;

			var falloff = 1f - (distance / radius);
			falloff = MathF.Pow( falloff, 2f );

			if ( !Renderer.TryGetBoneTransform( body.GetBone(), out var currentWorldTransform ) )
				continue;

			// Radial displacement
			var direction = (bodyPosition - explosionPosition).Normal;
			var displacedWorld = currentWorldTransform.Add( direction * strength * falloff, true );
			var displacedLocal = worldTransform.ToLocal( displacedWorld );

			displacedTransforms[body.BoneIndex] = displacedLocal;
		}

		// Let the lerp system handle the displacement and recovery
		if ( displacedTransforms.Count > 0 && recoveryDuration > 0f )
		{
			StartLerpFromDisplacedTransforms( displacedTransforms, recoveryDuration, Easing.AnticipateOvershoot );
		}
	}

	[Button( "Test Hit - Forward" )]
	public void TestHitReactionForward()
	{
		var chestBody = GetBodyByBoneName( "spine_02" );
		if ( chestBody.HasValue )
		{
			ApplyDirectionalHitReaction(
				chestBody.Value.Component.WorldPosition,
				WorldRotation.Forward,
				strength: 8f,
				radius: 40f,
				recoveryDuration: 0.5f
			);
		}
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
				strength: 50f,
				radius: 15f,
				recoveryDuration: 1f
			);
		}
	}

	[Button( "Test Explosion" )]
	public void TestExplosion()
	{
		var centerMass = GetMassCenter();
		ApplyExplosiveHitReaction(
			centerMass + WorldRotation.Forward * 50f,
			strength: 15f,
			radius: 80f,
			recoveryDuration: 1f
		);
	}
}
