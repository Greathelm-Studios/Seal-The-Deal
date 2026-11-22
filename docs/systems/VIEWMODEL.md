# Viewmodel (first-person arm)

**Status:** Current (slice 1 of the weapon work, third pass, plus 2026-11-21 true-POV body-view polish: the body view now carries fire kick, reload, sprint carry, climb drop, look lag, smoothed asymmetric ragdoll swing, crosshair keep-out cushioning, and left-arm sightline deflection, and the old separate `ViewmodelCamera` overlay pass has been removed. Still awaiting human play-feel review) · **Last updated:** 2026-11-21

In first person you see your own character's right arm, in the same suit and the same toon shading, holding a pistol one-handed to the right of the crosshair.

- **Standing:** the gun is held low in the bottom right of the view, well clear of the crosshair, nearly upright (tipped 4 degrees) and pointing straight ahead at the end of an almost straight arm, and rises and falls very slightly as you breathe. The forearm comes in straight behind the gun, with the wrist bent about 6 degrees. The palm lies against the right of the grip, the fingers close round the front onto its left side and the index finger sits in the trigger guard.
- **Turning:** the gun trails slightly behind the view, tips into the turn and catches up.
- **Setting off, stopping and strafing:** the gun hangs back from the direction you move and tips with a strafe, then swings a little past rest when you stop.
- **Running:** the gun sways in a smooth arc with each step; the sway fades out in the air and when you stop. Above walking speed it is carried a little lower and turned in.
- **Jumping and landing:** the gun sinks as you go up, lifts as you fall and dips on landing, deeper after a longer fall.
- **Leaning:** the gun trails the slide of the lean and stays a little more upright than the tilted view.
- **Climbing a ledge:** the gun drops out of the way and comes back up when you are over.
- **One hand or both:** a weapon is held in the right hand alone, or in both. In both, the gun moves to the middle of the view, a little closer, and the left arm comes up and cups the grip; the body does the same. Nothing in the game is two-handed yet, so for now **T** switches the stand-in pistol between the two (on your machine only).
- **Attack button:** the muzzle jumps up, the gun punches back and twists a little, and the hand goes with it. This is only the look: the pistol does not shoot anything yet (the attack button also still fires the old test blast).
- **R:** the gun drops, tips muzzle-up and rolls onto its side, then comes back. There is no ammunition yet.
- **Walls:** the arm is never cut by a wall, however close you stand.
- **Other players, the dev mirror and your own shadow:** see your full body holding the same pistol in the same aimed stance: right arm straight out in front of the eye, hand round the grip, the gun pointing where you aim, up and down included. Standing still it is steady and on the aim. Moving, the arm flips about: it is thrown wide by a strafe, up and down by a jump and a landing, and the muzzle is thrown off target with it, then it comes back to straight in about half a second. They never see the first-person arm. Knocked back or climbing, the arm goes with the rest of the body and the gun stays in the hand.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| The arm, the held pistol, sway, bob, kick and reload move | Each client, for the player it controls | Nothing | None |
| The body's gun arm and the pistol in its hand | Each client, for every player it shows | Nothing new: it reads the aim direction already inside each tick's input | None |

Nothing reads either arm back, so it cannot desync and costs no bandwidth. The body's arm points along `PlayerMotor.AimDirection`, which every machine fills from the input each tick runs on (the owner's own, the one the host receives, the one other clients are forwarded). Firing, ammunition, pickups and what other players see in the hand are separate, host-owned work (slices 2 to 4, and Adam's shooting and pickups lanes).

## How it is posed

The approach is the NorseFPS project's: **the gun is placed, and the arm follows it.**

1. `PlayerViewmodel` (execution order `50`, after `PlayerCamera` at `0`) computes look lag, lean counter-roll, sprint carry, climb drop, fire kick, and reload motion into `BodyViewMotionOffset` and `BodyViewMotionRotation`.
2. `PlayerHeldWeapon` (execution order `105`, after `PlayerAnimator` at `100` and before `PlayerRagdoll` at `110`) bends the body's right arm onto the aimed grip using `HandGrip` (and the left arm onto `GripSupport` when two-handed) so the ragdoll chases that aimed pose.
3. `HeldWeaponSettle` (execution order `115`, after `PlayerRagdoll` at `110`) runs `PlayerHeldWeapon.Hold(true)` a second time each frame. Unity runs each component once per frame at its own execution order, so a second component is what lets the arm hold bracket `PlayerRagdoll`: the steady sag under gravity (`_restOffset`, `_restTurn`) is stripped out while movement swing (`BodyViewArmSwing` for the owner, `BodyArmSwing` for others) is kept, clamped, and re-applied.

**Why `HandGrip` uses an analytic two-bone solve:** `PlayerAnimator` clears the humanoid avatar on frame 1 (because generic clips are written directly to the rig), so Humanoid IK is unavailable at runtime and Animation Rigging is avoided as an extra dependency. `HandGrip` is a plain `[Serializable]` class (not a component) so whichever component owns the skeleton controls exact frame timing. `Bind` reads the T-pose open hand and rest hand-to-forearm twist at `Start` before the avatar is cleared; `Reach` shares 70% of the axial wrist roll onto the forearm (`AlignForearmTwist`) without moving the hand so the sleeve cuff stays aligned behind the grip instead of candy-wrapping or poking out when the gun tilts. `EnsureCurledHand` caches baked finger quaternions (`Side = +1` right, `-1` left) and skips finger curling for remote players beyond `12 m`.

**The body follows the view.** `PlayerViewmodel` gives every player's body a `PlayerHeldWeapon` at spawn (`Attach`, rather than baking it onto `Player.prefab`, which is shared across lanes). It reads the weapon model, the hand's seat on it, the finger curls, and the elbow swing from `ViewmodelArms.prefab`, and the gun's place in front of the eye from `ViewmodelProfile`. Weapon copies are parented directly to `_grip.Hand` (scaled by `hold.Model.lossyScale / boneScale` to undo rig scale) so the gun rides with the ragdoll between passes, and seated at `_grip.Hand.position - gunRotation * _gripPosition` so the weapon never floats if steep aim puts the target out of reach. The body's gun is also held `6 cm` further out along the aim in third person (`BodyGunForward`), because the body's real shoulder is nearer the gun than the view's shoulder. Both passes pin the elbow hint (`ElbowSide = (0.45, -1, 0)` in third person, `BodyViewElbowSide = (0.65, -1, -0.25)` in first-person body view) so the arm keeps one shape while the torso sways.

So the hand cannot drift off the gun, whatever moves it. The first pass did the reverse (played a full-body pistol clip and hung the gun off the hand bone); seen from the eye that clip's arms filled the screen and its loose hand did not fit this gun.

## How it is drawn

The local player looks straight out of the real character body through the main camera (no separate `ViewmodelCamera` overlay pass). `PlayerViewmodel.OnBeginCameraRendering` writes a world-space sphere `_BodyViewHeadSphere` centred on `_head.position + _head.up * 0.155` (radius `BodyViewHeadClip` = `0.20 m`) for the owner's camera so the character and eye-card shaders clip the head, hair, and eye cards without cutting into the shoulders or sleeves, and clears `_BodyViewHeadSphere` to zero for mirrors, shadows, and every other camera. `ViewmodelArms.prefab` remains as the weapon/grip template asset read at spawn (`GreyboxViewmodelSetup` cuts arm meshes in Unity rather than Blender to preserve weights, bind poses, and outline normals without Blender's 100x scale trap, keeping `updateWhenOffscreen = true` and `AlwaysAnimate` so whole-body bounds never cull the arms).

## Files

- [PlayerViewmodel.cs](../../Assets/_Project/Scripts/Player/Viewmodel/PlayerViewmodel.cs): on the root of [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab). Creates the arm and its camera for the local player and places the gun each frame. Weapon code calls its `Equip()`, `Fire()` and `Reload()`.
- [ViewmodelArms.cs](../../Assets/_Project/Scripts/Player/Viewmodel/ViewmodelArms.cs): on the arms prefab. Shoulder pin and the left arm fold; the arm itself is a `HandGrip`.
- [HandGrip.cs](../../Assets/_Project/Scripts/Player/Viewmodel/HandGrip.cs): one right arm holding one grip: the two-bone solve, the hand turn and the finger closing, with the elbow swing and curl values. Used by the first-person arm and by the body.
- [WeaponHold.cs](../../Assets/_Project/Scripts/Player/Viewmodel/WeaponHold.cs): what the hands hold: a model and where each hand sits on it. Weapon code fills one in per weapon.
- [PlayerHeldWeapon.cs](../../Assets/_Project/Scripts/Player/Viewmodel/PlayerHeldWeapon.cs) and [HeldWeaponSettle.cs](../../Assets/_Project/Scripts/Player/Viewmodel/HeldWeaponSettle.cs): the body's gun arm, added to the player's `Graphics` object at spawn (not on the prefab). The second file is its after-the-ragdoll pass.
- [ViewmodelProfile.cs](../../Assets/_Project/Scripts/Player/Viewmodel/ViewmodelProfile.cs) and [DefaultViewmodel.asset](../../Assets/_Project/Profiles/DefaultViewmodel.asset): the tuning panel. Runtime never writes it.
- [ViewmodelArms.prefab](../../Assets/_Project/Prefabs/Viewmodel/ViewmodelArms.prefab): its origin is the eye. `GunMount` holds `Pistol` and `GripHand`; `Shoulder` is the shoulder point; `Arms` is the character's skeleton with arm geometry only. Generated; edit it and the tool leaves it alone from then on (§4).
- [GreyboxViewmodelSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxViewmodelSetup.cs): `Seal the Deal > Greybox > Rebuild Viewmodel` re-cuts the arm meshes from [STDMC_Toon.prefab](../../Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab), and rewrites the arms prefab. If [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab) has no `PlayerViewmodel` it asks, then adds that one component and changes nothing else; it never rebuilds the player prefab (it did until 2026-11-14, which on a prefab carrying other people's parts would have stripped them). Run it after the character model changes.
- [Assets/_Project/Art/Characters/STDMC/Viewmodel/](../../Assets/_Project/Art/Characters/STDMC/Viewmodel/): the arm-only meshes (and their outlines), cut by the tool. Never edit them; they are rewritten.
- [STDMC_ViewmodelMoves.fbx](../../Assets/_Project/Art/Characters/STDMC/Animations/STDMC_ViewmodelMoves.fbx): `Pistol_Idle_Loop` plays under the solve and decides which way the elbow bends. The file's other three clips are unused since the second pass. How it was made: [ArtSource/Characters/Viewmodel/README.md](../../ArtSource/Characters/Viewmodel/README.md). Generic, for the reason under "The clips" in [CHARACTER_ANIMATION.md](CHARACTER_ANIMATION.md).
- [Pistol.fbx](../../Assets/_Project/Art/Weapons/Models/Pistol.fbx): a plain stand-in pistol, 20 cm long, built from [ArtSource/Weapons/pistol.py](../../ArtSource/Weapons/pistol.py). Muzzle along +Z, origin at the middle of the grip. Shown at 75% size in the hand, because this character's hand is 6 cm across the knuckles.

## Tuning

In `DefaultViewmodel.asset`; changes show while playing.

| Inspector value | What it does |
|---|---|
| Field of view | How big the arm and gun look. Lower = bigger and closer. 62, as in NorseFPS. |
| **Gun position** | **The main framing dial**: where the grip sits from the eye (right, up, forward, metres). Keep it within about 0.4 m of the shoulder or the arm locks straight. |
| Gun turn | Resting pitch, yaw and roll of the gun. NorseFPS's 4 degrees up and 9 degrees in. |
| Shoulder position | Where the arm comes from. Level with the gun and behind it keeps the wrist straight; lower bends it. |
| Body gun forward | How much further out the body holds the gun than the view does, in metres along the aim, to straighten the body's arm. Default 0.06. |
| Body arm swing | How much the body's gun arm and muzzle move about when you move, as a multiple of what the ragdoll throws them. 0 = dead still on the aim; 1 = the ragdoll's own; default 0.5 (1.5 bounced far too much). Capped at 4 cm sideways, 7 cm up or down, 6 cm forward or back, and 12 degrees however high it is set. |
| Body swing settle seconds | How long the arm takes to come back to straight on the aim. Default 0.35. |
| Two-handed position / turn / blend seconds | Where the gun sits and how it is turned when held in both hands, and how long it takes to move there. Nearer the middle and closer in than the one-handed place, because the left arm has to reach it. |
| Look lag seconds / limit / settle / roll / shift | How far the gun trails a turn, the most it may trail, how quickly it catches up, and how much it tips and slides as it trails. |
| Move lag seconds / speed limit | How far the gun is left behind by your movement, and the fastest movement it reacts to. |
| Move spring speed / damping | How tight and how bouncy that is. Lower damping = more overshoot on stops and landings. |
| Land bounce | How hard a landing dips the gun. |
| Strafe roll / air tilt / lean counter roll | How far the gun tips in a strafe, while rising or falling, and how upright it stays in a lean. |
| Sprint shift / turn / blend | The carry the gun moves to between walking and sprint speed. |
| Climb drop / tilt / blend | How far the gun gets out of the way on a ledge. |
| Breath height / seconds | The idle rise and fall. |
| Bob height / side / roll / steps per metre / fade | The walk bob. |
| Kick pitch / back / roll / recover | The shot kick. It moves the gun only, never the aim. |
| Reload seconds / drop / tilt / roll | The reload move. |

On the arms prefab (`ViewmodelArms`): **Elbow swing** under the **Arm** header is how far the elbow hangs out to the side. Under the **Grip** header: how far the gripping fingers, the trigger finger and the thumb close, in degrees per joint. The hand's seat on the gun is the `GripHand` object under `GunMount`: its blue axis is where the fingers point, its green axis runs from little finger to index finger.

Where the starting numbers came from: NorseFPS frames its hand cannon at (0.18, -0.12, 0.70) m. This character's arm is 0.43 m long, so the gun was first pulled in to 0.32 m along the same line from the eye. In play that put the gun at mid-height beside the crosshair with a wide slab of sleeve under it, so on 2026-11-14 (Harvey's request, "arm lower", "a lazy-like grip") the gun moved to (0.125, -0.135, 0.38), turned (-1, -3, 4), with the shoulder at (0.165, -0.165, -0.10) (a 16 degree cant was tried first and dropped when Harvey asked for it "a lot straighter" and the arm further out); before that they were (0.082, -0.055, 0.32), (-4, -9, 0) and (0.17, -0.20, -0.08). The same day the hand was reseated: `GripHand` went from (0.024, 0.036, -0.05) pointing straight along the gun to (0.03, 0.005, -0.055) turned 4 degrees inward and 6 up, with tighter finger curls (grip 80/95/50, trigger 50/70/35, thumb 45/35/25), because before that the fingers hooked out in front of the grip and held nothing. The wrist was also badly bent (about 80 degrees between forearm and hand, measured): the shoulder sat far below the gun and the idle pose hangs the elbow straight down, so the forearm came up at the hand steeply. The shoulder is now level with the gun and behind it, and the arms prefab has an **Elbow swing** value (20 degrees) that swings the elbow out to the side. Bob and look-lag sizes are NorseFPS's; the kick is about 60% of its hand cannon's. The movement-weight numbers are first guesses, not play-tuned.

## Driving it from weapon code

```csharp
// On every machine, once, when weapon code takes over this player:
viewmodel.Driven = true;            // the stand-in keys (attack, R, T) stop

// On every machine, whenever the weapon a player holds changes:
viewmodel.Equip(new WeaponHold(model, gripSeat, gripTurn, supportSeat, supportTurn, twoHanded));
// or, for a weapon with no place for the left hand:
viewmodel.Equip(new WeaponHold(model, gripSeat, gripEuler));

// On the owning client:
viewmodel.Fire();                   // a shot
viewmodel.Reload(seconds);          // a reload started; lasts as long as the weapon's
viewmodel.CancelReload();           // it was interrupted
Transform muzzle = viewmodel.Muzzle; // where tracers should leave from
```

`Equip` swaps the model in the first-person view and in the body's hand together, and moves the hand seats onto it. Seats are in the model's own space from its pivot: blue (+Z) is where the fingers point, green (+Y) runs from the little finger to the index finger. The model's pivot is the point the view places in front of the eye, and its muzzle should point along +Z. `Muzzle` is the model's child named `Muzzle`, or the model itself when it has none. Nothing here is synced: call `Equip` on each machine from whatever syncs the held weapon. The body keeps every copy it has made and switches them on and off, so switching back and forth creates nothing.

`Fire`, `Reload` and `CancelReload` do nothing on players the client does not control.

**Meant for Adam's weapon system** (`WeaponHolder` / `WeaponView`, on his branch, not on origin as of 2026-11-14). The agreed split: his system keeps shooting, tracers, impacts and the camera kick, and stops drawing its own gun in the view and on the body; this viewmodel draws the gun in both places. Nothing is wired yet.

The wiring on his side: `Fire()` on `WeaponHolder.Fired` when the shot is the owner's own predicted one, `Reload()` on `ReloadStarted`. Setting `Driven` is what stops a shot kicking the gun twice.

## Body view (true POV)

True POV (looking out of the real body) is now the only first-person view; the old separate `ViewmodelCamera` overlay arms and F5 toggle have been removed. `ViewmodelArms.prefab` remains as the weapon/grip template asset read by `PlayerHeldWeapon` at spawn so `Player.prefab` and weapon seat authoring stay intact.

What keeps it comfortable: the view's direction still comes from the mouse alone, never from the head. The eye's position goes all the way to where the real head has settled (so the eye is never left behind or inside the body), but takes about a tenth of a second to get there; of the quick movement on top, the bob of each step and the jolt of a hit, it takes only a capped share.
The owner's own head is hidden from their own eye only: `PlayerViewmodel.OnBeginCameraRendering` sets a world-space sphere `_BodyViewHeadSphere` centred on the real head bone (`_head.position + _head.up * 0.155`, radius `BodyViewHeadClip` = `0.20` m) for the owner's camera and clears it to zero for every other camera. Because the sphere sits high around the skull and moves with the real head bone rather than the smoothed camera, the head, hair, and eye cards stay hidden without slicing into the shoulders or sleeves, while mirrors, shadows, and other players always see the full head.
Drops of blood that would start within half a metre of the eye are left out, so being hit does not fill the screen with red squares.

What keeps the arm lively without getting in the way:
- **Framed lower-right hold with reach slack:** in first person `PlayerHeldWeapon` omits third-person's extra `BodyGunForward` push and places the gun at `GunPosition + BodyViewGunShift` (`(0.045, 0.015, 0.04)` m, total `(0.170, -0.120, 0.420)` m) in the camera's exact rotation (`bodyEye.transform.rotation`, so lean, slide roll, and strafe roll match), with the eye at `BodyViewForward = 0.065` m so the lens stays clear of the shoulder/bicep while leaving enough elbow bend that strafes do not lock the arm straight.
- **Wide elbow & forearm twist alignment:** `BodyViewElbowSide` `(0.95, -1, -0.12)` keeps the elbow and sleeve out toward the right edge of the frame instead of tucked into the POV, `HandGrip.AlignForearmTwist` shares 70% of the axial wrist roll onto the forearm so the sleeve cuff does not poke out around the grip during tilts, and `PlayerAnimator.TurnHeadFirst` caps the standing head-lead limit to `15` degrees for the local player so turning right while standing does not twist the right shoulder across the lens.
- **Crisp asymmetric swing + crosshair keep-out:** the ragdoll's throw is scaled by `BodyViewArmSwing` (default `0.45`), soft-clamped asymmetrically (`+4.5 cm` outward / `-4.5 cm` downward, `-0.8 cm` inward / `+1.5 cm` upward, `-1.2 / +1.4 cm` fore/aft, and `±10°` turn), smoothed over `BodyViewSwingSmooth` (`0.035 s`) to strip solver jitter, and cushioned with a smooth exponential floor/ceiling (`BodyViewKeepOutRight = 0.155`, `BodyViewKeepOutDown = 0.085`, `BodyViewMinForward = 0.36`, cushion `0.03 m`) so strafes keep the pistol framed in the lower-right and never swing the arm into the sightline. `BodyViewKeepOutRight` scales by `(1 - TwoHandBlend)` so two-handed holds still center naturally.
- **Weapon actions & mantling:** `PlayerViewmodel` exposes `BodyViewMotionOffset` and `BodyViewMotionRotation` (fire kick, reload dip, sprint carry, climb drop, look lag, and lean counter-roll, without doubling up on step bob or camera carry), and `PlayerHeldWeapon` stays active during `MoveState.Mantling` for the local player so climbing drops the gun cleanly instead of waving across the face.
- **Free left arm deflection:** when one-handed, `KeepLeftArmClear` checks if the empty left hand enters the central sightline box and gently bends only the upper/lower arm (`HandGrip.ReachArm`, leaving hand rotation and fingers free) out to the left or down.

Tuning, on `DefaultViewmodel.asset` under "Body view": **Head follow** (`0.08`), **Smoothing** (`0.22`), **Forward** (`0.065`), **Max offset** (`0.15`), **Gun shift** (`(0.045, 0.015, 0.04)`), **Head clip** (`0.20`), **Arm swing** (`0.45`), **Swing smooth** (`0.035`), **Keep out right** (`0.155`), **Keep out down** (`0.085`), and **Min forward** (`0.36`).

Checked on 2026-11-15 by driving the player with scripted keys as host and reading six frames of each move: walk, sprint, strafe left and right, backward, a running jump, a 300 degrees a second spin, looking down while walking, looking up while sprinting, sprint-strafing while spinning, a melee hit, a shot, a reload and a two-handed walk. Looked at closely: sprint, backward, spin, looking down, the wild one and the melee hit. What the first pass showed and what was done:

| Seen | Cause | Fix |
|---|---|---|
| The player's own hair and face in view when sprinting, looking down and spinning | The head was never hidden: the bone lookup failed at run time (the Animator has no avatar once PlayerAnimator takes over), and the ragdoll puts every bone's size back each frame | The head bone now comes from PlayerAnimator, and is shrunk every frame after the ragdoll |
| The view filled with the inside of the suit while spinning and strafing | The eye followed only a third of the head's movement, so the body leaned up to 38 cm away from it (45 cm on a melee hit) | The eye follows the settled head in full, slowly; measured afterwards 20 cm in a sprint and 33 cm at worst, at the start of a backward run and on a melee hit |
| Red squares across the screen when hit | Drops spawned 20 cm from the eye | Drops near the eye are left out |
| A lean would have moved the eye twice | The real head already leans with the body | The camera's own sideways slide is off in the body view; the tilt stays |
| A headless reflection in the mirror, and a headless shadow | The head was hidden by shrinking its bone, which is posed once a frame for every camera | The bone is left alone; the character shaders clip what is right at the owner's eye, for the owner's camera only. Seen afterwards: no head in the owner's view standing, looking 60 degrees down, 50 up, sprinting and backing up; the shadow on the floor has its head; an outside camera sees the head |
| The arm looked worse after the head fix (Harvey: "It got worse") | That fix skipped everything of a character nearer the eye than 22 cm, a flat cut that also sliced the owner's own shoulder and sleeve and left the arm open-ended. The arm's placement itself had not changed | The cut is now a ball around the middle of the head, which stops short of the shoulders. Seen afterwards standing, looking 25 and 60 degrees down and 50 up, sprinting, backing up and strafing: the sleeve is whole and no head or hair shows |
| The gun slid down and out of the bottom of the view when walking or backing up (Harvey: "The arm definitely needs to come out more") | The body's gun was placed from the steady eye point, but the eye in this view rides on the real head, which leans away from that point | For the owner in the body view the gun is placed from the eye that is looking and along its look, lifted 7 cm, 4 cm toward the middle and 4 cm further out, and the eye sits 7 cm in front of the head bone instead of 10. Seen afterwards standing, sprinting, backing up, strafing, spinning, and looking 35 degrees down and 30 up: the arm and gun stay in the lower right of the frame in all of them |

Still rough, and for a person to judge: looking straight down shows the chest, shirt and tie but not the legs; a melee hit throws the sleeve across the view; nothing was judged in motion by a person or seen from a client.

## Known limitations

- **On other machines the body's gun arm follows the facing only, not up and down.** `PlayerMotor.AimDirection` is built from yaw alone since the movement rework removed `Aim` from the move input (merge of `harvey-netcode` into `buffer`, 2026-11-14). The real fix is to send look pitch with the input. The owner's own first-person view is unaffected.

- **The body's gun arm has been seen in stills and measured, not watched moving, so how the flipping reads is unjudged.** Seen in play as host on 2026-11-14 from the side and the front, aiming level, 35 degrees up and 40 down, and the owner's own view was checked for a second gun (there is none); strafing and a jump were measured, not watched. Leaning, a knock-back and **another client's view of it** are unchecked; the aim other clients see comes from forwarded input and has not been watched.
- **The body's gun is further out than the view's, and moves a little off the aim when moving.** Standing, measured in play as host: the hand is steady to about 2 cm, about 6 cm further forward than the view's point (the straighter arm), and the muzzle is 3 degrees off the aim, which is the gun's own resting turn. Moving, the hand is held within 4 cm sideways, 7 cm up or down and 6 cm forward or back of the aimed spot and the muzzle within 12 degrees; measured in play as host with the elbow pinned and the hand anchored to the eye, a hard left-right strafe moved the hand 9 cm at most and a jump 8 cm, with the muzzle within 15 degrees. The elbow still moves about 13 cm in a strafe, because the shoulder it hangs from sways with the torso; its direction is fixed. Before the hand was anchored to the eye it was measured from the ragdoll's shoulder and was dragged 21 cm about whatever the limits were. They exist because nothing checks the arm's final pose against the level, so a wide swing puts the gun through walls. What was tried and rejected by Harvey: fully pinned (the wrist hung dead still while the elbow flopped round it), partly pinned (sagged 6 cm, 13 degrees off at rest), and a swing of 1.5 capped at 30 cm and 50 degrees, then 0.6 at 15 cm and 25 degrees (both swung too far out).
- **The body's gun arm is added at spawn, not on Player.prefab.** The prefab has been edited by hand, so the greybox tool leaves it alone, and it is claimed on the workboard. If the prefab is ever rebuilt by the tool, this can move onto it.
- **Other players' views of the body show no kick, reload, sway or bob** (only the owner's body view carries kick, reload, sprint, climb, and look lag), and fidgets that use the right arm are overridden while the gun is held.
- **The motion's feel is not judged by a person.** The third pass was seen in Play Mode as host in still frames (standing, mid-jump, landed). Sprint carry, strafe tilt, the lean, the climb drop, the kick and the reload were not looked at in this pass, and nothing was seen from a joining client (the arm is local-only, so it should not differ).
- **What made the first pass "glitchy" was not pinned down** beyond the reversed posing described above. One real cause of roughness was found and fixed in the third pass: the step bob had a hard corner at the top of every step.
- **Crouch and slide have no gun motion.** They are on Adam's movement lane's branch, not here; when they merge, the gun will still trail the camera's drop but has no pose of its own for them.
- **The arm is at 99% of its reach** (0.428 of 0.433 m), which is what makes it straight. The shoulder follows all of the movement drift so the grip stays in the hand; the climb drop can still stretch it a few millimetres past reach. Moving the gun further forward in the profile needs the shoulder moved forward with it.
- **Temporary stand-in input.** Until weapon code sets `Driven`, `PlayerViewmodel.ReadTestInput` listens to the attack button (which also sends a stand-in bullet at the test dummy or, through the host, at another player via `PlayerHits.CheckStandInHit`), V (a stand-in melee swing with a 2 m reach, see [HIT_REACTIONS.md](HIT_REACTIONS.md)), the T hand switch and reload (the `Player/Reload` action where the actions asset has one, otherwise the R key). Compiled only in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` so Release builds cannot execute the stand-in hit RPC. It reads the project's actions asset, not a player's rebound copy, so rebound keys are not followed. Real fix: the weapon code calls `Fire()` and `Reload()`, sets `Driven`, and that method is deleted.
- **Adding the viewmodel to a player prefab that lacks it is untested.** `Rebuild Viewmodel`'s add step was only run where the component was already there (it left the prefab byte-identical).
- **The finger curl is one fixed grip**, set by eye for this pistol. `Equip` moves the hand seats per weapon but not how far the fingers close.
- **Swapping the held model is proved only as a mechanism.** In play as host on 2026-11-14 a stand-in rifle (a Kenney blaster with guessed seats, far too big) was swapped in and the pistol swapped back: both the view and the body changed, the left hand came up and went away, and the console stayed clean. No real second weapon has been fitted, there is no put-away or draw move (the model changes in one frame), and nothing calls `Equip` yet.
- **Only the right hand can be the main hand.** There is no left-handed one-handed hold and nothing held in the left hand alone; the left hand only supports.
- **Two-handed holding is only seen on the stand-in pistol**, in Editor renders and in play as host, standing still (the view and the body from the front and side). There is no two-handed weapon model, the hold has not been seen moving, and another client cannot see the T switch.
- **Picking up and throwing props is not built here.** Adam's pickups lane reports carry and throw on a branch that is not on origin; the hands above are the posing layer a carried prop would use (a main seat and a support seat on the prop).
- **The thumb does not cross to the left of the grip.** It lies along the right side under the slide; the curl can only close it about one axis. From the eye it reads as a relaxed thumb.
- **The hand mesh has thin black slivers at the cuff and between fingers** in close side views (outline hull on sharply bent joints). Not visible from the eye at the current framing.
- **The reload is a gun move, not a hand animation**: no magazine leaves the gun and the left hand never appears.
- **The pistol uses the materials Unity made from its FBX** (plain lit, not the toon shader), so it has no outline.
- **Layer 8 is now `Viewmodel`.** A branch that names layer 8 something else will clash on `ProjectSettings/TagManager.asset`.
- **Cost is unmeasured.** True POV uses no extra camera pass; runtime cost is the two-pass arm solve (`PlayerHeldWeapon` + `HeldWeaponSettle`) and head-clip shader check.
- **The cut sleeve is open at the shoulder.** Not visible from the eye at the default framing; a shoulder position further forward would show it.
