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

	[Property]
	//[Sync] // TODO: MAKE SYNCS WHEN FIELDS WORK
	public SkinnedModelRenderer Renderer
	{
		get;
		set
		{
			if ( !value.IsValid() )
				return;

			field = value;
			field.CreateBoneObjects = true;
		}
	}

	// TODO: CAN'T THIS BE JUST THE INTERFACE?
	public ShrimpleRagdollModeHandlers RagdollHandler { get; protected set; }

	ShrimpleRagdollModeProperty _mode = ShrimpleRagdollMode.Disabled; // TODO: WHEN SYNC IS FIXED TURN THIS INTO A FIELD SETTER
	[Sync]
	[Property]
	public ShrimpleRagdollModeProperty Mode
	{
		get => _mode;
		set
		{
			if ( value == _mode )
				return;

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
	/// Destroy and build the physics when changing <see cref="Mode"/> instead of just enabling/disabling the components
	/// </summary>
	[Property]
	public bool RebuildPhysicsOnChange { get; set; } = false;

	/// <summary>
	/// Call a network refresh on the Renderer's GameObject internally when creating/destroying physics
	/// </summary>
	[Property]
	public bool NetworkRefreshOnChange { get; set; } = true;

	/// <summary>
	/// All the bodies and joints for ragdoll mode were created
	/// </summary>
	[Sync]
	public bool PhysicsWereCreated { get; protected set; } = false;

	public Model Model => Renderer?.Model;

	public Dictionary<BoneCollection.Bone, GameObject> BoneObjects { get; protected set; } = new();

	protected override void OnStart()
	{
		base.OnStart();

		Renderer.CreateBoneObjects = true;

		if ( !IsProxy && (Network?.Active ?? false) )
		{
			SetupBodyTransforms();
			GameObject.Root.NetworkSpawn();
		}

		CreatePhysics();
		InternalSetRagdollMode( ShrimpleRagdollMode.Disabled, Mode );

		if ( IsProxy )
			InitializeProxy();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
	}

	internal void ComputeVisuals()
	{
		if ( !Active )
			return;

		if ( IsLerpingToAnimation )
		{
			UpdateLerpAnimations();
		}
		else
		{
			foreach ( var body in Bodies )
			{
				// Use the body's individual mode handler
				body.Value.ModeHandler.VisualUpdate?.Invoke( this, body.Value );
			}
		}
	}

	internal void ComputePhysics()
	{
		if ( !Active )
			return;

		if ( !IsProxy )
		{
			if ( IsLerpingToAnimation )
			{
				MoveObjectsFromMesh();
			}
			else
			{
				foreach ( var body in Bodies )
				{
					// Use the body's individual mode handler
					body.Value.ModeHandler.PhysicsUpdate?.Invoke( this, body.Value );
				}

				MoveGameObject();
			}

			if ( Network?.Active ?? false )
				SetBodyTransforms();
		}
		else
		{
			if ( Network?.Active ?? false )
				SetProxyTransforms();
		}
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{

	}

	void IScenePhysicsEvents.PrePhysicsStep()
	{

	}

	protected void CreateBoneObjects( PhysicsGroupDescription physics, bool discardHelpers = true )
	{
		if ( !Renderer.IsValid() || !Model.IsValid() )
			return;

		BoneObjects = Model.CreateBoneObjects( Renderer.GameObject );

		if ( !discardHelpers )
			return;

		var partNames = physics.Parts.Select( x => x.BoneName );

		foreach ( var bone in BoneObjects.ToList() )
		{
			if ( !bone.Value.Children.Any() && !partNames.Contains( bone.Key.Name ) )
			{
				bone.Value.Destroy(); // Remove helper bones we don't need
				BoneObjects.Remove( bone.Key );
			}
		}
	}

	protected void CreatePhysics()
	{
		if ( !Active || IsProxy )
			return;

		DestroyPhysics();

		if ( !Model.IsValid() )
			return;

		var physics = Model.Physics;
		if ( physics == null || physics.Parts.Count == 0 )
			return;

		CreateBoneObjects( physics ); // Maybe we can create these in editor
		CreateBodies( physics );
		CreateJoints( physics );

		foreach ( var body in Bodies.Values )
			body.Component.Enabled = true;
		foreach ( var joint in Joints )
			joint.Component.Enabled = true;

		if ( NetworkRefreshOnChange )
			Renderer?.Network?.Refresh(); // Only refresh the renderer as that's where we added the bone objects

		PhysicsWereCreated = true;
	}

	public void DestroyPhysics()
	{
		PhysicsWereCreated = false;

		if ( Renderer.IsValid() )
			Renderer.ClearPhysicsBones();
		BodyTransforms.Clear();

		DestroyJoints();
		DestroyBodies();

		if ( NetworkRefreshOnChange )
			Renderer?.Network?.Refresh();
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
	/// Set the mode for a specific body
	/// </summary>
	public void SetBodyMode( Body body, string modeName )
	{
		if ( !ShrimpleRagdollModeRegistry.TryGet( modeName, out var newHandler ) )
			return;

		// Exit old mode
		body.ModeHandler.OnExit?.Invoke( this, body );

		// Update body with new mode name
		var updatedBody = body.WithModeName( modeName );
		Bodies.Remove( body.BoneIndex );
		Bodies.Add( body.BoneIndex, updatedBody );

		// Enter new mode
		updatedBody.ModeHandler.OnEnter?.Invoke( this, updatedBody );
	}

	/// <summary>
	/// Set the mode for a specific body by bone name
	/// </summary>
	public void SetBodyModeByName( string boneName, string modeName )
	{
		var body = GetBodyByBoneName( boneName );
		if ( body.HasValue )
			SetBodyMode( body.Value, modeName );
	}

	protected void InternalSetRagdollMode( string oldMode, string newMode )
	{
		if ( !ShrimpleRagdollModeRegistry.TryGet( newMode ?? "Disabled", out var newHandler ) )
			return;

		// Exit all bodies from their current modes
		foreach ( var body in Bodies )
			body.Value.ModeHandler.OnExit?.Invoke( this, body.Value );

		RagdollHandler = newHandler;

		// Set all bodies to the new mode and enter
		foreach ( var kvp in Bodies.ToList() )
		{
			var updatedBody = kvp.Value.WithModeName( newMode );
			Bodies.Remove( kvp.Key );
			Bodies.Add( kvp.Key, updatedBody );
			updatedBody.ModeHandler.OnEnter?.Invoke( this, updatedBody );
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
		DisableBodies();
		DisableJoints();

		Renderer?.ClearPhysicsBones();
	}

	public void EnablePhysics()
	{
		if ( !Renderer.IsValid() )
			return;

		MoveGameObject();
		EnableBodies();
		EnableJoints();

		if ( Mode == ShrimpleRagdollMode.Statue )
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

		if ( !Game.IsEditor || Game.IsPlaying )
			return;

		SetupBoneList();
	}
}
