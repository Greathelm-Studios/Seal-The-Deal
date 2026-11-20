# PLAYER MOVEMENT

**Status:** Current · **Last updated:** 2026-11-12 · **Verified against:** the movement lane branch (`claude/lane-movement-864d86`), scripted play sessions in `Lane_Movement.unity`

## What happens in the game

You see the game in **first person** from your own eye; your body is hidden from you but casts its shadow. The feel is fast, high-mobility arena movement: momentum is everything, and a good player is never standing still.

- **Run** with WASD (6 m/s) and **sprint** with Shift (9 m/s). Starts are instant (walking speed in 0.1 s) and stops are crisp (a sprint stops in 0.3 s, 1 m). The view widens a little at speed.
- **Jump** with Space: tap for a 0.66 m hop, hold for a 1.6 m jump. You come down faster than you go up. **Hold Space and you hop again the instant you land, keeping all your speed**: three held hops out of a sprint stay at exactly 9 m/s, where running would hold you to it anyway.
- **Air strafe**: in the air, holding a strafe key while turning the view the same way adds speed and curves the jump. Three strafed hops took a player from 9 to 15 m/s. Pushing forward in the air adds almost nothing; pulling back brakes hard (a sprint jump goes 6.8 m held forward, 1.7 m pulling back).
- **Crouch** with Ctrl or C: 3 m/s, eye drops from 1.6 to 0.9 m, you fit under 1.3 m gaps, and you stay down until there is room to stand.
- **Slide**: crouch while moving at 7 m/s or more and you drop into a slide with a 2.5 m/s kick (9 → 11.4 m/s out of a sprint) that carries 17.8 m on the flat. Downhill pulls you faster (9.3 → 10.4 m/s down a 20° ramp). Jump out of a slide and you keep its speed. The kick has a 1.5 s cooldown so crouch-spamming gives nothing.
- **Climb and vault**: press Space at anything between 0.4 and 1.6 m tall in front of you (or jump into it while holding a direction). A desk takes 0.32 s to get on top of; the 1.6 m ledge 0.41 s. The 0.6 m **window sill is vaulted**: at a sprint you are over it and on the street in 0.5 s, still moving at 8.5 m/s. A climb keeps most of your speed (9.0 m/s in, 8.1 m/s out). Anything taller is **refused**: you just jump, and the game raises a "can't climb that" event saying why.
- **Spiral stairs**: a whole flight (4.5 m) takes 1.9 s up and 1.5 s down at a sprint, with the feet on the treads the whole way. Going up, the tight turn costs speed: 8.2 m/s on average, 6.1 m/s at the slowest (see Known limitations).
- **Lean** with Q / E on the floor: the view slides 0.45 m and tilts 12°, and stops at a wall. Shots leave from the leaned eye; other players see the body tilt.
- **Look**: mouse, or the right stick at 240°/s across and 150°/s up and down at full tilt. Straight up and straight down both work (±89°).
- A blast **throws** you and takes control away for a moment; it interrupts a climb at once.
- **Esc or the pad's Start** opens the menu. With the menu open every input reads as idle: a jump in progress lands, then you stand still, and nothing pressed while the menu was open fires afterwards.

Your own moves show the instant you press them. The host is still the judge: if it disagrees, you are moved to where the host says you are.

## Controls (the shared actions asset)

`Assets/InputSystem_Actions.inputactions`, map **Player**. Only the movement lane edits it. Every action is read through `PlayerInputReader`, and all of them read as idle while the menu is open.

| Action | Keyboard and mouse | Gamepad | Read as | Used by |
|---|---|---|---|---|
| Move | WASD / arrows | Left stick | `Move` | motor |
| Look | Mouse | — | `LookDelta` (counts) | camera |
| LookStick | — | Right stick | `LookStick` (rate; dead zone `Look Stick Dead Zone` on the profile) | camera |
| Sprint | Left Shift | Left stick press | `Sprint` | motor |
| Jump | Space | South (A / Cross) | `JumpHeld`, `Consume(Jump)` | motor |
| Crouch / slide | Left Ctrl, C | East (B / Circle) | `Crouch` | motor |
| Lean | Q / E | LB / RB | `Lean` (−1, 0, +1) | motor |
| Attack | Left mouse | Right trigger | `Consume(Attack)`, `Held(Attack)` | weapons (movement never reads it) |
| AltFire | Right mouse | Left trigger | `Consume(AltFire)`, `Held(AltFire)` | weapons (shooting lane) |
| Interact | F | North (Y / Triangle) | `Consume(Interact)` | pickups and doors |
| Throw / drop | G | Right stick press | `Consume(Throw)` | pickups and furniture |
| Reload | R | West (X / Square) | `Consume(Reload)` | weapons |
| Weapon slots 1–4 | 1, 2, 3, 4 | D-pad up, right, down, left | `ConsumeWeaponSlot()` (1–4, 0 = none) | weapons |
| Help | F1 | View (Select / Share) | `Pressed(PlayerPress.Help)` | movement's controls card |
| Menu | Esc | Start | read by `GameMenu` directly | menu |

Presses are latched every frame and kept until the owning system consumes them on its tick, so a tap between ticks is never lost; one system per button. `Pressed` and `WeaponSlotPressed` events fire on the frame for sound and UI. **Enter no longer fires.** The pad's Attack moved from West to the right trigger so West could be Reload.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Player body (position, velocity, state, jump timers, lean, crouch, slide, climb target and exit speed, event counters) | **Host.** The owning client predicts its own body. Other clients run each other player a few ticks past that player's last input that has arrived, repeating it (presses left out) for up to a third of a second, then letting go | Host → everyone: the reconcile, about 115 B before FishNet's packing | Every tick, 60/s |
| Player input (move, yaw, sprint, crouch, jump press and hold, lean side) | Owning client | Client → host, about 19 B | Every tick, 60/s |
| Animation state (`PlayerAnimState`) | Nobody: every machine works it out each frame from the body state above | Nothing extra | — |
| Jump, landing, climb, refusal events | Counted on the tick inside the reconcile, raised once per change on every machine | Inside the reconcile | — |
| Knockback | **Host only.** Clients never decide a hit | Arrives as part of the body state | On hit |
| Camera, view dip, speed widening | Local only | Nothing | — |

Bandwidth: with **8 players** the host sends each client 8 × ~115 B × 60/s ≈ **55 KB/s**, and its own upload is about 7 × 55 ≈ **390 KB/s (3.1 Mbit/s)** before packing. That is heavy for a residential upload; see Known limitations.

FishNet runs physics itself at 60 ticks/s (TimeManager physics mode) so predicted and authoritative simulation match. Unity's own gravity stays standard; the snappy arc comes from extra pull in the profile.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored, never written by runtime:** every field on `MovementProfile` (eye height, stick dead zones and look feel included); the capsule size on the Player prefab (its height is the standing height); spawn points; the Player layer.
**Derived at runtime:** jump launch speed; the ticks left of control loss, climb, coyote, jump buffer and slide-kick cooldown; the ledge stand point and climb exit speed; the lean and crouch amounts; the crouched capsule height (the prefab's height is kept as the standing height and never written).

Timed states count **ticks remaining**, so a reconcile restores the count and a replay counts it down again.

## How the momentum works

Quake-lineage acceleration, written for this game:

- **On the floor**, grip first takes `Ground Friction` × speed off each second (`Stop Speed` sets a floor so the last bit of a stop is crisp), then the held direction adds speed along itself up to the wanted speed, at `Ground Acceleration`. Turning is sharp; nothing is added above your running speed.
- **In the air**, the held direction can only add up to `Air Strafe Speed` (1.5 m/s) measured *along itself*. Pushing forward at 9 m/s adds nothing; a direction nearly square to your velocity always has room, so turning the view while strafing keeps adding. That is air strafing. Pulling back has lots of room and brakes.
- **Hops**: the jump happens on the landing tick before the floor grips, so a held or buffered hop loses nothing.
- **Slides** replace floor grip with `Slide Friction`, add a pull down any slope (`Slide Slope Gain`) and steer like the air.
- **`Max Speed` (24 m/s)** caps what the player's own movement can build. A blast can throw faster; the player then only loses that speed to grip.

## States

| State | Enters when | Does | Leaves when |
|---|---|---|---|
| Grounded | feet within the ground probe of a floor flatter than 50° | runs or slides as above; follows stairs and ramp tops down (`Step Down Snap`); Space or held hop jumps, or climbs if something climbable is ahead | leaves the floor; jumps; climbs |
| Airborne | left the floor or jumped | air acceleration as above; air gravity rising, heavier fall gravity falling; letting go of Space while rising keeps half the upward speed; a jump still works 0.1 s after walking off an edge | lands moving downward; grabs a ledge |
| Mantling | Space at, or a jump into, something whose top is between the min and max ledge height with room to stand on or over it | rises to the top, then moves across at least as fast as you came in; gravity ignored | stands on a deep top (Grounded) or clears a thin one (Airborne), keeping 90 % of the entry speed; times out at 2× the climb time |
| Knocked | the host applies a knockback (also mid-climb) | no steering; extra gravity; skids to a stop on the floor | the control-loss time passes |

`LedgeProbe` decides climbs with static physics queries only, so prediction and host agree: a capsule sweep finds a face between 0.4 m and the body's height, a ray finds its top within reach, and two capsule checks make sure the body fits while rising and on the far side. A top the ray can't reach is refused as **too high**; one without room is refused as **no room**.

## For the rig (Alex's character) and for sound, VFX and UI

`PlayerMotor.AnimState` is a read-only `PlayerAnimState`, rebuilt once a frame on every machine: planar speed, move relative to facing (x right, y forward, m/s), grounded (from the move state, so never true while rising off a hop), vertical speed, crouch amount and crouched, lean, mantling and which kind, sprinting, sliding, knocked. Feed an Animator from it; never write back. No bones are streamed.

Events on `PlayerMotor` (C# `event`s, raised once per real change on every machine, never twice for a replayed tick): `Jumped`, `Landed(fall speed)`, `MantleStarted(kind)`, `MantleRefused(why)`, `CrouchChanged`, `SprintChanged`, `SlideChanged`, `KnockedDown`. On `PlayerInputReader` (owner only): `Pressed(button)`, `WeaponSlotPressed(slot)`.

Until there is an animation, the crouch squashes the body toward the feet (`Squash Body When Crouched` on the motor; untick it once the rig has a crouch).

## UI contract: what a HUD reads from movement

Read from `PlayerMotor.Local` (null until the local player has spawned; subscribe to `PlayerMotor.LocalChanged` once it exists, see below) and from any other `PlayerMotor` for other players. **Nothing here is polled per frame by design**: take the value when an event fires, or read `AnimState` once in `Update` only for a continuously moving visual (a speed bar). Nothing here is decided by the UI: it shows host-confirmed state and sends no requests of its own.

| Thing | Type | Who owns it | Where it comes from | When it changes |
|---|---|---|---|---|
| Planar speed | `float`, m/s, `AnimState.PlanarSpeed` | Host decides; the owner predicts | Rebuilt every frame from the body's velocity | Every frame while moving |
| Grounded | `bool`, `AnimState.Grounded` | Host / predicted | Move state, so never true while rising off a hop | On leaving or touching the floor |
| Sprinting | `bool`, `AnimState.Sprinting`; event `SprintChanged(bool)` | Host / predicted | On the floor, not crouched, above the midpoint of walk and sprint speed | On crossing it |
| Crouched | `bool` and `float` 0..1, `AnimState.Crouched/Crouch`; event `CrouchChanged(bool)` | Host / predicted | Carried in the reconcile | Event once per duck and per stand; the float eases over about 0.12 s |
| Sliding | `bool`, `AnimState.Sliding`; event `SlideChanged(bool)` | Host / predicted | Slide state in the reconcile | Once at the start, once at the end |
| Mantling | `bool`, `AnimState.Mantling`, `AnimState.MantleKind` (`Climb`, `Vault`); event `MantleStarted(MantleKind)` | Host / predicted | Move state and a counter in the reconcile | Event once per climb start |
| Mantle refused | event `MantleRefused(MantleRefusal)`: `TooHigh` or `NoRoom` | Host / predicted | Counter in the reconcile, raised once per refusal on every machine | When the owner pressed jump at something that could not be climbed. Show it to the owner only (`motor == PlayerMotor.Local`) |
| Lean | `float` -1..+1, `AnimState.Lean` | Host / predicted | Reconcile | Eases at the lean speed |
| Jump / landing | events `Jumped()`, `Landed(float fallSpeed m/s)` | Host / predicted | Counters in the reconcile | Once per jump and per landing, including each hop of a chain |
| Knocked down | `bool`, `AnimState.Knocked`; event `KnockedDown()` | Host | Move state | Once per blast |
| Speed for the crosshair | `AnimState.PlanarSpeed` against `MovementProfile.WalkSpeed` and `SprintSpeed` | | | A crosshair that opens with speed reads these three numbers once a frame; it should not hold its own copy of the thresholds |

Pressed buttons are on `PlayerInputReader` (owner only): `Pressed(PlayerPress)` on the frame a button goes down, `WeaponSlotPressed(int 1-4)`. The HUD never consumes the latches; that belongs to the system that owns the button.

For help text, the real bindings come from the actions asset: read them with `InputAction.GetBindingDisplayString` for the action names in the Controls table above, so the card changes with the player's rebinding and the device in use.

## Feel and feedback (2026-11-14 pass, owner: "a tad faster, bouncier jump, sliding, much better feedback")

| Value | Was | Now | Why |
|---|---|---|---|
| Walk Speed | 6 | 6.75 m/s | +12.5%, ratio to sprint kept at 1.5 |
| Sprint Speed | 9 | 10.1 m/s | +12.5% |
| Ground Acceleration | 70 | 80 | keeps starts snappy and stays above grip × sprint (60.6) |
| Crouch Speed | 3 | 3.3 m/s | same +10% |
| Air Gravity Multiplier (rising) | 2 | 1.6 | a slower, floatier rise |
| Apex Gravity Multiplier | (none) | 0.6 under 1.5 m/s | a moment's hang at the top: the bounce |
| Fall Gravity Multiplier | 3 | 2.4 | still quicker down than up, less of a slam |
| Knocked Gravity Multiplier | (was the air value, 2) | 2 | blasts keep their old flight |
| Coyote Time / Jump Buffer | 0.10 / 0.12 s | 0.12 / 0.15 s | more forgiving chains |

**Camera (local, `PlayerCamera.Feedback.cs`):** one damped spring (6 per second, damping 0.5, so it overshoots once) that movement kicks: take-off dips and springs up (Jump Kick), landing by fall speed (Land Kick Per Speed, capped), the drop into a slide, a gather before a climb, a slight lag when sprint starts and a nudge forward on a hard stop. It moves the eye along the view's own axes and never turns it, so aim is untouched; `PlayerCamera.FeedbackOffset` / `FeedbackRoll` expose it for the viewmodel. Sliding rolls the view about 2° and widens it 4° on top of the speed widening. **Reduce motion** (Settings > Accessibility) turns the kicks, the roll and every field-of-view change off.

**Crosshair:** besides opening with speed and in the air, it opens 4 px while sliding and pulses on take-off (0.2) and on the start of a slide (0.35).

**Rumble (gamepad only, the pad the player last touched):** on landing by fall speed (0.05 per m/s, at most 0.7) and on the start of a slide (0.35), 0.14 s each; stopped when the camera lets go.

**Sound (`MovementSounds`, added to each player at wake when `MovementProfile.Sounds` is set; heard flat for your own body, placed in the world for others, on the mixer's SFX bus):** clip names the sound lane's generator makes into `Assets/_Project/Audio/Movement/`: `Move_Footstep_Walk_*`, `Move_Footstep_Run_*` (variations, stepped by distance: 1.7 m strides walking, 2.4 m sprinting), `Move_Jump`, `Move_Land_Light`, `Move_Land_Heavy` (from 11 m/s), `Move_Slide_Start`, `Move_Slide_Loop`, `Move_Slide_Stop`, `Move_Mantle`, `Move_Vault`, `Move_Wind_Loop` (own player only, from 10.5 to 18 m/s). `Seal the Deal > Lanes > Set Up Movement Sounds` makes the set, routes it to SFX and fills empty slots from that folder. Until the clips exist, movement is silent.

**Sliding is taught:** after 1.2 s of sprinting without having slid this session, a kit status line says "Sprinting: press <crouch key> to slide." (the key for the device in use) with the Notification sound, for 3 s or until the player slides. The controls card has its own Slide line. Sprint then crouch, or land from a hop while holding crouch, starts a slide whenever the player is moving at 7 m/s or more.

## Movement's HUD

Built from the UI kit's prefabs by `Seal the Deal > Lanes > Add Movement HUD To Open Scene` (also run by `Set Up Movement`), under the game menu's `Hud`; it only adds what is missing. Script: `Player/Hud/MovementHud.cs`.

- **Refusal line:** a kit `StatusLine` at the bottom centre. When the local player presses jump at something they can't climb it says why ("Can't climb that: it's above your reach." / "...there's no room on top."), with the kit's refusal mark (amber square, navy keyline) and the `Error` sound; it fades in at the kit's hover timing, holds 2.2 s (`Refusal Seconds`), and fades out. A second refusal restarts the hold.
- **Controls card:** a kit `Plate` of `KeyHint` lines, opened and closed with Help (F1, pad View), sliding in from the left at the kit's panel timing with `PanelOpen` / `PanelClose`; with Reduce motion it only fades. Every key is read from the player's own copy of the actions (`PlayerInputReader.BindingText`), so rebinding shows, and the card switches between keyboard and controller labels as soon as the player touches the other device (`PlayerInputReader.DeviceChanged`). Opening the game menu hides the HUD and closes the card; it never comes back open on its own.
- **Crosshair:** one crosshair, the UI kit's `UiCrosshair` (its shape, colour and size are the player's settings, lane 5's). Movement's `CrosshairFeed` sits on it and sets its `OpenPixels` gap every frame: open by speed above walking (`Crosshair Open Per Speed`, 1.2 px per m/s) plus `Crosshair Open In Air` (6 px) while airborne, capped at 16 px, or by the gun's own spread (`WeaponHolder.CurrentSpreadDegrees` turned into screen pixels at the current field of view) when that is wider, never both added, because the spread already grows when moving or airborne. It eases toward that at 80 px/s. A landing faster than 8 m/s, a hit, a precision hit and a kill each `Pulse` it outward and back (0.3, 0.5, 0.8, 1.0). Shooting draws its own hit marker. All values at 100% crosshair size on a 1080p screen, on `MovementProfile` under Crosshair.

## Scripts

Under [Assets/_Project/Scripts/Player/](../../Assets/_Project/Scripts/Player/):

- `PlayerMotor.cs`: the predicted tick and states; `PlayerMotor.Mantle.cs` climbing and vaulting; `PlayerMotor.Slide.cs` sliding; `PlayerMotor.Feedback.cs` the animation state, events and the body's lean and crouch pose. `ApplyKnockback` is the only outside way to move a player, and only the host may call it.
- `LedgeProbe.cs`: finds what can be climbed or vaulted, and why not.
- `PlayerAnimState.cs`: the read-only state a rig reads.
- `PlayerInputReader.cs`: the one place input is read; see Controls.
- `PlayerCamera.cs`: first-person camera; look speeds and limits now live on `MovementProfile` (the old scene values, including the Greybox scene's −35° look-up limit, are no longer read).
- `MovementProfile.cs`: every tuning value, with tooltips; asset `Assets/_Project/Profiles/DefaultMovement.asset`.
- `Dev/PlayerMovementSession.cs`: the scripted play session; its host-and-client run found that every reader shared one actions asset, so a remote body spawning switched off the local player's input. Each reader now takes its own copy of the asset (editor and development builds only; wakes only on `-movementSession`).
- `Combat/Knockback.cs` (not this lane's): turns a blast into `ApplyKnockback` calls; unchanged.

Prefab: `Player.prefab` is a 1.8 m capsule with the pivot at the feet, mass 80, frictionless, rotation frozen. Its `Graphics` child is the FishNet graphical object: FishNet moves it smoothly between ticks, so visuals and camera never step. Under it, a `Lean` pivot at hip height (0.9 m) holds `Body`, a nested instance of the character model [STDMC_Toon](../../Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab) with its feet at the player's feet. `Graphics` also carries `PlayerAnimator` and `PlayerRagdoll`, which pose the model's skeleton and run its ragdoll each frame ([CHARACTER_ANIMATION.md](CHARACTER_ANIMATION.md)). The owner's copy renders shadow-only (a part set to cast no shadow is hidden outright), and every part gets its own prefab shadow setting back when the owner leaves.

## Checking it

`Seal the Deal > Lanes > Set Up Movement` builds `Assets/_Project/Scenes/Lanes/Lane_Movement.unity`: a lobby floor and the floor above to the tower's dimensions (28 m wide, 0.6 m sill, spiral stairs), the street outside, a 0.75 m desk, a 1.6 m ledge, a 2.4 m wall, a 1.3 m crouch gap, box steps and a 20° slide ramp. It only adds what is missing.

The scripted session plays it through made-up keyboard, mouse and pad devices, so every press goes through the real bindings:

```
Unity -batchmode -projectPath <repo> -executeMethod CloseTheDeal.Editor.Lanes.MovementLaneSetup.RunSession -movementSession solo -sessionOut <folder> -local -sessionPort 17781
```

It writes `solo.log` (PASS / FAIL / MEASURE lines), camera frames and speed traces (CSV) to the folder, and exits non-zero on any failure. For host + client, build a development player with `-executeMethod ...MovementLaneSetup.BuildSessionPlayer -sessionBuild <path.app>`, run the editor with `-movementSession host` and the player with `-movementSession client`, both with the same `-sessionPort` and `-sessionLatency <ms each way>`. Use a port of your own: the default local port is shared by every checkout on the machine.

## Seen from a client (host + client, about 100 ms round trip)

Run by the scripted session: the editor as host and a development player as client, both on one Mac over loopback with FishNet's latency simulator (ping read 97 to 117 ms). What was measured, and what was not:

- **Top speed seen:** 13.3 m/s (a slide down the spiral stairs); 11 m/s sliding on the flat; 15.4 m/s air strafing in the solo run. `Max Speed` is 24 m/s.
- **Snap on the client** (the distance the host's correction moved the body's latest predicted position, after replaying the inputs the host had not yet seen): **0 corrections over 1 cm** through sprint, slide and hop, three chained hops, air strafing, a sprinting climb, a slide, the desk climb and crouch; **2 corrections, worst 2.0 cm** on the spiral stairs going up. A deliberate host-side blast the client could not predict showed a 199.5 cm snap, so the measure does see a snap. The view never moved against the body on those frames.
- **The older "corrections" figures in the first logs were wrong**: they compared against a tick-indexed history that was labelled two or more ticks off the host's. `LastCorrectionMetres` now uses FishNet's pre- and post-reconcile events instead.
- **The host seeing the client:** the client's body moved smoothly on the host (no frame moved against its velocity in the final run; one frame stepped 8.5 times its speed, cause not found).
- **Other players' crouch, lean and slide on a client:** seen in full since the input fix (client saw the host crouch to 1.00, lean to 1.00, slide at 11.4 m/s; before it, 0.13, 0.20 and no slide). The cause was FishNet handing over empty input, flagged Ticked but not Created, for ticks whose real input hadn't arrived; the motor ran those as "let go of everything". It now repeats the last real input for up to 20 ticks (see Who owns what).
- **Not seen at all:** a real second machine, Steam, more than 2 players, packet loss, or ping above about 120 ms.

## Level rules (for whoever builds floors)

- **Stairs need ramp collision.** The body is a physics capsule with no step-up. Walking into box steps with 0.18 m risers and no ramp, it bounced its way up: airborne on 39 of 90 ticks, speed swinging between 0 and 7.6 m/s. Put a ramp collider over every staircase.
- Steps lower than the min ledge height (0.4 m) are walked over only if they are ramps too.
- Anything climbable must be on a layer in the ground mask (everything solid, not players). Props are in it.
- **A slide does not follow the spiral stairs.** Sliding down the 2.6 m-radius spiral at 10.9 m/s carried the body out to 3.8 m from the column, off the treads. On floors with the glass cage that means sliding along the glass; in the lobby it means flying off the side. Leave room, or accept it as part of the chaos.

## Known limitations

- No ragdoll: a knocked or mantling player holds the airborne pose. Animation is procedural and has its own doc, [CHARACTER_ANIMATION.md](CHARACTER_ANIMATION.md). Ragdoll blending is its own task.
- **The animated body is wider than its collision.** The capsule is 0.7 m across; swinging arms and a leaning torso pass through walls and door frames.
- Other players are run ahead on their last input; when they let go or turn sharply, the next host state pulls them back, which can show as a small correction on their body.
- No ladders or ziplines yet; climbing means mantling.
- **A leaning player's head is not hittable.** The collider stays an upright capsule, so the 0.45 m of head that pokes round a corner can see and shoot but can't be hit. Fix when real hit detection lands: a head hitbox on the lean pivot.
- No gamepad lean testing yet; it is bound to the shoulder buttons. Interact moved from E to F to free E for lean; nothing used Interact yet.
- **Sprint speed dips at the foot of the spiral stairs.** With `Ground Turn Rate` 720°/s a sprint holds its speed round the flight's turn (the flight now takes 1.66 s, was 1.84 s; mean 7.8 m/s), but the step from the floor onto the first tread still dips it to about 5.3 m/s for a moment. Measured on the lane's slice, which uses the template tool's stair numbers (2.6 m treads, 0.8 m column, 4.5 m floors); the scripted run in the greybox tower placed the body on top of something at the start and measured nothing, so the real tower is not measured.
- **Bandwidth at 8 players** (about 3.1 Mbit/s up from the host before packing) is too much for many home connections. The fix, when needed: send other players' reconciles less often than every tick, or cut the reconcile down (the climb target and exit can be sent only while climbing).
- No ladders, ziplines or wall jumps.
- The committed floor templates in `Assets/_Project/Floors/` predate the layout [TOWER.md](TOWER.md) and the template tool describe: `Floor_Lobby.prefab` is 20 × 10 m with a 1 m sill, 4 m floors and straight ramps, where the tool builds 28 × 14 m, a 0.6 m sill, 4.5 m floors and spiral stairs (and `SpiralFlight.asset` is not committed). The doc and the tool agree, so the prefabs look stale: `Seal the Deal > Greybox > Rebuild Floor Templates` should bring them up to date. The lane scene builds its own slice to TOWER.md's numbers, so the stair, sill and slide results above are from that slice.
