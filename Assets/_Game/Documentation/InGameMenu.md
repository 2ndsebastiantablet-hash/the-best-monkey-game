# VR In-Game Menu

Status date: 2026-08-03

## Opening and closing

Press the **left Meta/Oculus controller menu button** to open the menu during single-player gameplay or while locally owned in the multiplayer waiting room. Press the same button again, or select **Resume**, to close it.

- Input action: `XRI LeftHand/Menu`
- OpenXR/Input System binding: `<XRController>{LeftHand}/menuButton`
- Input reference asset: `Assets/_Game/Input/LeftMenuButtonAction.asset`
- Input action asset: `Assets/_Game/Input/TBMGInputActions.asset`
- Menu prefab: `Assets/_Game/Prefabs/UI/InGameMenu.prefab`
- Runtime controller: `Assets/_Game/Scripts/UI/InGameMenuController.cs`

The action is edge-triggered and debounced for 0.25 seconds. The right controller system button is deliberately not bound because Quest reserves it for the operating system.

The menu is part of both `VRPlayer` and the local-owner portion of `NetworkVRPlayer`. It is active in `MainMap` and for the owning player in `MultiplayerLobby`; the same network-player prefab can carry it into later multiplayer match scenes. Remote player instances never enable or network their menu.

## Local pause behavior

Opening the menu is a local input suspension, not a simulation pause. It:

- disables the local `GorillaLocomotion.Player` component and its hand-push state;
- disables the local `VRTurningController`;
- temporarily disables the local physical hand colliders;
- clears linear/angular Rigidbody velocity and holds the body kinematic;
- leaves the head and both controller tracked-pose components enabled;
- enables lightweight left- and right-controller UI rays;
- places a world-space panel in front of the headset and checks for nearby walls before choosing its distance; and
- creates an EventSystem/Input System UI module only when the scene does not already have one.

`Time.timeScale` is never changed. Monsters, remote players, the network session, and host simulation continue normally.

Resume restores the exact previous locomotion, Rigidbody, collider, and turning states. It clears velocity again and resets GorillaLocomotion history so closing the menu cannot launch the player from stale hand or head motion. Scene transitions mark the menu as leaving so it cannot restore movement while unloading.

## Buttons

- **Resume** closes the panel without reloading the scene.
- **Settings** opens the existing reusable player settings panel (name, color, and turning settings).
- **Leave Game** disables itself, shows `Leaving room...`, blocks duplicate requests, performs cleanup, and returns to `Assets/_Game/Scenes/MainMenu.unity`.

Single-player leave clears motion and keeps local movement disabled during the scene transition. Multiplayer leave is bounded by an eight-second cleanup timeout; a failure is logged and a safe local return to MainMenu is still attempted.

For a client, leaving removes only that member from the Multiplayer Services session, shuts down its local Netcode instance, clears local room state, and leaves the host and other members running. For a host, leaving first sends connected clients a `The host ended the room.` disconnect reason, deletes the Multiplayer Services session, shuts down Netcode, and clears room state. Clients translate the deleted-session signal into the same readable room-ended result.

## Validation and Quest playtest checklist

Automated editor validation covers prefab uniqueness, exact input binding, local-owner-only multiplayer setup, open/resume state restoration, two controller rays, one EventSystem, tracking preservation, stale-velocity prevention, single-player leave, and a clean MainMenu return. Local-host and live online session regressions are maintained by the multiplayer validators.

Validation on 2026-08-03 completed with no Console errors:

- In-game menu single-player play-mode validator: passed (`errors=0`).
- Local Netcode host lifecycle and leave cleanup: passed (`errors=0`).
- Live Unity Services/Relay host create-or-join and host-delete cleanup: passed.
- Android Quest ARM64/IL2CPP build: succeeded with 0 errors and 2 build-report warnings.
- Ignored APK output: `Build/TheBestMonkeyGame-Multiplayer.apk`, 61,257,239 bytes.
- APK SHA-256: `BF0C4507C7F6AFCE2B66A7976FAF29FB1CBE1317CFF7CE718A19985FC737F846`.

On a physical Quest, verify:

1. In `MainMap`, tap the left controller menu button once to open and once to close; holding it must not rapidly toggle.
2. Open immediately after spawn and again after a death/respawn.
3. While open, look around and aim/select every button with each controller; neither hand should push the player and turning must be inactive.
4. Resume on level ground and confirm there is no jump, slide, or velocity launch.
5. Open near a wall and confirm the panel remains readable and does not intersect the wall.
6. Enter the multiplayer waiting room as host and as client, then repeat the open/resume checks.
7. Have a client leave and confirm the host and remaining client stay connected.
8. Have the host leave and confirm every client receives the room-ended result and can return to MainMenu.
9. Confirm MainMenu has normal audio and UI after every leave path.

## Known limitations

- Quest controller ergonomics and the OpenXR menu-button binding still require a physical-headset playtest; editor automation cannot prove the hardware button event.
- A second live online client and two physical Quest headsets have not been exercised by the automated validation.
- Host migration is not implemented. The current intended behavior is to close the room when the host leaves.
- Match-scene wiring will reuse the player prefab, but no separate network match scene exists yet.
