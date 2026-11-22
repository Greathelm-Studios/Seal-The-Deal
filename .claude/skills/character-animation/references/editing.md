# Editing clips

`node scripts/ca.mjs edit --blend <work.blend> <operation> [options]` makes a new clip from clips already in a retarget working file. The realistic motion comes from the source clips; edits only rework it, so prefer combining and adjusting library clips over asking for motion the library does not have.

Source clips are never changed. The new clip is added to the work file's clip list, so `preview`, `export` and `verify` pick it up. A clip with the same name is refused unless you pass `--replace`. Every operation except `list` takes `--name NEW`, `--replace`, `--out other.blend` (save elsewhere instead of in place) and `--map map.json` (normally not needed: the map used by `retarget` is found automatically).

Bones can be canonical library names (`upperarm_r`, `spine_01`, see [rigs and retargeting](rigs-and-retargeting.md)) or the character's own names.

## Operations

| Operation | What it makes | Default name |
|---|---|---|
| `list` | Prints the clips in the file with frames, length and where each came from | |
| `trim --clip A --start F --end F` | Frames F to F of A | `A_F_F` |
| `retime --clip A --speed 1.5` | A played faster (above 1) or slower (below 1); first and last frames stay exact, so loops still close | `A_x1p5` |
| `mirror --clip A` | A with left and right swapped (a right-handed reload becomes left-handed); root motion keeps going forward | `A_Mirror` |
| `transition --clip A --to B [--overlap 8]` | A, then a crossfade of `--overlap` frames into B. B's root motion continues from where A is | `A_to_B` |
| `layer --clip BASE --over B [--bones spine_01] [--at 0] [--fade 6] [--weight 1] [--repeat]` | BASE with the bones from `--bones` down (comma list) taken from B. B starts at frame `--at` and fades in and out over `--fade` frames; `--repeat` repeats B for the whole of BASE instead; `--weight` below 1 mixes the two | `BASE_B` |
| `pose --clip A --bone X [--rotate ...] [--move ...] [--start F] [--end F] [--ease 4] [--keep-feet]` | A with bone X turned and/or moved over frames F to F, easing in and out over `--ease` frames (no easing at the clip's own ends). Children follow. `--keep-feet` bends the legs so the feet stay where they were | `A_Posed` |
| `loop --clip A [--overlap 8]` | A made seamless: its tail crossfades into its head, and the last frame repeats the first. In-place clips only | `A_Loop` (or `A_Seamless_Loop`) |
| `foot-lock --clip A [--height-tol 0.025] [--speed-tol 0.15]` | A with feet pinned while planted (low and still), using leg IK | `A_Planted` |
| `hold --clip A [--frame F] [--length 2]` | Frame F of A (default the last) held still for `--length` seconds | `A_HoldF` |
| `ik --clip A --limb hand_r --move up:0.2 [--space root\|hips]` | A with a hand or foot (`hand_l`, `hand_r`, `foot_l`, `foot_r`) moved by an offset from where it is, the arm or leg bent to reach it. The hand or foot keeps its rotation; fingers and anything held follow. `root` uses the character's axes, `hips` turns them with the hips | `A_IK` |
| `ik --clip A --limb hand_l --pin F` | A with the hand or foot staying where it is at frame F (a hand on a rail or a table) | `A_Pinned` |
| `delay --clip A --bones spine_03 [--frames 3] [--cascade]` | A with those bones' rotations arriving `--frames` late (fractions allowed, negative leads). `--cascade` delays each listed bone a step more than the one before: follow-through | `A_Delay` |
| `exaggerate --clip A --bones upperarm_l,upperarm_r [--amount 1.5]` | A with those bones' rotations scaled around the clip's average pose: above 1 more motion, below 1 less | `A_Exaggerated` |
| `aim --clip A --at forward:3,up:1.6 [--bone Head]` | A with the head (or `--bone`) turned to look at a point, in metres from the ground under the hips at rest | `A_Aim` |

| `directions --clip WALK [--mode strafe\|facing] [--side-stride 0.45] [--arms 0.4]` | Eight clips from one looping forward walk or run (in place or root motion): `WALK_Fwd`, `_FwdL`, `_L`, `_BwdL`, `_Bwd`, `_BwdR`, `_R`, `_FwdR` (`_Loop` kept; `--name` sets the base). See [Directions](#directions) | `WALK_<dir>` |
| `style --clip WALK [--preset stroll\|brisk\|heavy\|sneak\|my.json] [...]` | A walk or run restyled. See [Style](#style) | `WALK_<Preset>` or `WALK_Styled` |

`pose`, `ik` and `aim` take `--start F --end F --ease 4` for a frame range (no easing at the clip's own ends) and `--weight` (0 to 1) for how far to go.

Names keep a trailing `_Loop` at the end (`Walk_Loop` retimed is `Walk_x1p5_Loop`) so engine loop settings still match.

### Axes for pose

`--rotate up:20,left:-10` and `--move down:0.05,forward:0.1` use the character's own axes from its rest pose: `up`, `down`, `left`, `right`, `forward`, `back`. Rotations are in degrees, applied in the order given; moves are in metres.

Rotations follow the right-hand rule around the named axis:

- `up:+` turns toward the character's left.
- `left:+` tips forward: a spine bends forward, a hanging arm swings back.
- `forward:+` tilts toward the character's right: it raises the left arm out to the side. `forward:-70` on `upperarm_r` raises the right arm to about shoulder height.

Check the result on a preview sheet; flip the sign if it went the wrong way.

### Directions

For an 8-way blend space (Unreal) or 2D blend tree (Unity) when the library has only a forward clip.

- `strafe` (default): the body keeps facing forward while the feet step toward each direction. Each foot's path around the hips is turned toward the direction and the legs are re-solved with IK; root-motion travel turns with it. Backward directions play the walk in reverse (toes land first, as when stepping back) and turn it at most 45 degrees. Steps shorten toward the sides along an ellipse, to `--side-stride` of their length straight sideways (full length leaves the feet out of reach), so diagonals keep their exact heading and root-motion speed drops sideways (0.58 m per cycle sideways against 1.30 m forward for the library walk). Feet that would cross are moved apart until they clear. Arm swing is scaled toward `--arms` going sideways.
- `facing`: each clip is the whole walk turned to face its direction, for root-motion sets where the character turns to travel.
- A clip already named `..._Fwd_Loop` is its own forward clip, so `Jog_Fwd_Loop` gives seven new clips.
- Refused: clips whose feet barely move (idles), that travel sideways, or that do not loop.

### Style

Presets: `stroll` (shorter, slower, softer), `brisk` (longer, quicker, more arm swing), `heavy` (slower, wider, more bounce and side sway, slightly lower), `sneak` (short slow steps, hips 12 cm lower, leaning forward, little arm swing). Any of these overrides a preset:

| Option | Meaning |
|---|---|
| `--stride 1` | step length (and root-motion travel) factor |
| `--cadence 1` | steps per second factor (the clip gets shorter or longer) |
| `--width 0` | metres to move each foot outward |
| `--bounce 1` | hips' up-down motion factor |
| `--sway 1` | hips' side-to-side motion factor |
| `--hips 0` | metres to raise (negative lowers) the hips; the knees bend to keep the feet down |
| `--arms 1` | arm swing factor |
| `--lean 0` | degrees to tip the spine forward |

`--save my.json` writes the style used; `--preset my.json` applies it to another walk, scaling the metre values to that character's height. Root-motion travel per second changes by stride times cadence.

## Recipes

- Lower stance on an idle: `pose --clip Idle_Loop --bone pelvis --move down:0.12 --keep-feet`.
- Wave or point while standing: `pose --clip Idle_Loop --bone upperarm_r --rotate forward:-70 --start 20 --end 55 --ease 6`, then adjust `lowerarm_r` the same way.
- Reach for something: `ik --clip Idle_Loop --limb hand_r --move up:0.3,forward:0.25 --start 20 --end 55`.
- Hand on a rail while walking (root-motion clips): `ik --clip Walk_Loop --limb hand_l --pin 0`. Pinning on an in-place walk holds the hand still while the body bobs, which is only right for short spans.
- Hold a weapon further from the body through a whole clip: `ik --limb hand_r --move forward:0.1 --space hips`.
- Heavier, looser upper body: `delay --clip Walk_Loop --bones spine_01,spine_02,spine_03,neck_01,Head --frames 1 --cascade`. Chest arriving late: `delay --bones spine_03 --frames 3`.
- Bigger arm swing on a walk: `exaggerate --clip Walk_Loop --bones upperarm_l,upperarm_r --amount 1.5`; add `lowerarm_l,lowerarm_r` for the forearms. Calmer idle: `--amount 0.5` on the spine.
- Look at something: `aim --clip Idle_Loop --at forward:2,left:1.5,up:1.5`. On a root-motion clip the point stays in place while the character moves, so the head tracks it.
- Walk while reloading: `layer --clip Walk_Loop --over Pistol_Reload --bones spine_01`. Retime the overlay first if it is longer than the base.
- One arm only: `--bones upperarm_r` (or `clavicle_r` to include the shoulder).
- Faster or slower locomotion: `retime`. Root-motion speed scales with it.
- Left-handed version of anything: `mirror`.
- Idle into walk for a cutscene: `transition --clip Idle_Loop --to Walk_Loop --overlap 10`.
- A once-off clip as a loop: `loop`.
- A pose to stay in after a one-shot (lying after a death, seated after sitting down): `hold --clip Death01 --length 3`, then `transition --clip Death01 --to Death01_Hold<last frame> --overlap 1` if the engine needs one clip. A static pose to start keying from: `hold` any frame.
- Feet sliding on a root-motion clip, an idle or an emote: `foot-lock`. In-place locomotion is left alone on purpose: its planted foot slides back by design, and the engine's root motion or foot IK handles it.

## Limits

- Edits rework existing motion. They cannot add weight, anticipation or physics that the source does not have; for a new full-body move use another clip, video of yourself, or the AI route.
- `layer` blends local bone rotations. Overlays that bend the spine a lot can make the upper body lean against the base's legs; lower `--weight` or layer from `spine_02`.
- `pose` with large rotations can push a limb through the body. Look at the preview.
- `loop` refuses clips with root-motion travel.
- `ik` warns when the limb cannot reach (it straightens and stops short). It moves only the arm or leg, so big reaches look stiff; turn or bend the body with `pose` first.
- `delay` and `exaggerate` change rotations only, of the listed bones; bones below follow, bones above do not. A whole-limb swing therefore grows by less than `--amount` (×1.6 on the upper arms gave ×1.43 on the arm's swing in the test).
- `aim` turns one bone, so turns past about 70 degrees look broken and it warns; turn the neck or body first with `pose`.
- `directions` makes side steps from forward steps: the knees still lift forward as in the source and the toes point ahead, so side walks read as stylised and side jogs as skipping. Good enough for blending and distant characters; a real strafe clip (another pack or the AI route) is better for close-up sideways movement. Preview the `_L` and `_R` clips.
- `style` keeps the source's timing within each step; it cannot add weight shifts the source does not have. Large `--stride` or lower `--hips` can put the feet out of reach, and it warns.
- `foot-lock` finds contacts from the clip itself (lowest point plus stillness, scaled by character height). If it misses a contact, raise `--speed-tol`; if it pins a foot that should move, lower it.

Always preview the new clip and read the printed numbers before exporting.
