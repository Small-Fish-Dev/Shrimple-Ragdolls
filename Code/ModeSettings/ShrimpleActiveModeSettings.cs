namespace ShrimpleRagdolls;

/// <summary>
/// Settings component for Active mode.
/// </summary>
public class ShrimpleActiveModeSettings : ShrimpleModeSettings
{
	public override string TargetMode => ShrimpleRagdollMode.Active;

	/// <summary>
	/// How fast the bodies take to interpolate to their desired position<br />
	/// Higher = "weaker" bodies
	/// </summary>
	[Property, Range( 0f, 1f ), Step( 0.1f )]
	public float LerpTime
	{
		get;
		set
		{
			field = value;
			ApplyToAllBodiesInMode();
		}
	} = 0f;

	/// <summary>
	/// Gravity is hard to counteract for active ragdolls so we disable it by default<br />
	/// If set to false, it will also respect <see cref="ShrimpleRagdoll.GravityScale"/>
	/// </summary>
	[Property]
	public bool ForceGravityDisabled
	{
		get;
		set
		{
			field = value;
			ApplyToAllBodiesInMode();
		}
	} = true;

	/// <summary>
	/// Joint limits are useful to tie bodies together in natural ways when deviating from animations<br />
	/// But sometimes animators cheat by making joints go beyond their natural limits, which makes our active ragdoll jittery<br />
	/// Multiply the joint limits by a value to give them more freedom to move
	/// </summary>
	[Property, Range( 1f, 2f ), Step( 0.1f )]
	public float JointLimitsMultiplier
	{
		get;
		set
		{
			field = value;
			ApplyToAllBodiesInMode();
		}
	} = 1.5f;

	public override void ApplySettings( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( ForceGravityDisabled )
			body.Component?.Gravity = false;

		ragdoll.LerpTime = LerpTime;

		var joint = body.GetParentJoint();
		if ( joint == null || !joint.Value.Component.IsValid() ) return;

		ragdoll.ResetJointSettings( joint.Value );

		if ( joint.Value.Component is BallJoint ballJoint )
		{
			ballJoint.SwingLimit *= JointLimitsMultiplier;
			ballJoint.TwistLimit *= JointLimitsMultiplier;
		}
		else if ( joint.Value.Component is HingeJoint hingeJoint )
		{
			hingeJoint.MinAngle *= JointLimitsMultiplier;
			hingeJoint.MaxAngle *= JointLimitsMultiplier;
		}
	}
}
