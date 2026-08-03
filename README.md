# The Best Monkey Game — Unity VR prototype

A Unity 6/Meta Quest horror prototype built around Another Axiom's official open-source GorillaLocomotion. The current revision adds configurable Quest floor-height calibration, animated NavMesh monsters, and a shared VR-safe jumpscare/respawn sequence.

## Project versions and targets

- Unity **6000.0.34f1 (Unity 6 LTS)**
- glTFast `com.unity.cloud.gltfast` **6.14.0**
- AI Navigation `com.unity.ai.navigation` **2.0.9**
- Input System **1.11.2**
- XR Core Utilities **2.4.0**
- XR Interaction Toolkit **3.0.8**
- XR Plug-in Management **4.5.0**
- OpenXR Plugin **1.14.3**
- Android API 29+, ARM64, IL2CPP, Vulkan with OpenGL ES 3 fallback
- Package ID `com.secondsebastiantablet.thebestmonkeygame`

Android XR Plug-in Management initializes OpenXR with Meta Quest support and Oculus Touch, Touch Pro, and Touch Plus interaction profiles.

## Open and run

1. Install Unity **6000.0.34f1** through Unity Hub with Android Build Support, Android SDK & NDK Tools, and OpenJDK.
2. Add this repository root as a Unity project and let Package Manager/importing finish.
3. Open `Assets/_Game/Scenes/MainMap.unity`, which is also the first enabled build scene.
4. Connect Quest Link/Air Link with Meta Quest Link selected as the active OpenXR runtime, then enter Play Mode.

Important assets:

- Main scene: `Assets/_Game/Scenes/MainMap.unity`
- Retained test scene: `Assets/_Game/Scenes/LocomotionTest.unity`
- Reusable player: `Assets/_Game/Prefabs/VRPlayer.prefab`
- Map prefab: `Assets/_Game/Prefabs/Environment/GiggleFartsMap.prefab`
- Map source: `Assets/ThirdParty/Map/giggle_farts_map.glb`
- Tiptoe prefab/source: `Assets/_Game/Prefabs/Monsters/Tiptoe.prefab`, `Assets/ThirdParty/Monsters/Tiptoe/tiptoe.glb`
- Statue prefab/source: `Assets/_Game/Prefabs/Monsters/Statue.prefab`, `Assets/ThirdParty/Monsters/Statue/statue.glb`
- Baked navigation: `Assets/_Game/Navigation/MainMapNavMesh.asset`
- Archived model source and license: `Assets/ThirdParty/GorillaModel`
- Gameplay scripts: `Assets/_Game/Scripts`
- Official locomotion source: `Assets/ThirdParty/GorillaLocomotion`

## Corrected map scale

The GLB was originally normalized to a 30 m footprint, but its vertical architecture then measured only about 0.72 m at a representative doorway. This was a map-scale mismatch: the XR player and tracked poses were already using Unity's normal one-unit-per-meter scale.

The complete `GiggleFartsMap` root is now uniformly scaled by **2.85** on X, Y, and Z. Nothing in `VRPlayer`, the XR origin, camera, controller targets, body collider, hand followers, or physics reach is scaled. The corrected rendered bounds are approximately **81.77 × 4.21 × 85.50 m**. Representative architecture now measures:

- Doorway: **2.05 m**
- Low wall: **1.03 m**
- Corridor ceiling: **2.63 m**

The same four substantial static, non-convex `MeshCollider` meshes are regenerated from the map geometry and inherit that uniform root scale. Two decorative eight-triangle card meshes intentionally remain non-collidable. Every collidable mesh is on the `Locomotion` layer, tagged `LocomotionSurface`, marked static, and has the official `Surface` component.

## Floor and tracking correction

`XRFloorTrackingOrigin` requests `TrackingOriginModeFlags.Floor` from every running XR input subsystem. `VRFloorHeightCalibration` then applies a configurable **−0.75 m** correction to the `XR Origin` tracking-space parent. Its practical Inspector range is −2.0 to +1.0 m. The tracked camera and controllers remain children of that space and continue receiving unmodified OpenXR position and rotation poses; the player, XR origin, camera, and controllers all remain at scale **(1, 1, 1)**.

For development calibration, place both tracked hands at the desired comfortable near-floor contact height and hold both controller primary buttons for two seconds. The component aligns the lowest hand sphere to 12 cm above the player-root floor, applies a four-second cooldown, clears linear/angular velocity, and reinitializes GorillaLocomotion pose history. This is a development aid, not a substitute for another physical Quest test.

The scene's `PlayerSpawn` is exactly on a raycast-selected upward-facing map surface at approximately **(0, 0.144, 0)**. The whole `VRPlayer` root starts there at scale **(1, 1, 1)**. The editor-only camera/controller fallback poses are close to the floor so non-XR Play Mode cannot recreate the earlier doubled-height result.

The body capsule follows the calibrated tracked head and keeps 1.5 cm of ground clearance. Spawn, calibration, death recovery, and fall respawn clear linear/angular velocity, wait for fresh tracked poses, synchronize transforms, and reinitialize GorillaLocomotion history. Horizontal spawn placement and the normal 1.5 m arm-length limit are unchanged.

## Player hierarchy and hands

The temporary gorilla body, extracted visual-hand meshes, `Visuals` hierarchy, and `GorillaVisualRig` component/script were removed. The supplied source ZIP/STL, attribution, reference image, and license remain archived for possible future use, but there is no player model instantiated in the prefab or scene.

The original left and right sphere followers are visible again and are the only hand representations. Each hand has one enabled primitive `MeshRenderer` and one trigger `SphereCollider`, has no visual child object or duplicate collider, and remains the authoritative GorillaLocomotion hand transform. Left and right retain their distinct original materials.

The reusable hierarchy is intentionally compact: `VRPlayer`, `XR Origin`, `Main Camera`, `Head Collider`, two controller targets, `Body Collider`, and `GorillaLocomotion` with the two sphere hands. The prefab validation rejects duplicate cameras, missing scripts, leftover model objects, non-unit rig scale, and hand children.

## Monster framework

The shared finite-state framework is split across navigation, perception, animation, audio, spawning, kill-trigger, and jumpscare components under `Assets/_Game/Scripts/Monsters`. Expensive sight checks are staggered, NavMesh paths are throttled, patrol destinations are reachable and non-repeating, and stuck agents repath instead of recalculating every rendered frame.

The current AI Navigation surface uses the four map physics meshes on the `Locomotion` layer, a 0.12 m voxel size, 128-voxel tiles, and a baked 28 KB `NavMeshData` asset. Eight named spawn points are distributed across the reachable island and are used for hidden relocation. Monster agents use a 0.24 m radius and 1.55 m height for the map's corridors.

### Tiptoe

Tiptoe preserves a 44-joint skin and the supplied `GorillaTag_IK_RigV3.001Action` clip. Its uniformly normalized visual height is **1.700 m**. The single imported clip loops at 1.15× while roaming and blends toward 2.5× during chase; root motion is disabled so only the NavMeshAgent moves the gameplay root.

- Roam speed: **5.0 m/s**
- Chase speed: **11.5 m/s**
- Sight: **30 m**, **120°**, with 0.15 s confirmation and wall obstruction
- Lost sight: follow the last-known position for a 2 s grace period
- Search: sample nearby reachable points for 6.5 s
- Escape: line of sight must remain broken and the player must be at least 17.5 m away

### Statue

Statue preserves a 28-joint skin and the supplied `Scene` clip. Its uniformly normalized visual height is **1.900 m**. The clip is used for roaming and is frozen at zero playback speed for the static watched pose; root motion is disabled.

- General awareness radius: **35 m**
- Direct-sight aggro: **15 m**, 100° forward field, unobstructed line of sight
- Strict player gaze: **25°** central cone to become watched and 30° to stop being watched
- Teleport intervals: **1.5, 1.1, 0.8, 0.5 s**
- Teleport distances: **12, 8, 5, 3, 1.5 m**
- Placement: sampled on the NavMesh, outside the central view, clear of walls/floor overlap, and varied around the player
- Escape: remain outside the full awareness radius for 2 s, then relocate at least 20 m away to a hidden valid spawn

Both GLBs keep mipmaps, non-readable textures, anisotropic level 1, and offscreen skin updates disabled. Tiptoe textures range from 64² to 256²; Statue textures are at most 512², so no oversized Quest textures are introduced.

## Shared jumpscare room

`MonsterSystems/JumpscareRoom` is isolated 500 m below and 200 m outside the map. A kill fades to black, disables GorillaLocomotion and hand pushes, zeros the rigidbody, temporarily pauses tracked-pose components, aligns the full rig during black, and shows a collider-free copy of the correct killer 1.25 m ahead. The monster shakes procedurally, advances slightly, and uses a labeled synthesized-noise placeholder scream for approximately two seconds.

The sequence fades out before returning the complete player root to `PlayerSpawn`, restores tracked poses and visible sphere hands, resets locomotion history, relocates the killer to a hidden spawn, and grants three seconds of spawn protection. Final monster-specific audio should replace `Assets/_Game/Audio/Monsters/placeholder_monster_noise.wav`.

## Controls

- Move the real headset and controllers to move the tracked rig.
- Press a hand against a floor or wall and push/pull to propel or climb.
- Push sharply away from a surface to jump; launch speed remains capped by GorillaLocomotion.
- Falling below the map returns the full rig to `PlayerSpawn`.
- Hold both controller primary buttons for two seconds to perform development floor recalibration.
- Thumbstick movement, teleport, and artificial turning remain disabled.

## Validation and Android build

The validation checks floor calibration placement, unit player/XR/camera scales, visible sphere hands, map measurements/colliders, the baked NavMesh, eight spawn points, both normalized animated monsters, dedicated kill triggers, the jumpscare room, build-scene order, and Android OpenXR configuration. It runs 720 Play Mode frames, forces the startup grace to complete for validation, confirms both agents activate on the NavMesh, and collects all errors:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath '<repository-path>' `
  -executeMethod TheBestMonkeyGame.Editor.ProjectVerification.Run `
  -logFile '<repository-path>\Build\MonsterPlayMode.log'
```

The Android smoke build command is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath '<repository-path>' `
  -executeMethod TheBestMonkeyGame.Editor.RevisionBootstrap.BuildAndroidSmokeTest `
  -logFile '<repository-path>\Build\MonsterAndroidBuild.log'
```

It produces `Build/TheBestMonkeyGame.apk`. The local `Build` directory and APK are intentionally ignored by Git.

## Quest install

1. Enable developer mode for the headset in the Meta Horizon mobile app and restart if required.
2. Connect an unlocked headset with a data-capable USB cable and accept **Allow USB debugging**.
3. Confirm `adb devices` shows the headset as `device`, then run `adb install -r .\Build\TheBestMonkeyGame.apk`.
4. Launch the app from **App Library > Unknown Sources**.

## Known limitations

- The **−0.75 m** floor correction is an informed starting value, not a headset-verified result. The next physical test should tune `VRFloorHeightCalibration.verticalOffset` in small increments and confirm normal standing hands need only a slight lowering to touch the floor.
- Tiptoe chase speed, perception, search escape distance, Statue gaze cone/teleport cadence, kill-trigger reach, jumpscare comfort, and all audio volumes require physical Quest playtesting.
- The archived gorilla mesh is intentionally not displayed. A future avatar should be a properly rigged model whose visuals follow the existing authoritative head and sphere-hand transforms without adding locomotion colliders.
- Map collision cost, lighting, texture compression, and the central spawn should still be profiled on each supported Quest model.
- There is no multiplayer, grabbing, haptics, menu, final audio, comfort turning, or production keystore yet.

## Licensing

GorillaLocomotion is from Another Axiom's official repository at commit `bc42e959cf3e69178f9147d89bd3ffeab1c432c4` and is licensed under MIT; see `Assets/ThirdParty/Licenses/Another-Axiom-GorillaLocomotion-MIT.txt`. The archived model's supplied public-domain dedication and attribution text are preserved with its source files.

The GLB's embedded metadata identifies **Giggle Fart's Map** and **Statue** by **Zman**, and **Tiptoe** by **GT/Cooldude16**, as CC BY 4.0. Sources and modification notes are recorded beside each asset under `Assets/ThirdParty`. Original project code has not yet been assigned a separate license.
