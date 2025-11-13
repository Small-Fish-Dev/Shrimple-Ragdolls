using Sandbox;
using Sandbox.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ShrimpleActiveRagdoll : Component
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

	public readonly record struct Body( Rigidbody Component, int Bone, List<Collider> Colliders );
	public readonly record struct Joint( Sandbox.Joint Component, Body Body1, Body Body2 );


	public Model Model => Renderer?.Model;
	public List<Body> Bodies { get; } = new();
	public List<Joint> Joints { get; } = new();
	//private NetworkTransforms BodyTransforms = new NetworkTransforms();

	private Transform[] _rendererBonePosition;
	private SkinnedModelRenderer.BoneVelocity[] _rendererBoneVelocity;

	protected override void OnStart()
	{
		base.OnStart();

		CreatePhysics();
		SetRagdollMode( Mode );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( Mode == RagdollMode.Enabled)
			PositionRendererBonesFromPhysics();
		if ( Mode == RagdollMode.Passive )
			PositionPhysicsFromRendererBones();
		if ( Mode == RagdollMode.Active )
			PositionPhysicsFromRendererBones();
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
		foreach ( Body body in Bodies )
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
		if ( (_rendererBonePosition == null || _rendererBoneVelocity == null) && Renderer != null && Renderer.SceneModel.IsValid() )
		{
			_rendererBonePosition = Renderer.GetBoneTransforms( world: true );
			_rendererBoneVelocity = Renderer.GetBoneVelocities();
		}

		if ( _rendererBonePosition == null || _rendererBoneVelocity == null )
		{
			return;
		}

		foreach ( KeyValuePair<BoneCollection.Bone, GameObject> item in Model.CreateBoneObjects( Renderer.GameObject ) )
		{
			if ( item.Key != null && item.Value.IsValid() && _rendererBonePosition.Length > item.Key.Index && _rendererBoneVelocity.Length > item.Key.Index )
			{
				Transform worldTransform = _rendererBonePosition[item.Key.Index];
				SkinnedModelRenderer.BoneVelocity boneVelocity = _rendererBoneVelocity[item.Key.Index];
				Rigidbody component = item.Value.GetComponent<Rigidbody>();
				if ( component != null )
				{
					component.WorldTransform = worldTransform;
					component.Velocity = boneVelocity.Linear;
					component.AngularVelocity = boneVelocity.Angular;
				}
			}
		}

		_rendererBoneVelocity = null;
		_rendererBonePosition = null;
	}


	private void CreatePhysics()
	{
		if ( !Active || IsProxy  )
			return;

		DestroyPhysics();

		if ( !Model.IsValid() )
			return;

		PhysicsGroupDescription physics = Model.Physics;
		if ( physics == null || physics.Parts.Count == 0 )
			return;

		Transform worldTransform = Renderer.WorldTransform;
		CreateParts( physics, worldTransform );
		CreateJoints( physics );
		foreach ( Body body in Bodies )
		{
			body.Component.Enabled = true;
		}

		foreach ( Joint joint in Joints )
		{
			joint.Component.Enabled = true;
		}

		Network?.Refresh();
	}

	private void CreateParts( PhysicsGroupDescription physics, Transform world)
	{
		Dictionary<BoneCollection.Bone, GameObject> dictionary = Model.CreateBoneObjects( Renderer.GameObject );
		BoneCollection bones = Model.Bones;
		foreach ( PhysicsGroupDescription.BodyPart part in physics.Parts )
		{
			BoneCollection.Bone bone = bones.GetBone( part.BoneName );
			if ( !dictionary.TryGetValue( bone, out var value ) )
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
			Bodies.Add( new Body( rigidbody, bone.Index, colliders ) );
		}
	}

	private void CreateJoints( PhysicsGroupDescription physics )
	{
		foreach ( PhysicsGroupDescription.Joint joint2 in physics.Joints )
		{
			Body body = Bodies[joint2.Body1];
			Body body2 = Bodies[joint2.Body2];
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

		foreach ( var body in Bodies )
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

	private void DisablePhysics()
	{
		if ( Bodies != null )
			foreach ( var body in Bodies )
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

		if ( Joints != null )
			foreach ( var joint in Joints )
				if ( joint.Component.IsValid() )
					joint.Component.Enabled = false;

		Renderer?.ClearPhysicsBones();
	}

	private void EnablePhysics()
	{
		if ( Bodies != null )
			foreach ( var body in Bodies )
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

		if ( Joints != null )
			foreach ( var joint in Joints )
				if ( joint.Component.IsValid() )
					joint.Component.Enabled = true;

		PositionPhysicsFromRendererBones();
	}
}
