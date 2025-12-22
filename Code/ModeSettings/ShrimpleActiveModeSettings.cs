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

	public override void ApplySettings( ShrimpleRagdoll ragdoll, ShrimpleRagdoll.Body body )
	{
		if ( ForceGravityDisabled )
			body.Component?.Gravity = false;

		ragdoll.LerpTime = LerpTime;
	}
}
