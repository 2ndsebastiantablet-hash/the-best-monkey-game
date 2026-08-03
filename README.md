# The Best Monkey Game — Unity VR prototype

A Unity 6/Meta Quest prototype built around Another Axiom's official open-source GorillaLocomotion. The current revision corrects the imported map's world scale, keeps the player on a floor-level OpenXR origin, and restores the original visible sphere hands.

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

`XRFloorTrackingOrigin` requests `TrackingOriginModeFlags.Floor` from every running XR input subsystem. `XR Origin` remains at local position zero and `PlayerFloorOffset` defaults to zero; its ±10 cm setting is only for small hardware/user calibration. There is no artificial standing-height camera offset, controller offset, or scale compensation.

The scene's `PlayerSpawn` is exactly on a raycast-selected upward-facing map surface at approximately **(0, 0.144, 0)**. The whole `VRPlayer` root starts there at scale **(1, 1, 1)**. The editor-only camera/controller fallback poses are close to the floor so non-XR Play Mode cannot recreate the earlier doubled-height result.

The body capsule follows the tracked head and keeps 1.5 cm of ground clearance. Spawn and respawn move the whole player root, clear rigidbody motion, wait for fresh tracked poses, synchronize transforms, and reinitialize GorillaLocomotion history. This preserves real headset height and hand reach while preventing stale tracking history from launching the player after a reset.

## Player hierarchy and hands

The temporary gorilla body, extracted visual-hand meshes, `Visuals` hierarchy, and `GorillaVisualRig` component/script were removed. The supplied source ZIP/STL, attribution, reference image, and license remain archived for possible future use, but there is no player model instantiated in the prefab or scene.

The original left and right sphere followers are visible again and are the only hand representations. Each hand has one enabled primitive `MeshRenderer` and one trigger `SphereCollider`, has no visual child object or duplicate collider, and remains the authoritative GorillaLocomotion hand transform. Left and right retain their distinct original materials.

The reusable hierarchy is intentionally compact: `VRPlayer`, `XR Origin`, `Main Camera`, `Head Collider`, two controller targets, `Body Collider`, and `GorillaLocomotion` with the two sphere hands. The prefab validation rejects duplicate cameras, missing scripts, leftover model objects, non-unit rig scale, and hand children.

## Controls

- Move the real headset and controllers to move the tracked rig.
- Press a hand against a floor or wall and push/pull to propel or climb.
- Push sharply away from a surface to jump; launch speed remains capped by GorillaLocomotion.
- Falling below the map returns the full rig to `PlayerSpawn`.
- Thumbstick movement, teleport, and artificial turning remain disabled.

## Validation and Android build

The validation checks the clean floor-origin prefab, visible authoritative sphere hands, absence of the temporary model, unit player/XR scales, map scale and architectural measurements, four map colliders/surfaces, exact floor spawn, build-scene order, and Android OpenXR configuration. It then runs 180 Play Mode frames while collecting errors:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath '<repository-path>' `
  -executeMethod TheBestMonkeyGame.Editor.ProjectVerification.Run `
  -logFile '<repository-path>\Build\ScalePlayMode.log'
```

The Android smoke build command is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath '<repository-path>' `
  -executeMethod TheBestMonkeyGame.Editor.RevisionBootstrap.BuildAndroidSmokeTest `
  -logFile '<repository-path>\Build\ScaleAndroidBuild.log'
```

It produces `Build/TheBestMonkeyGame.apk`. The local `Build` directory and APK are intentionally ignored by Git.

## Quest install

1. Enable developer mode for the headset in the Meta Horizon mobile app and restart if required.
2. Connect an unlocked headset with a data-capable USB cable and accept **Allow USB debugging**.
3. Confirm `adb devices` shows the headset as `device`, then run `adb install -r .\Build\TheBestMonkeyGame.apk`.
4. Launch the app from **App Library > Unknown Sources**.

## Known limitations

- Physical room-floor alignment, doorway feel, and collision comfort still require an on-headset test. If a headset-specific adjustment is needed, tune only the centimeter-scale `PlayerFloorOffset`; do not restore a standing-height camera offset or rescale the player.
- The archived gorilla mesh is intentionally not displayed. A future avatar should be a properly rigged model whose visuals follow the existing authoritative head and sphere-hand transforms without adding locomotion colliders.
- Map collision cost, lighting, texture compression, and the central spawn should still be profiled on each supported Quest model.
- There is no multiplayer, grabbing, haptics, menu, final audio, comfort turning, or production keystore yet.

## Licensing

GorillaLocomotion is from Another Axiom's official repository at commit `bc42e959cf3e69178f9147d89bd3ffeab1c432c4` and is licensed under MIT; see `Assets/ThirdParty/Licenses/Another-Axiom-GorillaLocomotion-MIT.txt`. The archived model's supplied public-domain dedication and attribution text are preserved with its source files.

The GLB's embedded metadata identifies **Giggle Fart's Map** by **Zman** as CC BY 4.0. Its source, author, license URL, and modification note are recorded in `Assets/ThirdParty/Map/ATTRIBUTION.md`. Original project code has not yet been assigned a separate license.
