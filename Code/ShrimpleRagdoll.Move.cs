namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Move the bone's mesh based on their Rigidbody transform
	/// </summary>
	public void MoveMeshFromBodies()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var worldTransform = Renderer.WorldTransform;

		foreach ( var body in Bodies )
		{
			// Can optimize by not running this if the body is sleeping, but there's times you want it to update anyways so maybe make it a property
			var transform = worldTransform.ToLocal( body.Value.Component.PhysicsBody.GetLerpedTransform( Time.Now ) );
			Renderer.SceneModel.SetBoneOverride( body.Key, in transform );
		}
	}

	/// <summary>
	/// Move the bone's mesh based on their objects transform
	/// </summary>
	public void MoveMeshFromObjects()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var worldTransform = Renderer.WorldTransform;
		foreach ( var body in Bodies ) // We still use bodies just for the bone references
		{
			var boneObject = BoneObjects[body.Value.GetBone()];
			var transform = worldTransform.ToLocal( boneObject.WorldTransform );
			Renderer.SceneModel.SetBoneOverride( body.Key, in transform );
		}
	}

	public void MoveMeshFromObject( Body body )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var bone = body.GetBone();
		var boneObject = BoneObjects[bone];
		var transform = Renderer.WorldTransform.ToLocal( boneObject.WorldTransform );
		Renderer.SceneModel.SetBoneOverride( bone.Index, in transform );
	}

	/// <summary>
	/// Move the bone's objects based on their mesh transform
	/// </summary>
	public void MoveObjectsFromMesh()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var renderBonePositions = Renderer.GetBoneTransforms( world: true );
		var renderBoneVelocities = Renderer.GetBoneVelocities();

		if ( renderBonePositions == null || renderBoneVelocities == null )
			return;

		foreach ( var item in BoneObjects )
		{
			if ( item.Key == null || !item.Value.IsValid() || item.Key.Index >= renderBonePositions.Length || item.Key.Index >= renderBoneVelocities.Length )
				continue;

			var component = item.Value.GetComponent<Rigidbody>();
			if ( component.IsValid() )
			{
				var worldTransform = renderBonePositions[item.Key.Index];
				var boneVelocity = renderBoneVelocities[item.Key.Index];
				component.WorldTransform = worldTransform;
				component.Velocity = boneVelocity.Linear;
				component.AngularVelocity = boneVelocity.Angular;
			}
		}
	}

	public void MoveObjectFromMesh( BoneCollection.Bone bone )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;
		if ( !Renderer.TryGetBoneTransform( bone, out var renderBoneTransform ) )
			return;
		var renderBoneVelocity = Renderer.GetBoneVelocity( bone.Index );
		var boneObject = BoneObjects[bone];

		var component = boneObject.GetComponent<Rigidbody>();
		if ( component.IsValid() )
		{
			component.WorldTransform = renderBoneTransform;
			component.Velocity = renderBoneVelocity.Linear;
			component.AngularVelocity = renderBoneVelocity.Angular;
		}
	}

	/// <summary>
	/// Physically move the bone's rigidbody based on their animation transforms
	/// </summary>
	public void MoveBodyFromAnimations( Body body )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		if ( !body.Component.IsValid() || !Renderer.TryGetBoneTransformAnimation( body.GetBone(), out var transform ) )
			return;

		body.Component.SmoothMove( in transform, Time.Delta, Time.Delta );
	}

	/// <summary>
	/// Move the bone's objects based on their animation transforms
	/// </summary>
	protected void MoveObjectsFromAnimations()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var boneObject in BoneObjects )
			if ( Renderer.TryGetBoneTransformAnimation( boneObject.Key, out var transform ) )
				boneObject.Value.WorldTransform = transform;
	}

	/// <summary>
	/// Set the Renderer and Root's transform following the <see cref="RagdollFollowMode"/>
	/// </summary>
	public void MoveGameObject()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		if ( RagdollHandler.PhysicsDriven )
		{
			var targetTransform = GetRagdollTransform( FollowOptions.Bone.Selected, FollowOptions.MergeBoneTransforms );

			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Position ) )
				Renderer.WorldPosition = targetTransform.Position;

			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Rotation ) )
				Renderer.WorldRotation = targetTransform.Rotation;
		}

		if ( GameObject != Renderer.GameObject && FollowOptions.RootObjectFollow )
		{
			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Position ) )
				GameObject.WorldPosition = Renderer.WorldPosition;

			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Rotation ) )
				GameObject.WorldRotation = Renderer.WorldRotation;
		}
	}

	/// <summary>
	/// Get the ragdoll's ideal transform from the provided bone
	/// </summary>
	/// <param name="boneName">Which bone to base off of</param>
	/// <param name="mergedBoneTransforms">The final renderer's transform should match the bone's transform</param>
	/// <returns></returns>
	public Transform GetRagdollTransform( string boneName, bool mergedBoneTransforms = true )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return WorldTransform;
		var bone = Renderer.Model.Bones.GetBone( boneName );
		var currentTransform = Bodies[bone.Index].Component.GameObject.WorldTransform;
		var targetTransform = currentTransform;

		if ( mergedBoneTransforms )
		{
			var localTransform = Renderer.Model.GetBoneTransform( boneName );
			var invRotation = localTransform.Rotation.Inverse;

			// Transform the bone's world transform back to root space
			var rotatedLocalPos = currentTransform.Rotation * (localTransform.Position * invRotation);
			targetTransform = new Transform(
				currentTransform.Position - rotatedLocalPos,
				currentTransform.Rotation * invRotation
			);
		}

		return targetTransform;
	}
}
