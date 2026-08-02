# The Best Monkey Game — VR Locomotion Foundation

A clean Unity/Meta Quest foundation built around Another Axiom's official open-source GorillaLocomotion implementation. This repository intentionally contains only the VR rig, arm-driven locomotion, Quest/OpenXR configuration, a reusable player prefab, and a locomotion test environment. It does not contain multiplayer or a full game loop.

## Project versions

- Unity: **6000.0.34f1 (Unity 6 LTS)**
- Input System: `com.unity.inputsystem` **1.11.2**
- XR Core Utilities: `com.unity.xr.core-utils` **2.4.0**
- XR Interaction Toolkit: `com.unity.xr.interaction.toolkit` **3.0.8**
- XR Plug-in Management: `com.unity.xr.management` **4.5.0**
- OpenXR Plugin: `com.unity.xr.openxr` **1.14.3**

The project targets Android API 29 or newer, ARM64, Vulkan with OpenGL ES 3 fallback, and the package ID `com.secondsebastiantablet.thebestmonkeygame`. Android XR Plug-in Management is configured to initialize OpenXR, with Meta Quest support and Oculus Touch, Meta Quest Touch Pro, and Meta Quest Touch Plus profiles enabled.

## Open the project

1. Install Unity **6000.0.34f1** through Unity Hub, including Android Build Support, Android SDK & NDK Tools, and OpenJDK.
2. In Unity Hub, choose **Add > Add project from disk** and select this repository's root directory.
3. Allow Package Manager and the asset database to finish importing.
4. Open `Assets/_Game/Scenes/LocomotionTest.unity`.

Main assets:

- Test scene: `Assets/_Game/Scenes/LocomotionTest.unity`
- Reusable player: `Assets/_Game/Prefabs/VRPlayer.prefab`
- Game scripts: `Assets/_Game/Scripts`
- Official locomotion source: `Assets/ThirdParty/GorillaLocomotion`
- Upstream license: `Assets/ThirdParty/Licenses/Another-Axiom-GorillaLocomotion-MIT.txt`

## GorillaLocomotion integration

`Player.cs` and `Surface.cs` were copied from the official [Another Axiom GorillaLocomotion repository](https://github.com/Another-Axiom/GorillaLocomotion) at upstream commit `bc42e959cf3e69178f9147d89bd3ffeab1c432c4`. The locomotion algorithm was not recreated or approximated. The upstream source is kept under `Assets/ThirdParty/GorillaLocomotion`, and its MIT license and copyright notice are preserved under `Assets/ThirdParty/Licenses`.

The `VRPlayer` prefab supplies the official `Player` component with:

- a dynamic, gravity-enabled Rigidbody with frozen rotation;
- tracked headset and left/right controller transforms driven by OpenXR device poses;
- a head sphere collider and height-adjusting body capsule;
- visible blue and red hand followers with trigger collision objects;
- a locomotion-only layer mask used by the official sphere-cast collision algorithm;
- tuned jump and velocity values suitable for initial testing;
- fall reset support.

Every floor, wall, ramp, platform, and obstacle used for locomotion is on the `Locomotion` layer, tagged `LocomotionSurface`, has a collider and shared physics material, and includes the official `Surface` component. No thumbstick, teleport, continuous-move, or snap-turn provider is attached to the rig.

## Test in the Unity Editor

For representative tracking, connect a Quest with a USB Link cable or Air Link, make the Meta Quest Link desktop software the active OpenXR runtime, open the test scene, and press Play. Put on the headset after Play Mode starts. The tracked head and controller transforms should update immediately.

Without an active XR device, the rig retains fallback editor poses so the scene can enter Play Mode and its physics can be checked, but controller-driven locomotion cannot be meaningfully evaluated. The project includes a batch-mode verification command used during setup:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath '<repository-path>' `
  -executeMethod TheBestMonkeyGame.Editor.ProjectVerification.Run `
  -logFile 'unity-verify.log'
```

## Controls

- Move your real headset and controllers to move the tracked rig.
- Press a hand against the floor and push backward/downward to propel the body.
- Press and sweep against walls to redirect movement.
- Hold contact and pull against walls, platforms, or cubes to climb.
- Push sharply away from a surface to jump; launch speed is capped by the official locomotion settings.
- Fall below the arena to return to `SpawnPoint`.
- Thumbsticks do not move or turn the player. Teleport and smooth locomotion are disabled by design.

## Enable Quest developer mode

1. Sign in to the [Meta Horizon developer dashboard](https://developers.meta.com/horizon/) and create or join a developer organization. Complete any account verification Meta requests.
2. Pair the headset with the Meta Horizon mobile app.
3. In the mobile app, select the headset, open **Headset settings > Developer mode**, and enable it.
4. Restart the headset if the developer options or USB prompt do not appear.

Meta occasionally changes the app labels; use Meta's current [developer documentation](https://developers.meta.com/horizon/documentation/) if the menu wording differs.

## Connect, build, and install an APK

1. Connect the unlocked Quest to the computer with a data-capable USB cable.
2. In the headset, accept **Allow USB debugging** and optionally select **Always allow from this computer**. USB file access is not required.
3. Confirm the connection from the Android SDK platform-tools directory:

   ```powershell
   adb devices
   ```

   The headset serial should show as `device`, not `unauthorized`.

4. In Unity, choose **File > Build Profiles**, select or add **Android**, and switch to it.
5. Confirm `LocomotionTest` is the enabled scene. The checked-in build settings already include it.
6. Leave **Build App Bundle** disabled to produce an APK, then choose **Build** and save it under a local `Build` directory. To deploy immediately, choose **Build And Run** with the Quest selected.
7. To install an already-built APK manually:

   ```powershell
   adb install -r .\Build\TheBestMonkeyGame.apk
   ```

8. Launch it from **App Library > Unknown Sources** in the headset.

Unity's current Android build overview is available in the [Unity 6 manual](https://docs.unity3d.com/6000.0/Documentation/Manual/android-BuildProcess.html), and its current Quest workflow is documented under [Develop for Meta Quest](https://docs.unity3d.com/6000.0/Documentation/Manual/xr-meta-quest-develop.html).

## Test environment

The scene contains a 24-by-24-meter floor, four enclosing walls, two ramps, low and high platforms, a bridge, three dedicated climb walls, stepped cubes, a balance beam, cubes at several heights, lighting, a procedural skybox, a spawn point, and a large fall-reset trigger.

## Known limitations and next steps

- Quest hardware feel and performance still require testing on a physical headset; the initial values are sensible defaults, not final comfort tuning.
- The hand visuals are simple colored placeholders. There are no animated hands, avatar body, grabbing interactions, haptics, audio, menus, or accessibility options yet.
- Editor-only testing without a connected XR runtime uses static fallback poses.
- The player has no artificial turning. Room-scale body turning is tracked, and an intentional turning solution can be added later if desired without adding thumbstick translation.
- The official source uses the older `Rigidbody.velocity` API. Unity 6 reports deprecation warnings but compiles it successfully; it is kept unchanged to preserve the official implementation.
- A production release needs a custom keystore, store metadata, comfort testing, performance profiling on each supported Quest model, and final package/version policy review.
- No multiplayer has been added.

## Repository hygiene

The Unity-generated `Library`, `Temp`, `Logs`, `obj`, `Build`, `UserSettings`, IDE files, local APKs, and local verification logs are ignored. Commit all `.meta` files alongside their assets.

## License

The GorillaLocomotion portion is licensed under the MIT License, copyright © 2021 Another-Axiom. See `Assets/ThirdParty/Licenses/Another-Axiom-GorillaLocomotion-MIT.txt`. Original project code in this repository has not yet been assigned a separate license.
