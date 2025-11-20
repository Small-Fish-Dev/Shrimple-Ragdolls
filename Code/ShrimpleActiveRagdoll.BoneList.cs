using System.Text.Json.Serialization;

public partial class ShrimpleActiveRagdoll
{
	public class BoneFollowOption
	{
		[KeyProperty]
		public RagdollFollowMode FollowMode { get; set; } = RagdollFollowMode.All;
		[KeyProperty]
		public BoneList Bone { get; set; } = new();
	}

	public class BoneList
	{
		[JsonIgnore]
		public List<string> Options => Model.Physics.Parts.Select( x => x.BoneName ).ToList();
		public Model Model { get; set; }
		public string Selected { get; set; }
	}

	protected void SetupBoneList()
	{
		// Can't do this inside of OnValidate, still broken somehow
		if ( !FollowOptions.Bone.Model.IsValid() || FollowOptions.Bone.Model != Renderer.Model )
		{
			FollowOptions.Bone?.Model = Model;
			FollowOptions.Bone.Selected = Model.Physics.Parts.Select( x => x.BoneName ).FirstOrDefault();
		}
	}
}
