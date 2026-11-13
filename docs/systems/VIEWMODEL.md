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

**Status:** Current (slice 1 of the weapon work, second pass: the gun is placed in the view the NorseFPS way and the arm reaches to it. Compiles; checked in still frames rendered in the Editor from the eye and from both sides. **This pass has not been seen in Play Mode or judged by a person**; the first pass was played by Alex and rejected as glitchy) · **Last updated:** 2026-11-13

In first person you see your own character's right arm, in the same suit and the same toon shading, holding a pistol one-handed to the right of the crosshair.

- **Standing:** the gun sits still, lower right of centre, muzzle turned slightly in and up. The fist is closed round the grip with the index finger under the trigger guard.
- **Turning:** the gun trails slightly behind the view and catches up.
- **Running:** the gun bobs with each step; the bob fades out in the air and when you stop.
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

1. `PlayerViewmodel` decides where the gun is this frame: the framing from the profile, plus look lag, walk bob, fire kick and the reload dip.
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
| Shoulder position | Where the arm comes from. Lower and further back shows less upper arm. |
| Look lag seconds / limit / settle | How far the gun trails a turn, the most it may trail, and how quickly it catches up. |
| Bob height / side / steps per metre / fade | The walk bob. |
| Kick pitch / back / roll / recover | The shot kick. It moves the gun only, never the aim. |
| Reload seconds / drop / tilt / roll | The reload move. |

On the arms prefab (`ViewmodelArms`, **Grip** header): how far the gripping fingers, the trigger finger and the thumb close, in degrees per joint. The hand's seat on the gun is the `GripHand` object under `GunMount`: its blue axis is where the fingers point, its green axis runs from little finger to index finger.

Where the starting numbers came from: NorseFPS frames its hand cannon at (0.18, -0.12, 0.70) m. This character's arm is 0.43 m long, so the gun is pulled in to 0.32 m along the same line from the eye; it lands on the same part of the screen. Bob and look-lag sizes are NorseFPS's; the kick is about 60% of its hand cannon's.

## Driving it from weapon code

```csharp
// On the owning client, when a shot has been fired / a reload has started:
viewmodel.Fire();
viewmodel.Reload();
```

Both do nothing on players the client does not control.

## Known limitations

- **This pass is not seen in Play Mode and not judged by a person.** It was checked by posing the prefab in the Editor (rest, full kick, deepest reload) and rendering it from the eye, the left and the right: the wrist is on its target, the fist is round the grip, the left arm is gone. Sway, bob, the kick's timing and the camera stack have only been compiled.
- **What made the first pass "glitchy" was not pinned down** beyond the reversed posing described above. If this pass still glitches in Play Mode, that is new information; say what it looks like.
- **Temporary stand-in input.** `PlayerViewmodel.ReadTestInput` listens to the attack button and reads the R key straight from the keyboard, because nothing fires or reloads yet and the actions asset has no reload action. Real fix: the weapon code of slice 2 calls `Fire()` and `Reload()` and that method is deleted.
- **The finger pose is one fixed grip**, set by eye for this pistol. Another weapon or a loose prop needs its own `GripHand` and curl values; that arrives with swapping the held item (slice 3).
- **From the eye the hand is mostly hidden** behind the gun and the jacket cuff; the framing shows the back of the gun and the sleeve.
- **The reload is a gun move, not a hand animation**: no magazine leaves the gun and the left hand never appears.
- **The pistol uses the materials Unity made from its FBX** (plain lit, not the toon shader), so it has no outline.
- **Layer 8 is now `Viewmodel`.** A branch that names layer 8 something else will clash on `ProjectSettings/TagManager.asset`.
- **Cost is unmeasured.** One extra camera pass over three small skinned meshes and the pistol, for the local player only.
- **The cut sleeve is open at the shoulder.** Not visible from the eye at the default framing; a shoulder position further forward would show it.
