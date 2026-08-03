# The Best Monkey Game - Unity VR production foundation

A Unity 6 / Meta Quest horror game built around Another Axiom's official open-source GorillaLocomotion. The current playable revision focuses on floor alignment, reliable locomotion recovery, a larger chase environment, and two animated NavMesh monsters. Multiplayer is intentionally not included yet.

## Project versions and targets

- Unity 6000.0.34f1
- glTFast 6.14.0
- AI Navigation 2.0.9
- Input System 1.11.2
- XR Core Utilities 2.4.0
- XR Interaction Toolkit 3.0.8
- XR Plug-in Management 4.5.0
- OpenXR Plugin 1.14.3
- Android API 29+, ARM64, IL2CPP, Vulkan with OpenGL ES 3 fallback
- Package ID `com.secondsebastiantablet.thebestmonkeygame`

The Android XR configuration uses OpenXR with Meta Quest support and Oculus Touch, Touch Pro, and Touch Plus controller profiles.

## Main assets

- Playable scene: `Assets/_Game/Scenes/MainMap.unity`
- Locomotion test scene: `Assets/_Game/Scenes/LocomotionTest.unity`
- Player prefab: `Assets/_Game/Prefabs/VRPlayer.prefab`
- Map prefab: `Assets/_Game/Prefabs/Environment/GiggleFartsMap.prefab`
- Tiptoe prefab: `Assets/_Game/Prefabs/Monsters/Tiptoe.prefab`
- Statue prefab: `Assets/_Game/Prefabs/Monsters/Statue.prefab`
- Baked navigation: `Assets/_Game/Navigation/MainMapNavMesh.asset`
- Official locomotion source: `Assets/ThirdParty/GorillaLocomotion`

`MainMap.unity` is the first enabled build scene.

## Map scale and collision

The complete `GiggleFartsMap` root is uniformly scaled to **3.50** on X, Y, and Z. The previous revision used 2.85. The player, XR rig, camera, tracked controllers, hand followers, and monsters remain unscaled.

Measured architecture after scaling:

- Rendered bounds: **100.42 x 5.17 x 105.00 m**
- Representative doorway: **2.52 m**
- Representative corridor ceiling: **3.23 m**
- Representative low wall: **1.26 m**
- Spawn-room horizontal clearance checked at head height: **13.10 x 8.03 m**

The map rebuild regenerates four static non-convex MeshColliders from the substantial source meshes. Each uses the `Locomotion` layer, `LocomotionSurface` tag, and GorillaLocomotion `Surface` component. The NavMesh is rebuilt from these physics colliders after every scale revision. Quest shadow distance is **145.38 m**.

## Floor alignment and tracking

The player root and `PlayerSpawn` are placed directly on the selected map floor at approximately **(0, 0.177, 0)**. The `XR Origin` remains at local zero and requests `TrackingOriginModeFlags.Floor`. A dedicated `Tracking Space Offset` child owns the one adjustable correction:

- Setting: `playerFloorOffset`
- Default: **-1.45 m**
- Inspector range: **-2.0 to +1.0 m**
- Hand calibration target: **2 cm above the floor**

The Main Camera has a serialized local position of zero and remains headset-driven. Controller targets remain OpenXR-driven. No rig, camera, controller, player, or arm-length scaling is used. Holding both controllers' primary buttons for two seconds recalibrates the offset from the lowest tracked hand and then rebuilds locomotion history.

`PlayerFloorDebugGizmo` displays the spawn floor point, XR Origin, calibrated pose-space origin, body-collider bottom, and maximum hand reach zone in the editor. `GorillaLocomotionDiagnostics` is disabled by default and can expose hand contacts, rigidbody velocity, calculated average velocity, movement-disable state, tracking-origin mode, and root height above the floor without generating log spam.

Headless/editor Play Mode uses a code-only fallback pose when no XR device exists. It is never serialized onto the camera and is bypassed whenever a real OpenXR device is available.

## GorillaLocomotion and respawn

The official locomotion references and tuning match the original working setup:

- Left/right controller targets and followers are correctly paired
- Head and body colliders are assigned
- Locomotion layer mask: `Locomotion`
- Velocity history: 10 samples
- Maximum arm length: 1.5 m
- Velocity limit: 0.8 m/s
- Maximum jump speed: 6.5 m/s
- Jump multiplier: 1.15
- Minimum sphere-cast distance: 0.055 m

The reset path now snaps hand followers to fresh tracked poses, clears contact and velocity history, zeros linear/angular velocity only during reset, and explicitly restores `disableMovement = false`. It uses two normal frame yields rather than `WaitForEndOfFrame`, which could leave headless and interrupted death resets permanently locked.

## Monster spawning

Eight named spawn points are distributed across North, South, East, and West map wings. Every point is:

- At least 35 m from PlayerSpawn
- Outside the initial player line of sight
- On the current baked NavMesh
- Clear of the map collision capsule
- At least 12 m from other selected spawn points

At startup, `MonsterSpawnCoordinator` selects different hidden regions and prevents overlap. Current deterministic starting regions are:

- Tiptoe: **South Wing**, approximately **(19.28, 0.18, -50.50)**
- Statue: **North Wing**, approximately **(-48.21, 0.18, 50.04)**

Both monsters remain dormant for **7 seconds**. A killer reset rejects the prior point, the other monster's location, visible points, and points below its minimum distance.

## Tiptoe tuning

Tiptoe retains the supplied 44-joint animated skin, loops `GorillaTag_IK_RigV3.001Action`, and has a normalized visual height of **1.700 m**. Root motion is disabled.

- Roam speed: **6.5 m/s**
- Chase speed: **14.5 m/s**
- Sight: **42 m**, 120 degrees, 0.15 s confirmation, wall-obstructed
- Lost-sight grace: **2.25 s**
- Search duration: **9 s**
- Search radius: **12 m** around the last-known position
- Escape distance: **30 m**
- Minimum spawn/reset distance: **30 m**
- Navigation repath interval: **0.28 s**

Death reset clears last-known position, sight time, search timing, chase state, navigation, animation state, and kill-trigger state before returning Tiptoe to Roaming.

## Statue tuning

Statue retains the supplied 28-joint animated skin, uses the `Scene` clip, and has a normalized visual height of **1.900 m**. Root motion is disabled and animation freezes while watched.

- Awareness radius: **48 m**
- Direct-sight range: **22 m**, 100-degree field
- Watched cone: **25 degrees**, with 30-degree release hysteresis
- Teleport intervals: **1.5, 1.1, 0.8, 0.5 s**
- Teleport distances: **18, 13, 9, 6, 3 m**
- Awareness escape confirmation: **2 s**
- Minimum spawn/reset distance: **35 m**

Teleport targets must be on the NavMesh, outside the central view, and clear of walls and floor overlap. Reset clears watched state, aggro memory, teleport stage/timers, navigation, animation state, and kill-trigger state before returning Statue to Roaming.

Monster 3D audio uses linear rolloff to **55 m**. The synthesized placeholder asset at `Assets/_Game/Audio/Monsters/placeholder_monster_noise.wav` should eventually be replaced with final monster-specific audio.

## Death flow and experimental jumpscares

Jumpscares are **disabled in the playable game**. `MainMap.unity`, the production player prefab, and the production monster prefabs contain no jumpscare room or monster-jumpscare component.

Preserved experimental work:

- Scene: `Assets/_Game/Scenes/Experimental/JumpscareRoom.unity`
- Room and monster prefabs: `Assets/_Game/Experimental/Jumpscares/Prefabs`
- Scripts: `Assets/_Game/Experimental/Jumpscares/Scripts`
- Black-room material: `Assets/_Game/Experimental/Jumpscares/Materials`

The experimental scene is not in build settings and its room controller is disabled.

Current playable death flow:

1. The first monster contact disarms duplicate kill triggers.
2. A short stable black fade begins without moving or rotating the tracked camera.
3. The full player root returns to floor-level `PlayerSpawn`.
4. Linear/angular velocity and GorillaLocomotion pose/history state are reset.
5. Visible sphere hands and locomotion are explicitly restored.
6. The killer moves to a hidden distant spawn and clears its AI state.
7. The player receives three seconds of spawn protection.
8. The fade clears.

## Validation and build

`ProjectVerification` checks the player hierarchy, floor point, unit rig scales, visible hands, GorillaLocomotion references, map measurements, colliders, NavMesh, eight hidden spawn points, separate monster regions, seven-second grace, animated monster setup, absence of playable jumpscares, experimental asset preservation, build-scene order, and Android OpenXR configuration.

The 720-frame Play Mode test also activates both agents, confirms each is on the NavMesh, asserts movement is unlocked, invokes a Tiptoe kill, and verifies normal respawn, visible hands, spawn protection, distant killer reset, and locomotion recovery. Red Console errors fail the run.

Android smoke builds use ARM64 and IL2CPP and write `Build/TheBestMonkeyGame.apk`. `Build`, APKs, Library, Temp, Logs, obj, and UserSettings are intentionally excluded from Git.

## Next physical Quest test

Physical headset validation is still required. Verify:

- Standing headset height and natural floor reach with the **-1.45 m** default
- Both hand spheres follow the correct controller and can press floor/walls
- No idle drift, startup launch, missing hand, or body/head misalignment
- Floor pushing, wall pushing, climbing, and jumping
- Both-button recalibration and the 2 cm hand target
- Movement and visible hands after monster death and fall reset
- No black-screen hang, camera lock, or jumpscare transition
- Door/corridor comfort at high locomotion speed
- Tiptoe cornering, overshoot, chase speed, and search behavior
- Statue awareness, gaze freeze, teleport clearance, and reset distance
- Lighting, shadows, collision cost, NavMesh behavior, and audio rolloff on the target Quest model

Do not treat automated Play Mode or Android build success as physical headset validation.

## Licensing

GorillaLocomotion is from Another Axiom's official repository at commit `bc42e959cf3e69178f9147d89bd3ffeab1c432c4` and is licensed under MIT; see `Assets/ThirdParty/Licenses/Another-Axiom-GorillaLocomotion-MIT.txt`.

The GLB metadata identifies Giggle Fart's Map and Statue by Zman, and Tiptoe by GT/Cooldude16, as CC BY 4.0. Sources and modification notes are recorded beside each asset under `Assets/ThirdParty`.
