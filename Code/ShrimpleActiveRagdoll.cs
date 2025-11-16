using System;

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
			if ( field == value )
				return;

			field = value;

			if ( Game.IsPlaying )
				SetRagdollMode( field );
		}
	}

	[Property]
	[ShowIf( nameof( Mode ), RagdollMode.Enabled )]
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

	public readonly record struct Body( Rigidbody Component, int Bone, List<Collider> Colliders );
	public readonly record struct Joint( Sandbox.Joint Component, Body Body1, Body Body2 );


	public Model Model => Renderer?.Model;
	public Dictionary<BoneCollection.Bone, Body> Bodies { get; } = new();
	public List<Joint> Joints { get; } = new();
	//private NetworkTransforms BodyTransforms = new NetworkTransforms();

	public Dictionary<BoneCollection.Bone, GameObject> BoneObjects { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();

		CreatePhysics();
		SetRagdollMode( Mode );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( Mode == RagdollMode.Enabled )
			MoveBonesFromObjects();
		if ( Mode == RagdollMode.Passive )
			MoveBodiesFromAnimations();
		if ( Mode == RagdollMode.Active )
		{
			MoveBodiesFromAnimations();
			MoveBonesFromObjects();
		}
	}

	private void CreateBoneObjects( PhysicsGroupDescription physics, bool discardHelpers = true )
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

	private void CreatePhysics()
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
		CreateParts( physics );
		CreateJoints( physics );

		foreach ( var body in Bodies.Values )
			body.Component.Enabled = true;
		foreach ( var joint in Joints )
			joint.Component.Enabled = true;

		Network?.Refresh( Renderer ); // Only refresh the rendeded as that's where we added the bone objects
	}

	private void CreateParts( PhysicsGroupDescription physics )
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
			var colliders = AddCollider( boneObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone, new Body( rigidbody, bone.Index, colliders ) );
		}

		//rigidbody.PhysicsBody.RebuildMass();
	}

	private void CreateStatueParts( PhysicsGroupDescription physics )
	{
		var rigidbody = Renderer.GameObject.AddComponent<Rigidbody>( startEnabled: false );

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

			var colliders = AddCollider( Renderer.GameObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone, new Body( rigidbody, bone.Index, colliders ) );
		}
	}

	private IEnumerable<Collider> AddCollider( GameObject parent, PhysicsGroupDescription.BodyPart part, Transform worldTransform )
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
			hullCollider.Points = hull.GetPoints().ToList();
			hullCollider.Surface = hull.Surface;
			hullCollider.Center = localTransform.Position;
			yield return hullCollider;
		}
	}

	private void CreateJoints( PhysicsGroupDescription physics )
	{
		foreach ( var jointDefinition in physics.Joints )
		{
			var body1 = Bodies.ElementAt( jointDefinition.Body1 ).Value;
			var body2 = Bodies.ElementAt( jointDefinition.Body2 ).Value;
			var child = jointDefinition.Frame1;
			var localFrame = jointDefinition.Frame2;
			Sandbox.Joint joint = null;
			if ( jointDefinition.Type == PhysicsGroupDescription.JointType.Hinge )
			{
				var hingeJoint = body1.Component.AddComponent<Sandbox.HingeJoint>( startEnabled: false );
				if ( jointDefinition.EnableTwistLimit )
				{
					hingeJoint.MinAngle = jointDefinition.TwistMin;
					hingeJoint.MaxAngle = jointDefinition.TwistMax;
				}

				if ( jointDefinition.EnableAngularMotor )
				{
					float rad = body1.Component.WorldTransform.ToWorld( in child ).Rotation.Up.Dot( jointDefinition.AngularTargetVelocity );
					hingeJoint.Motor = Sandbox.HingeJoint.MotorMode.TargetVelocity;
					hingeJoint.TargetVelocity = rad.RadianToDegree();
					hingeJoint.MaxTorque = jointDefinition.MaxTorque;
				}

				joint = hingeJoint;
			}
			else if ( jointDefinition.Type == PhysicsGroupDescription.JointType.Ball )
			{
				var ballJoint = body1.Component.AddComponent<BallJoint>( startEnabled: false );
				if ( jointDefinition.EnableSwingLimit )
				{
					ballJoint.SwingLimitEnabled = true;
					ballJoint.SwingLimit = new Vector2( jointDefinition.SwingMin, jointDefinition.SwingMax );
				}

				if ( jointDefinition.EnableTwistLimit )
				{
					ballJoint.TwistLimitEnabled = true;
					ballJoint.TwistLimit = new Vector2( jointDefinition.TwistMin, jointDefinition.TwistMax );
				}

				joint = ballJoint;
			}
			else if ( jointDefinition.Type == PhysicsGroupDescription.JointType.Fixed )
			{
				var fixedJoint = body1.Component.AddComponent<FixedJoint>( startEnabled: false );
				fixedJoint.LinearFrequency = jointDefinition.LinearFrequency;
				fixedJoint.LinearDamping = jointDefinition.LinearDampingRatio;
				fixedJoint.AngularFrequency = jointDefinition.AngularFrequency;
				fixedJoint.AngularDamping = jointDefinition.AngularDampingRatio;
				joint = fixedJoint;
			}
			else if ( jointDefinition.Type == PhysicsGroupDescription.JointType.Slider )
			{
				var sliderJoint = body1.Component.AddComponent<SliderJoint>( startEnabled: false );
				if ( jointDefinition.EnableLinearLimit )
				{
					sliderJoint.MinLength = jointDefinition.LinearMin;
					sliderJoint.MaxLength = jointDefinition.LinearMax;
				}

				var rotation = Rotation.FromPitch( -90f );
				child = child.WithRotation( rotation * child.Rotation );
				localFrame = localFrame.WithRotation( rotation * localFrame.Rotation );
				joint = sliderJoint;
			}

			if ( joint.IsValid() )
			{
				joint.Body = body2.Component.GameObject;
				joint.Attachment = Sandbox.Joint.AttachmentMode.LocalFrames;
				joint.LocalFrame1 = child.WithPosition( jointDefinition.Frame1.Position * body1.Component.WorldScale );
				joint.LocalFrame2 = localFrame.WithPosition( jointDefinition.Frame2.Position * body2.Component.WorldScale );
				joint.EnableCollision = jointDefinition.EnableCollision;
				joint.BreakForce = jointDefinition.LinearStrength;
				joint.BreakTorque = jointDefinition.AngularStrength;
				Joints.Add( new Joint( joint, body1, body2 ) );
			}
		}
	}

	private void DestroyPhysics()
	{
		if ( Renderer.IsValid() )
		{
			Renderer.ClearPhysicsBones();
		}

		//BodyTransforms.Clear();

		foreach ( var joint in Joints )
			if ( joint.Component.IsValid() )
				joint.Component.Destroy();

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				body.Component.GameObject.Flags &= ~GameObjectFlags.Absolute;
				body.Component.GameObject.Flags &= ~GameObjectFlags.PhysicsBone;
				body.Component.Destroy();

				foreach ( var collider in body.Colliders )
					if ( collider.IsValid() )
						collider.Destroy();
			}
		}

		Bodies.Clear();
		Joints.Clear();
		Network?.Refresh();
	}

	private void SetRagdollMode( RagdollMode mode )
	{
		if ( mode == RagdollMode.Disabled )
			DisablePhysics();

		if ( mode == RagdollMode.Enabled )
			EnablePhysics();

		if ( mode == RagdollMode.Passive )
			EnablePhysics();

		if ( mode == RagdollMode.Active )
			EnablePhysics();
	}

	public void DisablePhysics()
	{
		DisableBodies();
		DisableJoints();

		Renderer?.ClearPhysicsBones();
	}

	/// <summary>
	/// Disables all the rigidbodies and colliders
	/// </summary>
	protected void DisableBodies()
	{
		if ( Bodies == null )
			return;

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				body.Component.GameObject.Flags &= ~GameObjectFlags.Absolute;
				body.Component.GameObject.Flags &= ~GameObjectFlags.PhysicsBone;
				body.Component.Enabled = false;

				foreach ( var collider in body.Colliders )
				{
					if ( collider.IsValid() )
						collider.Enabled = false;
				}
			}
		}
	}

	/// <summary>
	/// Disables all joints
	/// </summary>
	protected void DisableJoints()
	{
		if ( Joints == null )
			return;

		foreach ( var joint in Joints )
		{
			if ( joint.Component.IsValid() )
				joint.Component.Enabled = false;
		}
	}

	public void EnablePhysics()
	{
		EnableBodies();
		EnableJoints();

		MoveObjectsFromBones();
	}

	/// <summary>
	/// Enables all the rigidbodies and colliders
	/// </summary>
	protected void EnableBodies()
	{
		if ( Bodies == null )
			return;

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				body.Component.GameObject.Flags |= GameObjectFlags.Absolute;
				body.Component.GameObject.Flags |= GameObjectFlags.PhysicsBone;
				body.Component.Enabled = true;

				foreach ( var collider in body.Colliders )
				{
					if ( collider.IsValid() )
						collider.Enabled = true;
				}
			}
		}
	}

	/// <summary>
	/// Enables all joints
	/// </summary>
	protected void EnableJoints()
	{
		if ( Joints == null )
			return;

		foreach ( var joint in Joints )
		{
			if ( joint.Component.IsValid() )
				joint.Component.Enabled = true;
		}
	}
}
