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

- **Standing:** the gun is held low in the bottom right of the view, well clear of the crosshair, tipped about 16 degrees onto its side in a loose, lazy hold, and rises and falls very slightly as you breathe. The forearm comes in nearly straight behind the gun, with the wrist bent about 12 degrees. The palm lies against the right of the grip, the fingers close round the front onto its left side and the index finger sits in the trigger guard.
- **Turning:** the gun trails slightly behind the view, tips into the turn and catches up.
- **Setting off, stopping and strafing:** the gun hangs back from the direction you move and tips with a strafe, then swings a little past rest when you stop.
- **Running:** the gun sways in a smooth arc with each step; the sway fades out in the air and when you stop. Above walking speed it is carried a little lower and turned in.
- **Jumping and landing:** the gun sinks as you go up, lifts as you fall and dips on landing, deeper after a longer fall.
- **Leaning:** the gun trails the slide of the lean and stays a little more upright than the tilted view.
- **Climbing a ledge:** the gun drops out of the way and comes back up when you are over.
- **Attack button:** the muzzle jumps up, the gun punches back and twists a little, and the hand goes with it. This is only the look: the pistol does not shoot anything yet (the attack button also still fires the old test blast).
- **R:** the gun drops, tips muzzle-up and rolls onto its side, then comes back. There is no ammunition yet.
- **Walls:** the arm is never cut by a wall, however close you stand.
- **Other players, and the dev mirror:** see your full body exactly as before, and never the first-person arm. The body does not hold the pistol yet.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| The arm, the held pistol, sway, bob, kick and reload move | Each client, for the player it controls | Nothing | — |

Nothing reads the arm back, so it cannot desync and costs no bandwidth. Firing, ammunition, pickups and what other players see in the hand are separate, host-owned work (slices 2 to 4, and Adam's shooting and pickups lanes).

## How it is posed

The approach is the NorseFPS project's: **the gun is placed, and the arm follows it.**

1. `PlayerViewmodel` decides where the gun is this frame: the framing from the profile, plus look lag, movement weight, walk bob, sprint carry, climb drop, breathing, fire kick and the reload dip. Movement weight is measured from how the camera itself moved since the last frame, fed through a spring, so it needs nothing from the movement code beyond the state it already publishes. It runs after `PlayerCamera` each frame so it reads this frame's view.
2. `ViewmodelArms` then slides the skeleton so the right shoulder sits on a fixed point below and right of the eye, bends the elbow so the wrist lands on the `GripHand` target on the gun (a two-bone solve ported from NorseFPS), turns the hand to that target, and closes each finger round the grip from an open hand.
3. The left arm is shrunk to nothing; gunplay is one-handed.

So the hand cannot drift off the gun, whatever moves it. The first pass did the reverse (played a full-body pistol clip and hung the gun off the hand bone); seen from the eye that clip's arms filled the screen and its loose hand did not fit this gun.

## How it is drawn

The arm is a second camera stacked on the player's camera. It draws only the `Viewmodel` layer (layer 8), after the world, with its own depth, so the world cannot cut into it; it also has its own field of view and a 1 cm near distance (the world camera's is 30 cm).
Just before any camera renders, the arm is switched off unless that camera is its own. That keeps it out of the dev mirror, the Scene view and any later spectator camera without touching those cameras' layer lists.
The camera and the arm exist only while that player is the local one. The player's camera gets one entry added to its camera stack at spawn and removed afterwards.

## Files

- [PlayerViewmodel.cs](../../Assets/_Project/Scripts/Player/Viewmodel/PlayerViewmodel.cs): on the root of [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab). Creates the arm and its camera for the local player and places the gun each frame. Weapon code calls its `Fire()` and `Reload()`.
- [ViewmodelArms.cs](../../Assets/_Project/Scripts/Player/Viewmodel/ViewmodelArms.cs): on the arms prefab. Shoulder pin, arm solve, hand turn, finger closing.
- [ViewmodelProfile.cs](../../Assets/_Project/Scripts/Player/Viewmodel/ViewmodelProfile.cs) and [DefaultViewmodel.asset](../../Assets/_Project/Profiles/DefaultViewmodel.asset): the tuning panel. Runtime never writes it.
- [ViewmodelArms.prefab](../../Assets/_Project/Prefabs/Viewmodel/ViewmodelArms.prefab): its origin is the eye. `GunMount` holds `Pistol` and `GripHand`; `Shoulder` is the shoulder point; `Arms` is the character's skeleton with arm geometry only. Generated; edit it and the tool leaves it alone from then on (§4).
- [GreyboxViewmodelSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxViewmodelSetup.cs): `Close the Deal > Greybox > Rebuild Viewmodel` re-cuts the arm meshes from [STDMC_Toon.prefab](../../Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab), rewrites the arms prefab and rebuilds the player prefab if the tool still owns it. Unlike `Rebuild Player Prefab` it does not open the Greybox scene. Run it after the character model changes.
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

Where the starting numbers came from: NorseFPS frames its hand cannon at (0.18, -0.12, 0.70) m. This character's arm is 0.43 m long, so the gun was first pulled in to 0.32 m along the same line from the eye. In play that put the gun at mid-height beside the crosshair with a wide slab of sleeve under it, so on 2026-11-14 (Harvey's request, "arm lower", "a lazy-like grip") the gun moved to (0.125, -0.135, 0.33), turned (-1, -8, 16), with the shoulder at (0.19, -0.20, -0.13); before that they were (0.082, -0.055, 0.32), (-4, -9, 0) and (0.17, -0.20, -0.08). The same day the hand was reseated: `GripHand` went from (0.024, 0.036, -0.05) pointing straight along the gun to (0.03, 0.005, -0.055) turned 12 degrees inward and 8 up, with tighter finger curls (grip 80/95/50, trigger 50/70/35, thumb 45/35/25), because before that the fingers hooked out in front of the grip and held nothing. The wrist was also badly bent (about 80 degrees between forearm and hand, measured): the shoulder sat far below the gun and the idle pose hangs the elbow straight down, so the forearm came up at the hand steeply. The shoulder is now level with the gun and behind it, and the arms prefab has an **Elbow swing** value (50 degrees) that swings the elbow out to the side. Bob and look-lag sizes are NorseFPS's; the kick is about 60% of its hand cannon's. The movement-weight numbers are first guesses, not play-tuned.

## Driving it from weapon code

```csharp
// On the owning client, when a shot has been fired / a reload has started:
viewmodel.Fire();
viewmodel.Reload();
```

Both do nothing on players the client does not control.

## Known limitations

- **The motion's feel is not judged by a person.** The third pass was seen in Play Mode as host in still frames (standing, mid-jump, landed). Sprint carry, strafe tilt, the lean, the climb drop, the kick and the reload were not looked at in this pass, and nothing was seen from a joining client (the arm is local-only, so it should not differ).
- **What made the first pass "glitchy" was not pinned down** beyond the reversed posing described above. One real cause of roughness was found and fixed in the third pass: the step bob had a hard corner at the top of every step.
- **Crouch and slide have no gun motion.** They are on Adam's movement lane's branch, not here; when they merge, the gun will still trail the camera's drop but has no pose of its own for them.
- **The arm is close to full reach.** At rest the wrist is about 3 cm short of the arm's 0.43 m; the shoulder follows half of the movement drift to keep the grip in the hand. Moving the gun further forward in the profile will straighten the arm.
- **Temporary stand-in input.** `PlayerViewmodel.ReadTestInput` listens to the attack button and reads the R key straight from the keyboard, because nothing fires or reloads yet and the actions asset has no reload action. Real fix: the weapon code of slice 2 calls `Fire()` and `Reload()` and that method is deleted.
- **The finger pose is one fixed grip**, set by eye for this pistol. Another weapon or a loose prop needs its own `GripHand` and curl values; that arrives with swapping the held item (slice 3).
- **The thumb does not cross to the left of the grip.** It lies along the right side under the slide; the curl can only close it about one axis. From the eye it reads as a relaxed thumb.
- **The hand mesh has thin black slivers at the cuff and between fingers** in close side views (outline hull on sharply bent joints). Not visible from the eye at the current framing.
- **The reload is a gun move, not a hand animation**: no magazine leaves the gun and the left hand never appears.
- **The pistol uses the materials Unity made from its FBX** (plain lit, not the toon shader), so it has no outline.
- **Layer 8 is now `Viewmodel`.** A branch that names layer 8 something else will clash on `ProjectSettings/TagManager.asset`.
- **Cost is unmeasured.** One extra camera pass over three small skinned meshes and the pistol, for the local player only.
- **The cut sleeve is open at the shoulder.** Not visible from the eye at the default framing; a shoulder position further forward would show it.
