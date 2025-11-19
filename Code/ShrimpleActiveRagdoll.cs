public partial class ShrimpleActiveRagdoll : Component
{
	// TODO ADD MODE STATUE ONLY ONE RIGIDBODY
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
		None = 1,
		[Icon( "open_with" )]
		Position = 2,
		[Icon( "autorenew" )]
		Rotation = 4,
		[Hide]
		All = Position | Rotation
	}

	[Property]
	public SkinnedModelRenderer Renderer { get; set; }

	[Property]
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
	/// ENABLED ONLY FOR MODES WITH NO ANIMATIONS (Enabled and Statue)<br />
	/// If you want to move the ragdoll you'll have to move the <see cref="Renderer"/>'s GameObject
	/// </summary>
	[Property]
	[ShowIf( nameof( PhysicsDriven ), true )]
	public RagdollFollowMode FollowMode
	{
		get;
		set
		{
			if ( !field.Contains( RagdollFollowMode.None ) && value.Contains( RagdollFollowMode.None ) )
				value = RagdollFollowMode.None;

			if ( value != RagdollFollowMode.None )
				value &= ~RagdollFollowMode.None;

			field = value;
		}
	} = RagdollFollowMode.All;

	/// <summary>
	/// Destroy and build the physics when changing <see cref="Mode"/> instead of just enabling/disabling the components
	/// </summary>
	[Property]
	public bool RebuildPhysicsOnChange { get; set; } = false; // TODO IMPLEMENT

	/// <summary>
	/// Call a network refresh on the Renderer's GameObject when changing <see cref="Mode"/>
	/// </summary>
	[Property]
	public bool NetworkRefreshOnChange { get; set; } = true; // TODO IMPLEMENT

	/// <summary>
	/// All the bodies and joints for ragdoll mode were created
	/// </summary>
	public bool RagdollPhysicsWereCreated { get; protected set; } = false;
	/// <summary>
	/// All the colliders were created for statue mode
	/// </summary>
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
	//protected NetworkTransforms BodyTransforms = new NetworkTransforms();

	public Dictionary<BoneCollection.Bone, GameObject> BoneObjects { get; protected set; }

	protected override void OnStart()
	{
		base.OnStart();

		InternalSetRagdollMode( Mode, Mode, true );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( Mode == RagdollMode.Enabled )
			MoveMeshFromBodies();
		if ( Mode == RagdollMode.Passive )
			MoveBodiesFromAnimations();
		if ( Mode == RagdollMode.Active )
		{
			MoveBodiesFromAnimations();
			MoveMeshFromBodies();
		}
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Active || IsProxy || Mode == RagdollMode.Disabled )
			return;

		MoveGameObject( false );
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

		Renderer?.Network?.Refresh(); // Only refresh the rendeded as that's where we added the bone objects
		RagdollPhysicsWereCreated = true;
	}

	protected void DestroyPhysics()
	{
		RagdollPhysicsWereCreated = false;
		StatuePhysicsWereCreated = false;

		if ( Renderer.IsValid() )
			Renderer.ClearPhysicsBones();
		//BodyTransforms.Clear();

		DestroyJoints();
		DestroyBodies();

		Renderer?.Network?.Refresh();
	}

	protected void CreateStatuePhysics()
	{
		if ( !Active || IsProxy )
			return;

		DestroyPhysics();

		if ( !Model.IsValid() )
			return;

		var physics = Model.Physics;
		if ( physics == null || physics.Parts.Count == 0 )
			return;

		CreateBoneObjects( physics );
		CreateStatueBodies( physics );
		MoveMeshFromObjects();

		foreach ( var body in Bodies.Values )
			body.Component.Enabled = true;

		Renderer?.Network?.Refresh(); // Only refresh the rendeded as that's where we added the bone objects
		RagdollPhysicsWereCreated = true;
		StatuePhysicsWereCreated = true;
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

	protected void InternalSetRagdollMode( RagdollMode oldMode, RagdollMode newMode, bool firstTime = false )
	{
		if ( newMode == RagdollMode.Disabled )
			DisablePhysics();

		if ( newMode == RagdollMode.Enabled )
		{
			if ( StatuePhysicsWereCreated || firstTime ) // If we were statue we need to recreate the physics
				CreatePhysics();

			EnablePhysics();
			Renderer?.ClearPhysicsBones();
		}

		if ( newMode == RagdollMode.Passive )
		{
			if ( StatuePhysicsWereCreated || firstTime )
				CreatePhysics();

			EnablePhysics();
			Renderer?.ClearPhysicsBones();
		}

		if ( newMode == RagdollMode.Active )
		{
			if ( StatuePhysicsWereCreated || firstTime )
				CreatePhysics();

			EnablePhysics();
			Renderer?.ClearPhysicsBones();
		}

		if ( newMode == RagdollMode.Statue )
		{
			if ( !StatuePhysicsWereCreated || firstTime )
				CreateStatuePhysics();

			EnablePhysics();
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

		MoveGameObject( true );
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
}
