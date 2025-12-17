namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	[Sync]
	public NetDictionary<int, Body> Bodies { get; protected set; } = new();

	[Sync]
	public NetDictionary<int, string> BodyModes { get; protected set; } = new();

	[Sync]
	public NetDictionary<int, BodyFlags> AllBodyFlags { get; protected set; } = new();

	protected void CreateBodies( PhysicsGroupDescription physics )
	{
		if ( !Renderer.IsValid() )
			return;

		foreach ( var part in physics.Parts )
			CreateBody( part );

		SetBodyHierarchyReferences();
	}

	protected void CreateBody( PhysicsGroupDescription.BodyPart part )
	{
		var bone = Model.Bones.GetBone( part.BoneName );

		if ( !BoneObjects.TryGetValue( bone, out var boneObject ) )
			return;

		AddFlags( boneObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		var rigidbody = boneObject.AddComponent<Rigidbody>( startEnabled: false );
		var colliders = AddColliders( boneObject, part ).ToList();

		Bodies.Add( bone.Index, new Body( this, rigidbody, boneObject, bone.Index, colliders ) );
	}

	public void AddFlags( GameObject gameObject, GameObjectFlags flags )
	{
		if ( !gameObject.IsValid() )
			return;

		if ( flags.Contains( GameObjectFlags.Absolute ) && !gameObject.Flags.Contains( GameObjectFlags.Absolute ) )
		{
			var previousTransform = gameObject.WorldTransform;
			gameObject.Flags |= GameObjectFlags.Absolute;
			gameObject.WorldTransform = previousTransform; // Keeps WorldTransform
		}

		gameObject.Flags |= flags;
	}

	public void RemoveFlags( GameObject gameObject, GameObjectFlags flags )
	{
		if ( !gameObject.IsValid() )
			return;

		if ( flags.Contains( GameObjectFlags.Absolute ) && gameObject.Flags.Contains( GameObjectFlags.Absolute ) )
		{
			var previousTransform = gameObject.WorldTransform;
			gameObject.Flags &= ~GameObjectFlags.Absolute;
			gameObject.WorldTransform = previousTransform; // Keeps WorldTransform
		}

		gameObject.Flags &= ~flags;
	}

	protected void SetBodyHierarchyReferences()
	{
		foreach ( var kvp in Bodies.ToList() )
		{
			var validParentBone = GetNearestValidParentBody( kvp.Value.GetBone()?.Parent );
			if ( validParentBone == null )
				continue;
			var newBody = kvp.Value.WithParent( validParentBone.Value.GetBone() );
			var children = kvp.Value.GetBone().Children;
			if ( children != null && children.Count() > 0 )
			{
				var childrenBodies = new List<BoneCollection.Bone>();
				foreach ( var childBone in children )
				{
					var childBody = GetNearestValidChildBody( childBone );
					if ( childBody != null )
						childrenBodies.Add( childBody.Value.GetBone() );
				}
				newBody = newBody.WithChildren( childrenBodies );
			}

			Bodies.Remove( kvp.Key );
			Bodies.Add( kvp.Key, newBody );
		}
	}

	protected IEnumerable<Collider> AddColliders( GameObject parent, PhysicsGroupDescription.BodyPart part )
	{
		foreach ( var sphere in part.Spheres )
		{
			var sphereCollider = parent.AddComponent<SphereCollider>();
			sphereCollider.Center = sphere.Sphere.Center;
			sphereCollider.Radius = sphere.Sphere.Radius;
			sphereCollider.Surface = sphere.Surface;
			yield return sphereCollider;
		}
		foreach ( var capsule in part.Capsules )
		{
			var capsuleCollider = parent.AddComponent<CapsuleCollider>();
			capsuleCollider.Start = capsule.Capsule.CenterA;
			capsuleCollider.End = capsule.Capsule.CenterB;
			capsuleCollider.Radius = capsule.Capsule.Radius;
			capsuleCollider.Surface = capsule.Surface;
			yield return capsuleCollider;
		}
		foreach ( var hull in part.Hulls )
		{
			var hullCollider = parent.AddComponent<HullCollider>();
			hullCollider.Type = HullCollider.PrimitiveType.Points;
			hullCollider.Points = hull.GetPoints().ToList();
			hullCollider.Surface = hull.Surface;
			yield return hullCollider;
		}
	}

	protected void SetupBodyModes()
	{
		if ( IsProxy )
			return;

		BodyModes.Clear();
		AllBodyFlags.Clear();

		var modeName = string.IsNullOrEmpty( Mode.Name ) ? ShrimpleRagdollMode.Disabled : Mode.Name;
		foreach ( var body in Bodies )
		{
			BodyModes.Add( body.Key, modeName );
			AllBodyFlags.Add( body.Key, BodyFlags.None );
		}
	}

	protected void DestroyBodies()
	{
		if ( Bodies == null )
			return;

		foreach ( var body in Bodies.Values )
		{
			foreach ( var collider in body.Colliders )
				if ( collider.IsValid() )
					collider.Destroy();

			RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
			body.Component.Destroy();
		}

		Bodies.Clear();
	}

	protected void DisableBodies()
	{
		if ( Bodies == null )
			return;

		foreach ( var body in Bodies.Values )
		{
			foreach ( var collider in body.Colliders )
				if ( collider.IsValid() )
					collider.Enabled = false;

			RemoveFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
			body.Component.Enabled = false;
		}
	}

	protected void EnableBodies()
	{
		if ( Bodies == null )
			return;

		foreach ( var body in Bodies.Values )
		{
			foreach ( var collider in body.Colliders )
				if ( collider.IsValid() )
					collider.Enabled = true;

			AddFlags( body.GameObject, GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone );
		}
	}

	public Body? GetParentBody( Body body )
	{
		var parentBone = body.GetParentBone();
		if ( parentBone == null )
			return null;

		return GetNearestValidParentBody( parentBone );
	}

	public IEnumerable<Body> GetChildrenBodies( Body body )
	{
		var childrenBones = body.GetChildrenBones();

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

	public Joint? GetParentJoint( Body body )
	{
		foreach ( var joint in Joints )
		{
			if ( joint.Body2 == body )
				return joint;
		}

		return null;
	}

	public ShrimpleRagdollModeHandlers GetBodyModeHandler( Body body )
	{
		if ( BodyModes.TryGetValue( body.BoneIndex, out var modeName ) &&
			 ShrimpleRagdollModeRegistry.TryGet( modeName, out var handler ) )
		{
			return handler;
		}

		ShrimpleRagdollModeRegistry.TryGet( ShrimpleRagdollMode.Disabled, out var disabledHandler );
		return disabledHandler;
	}

	public struct Body
	{
		public ShrimpleRagdoll Ragdoll;
		public Rigidbody Component;
		public GameObject GameObject;
		public int BoneIndex;
		public List<Collider> Colliders = new();
		public int ParentIndex;
		public List<int> ChildIndexes = new();
		public bool IsValid = false;
		public bool IsRootBone => ParentIndex == -1;

		public Body( ShrimpleRagdoll ragdoll, Rigidbody component, GameObject gameObject, int bone, List<Collider> colliders, int parent = -1, List<int> children = null, bool isValid = true )
		{
			Ragdoll = ragdoll;
			Component = component;
			GameObject = gameObject;
			BoneIndex = bone;
			Colliders = colliders;
			ParentIndex = parent;
			ChildIndexes = children;
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

		public Body WithGameObject( GameObject gameObject )
		{
			return this with { GameObject = gameObject };
		}

		public Body WithParent( BoneCollection.Bone parent )
		{
			return this with { ParentIndex = parent.Index };
		}

		public Body WithChildren( List<BoneCollection.Bone> children )
		{
			return this with { ChildIndexes = children?.Select( x => x.Index ).ToList() };
		}

		// Convenience methods using the ragdoll reference
		public BoneCollection.Bone GetBone() => BoneIndex >= 0 && BoneIndex < Ragdoll.Model.Bones.AllBones.Count() ? Ragdoll.Model.Bones.AllBones[BoneIndex] : null;
		public BoneCollection.Bone GetParentBone() => ParentIndex >= 0 && ParentIndex < Ragdoll.Model.Bones.AllBones.Count() ? Ragdoll.Model.Bones.AllBones[ParentIndex] : null;
		public List<BoneCollection.Bone> GetChildrenBones()
		{
			if ( ChildIndexes == null )
				return null;

			var ragdoll = Ragdoll; // Copy to local variable to avoid capturing 'this'
			return ChildIndexes.Select( x => ragdoll.Model.Bones.AllBones[x] ).ToList();
		}

		public Body? GetParentBody() => Ragdoll.GetParentBody( this );
		public IEnumerable<Body> GetChildrenBodies() => Ragdoll.GetChildrenBodies( this );
		public Joint? GetParentJoint() => Ragdoll.GetParentJoint( this );
		public ShrimpleRagdollModeHandlers GetModeHandler() => Ragdoll.GetBodyModeHandler( this );

		/// <summary>
		/// Get this body and all its descendants recursively
		/// </summary>
		public IEnumerable<Body> GetHierarchy()
		{
			yield return this;

			foreach ( var child in GetChildrenBodies() )
				foreach ( var descendant in child.GetHierarchy() )
					yield return descendant;
		}

		public void EnableColliders()
		{
			foreach ( var collider in Colliders )
				if ( collider.IsValid() )
					collider.Enabled = true;
		}

		public void DisableColliders()
		{
			foreach ( var collider in Colliders )
				if ( collider.IsValid() )
					collider.Enabled = false;
		}

		public void EnableRigidbody()
		{
			if ( Component.IsValid() )
				Component.Enabled = true;
		}

		public void DisableRigidbody()
		{
			if ( Component.IsValid() )
				Component.Enabled = false;
		}

		public void EnableParentJoint()
		{
			var parent = GetParentBody();
			if ( parent == null )
				return;

			if ( !parent.Value.Component?.Enabled ?? false )
				parent.Value.Component.Enabled = true; // Can't have a null physicsbody and a joint
			GetParentJoint()?.Component?.Enabled = true;
		}

		public void DisableParentJoint()
		{
			var parent = GetParentBody();
			if ( parent == null )
				return;

			if ( !parent.Value.Component?.Enabled ?? false && parent.Value.Component.Mass == 0f ) // Mass 0 means we didn't enable the colliders, so it was just for this joint
				parent.Value.Component.Enabled = false; // Disable if it was used just for this
			GetParentJoint()?.Component?.Enabled = false;
		}

		public static bool operator ==( Body left, Body right )
		{
			return left.BoneIndex == right.BoneIndex && left.ParentIndex == right.ParentIndex;
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
			return HashCode.Combine( BoneIndex, ParentIndex );
		}
	}

	/// <summary>
	/// Get the flags for a specific body
	/// </summary>
	public BodyFlags GetBodyFlags( Body body )
	{
		if ( AllBodyFlags.TryGetValue( body.BoneIndex, out var flags ) )
			return flags;
		return BodyFlags.None;
	}

	/// <summary>
	/// Set flags for a specific body
	/// </summary>
	public void SetBodyFlags( Body body, BodyFlags flags )
	{
		if ( !IsProxy && (Network?.Active ?? false) )
		{
			AllBodyFlags.Remove( body.BoneIndex );
			AllBodyFlags.Add( body.BoneIndex, flags );
		}
		else
		{
			AllBodyFlags[body.BoneIndex] = flags;
		}
	}

	/// <summary>
	/// Add flags to a specific body (combines with existing flags)
	/// </summary>
	public void AddBodyFlags( Body body, BodyFlags flags )
	{
		var currentFlags = GetBodyFlags( body );
		SetBodyFlags( body, currentFlags | flags );
	}

	/// <summary>
	/// Remove flags from a specific body
	/// </summary>
	public void RemoveBodyFlags( Body body, BodyFlags flags )
	{
		var currentFlags = GetBodyFlags( body );
		SetBodyFlags( body, currentFlags & ~flags );
	}

	/// <summary>
	/// Check if a body has specific flags
	/// </summary>
	public bool HasBodyFlags( Body body, BodyFlags flags )
	{
		var currentFlags = GetBodyFlags( body );
		return (currentFlags & flags) == flags;
	}
}

[Flags]
public enum BodyFlags
{
	None = 0,
	/// <summary>
	/// Don't run the physics update on this body
	/// </summary>
	NoPhysicsUpdate = 1,
	/// <summary>
	/// Don't run the visual update on this body
	/// </summary>
	NoVisualUpdate = 2,
}
