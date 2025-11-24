public partial class ShrimpleActiveRagdoll : Component
{
	public enum RagdollMode
	{
		/// <summary>
		/// ❌ Collisions<br />
		/// ❌ Physics<br />
		/// ✅ Animations
		/// </summary>
		[Icon( "person_off" )]
		Disabled,
		/// <summary>
		/// ✅ Collisions<br />
		/// ✅ Physics<br />
		/// ❌ Animations
		/// </summary>
		[Icon( "airline_seat_individual_suite" )]
		Enabled,
		/// <summary>
		/// ✅ Collisions<br />
		/// ❌ Physics<br />
		/// ✅ Animations
		/// </summary>
		[Icon( "man_2" )]
		Passive,
		/// <summary>
		/// ✅ Collisions<br />
		/// ✅ Physics<br />
		/// ✅ Animations
		/// </summary>
		[Icon( "sports_gymnastics" )]
		Active,
		/// <summary>
		/// ✅ Collisions<br />
		/// ❌ Physics<br />
		/// ❌ Animations
		/// </summary>
		[Icon( "accessibility" )]
		Statue
	}

	[Flags]
	public enum RagdollFollowMode
	{
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

	[Property]
	//[Sync]
	public RagdollMode Mode
	{
		get;
		set
		{
			var old = field;
			field = value;

			if ( Game.IsPlaying )
				InternalSetRagdollMode( old, value );
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
	public bool RagdollPhysicsWereCreated { get; protected set; } = false;
	/// <summary>
	/// All the colliders were created for statue mode
	/// </summary>
	[Sync]
	public bool StatuePhysicsWereCreated { get; protected set; } = false;

	/// <summary>
	/// All the bodies and joints were created for any mode
	/// </summary>
	public bool PhysicsWereCreated => RagdollPhysicsWereCreated || StatuePhysicsWereCreated;

	/// <summary>
	/// The GameObject's position depends on physics simulation<br />
	/// <see cref="RagdollMode.Enabled"/> or <see cref="RagdollMode.Statue"/>
	/// </summary>
	public bool PhysicsDriven => Mode == RagdollMode.Enabled || Mode == RagdollMode.Statue;
	/// <summary>
	/// The GameObject's position depends on animations or local transform<br />
	/// <see cref="RagdollMode.Passive"/> or <see cref="RagdollMode.Active"/>
	/// </summary>
	public bool AnimationsDriven => Mode == RagdollMode.Passive || Mode == RagdollMode.Active;

	public Model Model => Renderer?.Model;

	public Dictionary<BoneCollection.Bone, GameObject> BoneObjects { get; protected set; }

	protected override void OnStart()
	{
		base.OnStart();

		Renderer.CreateBoneObjects = true;
		InternalSetRagdollMode( Mode, Mode );

		if ( !IsProxy )
		{
			SetupBodyTransforms();
			GameObject.Root.NetworkSpawn();
		}
	}
	protected override async Task OnLoad()
	{
		if ( !IsProxy )
			return;

		LoadBodies();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Active || Mode == RagdollMode.Disabled )
			return;


		if ( Mode == RagdollMode.Enabled )
			MoveMeshFromBodies();
		if ( Mode == RagdollMode.Active )
			MoveMeshFromBodies();

		//var attachment = Renderer.GetAttachment( "eyes" );
		//DebugOverlay.Sphere( new Sphere( attachment.Value.Position, 3.0f ), Color.Red, Time.Delta );
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Active || Mode == RagdollMode.Disabled )
			return;

		if ( !IsProxy )
		{
			if ( Mode == RagdollMode.Passive )
				MoveBodiesFromAnimations();
			if ( Mode == RagdollMode.Active )
				MoveBodiesFromAnimations();

			MoveGameObject();
			SetBodyTransforms();

			foreach ( var collider in Renderer.GameObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			{
				//Log.Info( "Host: " + collider.GameObject.Name + ": " + collider.GetHashCode() );
			}
		}
		else
		{
			SetProxyTransforms();
			foreach ( var collider in Renderer.GameObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			{
				//Log.Info( "Proxy: " + collider.GameObject.Name + ": " + collider.GetHashCode() );
			}
		}
	}

	protected void CreateBoneObjects( PhysicsGroupDescription physics, bool discardHelpers = true )
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
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

		Log.Info( "FUUUUCK" );

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

		RagdollPhysicsWereCreated = true;
	}

	public void DestroyPhysics()
	{
		RagdollPhysicsWereCreated = false;
		StatuePhysicsWereCreated = false;

		if ( Renderer.IsValid() )
			Renderer.ClearPhysicsBones();
		//BodyTransforms.Clear();

		DestroyJoints();
		DestroyBodies();

		if ( NetworkRefreshOnChange )
			Renderer?.Network?.Refresh();
	}

	/// <summary>
	/// Set the ragdoll mode
	/// </summary>
	/// <param name="mode"></param>
	public void SetRagdollMode( RagdollMode mode )
	{
		if ( Mode == mode )
			return;

		Mode = mode;
	}

	protected void InternalSetRagdollMode( RagdollMode oldMode, RagdollMode newMode )
	{
		if ( newMode == RagdollMode.Disabled )
		{
			MakeRendererAbsolute( false );
			DisablePhysics();
		}

		if ( newMode == RagdollMode.Enabled )
		{
			if ( StatuePhysicsWereCreated || !RagdollPhysicsWereCreated || RebuildPhysicsOnChange ) // If we were statue we need to recreate the physics
				CreatePhysics();

			MakeRendererAbsolute( true );
			EnablePhysics();
			Renderer?.ClearPhysicsBones();
			MoveObjectsFromMesh();
		}

		if ( newMode == RagdollMode.Passive )
		{
			if ( StatuePhysicsWereCreated || !RagdollPhysicsWereCreated || RebuildPhysicsOnChange )
				CreatePhysics();

			MakeRendererAbsolute( false );
			EnablePhysics();
			Renderer?.ClearPhysicsBones();
			MoveObjectsFromMesh();
		}

		if ( newMode == RagdollMode.Active )
		{
			if ( StatuePhysicsWereCreated || !RagdollPhysicsWereCreated || RebuildPhysicsOnChange )
				CreatePhysics();

			MakeRendererAbsolute( false );
			EnablePhysics();
			Renderer?.ClearPhysicsBones();
			MoveObjectsFromMesh();
		}

		if ( newMode == RagdollMode.Statue )
		{
			if ( !StatuePhysicsWereCreated || RagdollPhysicsWereCreated || RebuildPhysicsOnChange )
				CreateStatuePhysics();

			MakeRendererAbsolute( false );
			EnablePhysics();
			MoveObjectsFromMesh();
		}
	}

	protected void MakeRendererAbsolute( bool absolute )
	{
		if ( !Renderer.IsValid() )
			return;

		if ( absolute )
		{
			if ( !Renderer.GameObject.Flags.Contains( GameObjectFlags.Absolute ) )
				Renderer.GameObject.Flags |= GameObjectFlags.Absolute;
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

		if ( Mode == RagdollMode.Statue )
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
