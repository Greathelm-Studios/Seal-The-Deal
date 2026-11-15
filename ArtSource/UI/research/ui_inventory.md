# UI inventory: Close the Deal

**Status:** Research, for the mockup agents · **Written:** 2026-11-12 · **Repo read:** local `main` at `f6b5535`, plus `origin/main` (4 commits ahead, not pulled)

This file tells a mockup what to draw. Every on-screen value maps to a field, string or rule in the repo, or is marked as not built yet.
Links are relative to this file. A `:N` suffix names a line.

Labels used throughout:

- **Exists in code today:** a player can change or see it in a build now.
- **Value exists in Inspector only:** the value is real and tunable, but only in the Unity Editor. A settings screen needs a small bridge to expose it.
- **Needs new code:** nothing in the repo holds this value yet.

---

## 1. Facts from code

### 1.1 Canvas

| Fact | Value | Source |
|---|---|---|
| Canvas name | `GameUI`, one canvas for the menu and the HUD | [GreyboxUiSetup.cs:21](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L21) |
| Render mode | Screen Space Overlay, sorting order 10 | [GreyboxUiSetup.cs:163-164](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L163) |
| Scale mode | Scale With Screen Size | [GreyboxUiSetup.cs:166](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L166) |
| Reference resolution | **1920 × 1080** | [GreyboxUiSetup.cs:167](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L167) |
| Match width/height | 0.5 | [GreyboxUiSetup.cs:168](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L168) |
| Preview capture size | 1920 × 1080, over a mid-grey `#9EA6AD` backdrop, the worst case for the HUD | [GreyboxUiPreview.cs:18-19](../../../Assets/_Project/Editor/Greybox/GreyboxUiPreview.cs#L18), [:58](../../../Assets/_Project/Editor/Greybox/GreyboxUiPreview.cs#L58) |
| UI input | Input System UI module (`InputSystemUIInputModule`) | [GreyboxUiSetup.cs:485](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L485) |
| Menu card | 720 px wide, padding 48 left/right and 44 top/bottom, 14 px spacing, height fits its content | [GreyboxUiSetup.cs:35](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L35), [:239-241](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L239) |
| Button heights | Primary 64 px, secondary 56 px | [GreyboxUiSetup.cs:36-37](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L36) |
| Status row | 22 pt, wraps, 60 px tall: room for two lines, so the card doesn't jump | [GreyboxUiSetup.cs:248-250](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L248) |

Mockups should draw at 1920 × 1080. A status string must fit two lines at 22 pt in a 624 px text column (720 minus the padding).

### 1.2 Fonts

| Fact | Value | Source |
|---|---|---|
| Text component | TextMeshPro (`TextMeshProUGUI`) everywhere | [GreyboxUiSetup.cs:368](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L368) |
| Typeface | **TMP default: Liberation Sans** (`LiberationSans SDF`). No other font is in the repo. | [LiberationSans SDF.asset](../../../Assets/TextMesh%20Pro/Resources/Fonts%20%26%20Materials/LiberationSans%20SDF.asset), its source [LiberationSans.ttf](../../../Assets/TextMesh%20Pro/Fonts/LiberationSans.ttf) |
| Bold | Synthetic bold, with letter spacing pulled in by 2 because it spaces wide | [GreyboxUiSetup.cs:335-336](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L335) |
| Sizes in use | Title 40, status 22, button 22, players 20, subtitle/mode/hints 18, HUD menu chip 18, controls 17, network readout 16 | [GreyboxUiSetup.cs:205-206](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L205), [:243-262](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L243), [:281](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L281), [:291](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L291), [:334](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L334) |
| Overflow | No wrap with ellipsis, except the status row and hints, which wrap | [GreyboxUiSetup.cs:374-375](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L374) |

The art bible says the final typeface is still open: "Lock licensed/open fonts after testing title, microtype, UI and world signage together" ([art_bible.txt:592-593](../../Kit/concepts/Office/refs/art_bible.txt#L592)). Its hierarchy is display (geometric or techno sans), data (monospaced or technical sans), editorial (thin sans) ([art_bible.txt:411-418](../../Kit/concepts/Office/refs/art_bible.txt#L411)).

### 1.3 Colours (greybox palette, converted from code)

| Name in code | Hex | Alpha | Used for | Source |
|---|---|---|---|---|
| `Dim` | `#05080D` | 72% | Full-screen dim behind the menu; also blocks clicks | [GreyboxUiSetup.cs:26](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L26) |
| `Card` | `#171A21` | 98% | Menu card | [:27](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L27) |
| `Divider` | `#2B303B` | 100% | 1 px rules | [:28](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L28) |
| `Text` | `#F2F2F5` | 100% | Main text, crosshair dot | [:29](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L29) |
| `Muted` | `#A3ABB5` | 100% | Secondary lines, hints, controls | [:30](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L30) |
| `Primary` | `#F0B54C` (amber) | 100% | The one primary button per state | [:31](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L31) |
| `PrimaryText` | `#1A1405` | 100% | Label on the primary button | [:32](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L32) |
| `Secondary` | `#2B303B` (same value as `Divider`) | 100% | Secondary buttons | [:33](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L33) |
| Crosshair ring | `#000000` | 60% | Ring behind the crosshair dot | [:202](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L202) |
| HUD chip | `#0D0F14` | 78% | Backing behind HUD text | [:216](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L216) |
| Button tint: normal | `#DBDBDB` multiplier | 100% | Resting state, a little darker than the base colour | [:352](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L352) |
| Button tint: hover and selected | `#FFFFFF` multiplier | 100% | Hover and keyboard focus look the same | [:353](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L353), [:355](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L355) |
| Button tint: pressed | `#B2B2B2` multiplier | 100% | Pressed | [:354](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L354) |
| Button tint: disabled | `#999999` multiplier | 40% | Dimmed, not hidden | [:356](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L356) |
| Tint fade | 0.08 s | — | Transition between button states | [:358](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L358) |

**The greybox palette does not match the art bible.** The art bible's swatches are Paper White `#F3F4F4`, Silver Grey `#C9CED3`, Deep Navy `#111B38`, Corporate Cobalt `#244FB9`, Electric Blue `#2879EF`, Cyan `#20D4E6`, Near Black `#101116` ([art_bible.txt:156-182](../../Kit/concepts/Office/refs/art_bible.txt#L156)). Electric Blue is the one named for "UI highlights". Amber has no place in it, and the warning colour is still TBD ([art_bible.txt:589](../../Kit/concepts/Office/refs/art_bible.txt#L589)). The mockups should use the bible swatches. The greybox values are listed so the mockups can be compared against what ships now.

### 1.4 Crosshair today

Two flat square `Image`s, centred, no sprite ([GreyboxUiSetup.cs:201-203](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L201), drawn by `Block` at [:466-474](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L466)):

- `CrosshairRing`: 14 × 14 px, black at 60%.
- `Crosshair`: 6 × 6 px, `#F2F2F5`, on top of it.

It never changes: no spread, no hit marker, no per-weapon form. It is a centred square, not a round dot. Aim is a ray from the camera through screen centre to 200 m, skipping the Player layer, so the shot lands where the crosshair is ([PlayerCamera.cs:27](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L27), [:82-91](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L82)).

### 1.5 Menu states, labels and strings in code today

The menu is one card ([GameMenu.cs:18](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L18)). Its sections switch on `SteamLobby` state ([GameMenu.cs:138-157](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L138)).

**Always on the card**

| Element | Exact text | Source |
|---|---|---|
| Title | `CLOSE THE DEAL` (bold) | [GreyboxUiSetup.cs:243](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L243) |
| Subtitle | `Greybox playtest` | [GreyboxUiSetup.cs:244](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L244) |
| Mode line, before ready | `Starting...` | [GameMenu.cs:162](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L162) |
| Mode line, local forced | `Local test mode (forced)` | [GameMenu.cs:164](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L164) |
| Mode line, local, no Steam | `Local test mode · Steam isn't running` | [GameMenu.cs:164](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L164) |
| Mode line, Steam | `Steam · signed in as {persona}` | [GameMenu.cs:165](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L165) |
| Controls block | `WASD move    Shift sprint    Space jump    Mouse look` / `Left click blast, aimed at the crosshair` / `Climb jump at a ledge and keep holding forward    Esc menu` | [GreyboxUiSetup.cs:256-259](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L256) |

**State A: not in a game** (`StartSection`, [GreyboxUiSetup.cs:266-274](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L266))

| Element | Exact text | Shown when | Source |
|---|---|---|---|
| Primary button | `Host game` | Always; disabled until `Ready` | [GreyboxUiSetup.cs:271](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L271), [GameMenu.cs:146](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L146) |
| Hint | `To join a friend, accept their invite, or pick Join Game on their name in your Steam friends list.` | Steam mode | [GreyboxUiSetup.cs:272](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L272), [GameMenu.cs:147](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L147) |
| Secondary button | `Join game on this PC` | Local mode only; disabled until `Ready` | [GreyboxUiSetup.cs:273](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L273), [GameMenu.cs:148-149](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L148) |

**State B: in a game, menu open** (`GameSection`, [GreyboxUiSetup.cs:276-286](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L276))

| Element | Exact text | Shown when | Source |
|---|---|---|---|
| Players line | `Players  {count} / {max}` (max is 8) | Hosting or joined | [GameMenu.cs:155](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L155) |
| Secondary button | `Invite with the Steam overlay` | Only when Steam's overlay is present (not in the Editor, not outside Steam) | [GreyboxUiSetup.cs:282](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L282), [SteamLobby.cs:76](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L76) |
| Hint | `Friends can join from your name in their Steam friends list.` | In a Steam lobby | [GreyboxUiSetup.cs:283](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L283), [GameMenu.cs:152](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L152) |
| Primary button | `Back to game` | Has a spawned player | [GreyboxUiSetup.cs:284](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L284), [GameMenu.cs:153](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L153) |
| Secondary button | `Leave game` | Always in this state | [GreyboxUiSetup.cs:285](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L285) |

**State C: in game, menu closed (HUD)**: crosshair, `Esc  Menu` chip top-left at (24, −24), 150 × 40 ([GreyboxUiSetup.cs:205](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L205)), and the network readout bottom-left at (24, 24), 470 × 64 ([GreyboxUiSetup.cs:206](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L206)).

**Status line strings** (all from [SteamLobby.cs:29](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L29), shown in the card's status row):

| # | Situation | Exact string | Line |
|---|---|---|---|
| S1 | Scene wired wrong (dev) | `Network setup is out of date. Run Close the Deal > Greybox > Set Up Scene.` | [116](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L116) |
| S2 | Transport missing (dev) | `Network setup is missing {Tugboat or FishySteamworks}. Run Close the Deal > Greybox > Set Up Scene.` | [259](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L259) |
| S3 | Ready, local | `Ready. Host a game, or join one running on this PC.` | [130](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L130) |
| S4 | Ready, Steam | `Ready. Host a game, or join a friend from your Steam friends list.` | [141](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L141) |
| S5 | Hosting, local | `Hosting on this PC. A second copy can now press Join game on this PC.` | [191](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L191), [494](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L494) |
| S6 | Creating lobby | `Creating a Steam lobby...` | [195](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L195) |
| S7 | Joining, local | `Joining the game on this PC...` | [206](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L206) |
| S8 | Left by choice | `You left the game.` | [215](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L215) |
| S9 | Lobby creation failed | `Steam couldn't create a lobby ({EResult}). Check Steam is online and try again.` | [293](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L293) |
| S10 | Join refused | `Couldn't join: ` + reason J1–J4 | [310](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L310) |
| S11 | Connecting | `Connecting to {host}'s game...` | [339](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L339) |
| S12 | Joining from an invite or friends list | `Joining from Steam...` | [360](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L360) |
| S13 | Host left the lobby | `{host} closed the game.` | [377](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L377), [479](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L479) |
| S14 | Host dropped from the lobby | `Lost {host}'s game: their connection dropped.` | [377](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L377) |
| S15 | Version mismatch | `Can't join: {host} is on {version X or an older version} and you're on version {mine}. Whoever is behind needs to update.` | [436-437](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L436) |
| S16 | Join timed out (15 s) or never connected | `Couldn't reach {host}'s game. They may have left, or Steam couldn't connect you. Try joining again.` | [442](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L442), timeout [91](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L91) |
| S17 | Link lost mid-game | `Lost the connection to {host}'s game.` | [480](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L480) |
| S18 | Connected, local | `Connected to the game on this PC.` | [488](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L488) |
| S19 | Connected, Steam | `Connected to {host}'s game.` | [488](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L488) |
| S20 | Hosting, Steam | `Hosting. Invite a friend, or they can pick Join Game on your name in their Steam friends list.` | [495](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L495) |
| — | Card's first status, before `SteamLobby.Start` | `Starting...` | [GreyboxUiSetup.cs:248](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L248) |

`{host}` is the host's Steam persona name, or `the host` when unknown ([SteamLobby.cs:534-540](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L534)).

**Join refusal reasons** (`JoinFailureReason`, mapped from Steam's `EChatRoomEnterResponse`, [SteamLobby.cs:419-432](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L419)):

| # | Steam response | Exact reason text |
|---|---|---|
| J1 | `k_EChatRoomEnterResponseDoesntExist` | `that game has closed.` |
| J2 | `k_EChatRoomEnterResponseNotAllowed` | `the lobby is friends-only. Add the host as a Steam friend first.` |
| J3 | `k_EChatRoomEnterResponseFull` | `the game is full.` |
| J4 | any other value | `Steam refused ({response}). Try again.` |

**Steam rich presence** (what friends see on your name): `In {host}'s lobby ({count}/{max})` ([SteamLobby.cs:416](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L416)).

### 1.6 Key bindings (exact, from the actions asset)

Asset: [InputSystem_Actions.inputactions:5](../../../Assets/InputSystem_Actions.inputactions#L5), `Player` map, referenced by the Player prefab ([Player.prefab:282](../../../Assets/_Project/Prefabs/Player.prefab#L282)). Only Move, Look, Sprint, Jump, Attack and Lean are read ([PlayerInputReader.cs:71-77](../../../Assets/_Project/Scripts/Player/PlayerInputReader.cs#L71)).

| Action | Keyboard and mouse | Gamepad | Read by the game? | Lines |
|---|---|---|---|---|
| Move | W A S D, and the arrow keys | Left stick | Yes | [:103-202](../../../Assets/InputSystem_Actions.inputactions#L103) |
| Look | Mouse delta | Right stick | Yes | [:235](../../../Assets/InputSystem_Actions.inputactions#L235), [:246](../../../Assets/InputSystem_Actions.inputactions#L246) |
| Attack (fires the test blast) | Left mouse, **and Enter** | West face button (X / Square) | Yes | [:268](../../../Assets/InputSystem_Actions.inputactions#L268), [:279](../../../Assets/InputSystem_Actions.inputactions#L279), [:323](../../../Assets/InputSystem_Actions.inputactions#L323) |
| Sprint (hold) | Left Shift | Left stick press | Yes | [:356](../../../Assets/InputSystem_Actions.inputactions#L356), [:367](../../../Assets/InputSystem_Actions.inputactions#L367) |
| Jump (tap hops, hold for full height) | Space | South face button (A / Cross) | Yes | [:389](../../../Assets/InputSystem_Actions.inputactions#L389), [:400](../../../Assets/InputSystem_Actions.inputactions#L400) |
| Lean left / right (hold) | **Q / E** | Left / right shoulder | Yes | [:499](../../../Assets/InputSystem_Actions.inputactions#L499), [:510](../../../Assets/InputSystem_Actions.inputactions#L510), [:532](../../../Assets/InputSystem_Actions.inputactions#L532), [:543](../../../Assets/InputSystem_Actions.inputactions#L543) |
| Interact | F | North face button (Y / Triangle) | No: bound, unused | [:444](../../../Assets/InputSystem_Actions.inputactions#L444), [:455](../../../Assets/InputSystem_Actions.inputactions#L455) |
| Crouch | C | East face button (B / Circle) | No: bound, unused | [:466](../../../Assets/InputSystem_Actions.inputactions#L466), [:477](../../../Assets/InputSystem_Actions.inputactions#L477) |
| Previous | 1 | D-pad left | No: bound, unused | [:422](../../../Assets/InputSystem_Actions.inputactions#L422), [:433](../../../Assets/InputSystem_Actions.inputactions#L433) |
| Next | 2 | D-pad right | No: bound, unused | [:334](../../../Assets/InputSystem_Actions.inputactions#L334), [:345](../../../Assets/InputSystem_Actions.inputactions#L345) |
| Menu | **Esc**, hard-coded, not an action | **None** | Yes, keyboard only | [GameMenu.cs:91-93](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L91) |
| Climb | No key: jump at a ledge between 0.4 and 1.6 m and keep holding forward | — | Yes | [PlayerMotor.cs:605-618](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L605) |

The UI map navigates with WASD, the arrow keys, either stick or the d-pad, and uses the generic Submit and Cancel ([InputSystem_Actions.inputactions:663-905](../../../Assets/InputSystem_Actions.inputactions#L663)). Nothing listens for Cancel, so a pad cannot open or close the menu.

### 1.7 Camera and look

| Field | Live value | Code default | Range | Source |
|---|---|---|---|---|
| Field of view | **60° vertical** (about 91° horizontal at 16:9), both scenes | Camera component | none set | [Greybox.unity:1357](../../../Assets/_Project/Scenes/Greybox.unity#L1357), `KitOffice.unity` line 34395, which exists only on `origin/main` (local main is behind) |
| Near clip | 0.3 m | Camera component | — | [Greybox.unity:1355](../../../Assets/_Project/Scenes/Greybox.unity#L1355) |
| `PlayerCamera._sensitivity` | 0.12 degrees per mouse unit (both scenes) | 0.12 | **No `[Range]`** | [PlayerCamera.cs:18-19](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L18), [Greybox.unity:1450](../../../Assets/_Project/Scenes/Greybox.unity#L1450) |
| `PlayerCamera._pitchLimits` | **Greybox: −35° up / 70° down.** KitOffice (origin/main only, line 34441): −85° / 85° | −85 / 85 | Keep inside ±89 (tooltip) | [PlayerCamera.cs:21-22](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L21), [Greybox.unity:1451](../../../Assets/_Project/Scenes/Greybox.unity#L1451) |
| `PlayerCamera._wallClearance` | Greybox 0.25 m | 0.2 m | Above near clip | [PlayerCamera.cs:24-25](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L24), [Greybox.unity:1449](../../../Assets/_Project/Scenes/Greybox.unity#L1449) |
| Eye height | 1.6 m (on the blast profile) | 1.6 | — | [BlastProfile.cs:36-37](../../../Assets/_Project/Scripts/Combat/BlastProfile.cs#L36), [TestBlast.asset:22](../../../Assets/_Project/Profiles/TestBlast.asset#L22) |
| Invert Y | Not present. Pitch is `pitch − look.y` | — | — | [PlayerCamera.cs:102](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L102) |

Mouse look applies only while the cursor is locked ([PlayerCamera.cs:98](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L98)).
**The stick uses the mouse formula**: stick value × 0.12 per frame, with no time scaling ([PlayerCamera.cs:100](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L100)). At 60 fps a full stick turns about 7° per second, so gamepad look is effectively unusable today.

### 1.8 Lean

| Fact | Value | Source |
|---|---|---|
| Keys | Hold Q (left) / E (right); both held cancel | [PlayerInputReader.cs:56-67](../../../Assets/_Project/Scripts/Player/PlayerInputReader.cs#L56) |
| Distance | 0.45 m | [MovementProfile.cs:57-58](../../../Assets/_Project/Scripts/Player/MovementProfile.cs#L57), [DefaultMovement.asset:29](../../../Assets/_Project/Profiles/DefaultMovement.asset#L29) |
| Tilt | 12° | [MovementProfile.cs:60-61](../../../Assets/_Project/Scripts/Player/MovementProfile.cs#L60) |
| Speed | 6 full leans per second | [MovementProfile.cs:63-64](../../../Assets/_Project/Scripts/Player/MovementProfile.cs#L63) |
| Only on the floor | Jumping or a blast straightens you | [PlayerMotor.cs:582-586](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L582) |
| Mode | Hold only; no toggle | [PlayerInputReader.cs:64-65](../../../Assets/_Project/Scripts/Player/PlayerInputReader.cs#L64) |

These are designer-owned profile values (AGENTS.md §4): a settings screen must not write them.

### 1.9 Other tunables a settings screen would own

| Value | Live value | Where | Source |
|---|---|---|---|
| Stick dead zone | 0.1, range 0–0.5 | Player prefab, `PlayerInputReader._deadZone` | [PlayerInputReader.cs:19-20](../../../Assets/_Project/Scripts/Player/PlayerInputReader.cs#L19), [Player.prefab:283](../../../Assets/_Project/Prefabs/Player.prefab#L283) |
| Network readout refresh | 0.1 s | `PredictionDebugHud._interval` | [PredictionDebugHud.cs:37-38](../../../Assets/_Project/Scripts/UI/PredictionDebugHud.cs#L37) |
| vSync | On at the PC quality level (`vSyncCount: 1`), off at Mobile | QualitySettings | [QualitySettings.asset:86](../../../ProjectSettings/QualitySettings.asset#L86), [:32](../../../ProjectSettings/QualitySettings.asset#L32) |
| Quality levels | `Mobile`, `PC`; current index 1 (PC) | QualitySettings | [QualitySettings.asset:7-10](../../../ProjectSettings/QualitySettings.asset#L7), [:64](../../../ProjectSettings/QualitySettings.asset#L64) |
| Display mode | Full-screen window (`fullscreenMode: 1`), not resizable | Player settings | [ProjectSettings.asset:98](../../../ProjectSettings/ProjectSettings.asset#L98), [:110](../../../ProjectSettings/ProjectSettings.asset#L110) |
| Default window size | 1024 × 768 | Player settings | [ProjectSettings.asset:44-45](../../../ProjectSettings/ProjectSettings.asset#L44) |
| Game version shown in mismatch errors | `0.1.0` (`Application.version`) | Player settings | [ProjectSettings.asset:146](../../../ProjectSettings/ProjectSettings.asset#L146), [SteamLobby.cs:299](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L299) |
| Max players | 8 | `SteamLobby._maxPlayers` | [SteamLobby.cs:31-32](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L31), [Greybox.unity:3476](../../../Assets/_Project/Scenes/Greybox.unity#L3476) |
| Force local test mode | Off; also `-local` on the command line | `SteamLobby._forceLocalTestMode` | [SteamLobby.cs:34-35](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L34), [:123](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L123) |
| Audio | **No AudioMixer and no audio files in the repo** | — | `git ls-files` finds no `.mixer`, `.wav`, `.ogg` or `.mp3` |

### 1.10 Game state a UI could show (what exists)

| State | Field | Synced? | Source |
|---|---|---|---|
| Local player exists | `PlayerMotor.Local` | Local | [PlayerMotor.cs:162](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L162) |
| Move state: Grounded, Airborne, Mantling, Knocked | `PlayerMotor.State` (`MoveState`) | Host → all, every tick | [PlayerMotor.cs:11-17](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L11), [:185](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L185) |
| Control loss after a blast | 0.7 s (`ControlLossSeconds`) | Inside the reconcile | [BlastProfile.cs:27-28](../../../Assets/_Project/Scripts/Combat/BlastProfile.cs#L27) |
| Speed | `PlayerMotor.PlanarSpeed` (walk 6, sprint 9 m/s) | Local | [PlayerMotor.cs:187](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L187) |
| Lean | `PlayerMotor.Lean`, −1 to +1 | Reconcile | [PlayerMotor.cs:191](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L191) |
| Corrections | `LastCorrectionMetres`, `CorrectionCount` | Local | [PlayerMotor.cs:197-200](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L197) |
| Ping | `TimeManager.RoundTripTime` | Local | [PredictionDebugHud.cs:69](../../../Assets/_Project/Scripts/UI/PredictionDebugHud.cs#L69) |
| Players hit by a blast | `Knockback.Explode` returns a count; the caller throws it away | Host only | [Knockback.cs:16](../../../Assets/_Project/Scripts/Combat/Knockback.cs#L16), [PlayerMotor.cs:665](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L665) |
| Locked door damage | `BreakableDoor._hits` vs `_hitsToBreak` (3, range 1–20), `IsBroken` | Host SyncVar | [BreakableDoor.cs:15-23](../../../Assets/_Project/Scripts/Tower/BreakableDoor.cs#L15) |
| Tower seed | `TowerBuilder.Seed` | Host SyncVar | [TowerBuilder.cs:31](../../../Assets/_Project/Scripts/Tower/TowerBuilder.cs#L31), [:41](../../../Assets/_Project/Scripts/Tower/TowerBuilder.cs#L41) |
| Floor stack | Lobby, 9 offices with a checkpoint after every 3, boardroom, roof = **15 floors**, index 0 to 14 | Built locally from the seed | [TowerLayout.cs:19-46](../../../Assets/_Project/Scripts/Tower/TowerLayout.cs#L19), [DefaultTower.asset:15](../../../Assets/_Project/Profiles/DefaultTower.asset#L15) |
| Floor kinds | `Lobby`, `Office`, `Checkpoint`, `Boardroom`, `Roof` | — | [FloorTemplate.cs:5-12](../../../Assets/_Project/Scripts/Tower/FloorTemplate.cs#L5) |
| Floor height | 4.5 m (the asset; code default 4) | — | [DefaultTower.asset:18](../../../Assets/_Project/Profiles/DefaultTower.asset#L18), [TowerProfile.cs:25](../../../Assets/_Project/Scripts/Tower/TowerProfile.cs#L25) |
| Team | `ClientId % 2`, worked out on the host at spawn and **not synced** | Host only | [TeamSpawner.cs:65](../../../Assets/_Project/Scripts/Tower/TeamSpawner.cs#L65) |
| Building names | `Building A`, `Building B` (object names) | — | [TowerBuilder.cs:112](../../../Assets/_Project/Scripts/Tower/TowerBuilder.cs#L112) |
| Lobby membership | `PlayerCount`, `MaxPlayers`, `IsHosting`, `InLobby`, `LocalMode`, `Ready` | Steam / FishNet | [SteamLobby.cs:43-76](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L43) |
| Props | `PropSpawner.SpawnedCount` | Host | [PropSpawner.cs:20](../../../Assets/_Project/Scripts/Props/PropSpawner.cs#L20) |

Not in the repo at all: health, death, respawn, a match clock, checkpoint reached, rival progress, weapons, ammo, pickups, a secretary, a results screen, player names or team markers, audio.

---

## 2. Screen list for v1

Scope from [GDD.md:170-185](../../../docs/GDD.md#L170). The **Built** column is about the screen, not its parts.

| # | Screen | Built? | What it is for | GDD source |
|---|---|---|---|---|
| 1 | Title / main menu | Partly: the State A card | Host, join, settings, quit. Title and the line "Terms and Conditions Apply." | Art bible §13 |
| 2 | Hosting and joining | Partly: status row, State B | Create the Steam lobby, join from an invite or the friends list, and every wait and failure in between | [GDD.md:174](../../../docs/GDD.md#L174), [NETCODE.md:14](../../../docs/systems/NETCODE.md#L14) |
| 3 | Reception (lobby) | No | Diegetic: your team's ground floor with the secretary's desk. Screen-space overlay: roster for both buildings, invite, ready state, and the host's start, which is talking to the secretary | [GDD.md:133-140](../../../docs/GDD.md#L133) |
| 4 | Settings | No | Tabs: Controls, Video, Audio, Accessibility (rows in §2.1) | MENU_AND_HUD known limitations |
| 5 | In-game menu (does not pause) | Partly: State B | Back to game, settings, invite, leave. The game keeps running behind it | [MENU_AND_HUD.md:9](../../../docs/systems/MENU_AND_HUD.md#L9) |
| 6 | In-game HUD | Partly: crosshair, Esc chip, debug readout | §3 | — |
| 7 | Down and respawn | No | Killed: back on the same floor in 5 s, with a countdown | [GDD.md:51](../../../docs/GDD.md#L51) |
| 8 | Window exit | No | Blown out of a window: the fall plays, then back to the team's last checkpoint | [GDD.md:52](../../../docs/GDD.md#L52) |
| 9 | Checkpoint banner | No | Progress saved, and the rival's floor is revealed | [GDD.md:33](../../../docs/GDD.md#L33), [:177](../../../docs/GDD.md#L177) |
| 10 | Rival progress readout | No | The rival's highest floor, as revealed at checkpoints | [GDD.md:63](../../../docs/GDD.md#L63) |
| 11 | Match clock | No | Target 12:00, hard cap 15:00, at which the highest team wins | [GDD.md:40-50](../../../docs/GDD.md#L40) |
| 12 | Results and rematch | No | Winner, how (roof or cap), time, per-player lines; Rematch / Return to reception | [GDD.md:183](../../../docs/GDD.md#L183) |
| 13 | Connection lost and failed join | Partly: status row only | S8–S17 and J1–J4, with a clear next action | [NETCODE.md:14](../../../docs/systems/NETCODE.md#L14) |
| 14 | Leave confirmation | No | Leaving as host ends the run for everyone ([GDD.md:160](../../../docs/GDD.md#L160)), so it needs a confirm | — |

### 2.1 Settings rows

Ranges marked *proposed* are mine; no range exists in code. The FOV default is the live value.

**Controls**

| Row | Control | Range | Default | Existence |
|---|---|---|---|---|
| Mouse sensitivity | Slider with a number field | *proposed* 0.02–0.50 (degrees per mouse unit) | 0.12 | **Value exists in Inspector only** (`PlayerCamera._sensitivity`, [PlayerCamera.cs:19](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L19)) |
| Invert vertical look | Toggle | On / off | Off | **Needs new code** |
| Controller look speed | Slider | *proposed* 60–360 °/s at full tilt | *proposed* 180 | **Needs new code** (stick look has no time scaling today, [PlayerCamera.cs:100](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L100)) |
| Controller dead zone | Slider | 0–0.5 (from the `[Range]`) | 0.1 | **Value exists in Inspector only** (`PlayerInputReader._deadZone`, [PlayerInputReader.cs:20](../../../Assets/_Project/Scripts/Player/PlayerInputReader.cs#L20)) |
| Lean | Segmented: Hold / Toggle | — | Hold | **Needs new code** (hold only today) |
| Sprint | Segmented: Hold / Toggle | — | Hold | **Needs new code** |
| Key bindings | List of actions, each with a keyboard and a pad slot; Reset to default | Actions in §1.6 | §1.6 | **Needs new code** (the Input System supports rebinding; nothing saves a binding) |

**Video**

| Row | Control | Range | Default | Existence |
|---|---|---|---|---|
| Field of view | Slider, labelled in **horizontal** degrees | *proposed* 80–110 horizontal (about 51–77 vertical at 16:9) | 91 horizontal (60 vertical) | **Value exists in Inspector only** (Camera FOV, [Greybox.unity:1357](../../../Assets/_Project/Scenes/Greybox.unity#L1357)) |
| Display mode | Dropdown: Full screen, Borderless window, Windowed | — | Borderless window (the current `fullscreenMode: 1`) | **Value exists in Inspector only** ([ProjectSettings.asset:110](../../../ProjectSettings/ProjectSettings.asset#L110)) |
| Resolution | Dropdown of the monitor's modes | Monitor's list | Desktop resolution | **Value exists in Inspector only** (default 1024 × 768, [ProjectSettings.asset:44-45](../../../ProjectSettings/ProjectSettings.asset#L44)) |
| Vertical sync | Toggle | On / off | On | **Value exists in Inspector only** ([QualitySettings.asset:86](../../../ProjectSettings/QualitySettings.asset#L86)) |
| Frame rate limit | Dropdown: 60, 120, 144, 240, None | — | None (vSync governs) | **Needs new code** |
| Graphics quality | Dropdown: Low, High | Maps to `Mobile`, `PC` | High | **Value exists in Inspector only** ([QualitySettings.asset:7](../../../ProjectSettings/QualitySettings.asset#L7)) |
| Screen shake | Slider | 0–100% | 100% | **Needs new code** (no shake yet; v1 scope lists it, [GDD.md:184](../../../docs/GDD.md#L184)) |

**Audio**

| Row | Control | Range | Default | Existence |
|---|---|---|---|---|
| Master volume | Slider | 0–100% | 80% | **Needs new code** (no mixer) |
| Effects | Slider | 0–100% | 100% | **Needs new code** |
| Music | Slider | 0–100% | 70% | **Needs new code** |
| Secretary | Slider | 0–100% | 100% | **Needs new code** |
| Play audio when the window is in the background | Toggle | On / off | Off | **Needs new code** |

**Accessibility and interface**

| Row | Control | Range | Default | Existence |
|---|---|---|---|---|
| Secretary subtitles | Toggle | On / off | On | **Needs new code** |
| Interface size | Slider | 80–130% | 100% | **Needs new code** (CanvasScaler is fixed at 1920 × 1080, match 0.5) |
| Crosshair style | Segmented: Dot, Cross, Circle | — | Dot | **Needs new code** |
| Crosshair colour | Swatches: Paper White, Cyan, Electric Blue | — | Paper White | **Needs new code** |
| Crosshair size | Slider | 50–200% | 100% | **Needs new code** |
| Show network stats | Toggle | On / off | Off | **Needs new code** (the readout exists, [PredictionDebugHud.cs:13](../../../Assets/_Project/Scripts/UI/PredictionDebugHud.cs#L13), but it is always on and has no toggle) |

Settings that change rules (lean distance, tilt, pitch limits) stay off this screen: they are designer-owned (AGENTS.md §4).

---

## 3. HUD elements

Netcode rule for every row: the host owns outcomes; clients only show them ([AGENTS.md:68](../../../AGENTS.md#L68)).
**Event** = redraw when the value changes. **Per frame** = must track motion.

| Element | What it shows | Data source | Update | Priority |
|---|---|---|---|---|
| Crosshair | Aim point | Exists: `Crosshair` and `CrosshairRing` ([GreyboxUiSetup.cs:202-203](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L202)); aim from `PlayerCamera.AimDirectionFrom` ([PlayerCamera.cs:82](../../../Assets/_Project/Scripts/Player/PlayerCamera.cs#L82)) | Static; form changes on weapon switch (event) | Always on |
| Hit marker | Your shot hit a player | Not built yet. Host decides the hit; `Knockback.Explode` already returns the count ([Knockback.cs:16](../../../Assets/_Project/Scripts/Combat/Knockback.cs#L16)), and the host would send it to the shooter in a target RPC | Event | Contextual, 0.2 s |
| Weapon and ammo | Pistol/rifle rounds, rocket count, throwables held, zipline gun charge | Not built yet. Today only the test blast exists ([BlastProfile.cs:10](../../../Assets/_Project/Scripts/Combat/BlastProfile.cs#L10)). Host owns ammo and pickups; the owner predicts its own ammo count | Event | Always on (small, bottom-right) |
| Weapon slots | Which of the four is held: 1 Pistol/rifle, 2 Rocket launcher, 3 Throwable, 4 Zipline gun | Not built yet. Previous / Next are bound to 1 / 2 and the d-pad, but unused ([inputactions:334](../../../Assets/InputSystem_Actions.inputactions#L334), [:422](../../../Assets/InputSystem_Actions.inputactions#L422)) | Event | Contextual: shown on switch, then fades |
| Health | Damage before you go down | Not built yet; the GDD has deaths but no damage model. Host owns damage | Event | Contextual: a thin bar that appears only when damaged and fades at full health `[Owner]` (open question 2) |
| Knocked | You're airborne after a blast and can't steer | `PlayerMotor.State == MoveState.Knocked` ([PlayerMotor.cs:185](../../../Assets/_Project/Scripts/Player/PlayerMotor.cs#L185)) for 0.7 s ([BlastProfile.cs:28](../../../Assets/_Project/Scripts/Combat/BlastProfile.cs#L28)) | Event on state change | Contextual; edge treatment, no text |
| Match clock | `mm:ss` elapsed toward 15:00; warning from 12:00 | Not built yet. Host owns the start tick in a match-state network object; clients count locally from it | Per second | Always on, top centre |
| Current floor | `Floor 7` and its kind | Not built yet. Can be derived locally from height ÷ floor height (4.5 m) and the seeded stack ([TowerLayout.cs:19](../../../Assets/_Project/Scripts/Tower/TowerLayout.cs#L19)); display only, decides nothing | Event on floor change | Always on, next to the clock |
| Rival progress | The rival's highest floor at the moment of reveal | Not built yet. The host owns each team's highest checkpoint; sent when a team reaches a checkpoint | Event | **Transition only** in the checkpoint banner, plus a small stale readout under the clock, `Rival: floor {m} · {mm:ss} ago` `[Owner]` (open question 1) |
| Checkpoint banner | `Checkpoint · Floor {n}` plus the rival's floor | Not built yet. `FloorKind.Checkpoint` exists ([FloorTemplate.cs:9](../../../Assets/_Project/Scripts/Tower/FloorTemplate.cs#L9)); the host decides when a team reaches it | Event | Transition only, about 3 s |
| Secretary subtitle | Her line as text | Not built yet; the host triggers lines on run events | Event | Contextual, bottom centre |
| Respawn countdown | `Back on the floor in 5` | Not built yet; the host owns death and the 5 s timer | Per second | Transition only |
| Window exit | `Returning to Checkpoint · Floor {n}` | Not built yet; the host detects the exit | Event | Transition only |
| Door strength | Hits left on a locked door you're looking at | `BreakableDoor._hits` / `_hitsToBreak` (3), synced ([BreakableDoor.cs:16-19](../../../Assets/_Project/Scripts/Tower/BreakableDoor.cs#L16)) | Event on hit | Contextual: while aimed at a door |
| Interact prompt | `F Speak to reception` (the secretary), pick up | Not built yet. Interact is bound to F and north face, unused ([inputactions:444](../../../Assets/InputSystem_Actions.inputactions#L444)) | Event on look-at | Contextual |
| Team markers | Teammate names over heads, through walls | Not built yet. Team is host-only today ([TeamSpawner.cs:65](../../../Assets/_Project/Scripts/Tower/TeamSpawner.cs#L65)) and needs syncing | Per frame (screen position) | Always on for teammates |
| Connection warning | Ping high or the host isn't answering | `TimeManager.RoundTripTime` exists ([PredictionDebugHud.cs:69](../../../Assets/_Project/Scripts/UI/PredictionDebugHud.cs#L69)); the threshold is not built | Event, checked about once a second | Contextual |
| Network stats | State, speed, ping, corrections | Exists: `PredictionDebugHud` ([PredictionDebugHud.cs:73-80](../../../Assets/_Project/Scripts/UI/PredictionDebugHud.cs#L73)) | Up to 10 per second, only on change | Off by default (setting) |
| Menu hint | `Esc Menu` | Exists ([GreyboxUiSetup.cs:205](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L205)) | Static | Cut from the final HUD; it is in the controls list |

Lean has no HUD element: the 12° camera tilt already shows it.
The noise budget, from the art bible: "small high-contrast information blocks"; big graphics only for transitions, results and the lobby ([art_bible.txt:433-434](../../Kit/concepts/Office/refs/art_bible.txt#L433)).

---

## 4. States

### 4.1 Per screen

| Screen | First run | Loading / connecting | Empty | Failure | Offline / local | Paused | Quit confirm | Resume |
|---|---|---|---|---|---|---|---|---|
| Title | Settings at defaults; one line: `Esc opens the menu during a game.` | `Starting...` until `Ready` ([GameMenu.cs:161-162](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L161)); Host disabled | — | S1/S2 are dev-only; in a shipped build, show "Unable to start the network. Restart the game." | Mode line says local; `Join game on this PC` replaces the friends hint | — | `Quit to desktop?` | — |
| Hosting / joining | — | S6, S11, S12, S7; join gives up at 15 s ([SteamLobby.cs:91](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L91)) | No friends online: the invite overlay is Steam's own; we show only the friends-list hint | S9, J1–J4, S15, S16 | Local join only; no invite | — | Cancel joining = Leave | — |
| Reception | Teach Interact at the desk | Floors build from the seed ([TowerBuilder.cs:63-69](../../../Assets/_Project/Scripts/Tower/TowerBuilder.cs#L63)) | Host alone: roster shows one name; Start stays available (1v0 is a test) or disabled; owner decision (open question 4) | A player drops: roster row goes; S13/S14/S17 for clients if the host goes | Local: no invite button | Menu opens over it | Host leaving ends it for all | Back to game |
| In-game menu | — | — | — | — | — | **Never pauses**: says the game continues | Leave confirm (§5) | Back to game, or Esc |
| HUD | Controls shown once in reception, not on the HUD | Spawning: no HUD until `PlayerMotor.Local` exists ([GameMenu.cs:80](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L80)) | No weapon held: ammo block hidden | Connection warning chip | Same HUD | Menu open hides the HUD ([GameMenu.cs:110](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L110)) | — | HUD back, cursor locked |
| Results | — | Waiting for the host's rematch | — | Host left during results: S13, back to title | Same | — | Return to reception | — |
| Settings | Defaults | — | — | Unsupported resolution: revert after 10 s unless kept | — | Opening it from the in-game menu does not pause | Unsaved changes: `Apply changes?` | — |

Rule kept from the code: **menu open ⇔ mouse free ⇔ player idle** ([GameMenu.cs:106-116](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L106), [PlayerInputReader.cs:32](../../../Assets/_Project/Scripts/Player/PlayerInputReader.cs#L32)). If the cursor is freed elsewhere (alt-tab), the menu opens to match ([GameMenu.cs:94-95](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L94)).

### 4.2 Per control

| State | Today ([GreyboxUiSetup.cs:348-360](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L348)) | Draw in the mockups |
|---|---|---|
| Default | 86% tint of the base colour | Hairline outline, label in Near Black on Paper White |
| Hover | Full colour | Electric Blue left rule (4 px) slides in, about 120 ms |
| Focus (keyboard or pad) | **Same as hover** | Must differ from hover: a 2 px Electric Blue outline offset 3 px, so the pad user can always find focus after the mouse moves |
| Pressed | 70% tint | Fill Corporate Cobalt, label Paper White, about 1 px down |
| Disabled | 60% tint at 40% alpha | Silver Grey label, no rule; the reason in the status row (for example `Starting...`) |

### 4.3 Focus order

Today, focus lands on `Back to game` in game, on `Host game` before a game, and on `Leave game` when in a lobby without a player ([GameMenu.cs:119-128](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L119)). Navigation is Unity's automatic order.

| Screen | Order (first = focused on open) |
|---|---|
| Title, Steam | Host game → Settings → Quit to desktop |
| Title, local | Host game → Join on this PC → Settings → Quit to desktop |
| In-game menu | Back to game → Invite friends (when shown) → Settings → Leave game |
| Leave confirm | **Stay** (safe default) → Leave game / End game for everyone |
| Settings | Tab row (LB/RB or Q/E switch tabs) → rows top to bottom → Apply → Back |
| Results | Rematch (host) / Ready for rematch (client) → Return to reception |

Pad gaps: no pause button, and Cancel (B / Circle) does nothing ([GameMenu.cs:91-93](../../../Assets/_Project/Scripts/UI/GameMenu.cs#L91)). The mockups should assume Start opens and closes the menu, and B goes back one level.

---

## 5. Copy

Voice: straight-faced corporate and legal. The fiction lives in headings, microtype and the secretary; buttons stay plain verbs so a stranger can act on them. Errors stay plain and say how to recover. Sentence case for buttons and body; capitals only for the title, small section labels and transition headlines (banners such as `DEAL CLOSED`).
Steam's own button is called **Join Game**; keep that exact name wherever we point to it.
`[Owner]` marks a design decision for Adam, not settled.

### 5.1 Title

| Element | Final copy |
|---|---|
| Title | `CLOSE THE DEAL` (working title, GDD; the art bible marks it locked) `[Owner]` |
| Line under it | `Terms and Conditions Apply.` |
| Microtype corner | `v0.1.0 · Early Access` (from `Application.version`) |
| Mode line, Steam | `Signed in to Steam as {persona}` |
| Mode line, local | `Local test mode. Steam is not running.` |
| Mode line, forced local | `Local test mode` |
| Buttons | `Host game` · `Join on this PC` (local only) · `Settings` · `Quit to desktop` |
| Join hint | `To join a friend, accept their invite or choose Join Game on their name in Steam.` |
| Ready (Steam) | `Ready. Host a game, or join a friend from Steam.` |
| Ready (local) | `Ready. Host a game, or join one running on this PC.` |
| Starting | `Starting...` |
| Network broken (replaces S1/S2 in a shipped build) | `Unable to start the network. Restart the game.` |
| Quit confirm | Title `Quit to desktop?` · Buttons `Quit to desktop` / `Stay` |

### 5.2 Hosting and joining

| Situation | Final copy |
|---|---|
| Creating | `Opening a lobby on Steam...` |
| Hosting, Steam | `Hosting. Invite friends, or they can choose Join Game on your name in Steam.` |
| Hosting, local | `Hosting on this PC. Start a second copy and choose Join on this PC.` |
| Joining from Steam | `Joining from Steam...` |
| Joining, local | `Joining the game on this PC...` |
| Connecting | `Connecting to {host}'s game...` |
| Connected | `Connected to {host}'s game.` |
| Invite button | `Invite friends` |
| Players line | `{count} of {max} seats filled` |

### 5.3 Failures (plain; each names the fix)

| Code | Final copy |
|---|---|
| S9 | `Steam could not open a lobby. Check that Steam is online, then host again.` |
| J1 | `Unable to join. That game has closed.` |
| J2 | `Unable to join. The lobby is friends-only: add {host} as a Steam friend first.` |
| J3 | `Unable to join. The game is full ({max} players).` |
| J4 | `Unable to join. Steam refused the request ({response}). Try again.` |
| S15 | `Unable to join. {host} is on version {theirs}, you are on {mine}. Whoever is behind needs to update.` |
| S16 | `Unable to reach {host}'s game. They may have left. Try joining again.` |
| S13 | `{host} closed the game. The run has ended.` |
| S14 | `Lost {host}'s game. Their connection dropped.` |
| S17 | `Lost the connection to {host}'s game.` |
| S8 | `You left the game.` |

J2 must not interpolate `{host}` until the lobby data is read; if the name is unknown it falls back to `the host` ([SteamLobby.cs:536-537](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L536)).

### 5.4 Reception

| Element | Final copy |
|---|---|
| Section label | `RECEPTION` |
| Roster headings | `{Company A}` / `{Company B}`: firm names are `[Owner]`; until named, the code's `Building A` / `Building B` |
| Empty seat | `Seat available` |
| Interact prompt (host) | `F  Begin the run` |
| Interact prompt (client) | `F  Speak to reception` |
| Client waiting | `Waiting for {host} to begin the run.` |
| Secretary, run start | `Good morning. Your equipment is at the desk. The first team to reach the roof closes the deal.` |

### 5.5 In-game menu

| Element | Final copy |
|---|---|
| Heading | `MENU` |
| Line under it | `The game continues while this is open.` |
| Buttons | `Back to game` · `Invite friends` · `Settings` · `Leave game` |
| Leave confirm, client | Title `Leave {host}'s game?` · Body `Your team carries on without you.` · Buttons `Leave game` / `Stay` |
| Leave confirm, host | Title `End the game for everyone?` · Body `You are the host. Leaving sends all {count} players back to the title.` · Buttons `End game for everyone` / `Stay` |

### 5.6 HUD and transitions

| Element | Final copy |
|---|---|
| Clock | `12:34` (no label) |
| Clock warning, from 12:00 | `Quarter closes in 3:00` |
| Floor | `FLOOR 7` (lobby `LOBBY`, then `BOARDROOM`, `ROOF`) |
| Checkpoint banner | `CHECKPOINT · FLOOR {n}` / `Progress saved. {Rival} is on floor {m}.` |
| Secretary, checkpoint | `Floor {n}. Progress has been recorded.` |
| Respawn | `Back on the floor in {s}` |
| Secretary, down | `An employee is unavailable. Returning in {s} seconds.` (`{s}` reads the host's respawn timer, 5 today) |
| Window exit | `LEFT THE BUILDING` / `Returning to Checkpoint · Floor {n}` |
| Secretary, window exit | `An employee has left the building. Please return to your last checkpoint.` |
| Door | `Locked door · {hits left} of {total}` |
| Connection warning | `Connection unstable` |
| Weapon names | `Pistol` (or `Rifle`), `Rocket launcher`, `Explosive`, `Zipline gun` (pistol vs rifle is `[Owner]`) |

### 5.7 Results

| Element | Final copy |
|---|---|
| Win, roof | `DEAL CLOSED` / `{Team} reached the roof at {mm:ss}.` |
| Win, cap | `QUARTER CLOSED` / `{Team} finished highest, on floor {n}.` |
| Your result line | `Your team won.` / `Your team lost.` |
| Secretary, win | `Congratulations. Your quarter has closed ahead of target.` |
| Secretary, loss | `The deal has gone to another firm. Please clear your desk.` |
| Buttons, host | `Rematch` · `Return to reception` |
| Buttons, client | `Ready for rematch` · `Leave game` |
| Waiting | `Waiting for {host} to start the rematch.` |

### 5.8 Settings

| Element | Final copy |
|---|---|
| Heading | `SETTINGS` |
| Tabs | `Controls` · `Video` · `Audio` · `Accessibility` |
| Rows | As named in §2.1 |
| Footer buttons | `Apply` · `Reset to defaults` · `Back` |
| Unsaved | `Apply changes?` · `Apply` / `Discard` |
| Resolution revert | `Keep this resolution? Reverting in {s}.` · `Keep` / `Revert` |
| Rebind prompt | `Press a key for {action}. Esc cancels.` |
| Rebind clash | `{key} is already used for {other}. Assigning it here clears it there.` · `Assign` / `Cancel` |

---

## 6. Doc and code mismatches

| # | Mismatch | Which side looks right |
|---|---|---|
| 1 | **Release dates have passed.** [AGENTS.md:59-60](../../../AGENTS.md#L59) and [GDD.md:9](../../../docs/GDD.md#L9) say release Thu 29 Oct 2026 and store page 7 Oct; today is 12 Nov. Every system doc says "Last updated 2026-10-03". `git tag --list` is empty, so the repo does not show whether the game shipped. | Neither can be confirmed from the repo; the owner must restate the deadline. |
| 2 | **Lobby size.** [NETCODE.md:9](../../../docs/systems/NETCODE.md#L9) says up to 4 players; code, scene and GDD say 8 ([SteamLobby.cs:32](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L32), [GDD.md:14](../../../docs/GDD.md#L14)). | Code. |
| 3 | UI preview shows `Players  1 / 4` ([GreyboxUiPreview.cs:103](../../../Assets/_Project/Editor/Greybox/GreyboxUiPreview.cs#L103)). | Code (8); the preview is stale. |
| 4 | **Weapons.** Art bible §16 lists Pistol, Sniper, Bazooka-type, Fire extinguisher/smoke ([art_bible.txt:446-473](../../Kit/concepts/Office/refs/art_bible.txt#L446)); GDD v1 lists Pistol/rifle, Rocket launcher, Throwable explosive, Zipline gun ([GDD.md:111-116](../../../docs/GDD.md#L111)). | GDD (it owns design, AGENTS.md §7). HUD mockups follow the GDD. |
| 5 | **Title.** Art bible marks `CLOSE THE DEAL` LOCKED ([art_bible.txt:405-406](../../Kit/concepts/Office/refs/art_bible.txt#L405)); GDD says working title, not final ([GDD.md:16](../../../docs/GDD.md#L16), [:221](../../../docs/GDD.md#L221)). | GDD, until the owner confirms. |
| 6 | **Greybox camera still has third-person values**: pitch −35/70, wall clearance 0.25, and dead `_distance`/`_pivotHeight`/`_shoulderOffset` fields ([Greybox.unity:1444-1451](../../../Assets/_Project/Scenes/Greybox.unity#L1444)). In first person you can only look 35° up there. KitOffice has −85/85. | Probably code default −85/85; reported, not changed (AGENTS.md §4). |
| 7 | Controls block omits lean (Q/E) ([GreyboxUiSetup.cs:256-259](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L256)); lean was decided 2026-10-03 ([GDD.md:224](../../../docs/GDD.md#L224)). | GDD; the card is stale. |
| 8 | [MENU_AND_HUD.md:17](../../../docs/systems/MENU_AND_HUD.md#L17) quotes `Joining lobby…`; the code says `Joining from Steam...` ([SteamLobby.cs:360](../../../Assets/_Project/Scripts/Net/SteamLobby.cs#L360)). The doc uses `…`, code uses `...`. Doc says `Esc · Menu`; code says `Esc  Menu`. | Code. |
| 9 | Crosshair comment says "white dot with a dark ring"; it is two squares ([GreyboxUiSetup.cs:201-203](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L201)). | Code is what ships; the comment overstates it. |
| 10 | `GreyboxUiPreview` summary says three states; it renders four ([GreyboxUiPreview.cs:10](../../../Assets/_Project/Editor/Greybox/GreyboxUiPreview.cs#L10), [:66-73](../../../Assets/_Project/Editor/Greybox/GreyboxUiPreview.cs#L66)). | Code. |
| 11 | `PlayerInputReader` summary mentions "typing a lobby code" ([PlayerInputReader.cs:11](../../../Assets/_Project/Scripts/Player/PlayerInputReader.cs#L11)); lobby codes were removed. | Code behaviour; the comment is stale. |
| 12 | **Rebuild Game UI only targets Greybox.unity** ([GreyboxUiSetup.cs:20](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L20), [:49-50](../../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs#L49)), but origin/main ("Make the KitOffice scene playable") now puts its own `GameUI` in KitOffice. A UI rebuild leaves KitOffice's copy stale. | Tool needs to cover both scenes, or the UI should be a prefab. |
| 13 | GDD loop step 2 mentions keycards ([GDD.md:31](../../../docs/GDD.md#L31)); v1 scope does not list them and access cards are roadmap ([GDD.md:127](../../../docs/GDD.md#L127)). | v1 scope list. No keycard HUD in v1 mockups. |
| 14 | `MovementProfile` C# defaults (jump 1.3 m, air steering 12) differ from the asset and [PLAYER_MOVEMENT.md:68](../../../docs/systems/PLAYER_MOVEMENT.md#L68) (1.6 m, 20) ([MovementProfile.cs:32-35](../../../Assets/_Project/Scripts/Player/MovementProfile.cs#L32)). | The asset and doc; the defaults only affect a new profile. |
| 15 | Enter is bound to Attack ([inputactions:323](../../../Assets/InputSystem_Actions.inputactions#L323)) and is also UI Submit. Pressing Enter on `Back to game` likely fires a blast on the same frame. **Not verified in Play Mode.** | The binding looks like a template leftover. |
| 16 | Player-facing dev text: S1/S2 tell players to run an editor menu; `companyName: DefaultCompany` ([ProjectSettings.asset:15](../../../ProjectSettings/ProjectSettings.asset#L15)); subtitle `Greybox playtest`. | Fine for playtests; must change before a public build. |

---

## 7. Open questions that block UI

| # | Question | Recommended answer |
|---|---|---|
| 1 | Is rival progress always on screen, or only revealed at checkpoints? | Only at checkpoints, as the GDD says. Then keep a small `Rival: floor {m} · {mm:ss} ago` under the clock so the reveal still matters. |
| 2 | Is there health, or does any heavy hit kill? | Health of 100, host-owned, drawn as a thin bar that appears only when damaged. Without it, nothing tells a player how close they are to the 5 s respawn. |
| 3 | What are the two firms called, and how are teams told apart (art bible TBD, [art_bible.txt:586](../../Kit/concepts/Office/refs/art_bible.txt#L586))? | Two fictional firm names with a wordmark each; team colour via a cyan or white accent on the Cobalt system. Needed for roster, markers and results. |
| 4 | Who starts the run, and can a team be empty? | Host only, at the secretary's desk; allow 1v0 in local test mode only. |
| 5 | Do players choose a team, or is it automatic (today `ClientId % 2`)? | Automatic, plus a host-only `Swap` in the reception roster. Team must become synced state either way. |
| 6 | What wins: the first player on the roof, or the team? Tie at 15:00 on the same floor? | First player on the roof wins for the team; a tie at the cap goes to the team that reached that floor first. Results copy depends on it. |
| 7 | Primary accent and fonts: amber is not in the art bible, and the typeface is TBD. | Replace amber with Electric Blue `#2879EF`; reserve one warning colour; test one geometric sans plus one monospaced face before mockups are approved. |
| 8 | Is it a pistol or a rifle in v1, and how does the zipline gun's rarity show? | One rifle (the art bible's long-range role); the zipline gun shows charges, not ammo, and a pickup banner when found. |
