<img width="1280" height="720" alt="ShrimpleRagdollsWide" src="https://github.com/user-attachments/assets/2c3f9dc5-bc71-4b94-b3b3-38f183948ac1" />

# Shrimple Ragdolls
### A ModelPhysics wrapper that expands its features and lets you easily switch to different ragdoll modes.

- **Enabled:** The default mode we all know as "ragdoll", driven by physics
- **Passive:** Collisions enabled on all bodies, driven by animations
- **Active:** Collisions and simulation enabled, but bodies follow the renderer, driven by animations then by physics
- **Motor:** Everything enabled, but the joint's motors are set to follow their parent space animations, similar to euphoria ragdolls    
- **Driven:** Like Active, but the bodies are pulled toward the animation by springs with a *finite* force budget, so they can be overpowered. Grabs, punches and walls actually win and drag the rest of the ragdoll with them

### How to use
Shrimple look up the Shrimple Ragdoll component and add it to your gameobject, then set the renderer you want to target and the mode.
If you want to mess with physics beforehand, like tying the ragdolls like in the video, shrimple enable CreateGameObjects on the renderer.

### Lerp
Shrimple Ragdoll also offers lerping between various modes to smoothly transition, for example from Enabled to Passive, like getting up.

- **StartLerpMeshToAnimation( duration, targetMode ):** Override the bone's transform to the current animation pose, then switch to targetMode
- **StartLerpObjectsToAnimation( duraction, targetMode):** Lerp the objects to the current animation pose, then switch to targetMode
- **StartLerpBodiesToAnimation( duration, targetMode):** Lerp the bodies physically to the current animation pose, then switch to targetMode
And various other overrides that lets you select specific bodies and modes.

### Procedural hit reactions
With the Shrimple Ragdoll component, you'll be able to use procedural localized hit reactions on your model: You define a hit position, hit direction, strength, radius, duration, and it will automatically and smoothly translate the impacted bones backwards, twisting and offsetting depending on the force.

- **ApplyHitReaction( hitPosition, force, radius, duration, rotationStrength ):** Start the procedural hit reaction on the specified world position and radius, it will then look up which bones fall inside of the radius and begin the animation.
Multiple hit reactions can happen at the same time, but beware of having too many happen on the same spot too quickly or else your head will start twisting backwards!

### Driven mode
Active pins every body to the animation, which is why nothing can move a ragdoll in it. Driven replaces that pin with a spring that has a maximum force, so anything stronger wins and drags the rest of the skeleton with it through the joints. Every body has a strength from 0 (limp) to 1 (holding the pose) scaling its budget, and grabs and blows ease it down.

The ragdoll doesn't move the renderer in this mode, your character controller does. Feed it **GetPushVelocity**, or turn on FollowRootPosition and the renderer chases them by itself.

- **GrabBody( body, worldPoint, carryMass ):** Take hold, returns a `RagdollGrab` you feed a target to every frame with MoveTo. `carryMass` is how much mass that hand can hold up, so their weight decides whether you can lift them or only steer a limb. Also GrabBone and GrabNearest
- **ApplyRecoil( body or position, impulse, ... ):** Punch them, a real impulse plus a strength dip so the blow travels instead of being shrugged off. NetworkRecoil does the same from a non owner
- **WeakenChain( bone, strength, falloff, depth, ... ):** Ease a bone limp and let it bleed along the skeleton, so an arm hangs off a held wrist instead of snapping straight. SetBodyStrength, WeakenBody and RestoreStrength for the simpler cases
- **GetPushVelocity() / GetTurnSpeed():** The gap between where the bodies ended up and where the animation wanted them, as a velocity and a yaw speed to hand your controller
- **IsOverwhelmed / Overwhelmed:** They've been dragged too far to keep pretending, a good moment to switch to Enabled
- **HaulFraction / IsBeingGrabbed:** How much of their weight is being carried, and whether anything has hold of them

Grabbing and recoil run on whoever owns the ragdoll, so take ownership before grabbing one you don't own, or use NetworkRecoil which doesn't need it.

#### Driven properties
- **RootDriveStrength / BodyDriveStrength:** Force budget for the pelvis and for everything else. BodyDriveStrength is the number a grab has to beat to hold a limb still
- **RootAngularStrength:** How hard the root twists itself back upright
- **DriveFrequency:** Spring frequency of the drive, higher tracks the animation more tightly
- **StrengthMultiplier:** Scales every body at once, drop it for stunned or exhausted
- **HaulLimpness / HaulMotorLimpness:** How much anchoring and articulation they give up while being hauled, which is what makes lifting someone possible at all
- **StruggleRate:** How fast they work themselves free of a grab, 0 to disable
- **MaxPoseError:** How far the root may drift before IsOverwhelmed trips

Driven reuses MotorFrequency and MotorDamping for its joint motors, scaled per joint by the child body's strength.

### Partial Ragdolling
You're able to partially ragdoll only certain bones or limbs so that they flop around, like breaking an arm or leg!

- **RagdollBone( rootBone, targetMode, includeChildren ):** Ragdolls a single bone and optionally all its children
- **UnragdollBone( rootBone, IncludeChildren ):** Unragdolls a single bone and optionally all its children

You're also able to set this in the editor through Advanced Properties, there's a Dictionary where you set the bone name and the target mode, this is set inside of OnStart so you could for example have a zombie NPC where in their prefab their spine is set to motor and they'll flop around.

### Extra properties
Shrimple Ragdolls come with extra properties on top of ModelPhysics that you can set

- **Gravity:** Set gravity to all bodies
- **GravityScale:** Set gravity scale to all bodies
- **LinearDamping:** Set linear damping to all bodies
- **AngularDamping:** Set angular damping to all bodies
- **MassOverride:** Correctly scale the mass of all bodies based on their ratio so that it matches this value
- **Surface:** Set the Surface on all colliders
- **ColliderFlags:** Set ColliderFlags on all colliders
- **MassCenter:** Calculates the current mass center by the average of the bodies mass center weighted by their mass

### Advanced properties
To enable these you'll have to right click the component and check the "Show Advanced Properties" box

- **MotorFrequency:** The frequency of the joint's motors
- **MotorDamping:** The damping of the joint's motors
- **ActiveLerpTime:** How fast the Active mode takes to reach final transform
- **PartialRagdollConfig:** A dictionary to manually set partial ragdolled bones in the editor

### Extra methods
- **Move( Transform ):** Move the ragdoll without affecting its velocity or simulating collisions
- **ApplyVelocity( Vector3 ):** Apply a velocity to the ragdoll as a whole rather than on every body individually
- **ApplyAngularVelocity( Vector3 ):** Correctly apply an angular velocity to the ragdoll, spinning it around the mass center
- **ApplyForce( Vector3 ):** Apply a force to the ragdoll on all bodies
- **ApplyTorque( Vector3 ):** Apply a torque to the ragdoll on all bodies
- **ApplyImpulse( Vector3 ):** Apply an impulse to the ragdoll on all bodies
- **GetModelMass():** Returns the model's default mass through its physics model data
- **SleepPhysics():** Put all bodies to sleep
- **WakePhysics():** Wake up all bodies
- **GetBodyByX(x):** Various methods to retrieve a ragdoll body, by bone name, bone index, bone or physics body
- **GetBodyIndex( boneIndex ):** Index into Bodies for a bone, or -1 if that bone has no body
- **GetDescendantBones(x):** Returns the bone and all of its descendants in the skeleton
- **MultiplyJointLimits( float ):** Multiply the limits of each joint so they're able to move more
﻿
