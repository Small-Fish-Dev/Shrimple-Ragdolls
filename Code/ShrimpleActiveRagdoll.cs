using System;

public class ShrimpleActiveRagdoll : Component
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
			PositionRendererBonesFromPhysics();
		if ( Mode == RagdollMode.Passive )
			PositionPhysicsFromRendererBones();
		if ( Mode == RagdollMode.Active )
			MovePhysicsFromRenderBones();
	}


	private void PositionRendererBonesFromPhysics()
	{
		Rigidbody componentInChildren = GetComponentInChildren<Rigidbody>( includeDisabled: true );
		if ( componentInChildren.IsValid() && componentInChildren.MotionEnabled )
		{
			Renderer.WorldTransform = componentInChildren.WorldTransform;
		}

		if ( !Renderer.IsValid() )
		{
			return;
		}

		SceneModel sceneModel = Renderer.SceneModel;
		if ( !sceneModel.IsValid() )
		{
			return;
		}

		Renderer.ClearPhysicsBones();
		Transform worldTransform = Renderer.WorldTransform;
		foreach ( var body in Bodies.Values )
		{
			Rigidbody component = body.Component;
			if ( !component.IsValid() )
			{
				continue;
			}

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

			Transform transform = worldTransform.ToLocal( component.WorldTransform );
			sceneModel.SetBoneOverride( body.Bone, in transform );
		}
	}

	private void PositionPhysicsFromRendererBones()
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

	private void MovePhysicsFromRenderBones()
	{
		if ( !Renderer.IsValid() || !Renderer.SceneModel.IsValid() )
			return;

		foreach ( var pair in Bodies )
		{
			if ( !pair.Value.Component.IsValid() || !Renderer.TryGetBoneTransformAnimation( pair.Key, out var transform ) )
				continue;

			pair.Value.Component.SmoothMove( in transform, 0.1f, Time.Delta );
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

		BoneObjects = Model.CreateBoneObjects( Renderer.GameObject );

		Transform worldTransform = Renderer.WorldTransform;
		CreateParts( physics, worldTransform );
		CreateJoints( physics );
		foreach ( var body in Bodies.Values )
		{
			body.Component.Enabled = true;
		}

		foreach ( var joint in Joints )
		{
			joint.Component.Enabled = true;
		}

		Network?.Refresh();
	}

	private void CreateParts( PhysicsGroupDescription physics, Transform world )
	{
		var bones = Model.Bones;
		foreach ( var part in physics.Parts )
		{
			BoneCollection.Bone bone = bones.GetBone( part.BoneName );
			if ( !BoneObjects.TryGetValue( bone, out var value ) )
			{
				continue;
			}

			if ( !value.Flags.Contains( GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone ) )
			{
				value.Flags |= GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone;
				if ( !Renderer.IsValid() || !Renderer.TryGetBoneTransform( in bone, out var tx ) )
				{
					tx = world.ToWorld( part.Transform );
				}

				value.WorldTransform = tx;
			}

			Rigidbody rigidbody = value.AddComponent<Rigidbody>( startEnabled: false );
			rigidbody.LinearDamping = part.LinearDamping;
			rigidbody.AngularDamping = part.AngularDamping;
			rigidbody.MassOverride = part.Mass;
			rigidbody.OverrideMassCenter = part.OverrideMassCenter;
			rigidbody.MassCenterOverride = part.MassCenterOverride;
			Transform child = rigidbody.WorldTransform;
			List<Collider> colliders = new();
			//BodyTransforms.Set( Bodies.Count, child );
			foreach ( PhysicsGroupDescription.BodyPart.SpherePart sphere in part.Spheres )
			{
				SphereCollider sphereCollider = value.AddComponent<SphereCollider>();
				sphereCollider.Center = sphere.Sphere.Center;
				sphereCollider.Radius = sphere.Sphere.Radius;
				sphereCollider.Surface = sphere.Surface;
				colliders.Add( sphereCollider );
			}

			foreach ( PhysicsGroupDescription.BodyPart.CapsulePart capsule in part.Capsules )
			{
				CapsuleCollider capsuleCollider = value.AddComponent<CapsuleCollider>();
				capsuleCollider.Start = capsule.Capsule.CenterA;
				capsuleCollider.End = capsule.Capsule.CenterB;
				capsuleCollider.Radius = capsule.Capsule.Radius;
				capsuleCollider.Surface = capsule.Surface;
				colliders.Add( capsuleCollider );
			}

			foreach ( PhysicsGroupDescription.BodyPart.HullPart hull in part.Hulls )
			{
				HullCollider hullCollider = value.AddComponent<HullCollider>();
				hullCollider.Type = HullCollider.PrimitiveType.Points;
				hullCollider.Points = hull.GetPoints().ToList();
				hullCollider.Surface = hull.Surface;
				colliders.Add( hullCollider );
			}
			Bodies.Add( bone, new Body( rigidbody, bone.Index, colliders ) );
		}
	}

	private void CreateJoints( PhysicsGroupDescription physics )
	{
		foreach ( PhysicsGroupDescription.Joint joint2 in physics.Joints )
		{
			Body body = Bodies.ElementAt( joint2.Body1 ).Value;
			Body body2 = Bodies.ElementAt( joint2.Body2 ).Value;
			Transform child = joint2.Frame1;
			Transform localFrame = joint2.Frame2;
			Sandbox.Joint joint = null;
			if ( joint2.Type == PhysicsGroupDescription.JointType.Hinge )
			{
				Sandbox.HingeJoint hingeJoint = body.Component.AddComponent<Sandbox.HingeJoint>( startEnabled: false );
				if ( joint2.EnableTwistLimit )
				{
					hingeJoint.MinAngle = joint2.TwistMin;
					hingeJoint.MaxAngle = joint2.TwistMax;
				}

				if ( joint2.EnableAngularMotor )
				{
					float rad = body.Component.WorldTransform.ToWorld( in child ).Rotation.Up.Dot( joint2.AngularTargetVelocity );
					hingeJoint.Motor = Sandbox.HingeJoint.MotorMode.TargetVelocity;
					hingeJoint.TargetVelocity = rad.RadianToDegree();
					hingeJoint.MaxTorque = joint2.MaxTorque;
				}

				joint = hingeJoint;
			}
			else if ( joint2.Type == PhysicsGroupDescription.JointType.Ball )
			{
				BallJoint ballJoint = body.Component.AddComponent<BallJoint>( startEnabled: false );
				if ( joint2.EnableSwingLimit )
				{
					ballJoint.SwingLimitEnabled = true;
					ballJoint.SwingLimit = new Vector2( joint2.SwingMin, joint2.SwingMax );
				}

				if ( joint2.EnableTwistLimit )
				{
					ballJoint.TwistLimitEnabled = true;
					ballJoint.TwistLimit = new Vector2( joint2.TwistMin, joint2.TwistMax );
				}

				joint = ballJoint;
			}
			else if ( joint2.Type == PhysicsGroupDescription.JointType.Fixed )
			{
				FixedJoint fixedJoint = body.Component.AddComponent<FixedJoint>( startEnabled: false );
				fixedJoint.LinearFrequency = joint2.LinearFrequency;
				fixedJoint.LinearDamping = joint2.LinearDampingRatio;
				fixedJoint.AngularFrequency = joint2.AngularFrequency;
				fixedJoint.AngularDamping = joint2.AngularDampingRatio;
				joint = fixedJoint;
			}
			else if ( joint2.Type == PhysicsGroupDescription.JointType.Slider )
			{
				SliderJoint sliderJoint = body.Component.AddComponent<SliderJoint>( startEnabled: false );
				if ( joint2.EnableLinearLimit )
				{
					sliderJoint.MinLength = joint2.LinearMin;
					sliderJoint.MaxLength = joint2.LinearMax;
				}

				Rotation rotation = Rotation.FromPitch( -90f );
				child = child.WithRotation( rotation * child.Rotation );
				localFrame = localFrame.WithRotation( rotation * localFrame.Rotation );
				joint = sliderJoint;
			}

			if ( joint.IsValid() )
			{
				joint.Body = body2.Component.GameObject;
				joint.Attachment = Sandbox.Joint.AttachmentMode.LocalFrames;
				joint.LocalFrame1 = child.WithPosition( joint2.Frame1.Position * body.Component.WorldScale );
				joint.LocalFrame2 = localFrame.WithPosition( joint2.Frame2.Position * body2.Component.WorldScale );
				joint.EnableCollision = joint2.EnableCollision;
				joint.BreakForce = joint2.LinearStrength;
				joint.BreakTorque = joint2.AngularStrength;
				Joints.Add( new Joint( joint, body, body2 ) );
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

		foreach ( var componentsInChild in GetComponentsInChildren<Collider>( includeDisabled: true ) )
		{
			if ( componentsInChild.IsValid() && componentsInChild.GameObject.Flags.Contains( GameObjectFlags.PhysicsBone ) )
			{
				componentsInChild.Destroy();
			}
		}

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				body.Component.GameObject.Flags &= ~GameObjectFlags.Absolute;
				body.Component.GameObject.Flags &= ~GameObjectFlags.PhysicsBone;
				body.Component.Destroy();
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
		{
			EnablePhysics();
		}

		if ( mode == RagdollMode.Active )
		{
			EnablePhysics();
		}
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

		PositionPhysicsFromRendererBones();
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
