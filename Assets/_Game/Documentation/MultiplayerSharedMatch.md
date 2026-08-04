# Shared Multiplayer Match

## Match and scene flow

`MultiplayerMatchManager` is a persistent server-owned `NetworkObject` registered by `MultiplayerMatchBootstrap`. Its replicated state moves through `Waiting -> Starting -> Playing -> Ending -> ReturningToLobby -> Waiting`. Only the server writes that state. The room remains connected while Netcode scene management loads `MainMap` or `MultiplayerLobby`; players are not disconnected and the session is not recreated.

The host's `START MATCH` control is in `Assets/_Game/Scenes/MultiplayerLobby.unity`. It is hidden from clients, disables during a transition, and submits a server RPC that is independently checked by `MatchPermissionValidator`. The host-only `END MATCH` control is part of `Assets/_Game/Prefabs/UI/InGameMenu.prefab` and uses the same server-side validation. Start and End are the host's only match permissions.

Rooms publish `Waiting`, `Starting`/`Playing`, or `ReturningToLobby` in the session match-state property. Connection approval and session joining reject fifth players, incompatible builds, and late joins while a match is active. A scene-load timeout disconnects only timed-out clients and recovers the remaining room to the lobby.

## Authority ownership

- Server: match state, scene transitions, spawn assignments, monster spawning and AI, target choice, NavMesh movement, gaze evaluation, teleports, kill validation, death state, respawn destination, and protection expiry.
- Owning client: XR tracking, camera, AudioListener, GorillaLocomotion, turning, physical collision hands, local menu, compact head/hand pose upload, and applying server relocation commands.
- Remote clients: interpolated head/hands, name/color, interpolated monster transforms, replicated animation state/speed, and discrete spatial audio cues. They have no XR input, camera, AudioListener, GorillaLocomotion, authoritative hands, navigation, or kill authority.

## Player spawns and respawn

`MultiplayerSpawnManager` uses four indexed transforms under `MainMap/MultiplayerMatchSystems/MultiplayerPlayerSpawns`. Connected player objects are sorted by `OwnerClientId` and assigned one unique point. Each point is offset around the corrected safe-room floor spawn without scaling the XR rig. Relocation moves the complete local player root, zeroes linear/angular velocity, resets GorillaLocomotion head/hand history, and restores movement.

All players receive the seven-second match startup grace plus an extra second of spawn protection. A validated kill affects one `NetworkPlayerMatchState`: it marks that owner respawning, briefly fades that owner only, relocates them to their assigned point, restores movement, marks them alive, and gives three seconds of server-enforced protection. Respawn coroutines are independent and are cancelled safely on disconnect or End Match. No jumpscare path is used.

## Shared monsters

Multiplayer spawns exactly one `NetworkTiptoe` and one `NetworkStatue`; the original prefabs and local brains remain available for Single Player. Only the server enables the NavMesh agents or perception loop. Monster transforms replicate at a controlled 15 Hz and clients interpolate them, avoiding a competing `NetworkTransform`.

Tiptoe evaluates all connected, alive, non-respawning, unprotected match players. Direct-line-of-sight candidates are preferred, then distance. He keeps a target for a minimum commitment window and requires a meaningful distance improvement before switching, preventing frame-by-frame flicker. Losing sight produces last-known-position search before roaming resumes.

Statue awareness and victim selection are server-side. She freezes when any relevant player is alive, unprotected, inside 22 metres, has her inside a strict 20-degree central headset gaze cone, and has unobstructed line of sight. Synchronized headset forward vectors are used—never hand direction. She may teleport only when no relevant player directly watches her, and one observer can freeze her while she is targeting someone else.

## Leaving behavior

A normal client's existing Leave Game action shuts down only that client's connection, despawns its network player, releases its spawn, removes it from monster candidates, and returns it to `MainMenu`. The match continues. Host Leave Game keeps the established behavior: delete/close the room, disconnect members, and return everyone to `MainMenu` with the existing disconnect presentation. End Match is different: it stops kills and AI, returns everyone to the lobby, restores lobby player objects, and keeps the room usable for another round.

## Development validation

`MultiplayerMatchPlayValidator` automates a local-host full cycle: lobby, Start, MainMap, spawn/protection, both shared monsters, host death/respawn, End, connected lobby return, and final room leave. Multiplayer Play Mode tags `AutoHost`, `AutoClient`, and `AutoMatchCycle` provide a two-instance path; the host drives the synchronized round and both instances log their spawn/player count and lobby return. These paths are wrapped in `UNITY_EDITOR` and are absent/inactive in Quest players.

Current limitation: automated local-host validation cannot prove Internet/Relay behavior, timing across two physical headsets, real headset floor calibration, real tracking occlusion, comfort, or Quest thermal/bandwidth performance. Complete those on hardware before release.

## Physical two-headset checklist

1. Install the same ARM64 IL2CPP build on two Quest headsets and confirm identical network version.
2. Host a private room on headset A; join its exact code from headset B. Verify both lobby avatars, names, colors, head, and hands.
3. Confirm only A sees Start Match. Start, time both scene loads, and verify unique floor-aligned spawns with no launch or overlap.
4. During the seven-second grace, verify neither monster moves or kills. Confirm one shared Tiptoe and Statue occupy the same positions on both headsets.
5. Have each player alternately attract Tiptoe. Check line-of-sight targeting, commitment/hysteresis, search, and that only the killed player fades and respawns with three-second protection.
6. Watch Statue centrally from either headset while the other player is targeted. Confirm she freezes. Look away on both headsets and confirm only the server-chosen teleport occurs, identically for both.
7. Kill the host, then the client, then attempt near-simultaneous deaths. Confirm the match and other player's control continue.
8. Have the client Leave Game mid-match. Confirm only B returns to MainMenu and A's match continues without stale monster targeting.
9. Rejoin only after A ends the match. Confirm an active-match join attempt gives the clear rejection message.
10. From A, use End Match while a respawn is active. Confirm both connected players return to the lobby, Start re-enables, and a second round starts without duplicate players/monsters/cameras/listeners.
11. Repeat with host Leave Game and verify the room closes and B receives the existing host-disconnect message.
12. Run Single Player from a cold launch: verify the original VR player, Tiptoe, Statue, death/respawn, floor calibration, pause menu, and absence of multiplayer UI.
