# The Best Monkey Game — Unity VR prototype

A Unity 6/Meta Quest prototype built around Another Axiom's official open-source GorillaLocomotion. The current revision adds floor-correct OpenXR tracking, a temporary gorilla avatar, and the imported Giggle Farts map while retaining the original locomotion test scene.

## Project versions and targets

- Unity **6000.0.34f1 (Unity 6 LTS)**
- glTFast `com.unity.cloud.gltfast` **6.14.0**
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
- Temporary model source and generated meshes: `Assets/ThirdParty/GorillaModel`
- Gameplay scripts: `Assets/_Game/Scripts`
- Official locomotion source: `Assets/ThirdParty/GorillaLocomotion`

## Floor and tracking correction

The old rig authored the editor fallback headset at `Y = 1.65` and controllers at `Y = 1.2`, but it did not explicitly request a floor tracking origin. On a device-origin XR runtime, tracked standing height could therefore be combined with a virtual standing-height offset, leaving the camera and hands too high to reach the floor.

`XRFloorTrackingOrigin` now requests `TrackingOriginModeFlags.Floor` from each running XR input subsystem. `TrackingSpace` stays at zero and `PlayerFloorOffset` defaults to zero; its ±10 cm range is only for small hardware/user calibration. No artificial 1.65 m camera offset remains. The editor-only fallback pose is near the floor so non-XR Play Mode does not imitate the old bug.

The body capsule follows the tracked head but begins 1.5 cm above the player-root floor. Spawn and respawn move the whole player root, zero rigidbody motion, wait for fresh OpenXR poses, synchronize transforms, and then reinitialize GorillaLocomotion history. This prevents a stale hand/head history from launching the player after a reset.

## Temporary gorilla model and hands

The supplied archive contains one connected, unrigged binary STL; it has no bones, armature, named hand objects, or separable skinned hand meshes. Its included README attributes the model to Thingiverse user CloverPatch170, and the supplied license is a Creative Commons Public Domain Dedication. Those original files are preserved under `Assets/ThirdParty/GorillaModel/License`.

The complete source model is displayed as a collider-free temporary body. It follows the tracked headset in X/Z and follows headset yaw only, so headset pitch and roll do not tilt the body. The two visible controller hands are meshes extracted from the actual low hand/finger regions of that STL and attached to the physics followers. They are source-derived static visual meshes, not animated/skinned bones.

The original left/right sphere followers remain the authoritative locomotion hands. Their `SphereCollider` components and transforms are enabled and unchanged for collision, but their primitive `MeshRenderer` components are disabled. The source-derived hand visuals are children of those spheres and have no colliders, so appearance cannot change reach or locomotion behavior.

## Imported map and collisions

glTFast imports the GLB hierarchy, material splits, textures, and meshes. The source was Z-up and included embedded Sketchfab/FBX unit transforms, so the generated map prefab applies the required axis correction, centers the arena, places its lowest rendered point on `Y = 0`, and normalizes the footprint to **28.69 × 30.00 m**. The playable height range is approximately **1.48 m**.

Collision uses four static, non-convex `MeshCollider` components—one for each substantial material-separated mesh, about 20,000 source triangles total. Two decorative eight-triangle card meshes intentionally have no collider. Every collidable mesh is on the `Locomotion` layer, tagged `LocomotionSurface`, marked static, and has the official `Surface` component. This avoids creating more than 1,600 tiny component colliders while retaining the map's playable geometry.

`PlayerSpawn` is at approximately `(0, 0.071, 0)`, two centimeters above a raycast-selected upward-facing map surface, and faces toward the map center. `FallResetArea` spans the imported bounds below the arena.

## Controls

- Move the real headset and controllers to move the tracked rig.
- Press a hand against a floor or wall and push/pull to propel or climb.
- Push sharply away from a surface to jump; launch speed remains capped by GorillaLocomotion.
- Falling below the map returns the full rig to `PlayerSpawn`.
- Thumbstick movement, teleport, and artificial turning remain disabled.

## Validation and Android build

The checked-in validation command verifies the floor-origin prefab, invisible authoritative hand renderers, collider-free visual hands/body, four map colliders and surfaces, spawn wiring, build-scene order, and Android OpenXR configuration. It then runs 180 Play Mode frames while collecting errors:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath '<repository-path>' `
  -executeMethod TheBestMonkeyGame.Editor.ProjectVerification.Run `
  -logFile '<repository-path>\Build\RevisionPlayMode.log'
```

The Android smoke build command is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath '<repository-path>' `
  -executeMethod TheBestMonkeyGame.Editor.RevisionBootstrap.BuildAndroidSmokeTest `
  -logFile '<repository-path>\Build\RevisionAndroidBuild.log'
```

It produces `Build/TheBestMonkeyGame.apk`. The local `Build` directory and APK are intentionally ignored by Git.

## Quest install

1. Enable developer mode for the headset in the Meta Horizon mobile app and restart if required.
2. Connect an unlocked headset with a data-capable USB cable and accept **Allow USB debugging**.
3. Confirm `adb devices` shows the headset as `device`, then run:

   ```powershell
   adb install -r .\Build\TheBestMonkeyGame.apk
   ```

4. Launch the app from **App Library > Unknown Sources**.

## Known limitations and tuning points

- Physical Quest hardware feel and actual room-floor alignment still require an on-headset test. If needed, tune only the centimeter-scale `PlayerFloorOffset`; do not restore a standing-height camera offset.
- The avatar is an unrigged temporary model. Hand visual rotation/scale, body scale/offset, first-person clipping, and eventual replacement with a rigged avatar need headset review.
- The map collider grouping is practical for this prototype, but collision cost, materials, lighting, texture compression, and the chosen central spawn should be profiled/tuned on each supported Quest model.
- OpenXR reports two optional build recommendations about newer pose and thumbstick control types; neither blocks the successful build or the current tracked-pose implementation.
- There is no multiplayer, grabbing, haptics, menu, final audio, comfort turning, or production keystore yet.

## Licensing

GorillaLocomotion is from Another Axiom's official repository at commit `bc42e959cf3e69178f9147d89bd3ffeab1c432c4` and is licensed under MIT; see `Assets/ThirdParty/Licenses/Another-Axiom-GorillaLocomotion-MIT.txt`. The temporary model's supplied public-domain dedication and attribution text are preserved with the asset.

The GLB's embedded metadata identifies **Giggle Fart's Map** by **Zman** as CC BY 4.0. Its source, author, license URL, and modification note are recorded in `Assets/ThirdParty/Map/ATTRIBUTION.md`. Original project code has not yet been assigned a separate license.
