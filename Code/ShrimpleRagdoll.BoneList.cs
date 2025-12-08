using System.Text.Json.Serialization;

public partial class ShrimpleRagdoll
{
	public class BoneFollowOption
	{
		/// <summary>
		/// If the root object is not the renderer, make that follow the renderer
		/// </summary>
		[KeyProperty]
		public bool RootObjectFollow { get; set; } = true;
		/// <summary>
		/// How the renderer will follow the ragdoll
		/// </summary>
		[KeyProperty]
		public RagdollFollowMode FollowMode { get; set; } = RagdollFollowMode.All;
		/// <summary>
		/// Which bone the renderer will merge to
		/// </summary>
		[KeyProperty]
		public BoneList Bone { get; set; } = new();
		/// <summary>
		/// Merge the renderer's bone transform to the selected bone's transform
		/// </summary>
		[KeyProperty]
		public bool MergeBoneTransforms { get; set; } = true;
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
