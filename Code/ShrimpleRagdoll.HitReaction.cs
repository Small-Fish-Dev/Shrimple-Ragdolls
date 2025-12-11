using Sandbox.Utility;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Apply a hit reaction at a world position, affecting nearby bodies
	/// </summary>
	/// <param name="hitPosition">World position where the hit occurred</param>
	/// <param name="force">Base force to apply</param>
	/// <param name="radius">Maximum distance from hit position to affect bodies</param>
	/// <param name="lerpBackDuration">How long to lerp back to animation (0 = don't lerp back)</param>
	/// <param name="lerpBackEasing">Easing function for the lerp back</param>
	public void ApplyHitReaction( Vector3 hitPosition, Vector3 force, float radius = 30f, float lerpBackDuration = 0.5f, Easing.Function lerpBackEasing = null )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		lerpBackEasing ??= Easing.EaseOut;
		var affectedBodies = new List<Body>();

		// First pass: Find affected bodies and set them to ragdoll mode
		foreach ( var body in Bodies.Values )
		{

			var bodyPosition = body.Component.WorldPosition;
			var distance = Vector3.DistanceBetween( hitPosition, bodyPosition );

			// Only affect bodies within radius
			if ( distance > radius )
				continue;

			affectedBodies.Add( body );

			// Set to ragdoll mode (Enabled)
			SetBodyMode( body, ShrimpleRagdollMode.Enabled, includeChildren: false );
		}

		if ( affectedBodies.Count == 0 )
			return;

		// Second pass: Apply forces to the now-ragdolled bodies
		foreach ( var body in affectedBodies )
		{
			var bodyPosition = body.Component.WorldPosition;
			var distance = Vector3.DistanceBetween( hitPosition, bodyPosition );

			// Calculate falloff based on distance (closer = stronger)
			var falloff = 1f - (distance / radius);
			falloff = MathF.Pow( falloff, 2f ); // Square for more dramatic falloff

			// Apply force to the body
			var scaledForce = force * falloff;
			body.Component.ApplyImpulse( scaledForce );
		}

		// Third pass: Start lerp back to animation
		if ( lerpBackDuration > 0f )
		{
			StartLerpToAnimation( lerpBackDuration, lerpBackEasing, Mode.Name );
		}
	}

	/// <summary>
	/// Apply a hit reaction from a direction, useful for bullet hits
	/// </summary>
	/// <param name="hitPosition">World position where the hit occurred</param>
	/// <param name="direction">Direction of the force (will be normalized)</param>
	/// <param name="forceMagnitude">How strong the force is</param>
	/// <param name="radius">Maximum distance from hit position to affect bodies</param>
	/// <param name="lerpBackDuration">How long to lerp back to animation (0 = don't lerp back)</param>
	public void ApplyDirectionalHitReaction( Vector3 hitPosition, Vector3 direction, float forceMagnitude = 500f, float radius = 30f, float lerpBackDuration = 0.5f )
	{
		var force = direction.Normal * forceMagnitude;
		ApplyHitReaction( hitPosition, force, radius, lerpBackDuration, Easing.EaseOut );
	}

	/// <summary>
	/// Apply a hit reaction that affects a specific body part and nearby bodies
	/// </summary>
	/// <param name="boneName">Name of the bone to hit</param>
	/// <param name="force">Force to apply</param>
	/// <param name="radius">Radius to affect nearby bodies</param>
	/// <param name="lerpBackDuration">How long to lerp back to animation</param>
	public void ApplyBoneHitReaction( string boneName, Vector3 force, float radius = 30f, float lerpBackDuration = 0.5f )
	{
		var body = GetBodyByBoneName( boneName );
		if ( !body.HasValue || !body.Value.Component.IsValid() )
			return;

		var hitPosition = body.Value.Component.WorldPosition;
		ApplyHitReaction( hitPosition, force, radius, lerpBackDuration );
	}

	/// <summary>
	/// Apply an explosive hit reaction from a point
	/// </summary>
	/// <param name="explosionPosition">Center of the explosion</param>
	/// <param name="explosionForce">Force of the explosion</param>
	/// <param name="explosionRadius">Radius of the explosion</param>
	/// <param name="lerpBackDuration">How long to lerp back to animation</param>
	public void ApplyExplosiveHitReaction( Vector3 explosionPosition, float explosionForce = 1000f, float explosionRadius = 100f, float lerpBackDuration = 0.8f )
	{
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		var affectedBodies = new List<(Body body, Vector3 force)>();

		// First pass: Find affected bodies and calculate forces
		foreach ( var body in Bodies.Values )
		{
			if ( !body.Component.IsValid() || !body.Component.PhysicsBody.IsValid() )
				continue;

			var bodyPosition = body.Component.WorldPosition;
			var distance = Vector3.DistanceBetween( explosionPosition, bodyPosition );

			if ( distance > explosionRadius )
				continue;

			// Calculate direction and falloff
			var direction = (bodyPosition - explosionPosition).Normal;
			var falloff = 1f - (distance / explosionRadius);
			falloff = MathF.Pow( falloff, 2f );

			// Calculate force
			var force = direction * explosionForce * falloff;
			affectedBodies.Add( (body, force) );

			// Set to ragdoll mode
			SetBodyMode( body, ShrimpleRagdollMode.Enabled, includeChildren: false );
		}

		if ( affectedBodies.Count == 0 )
			return;

		// Second pass: Apply forces
		foreach ( var (body, force) in affectedBodies )
		{
			body.Component.ApplyImpulse( force );
		}

		// Start lerp back
		if ( lerpBackDuration > 0f )
		{
			StartLerpToAnimation( lerpBackDuration, Easing.EaseOut, Mode.Name );
		}
	}

	[Button( "Test Hit Reaction - Chest" )]
	public void TestHitReactionChest()
	{
		var chestBody = GetBodyByBoneName( "spine_02" );
		if ( chestBody.HasValue )
		{
			ApplyHitReaction(
				chestBody.Value.Component.WorldPosition,
				WorldRotation.Forward * 300f,
				radius: 40f,
				lerpBackDuration: 0.5f
			);
		}
	}

	[Button( "Test Hit Reaction - Head" )]
	public void TestHitReactionHead()
	{
		var headBody = GetBodyByBoneName( "head" );
		if ( headBody.HasValue )
		{
			ApplyDirectionalHitReaction(
				headBody.Value.Component.WorldPosition,
				WorldRotation.Forward,
				forceMagnitude: 200f,
				radius: 20f,
				lerpBackDuration: 0.3f
			);
		}
	}

	[Button( "Test Explosion" )]
	public void TestExplosion()
	{
		var centerMass = GetMassCenter();
		ApplyExplosiveHitReaction(
			centerMass + WorldRotation.Forward * 50f,
			explosionForce: 800f,
			explosionRadius: 80f,
			lerpBackDuration: 1f
		);
	}
}
