namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// List of joints from ModelPhysics
	/// </summary>
	public List<ModelPhysics.Joint> Joints => ModelPhysics?.Joints;

	/// <summary>
	/// Disables all joints
	/// </summary>
	public void DisableJoints()
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
	public void EnableJoints()
	{
		if ( Joints == null )
			return;

		foreach ( var joint in Joints )
		{
			if ( joint.Component.IsValid() )
				joint.Component.Enabled = true;
		}
	}

	/// <summary>
	/// Get the index of a joint in the Joints list
	/// </summary>
	public int GetJointIndex( ModelPhysics.Joint joint )
	{
		if ( Joints == null )
			return -1;

		for ( int i = 0; i < Joints.Count; i++ )
		{
			if ( Joints[i].Component == joint.Component )
				return i;
		}
		return -1;
	}

	/// <summary>
	/// Reset a joint's settings to the original model physics values
	/// </summary>
	public void ResetJointSettings( ModelPhysics.Joint joint )
	{
		if ( !joint.Component.IsValid() )
			return;

		var jointIndex = GetJointIndex( joint );
		var physics = Model?.Physics;
		if ( physics == null || jointIndex < 0 || jointIndex >= physics.Joints.Count )
			return;

		var jointDesc = physics.Joints[jointIndex];

		if ( joint.Component is HingeJoint hingeJoint )
		{
			if ( jointDesc.EnableTwistLimit )
			{
				hingeJoint.MinAngle = jointDesc.TwistMin;
				hingeJoint.MaxAngle = jointDesc.TwistMax;
			}
		}
		else if ( joint.Component is BallJoint ballJoint )
		{
			if ( jointDesc.EnableSwingLimit )
			{
				ballJoint.SwingLimitEnabled = true;
				ballJoint.SwingLimit = new Vector2( jointDesc.SwingMin, jointDesc.SwingMax );
			}

			if ( jointDesc.EnableTwistLimit )
			{
				ballJoint.TwistLimitEnabled = true;
				ballJoint.TwistLimit = new Vector2( jointDesc.TwistMin, jointDesc.TwistMax );
			}
		}
		else if ( joint.Component is FixedJoint fixedJoint )
		{
			fixedJoint.LinearFrequency = jointDesc.LinearFrequency;
			fixedJoint.LinearDamping = jointDesc.LinearDampingRatio;
			fixedJoint.AngularFrequency = jointDesc.AngularFrequency;
			fixedJoint.AngularDamping = jointDesc.AngularDampingRatio;
		}
		else if ( joint.Component is SliderJoint sliderJoint )
		{
			if ( jointDesc.EnableLinearLimit )
			{
				sliderJoint.MinLength = jointDesc.LinearMin;
				sliderJoint.MaxLength = jointDesc.LinearMax;
			}
		}
	}

	/// <summary>
	/// Reset a joint's settings by index
	/// </summary>
	public void ResetJointSettings( int jointIndex )
	{
		if ( Joints == null || jointIndex < 0 || jointIndex >= Joints.Count )
			return;

		ResetJointSettings( Joints[jointIndex] );
	}

	/// <summary>
	/// Reset all joints to their original model physics settings
	/// </summary>
	public void ResetAllJointSettings()
	{
		if ( Joints == null )
			return;

		for ( int i = 0; i < Joints.Count; i++ )
			ResetJointSettings( i );
	}
}
