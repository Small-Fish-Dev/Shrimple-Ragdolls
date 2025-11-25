public partial class ShrimpleActiveRagdoll
{
	public record struct Body( Rigidbody Component, int Bone, List<Collider> Colliders, BoneCollection.Bone Parent = null, List<BoneCollection.Bone> Children = null, bool IsValid = false )
	{
		public Body WithColliders( List<Collider> colliders )
			=> this with { Colliders = colliders };

		public Body WithComponent( Rigidbody component )
			=> this with { Component = component };

		public Body WithBone( int bone )
			=> this with { Bone = bone };

		public Body WithParent( BoneCollection.Bone parent )
			=> this with { Parent = parent };

		public Body WithChildren( List<BoneCollection.Bone> children )
			=> this with { Children = children };
	}

	public Dictionary<BoneCollection.Bone, Body> Bodies { get; protected set; } = new();

	protected void CreateBodies( PhysicsGroupDescription physics )
	{
		foreach ( var part in physics.Parts )
		{
			var bone = Model.Bones.GetBone( part.BoneName );

			if ( !BoneObjects.TryGetValue( bone, out var boneObject ) )
				continue;

			if ( !boneObject.Flags.Contains( GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone ) )
			{
				boneObject.Flags |= GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone;

				if ( !Renderer.IsValid() || !Renderer.TryGetBoneTransform( in bone, out var boneTransform ) )
					boneTransform = Renderer.WorldTransform.ToWorld( part.Transform );

				boneObject.WorldTransform = boneTransform;
			}

			//var child = rigidbody.WorldTransform;
			//BodyTransforms.Set( Bodies.Count, child );

			var rigidbody = boneObject.AddComponent<Rigidbody>( startEnabled: false );
			var colliders = AddColliders( boneObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone, new Body( rigidbody, bone.Index, colliders, IsValid: true ) );
		}

		SetBodyHierarchyReferences();

		//rigidbody.PhysicsBody.RebuildMass();
	}


	protected void SetBodyHierarchyReferences()
	{
		foreach ( var kvp in Bodies.ToList() )
		{
			var validParentBone = GetNearestValidParentBody( kvp.Key.Parent );
			if ( validParentBone == null )
				continue;
			var newBody = kvp.Value.WithParent( GetBoneByBody( validParentBone.Value ) );

			if ( kvp.Key.Children != null && kvp.Key.Children.Count() > 0 )
			{
				var childrenBodies = new List<BoneCollection.Bone>();
				foreach ( var childBone in kvp.Key.Children )
				{
					var childBody = GetNearestValidChildBody( childBone );
					if ( childBody != null )
						childrenBodies.Add( GetBoneByBody( childBody.Value ) );
				}
				newBody = newBody.WithChildren( childrenBodies );
			}

			Bodies[kvp.Key] = newBody;
		}
	}

	protected void CreateStatueBodies( PhysicsGroupDescription physics )
	{
		var rigidbody = Renderer.GameObject.AddComponent<Rigidbody>( startEnabled: false );

		foreach ( var part in physics.Parts )
		{
			var bone = Model.Bones.GetBone( part.BoneName );

			if ( !BoneObjects.TryGetValue( bone, out var boneObject ) )
				continue;

			if ( !Renderer.IsValid() || !Renderer.TryGetBoneTransform( in bone, out var boneTransform ) )
				boneTransform = Renderer.WorldTransform.ToWorld( part.Transform );

			boneObject.WorldTransform = boneTransform;

			var colliders = AddColliders( Renderer.GameObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone, new Body( rigidbody, bone.Index, colliders ) );
		}
	}

	protected IEnumerable<Collider> AddColliders( GameObject parent, PhysicsGroupDescription.BodyPart part, Transform worldTransform )
	{
		var localTransform = parent.WorldTransform.ToLocal( worldTransform );

		foreach ( var sphere in part.Spheres )
		{
			var sphereCollider = parent.AddComponent<SphereCollider>();
			sphereCollider.Center = localTransform.PointToWorld( sphere.Sphere.Center );
			sphereCollider.Radius = sphere.Sphere.Radius;
			sphereCollider.Surface = sphere.Surface;
			yield return sphereCollider;
		}
		foreach ( var capsule in part.Capsules )
		{
			var capsuleCollider = parent.AddComponent<CapsuleCollider>();
			capsuleCollider.Start = localTransform.PointToWorld( capsule.Capsule.CenterA );
			capsuleCollider.End = localTransform.PointToWorld( capsule.Capsule.CenterB );
			capsuleCollider.Radius = capsule.Capsule.Radius;
			capsuleCollider.Surface = capsule.Surface;
			yield return capsuleCollider;
		}
		foreach ( var hull in part.Hulls )
		{
			var hullCollider = parent.AddComponent<HullCollider>();
			hullCollider.Type = HullCollider.PrimitiveType.Points;
			hullCollider.Points = hull.GetPoints()
				.Select( x => localTransform.PointToWorld( x ) ) // Looks weird but we're turning local points into world points relative to the parent which is still local in the grand scheme of things
				.ToList();
			hullCollider.Surface = hull.Surface;
			hullCollider.Center = localTransform.Position;
			yield return hullCollider;
		}
	}

	/// <summary>
	/// Destroy all rigidbody components and colliders, then clear the bodies list
	/// </summary>
	protected void DestroyBodies( bool keepTransform = true )
	{
		if ( Bodies == null )
			return;

		var rigidbodies = new HashSet<Rigidbody>();

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
				rigidbodies.Add( body.Component );

			foreach ( var collider in body.Colliders )
				if ( collider.IsValid() )
					collider.Destroy();
		}

		foreach ( var rigidbody in rigidbodies )
		{
			var oldTransform = rigidbody.WorldTransform;
			rigidbody.GameObject.Flags &= ~GameObjectFlags.Absolute;
			rigidbody.GameObject.Flags &= ~GameObjectFlags.PhysicsBone;

			if ( keepTransform && rigidbody.IsValid() )
				rigidbody.WorldTransform = oldTransform;

			rigidbody.Destroy();
		}

		Bodies.Clear();
	}

	/// <summary>
	/// Disables all the rigidbodies and colliders
	/// </summary>
	protected void DisableBodies( bool keepTransform = true )
	{
		if ( Bodies == null )
			return;

		var rigidbodies = new HashSet<Rigidbody>();

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				rigidbodies.Add( body.Component );

				foreach ( var collider in body.Colliders )
				{
					if ( collider.IsValid() )
						collider.Enabled = false;
				}
			}
		}

		foreach ( var rigidbody in rigidbodies ) // This avoids doing this stuff multiple times to the same rigidbody if it is used for multiple bones
		{
			var oldTransform = rigidbody.WorldTransform;
			rigidbody.GameObject.Flags &= ~GameObjectFlags.Absolute;
			rigidbody.GameObject.Flags &= ~GameObjectFlags.PhysicsBone;
			rigidbody.Enabled = false;

			if ( keepTransform && rigidbody.IsValid() )
				rigidbody.WorldTransform = oldTransform;
		}
	}

	/// <summary>
	/// Enables all the rigidbodies and colliders
	/// </summary>
	protected void EnableBodies( bool keepTransform = true )
	{
		if ( Bodies == null )
			return;

		var rigidbodies = new HashSet<Rigidbody>();

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				rigidbodies.Add( body.Component );

				foreach ( var collider in body.Colliders )
				{
					if ( collider.IsValid() )
						collider.Enabled = true;
				}
			}
		}

		foreach ( var rigidbody in rigidbodies )
		{
			var oldTransform = rigidbody.WorldTransform;
			rigidbody.GameObject.Flags |= GameObjectFlags.Absolute;
			rigidbody.GameObject.Flags |= GameObjectFlags.PhysicsBone;
			rigidbody.Enabled = true;

			if ( keepTransform && rigidbody.IsValid() )
				rigidbody.WorldTransform = oldTransform;
		}
	}
}
