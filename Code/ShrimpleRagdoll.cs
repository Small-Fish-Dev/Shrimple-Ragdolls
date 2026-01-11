namespace ShrimpleRagdolls;

[Icon( "sports_gymnastics" )]
[Description( "Ragdoll with many presets and functionalities" )]
public partial class ShrimpleRagdoll : Component, IScenePhysicsEvents
{
	[Flags]
	public enum RagdollFollowMode
	{
		[Hide]
		[Icon( "cancel" )]
		None = 0,
		[Icon( "open_with" )]
		Position = 1,
		[Icon( "autorenew" )]
		Rotation = 2,
		[Hide]
		All = Position | Rotation
	}

	SkinnedModelRenderer _renderer;

	[Property]
	public SkinnedModelRenderer Renderer
	{
		get => _renderer;
		set
		{
			if ( !value.IsValid() )
				return;

			_renderer = value;
			_renderer.CreateBoneObjects = true;
		}
	}

	/// <summary>
	/// Internal ModelPhysics component that handles physics creation and networking
	/// </summary>
	public ModelPhysics ModelPhysics { get; protected set; }

	public ShrimpleRagdollModeHandlers RagdollHandler { get; protected set; }

	ShrimpleRagdollModeProperty _mode = ShrimpleRagdollMode.Disabled;
	[Property]
	public ShrimpleRagdollModeProperty Mode
	{
		get => _mode;
		set
		{
			if ( value == _mode )
				return;

			if ( !IsProxy )
				InternalSetRagdollMode( _mode, value );
			_mode = value;
		}
	}

	/// <summary>
	/// If the ragdoll's renderer is not the root object, how should the root gameobject follow the ragdoll's movement<br />
	/// If you want to move the ragdoll you'll have to move the <see cref="Renderer"/>'s GameObject<br />
	/// If you want to move the ragdoll's physics you'll have to call <see cref="Move(global::Transform)"/> and in animation driven modes also the Renderer's GameObject
	/// </summary>
	[Property]
	public BoneFollowOption FollowOptions { get; set; } = new();

	/// <summary>
	/// All the bodies and joints for ragdoll mode were created
	/// </summary>
	public bool PhysicsWereCreated => ModelPhysics?.PhysicsWereCreated ?? false;

	/// <summary>
	/// Per-body mode overrides that are applied on start or when physics is created.
	/// These override the default ragdoll mode for specific bones.
	/// </summary>
	[Property, Group( "Body Mode Overrides" )]
	public List<BodyModeOverride> BodyModeOverrides { get; set; } = new();
	public Model Model => Renderer?.Model;
	public Dictionary<BoneCollection.Bone, GameObject> BoneObjects { get; protected set; } = new();
	/// <summary>
	/// Called when <see cref="Mode"/> has changed<br />
	/// string oldMode, string newMode
	/// </summary>
	public Action<string, string> OnModeChange { get; set; }

	private bool _proxyInitialized = false;

	protected override void OnStart()
	{
		base.OnStart();
		Assert.NotNull( Renderer, "Ragdoll's renderer can't be null" );
		Assert.NotNull( Model, "Ragdoll's model can't be null" );

		Renderer.CreateBoneObjects = true;
		Renderer.SceneModel.Update( RealTime.Delta ); // Update animation by 1 frame so the physics aren't added to the bind pose

		SetupBoneLists(); // Make sure BoneLists are initialized before creating physics

		if ( !IsProxy )
		{
			CreateModelPhysics();
			InternalSetRagdollMode( ShrimpleRagdollMode.Disabled, Mode );
		}
		else
		{
			// Proxies need to build BoneObjects for visual updates
			BuildBoneObjects();
			// Cache will be built from networked BodyBoneIndexes when accessed
			InvalidateBodiesCache();
			// Try to initialize proxy if data is already available
			TryInitializeProxy();
		}
	}

	/// <summary>
	/// Try to initialize proxy state from networked data.
	/// Called on start and during update until successful.
	/// </summary>
	protected void TryInitializeProxy()
	{
		if ( !IsProxy || _proxyInitialized )
			return;

		// Wait until networked data is available
		if ( BodyBoneIndexes.Count == 0 )
			return;

		// Initialize body flags for proxies
		InitializeProxyBodyFlags();
		InvalidateBodiesCache();
		_proxyInitialized = true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// Keep trying to initialize proxy until successful
		if ( IsProxy && !_proxyInitialized )
			TryInitializeProxy();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
	}

	internal void ComputeVisuals()
	{
		if ( !Active )
			return;

		if ( IsLerpingToAnimation && LerpToAnimationMode == LerpMode.Mesh )
			UpdateLerpAnimations();

		foreach ( var kvp in Bodies )
		{
			// Skip bodies with NoVisualUpdate flag
			if ( HasBodyFlags( kvp.Value, BodyFlags.NoVisualUpdate ) )
				continue;

			var handler = GetBodyModeHandler( kvp.Value );
			handler.VisualUpdate?.Invoke( this, kvp.Value );
		}
	}

	internal void ComputePhysics()
	{
		if ( !Active )
			return;

		if ( !IsProxy )
		{
			if ( IsLerpingToAnimation && LerpToAnimationMode != LerpMode.Mesh )
				UpdateLerpAnimations();

			foreach ( var kvp in Bodies )
			{
				// Skip bodies with NoPhysicsUpdate flag
				if ( HasBodyFlags( kvp.Value, BodyFlags.NoPhysicsUpdate ) )
					continue;

				var handler = GetBodyModeHandler( kvp.Value );
				handler.PhysicsUpdate?.Invoke( this, kvp.Value );
			}

			if ( Mode != ShrimpleRagdollMode.Disabled )
				MoveGameObject();
		}
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{
		// ModelPhysics handles networking
	}

	void IScenePhysicsEvents.PrePhysicsStep()
	{
		// ModelPhysics handles networking
	}

	protected void CreateModelPhysics()
	{
		if ( !Active || IsProxy )
			return;

		if ( ModelPhysics.IsValid() || !Model.IsValid() )
			return;

		var physics = Model.Physics;
		if ( physics == null || physics.Parts.Count == 0 )
			return;

		// Create ModelPhysics component on the Renderer's GameObject
		ModelPhysics = Renderer.GameObject.AddComponent<ModelPhysics>();
		ModelPhysics.Renderer = Renderer;
		ModelPhysics.Model = Model;
		ModelPhysics.StartAsleep = StartAsleep;
		ModelPhysics.RigidbodyFlags = RigidbodyFlags;
		ModelPhysics.Locking = Locking;
		ModelPhysics.IgnoreRoot = true; // We do it ourselves
		ModelPhysics.MotionEnabled = true;

		// Build BoneObjects dictionary from ModelPhysics
		BuildBoneObjects();

		// Sync the bone indexes to network for proxies
		SyncBodyBoneIndexes();

		// Setup body modes and hierarchy
		SetupBodyModes();
		SetupPhysics();

		// Apply body mode overrides
		ApplyBodyModeOverrides();
	}

	protected void BuildBoneObjects()
	{
		BoneObjects.Clear();

		if ( !Model.IsValid() )
			return;

		BoneObjects = Model.CreateBoneObjects( Renderer.GameObject );
	}

	public void DestroyPhysics()
	{
		if ( Renderer.IsValid() )
			Renderer.ClearPhysicsBones();

		if ( ModelPhysics.IsValid() )
		{
			ModelPhysics.Destroy();
			ModelPhysics = null;
		}

		BodyModes.Clear();
		AllBodyFlags.Clear();
	}

	/// <summary>
	/// Apply all configured body mode overrides
	/// </summary>
	public void ApplyBodyModeOverrides()
	{
		// Don't apply overrides if physics hasn't been created yet
		if ( !PhysicsWereCreated || Bodies == null || Bodies.Count == 0 )
			return;

		if ( BodyModeOverrides == null || BodyModeOverrides.Count == 0 )
			return;

		foreach ( var modeOverride in BodyModeOverrides )
		{
			if ( string.IsNullOrEmpty( modeOverride.Bone.Selected ) )
				continue;

			SetBodyModeByName( modeOverride.Bone.Selected, modeOverride.Mode, modeOverride.IncludeChildren );
		}
	}

	/// <summary>
	/// Set the ragdoll mode for all bodies
	/// </summary>
	/// <param name="mode"></param>
	public void SetRagdollMode( string mode )
	{
		if ( Mode == mode )
			return;

		Mode = mode;
	}

	/// <summary>
	/// Set the mode for a specific body by bone index
	/// </summary>
	/// <param name="boneIndex">The index of the bone</param>
	/// <param name="modeName">The name of the mode</param>
	/// <param name="includeChildren">If true, also sets the mode for all children and descendants</param>
	public void SetBodyMode( int boneIndex, string modeName, bool includeChildren = false )
	{
		if ( !ShrimpleRagdollModeRegistry.TryGet( modeName, out var newHandler ) )
			return;

		BodyModes[boneIndex] = modeName;

		// If physics is created, apply the mode immediately
		if ( PhysicsWereCreated && Bodies.TryGetValue( boneIndex, out var body ) )
		{
			var oldHandler = GetBodyModeHandler( body );
			oldHandler.OnExit?.Invoke( this, body );
			newHandler.OnEnter?.Invoke( this, body );
			ApplyModeSettings( body, modeName );

			if ( includeChildren )
			{
				foreach ( var hierarchyBody in body.GetHierarchy().Skip( 1 ) ) // Skip the first (parent) body
				{
					SetBodyMode( hierarchyBody.BoneIndex, modeName, false ); // Recursive call with includeChildren = false
				}
			}
		}
	}

	/// <summary>
	/// Set the mode for a specific body by bone name
	/// </summary>
	/// <param name="boneName">The name of the bone</param>
	/// <param name="modeName">The name of the mode</param>
	/// <param name="includeChildren">If true, also sets the mode for all children and descendants</param>
	public void SetBodyModeByName( string boneName, string modeName, bool includeChildren = false )
	{
		// Try to find the bone index
		var bone = Model?.Bones?.GetBone( boneName );
		if ( bone == null )
			return;

		SetBodyMode( bone.Index, modeName, includeChildren );
	}

	/// <summary>
	/// Set the mode for a specific body
	/// </summary>
	/// <param name="body">The body to set the mode for</param>
	/// <param name="modeName">The name of the mode</param>
	/// <param name="includeChildren">If true, also sets the mode for all children and descendants</param>
	public void SetBodyMode( Body body, string modeName, bool includeChildren = false )
	{
		SetBodyMode( body.BoneIndex, modeName, includeChildren );
	}

	protected void InternalSetRagdollMode( string oldMode, string newMode )
	{
		if ( !ShrimpleRagdollModeRegistry.TryGet( newMode ?? "Disabled", out var newHandler ) )
			return;

		// Stop any active lerp to prevent it from interfering with the new mode
		StopLerp();

		// Save velocities before mode transition to prevent velocity spikes
		var savedVelocities = new Dictionary<int, (Vector3 Linear, Vector3 Angular)>();
		foreach ( var kvp in Bodies )
		{
			if ( kvp.Value.Component.IsValid() )
				savedVelocities[kvp.Key] = (kvp.Value.Component.Velocity, kvp.Value.Component.AngularVelocity);
		}

		// Exit all bodies from their current modes
		foreach ( var kvp in Bodies )
		{
			var oldHandler = GetBodyModeHandler( kvp.Value );
			oldHandler.OnExit?.Invoke( this, kvp.Value );
		}

		RagdollHandler = newHandler;

		// Set all bodies to the new mode
		foreach ( var kvp in Bodies.ToList() )
		{
			BodyModes[kvp.Key] = newMode;

			// Enter new mode
			newHandler.OnEnter?.Invoke( this, kvp.Value );

			// Apply mode settings if available
			ApplyModeSettings( kvp.Value, newMode );
		}

		// Restore velocities after mode transition
		foreach ( var (boneIndex, velocity) in savedVelocities )
		{
			if ( Bodies.TryGetValue( boneIndex, out var body ) && body.Component.IsValid() )
			{
				body.Component.Velocity = velocity.Linear;
				body.Component.AngularVelocity = velocity.Angular;
			}
		}

		// Re-apply body mode overrides after setting global mode
		ApplyBodyModeOverrides();

		OnModeChange?.Invoke( oldMode, newMode );
	}

	/// <summary>
	/// Apply mode settings from attached ModeSettings components
	/// </summary>
	protected void ApplyModeSettings( Body body, string modeName )
	{
		var modeSettings = GetComponents<ShrimpleModeSettings>();
		foreach ( var settings in modeSettings )
		{
			if ( settings.TargetMode == modeName )
			{
				settings.ApplySettings( this, body );
			}
		}
	}

	public void MakeRendererAbsolute( bool absolute )
	{
		if ( !Renderer.IsValid() )
			return;

		if ( absolute )
		{
			if ( !Renderer.GameObject.Flags.Contains( GameObjectFlags.Absolute ) )
			{
				var worldTransform = Renderer.WorldTransform;
				Renderer.GameObject.Flags |= GameObjectFlags.Absolute;
				Renderer.WorldTransform = worldTransform;
			}
		}
		else
		{
			if ( Renderer.GameObject.Flags.Contains( GameObjectFlags.Absolute ) )
			{
				Renderer.GameObject.Flags &= ~GameObjectFlags.Absolute;
				Renderer.WorldTransform = Renderer.LocalTransform;
			}
		}
	}

	public void DisablePhysics()
	{
		if ( ModelPhysics.IsValid() )
			ModelPhysics.Enabled = false;

		Renderer?.ClearPhysicsBones();
	}

	public void EnablePhysics()
	{
		if ( !Renderer.IsValid() )
			return;

		MoveGameObject();

		if ( ModelPhysics.IsValid() )
			ModelPhysics.Enabled = true;

		foreach ( var kvp in Bodies )
		{
			var handler = GetBodyModeHandler( kvp.Value );
			handler.OnEnter?.Invoke( this, kvp.Value );
		}

		bool hasStatueMode = false;
		foreach ( var kvp in Bodies )
		{
			if ( BodyModes.TryGetValue( kvp.Key, out var modeName ) && modeName == ShrimpleRagdollMode.Statue )
			{
				hasStatueMode = true;
				break;
			}
		}

		if ( hasStatueMode )
			MoveMeshFromObjects();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		EnablePhysics();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		DisablePhysics();
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Game.IsEditor || Game.IsPlaying && !Gizmo.IsSelected )
			return;

		SetupBoneLists();
	}
}
