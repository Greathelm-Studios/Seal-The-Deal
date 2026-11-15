# Handover: movement lane

**Status:** Ready for review (not approved) · **Last updated:** 2026-11-14 (UI only from here, per the owner) · **From:** movement lane session, branch `claude/lane-movement-864d86`

## Goal

Fast, high-mobility arena movement (the owner's STRAFTAT target; the GDD still says "chaotic, not precise") on the predicted FishNet body, with every input the other lanes need, a read-only animation state, and movement's part of the UI revamp. Owning doc: [docs/systems/PLAYER_MOVEMENT.md](systems/PLAYER_MOVEMENT.md) (feel, states, controls table, UI contract, client results, limitations).

## Where it stands

Commits on the lane branch, all merged into `buffer` by the orchestrator except the last:
- "Rework player movement: momentum, slide, vault, crouch, input and anim state": momentum model, hold-to-hop, air strafing, slide, climb and vault with refusals, stair snapping, look limits, stick speeds, the shared input actions, `PlayerAnimState` and events, the scripted play session and `Lane_Movement.unity`.
- "Show other players' crouch, lean and slide on every client; drop the test blast": the remote-pose fix (review findings PM-1, PM-2, PM-4, PM-5), test blast removed, `PlayerMotor.LocalChanged`, `UseProfile`, `LeanedEyePosition`, stick dead zones and edge turn boost and slide roll on the profile.
- "Add movement's HUD: refusal line and controls card from the UI kit": `Player/Hud/MovementHud.cs` and its setup menu, `Ground Turn Rate`, the Help action, private session ports and a server assertion.
- The settings hooks (this turn): `InputPrefs` look sensitivity, invert, stick speed, stick dead zone and key rebinding are applied in `PlayerCamera` and `PlayerInputReader`, which set `LookHooked` / `BindingsHooked` so lane 5's settings screen shows those rows.

- "Feed the crosshair from speed, air time and spread": `Player/Hud/CrosshairFeed.cs` on the kit's crosshair opens it by speed (3.6 px at a sprint) and in the air (9.6 px in a sprint jump), or by the gun's spread when wider, and pulses it on hard landings, hits and kills.

## What was checked

All by the scripted session (`Scripts/Player/Dev/PlayerMovementSession.cs`), which plays through made-up keyboard, mouse and pad devices so every press goes through the real bindings. Evidence for the first slice is in [docs/handover/movement/](handover/movement/); later logs and captures are in the session's scratch folder, listed in the turn report.
- Compile check: 0 errors.
- Solo session in `Lane_Movement.unity`: every check passes, including look ±89°, pad stick 240°/s across (357°/s with the edge boost held), crouch, slide, hops kept at 9.00 m/s, air strafing 9 to 15 m/s, climb, vault, the 2.4 m refusal, the menu opened mid-jump, a blast mid-climb, respawn, and the settings check (sensitivity and invert, a rebind of Jump to J).
- Knockback: 6 m/s forward plus 4.5 m/s up rises 0.55 m (0.52 m predicted). The "stronger knockback" report was lane 4's test calling it every 0.25 s; withdrawn by them.
- Host and client on one Mac at about 100 ms (ping 97 to 117 ms): 0 corrections over 1 cm in every route section except the slide down the spiral stairs (worst 16.3 cm). A host-side blast the client could not predict showed a 240 cm snap, so the measure works. The client saw the host crouch, lean and slide in full. Measured A/B: the host repeating a client's last input when its buffer runs dry beats running it empty (that showed corrections up to 25 cm in four sections).
- HUD captures at 1920×1080, 1280×720 and 2560×1080 in `Lane_Movement.unity` and `Lane_Integration.unity`: default, refusal, refusal faded, help on keyboard, help on pad, menu over the card. Every sound event fired (Error, PanelOpen, PanelClose).
- Not seen: a second real machine, Steam, more than two players, packet loss, ping above 120 ms, the real greybox tower's stairs (the automated run placed the body wrongly and measured nothing), and anyone playing it by hand.

## Known issues

- **Sprint dips going up the spiral stairs.** The speed trace (`solo_trace_stairs_up.csv` from the scripted session) shows it is not the turn (`Ground Turn Rate` holds speed there) but contacts: a drop from 9.0 to 6.9 m/s about 2.9 m up the flight and the worst, to 5.1 m/s, at 4.39 m, where the flight's top meets the upper floor's slab edge. Likely cause: the capsule catching on the 48 flat facets of the flight mesh and on the lip at the top. Likely fix: a smoother collider for the flight (more segments, or a separate collision mesh) and a flush top edge, in the tower tool rather than the motor. The scripted check for it fails for this reason.
- **A Space press is sometimes swallowed in the scripted session.** In about half the solo runs, the first press after the menu-opened-mid-jump check does not start the next climb; the menu check itself passes. Not seen outside the scripted session; cause not found. The likely area is the game menu's close-on-Submit-release guard (Space is also UI Submit) interacting with the session holding the menu closed.
- **Other players' gun arm has no pitch.** `PlayerMotor.AimDirection` (Harvey's viewmodel) is built from yaw only, because movement's rework removed `Aim` from `MoveInput`. Giving remote arms pitch means sending pitch in the input again (2 bytes would do); noted in VIEWMODEL.md.
- A slide down the spiral stairs leaves the treads (drifts to 4 m from the column) and is where the remaining 16-25 cm client corrections happen.
- `GreyboxSceneSetup.cs` still sets the removed motor fields `_blast`, `_playerMask`, `_propMask`; it only logs "no field" warnings. Not this lane's file.
- Ledge checks include the Prop layer, so climbing a moving prop could make host and client disagree (review PM-7, suspected, not seen).
- Bandwidth at 8 players is about 3.1 Mbit/s up from the host before packing.
- PlayerPrefs are one file shared by every checkout on this Mac (productName is still "CloseTheDeal"; renaming is Adam's call). The scripted session ignores saved rebinds, and puts back any look setting or rebind its settings check changed, even when play is stopped mid-check.
- The scripted session holds the game menu closed while it runs, because a headless editor cannot lock the cursor; a real player never sees that.

## Movement feel pass (owner re-opened movement, 2026-11-14/15)

- "Make movement a touch faster, the jump bouncier, and every move answer" (93faace): walk 6.75, sprint 10.1 m/s; rising gravity 1.6x, an apex hang (0.6x under 1.5 m/s), falling 2.4x; coyote 0.12 s, buffer 0.15 s; camera spring, slide roll and FOV, crosshair pulses, gamepad rumble, movement sound events, slide hint and a Slide line on the controls card. Measured in the filmed route: hops keep 10.1 m/s, apex 1.69 m after 0.56 s (was about 0.40 s), the ramp slide builds 9.75 to 11.5 m/s.
- This slice: the crosshair feed now adds itself to the HUD crosshair of any scene when the camera starts following the local player (so Lane_Integration gets it without a scene edit), sound slots take lane 9's variations (Move_Jump_01, _02 and so on), and a doubled comment is fixed.
- Reduce motion: the camera kicks, slide roll and FOV changes stop; the crosshair pulses also stop, because the kit's crosshair checks the same Accessibility setting.

## Assumptions made while Adam was away (confirm or change)

- A hop now lasts about 1.1 s in the air (was about 0.73 s, worked out from the profile values): "bouncier" read as more hang. If it feels floaty, Apex Gravity Multiplier (0.6) and Air Gravity Multiplier (1.6) on the movement profile tune it.
- Gamepad sprint stays a held left-stick click; a toggle would be kinder while sliding but is a design call.
- Movement is silent until recorded clips land; lane 9's generator writes them into Assets/_Project/Audio/Movement/ and Set Up Movement Sounds fills the slots.

- Feel target is STRAFTAT-like momentum, not the GDD's "chaotic, not precise".
- Hold Space to keep hopping (`Hold To Hop` on), not timed hops.
- `Ground Turn Rate` 720°/s: every turn on foot keeps its speed below 100°; reversals still brake.
- Help is F1 and the pad's View button; the card shows the device last touched.
- Refusal wording: "Can't climb that: it's above your reach." / "Can't climb that: there's no room on top."
- Lean, sprint and crouch are hold only; no toggle option.
- Profile values changed from the designer's originals: Ground Acceleration 45 to 70, braking replaced by friction 6 and stop speed 3, Air Acceleration 20 to 30, Mantle Speed Keep 0.9, plus the new values listed in the doc.

## Merge and re-run steps

1. Merge the lane branch. `DefaultMovement.asset` and `InputSystem_Actions.inputactions` are edited in place (GUIDs unchanged). `Player.prefab` is not edited by this lane.
2. Run `Close the Deal > Lanes > Set Up Movement` (lane scene and its HUD) and `Close the Deal > Lanes > Add Movement HUD To Open Scene` in any other scene that should show it (it only adds what is missing).
3. Hand test in `Lane_Movement.unity`: Host; sprint, hold Space to hop, Ctrl at a run to slide, Space at the desk, sill, ledge and the 2.4 m wall (an amber line says why), F1 for the controls card, touch a pad and watch the labels switch, Settings > Controls to change sensitivity, invert and rebind Jump.

## One thing that still makes movement feel like a demo

Other players are still capsules with a procedural pose: their crouch, lean and slide now arrive, but nothing on their body animates a slide or a vault, so the speed you feel isn't shown on anyone else.

## Next steps

The owner has stopped gameplay and mechanics work; the open items above wait for him. UI captures of movement's pieces in `Lane_Integration.unity` (crosshair, refusal line, help card on keyboard and pad, menu over the card) are listed in the last turn report.


Keep a slide on the spiral treads (or accept and document it as a level rule).
