public partial class ShrimpleActiveRagdoll
{
	/// <summary>
	/// Move the bone's mesh based on their Rigidbody transform
	/// </summary>
	protected void MoveMeshFromBodies()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var worldTransform = Renderer.WorldTransform;
		foreach ( var body in Bodies )
		{
			if ( body.Value.Component.Sleeping || !body.Value.Component.MotionEnabled ) // Optimization, don't override if it's sleeping
				continue;
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

			//if ( body.Value.Component.Sleeping )
			//	continue;

			var transform = worldTransform.ToLocal( body.Value.Component.GameObject.WorldTransform );
			Renderer.SceneModel.SetBoneOverride( body.Key.Index, in transform );
		}
	}

	/// <summary>
	/// Move the bone's mesh based on their objects transform
	/// </summary>
	protected void MoveMeshFromObjects()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		var worldTransform = Renderer.WorldTransform;
		foreach ( var body in Bodies ) // We still use bodies just for the bone references
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

			var boneObject = BoneObjects[body.Key];
			Transform transform = worldTransform.ToLocal( boneObject.WorldTransform );
			Renderer.SceneModel.SetBoneOverride( body.Key.Index, in transform );
		}
	}

	/// <summary>
	/// Move the bone's objects based on their mesh transform
	/// </summary>
	protected void MoveObjectsFromMesh()
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
	protected void MoveBodiesFromAnimations()
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
	protected void MoveObjectsFromAnimations()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var boneObject in BoneObjects )
			if ( Renderer.TryGetBoneTransformAnimation( boneObject.Key, out var transform ) )
				boneObject.Value.WorldTransform = transform;
	}

	/// <summary>
	/// Follow the <see cref="RagdollFollowMode"/>
	/// </summary>
	protected void MoveGameObject()
	{
		if ( !PhysicsWereCreated )
			return;

		if ( Mode == RagdollMode.Enabled )
		{
			var bone = Renderer.Model.Bones.GetBone( FollowOptions.Bone.Selected );
			Renderer.TryGetBoneTransformAnimation( bone, out var animationTransform );
			var currentTransform = Bodies[bone].Component.GameObject.WorldTransform;
			var targetTransform = currentTransform.ToWorld( animationTransform.ToLocal( Renderer.WorldTransform ) );

			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Position ) )
			{
				Renderer.WorldPosition = targetTransform.Position;

				if ( GameObject.Root != Renderer.GameObject && FollowOptions.RootObjectFollow )
					GameObject.Root.WorldPosition = Renderer.WorldPosition;
			}
			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Rotation ) )
			{
				Renderer.WorldRotation = targetTransform.Rotation;

				if ( GameObject.Root != Renderer.GameObject && FollowOptions.RootObjectFollow )
					GameObject.Root.WorldRotation = Renderer.WorldRotation;
			}
		}
	}
}
