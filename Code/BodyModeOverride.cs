/// <summary>
/// Defines a mode override for a specific bone and optionally its children
/// </summary>
[Serializable]
public class BodyModeOverride
{
	/// <summary>
	/// The bone to apply the mode to
	/// </summary>
	[Property]
	public ShrimpleRagdoll.BoneList Bone { get; set; } = new();

	/// <summary>
	/// The mode to apply to this bone
	/// </summary>
	[Property]
	public ShrimpleRagdollModeProperty Mode { get; set; } = ShrimpleRagdollMode.Disabled;

	/// <summary>
	/// If true, also applies this mode to all children and descendants of this bone
	/// </summary>
	[Property]
	public bool IncludeChildren { get; set; } = false;

	public BodyModeOverride()
	{
	}

	public BodyModeOverride( ShrimpleRagdoll.BoneList bone, string mode, bool includeChildren = false )
	{
		Bone = bone;
		Mode = mode;
		IncludeChildren = includeChildren;
	}
}
