namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// Bones that currently have a partial ragdoll override
	/// </summary>
	public IReadOnlyDictionary<int, RagdollMode> PartialRagdollOverrides => _partialRagdollOverrides;
	private readonly Dictionary<int, RagdollMode> _partialRagdollOverrides = new();

	/// <summary>
	/// Ragdolls a single bone and optionally all its children
	/// </summary>
	/// <param name="rootBone">The target bone</param>
	/// <param name="mode">Which mode to set the bone (Only works for Enabled and Motor)</param>
	/// <param name="includeChildren">Include all children of the target bone</param>
	public void RagdollBone( BoneCollection.Bone rootBone, RagdollMode mode = RagdollMode.Enabled, bool includeChildren = true )
	{
		if ( Mode == RagdollMode.None )
			return;

		if ( !Renderer.IsValid() || !Renderer.Model.IsValid() || rootBone == null )
			return;

		var bones = includeChildren ? GetDescendantBones( rootBone ) : new[] { rootBone };
		var indices = bones.Select( b => b.Index ).ToHashSet();

		foreach ( var body in Bodies )
		{
			if ( !indices.Contains( body.Bone ) || !body.Component.IsValid() )
				continue;

			foreach ( var collider in body.Component.GameObject.GetComponents<Collider>() )
				collider.Enabled = true;

			body.Component.Enabled = true;
			body.Component.MotionEnabled = true;
			body.Component.Gravity = Gravity; // We reset this for active mode so reenable it
		}

		// Gotta enable joints last otherwise they error out
		foreach ( var joint in GetJointsForBones( indices ) )
		{
			joint.Component.Enabled = true;

			if ( joint.Component is BallJoint ballJoint )
			{
				ballJoint.Motor = BallJoint.MotorMode.Disabled;
				ballJoint.Frequency = 0f;
			}
			else if ( joint.Component is HingeJoint hingeJoint )
			{
				hingeJoint.Motor = HingeJoint.MotorMode.Disabled;
				hingeJoint.Frequency = 0f;
			}
		}

		foreach ( var index in indices )
			_partialRagdollOverrides[index] = mode;
	}

	public void RagdollBone( string boneName, RagdollMode mode = RagdollMode.Enabled, bool includeChildren = true )
		=> RagdollBone( Renderer?.Model?.Bones?.GetBone( boneName ), mode, includeChildren );

	public void RagdollBone( int boneIndex, RagdollMode mode = RagdollMode.Enabled, bool includeChildren = true )
		=> RagdollBone( Renderer?.Model?.Bones?.AllBones[boneIndex], mode, includeChildren );

	/// <summary>
	/// Unragdoll the bone optionally all its children
	/// </summary>
	public void UnragdollBone( BoneCollection.Bone rootBone, bool includeChildren = true )
	{
		if ( !Renderer.IsValid() || !Renderer.Model.IsValid() || rootBone == null )
			return;

		var bones = includeChildren ? GetDescendantBones( rootBone ) : new[] { rootBone };
		var indices = bones.Select( b => b.Index ).ToHashSet();

		var modeWantsRigidbodies = Mode != RagdollMode.None;
		var modeWantsMotion = Mode == RagdollMode.Enabled || Mode == RagdollMode.Active || Mode == RagdollMode.Motor;
		var modeWantsJoints = Mode != RagdollMode.None && Mode != RagdollMode.Passive;
		var modeWantsGravity = Mode != RagdollMode.Active && Gravity;

		// Gotta disable joints first otherwise they error out
		foreach ( var joint in GetJointsForBones( indices ) )
		{
			if ( joint.Component.IsValid() )
				joint.Component.Enabled = modeWantsJoints;
		}

		foreach ( var body in Bodies )
		{
			if ( !indices.Contains( body.Bone ) || !body.Component.IsValid() )
				continue;

			body.Component.MotionEnabled = modeWantsMotion;
			body.Component.Enabled = modeWantsRigidbodies;
			body.Component.Gravity = modeWantsGravity;

			foreach ( var collider in body.Component.GameObject.GetComponents<Collider>() )
				collider.Enabled = modeWantsRigidbodies;
		}

		foreach ( var index in indices )
			_partialRagdollOverrides.Remove( index );
	}

	public void UnragdollBone( string boneName, bool includeChildren = true )
		=> UnragdollBone( Renderer?.Model?.Bones?.GetBone( boneName ), includeChildren );

	public void UnragdollBone( int boneIndex, bool includeChildren = true )
		=> UnragdollBone( Renderer?.Model?.Bones?.AllBones[boneIndex], includeChildren );

	/// <summary>
	/// Clear all partial ragdoll overrides, returning full control to the global mode.
	/// </summary>
	public void ClearPartialRagdoll() => _partialRagdollOverrides.Clear();

	/// <summary>
	/// Bone overrides to apply on start and whenever this property is changed.
	/// Key is the bone name, value is the ragdoll mode.
	/// </summary>
	[Advanced, Property, Group( "Partial Ragdoll" )]
	public Dictionary<string, RagdollMode> PartialRagdollConfig
	{
		get;
		set
		{
			field = value;
			ApplyPartialRagdollConfig();
		}
	} = new();

	private void ApplyPartialRagdollConfig()
	{
		ClearPartialRagdoll();

		if ( PartialRagdollConfig == null )
			return;

		foreach ( var (boneName, mode) in PartialRagdollConfig )
			RagdollBone( boneName, mode );
	}

	public bool IsBonePartiallyRagdolled( BoneCollection.Bone bone )
		=> bone != null && _partialRagdollOverrides.ContainsKey( bone.Index );

	public bool IsBonePartiallyRagdolled( string boneName )
		=> IsBonePartiallyRagdolled( Renderer?.Model?.Bones?.GetBone( boneName ) );

	public bool IsBonePartiallyRagdolled( int boneIndex )
		=> _partialRagdollOverrides.ContainsKey( boneIndex );

	public RagdollMode? GetBoneOverrideMode( BoneCollection.Bone bone )
		=> bone != null && _partialRagdollOverrides.TryGetValue( bone.Index, out var m ) ? m : null;

	private IEnumerable<ModelPhysics.Joint> GetJointsForBones( HashSet<int> boneIndices )
		=> Joints?.Where( j => j.Component.IsValid() && boneIndices.Contains( j.Body2.Bone ) )
		   ?? Enumerable.Empty<ModelPhysics.Joint>();
}
