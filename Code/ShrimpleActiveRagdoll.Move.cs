public partial class ShrimpleActiveRagdoll
{
	/// <summary>
	/// Move the bone's mesh based on their object transform
	/// </summary>
	private void MoveMeshFromObjects()
	{
		/*
		Rigidbody componentInChildren = GetComponentInChildren<Rigidbody>( includeDisabled: true ); // TODO PLACEMODE
		if ( componentInChildren.IsValid() && componentInChildren.MotionEnabled )
		{
			Renderer.WorldTransform = componentInChildren.WorldTransform;
		}*/

		if ( !Renderer.IsValid() )
		{
			return;
		}

		SceneModel sceneModel = Renderer.SceneModel;
		if ( !sceneModel.IsValid() )
		{
			return;
		}

		Renderer.ClearPhysicsBones();
		Transform worldTransform = Renderer.WorldTransform;
		foreach ( var bone in BoneObjects )
		{
			/*
			if ( !MotionEnabled && !component.MotionEnabled )
			{
				Transform transform = sceneModel.Transform.ToLocal( sceneModel.GetWorldSpaceAnimationTransform( body.Bone ) );
				sceneModel.SetBoneOverride( body.Bone, in transform );
				if ( component.Transform.SetLocalTransformFast( worldTransform.ToWorld( in transform ) ) )
				{
					component.Transform.TransformChanged( useTargetLocal: true );
				}
			}
			else
			{
				Transform transform = worldTransform.ToLocal( component.WorldTransform );
				sceneModel.SetBoneOverride( body.Bone, in transform );
			}*/


			Transform transform = worldTransform.ToLocal( bone.Value.WorldTransform );
			sceneModel.SetBoneOverride( bone.Key.Index, in transform );
		}
	}

	/// <summary>
	/// Move the bone's objects based on their mesh transform
	/// </summary>
	private void MoveObjectsFromMesh()
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

			var component = item.Value.GetComponent<Rigidbody>(); // TODO: Cache this?
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

	/// <summary>
	/// Physically move the bone's rigidbodies based on their animation transforms
	/// </summary>
	private void MoveBodiesFromAnimations()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var pair in Bodies )
		{
			if ( !pair.Value.Component.IsValid() || !Renderer.TryGetBoneTransformAnimation( pair.Key, out var transform ) )
				continue;

			pair.Value.Component.SmoothMove( in transform, 0.1f, Time.Delta );
		}
	}

	/// <summary>
	/// Move the bone's objects based on their animation transforms
	/// </summary>
	private void MoveObjectsFromAnimations()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var boneObject in BoneObjects )
			if ( Renderer.TryGetBoneTransformAnimation( boneObject.Key, out var transform ) )
				boneObject.Value.WorldTransform = transform;
	}
}
