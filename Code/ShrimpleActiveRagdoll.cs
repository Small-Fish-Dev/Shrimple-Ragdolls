using Sandbox;
using Sandbox.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ShrimpleActiveRagdoll : Component
{
	[Property]
	public SkinnedModelRenderer Renderer { get; set; }
	public Model Model => Renderer?.Model;
	public List<ModelPhysics.Body> Bodies { get; } = new();
	public List<ModelPhysics.Joint> Joints { get; } = new();
	//private NetworkTransforms BodyTransforms = new NetworkTransforms();

	protected override void OnStart()
	{
		base.OnStart();

		CreatePhysics();
	}

	private void CreatePhysics()
	{
		if ( !base.Active || base.IsProxy  )
			return;

		DestroyPhysics();

		if ( !Model.IsValid() )
			return;

		PhysicsGroupDescription physics = Model.Physics;
		if ( physics == null || physics.Parts.Count == 0 )
			return;

		Transform worldTransform = base.WorldTransform;
		CreateParts( physics, worldTransform );
		CreateJoints( physics );
		foreach ( ModelPhysics.Body body in Bodies )
		{
			body.Component.Enabled = true;
		}

		foreach ( ModelPhysics.Joint joint in Joints )
		{
			joint.Component.Enabled = true;
		}

		base.Network?.Refresh();
	}

	private void CreateParts( PhysicsGroupDescription physics, Transform world)
	{
		Dictionary<BoneCollection.Bone, GameObject> dictionary = Model.CreateBoneObjects( base.GameObject );
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
			//BodyTransforms.Set( Bodies.Count, child );
			Bodies.Add( new ModelPhysics.Body( rigidbody, bone.Index, base.WorldTransform.ToLocal( in child ) ) );
			foreach ( PhysicsGroupDescription.BodyPart.SpherePart sphere in part.Spheres )
			{
				SphereCollider sphereCollider = value.AddComponent<SphereCollider>();
				sphereCollider.Center = sphere.Sphere.Center;
				sphereCollider.Radius = sphere.Sphere.Radius;
				sphereCollider.Surface = sphere.Surface;
			}

			foreach ( PhysicsGroupDescription.BodyPart.CapsulePart capsule in part.Capsules )
			{
				CapsuleCollider capsuleCollider = value.AddComponent<CapsuleCollider>();
				capsuleCollider.Start = capsule.Capsule.CenterA;
				capsuleCollider.End = capsule.Capsule.CenterB;
				capsuleCollider.Radius = capsule.Capsule.Radius;
				capsuleCollider.Surface = capsule.Surface;
			}

			foreach ( PhysicsGroupDescription.BodyPart.HullPart hull in part.Hulls )
			{
				HullCollider hullCollider = value.AddComponent<HullCollider>();
				hullCollider.Type = HullCollider.PrimitiveType.Points;
				hullCollider.Points = hull.GetPoints().ToList();
				hullCollider.Surface = hull.Surface;
			}
		}
	}

	private void CreateJoints( PhysicsGroupDescription physics )
	{
		foreach ( PhysicsGroupDescription.Joint joint2 in physics.Joints )
		{
			ModelPhysics.Body body = Bodies[joint2.Body1];
			ModelPhysics.Body body2 = Bodies[joint2.Body2];
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
				Joints.Add( new ModelPhysics.Joint( joint, body, body2, child, localFrame ) );
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

		foreach ( ModelPhysics.Joint joint in Joints )
			if ( joint.Component.IsValid() )
				joint.Component.Destroy();

		foreach ( Collider componentsInChild in GetComponentsInChildren<Collider>( includeDisabled: true ) )
		{
			if ( componentsInChild.IsValid() && componentsInChild.GameObject.Flags.Contains( GameObjectFlags.PhysicsBone ) )
			{
				componentsInChild.Destroy();
			}
		}

		foreach ( ModelPhysics.Body body in Bodies )
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
		base.Network?.Refresh();
	}
}
