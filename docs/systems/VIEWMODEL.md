<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-13
  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
  Modified  : Claude Fable 5.2, 2026-11-13 — second pass. Alex found the first version
              "glitchy" and asked to "steal the viewmodel framing and posing from the
              norsefps project", then that the hand "doesnt wrap around the gun well".
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the first-person arm and held weapon (AGENTS.md §7): what
              the player sees, who owns it, where the files are, how to tune it, what is
              unchecked.
-->
# Viewmodel (first-person arm)

**Status:** Current (slice 1 of the weapon work, third pass: the gun is carried low and canted in a loose hold, the hand wraps the grip, and the gun moves with the player's own movement. The hold and carry were checked on 2026-11-14 in Editor renders of the prefab from the eye, above, both sides and behind, not in a play session. The movement was seen running in the Editor as host on 2026-11-14 in still frames only: standing, mid-jump and after landing, with the hand staying on the grip. **How the motion feels has not been judged by a person**, and sprint, strafe, lean and climb were not looked at) · **Last updated:** 2026-11-14

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
| The arm, the held pistol, sway, bob, kick and reload move | Each client, for the player it controls | Nothing | — |
| The body's gun arm and the pistol in its hand | Each client, for every player it shows | Nothing new: it reads the aim direction already inside each tick's input | — |

Nothing reads either arm back, so it cannot desync and costs no bandwidth. The body's arm points along `PlayerMotor.AimDirection`, which every machine fills from the input each tick runs on (the owner's own, the one the host receives, the one other clients are forwarded). Firing, ammunition, pickups and what other players see in the hand are separate, host-owned work (slices 2 to 4, and Adam's shooting and pickups lanes).

## How it is posed

The approach is the NorseFPS project's: **the gun is placed, and the arm follows it.**

1. `PlayerViewmodel` decides where the gun is this frame: the framing from the profile, plus look lag, movement weight, walk bob, sprint carry, climb drop, breathing, fire kick and the reload dip. Movement weight is measured from how the camera itself moved since the last frame, fed through a spring, so it needs nothing from the movement code beyond the state it already publishes. It runs after `PlayerCamera` each frame so it reads this frame's view.
2. `ViewmodelArms` then slides the skeleton so the right shoulder sits on a fixed point below and right of the eye, bends the elbow so the wrist lands on the `GripHand` target on the gun (a two-bone solve ported from NorseFPS), turns the hand to that target, and closes each finger round the grip from an open hand.
3. Held in one hand, the left arm is shrunk to nothing. Held in both, it is solved the same way onto a second seat on the gun, `GripSupport`.

**Hands.** Each arm is a `HandGrip` with a side (left or right); a left hand is the right hand's solve mirrored. What is held says where each hand sits: a main seat (`GripHand`, the right hand) and an optional support seat (`GripSupport`, the left). A weapon with no support seat is one-handed. `PlayerViewmodel.TwoHanded` says which hold is in use; weapon code sets it when the held weapon changes. It is local to each machine: nothing sends it, and it belongs with whatever syncs which weapon a player holds.

**The body follows the view.** `PlayerViewmodel` gives every player's body a `PlayerHeldWeapon` at spawn. It reads the weapon model, the hand's seat on it, the finger curls and the elbow swing from the first-person arms prefab, and the gun's place in front of the eye from the same profile, so there is one set of numbers: retune the view and the body follows. Each frame it bends the body's right arm onto that grip twice: once before the ragdoll, exactly, so the ragdoll's arm is trying to hold the gun out; and once after it, reading how far the ragdoll left the hand and the gun from that aimed pose. The part of that gap that stays put (the arm's sag under its own weight) is taken out, so at rest the arm is straight and the gun is on the aim; the part that changes (the lag, swing and bounce from moving) is kept and multiplied (**Body arm swing**). The body's gun is also held a fixed 6 cm further out along the aim than the view's (**Body gun forward**), because the body's real shoulder is nearer the gun than the one the view pretends to have and the elbow would otherwise hang bent. The elbow itself is pinned: both passes hang it down and a little out relative to the aim, so the arm keeps one shape while the torso sways and only the hand moves, by the small limited swing. The bending is `HandGrip`, the same code the first-person arm uses.

So the hand cannot drift off the gun, whatever moves it. The first pass did the reverse (played a full-body pistol clip and hung the gun off the hand bone); seen from the eye that clip's arms filled the screen and its loose hand did not fit this gun.

## How it is drawn

The arm is a second camera stacked on the player's camera. It draws only the `Viewmodel` layer (layer 8), after the world, with its own depth, so the world cannot cut into it; it also has its own field of view and a 1 cm near distance (the world camera's is 30 cm).
Just before any camera renders, the arm is switched off unless that camera is its own. That keeps it out of the dev mirror, the Scene view and any later spectator camera without touching those cameras' layer lists.
The camera and the arm exist only while that player is the local one. The player's camera gets one entry added to its camera stack at spawn and removed afterwards.

## Files

- [PlayerViewmodel.cs](../../Assets/_Project/Scripts/Player/Viewmodel/PlayerViewmodel.cs): on the root of [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab). Creates the arm and its camera for the local player and places the gun each frame. Weapon code calls its `Equip()`, `Fire()` and `Reload()`.
- [ViewmodelArms.cs](../../Assets/_Project/Scripts/Player/Viewmodel/ViewmodelArms.cs): on the arms prefab. Shoulder pin and the left arm fold; the arm itself is a `HandGrip`.
- [HandGrip.cs](../../Assets/_Project/Scripts/Player/Viewmodel/HandGrip.cs): one right arm holding one grip: the two-bone solve, the hand turn and the finger closing, with the elbow swing and curl values. Used by the first-person arm and by the body.
- [WeaponHold.cs](../../Assets/_Project/Scripts/Player/Viewmodel/WeaponHold.cs): what the hands hold: a model and where each hand sits on it. Weapon code fills one in per weapon.
- [PlayerHeldWeapon.cs](../../Assets/_Project/Scripts/Player/Viewmodel/PlayerHeldWeapon.cs) and [HeldWeaponSettle.cs](../../Assets/_Project/Scripts/Player/Viewmodel/HeldWeaponSettle.cs): the body's gun arm, added to the player's `Graphics` object at spawn (not on the prefab). The second file is its after-the-ragdoll pass.
- [ViewmodelProfile.cs](../../Assets/_Project/Scripts/Player/Viewmodel/ViewmodelProfile.cs) and [DefaultViewmodel.asset](../../Assets/_Project/Profiles/DefaultViewmodel.asset): the tuning panel. Runtime never writes it.
- [ViewmodelArms.prefab](../../Assets/_Project/Prefabs/Viewmodel/ViewmodelArms.prefab): its origin is the eye. `GunMount` holds `Pistol` and `GripHand`; `Shoulder` is the shoulder point; `Arms` is the character's skeleton with arm geometry only. Generated; edit it and the tool leaves it alone from then on (§4).
- [GreyboxViewmodelSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxViewmodelSetup.cs): `Close the Deal > Greybox > Rebuild Viewmodel` re-cuts the arm meshes from [STDMC_Toon.prefab](../../Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab), and rewrites the arms prefab. If [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab) has no `PlayerViewmodel` it asks, then adds that one component and changes nothing else; it never rebuilds the player prefab (it did until 2026-11-14, which on a prefab carrying other people's parts would have stripped them). Run it after the character model changes.
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

## Body view (experiment)

Press **F5** while playing (Editor and development builds) to switch between the usual view and the body view. The choice is remembered on that machine.
In the body view the separate first-person arms are not drawn. The player looks out of their real body instead: the same arms, gun and legs everyone else sees, including the flinch when hit.
It was asked for by Harvey on 2026-11-15 as something to try ("true POV"), and nothing has been decided.

What keeps it comfortable: the view's direction still comes from the mouse alone, never from the head. The eye's position goes all the way to where the real head has settled (so the eye is never left behind or inside the body), but takes about a tenth of a second to get there; of the quick movement on top, the bob of each step and the jolt of a hit, it takes only a capped share.
The head itself is shrunk to almost nothing on the owner's screen ([BodyViewHead.cs](../../Assets/_Project/Scripts/Player/Viewmodel/BodyViewHead.cs)), because the character's meshes are split by material and there is no head mesh to hide.
Drops of blood that would start within half a metre of the eye are left out, so being hit does not fill the screen with red squares.

Tuning, on the viewmodel profile under "Body view": **Head follow** (the share of quick head movement the eye takes; 0 = a gliding eye), **Smoothing** (seconds to settle onto where the head has moved to), **Forward** (how far in front of the head bone the eye sits) and **Max offset** (the most quick movement the eye may take), and **Gun shift** (where the gun is held, measured from the usual view's framing: right, up, further out).

Checked on 2026-11-15 by driving the player with scripted keys as host and reading six frames of each move: walk, sprint, strafe left and right, backward, a running jump, a 300 degrees a second spin, looking down while walking, looking up while sprinting, sprint-strafing while spinning, a melee hit, a shot, a reload and a two-handed walk. Looked at closely: sprint, backward, spin, looking down, the wild one and the melee hit. What the first pass showed and what was done:

| Seen | Cause | Fix |
|---|---|---|
| The player's own hair and face in view when sprinting, looking down and spinning | The head was never hidden: the bone lookup failed at run time (the Animator has no avatar once PlayerAnimator takes over), and the ragdoll puts every bone's size back each frame | The head bone now comes from PlayerAnimator, and is shrunk every frame after the ragdoll |
| The view filled with the inside of the suit while spinning and strafing | The eye followed only a third of the head's movement, so the body leaned up to 38 cm away from it (45 cm on a melee hit) | The eye follows the settled head in full, slowly; measured afterwards 20 cm in a sprint and 33 cm at worst, at the start of a backward run and on a melee hit |
| Red squares across the screen when hit | Drops spawned 20 cm from the eye | Drops near the eye are left out |
| A lean would have moved the eye twice | The real head already leans with the body | The camera's own sideways slide is off in the body view; the tilt stays |
| The gun slid down and out of the bottom of the view when walking or backing up (Harvey: "The arm definitely needs to come out more") | The body's gun was placed from the steady eye point, but the eye in this view rides on the real head, which leans away from that point | For the owner in the body view the gun is placed from the eye that is looking and along its look, lifted 7 cm, 4 cm toward the middle and 4 cm further out, and the eye sits 7 cm in front of the head bone instead of 10. Seen afterwards standing, sprinting, backing up, strafing, spinning, and looking 35 degrees down and 30 up: the arm and gun stay in the lower right of the frame in all of them |

Still rough, and for a person to judge: looking straight down shows the chest, shirt and tie but not the legs; a melee hit throws the sleeve across the view; the owner's own shadow has no head; the body's gun has no kick, reload move or sway; nothing was judged in motion by a person or seen from a client.

## Known limitations

- **The body's gun arm has been seen in stills and measured, not watched moving, so how the flipping reads is unjudged.** Seen in play as host on 2026-11-14 from the side and the front, aiming level, 35 degrees up and 40 down, and the owner's own view was checked for a second gun (there is none); strafing and a jump were measured, not watched. Leaning, a knock-back and **another client's view of it** are unchecked; the aim other clients see comes from forwarded input and has not been watched.
- **The body's gun is further out than the view's, and moves a little off the aim when moving.** Standing, measured in play as host: the hand is steady to about 2 cm, about 6 cm further forward than the view's point (the straighter arm), and the muzzle is 3 degrees off the aim, which is the gun's own resting turn. Moving, the hand is held within 4 cm sideways, 7 cm up or down and 6 cm forward or back of the aimed spot and the muzzle within 12 degrees; measured in play as host with the elbow pinned and the hand anchored to the eye, a hard left-right strafe moved the hand 9 cm at most and a jump 8 cm, with the muzzle within 15 degrees. The elbow still moves about 13 cm in a strafe, because the shoulder it hangs from sways with the torso; its direction is fixed. Before the hand was anchored to the eye it was measured from the ragdoll's shoulder and was dragged 21 cm about whatever the limits were. They exist because nothing checks the arm's final pose against the level, so a wide swing puts the gun through walls. What was tried and rejected by Harvey: fully pinned (the wrist hung dead still while the elbow flopped round it), partly pinned (sagged 6 cm, 13 degrees off at rest), and a swing of 1.5 capped at 30 cm and 50 degrees, then 0.6 at 15 cm and 25 degrees (both swung too far out).
- **The body's gun arm is added at spawn, not on Player.prefab.** The prefab has been edited by hand, so the greybox tool leaves it alone, and it is claimed on the workboard. If the prefab is ever rebuilt by the tool, this can move onto it.
- **The body shows no kick, reload, sway or bob**, and fidgets that use the right arm are overridden while the gun is held.
- **The motion's feel is not judged by a person.** The third pass was seen in Play Mode as host in still frames (standing, mid-jump, landed). Sprint carry, strafe tilt, the lean, the climb drop, the kick and the reload were not looked at in this pass, and nothing was seen from a joining client (the arm is local-only, so it should not differ).
- **What made the first pass "glitchy" was not pinned down** beyond the reversed posing described above. One real cause of roughness was found and fixed in the third pass: the step bob had a hard corner at the top of every step.
- **Crouch and slide have no gun motion.** They are on Adam's movement lane's branch, not here; when they merge, the gun will still trail the camera's drop but has no pose of its own for them.
- **The arm is at 99% of its reach** (0.428 of 0.433 m), which is what makes it straight. The shoulder follows all of the movement drift so the grip stays in the hand; the climb drop can still stretch it a few millimetres past reach. Moving the gun further forward in the profile needs the shoulder moved forward with it.
- **Temporary stand-in input.** Until weapon code sets `Driven`, `PlayerViewmodel.ReadTestInput` listens to the attack button (which also sends a stand-in bullet at the test dummy or, through the host, at another player), V (a stand-in melee swing with a 2 m reach, see [HIT_REACTIONS.md](HIT_REACTIONS.md)), the T hand switch and reload (the `Player/Reload` action where the actions asset has one, otherwise the R key). It reads the project's actions asset, not a player's rebound copy, so rebound keys are not followed. Real fix: the weapon code calls `Fire()` and `Reload()`, sets `Driven`, and that method is deleted.
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
- **Cost is unmeasured.** One extra camera pass over three small skinned meshes and the pistol, for the local player only.
- **The cut sleeve is open at the shoulder.** Not visible from the eye at the default framing; a shoulder position further forward would show it.
