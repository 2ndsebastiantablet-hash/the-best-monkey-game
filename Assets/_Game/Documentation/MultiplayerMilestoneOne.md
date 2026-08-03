# Multiplayer Milestone One

Status date: 2026-08-03

## Protected single-player baseline

- Last known working single-player commit: `654e2f59b0129c2b912553f8dd09ac59f649bc13`
- Safety tag: `single-player-working-2026-08-03` (pushed to `origin`)
- Unity: `6000.0.34f1` (`5ab2d9ed9190`)
- Existing gameplay scene: `Assets/_Game/Scenes/MainMap.unity`
- Existing single-player prefab: `Assets/_Game/Prefabs/VRPlayer.prefab`
- Previous single-player Quest APK: successful before this milestone (51,295,321 bytes)
- Post-milestone single-player regression: 720 frames, 0 errors; player structure, map scale, both existing monsters, death, and respawn checks passed

The existing `VRPlayer` prefab has no `NetworkObject`. The current map, GorillaLocomotion implementation, player setup, scale, collisions, camera height, Tiptoe, Statue, and monster behavior were not networked or changed by this milestone.

## Package versions

- Netcode for GameObjects: `com.unity.netcode.gameobjects` `2.13.1`
- Unity Transport: `com.unity.transport` `2.7.4`
- Multiplayer Services: `com.unity.services.multiplayer` `1.2.0`
- Unity Authentication: `com.unity.services.authentication` `3.7.3`
- Multiplayer Play Mode: `com.unity.multiplayer.playmode` `1.3.3`
- Multiplayer Tools: `com.unity.multiplayer.tools` `2.2.10`

Multiplayer Services 1.2.0 is deliberately pinned. It is the current unified Sessions workflow compatible with this project's Unity 6.0 editor and Multiplayer Play Mode 1.3.3. Tested Multiplayer Services 2.2/2.3 combinations did not compile against that editor/package set.

## Architecture and assets

- Persistent bootstrap prefab: `Assets/_Game/Prefabs/Multiplayer/GameBootstrap.prefab`
- Bootstrap source: `Assets/_Game/Scripts/Multiplayer/Core/`
- Main menu: `Assets/_Game/Scenes/MainMenu.unity`
- Waiting room: `Assets/_Game/Scenes/MultiplayerLobby.unity`
- Network player: `Assets/_Game/Prefabs/Multiplayer/NetworkVRPlayer.prefab`
- Profile/settings: `Assets/_Game/Scripts/Multiplayer/Profile/`
- Networking/player synchronization: `Assets/_Game/Scripts/Multiplayer/Network/`
- World-space VR UI/controller ray: `Assets/_Game/Scripts/Multiplayer/UI/`
- Editor build and validation utilities: `Assets/_Game/Editor/MultiplayerMilestoneBuilder.cs`, `MultiplayerMilestonePlayValidator.cs`, and `MultiplayerOnlineSmokeValidator.cs`

`GameBootstrap` initializes Unity Services once, authenticates anonymously, owns resettable service instances, reports readable status/errors, and survives the menu-to-lobby transition. Raw Unity Player IDs remain internal and are not shown or logged.

The local profile stores one sanitized JSON document in PlayerPrefs through `PlayerProfileService`; unrelated gameplay code does not read PlayerPrefs. Names are nonblank, stripped of control/rich-text characters, and limited to 16 characters. Colors come from an opaque approved palette. Snap turning is the default at 45 degrees; smooth turning defaults to 90 degrees/second. Both rotate the player root around the tracked headset pivot.

## Session flow

Online rooms use anonymous Unity Authentication, Multiplayer Services Sessions, Netcode for GameObjects, and Relay transport. Capacity is four and sessions are private.

Room codes are trimmed, uppercased, and restricted to 4-12 ASCII letters/digits. A code is deterministically mapped to a versioned Unity session ID (`tbmg-net-1-CODE`) and passed to `CreateOrJoinSessionAsync`. That service operation is atomic, so simultaneous callers cannot both create different hosts for the same normalized custom code. The entered custom code is also stored as a member-visible session property for the lobby UI. Incompatible network versions are rejected.

Repeated create/join attempts are rate-limited. Connection approval validates the network version and disables automatic client-controlled player spawning. The server-owned lobby spawner creates the allowed network-player prefab. Host permissions are centralized and based on authenticated/session identity plus server client ownership, never a display name. Host status only grants room-management permission.

## Network player behavior

The owner enables the existing local XR/GorillaLocomotion rig, camera, listener, input, turning, tracking, and physical hands. Remote instances enable only colored head and hand visuals. Root yaw/position, headset, both hands, display name, and palette color are owner-written at 20 Hz and interpolated remotely. Remote visuals do not drive physics, locomotion, cameras, listeners, or arbitrary spawning.

The owner's head visual is hidden so it cannot obstruct the camera. Development pose simulation is editor-only and disables itself in production builds.

## Multiplayer Play Mode

`ProjectSettings/VirtualProjectsConfig.json` defines `AutoHost` and `AutoClient` player tags.

1. Open Multiplayer Play Mode and enable two editor players.
2. Assign `AutoHost` to Player 1 and `AutoClient` to Player 2.
3. Enter Play Mode from `MainMenu`.
4. Both players auto-enter the local waiting room with separate development names/colors.
5. Verify two player entries/visual rigs, ownership, join/leave, then leave and rejoin.

The automated single-editor local lifecycle passed: host startup, owner spawn, simulated head/hands, exactly one active camera/listener, ownership, and leave cleanup. A second editor player was configured but was not automatically launched during the command-line validation; perform the steps above for interactive two-editor confirmation.

## Service and build results

- Unity Cloud project: linked as `5b2d045d-b541-45d6-b162-0c220b01b4fa`
- Manual Cloud Project ID step currently required: none
- Live online smoke: Unity Services initialized, anonymous authentication succeeded, a private Relay-backed session was created/joined with the exact custom-code mapping, the host player spawned, and leave cleanup passed
- Online test limitation: one live service host was exercised; a second online client and two physical Quest headsets were not tested
- Android Quest: ARM64/IL2CPP build succeeded with 0 errors
- APK: `Build/TheBestMonkeyGame-Multiplayer.apk` (ignored by Git), 61,205,687 bytes
- APK SHA-256: `46F597B2E3B95AC607CE376EBC40BDC0FD23A743279FF15506BB711E684FCEFC`

The Android build reported optional OpenXR input-control migration notices and a batch-editor Unity Services linkage-state notice (the build process was not signed into the Unity Editor account). Runtime UGS initialization/authentication and a Relay-backed session were independently verified successfully against the serialized Cloud Project ID. If this repository is cloned into a different Unity organization, link it through **Edit > Project Settings > Services** and enable Authentication, Lobby, and Relay for the linked Cloud project.

## Security and future work

This milestone authenticates online users, keeps IDs hidden, sanitizes inputs, validates room/network versions and ownership, rate-limits room attempts, centralizes permissions, constrains network spawning, uses Relay rather than exposing player IPs, and does not log access tokens or credentials.

Future production work still needs persistent accounts, durable room bans, player reports, global moderation, block lists, voice safety, anti-cheat, host migration, and any backend policy/content-moderation layer desired for the global custom-code namespace.

The precise next milestone is a host-authorized transition from the waiting room into a multiplayer match scene, followed by multiplayer-safe player death/respawn and monster authority/synchronization. Monster networking and behavior changes remain intentionally out of scope until that milestone.
