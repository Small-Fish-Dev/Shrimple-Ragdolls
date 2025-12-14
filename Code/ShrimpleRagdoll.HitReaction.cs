using Sandbox.Utility;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Apply a hit reaction by ragdolling and applying force to bones near a world position
	/// </summary>
	/// <param name="hitPosition">World position where the hit occurred</param>
	/// <param name="force">Force vector to apply</param>
	/// <param name="radius">Maximum distance from hit position to affect bodies</param>
	/// <param name="recoveryDuration">How long to lerp back (0 = no lerp back)</param>
	/// <param name="recoveryDelay">How long to wait before starting lerp back</param>
	/// <param name="recoveryEasing">Easing function for recovery (defaults to AnticipateOvershoot)</param>
	public void ApplyHitReaction( Vector3 hitPosition, Vector3 force, float radius = 30f, float recoveryDuration = 0.5f, float recoveryDelay = 0.1f, Easing.Function recoveryEasing = null )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		recoveryEasing ??= Easing.AnticipateOvershoot;
		var affectedBodies = new List<(Body body, string originalMode)>();

		// Find affected bodies, set to ragdoll, and apply force
		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() )
				continue;

			var bodyPosition = body.Component.WorldPosition;
			var distance = Vector3.DistanceBetween( hitPosition, bodyPosition );

			if ( distance > radius )
				continue;

			// Calculate falloff (closer = stronger force)
			var falloff = 1f - (distance / radius);
			falloff = MathF.Pow( falloff, 2f );

			// Store original mode
			var originalMode = BodyModes.TryGetValue( body.BoneIndex, out var mode ) ? mode : ShrimpleRagdollMode.Disabled;
			affectedBodies.Add( (body, originalMode) );

			// Set to ragdoll mode
			SetBodyMode( body, ShrimpleRagdollMode.Enabled, includeChildren: false );

			// Apply force
			var scaledForce = force * falloff;
			body.Component.ApplyImpulse( scaledForce );
		}

		// Schedule lerp back after delay
		if ( affectedBodies.Count > 0 && recoveryDuration > 0f )
		{
			Task.RunInThreadAsync( async () =>
			{
				await Task.DelaySeconds( recoveryDelay );
				await Task.MainThread();

				// Start lerp back for each affected body
				var bodies = affectedBodies.Select( x => x.body ).ToList();
				StartLerpBodiesToAnimation( bodies, recoveryDuration, recoveryEasing );

				// After lerp completes, restore original modes
				await Task.DelaySeconds( recoveryDuration );
				await Task.MainThread();

				foreach ( var (body, originalMode) in affectedBodies )
				{
					SetBodyMode( body, originalMode, includeChildren: false );
				}
			} );
		}
	}

	/// <summary>
	/// Apply a directional hit reaction (e.g., bullet impact)
	/// </summary>
	public void ApplyDirectionalHitReaction( Vector3 hitPosition, Vector3 direction, float forceMagnitude = 500f, float radius = 30f, float recoveryDuration = 0.5f, float recoveryDelay = 0.1f )
	{
		var force = direction.Normal * forceMagnitude;
		ApplyHitReaction( hitPosition, force, radius, recoveryDuration, recoveryDelay );
	}

	/// <summary>
	/// Apply an explosive hit reaction (pushes outward from center)
	/// </summary>
	public void ApplyExplosiveHitReaction( Vector3 explosionPosition, float forceMagnitude = 1000f, float radius = 100f, float recoveryDuration = 0.8f, float recoveryDelay = 0.15f )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var affectedBodies = new List<(Body body, string originalMode, Vector3 force)>();

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

			// Store original mode
			var originalMode = BodyModes.TryGetValue( body.BoneIndex, out var mode ) ? mode : ShrimpleRagdollMode.Disabled;

			// Calculate radial force
			var direction = (bodyPosition - explosionPosition).Normal;
			var force = direction * forceMagnitude * falloff;

			affectedBodies.Add( (body, originalMode, force) );

			// Set to ragdoll mode
			SetBodyMode( body, ShrimpleRagdollMode.Enabled, includeChildren: false );

			// Apply force
			body.Component.ApplyImpulse( force );
		}

		// Schedule lerp back after delay
		if ( affectedBodies.Count > 0 && recoveryDuration > 0f )
		{
			Task.RunInThreadAsync( async () =>
			{
				await Task.DelaySeconds( recoveryDelay );
				await Task.MainThread();

				// Start lerp back
				var bodies = affectedBodies.Select( x => x.body ).ToList();
				StartLerpBodiesToAnimation( bodies, recoveryDuration, Easing.AnticipateOvershoot );

				// After lerp completes, restore original modes
				await Task.DelaySeconds( recoveryDuration );
				await Task.MainThread();

				foreach ( var (body, originalMode, _) in affectedBodies )
				{
					SetBodyMode( body, originalMode, includeChildren: false );
				}
			} );
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
				forceMagnitude: 300f,
				radius: 40f,
				recoveryDuration: 0.5f,
				recoveryDelay: 0.1f
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
				forceMagnitude: 50000f,
				radius: 26f,
				recoveryDuration: 1f,
				recoveryDelay: 1f
			);
		}
	}

	[Button( "Test Explosion" )]
	public void TestExplosion()
	{
		var centerMass = GetMassCenter();
		ApplyExplosiveHitReaction(
			centerMass + WorldRotation.Forward * 50f,
			forceMagnitude: 800f,
			radius: 80f,
			recoveryDuration: 1f,
			recoveryDelay: 0.2f
		);
	}
}
