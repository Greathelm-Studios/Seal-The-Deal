# Hit reactions

**Status:** Current for the dummy, the bench, the flinch (players and dummy) and the dummy's knock-down (its rules are placeholders); **Target** for knock-down on players · **Last updated:** 2026-11-21

A player who is shot or struck flinches where everyone can see it, exaggerated so it reads from across a room: a shot snaps the part it hit, a melee swing throws the whole top half back so the body folds at the waist and staggers back up.
The player keeps running and aiming throughout; only the body shown flinches.

A test dummy also stands in the level wearing the player's character, in the player's idle (breathing and fidgets included).
From an Editor window you hit it with a chosen kind of hit, at a chosen strength, body part and direction, and watch how the body takes it.

- **Light hits flinch:** the struck part and the parts joined to it are knocked away and their joints go slack for a fraction of a second, more for a harder hit, then the body pulls itself back to its pose.
- **Melee hits shove:** part of the push is thrown over the whole upper body (chest, head, both arms), those joints go slack and they take longer to recover.
- **Blasts push the whole body** away from the blast's centre, the near side harder, so it is turned as well as pushed.
- **Hard hits knock the dummy down:** the body goes limp and is thrown, falls, lies on the floor as a real ragdoll, then is drawn back up onto its feet where it landed and settles into the idle. With the placeholder rules a thrown desk and any blast do this; a thrown chair, a melee hit and a pistol shot only flinch. **Players are never knocked down** (their profile's knock-down strength is 0).

The body it runs on is the active ragdoll described in [CHARACTER_ANIMATION.md](CHARACTER_ANIMATION.md).

## Using it

1. `Close the Deal > Dev > Place Hit Dummy` puts one on the floor 3 m in front of the Scene view (or in front of your camera while playing), facing you. The scene is not saved.
2. Press Play.
3. `Close the Deal > Dev > Hit Bench`: pick a preset (pistol shot, melee hit, thrown chair, thrown desk, blast near, blast far) or set the values, then press **Hit**.
   With its Dummy slot left empty the bench hits the dummy nearest your camera, so a dummy placed in front of you is the one hit even while another stands on the roof. The window says whether the dummy can be hit right now, and under the button what the last hit did (flinched, knocked down). It also writes each hit to the Console as `[HitBench]`.

**If nothing happens:** read the bench's status line. The usual cause is the code being reloaded during Play (another change to a script while the game runs): every character's ragdoll loses its body and stops, with one `[PlayerRagdoll] Lost its body` error. Stop and press Play again. Setting `Edit > Preferences > General > Script Changes While Playing` to **Recompile After Finished Playing** stops it happening; in a shared Editor with several sessions writing code, that setting matters.

`Close the Deal > Dev > Build Hit Dummy Prefab` rebuilds the prefab; like the greybox prefabs it is left alone once a person has edited it.

**Setting up players:** run `Close the Deal > Greybox > Add Hit Reactions to Player` once (it asks first). It adds `PlayerHits` to `Player.prefab`, creates `Assets/_Project/Profiles/PlayerHitReaction.asset` with the player tuning if it is missing, and gives the player's ragdoll that profile if it has none. Nothing else in the prefab changes. `Rebuild Player Prefab` adds the same.

## Shooting, swinging and throwing

- **Shooting:** the pistol's left click sends a stand-in bullet (20 N·s) down the crosshair. A dummy it strikes flinches where it was hit, on your screen. Another player it strikes is reported to the host, which checks the shot and makes that player flinch on every screen. That code is the weapons work's `PlayerViewmodel`, not this system's. The old greybox test blast still goes off on the same click and throws anyone within 3 m of where you aim, you included.
- **Melee (stand-in):** V swings at whatever is within 2 m of your eye along the crosshair, at 60 N·s: the dummy directly, another player through the host as above. No swing is shown in your view.
- Both are **throwaway** and stop as soon as weapon code sets `PlayerViewmodel.Driven`. Real weapons call `PlayerHits.Strike` on the host instead (below).
- **Picking up and throwing (stand-in):** F picks up the loose object under the crosshair within 3 m and up to 60 kg, or puts down the one held; G throws it. A throw that strikes the dummy hits it with the object's weight times its speed: a monitor (4 kg) flinches it, a chair (8 kg) or a cabinet (30 kg) knocks it down. Host only (on a client F and G do nothing); Editor and development builds only.
  [DevCarry.cs](../../Assets/_Project/Scripts/Player/DevCarry.cs) and [ThrownHitTracker.cs](../../Assets/_Project/Scripts/Player/ThrownHitTracker.cs) are **throwaway**: Adam's pickup lane owns the real pick-up and throw (its ItemCarrier, not yet on this branch). Delete both files when that arrives, and have its throw report a `HitInfo` of type ThrownProp the same way.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| A hit on a player | The host decides it (weapon code calls `PlayerHits.Strike` on the host) | One reliable message to every machine carrying the `HitInfo`, its point relative to the victim (about 35 bytes) | Once per hit; nothing per tick |
| The player's flinch | Each machine, on its own copy of the victim's ragdoll | Nothing (bones are never streamed) | None |
| `HitFeed.Struck` (blood and other effects) | Each machine, raised locally after the flinch starts | Nothing | None |
| Stand-in shot or swing at a player (throwaway) | The shooter's machine asks; the host checks and decides | One reliable message shooter to host: eye position, aim and kind (about 25 bytes) | Once per press that lands on a player |
| The dummy and its reactions | Each client, for its own screen | Nothing | None |

Clients never decide a hit on a player: the stand-in message carries only where the shooter's eye was and which way it looked, the host ignores it if that eye is more than `1.5 m` from where the host has the shooter (covering ~0.7 m prediction lead at sprint on a 100 ms round trip plus ~0.5 m lean), and the host casts the ray again itself.
The flinch moves only the ragdoll, which lives in its own physics scene, so the capsule, `PlayerMotor`, prediction and reconciling are untouched.
Hit points travel in victim-local space (`InverseTransformPoint` on the host, `TransformPoint` on each client's ragdoll transform) because smoothing and interpolation offset each machine's visual slightly; sending local space keeps the hit on the same limb everywhere. `ShowStrike` uses `[ObserversRpc(ExcludeServer = true, RunLocally = true)]` so the host reacts immediately without FishNet dropping a duplicate self-delivery.
The dummy has no network identity and lives on the `Player` layer so `RagdollWorld` skips its capsule when copying level geometry while blasts still detect it (kept as its own prefab rather than stripping `Player.prefab`, because an unspawned `PlayerMotor` reports off-floor and editing nested networked prefabs breaks FishNet's prefab list). In a networked game it reacts only on the screen of whoever hit it. It is a development aid, not a gameplay object.

**For weapon code:** on the host, after your own ray or overlap struck a player's capsule, call `GetComponentInParent<PlayerHits>().Strike(new HitInfo { Point, Direction, Impulse, Type })`. Calling it on a client only logs a warning.

## The hit

One description, [HitInfo.cs](../../Assets/_Project/Scripts/Player/HitInfo.cs), used by the bench now and meant for real weapons later:

| Field | Meaning |
|---|---|
| Point | Where it struck, world space. For a blast, the blast's centre. |
| Direction | The way the hit travels, unit length. |
| Impulse | How hard, in newton-seconds **delivered to this body** (the weapon applies its own falloff first). |
| Radius | 0 for a point hit. Above 0 it is a blast: every part within that distance of Point is pushed. |
| Type | Bullet, ThrownProp, Blast or Melee. |

Alex's weapons session has agreed the shape (2026-11-13); a host-side hit event will carry the shooter, the victim and the weapon beside this struct, not inside it.
The file stays under `Scripts/Player/` because `Scripts/Combat/` is claimed by Adam's shooting lane, which may have a hit or damage type of its own and **has not answered yet**.
A thrown prop's impulse is its mass times its speed at impact, not a per-prop table.

**Why the caller states the impulse** instead of a bullet or a chair physically striking the ragdoll: the ragdoll lives in a physics scene of its own, where bullets and thrown props do not exist. Impulse (`N·s`) is used rather than velocity change because ragdoll parts have different masses (a forearm flies further than a chest for the same impulse), and `MinFlinchRecover` is clamped to `0.05 s` (six 120 Hz ragdoll steps, the shortest visible joint give-and-recover).

## Files

- [PlayerHits.cs](../../Assets/_Project/Scripts/Player/PlayerHits.cs): on the player's root beside `PlayerMotor`; `Strike(HitInfo)` (host only) is the one place to hit a player. Also holds the stand-in shot's host-side check (throwaway).
- [HitDummy.cs](../../Assets/_Project/Scripts/Player/HitDummy.cs): on the dummy's root beside its capsule; `Hit(in HitInfo)` is the one place to hit it.
- [HitFeed.cs](../../Assets/_Project/Scripts/Player/HitFeed.cs): raised on every machine after a player or the dummy starts reacting, for effects such as blood.
- [PlayerRagdoll.cs](../../Assets/_Project/Scripts/Player/PlayerRagdoll.cs): `Hit(in HitInfo)` does the reaction. It needs a hit profile; without one, hits do nothing to that body.
- [HitReactionProfile.cs](../../Assets/_Project/Scripts/Player/HitReactionProfile.cs): the tuning panel. `Assets/_Project/Profiles/DefaultHitReaction.asset` is the dummy's (Alex's tuning); `Assets/_Project/Profiles/PlayerHitReaction.asset` is the players'.
- [GreyboxHitsSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxHitsSetup.cs): `Add Hit Reactions to Player`, and the same step in the greybox player build.
- [HitDummy.prefab](../../Assets/_Project/Prefabs/Dev/HitDummy.prefab): the character, `PlayerAnimator` with no motor (so it stands), `PlayerRagdoll`, and a player-sized capsule on the Player layer.
- [HitDummySetup.cs](../../Assets/_Project/Editor/Dev/HitDummySetup.cs) and [HitBenchWindow.cs](../../Assets/_Project/Editor/Dev/HitBenchWindow.cs): the menu items.

## Tuning

The dummy in `DefaultHitReaction.asset`, players in `PlayerHitReaction.asset`; the same panel. The dummy's defaults are first guesses from frame strips; the players' are first guesses worked out from the ragdoll's weights and joint strengths. **Neither has been judged in play.**

| Inspector value | What it does | Players' starting value |
|---|---|---|
| Impulse share | How much of a hit's push the body takes. Lower = every hit moves it less. | 4 (dummy 2): a pistol round lands as 80 N·s |
| Neighbour share | How much of the push the parts joined to the struck one also take. 0 = only the struck part jerks. | 0.35 (dummy 0.6), so a shot snaps the one part |
| Max kick speed | The fastest one hit may throw a single part, m/s. Stops a heavy hit firing a forearm across the room. | 14 (dummy 8) |
| Flinch full impulse | A hit this hard (N·s) makes the struck joint fully slack. Lower = small hits crumple the body more. | 15: one pistol round is enough |
| Flinch limp share | How strong a fully slack joint still is. 0 = hangs loose. | 0.04 |
| Flinch recover | Seconds for a joint slackened by a shot (or anything but melee) to get its strength back. | 0.35 (dummy 0.5): a quick snap back |
| Melee body share | How much of a melee hit's push is thrown over the whole upper body instead of only where it struck. 0 = a swing lands like a heavy shot. | 0.7 |
| Melee recover | Seconds for joints slackened by a melee hit to recover. Longer = groggier. | 0.9 |
| Knock down impulse | A hit at least this hard (N·s) floors the body. 0 = never. **Placeholder rule.** | 0: must stay 0 for players (a warning is logged at spawn otherwise) |
| Bullets knock down | Off = shots only ever flinch. Off, as Alex chose on 2026-11-14 for the dummy ("only flinch"); for real players it is still Adam's rule. | Off; unused while knock-down is 0 |
| Down limp share | How strong the joints still are while down. 0 = a limp rag. | Unused (dummy only) |
| Down friction | How much the floor grips a fallen body. Lower = longer slides. | Unused (dummy only) |
| Down min time / Down max time | The shortest and longest it lies there, seconds. **Placeholder rule.** | Unused (dummy only) |
| Settle speed | It gets up once its hips move slower than this, m/s. | Unused (dummy only) |
| Get up time | Seconds to be drawn back onto its feet. | Unused (dummy only) |

The dummy's asset predates the melee rows, so it uses their code defaults (0.6 and 0.8 s) until someone sets them.

The bench's preset strengths are placeholders worked out from weight times speed (pistol 20 N·s, melee 60, chair 80, desk 320, blast 235 to 590) until real weapons report their own.

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

Until then the knock-down exists on the dummy only, with those rules as tunable placeholder values on the hit profile. Players already receive hits through `PlayerHits` (above), so a knock-down would start from the same host message.
Wiring it to real players changes netcode authority (the host decides the trigger and impulse, every client simulates its own ragdoll, bones are never streamed) and needs its ownership table written here and agreed before any code touches `PlayerMotor`.

## Known limitations

- **A code reload during Play stops every ragdoll** (players' too) until Play is restarted. The animator has the same weakness and still throws every frame after one; it belongs to the animation work.
- **Not judged in play.** Checked in frame strips of a hidden copy of the dummy; nobody has yet tried the bench in Play Mode.
- **The get-up has no animation.** The body is hoisted by its hips in about a quarter of a second, arms flung, rather than climbing to its feet. A get-up clip is the natural next step.
- **A near blast throws the dummy about 7 m** in the preview (Max kick speed 8 m/s, low ragdoll gravity). Lower Max kick speed for shorter flights.
- **A fallen body tumbles oddly**, legs over head, because its own parts do not collide with each other and the pelvis is made deliberately hard to turn.
- **Falling off a ledge is untested**, as is landing on stairs or furniture. The previews use a flat floor.
- **The player flinch has not been seen from a client** (AGENTS.md §3), nor played at all: the code was written without running Unity. Until it has been seen on a second machine, treat it as not done.
- **Knock-down on players is not built.** Players only flinch, however hard the hit; the rules are Adam's (below).
- **You barely see your own flinch.** Your own body is drawn as a shadow only, so a hit on you shows in your shadow and on everyone else's screen; nothing moves your camera or arms.
- **The gun arm flinches less than the rest** (read in code, not seen): after the ragdoll, `PlayerHeldWeapon` pulls the hand back toward the aimed gun and caps how far it may swing, so a hit there is mostly taken by the elbow and shoulder.
- **The stand-in shot has no lag compensation.** The host checks it against where players are on the host now, so a shot at someone moving fast across your view can miss on the host although it looked like a hit. Real weapons will need the same answer.
- **The stand-in swing is a single 2 m ray** along the crosshair, with nothing shown in your view, and it is not rate limited. All of it is deleted with the stand-in input.
- **A hit lands on the part nearest the point**, so a point far from the body still hits something; the caller is trusted to have struck the body.
- **The bench's body-part positions are estimates** for the standing idle; mid-fidget an arm may not be where the bench aims.
- **The stand-in pick-up has no hand, outline or prompt**, and a held object can be pushed through thin geometry if you walk into a wall. F reads the F key directly, so rebinding does not reach it.
- **Thrown props never touch the ragdoll physically** (moving colliders are not copied into the ragdoll scene), so a prop hit only exists if something reports it as a `HitInfo`.
