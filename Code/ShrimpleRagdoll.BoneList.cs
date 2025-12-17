namespace ShrimpleRagdolls;

using System.Text.Json.Serialization;

public partial class ShrimpleRagdoll
{
	public class BoneList
	{
		[JsonIgnore]
		public List<string> Options => Model?.Physics?.Parts?.Select( x => x.BoneName ).ToList() ?? new List<string>();

		[JsonIgnore, Hide]
		public Model Model { get; set; }

		public string Selected { get; set; }

		/// <summary>
		/// Updates the model reference and sets a default selection if needed
		/// </summary>
		public void Setup( Model model )
		{
			if ( !model.IsValid() )
				return;

			// Always update the model reference since it's not serialized
			Model = model;

			// Set default selection if empty
			if ( string.IsNullOrEmpty( Selected ) && model?.Physics?.Parts?.Count > 0 )
			{
				Selected = model.Physics.Parts.Select( x => x.BoneName ).FirstOrDefault();
			}
		}
	}

	public class BoneFollowOption
	{
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

	protected void SetupBoneLists()
	{
		if ( !Model.IsValid() )
			return;

		// Setup FollowOptions bone list
		if ( FollowOptions != null )
		{
			FollowOptions.Bone ??= new BoneList();
			FollowOptions.Bone.Setup( Model );
		}

		// Setup all ModeOverride bone lists
		if ( BodyModeOverrides != null )
		{
			foreach ( var modeOverride in BodyModeOverrides )
			{
				if ( modeOverride == null )
					continue;
				modeOverride.Bone ??= new BoneList();
				modeOverride.Bone.Setup( Model );
			}
		}
	}
}
