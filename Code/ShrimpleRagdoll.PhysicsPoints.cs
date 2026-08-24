namespace ShrimpleRagdolls;

public partial class ShrimpleRagdoll
{
	/// <summary>
	/// To compensate for height parameters, ModelPhysics scales the joint's frames on creation to match<br/>
	/// But that doesn't update the physics points and they up misaligned, very visible with extremely short people.<br/>
	/// We manually sync the physics points here
	/// </summary>
	public void UpdatePhysicsPoints()
	{
		var joints = ModelPhysics?.Joints;
		if ( joints == null )
			return;

		for ( int i = 0; i < joints.Count; i++ )
		{
			var joint = joints[i];
			if ( !joint.Component.IsValid() ) continue;

			var rb1 = joint.Body1.Component;
			var rb2 = joint.Body2.Component;
			if ( !rb1.IsValid() || !rb2.IsValid() ) continue;

			var physicsBody1 = rb1.PhysicsBody;
			var physicsBody2 = rb2.PhysicsBody;
			var jointBody = joint.Component.Body;
			if ( !physicsBody1.IsValid() || !physicsBody2.IsValid() || !jointBody.IsValid() ) continue;

			var worldScale1 = joint.Component.WorldTransform.UniformScale;
			var p1WorldPos = physicsBody1.Transform.PointToWorld( joint.LocalFrame1.Position * worldScale1 );

			var worldScale2 = jointBody.WorldTransform.UniformScale;
			var p2LocalPos = physicsBody2.Transform.PointToLocal( p1WorldPos );

			joints[i] = joint with { LocalFrame2 = joint.LocalFrame2.WithPosition( p2LocalPos / worldScale2 ) };

			var p2 = joint.Component.Point2;
			p2.LocalPosition = p2LocalPos;
			joint.Component.Point2 = p2;
		}
	}
}
