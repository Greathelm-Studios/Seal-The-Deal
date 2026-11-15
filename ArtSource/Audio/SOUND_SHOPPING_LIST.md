# Sound shopping list

Adam heard the synthesised set and judged it "very artificial". This is every sound slot the game has, for finding recordings in free, open-licence libraries **once Adam has approved the sources** (nothing is downloaded before that). Every slot keeps its file name and event: a cleared recording put at `ArtSource/Audio/recorded/<folder>/<file name>` with its row in [LICENCES.md](LICENCES.md) replaces the synthesised one on the next run of `python3 ArtSource/Audio/ui/make_ui_sounds.py`, levelled to the same loudness.

How to read it:
- **Order** is by how much each sound matters to feel: tier 1 is heard constantly in play and makes the game read as a demo when it is fake; tier 4 is menu ticks.
- **Keep?** says whether the synthesised one is plausibly fine to keep: **record** = needs a recording; **maybe** = a recording would probably help, but the synthesised one is usable; **keep** = short tonal UI sounds where synthesis is normal in games (judge by ear on the audition page).
- **Length** is the target after trimming. **Var.** is how many variations to find (several takes stop repeats sounding mechanical). Movement variations are numbered files: `Move_Footstep_Walk_01.wav` to `_04.wav`.
- **Search** is a phrase to type into a sound library.
- Mono is fine for everything; stereo files are folded to mono. Choose dry recordings (little room echo) for sounds heard in the world: the game adds distance.

## Tier 1: heard every few seconds in play

| File | Sounds like | Length | Loops | Var. | Search | Keep? |
|---|---|---|---|---|---|---|
| `Weapon_PistolShot.wav` | A close, sharp handgun crack with a short indoor tail | 0.3–0.5 s | no | 3 | "pistol shot close indoor dry" | record |
| `Weapon_AR15Shot.wav` | A rifle shot, tight, no long tail: it repeats 10 times a second | 0.1–0.2 s | no | 3 | "assault rifle single shot short" | record |
| `Weapon_ImpactWorld.wav` | A bullet hitting a hard wall or desk: a crack and a little debris | 0.1–0.3 s | no | 4 | "bullet impact concrete" / "bullet hit wood" | record |
| `Weapon_ImpactBody.wav` | A dull, muffled thud of a hit on a person (not gory) | 0.1–0.2 s | no | 3 | "bullet impact body thud" | record |
| `Move_Footstep_Walk` | One step on office carpet or hard floor in shoes, walking | 0.15–0.3 s | no | 4 | "footstep shoe office floor walk" | record (no synth exists) |
| `Move_Footstep_Run` | One running step, heavier and quicker | 0.15–0.25 s | no | 4 | "footstep run shoe hard floor" | record (no synth exists) |
| `Move_Land_Light` | Landing from a hop: both feet, short | 0.2–0.3 s | no | 2 | "jump landing feet light" | record (no synth exists) |
| `Move_Land_Heavy` | Landing from a fall: weight, a knee-bend grunt of cloth | 0.3–0.5 s | no | 2 | "landing heavy body thud floor" | record (no synth exists) |
| `Move_Jump` | Push-off: a shoe scuff and a cloth swish | 0.2–0.3 s | no | 2 | "jump take off cloth whoosh" | record (no synth exists) |
| `UI_HitMarker.wav` | A short, bright tick that says "you hit"; must cut through gunfire | 0.05 s | no | 1 | "hit marker tick" / "ui click metal short" | maybe |
| `UI_HitMarkerHead.wav` | The same, brighter and higher: a precision hit | 0.05 s | no | 1 | "headshot ding short" | maybe |
| `UI_KillConfirm.wav` | A satisfying confirm on a downed rival, plays over the hit tick | 0.2 s | no | 1 | "kill confirm ui" / "ding confirm short" | maybe |
| `Weapon_GlassShatter.wav` | An office window pane breaking and falling | 0.6–1.0 s | no | 2 | "glass window shatter" | record |
| `Item_Explosion.wav` | The grenade briefcase going off indoors: crack, boom, debris | 1.0–1.5 s | no | 2 | "explosion indoor grenade" | record |

## Tier 2: moves and handling

| File | Sounds like | Length | Loops | Var. | Search | Keep? |
|---|---|---|---|---|---|---|
| `Move_Slide_Start` | Dropping into a slide: a body hitting the floor, cloth | 0.3 s | no | 1 | "slide start body floor" | record (no synth exists) |
| `Move_Slide_Loop` | Sliding along a smooth office floor | 1–2 s | **yes, seamless** | 1 | "sliding friction floor loop" | record (no synth exists) |
| `Move_Slide_Stop` | Coming out of a slide: a scuff and a step | 0.3 s | no | 1 | "slide stop scuff" | record (no synth exists) |
| `Move_Mantle` | Hands slapping a ledge, a pull-up grunt of cloth | 0.4–0.6 s | no | 2 | "climb ledge hands grab" | record (no synth exists) |
| `Move_Vault` | A hand plant and a swing over a desk | 0.4–0.6 s | no | 2 | "vault over obstacle hand" | record (no synth exists) |
| `Move_Wind_Loop` | Wind past the ears at speed; faded up by speed in the game | 2–4 s | **yes, seamless** | 1 | "wind rush loop ears" | record (no synth exists) |
| `Weapon_ReloadMagOut.wav` | A magazine released and pulled out | 0.1–0.2 s | no | 1 | "magazine release out pistol" | record |
| `Weapon_ReloadMagIn.wav` | A magazine slid in and seated with a click | 0.15–0.25 s | no | 1 | "magazine insert click" | record |
| `Weapon_ReloadSlide.wav` | A pistol slide pulled back and released | 0.15–0.25 s | no | 1 | "pistol slide rack" | record |
| `Weapon_ReloadCharge.wav` | A rifle charging handle pulled and let go | 0.2–0.3 s | no | 1 | "rifle charging handle bolt" | record |
| `Weapon_ReloadFinished.wav` | A small settled click (optional) | 0.05–0.1 s | no | 1 | "gun handling click small" | maybe |
| `Weapon_DryFire.wav` | An empty gun's trigger: a dull click | 0.05–0.1 s | no | 1 | "dry fire click empty gun" | record |
| `Weapon_Swap.wav` | Holstering one gun and drawing another: cloth, a catch | 0.2–0.3 s | no | 1 | "weapon draw holster cloth" | record |
| `Item_PickUp.wav` | Grabbing an object off a desk | 0.15 s | no | 2 | "pick up object grab" | record |
| `Item_Drop.wav` | Setting or dropping it down | 0.2 s | no | 2 | "drop object floor" | record |
| `Item_Throw.wav` | A throw: an arm and air swish | 0.2 s | no | 2 | "throw whoosh" | record |
| `Item_ImpactLight.wav` | A light thrown thing landing: plastic, a tablet, a plant pot | 0.1–0.2 s | no | 3 | "plastic object hit floor" | record |
| `Item_ImpactHeavy.wav` | A heavy thing landing: an office chair, a cubicle panel | 0.3–0.5 s | no | 3 | "heavy furniture fall thud" | record |
| `Weapon_Hurt.wav` | Your own body taking a hit: a dull thump (2D) | 0.2 s | no | 2 | "body hit punch thud" | record |
| `Weapon_Death.wav` | You go down: a fall, the sound falling away (2D) | 0.6–0.8 s | no | 1 | "body fall down floor" | record |
| `Weapon_Respawn.wav` | Back on the floor: a light, clean arrival (2D) | 0.4–0.6 s | no | 1 | "respawn whoosh soft" / "teleport soft" | maybe |

## Tier 3: match moments, items, the menu bed

| File | Sounds like | Length | Loops | Var. | Search | Keep? |
|---|---|---|---|---|---|---|
| `UI_FloorOpened.wav` | An office lift's arrival chime: two soft tones | 0.6 s | no | 1 | "elevator ding chime" | record |
| `UI_BriefcaseTaken.wav` | A briefcase handle grabbed and latched | 0.2–0.3 s | no | 1 | "briefcase latch grab" | record |
| `UI_BriefcaseDropped.wav` | A briefcase hitting the floor | 0.2–0.3 s | no | 1 | "briefcase drop floor" | record |
| `UI_CountdownTick.wav` | A clean countdown tick: 3, 2, 1 | 0.07 s | no | 1 | "countdown beep tick" | maybe |
| `UI_CountdownGo.wav` | The go: a brighter, rising tone | 0.5 s | no | 1 | "race start go beep" | maybe |
| `UI_MatchWon.wav` | Your firm wins: a short, upbeat sting | 1.0–1.5 s | no | 1 | "win jingle short corporate" | record |
| `UI_MatchLost.wav` | Your firm loses: a short, gentle downbeat sting | 1.0–1.5 s | no | 1 | "lose jingle short soft" | record |
| `UI_TitleAppear.wav` | The title appears: a soft, airy chime | 1.0 s | no | 1 | "logo chime soft airy" | maybe |
| `UI_TransitionFrameZoom.wav` | The game starting: a whoosh that lands on a bright chord | 0.8–0.9 s | no | 1 | "whoosh rise impact soft" | maybe |
| `UI_Notification.wav` | A polite office notification, two tones | 0.4 s | no | 1 | "notification chime office" | maybe |
| `UI_LobbyReady.wav` | You're ready: three quick rising tones | 0.25 s | no | 1 | "ready confirm ui" | keep |
| `UI_LobbyUnready.wav` | The same, falling | 0.2 s | no | 1 | "cancel ui soft" | keep |
| `UI_HandsFull.wav` | A soft "can't" bonk | 0.2 s | no | 1 | "ui deny soft" | keep |
| `UI_Error.wav` | A soft, dull refusal | 0.3 s | no | 1 | "ui error soft" | keep |
| `Item_VentLoop.wav` | A fire extinguisher's gas spraying | 1–2 s | **yes, seamless** | 1 | "fire extinguisher spray loop" | record |
| `Item_VentEnd.wav` | The extinguisher running empty: a sputter | 0.4 s | no | 1 | "fire extinguisher sputter empty" | record |
| `Item_Arm.wav` | Arming the grenade briefcase: a two-part mechanical click | 0.1 s | no | 1 | "mechanical switch click arm" | record |
| `Item_Beep.wav` | The grenade's fuse beep (13 in 3.5 s, under 0.06 s each) | 0.05 s | no | 1 | "electronic beep short" | keep |
| `Item_WetFloorDown.wav` | A plastic wet-floor sign set down on a wet floor: clack and splash | 0.3 s | no | 1 | "plastic sign drop" + "water splash small" | record |
| `Item_Slip.wav` | A shoe squeak and a slip | 0.3 s | no | 2 | "shoe squeak slip" | record |
| `Ambience_MenuBed.wav` (folder Ambience) | A calm, airy bed for the title and the lobby: soft pads, distant city or office air, no strong melody | 1–3 min | **yes, seamless** | 1 | "ambient pad calm loop" / "office ambience quiet" | record (or music) |

## Tier 4: menu ticks (synthesis is normal here)

| File | Sounds like | Length | Loops | Var. | Search | Keep? |
|---|---|---|---|---|---|---|
| `UI_HoverMove.wav` | A tiny glassy tick as the mouse crosses a button | 0.1 s | no | 1 | "ui hover tick soft" | keep |
| `UI_Focus.wav` | The same, softer, when keys move focus | 0.1 s | no | 1 | "ui tick soft" | keep |
| `UI_Select.wav` | A button pressed down | 0.15 s | no | 1 | "ui click press" | keep |
| `UI_Confirm.wav` | A button released and done: a small rising confirm | 0.25 s | no | 1 | "ui confirm soft" | maybe |
| `UI_Back.wav` | Back, Esc, Stay: a small falling answer | 0.2 s | no | 1 | "ui back cancel soft" | maybe |
| `UI_TabChange.wav` | Switching tabs: a tick and a breath of air | 0.12 s | no | 1 | "ui tab switch swish" | keep |
| `UI_ToggleOn.wav` / `UI_ToggleOff.wav` | A switch on (rising) and off (falling) | 0.14 s | no | 1 each | "ui toggle switch" | keep |
| `UI_SliderTick.wav` | A tiny tick per slider step; the game pitches it by the value | 0.05 s | no | 1 | "ui slider tick" | keep |
| `UI_KeyBindListen.wav` / `UI_KeyBindSet.wav` | Waiting for a key (two soft pulses); the key saved (a clear ding) | 0.2 / 0.3 s | no | 1 each | "ui waiting pulse", "ui success ding" | keep |
| `UI_PanelOpen.wav` / `UI_PanelClose.wav` | A dialog opening and closing | 0.3 s | no | 1 each | "ui popup open", "ui popup close" | maybe |
| `UI_Transition.wav` | A screen opening over another: a soft whoosh | 0.5 s | no | 1 | "ui whoosh soft" | maybe |
| `UI_TransitionCloudWipe.wav`, `UI_TransitionPaneSlide.wav`, `UI_TransitionFade.wav` | The three screen-change overlays: a swish, three glass taps, a puff of air | 0.1–0.3 s | no | 1 each | "ui transition swish" | keep |
| `UI_DropdownOpen.wav` / `UI_DropdownClose.wav` | Not used: the menus have no dropdown | | | | | not needed |

## Totals

- Rows marked **record**: 41 (the 11 movement rows are 22 files with their variations, and have no synthesised version: they stay silent until recordings arrive).
- Rows marked **maybe**: 14. Rows marked **keep**: 13 (judge them by ear on the audition page first).
