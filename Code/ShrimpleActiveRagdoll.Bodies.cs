using System;

public partial class ShrimpleActiveRagdoll
{
	public readonly record struct Body( Rigidbody Component, int Bone, List<Collider> Colliders );
	public Dictionary<BoneCollection.Bone, Body> Bodies { get; protected set; } = new();

	protected void CreateBodies( PhysicsGroupDescription physics )
	{
		foreach ( var part in physics.Parts )
		{
			var bone = Model.Bones.GetBone( part.BoneName );

			if ( !BoneObjects.TryGetValue( bone, out var boneObject ) )
				continue;

			if ( !boneObject.Flags.Contains( GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone ) )
			{
				boneObject.Flags |= GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone;

				if ( !Renderer.IsValid() || !Renderer.TryGetBoneTransform( in bone, out var boneTransform ) )
					boneTransform = Renderer.WorldTransform.ToWorld( part.Transform );

				boneObject.WorldTransform = boneTransform;
			}

			//var child = rigidbody.WorldTransform;
			//BodyTransforms.Set( Bodies.Count, child );

			var rigidbody = boneObject.AddComponent<Rigidbody>( startEnabled: false );
			var colliders = AddCollider( boneObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone, new Body( rigidbody, bone.Index, colliders ) );
		}

		//rigidbody.PhysicsBody.RebuildMass();
	}

	protected void CreateStatueBodies( PhysicsGroupDescription physics )
	{
		var rigidbody = Renderer.GameObject.AddComponent<Rigidbody>( startEnabled: false );

		foreach ( var part in physics.Parts )
		{
			var bone = Model.Bones.GetBone( part.BoneName );

			if ( !BoneObjects.TryGetValue( bone, out var boneObject ) )
				continue;

			if ( !boneObject.Flags.Contains( GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone ) )
			{
				boneObject.Flags |= GameObjectFlags.Absolute | GameObjectFlags.PhysicsBone;

				if ( !Renderer.IsValid() || !Renderer.TryGetBoneTransform( in bone, out var boneTransform ) )
					boneTransform = Renderer.WorldTransform.ToWorld( part.Transform );

				boneObject.WorldTransform = boneTransform;
			}

			var colliders = AddCollider( Renderer.GameObject, part, boneObject.WorldTransform ).ToList();
			Bodies.Add( bone, new Body( rigidbody, bone.Index, colliders ) );
		}
	}

	protected IEnumerable<Collider> AddCollider( GameObject parent, PhysicsGroupDescription.BodyPart part, Transform worldTransform )
	{
		var localTransform = parent.WorldTransform.ToLocal( worldTransform );

		foreach ( var sphere in part.Spheres )
		{
			var sphereCollider = parent.AddComponent<SphereCollider>();
			sphereCollider.Center = localTransform.PointToWorld( sphere.Sphere.Center );
			sphereCollider.Radius = sphere.Sphere.Radius;
			sphereCollider.Surface = sphere.Surface;
			yield return sphereCollider;
		}
		foreach ( var capsule in part.Capsules )
		{
			var capsuleCollider = parent.AddComponent<CapsuleCollider>();
			capsuleCollider.Start = localTransform.PointToWorld( capsule.Capsule.CenterA );
			capsuleCollider.End = localTransform.PointToWorld( capsule.Capsule.CenterB );
			capsuleCollider.Radius = capsule.Capsule.Radius;
			capsuleCollider.Surface = capsule.Surface;
			yield return capsuleCollider;
		}
		foreach ( var hull in part.Hulls )
		{
			var hullCollider = parent.AddComponent<HullCollider>();
			hullCollider.Type = HullCollider.PrimitiveType.Points;
			hullCollider.Points = hull.GetPoints().ToList();
			hullCollider.Surface = hull.Surface;
			hullCollider.Center = localTransform.Position;
			yield return hullCollider;
		}
	}

	/// <summary>
	/// Disables all the rigidbodies and colliders
	/// </summary>
	protected void DisableBodies()
	{
		if ( Bodies == null )
			return;

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				body.Component.GameObject.Flags &= ~GameObjectFlags.Absolute;
				body.Component.GameObject.Flags &= ~GameObjectFlags.PhysicsBone;
				body.Component.Enabled = false;

				foreach ( var collider in body.Colliders )
				{
					if ( collider.IsValid() )
						collider.Enabled = false;
				}
			}
		}
	}

	/// <summary>
	/// Enables all the rigidbodies and colliders
	/// </summary>
	protected void EnableBodies()
	{
		if ( Bodies == null )
			return;

		foreach ( var body in Bodies.Values )
		{
			if ( body.Component.IsValid() )
			{
				body.Component.GameObject.Flags |= GameObjectFlags.Absolute;
				body.Component.GameObject.Flags |= GameObjectFlags.PhysicsBone;
				body.Component.Enabled = true;

				foreach ( var collider in body.Colliders )
				{
					if ( collider.IsValid() )
						collider.Enabled = true;
				}
			}
		}
	}
}
