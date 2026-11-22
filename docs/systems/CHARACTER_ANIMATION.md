# Character animation

**Status:** Current (seventh pass: the active ragdoll chases real animation clips instead of hand-written poses; plus 2026-11-21 remote ragdoll LOD culling and local BodyView head-turn cap. Checked in frame strips from the side and front; **not yet judged by a person and not yet seen in Play Mode**) · **Last updated:** 2026-11-21

The player's body is a real ragdoll: eleven rigidbodies joined like a skeleton, the way TABS and Gang Beasts build theirs.
It is not limp. Animation clips say what the body should be doing every frame (standing, running any way, sprinting, in the air, landing), and every joint has a motor pulling toward that. What you see is the body trying to do the clip.

- **Standing:** as on the character sheet: feet together, legs straight, arms hanging straight down at his sides, head a little forward. He breathes: the chest tips back and the shoulders lift about 3 cm, sixteen times a minute.
- **Looking round:** standing, the head leads. Up to 50 degrees either side only the head and shoulders turn; past that the body turns to face where you are looking. Moving or in the air, the body always faces the look.
- **Fidgets:** every 5 to 12 seconds of standing still he does one small thing, picked at random: scratches the back of his head, scratches his hip, yawns and stretches both arms up, or looks left and right. Moving off cuts it short.
- **Setting off:** the legs go from the stance straight into the first stride of the run, always the same first step. Most of the player's violent start (0 to 6 m/s in 0.13 s) now carries the whole body with it, so the top no longer bends back and the legs no longer trail.
- **Running:** a full run with a forward lean, pumping bent arms and a heel kick. The legs turn over faster or slower to match the real speed across the floor.
- **Strafing and back-pedalling:** the chest and shoulders stay squared toward where the player is aiming while the feet step sideways, diagonally or backwards (eight directions). Sideways and backwards are calm, upright steps; `AlignUpperBodyToAim` measures the clip's chest yaw and counter-twists spine, chest and upper chest (counter-rotating neck and head so the gaze stays level forward) so another player can read aim direction and the held weapon arm is not fighting a twisted shoulder. The legs are always on exactly one of the eight clips (the nearest to the way he is travelling), and a change of direction cross-fades to the new one over about an eighth of a second. A quick left-right flip keeps its stride instead of dropping toward the standing stance.
- **Sprinting:** a longer, faster stride, forward only; sprinting sideways uses the run.
- **Crouching:** a low, hunched crouch with the head held level when still, and a crouched walk in eight directions when moving, eased in and out over about an eighth of a second from the motor's crouch. Everyone sees it: it reads the motor's animation state, which every client has. The motor's old stand-in, which squashed the whole body toward the feet, is switched off on `Player.prefab` and in the greybox tool.
- **Sliding:** within about an eighth of a second of the slide starting he drops into a seated, feet-first slide, torso leaning back and head up, and holds it for as long as the motor slides. When the slide ends he comes up in about a quarter of a second into whatever the motor ends in: the crouch if crouch is still held, or standing.
- **Stopping:** the legs keep their stride for a tenth of a second after the speed drops (so a flip of direction is not mistaken for a stop), then ease smoothly into the stance over `StopBlendTime` (default 0.22 s) while stride cadence slows with the blend.
- **Jumping:** the jump clip shaped on top by vertical velocity (`ShapeAirPose`): arms tuck inward and downward with a natural elbow bend instead of sticking straight out, the torso leans forward and knees tuck while rising, and the legs extend slightly to reach for the floor while falling. `AirBrace` (1.35) keeps the ragdoll cohesive without locking stiff.
- **Landing:** a crouch whose depth grows with how fast the body came down (`LandFullSpeed = 13 m/s`, so a normal ~9.7 m/s jump reads at ~75% depth and running landings keep 60%), then it stands up into whatever it is doing next.
- **Hits and knock-back:** running into something, or being blasted, weakens every joint for a moment, so the body goes properly limp. Reactions to weapon hits (a flinch on players and on a test dummy; knock-down on the dummy only) are in [HIT_REACTIONS.md](HIT_REACTIONS.md).
- **Collisions:** every part bumps into the level and into other players' ragdolls.

It hangs from its hips, which a spring drags along after the player. **It does not balance on its feet and cannot fall over**; the player capsule still decides where you are.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| The clip blend and the ragdoll | Each client, for its own screen | Nothing | None |

Every client works the blend out from what it already has (how its smoothed copy of the player moved this frame, and the motor's state), and runs its own ragdoll.
Two screens will show the same player's limbs in different places. Nothing reads them back, so that cannot desync, and it costs no bandwidth.

**The ragdoll lives in a physics scene of its own** ([RagdollWorld.cs](../../Assets/_Project/Scripts/Player/RagdollWorld.cs)), stepped by that class at 120 steps a second and by nothing else.
The game's own physics scene is stepped by FishNet once a tick and re-stepped several times on every reconcile; a ragdoll in it would be simulated many times over and could shove the predicted capsule and the host's props. In its own scene it can do neither.
The price is that the level's colliders are in the other scene, so the ragdoll scene keeps copies of the solid level geometry within 3 m of each ragdoll (refreshes staggered across ragdolls every 0.2 s, dropped past 8 m hysteresis; disabled-state checks throttled to 0.2 s with last-known bounds cached for disabled colliders). Remote ragdolls beyond 25 m (resume at 22 m) or more than 9 m vertically away (resume at 8 m) pause simulation and leave `RagdollWorld` (re-adding and snapping cleanly when back in range); when no active ragdolls remain, `RagdollWorld` unloads its physics scene and clears its proxy table.

## Architecture and design rationale

- **Why a manual `PlayableGraph` instead of an Animator Controller:** Animator Controllers evaluate on Unity's schedule before FishNet moves the smoothed `Graphics` visual for the frame, and state would live in an asset a rebuilt prefab must wire to. `PlayerAnimator` sits on `Graphics` (above the lean pivot so lean slide is not read as running) at execution order 100 (before `PlayerRagdoll` at 110), drives a manual `PlayableGraph` with `applyRootMotion = false` and `cullingMode = AlwaysAnimate` (so the owner's shadow-only body keeps animating for the ragdoll), resets the skeleton to T-pose rest before `_graph.Evaluate()` (so un-animated bones such as the lower spine do not keep the ragdoll's previous frame output), and exposes `HeadBone` directly because clearing the humanoid avatar makes runtime `Animator` bone lookups return null.
- **Play-Mode script reload guard:** Unity preserves plain fields across a Play-Mode recompile without re-running `Start()`, so `PlayerAnimator` uses `[NonSerialized] bool _ready` and `PlayerRagdoll` checks `Running` to disable cleanly with one logged error instead of throwing every frame.
- **Direction snapping, stride hold, upper-body aim align, air shaping, and head turn:** Sideways/backward clips are not phase-synced with forward clips and measured velocity wanders a few degrees as the camera turns, so `PlayerAnimator` snaps to one of 8 directions with 10° hysteresis (`RunDirectionSlack`) and cross-fades over 0.12 s (`RunDirectionBlendTime`). Holding stride for 0.1 s (`StrideHoldTime`) prevents left-right direction flips from dipping toward idle while letting real stops settle cleanly over `StopBlendTime` (0.22 s) with cadence scaling down alongside `_moveWeight`. After `_graph.Evaluate()`, `AlignUpperBodyToAim` measures the evaluated chest yaw relative to rest and counter-twists spine (35%), chest (35%) and upper chest (30%) back toward `_model.forward` when strafing or moving backwards (excluding slides), counter-rotating neck and head so gaze stays forward and keeping spine twist well inside `PlayerRagdoll`'s 50° `SpineBend` limit. `ShapeAirPose` tucks the arms down/forward along the anatomical elbow hinge axis and adds velocity-driven spine pitch and asymmetric thigh/knee tuck-vs-reach (zero-allocation, skipped while knocked back). Standing head turns (`TurnHeadFirst`) rotate the full body once past `HeadTurnLimit` (clamped to `15°` for the local player in first-person body view so looking right does not twist the shoulder across the camera). Breathing (`Breathe`) is additive in code so amplitude and rate tune live without re-exporting clips. In the clip-free fallback, `SolveLeg` resets leg bones to rest each frame so twist does not accumulate across frames.
- **Why `RagdollWorld` instead of FishNet `OfflineRigidbody`:** `OfflineRigidbody` would step only once per 60 Hz tick (visibly stepping at high frame rates) and could still push predicted capsules. `RagdollWorld` steps at 120 Hz (`MaxStepsPerFrame = 4`, covering down to 30 fps) and exposes `Blend` for smooth frame interpolation.
- **Ragdoll mass skew, steadiness, and joint setup:** Part masses (`10, 10, 5, 2, 1.5, 2, 1.5, 5, 3.5, 5, 3.5` kg) give the head 11% and head+chest ~33% of total mass so the top sways over the legs (above 50% collapsed under 45 m/s² starts). Pelvis inertia is multiplied by `25` (`HipsSteadiness`) so swinging 17 kg of legs stays within ~2° of the pose. Joints use `RotationDriveMode.Slerp` with `useAcceleration = true` (one frequency scales to both heavy chests and light forearms) and `14` solver / `4` velocity iterations. Knees and elbows are hinges; `NeckTurn = 120°` exceeds a real neck's 80° because the chest rigidbody sits at the lowest spine bone (which turns ~90° in sideways runs). Neck and upper spine bones are left out of the rigidbody chain so they keep the pose's twist and bend on top of the chest part. Start/stop carry (`MaxCarryPush = 150 m/s²`) reads the visual's acceleration (not the hips target, which includes clip bob), passing 60 m/s² stops through while softening jump launches and teleports (`SnapDistance = 2.5 m`). Knocked-down dummy parts switch to `ContinuousDynamic` collision and refresh proxies every frame so fast limbs do not tunnel through 0.2 m floor slabs.
- **Dev mirror (`DevMirror`):** Uses a reflected eye behind the glass and an oblique frustum sized to the viewport-raycast visible patch of the quad (`PatchMargin = 0.03`, `MinPatch = 0.05 m`, `MinEyeDistance = 0.05 m`, `24`-bit depth for URP stencil, `Clamp` wrap so URP Unlit does not tile outside the patch, left-right UV flip, and `Scale(1, 1, -1)` for Unity's `-Z` camera space). `RenderPipelineManager` callbacks temporarily unhide the local player's body only for the mirror camera.

## Files

- [PlayerAnimator.cs](../../Assets/_Project/Scripts/Player/PlayerAnimator.cs): decides the pose. With clips in the profile it blends them onto the skeleton itself (no Animator Controller, no parameters) and sets every clip's time by hand, so the stride matches the real speed. With any clip slot empty it falls back to the older hand-written procedural pose, which is still in the file.
- [PlayerRagdoll.cs](../../Assets/_Project/Scripts/Player/PlayerRagdoll.cs): runs straight after. Builds the bodies and joints from the T-pose at spawn; each frame it reads the pose as targets, aims the joint motors, steps the ragdoll scene, and overwrites the skeleton with where the bodies are.
- [RagdollWorld.cs](../../Assets/_Project/Scripts/Player/RagdollWorld.cs): the separate physics scene and its copies of the level.
- Both components sit on the `Graphics` object of [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab), added by the greybox tool. Adding clips did not change the prefab: the clips are on the profile.
- [AnimationProfile.cs](../../Assets/_Project/Scripts/Player/AnimationProfile.cs) and [DefaultAnimation.asset](../../Assets/_Project/Profiles/DefaultAnimation.asset): the clip slots and the tuning panel. Every field has a tooltip with its unit. Runtime never writes it.
- [STDMC_Moves.fbx](../../Assets/_Project/Art/Characters/STDMC/Animations/STDMC_Moves.fbx): the clips (the twelve in use, plus the library's untouched `Idle_Loop`), skeleton only, imported as **Generic** (see below).
- [DevMirror.cs](../../Assets/_Project/Scripts/Player/DevMirror.cs) and [DevMirror.prefab](../../Assets/_Project/Prefabs/Dev/DevMirror.prefab): a test mirror. In first person your own body is shadow-only, so the mirror shows it to its own camera alone. A true reflection that follows your eye and fits the quad at any size. It draws only the patch of glass that is on screen, at about the screen's resolution (Picture Scale on the mirror), so it stays sharp close up; seen at a steep angle along a long mirror the far end still goes soft. It costs one extra render of the scene per frame.

## The clips

| Slot | Clip | Source |
|---|---|---|
| Idle | `Idle_Sheet_Loop` | Made to match Alex's character sheet: the whole body is the rig's own neutral pose (straight spine and legs, feet together, head as modelled), with only the arms brought down 66 degrees and the shoulders dropped 16. Nothing of the library idle is left but the finger curl; its staggered, turned stance and forward-bent torso kept reading as wrong. It does not move by itself: the breathing below is all of its motion |
| Run, forward | `Run_Fwd_Loop` (made for 5.36 m/s) | Library 1's `Jog_Fwd_Loop` with the neck and head lifted 8 and 18 degrees, because Alex found the runner looking at the floor |
| Run, forward-left and forward-right | `Run_FwdL_Loop`, `Run_FwdR_Loop` | generated from the forward run, same head lift |
| Run, sideways and backwards (five directions) | `KRun_L/R/BwdL/Bwd/BwdR_Loop` (made for 4 m/s) | Kevin Iglesias, *Human Basic Motions FREE* (`Run01_Left/Right/BackwardLeft/Backward/BackwardRight`, in-place), retargeted unchanged. The hips turn into the run while the head stays forward. They replace three generated attempts (a jog-based skip, a walk whose feet crossed, a widened walk) that Alex found chaotic |
| Sprint | `SprintUp_Loop` (made for 8.25 m/s) | Library 1's `Sprint_Loop` with the neck and head lifted 8 and 20 degrees |
| Air | `Jump_Loop` | Library 1 |
| Landing | `Jump_Land` | Library 1 |
| Fidgets | `Fidget_ScratchHead`, `Fidget_ScratchHip`, `Fidget_Yawn`, `Fidget_LookAround` | Made in Blender on top of the idle pose with the skill's hand IK and bone turns; each is 2.5 s and starts and ends on the idle. Not captured motion |
| Crouch idle | `CrouchUp_Idle_Loop` | Library 1's `Crouch_Idle_Loop` with the neck and head lifted 14 and 26 degrees: the original looks at the floor (head 45 degrees forward), Alex: "his head looks down" |
| Crouch walk, eight directions | `CSneak_Fwd_Loop` (made for 0.75 m/s) and `CSneak_FwdR/R/BwdR/Bwd/BwdL/L/FwdL_Loop` | Library 1's `Crouch_Fwd_Loop` with the same head lift; the other seven generated from it with the skill's `directions`. Tried and dropped: a crouched run made from our run (the back knee drags on the floor) and a crouched walk made from the walk (barely crouched, legs splay) |
| Slide | `Slide_Sit_Loop` | Library 2's `Slide_Loop` with the spine tipped up 22, 16 and 10 degrees and the head lowered 12, because the original lies flat on its back |

- **Licence:** the Quaternius library is CC0 (public domain); nothing to credit. **The Kevin Iglesias pack states no licence** on its page or in its download; the author told a commenter that a link to keviniglesias.com is enough credit. Until that is settled, treat its five clips as usable in the game but not cleared for redistribution in a public repository (Adam's call), and credit keviniglesias.com.
- **How they were made:** retargeted onto `STDMC_Rigged.fbx`'s own skeleton in Blender with the `character-animation` skill, so each clip is plain bone animation that already fits this rig exactly.
- **Why Generic and not Humanoid:** this model's origin is at its hips, not its feet. Unity's humanoid maths measures everything from the origin up, and with it the hips landed metres away. Generic clips are written to the bones as they are. For the same reason `PlayerAnimator` takes the humanoid avatar off the model's Animator on the first frame; the avatar is only needed at spawn, to find bones by name.
- **Editing in Blender:** this model is 0.138 Blender units tall, so any edit given in metres (the skill's `style --width`, `pose --move`) has to be multiplied by about 0.077; rotations and factors are unaffected.
- **Re-exporting:** the Blender armature has to have its scale applied (so `Armature` imports at scale 1, like the rig's own file) before export, or every bone is 100 times out. Import settings: Generic, no avatar, Preserve Hierarchy on, the rig's scale factor, Loop Time on every `_Loop` clip.

## Tuning

All in `DefaultAnimation.asset`.

**Clips** header:

| Inspector value | What it does |
|---|---|
| Run clip speed / Sprint clip speed | The speed each clip's stride was made for. The legs speed up or slow down to cover the real distance. Lower = quicker legs; raise it if the feet skate. |
| Side clip speed | How fast the legs step when moving sideways or backwards. At the default 6 a strafe at walking speed steps about one and a half times a second. Lower = quicker, busier steps that slide less. |
| Strafe aim align | How much the chest and shoulders counter-twist back toward aim when strafing or running backwards (0..1). Default 0.85 keeps shoulders nearly square to aim while legs run sideways. |
| Air arm tuck | Degrees the arms are brought down toward the sides from the jump clip while airborne. Default 32 removes the scarecrow look. |
| Air pose motion | Multiplier (0..1.5) for velocity-driven airborne spine pitch and rising knee tuck vs falling leg reach. Default 1. |
| Head turn limit | How far you can look round with only the head and shoulders turning, in degrees either side, before the body follows. Standing only. |
| Body turn speed | How fast the body turns to face the look once it follows, in degrees per second. |
| Fidget clips | The list of fidgets; one is picked at random. Add or remove clips here. |
| Fidget min wait / max wait | The shortest and longest he stands still between fidgets, in seconds. |
| Crouch clips, crouch idle clip, slide clip | The crouch and slide clips. With any of them empty the body stays standing when the player crouches or slides. |
| Crouch clip speed | How fast the legs step when crouched. The clip is a slow sneak made for 0.75 m/s and the game's crouch is 3.3 m/s; at the default 2 the steps run at about one and a half times the clip's pace and the feet skate a little. Lower = quicker, busier steps that slide less. |
| Slide blend in / out | How long the body takes to drop into the slide, and to come up out of it, in seconds. |
| Land full speed | How hard a landing has to be to play the whole crouch. An ordinary jump lands at about 9.7 m/s, so the default of 13 gives about three quarters depth. Lower = deeper landings. |
| Stop blend time | Under Smoothing: how long running eases back into the standing stance after stopping, in seconds. Default 0.22. |

**Standing** header (two values are used with the clips):

| Inspector value | What it does |
|---|---|
| Breath amount | How far the chest tips and the shoulders lift with each breath, in degrees. 0 = only what the idle clip has. Default 8, about 3 cm. |
| Breath rate | Breaths per second. |

**Ragdoll** header (the feel):

| Inspector value | What it does |
|---|---|
| Start stop carry | **The start-and-stop floppiness dial.** How much of the player's speeding up and slowing down the body simply goes along with. 0 = the old full yank (top bends back on a start, folds over on a stop); 1 = starts and stops as one piece. Default 0.65. |
| Spine strength | The main wobble dial. Lower = the top half sways, lags and droops more. |
| Air brace | How much harder every joint pulls in the air. 1 = a jump sprawls and tumbles; 1.35 = holds its shape without locking stiff; 2 = very rigid. |
| Head strength | Lower = a heavier, lolling head. |
| Arm strength | Lower = arms that trail and flail. Raised from 13 to 20 when Alex asked for the arms a little less wobbly. |
| Leg strength | Keep well above the others or the legs cannot keep up with the run. |
| Joint damping | How long everything keeps swinging. 1 = stiff, 0.6 = swings past and settles, below 0.3 = flaps. |
| Hips follow | How tightly the body is dragged after the player. Lower = it trails and sloshes. |
| Upright strength | How firmly the pelvis is held level and facing forward. |
| Ragdoll gravity | Lower = floaty limbs. |
| Limp share | How weak the joints go when hit or knocked back. |

The headers marked "procedural pose only" do nothing while the clips are assigned.

## Known limitations

- **Not judged by a person, and not seen in Play Mode.** It was checked by playing it on a hidden copy of the player in the Editor at 60 steps a second and reading the frames from the side and the front: standing start, steady run, stop, jump, strafe, idle. A recorded Play Mode run was attempted and did not complete (the recorder could not put the lobby into local test mode). Another player's view and frame cost with 8 players are unchecked.
- **It follows the clips closely now.** In the preview the hips stay within about 2 degrees of the clip. If that reads as too stiff, lower Start stop carry and Spine strength before anything else.
- **Sideways running turns the hips into the travel direction.** `AlignUpperBodyToAim` squares the chest and shoulders back toward aim (default 85%), while the hips and legs still turn into the strafe clip.
- **Sideways and backward runs are made for 4 m/s** and are played faster to cover 6 (Side clip speed, now 5); some foot slide remains.
- **Run directions snap to eight.** A gamepad stick held between two of them uses the nearer clip, so the feet can point up to about 30 degrees off the true direction.
- **Fidgets are hand-posed, not captured, and differ per screen.** They are simple arm and head moves with no finger work, and the hip scratch is small. Each client picks its own, so two players do not see the same fidget at the same moment.
- **The right arm is taken over while the player holds a weapon.** `PlayerHeldWeapon` bends it onto the gun before and after the ragdoll each frame, so the clips' right arm and the ragdoll's lag on it do not show ([VIEWMODEL.md](VIEWMODEL.md)). Knocked back or climbing, it lets go and the arm follows the clip and the ragdoll again.
- **No turn-in-place, no crouch, no climb.** Mantling players use the air clip.
- **It cannot fall over or lie on the floor.** The hips are always held up by their spring. A true free ragdoll on knock-out would need the motor to hand control over, which is netcode work.
- **Joint limits are generic.** They are ordinary human ranges measured from the T-pose, not fitted to this character; the shoulders have none.
- **Its own parts pass through each other.** With self-collision the arms caught on the ribs every stride.
- **No carrying or throwing yet.** Alex's direction (2026-11-13, not yet in the GDD) is that any loose object can be picked up and thrown. When that arrives the body needs a carry pose for small and large objects, a throw, and a reaction to being hit by one; none exist. The pickup system itself is Adam's lane 3, not this one.
- **The crouch walk is a sneak played fast.** The library has no crouched run, and the game's crouch is jogging pace, so the steps are quicker than the clip was made for and still skate. A real crouch-run clip (Mixamo has one, behind an Adobe login) would fit better.
- **No slide entry or exit clips.** The slide is one held pose blended in and out; the library's `Slide_Start` and `Slide_Exit` are not used.
- **Crouch side and back steps are generated, not captured.** The crouch walk only exists going forward in the library; the other seven directions are made from it.
- **Sliding body and the floor copy unchecked.** In the slide the hips and legs are at floor height; how the ragdoll's parts meet the level's floor copy while sliding has not been seen in Play Mode.
- **Level copies stand still.** Doors that move and props that are thrown are not followed, so the ragdoll does not react to a chair hitting it. Only box, sphere, capsule and mesh colliders are copied.
- **Feet do not grip the floor.** Nothing holds a planted foot in place.
- **The model's Animator has no avatar while playing.** Anything added later that asks it for a bone at runtime (not at spawn) will get nothing.
- **Two pose systems in one file.** The procedural pose is kept only as the fallback for an empty clip slot; once the clips are accepted it can be deleted along with its profile fields.
- **Cost is unmeasured.** Eleven rigidbodies, ten joints, one physics step at 120 a second and a twelve-clip blend per frame, per player, on every client.
- **Lane clash:** Adam's movement lane regenerates `Player.prefab` from its own setup menu. If that drops `PlayerAnimator` and `PlayerRagdoll` from `Graphics`, the character goes back to a T-pose; re-run the greybox player rebuild or carry the two components across.
