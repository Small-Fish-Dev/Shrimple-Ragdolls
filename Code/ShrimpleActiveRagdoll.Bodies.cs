public partial class ShrimpleActiveRagdoll
{
	[Sync]
	public NetDictionary<int, Body> Bodies { get; protected set; } = new();

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

			var rigidbody = boneObject.AddComponent<Rigidbody>( startEnabled: false );
			var colliders = AddColliders( boneObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone.Index, new Body( rigidbody, bone.Index, colliders ) );
		}

		SetBodyHierarchyReferences();
	}


	protected void SetBodyHierarchyReferences()
	{
		foreach ( var kvp in Bodies.ToList() )
		{
			var validParentBone = GetNearestValidParentBody( kvp.Value.GetBone( Model ).Parent );
			if ( validParentBone == null )
				continue;
			var newBody = kvp.Value.WithParent( GetBoneByBody( validParentBone.Value ) );
			var children = kvp.Value.GetBone( Model ).Children;
			if ( children != null && children.Count() > 0 )
			{
				var childrenBodies = new List<BoneCollection.Bone>();
				foreach ( var childBone in children )
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

			var colliders = AddColliders( boneObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone.Index, new Body( rigidbody, bone.Index, colliders ) );
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

	/// <summary>
	/// Return the nearest valid parent body of this body.
	/// </summary>
	/// <param name="body"></param>
	/// <returns>null if no valid parent body is found.</returns>
	public Body? GetParentBody( Body body )
	{
		var parentBone = body.GetParentBone( Model );
		if ( parentBone == null )
			return null;

		return GetNearestValidParentBody( parentBone );
	}

	/// <summary>
	/// Return all valid child bodies of this body.
	/// </summary>
	/// <param name="body"></param>
	/// <returns></returns>
	public IEnumerable<Body> GetChildrenBodies( Body body )
	{
		var childrenBones = body.GetChildrenBones( Model );

		if ( childrenBones != null )
		{
			foreach ( var childBone in childrenBones )
			{
				var childBody = GetNearestValidChildBody( childBone );
				if ( childBody != null )
					yield return childBody.Value;
			}
		}
	}

	/// <summary>
	/// Retrieves the joint that connects the specified body to its parent body, if such a joint exists.
	/// </summary>
	/// <param name="body"></param>
	/// <returns>null if no such joint exists.</returns>
	public Joint? GetParentJoint( Body body )
	{
		var parentBody = GetParentBody( body );
		if ( parentBody == null )
			return null;

		foreach ( var joint in Joints )
		{
			if ( joint.Body1 == parentBody.Value && joint.Body2 == body )
				return joint;
		}

		return null;
	}

	public struct Body
	{
		public Rigidbody Component;
		public Model Model;
		private int _boneIndex;
		public List<Collider> Colliders = new();
		private int _parentIndex;
		private List<int> _childIndexes = new();
		public bool IsValid = false;

		public Body( Rigidbody component, int bone, List<Collider> colliders, int parent = -1, List<int> children = null, bool isValid = true )
		{
			Component = component;
			_boneIndex = bone;
			Colliders = colliders;
			_parentIndex = parent;
			_childIndexes = children;
			IsValid = isValid;
		}

		public Body WithColliders( List<Collider> colliders )
		{
			return this with { Colliders = colliders };
		}

		public Body WithComponent( Rigidbody component )
		{
			return this with { Component = component };
		}

		public Body WithBone( BoneCollection.Bone bone )
		{
			return this with { _boneIndex = bone.Index };
		}

		public Body WithParent( BoneCollection.Bone parent )
		{
			return this with { _parentIndex = parent.Index };
		}

		public Body WithChildren( List<BoneCollection.Bone> children )
		{
			return this with { _childIndexes = children?.Select( x => x.Index ).ToList() };
		}

		public BoneCollection.Bone GetBone( Model model ) => model.Bones.AllBones[_boneIndex];
		public BoneCollection.Bone GetParentBone( Model model ) => model.Bones.AllBones[_parentIndex];
		public List<BoneCollection.Bone> GetChildrenBones( Model model ) => _childIndexes?.Select( x => model.Bones.AllBones[x] ).ToList();

		public static bool operator ==( Body left, Body right )
		{
			return left._boneIndex == right._boneIndex && left._parentIndex == right._parentIndex;
		}

		public static bool operator !=( Body left, Body right )
		{
			return !(left == right);
		}

		public override bool Equals( object obj )
		{
			return obj is Body other && this == other;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine( _boneIndex, _parentIndex );
		}
	}
}
