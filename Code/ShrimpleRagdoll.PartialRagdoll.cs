namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	private readonly HashSet<int> _partialRagdollBoneIndices = new();

	/// <summary>
	/// Currently ragdolled bones
	/// </summary>
	public IEnumerable<BoneCollection.Bone> PartialRagdollBones => _partialRagdollBoneIndices
		.Select( i => Renderer?.Model?.Bones?.AllBones[i] )
		.Where( b => b != null );

	/// <summary>
	/// Ragdolls a single bone and optionally all its children
	/// </summary>
	public void RagdollBone( BoneCollection.Bone rootBone, bool includeChildren = true )
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
			_partialRagdollBoneIndices.Add( index );
	}

	public void RagdollBone( string boneName, bool includeChildren = true )
		=> RagdollBone( Renderer?.Model?.Bones?.GetBone( boneName ), includeChildren );

	public void RagdollBone( int boneIndex, bool includeChildren = true )
		=> RagdollBone( Renderer?.Model?.Bones?.AllBones[boneIndex], includeChildren );

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
			_partialRagdollBoneIndices.Remove( index );
	}

	public void UnragdollBone( string boneName, bool includeChildren = true )
		=> UnragdollBone( Renderer?.Model?.Bones?.GetBone( boneName ), includeChildren );

	public void UnragdollBone( int boneIndex, bool includeChildren = true )
		=> UnragdollBone( Renderer?.Model?.Bones?.AllBones[boneIndex], includeChildren );

	/// <summary>
	/// Clear all partial ragdoll overrides, returning full control to the global mode.
	/// </summary>
	public void ClearPartialRagdoll() => _partialRagdollBoneIndices.Clear();

	public bool IsBonePartiallyRagdolled( BoneCollection.Bone bone )
		=> bone != null && _partialRagdollBoneIndices.Contains( bone.Index );

	public bool IsBonePartiallyRagdolled( string boneName )
		=> IsBonePartiallyRagdolled( Renderer?.Model?.Bones?.GetBone( boneName ) );

	public bool IsBonePartiallyRagdolled( int boneIndex )
		=> _partialRagdollBoneIndices.Contains( boneIndex );

	private IEnumerable<ModelPhysics.Joint> GetJointsForBones( HashSet<int> boneIndices )
		=> Joints?.Where( j => j.Component.IsValid() && boneIndices.Contains( j.Body2.Bone ) )
		   ?? Enumerable.Empty<ModelPhysics.Joint>();
}
