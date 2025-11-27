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
			//if ( body.Value.Component.Sleeping || !body.Value.Component.MotionEnabled ) // Optimization, don't override if it's sleeping
			//	continue;
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
			var transform = worldTransform.ToLocal( body.Value.Component.PhysicsBody.GetLerpedTransform( Time.Now ) );
			Renderer.SceneModel.SetBoneOverride( body.Key, in transform );
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

			var boneObject = BoneObjects[body.Value.GetBone( Model )];
			Transform transform = worldTransform.ToLocal( boneObject.WorldTransform );
			Renderer.SceneModel.SetBoneOverride( body.Key, in transform );
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
			if ( !pair.Value.Component.IsValid() || !Renderer.TryGetBoneTransformAnimation( pair.Value.GetBone( Model ), out var transform ) )
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
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count() == 0 )
			return;

		if ( Mode == RagdollMode.Enabled )
		{
			var bone = Renderer.Model.Bones.GetBone( FollowOptions.Bone.Selected );
			var localTransform = Renderer.Model.GetBoneTransform( FollowOptions.Bone.Selected );
			var currentTransform = Bodies[bone.Index].Component.GameObject.WorldTransform;
			// Maybe there's a better way to get the bones to match without instantiating a new Transform but I couldn't find it haha 
			var targetTransform = currentTransform.ToWorld( new Transform( -localTransform.Position * localTransform.Rotation.Inverse, localTransform.Rotation.Inverse ) );

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

		if ( Mode == RagdollMode.Statue )
		{
			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Position ) )
			{
				if ( GameObject.Root != Renderer.GameObject && FollowOptions.RootObjectFollow )
					GameObject.Root.WorldPosition = Renderer.WorldPosition;
			}
			if ( FollowOptions.FollowMode.Contains( RagdollFollowMode.Rotation ) )
			{
				if ( GameObject.Root != Renderer.GameObject && FollowOptions.RootObjectFollow )
					GameObject.Root.WorldRotation = Renderer.WorldRotation;
			}
		}
	}
}
