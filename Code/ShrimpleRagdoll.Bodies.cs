namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Wrapper struct for ModelPhysics.Body that provides convenient access methods
	/// </summary>
	public readonly record struct Body
	{
		private readonly ShrimpleRagdoll _ragdoll;
		private readonly ModelPhysics.Body _body;

		public Body( ShrimpleRagdoll ragdoll, ModelPhysics.Body body )
		{
			_ragdoll = ragdoll;
			_body = body;
		}

		/// <summary>
		/// The underlying Rigidbody component
		/// </summary>
		public Rigidbody Component => _body.Component;

		/// <summary>
		/// The bone index this body is associated with
		/// </summary>
		public int BoneIndex => _body.Bone;

		/// <summary>
		/// The local transform of the body relative to the bone
		/// </summary>
		public Transform LocalTransform => _body.LocalTransform;

		/// <summary>
		/// The GameObject containing this body's rigidbody
		/// </summary>
		public GameObject GameObject => _body.Component?.GameObject;

		/// <summary>
		/// Get the bone associated with this body
		/// </summary>
		public BoneCollection.Bone GetBone() => _ragdoll.GetBone( BoneIndex );

		/// <summary>
		/// Check if this body is the root bone (has no parent joint)
		/// </summary>
		public bool IsRootBone => GetParentJoint() == null;

		/// <summary>
		/// Get the parent body via joint (Body1 of the joint where this body is Body2)
		/// </summary>
		public Body? GetParent() => _ragdoll.GetParentBody( this );

		/// <summary>
		/// Get children bodies via joints (bodies that have this body as Body1 in their joint)
		/// </summary>
		public IEnumerable<Body> GetChildren() => _ragdoll.GetChildrenBodies( this );

		/// <summary>
		/// Get this body and all its descendants recursively
		/// </summary>
		public IEnumerable<Body> GetHierarchy()
		{
			yield return this;

			foreach ( var child in GetChildren() )
				foreach ( var descendant in child.GetHierarchy() )
					yield return descendant;
		}

		/// <summary>
		/// Get the parent joint for this body (joint where this body is Body2)
		/// </summary>
		public ModelPhysics.Joint? GetParentJoint() => _ragdoll.GetParentJoint( this );

		/// <summary>
		/// Enable colliders for this body
		/// </summary>
		public void EnableColliders()
		{
			var go = GameObject;
			if ( !go.IsValid() )
				return;

			foreach ( var collider in go.GetComponents<Collider>() )
				if ( collider.IsValid() )
					collider.Enabled = true;
		}

		/// <summary>
		/// Disable colliders for this body
		/// </summary>
		public void DisableColliders()
		{
			var go = GameObject;
			if ( !go.IsValid() )
				return;

			foreach ( var collider in go.GetComponents<Collider>() )
				if ( collider.IsValid() )
					collider.Enabled = false;
		}

		/// <summary>
		/// Enable the rigidbody component
		/// </summary>
		public void EnableRigidbody()
		{
			if ( Component.IsValid() )
				Component.Enabled = true;
		}

		/// <summary>
		/// Disable the rigidbody component
		/// </summary>
		public void DisableRigidbody()
		{
			if ( Component.IsValid() )
				Component.Enabled = false;
		}

		/// <summary>
		/// Enable the parent joint for this body
		/// </summary>
		public void EnableParentJoint()
		{
			var parent = GetParent();
			if ( parent == null )
				return;

			if ( !(parent.Value.Component?.Enabled ?? true) )
				parent.Value.Component.Enabled = true;

			var parentJoint = GetParentJoint();
			if ( parentJoint?.Component != null )
				parentJoint.Value.Component.Enabled = true;
		}

		/// <summary>
		/// Disable the parent joint for this body
		/// </summary>
		public void DisableParentJoint()
		{
			var parent = GetParent();
			if ( parent == null )
				return;

			if ( !(parent.Value.Component?.Enabled ?? true) && parent.Value.Component.Mass == 0f )
				parent.Value.Component.Enabled = false;

			var parentJoint = GetParentJoint();
			if ( parentJoint?.Component != null )
				parentJoint.Value.Component.Enabled = false;
		}

		/// <summary>
		/// Add the PhysicsBone tag to allow ModelPhysics to control the bone override
		/// </summary>
		public void AddPhysicsBoneTag()
		{
			var go = GameObject;
			if ( go.IsValid() )
				_ragdoll.AddFlags( go, GameObjectFlags.PhysicsBone );
		}

		/// <summary>
		/// Remove the PhysicsBone tag to control the bone override ourselves
		/// </summary>
		public void RemovePhysicsBoneTag()
		{
			var go = GameObject;
			if ( go.IsValid() )
				_ragdoll.RemoveFlags( go, GameObjectFlags.PhysicsBone );
		}

		/// <summary>
		/// Check if this body has the PhysicsBone tag
		/// </summary>
		public bool HasPhysicsBoneTag()
		{
			var go = GameObject;
			return go.IsValid() && go.Flags.Contains( GameObjectFlags.PhysicsBone );
		}
	}

	/// <summary>
	/// Dictionary mapping bone index to Body wrapper
	/// </summary>
	public Dictionary<int, Body> Bodies
	{
		get
		{
			var mpBodies = ModelPhysics?.Bodies;
			if ( _bodiesCache == null || _bodiesCacheVersion != (mpBodies?.Count ?? 0) )
				RebuildBodiesCache();
			return _bodiesCache;
		}
	}

	private Dictionary<int, Body> _bodiesCache;
	private int _bodiesCacheVersion = -1;

	/// <summary>
	/// Per-body mode names
	/// </summary>
	public Dictionary<int, string> BodyModes { get; protected set; } = new();

	/// <summary>
	/// Per-body flags for controlling updates
	/// </summary>
	public Dictionary<int, BodyFlags> AllBodyFlags { get; protected set; } = new();

	protected void RebuildBodiesCache()
	{
		_bodiesCache = new Dictionary<int, Body>();

		var mpBodies = ModelPhysics?.Bodies;
		if ( mpBodies == null )
			return;

		foreach ( var mpBody in mpBodies )
			_bodiesCache[mpBody.Bone] = new Body( this, mpBody );

		_bodiesCacheVersion = mpBodies.Count;
	}

	/// <summary>
	/// Get the parent joint for a body (joint where this body is Body2)
	/// </summary>
	public ModelPhysics.Joint? GetParentJoint( Body body )
	{
		if ( Joints == null )
			return null;

		foreach ( var joint in Joints )
		{
			if ( joint.Body2.Bone == body.BoneIndex )
				return joint;
		}

		return null;
	}

	/// <summary>
	/// Get the parent body via joint (Body1 of the joint where this body is Body2)
	/// </summary>
	public Body? GetParentBody( Body body )
	{
		var parentJoint = GetParentJoint( body );
		if ( parentJoint == null )
			return null;

		var parentBoneIndex = parentJoint.Value.Body1.Bone;
		if ( Bodies.TryGetValue( parentBoneIndex, out var parentBody ) )
			return parentBody;

		return null;
	}

	/// <summary>
	/// Get children bodies via joints (bodies that have this body as Body1 in their joint)
	/// </summary>
	public IEnumerable<Body> GetChildrenBodies( Body body )
	{
		if ( Joints == null )
			yield break;

		foreach ( var joint in Joints )
		{
			if ( joint.Body1.Bone == body.BoneIndex )
			{
				var childBoneIndex = joint.Body2.Bone;
				if ( Bodies.TryGetValue( childBoneIndex, out var childBody ) )
					yield return childBody;
			}
		}
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

	/// <summary>
	/// Set flags on a GameObject to match the target flags, adding missing ones and removing extra ones
	/// </summary>
	public void SetFlags( GameObject gameObject, GameObjectFlags targetFlags )
	{
		if ( !gameObject.IsValid() )
			return;

		// Flags to add: in target but not in current
		var flagsToAdd = targetFlags & ~gameObject.Flags;
		if ( flagsToAdd != 0 )
			AddFlags( gameObject, flagsToAdd );

		// Flags to remove: in current but not in target
		var flagsToRemove = gameObject.Flags & ~targetFlags;
		if ( flagsToRemove != 0 )
			RemoveFlags( gameObject, flagsToRemove );
	}

	protected void SetupBodyModes()
	{
		if ( IsProxy )
			return;

		BodyModes.Clear();
		AllBodyFlags.Clear();

		var modeName = string.IsNullOrEmpty( Mode.Name ) ? ShrimpleRagdollMode.Disabled : Mode.Name;

		if ( Bodies == null )
			return;

		foreach ( var kvp in Bodies )
		{
			BodyModes[kvp.Key] = modeName;
			AllBodyFlags[kvp.Key] = BodyFlags.None;
		}
	}

	// Body helper methods

	/// <summary>
	/// Get the bone by index
	/// </summary>
	public BoneCollection.Bone GetBone( int boneIndex )
	{
		if ( boneIndex < 0 || Model?.Bones?.AllBones == null || boneIndex >= Model.Bones.AllBones.Count() )
			return null;
		return Model.Bones.AllBones[boneIndex];
	}

	/// <summary>
	/// Get the mode handler for a body
	/// </summary>
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

	// Body flags methods

	public BodyFlags GetBodyFlags( Body body ) => GetBodyFlags( body.BoneIndex );

	public BodyFlags GetBodyFlags( int boneIndex )
	{
		if ( AllBodyFlags.TryGetValue( boneIndex, out var flags ) )
			return flags;
		return BodyFlags.None;
	}

	public void SetBodyFlags( Body body, BodyFlags flags ) => SetBodyFlags( body.BoneIndex, flags );

	public void SetBodyFlags( int boneIndex, BodyFlags flags )
	{
		AllBodyFlags[boneIndex] = flags;
	}

	public void AddBodyFlags( Body body, BodyFlags flags ) => AddBodyFlags( body.BoneIndex, flags );

	public void AddBodyFlags( int boneIndex, BodyFlags flags )
	{
		var currentFlags = GetBodyFlags( boneIndex );
		SetBodyFlags( boneIndex, currentFlags | flags );
	}

	public void RemoveBodyFlags( Body body, BodyFlags flags ) => RemoveBodyFlags( body.BoneIndex, flags );

	public void RemoveBodyFlags( int boneIndex, BodyFlags flags )
	{
		var currentFlags = GetBodyFlags( boneIndex );
		SetBodyFlags( boneIndex, currentFlags & ~flags );
	}

	public void AddAllBodyFlags( BodyFlags flags )
	{
		if ( Bodies == null )
			return;

		foreach ( var kvp in Bodies )
			AddBodyFlags( kvp.Key, flags );
	}

	public void RemoveAllBodyFlags( BodyFlags flags )
	{
		if ( Bodies == null )
			return;

		foreach ( var kvp in Bodies )
			RemoveBodyFlags( kvp.Key, flags );
	}

	public bool HasBodyFlags( Body body, BodyFlags flags ) => HasBodyFlags( body.BoneIndex, flags );

	public bool HasBodyFlags( int boneIndex, BodyFlags flags )
	{
		var currentFlags = GetBodyFlags( boneIndex );
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
