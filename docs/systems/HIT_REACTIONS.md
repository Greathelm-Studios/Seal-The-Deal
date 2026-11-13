<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-13
  Prompt    : Alex: "a test dummy in the scene to test animations of being hit by certain powers
              of weapons (bullets bombs explosions props, etc) as we want to add a ragdoll system
              when explosion systems or heavy or big props get thrown"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for how a body reacts to hits (AGENTS.md §7): the test dummy, the
              hit bench, the flinch, and the knock-down that is still to be built.
-->
# Hit reactions

**Status:** Current for the dummy, the bench, the flinch and the dummy's knock-down (a dev aid, local only; its knock-down rules are placeholders); **Target** for anything on real players · **Last updated:** 2026-11-13

A test dummy stands in the level wearing the player's character, in the player's idle (breathing and fidgets included).
From an Editor window you hit it with a chosen kind of hit, at a chosen strength, body part and direction, and watch how the body takes it.

- **Light hits flinch:** the struck part and the parts joined to it are knocked away and their joints go slack for a fraction of a second, more for a harder hit, then the body pulls itself back to its pose.
- **Blasts push the whole body** away from the blast's centre, the near side harder, so it is turned as well as pushed.
- **Hard hits knock it down:** the body goes limp and is thrown, falls, lies on the floor as a real ragdoll, then is drawn back up onto its feet where it landed and settles into the idle. With the placeholder rules a thrown desk and any blast do this; a thrown chair and a pistol shot only flinch.

The body it runs on is the active ragdoll described in [CHARACTER_ANIMATION.md](CHARACTER_ANIMATION.md).

## Using it

1. `Close the Deal > Dev > Place Hit Dummy` puts one on the floor 3 m in front of the Scene view (or in front of your camera while playing), facing you. The scene is not saved.
2. Press Play.
3. `Close the Deal > Dev > Hit Bench`: pick a preset (pistol shot, thrown chair, thrown desk, blast near, blast far) or set the values, then press **Hit**.

`Close the Deal > Dev > Build Hit Dummy Prefab` rebuilds the prefab; like the greybox prefabs it is left alone once a person has edited it.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| The dummy and its reactions | Each client, for its own screen | Nothing | — |

The dummy has no network identity. In a networked game it reacts only on the screen of whoever hit it. It is a development aid, not a gameplay object.

## The hit

One description, [HitInfo.cs](../../Assets/_Project/Scripts/Player/HitInfo.cs), used by the bench now and meant for real weapons later:

| Field | Meaning |
|---|---|
| Point | Where it struck, world space. For a blast, the blast's centre. |
| Direction | The way the hit travels, unit length. |
| Impulse | How hard, in newton-seconds **delivered to this body** (the weapon applies its own falloff first). |
| Radius | 0 for a point hit. Above 0 it is a blast: every part within that distance of Point is pushed. |
| Type | Bullet, ThrownProp or Blast. |

Alex's weapons session has agreed the shape (2026-11-13); a host-side hit event will carry the shooter, the victim and the weapon beside this struct, not inside it.
The file stays under `Scripts/Player/` because `Scripts/Combat/` is claimed by Adam's shooting lane, which may have a hit or damage type of its own and **has not answered yet**.
A thrown prop's impulse is its mass times its speed at impact, not a per-prop table.

**Why the caller states the impulse** instead of a bullet or a chair physically striking the ragdoll: the ragdoll lives in a physics scene of its own, where bullets and thrown props do not exist.

## Files

- [HitDummy.cs](../../Assets/_Project/Scripts/Player/HitDummy.cs): on the dummy's root beside its capsule; `Hit(in HitInfo)` is the one place to hit it.
- [PlayerRagdoll.cs](../../Assets/_Project/Scripts/Player/PlayerRagdoll.cs): `Hit(in HitInfo)` does the reaction. It needs a hit profile; the player's own ragdoll has none assigned, so hits do nothing to players today.
- [HitReactionProfile.cs](../../Assets/_Project/Scripts/Player/HitReactionProfile.cs) and `Assets/_Project/Profiles/DefaultHitReaction.asset`: the tuning panel.
- [HitDummy.prefab](../../Assets/_Project/Prefabs/Dev/HitDummy.prefab): the character, `PlayerAnimator` with no motor (so it stands), `PlayerRagdoll`, and a player-sized capsule on the Player layer.
- [HitDummySetup.cs](../../Assets/_Project/Editor/Dev/HitDummySetup.cs) and [HitBenchWindow.cs](../../Assets/_Project/Editor/Dev/HitBenchWindow.cs): the menu items.

## Tuning

All in `DefaultHitReaction.asset`. Every default is a first guess from frame strips, not from play.

| Inspector value | What it does |
|---|---|
| Impulse share | How much of a hit's push the body takes. Lower = every hit moves it less. |
| Neighbour share | How much of the push the parts joined to the struck one also take. 0 = only the struck part jerks. |
| Max kick speed | The fastest one hit may throw a single part, m/s. Stops a heavy hit firing a forearm across the room. |
| Flinch full impulse | A hit this hard (N·s) makes the struck joint fully slack. Lower = small hits crumple the body more. |
| Flinch limp share | How strong a fully slack joint still is. 0 = hangs loose. |
| Flinch recover | Seconds for a slack joint to get its strength back. |
| Knock down impulse | A hit at least this hard (N·s) floors the body. 0 = never. **Placeholder rule.** |
| Bullets knock down | Off = shots only ever flinch. **Placeholder rule.** |
| Down limp share | How strong the joints still are while down. 0 = a limp rag. |
| Down friction | How much the floor grips a fallen body. Lower = longer slides. |
| Down min time / Down max time | The shortest and longest it lies there, seconds. **Placeholder rule.** |
| Settle speed | It gets up once its hips move slower than this, m/s. |
| Get up time | Seconds to be drawn back onto its feet. |

The bench's preset strengths are placeholders worked out from weight times speed (pistol 20 N·s, chair 80, desk 320, blast 235 to 590) until real weapons report their own.

## How the knock-down works (dummy only)

`PlayerRagdoll` normally holds its hips up on a spring. A knock-down lets that spring go and drops every joint to near-limp, so the eleven bodies fall freely in the ragdoll's own physics scene. While down, the parts use continuous collision and a grippier surface, and the level copies around the body are refreshed every frame, because a thrown body outruns the usual refresh.
Once the hips have come to rest (or the longest time is up) the spring and the joints return to full strength over the get-up time, which hoists the body into its pose.
The dummy's own root follows the fallen body ([HitDummy.cs](../../Assets/_Project/Scripts/Player/HitDummy.cs)), so it rises where it landed. A player's ragdoll must never do that: the capsule decides where a player is.

## Target: knock-down on real players (not built)

The GDD says players ragdoll "on explosions and heavy hits, then recover quickly. It should be funny, not a long stun."
The rest is undecided and is **Adam's to settle**:

1. Which hits only flinch and which knock the body down.
2. How long a knocked-down body stays down, and how it gets up.
3. Whether a knocked-down player keeps any control (looking, shooting), and whether they can be hit again while down.

Until then the knock-down exists on the dummy only, with those rules as tunable placeholder values on the hit profile.
Wiring it to real players changes netcode authority (the host decides the trigger and impulse, every client simulates its own ragdoll, bones are never streamed) and needs its ownership table written here and agreed before any code touches `PlayerMotor`.

## Known limitations

- **Not judged in play.** Checked in frame strips of a hidden copy of the dummy; nobody has yet tried the bench in Play Mode.
- **The get-up has no animation.** The body is hoisted by its hips in about a quarter of a second, arms flung, rather than climbing to its feet. A get-up clip is the natural next step.
- **A near blast throws the dummy about 7 m** in the preview (Max kick speed 8 m/s, low ragdoll gravity). Lower Max kick speed for shorter flights.
- **A fallen body tumbles oddly**, legs over head, because its own parts do not collide with each other and the pelvis is made deliberately hard to turn.
- **Falling off a ledge is untested**, as is landing on stairs or furniture. The previews use a flat floor.
- **Hits do nothing to players.** Deliberate until the knock-down design is agreed.
- **A hit lands on the part nearest the point**, so a point far from the body still hits something; the caller is trusted to have struck the body.
- **The bench's body-part positions are estimates** for the standing idle; mid-fidget an arm may not be where the bench aims.
- **Thrown props never touch the ragdoll physically** (moving colliders are not copied into the ragdoll scene), so a prop hit only exists if something reports it as a `HitInfo`.
