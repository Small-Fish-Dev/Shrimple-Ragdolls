public partial class ShrimpleActiveRagdoll
{
	public readonly record struct Joint( Sandbox.Joint Component, Body Body1, Body Body2 );
	public List<Joint> Joints { get; protected set; } = new();

	protected void CreateJoints( PhysicsGroupDescription physics )
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

	/// <summary>
	/// Destroy all joint components and clear the joints list
	/// </summary>
	protected void DestroyJoints()
	{
		if ( Joints == null )
			return;

		foreach ( var joint in Joints )
			if ( joint.Component.IsValid() )
				joint.Component.Destroy();

		Joints.Clear();
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
