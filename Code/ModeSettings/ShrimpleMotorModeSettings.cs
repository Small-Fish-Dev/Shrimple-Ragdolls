namespace ShrimpleRagdolls;

/// <summary>
/// Settings component for Motor mode.
/// Controls joint motor frequency and damping for physics-driven animation following.
/// </summary>
public class ShrimpleMotorModeSettings : ShrimpleModeSettings
{
	public override string TargetMode => ShrimpleRagdollMode.Motor;

	/// <summary>
	/// How fast the motor responds to reach the target rotation
	/// </summary>
	[Property, Range( 0f, 100f ), Step( 1f )]
	public float Frequency
	{
		get;
		set
		{
			field = value;
			ApplyToAllBodiesInMode();
		}
	} = 30f;

	/// <summary>
	/// How much the motor resists oscillation
	/// </summary>
	[Property, Range( 0f, 5f ), Step( 0.1f )]
	public float DampingRatio
	{
		get;
		set
		{
			field = value;
			ApplyToAllBodiesInMode();
		}
	} = 1f;

	public override void ApplySettings( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		var joint = body.GetParentJoint()?.Component;
		if ( !joint.IsValid() )
			return;

		if ( joint is BallJoint ballJoint )
		{
			ballJoint.Frequency = Frequency;
			ballJoint.DampingRatio = DampingRatio;
		}
		else if ( joint is HingeJoint hingeJoint )
		{
			hingeJoint.Frequency = Frequency;
			hingeJoint.DampingRatio = DampingRatio;
		}
	}
}
