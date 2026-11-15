# Close the Deal — UI/UX research brief

Written 2026-11-12. Research only: nothing in `Assets/` was touched. Lead rulebook: the `game-ui-ux` skill (anchors and containers, scale to a reference resolution, safe area, focus navigation, screen stack, event-driven HUD). The project inputs are [docs/GDD.md](../../../docs/GDD.md), [docs/systems/MENU_AND_HUD.md](../../../docs/systems/MENU_AND_HUD.md) and art bible §03 (palette), §13 (type) and §14 (UI/HUD).

Every recommendation carries a tag saying what it rests on:

- **[XAG 101/102/103]** Xbox Accessibility Guidelines.
- **[GAG]** Game Accessibility Guidelines.
- **[Steam]** Steamworks Deck compatibility.
- **[WCAG]** W3C Web Content Accessibility Guidelines.
- **[computed]** WCAG contrast ratios and Machado-2009 colour-blind simulation, run for this brief (method in §9).
- **[community]** Third-party guides; not confirmed by the publisher.
- **[judgement]** My call. No external evidence.

All pixel values are at the **1920×1080 reference** unless marked otherwise. "720p" means 1280×720, which scales everything by 0.667.

---

## 0. Findings that decide most of the screens

1. **No single crosshair or HUD colour survives this palette.** White on Paper White (#F3F4F4) is 1.10:1. Near Black on Deep Navy is 1.11:1. Cyan on white is 1.64:1. Every element that floats over the world needs two tones: a light core with a dark outline, or text on a dark chip. **[computed]** XAG 102 shows this two-tone pattern in shipped games: Immortals Fenyx Rising uses white with a black outline, and For Honor uses a black outline wrapped in white. **[XAG 102]**
2. **Blended against pure white (#FFFFFF), a navy chip needs at least 85% opacity.** Sun patches clip to pure white, so this is the worst case. At 85%, white text reaches 10.75, cyan 5.95, amber #FF9E1B 5.20 and Silver Grey 6.78. Amber sets the floor: at 80% it drops to 4.33 and fails the 4.5:1 that XAG 102 sets for standard-size text. Cyan holds until about 78% (4.65), then fails at 75% (4.18) and 70% (3.52). **[computed, XAG 102]**
3. **There is room for only one warm hue.** Under deuteranopia simulation, amber and orange end up ΔE 15 apart, and orange and vermilion ΔE 18. A warm warning colour and a warm team colour cannot coexist. Magenta also fails: for protanopes it collapses toward cobalt (ΔE 80 → 37). **[computed]** Of the warm candidates, amber/orange keeps the largest distance from cyan and cobalt under all three simulations. **[computed]**
4. **Team colour matters less in this game than in most shooters.** The towers already separate the teams in space: the enemy is "across the gap". Team colour does real work only in a few places: zipline raiders inside your tower, name tags, the race tracker and the results screen. The art bible's TBD rule ("Do not solve uncertainty by adding more visual information") points to rival-relative colouring rather than per-firm brand colours. **[judgement, art bible §22]**
5. **Doc conflict.** [MENU_AND_HUD.md](../../../docs/systems/MENU_AND_HUD.md) gives primary buttons the colour amber. Any warm reserved warning colour clashes with that. **The greybox side should change**: in the art bible, cobalt and electric blue are the "active accent" (§03) and the warm hue is reserved (§14, §22). **[judgement]**
6. **This is a multiplayer game, so "pause" cannot pause.** MENU_AND_HUD.md already makes the player idle while the menu is open. The pause screen must therefore be a partial overlay that keeps threats visible, not a full-screen modal. **[repo docs, judgement]**

---

## 1. Main menu / title

### What it must do
- Get a friend group into the same game in the fewest steps. GAG (basic) says to "Allow the game to be started without the need to navigate through multiple levels of menus". **[GAG]**
- The friend-group games all use Steam friends as the join path, and none of them uses a lobby code:
  - **R.E.P.O.** Host → new game → **Invite** opens the Steam overlay. The friend picks **Join Game** from the main menu, which opens Steam's friends list, and then clicks Join Game on the host. There is no matchmaking. **[community, search summary]**
  - **Content Warning.** **Play With Friends** → choose a save slot → invite from either the in-game monitor (press E) or the Steam overlay (Shift+Tab → right-click a friend → Invite to Join). Up to 4 players. **[community, opened]**
  - **Lethal Company.** Host sets the lobby to public or friends-only. You invite from the pause menu on the ship with **Invite Friends**, which lists your Steam friends. **[community, search summary]**
  - **PEAK.** A Lobby Mode setting offers Invite Only. Changing it means re-hosting. The host can kick, and kicked players cannot rejoin. There is no matchmaking. **[Landfall FAQ, opened]**
- The project has already decided: there are no lobby codes, joining is through a Steam invite or **Join Game** on the host's name, and join-in-progress goes into the lobby only. **[MENU_AND_HUD.md, GDD]** That matches every reference above.
- Keyboard and gamepad focus must land on a control when the screen opens. **[game-ui-ux]** Steam Deck needs full controller navigation, glyphs that match the active input, and an on-screen keyboard for any text entry. **[Steam]**

### Pattern
Use a title "document" rather than a game-y button stack. The art bible asks for "forms, contract clauses, floor directories" without going skeuomorphic (§14). The title is large, sharp and corporate, with "Terms and Conditions Apply." set as fine print (§13). Composition: a big field of white or cobalt space with the title and a small block of data. Big graphics are kept for the title, lobby and results, not the HUD (§14, §19).

### Recommendation
- **Primary actions, in this order:** **Host game**, **Join a friend** (opens the Steam friends overlay, or explains it when the overlay is missing), **Settings**, **Quit**. Leave out Credits and Extras until they exist. A first-time player reaches a hosted lobby in **2 activations** (Host → confirm lobby privacy). **[GAG, community pattern, judgement]**
- **Lobby privacy at host time:** **Friends only** (default) or **Invite only**. Leave public matchmaking out of v1; none of the reference games has it at launch. **[community, Landfall FAQ]**
- **Button size:** **64 px tall** (43 px at 720p), minimum **360 px wide**, **12 px** vertical gap. That clears WCAG's 44×44 enhanced target at 1080p and its 24×24 minimum at 720p. **[WCAG 2.5.8 / 2.5.5]**
- **Button label size:** **28 px** rendered body height (ascender to descender, measured the XAG 101 way, not the TMP font-size value). That gives **19 px at 720p**. **[XAG 101]**
- **Text:** fine print and status line at **20 px** body height or more (13.3 px at 720p, above Steam Deck's 12 px target). Nothing on screen under **18 px** (12 px at 720p). The XAG 101 PC floor is 18 px at 1080p; the Steam Deck hard floor is 9 px at 1280×800 with a 12 px target. **[XAG 101, Steam]**
- **Thin "editorial" weights** (art bible §13): only at **36 px or more**, which XAG 102 counts as large text with a 3:1 minimum. Body text uses regular weight or heavier. Hairline rules that must survive 720p are **2 px or more** at 1080p. **[XAG 102, judgement]**
- **Contrast on a Paper White field:** Deep Navy text 15.40:1, Corporate Cobalt 6.59:1, Electric Blue #2879EF only 3.76:1. Electric Blue is for large text and focus rings, never body copy. **[computed]**
- **Focus state:** a **3 px** electric-blue or cobalt bar on the left edge plus a background tint. Never colour alone; XAG 103 rules out colour as the sole signal. Focus moves in **≤ 120 ms ease-out**. Default focus lands on **Host game**, or on **Back to game** when already in a game (MENU_AND_HUD.md rule). **[XAG 103, game-ui-ux, judgement]**
- **Status line** (MENU_AND_HUD.md) stays: one line, plain words, every wait named ("Joining Adam's game…"). A failed join gives the reason (friends-only, full, version mismatch, unreachable). Disabled buttons are dimmed to **3:1 or more** plus a reason, not hidden. **[XAG 102 inactive 3:1, XAG 103 not colour alone]**
- **Transitions:** screen-to-screen **200–250 ms** slide or snap ("sliding alignment, snapping lines, document-like reveal", art bible §14). Input works from frame 0 and never waits for the animation. **[art bible, judgement]**
- **Every state** (game-ui-ux): first run (no settings saved yet: show a one-time "Display and audio" check), Steam unavailable (local mode text from MENU_AND_HUD.md), connecting, failed, host left, offline. **[judgement]**

---

## 2. Settings screen

### What it must do
- **Required at basic level:** remappable controls, adjustable sensitivity, no essential information in colour alone, subtitles for important speech, and all settings saved. **[GAG]**
- **Intermediate level:** adjustable field of view, adjustable contrast, interface resizing, subtitles on before any sound plays. **[GAG]**
- **Text scaling up to 200% of the minimum size** without losing content, and at least one sans-serif face. **[XAG 101]**
- **A high-contrast mode** where all UI reaches 7:1 or more. **[XAG 102]**
- **A colour-blind preview** across dark, mid-tone and bright scenes before committing. Battlefield 2042 does this and XAG 102 cites it. **[XAG 102]**
- **The secretary is the game's voice** (GDD), so subtitles are not optional. GAG (basic): "Provide subtitles for all important speech". **[GAG, GDD]**

### Recommendation
- **Tabs, in this order:**
  1. **Gameplay:** sensitivity, invert Y, FOV, crosshair, hit markers.
  2. **Video:** display mode, resolution, frame cap, V-Sync, quality preset, brightness.
  3. **Audio:** buses below.
  4. **Controls:** rebinding.
  5. **Accessibility:** text size, subtitles, HUD chip opacity, high contrast, colour presets, screen shake, camera bob, ragdoll camera.

  Tabs switch with **Q/E** and LB/RB. The tab bar is **56 px** tall with labels at **24 px**. **[judgement, game-ui-ux focus rules]**
- **Row layout:** **64 px** row height, label left-aligned at **22 px**, control right-aligned in a **640 px** column, and a one-line description of the focused row at the bottom in **20 px**. That gives the 80-character line limit and left alignment from XAG 101. **[XAG 101, WCAG target]**
- **When changes apply:**
  - **Immediately with live preview:** sensitivity, FOV, audio volumes, crosshair, text size, subtitles, colour.
  - **Apply plus a confirm with a 15 s auto-revert:** display mode, resolution, monitor and refresh rate only. The prompt reads "Keep these display settings? Reverting in 15 s", with **Keep** focused only after the player moves focus. This mirrors the Windows display-settings convention. **[community; the 15 s figure is from Windows guides, not official Microsoft docs]**
  - **Revert:** each tab has **Reset to defaults** with a confirm step. Back or Esc with unsaved deferred changes asks Apply / Discard.
- **Mouse sensitivity:** a slider plus a numeric field. Range **0.05–10.00**, step **0.01**, default **1.00**, scaled so that 1.00 is about **35 cm/360 at 800 DPI**. Separate ADS or scope multiplier **0.1–2.0**, default **1.0**. **[GAG for existence; the values are judgement]**
- **FOV:** show **horizontal degrees at 16:9**, which is how Apex Legends and Overwatch label it. Range **70–110**, default **90**, with a "horizontal, 16:9" note in the description. Apex's PC slider is 70–110 horizontal and Overwatch's PC maximum is 103. **[community]**
  - Unity's `Camera.fieldOfView` is vertical: convert with vFOV = 2·atan(tan(hFOV/2) / (16/9)). That gives 70 → 43.0°, 90 → 58.7°, 103 → 70.5° and 110 → 77.5°. **[computed]**
  - Use Hor+ on ultrawide: keep the vertical value and let the wider screen show more. **[judgement]**
  - A default of 90 rather than 103+ is a motion-comfort call, because self-ragdoll flights are seen in first person (GDD). GAG says to set an "appropriate default". **[GAG, judgement]**
- **Audio buses:**
  - **Master**, **Music**, **Effects**, **Secretary (voice)**, **Voice chat**, **Interface**.
  - Each runs 0–100 in steps of 5. Defaults: 80 / 60 / 80 / 100 / 80 / 70.
  - The secretary gets her own bus because she carries the comedy and the information.
  - Add **Voice chat: push-to-talk / open mic / off** if there is in-game voice; if the game relies on Steam/Discord voice, leave it out.
  - **[judgement; buses map to a Unity AudioMixer]**
- **Rebinding:**
  - Every action can be rebound, with two slots per action (primary and alternate).
  - When you press a key that is already in use, a conflict prompt names the action it is bound to and offers **Swap**.
  - Esc cannot be bound because it cancels.
  - Each action gets a "Reset" button.
  - Gamepad layout presets, with glyphs that match the active device.
  - **[GAG basic, Steam]**
- **Accessibility tab values:**
  - **Text size:** 100 / 125 / 150 / 175 / 200%. **[XAG 101]**
  - **Subtitles:** on by default, size 100–200%, background opacity 0–100% (default 85%), speaker name shown ("Reception:"). **[GAG, XAG 102: Minecraft background-opacity example]**
  - **HUD chip opacity:** 70–100%, default 85%. Below 85%, amber text on a chip over pure white falls under 4.5:1 (4.33 at 80%), and below about 78% cyan does too (§0). The 70% floor is a player choice, and the slider description should state that cost. **[computed]**
  - **High contrast UI:** off/on. When on, chips are 100% Deep Navy and all text is 7:1 or more. **[XAG 102]**
  - **Colour presets:** Default / Deuteranopia / Protanopia / Tritanopia / Custom (pick the rival and own colours), with a live preview over white, cobalt and navy swatches. **[XAG 102/103, BF2042 and R6 examples]**
  - **Screen shake:** 0–100%, default 100. **Camera bob:** on/off. **Ragdoll camera:** first person / detach to third person (comfort). **[GAG intermediate: background movement; judgement on ragdoll]**
- **Persistence:** save on every change, to a file in the user folder. Never reset on update. GAG basic: "Ensure that all settings are saved/remembered". **[GAG]**

---

## 3. Crosshair and crosshair customisation

### Evidence
- **CS2** exposes:
  - style (0–4: default, default static, classic, classic dynamic, classic static)
  - colour (presets, or custom RGB 0–255)
  - size (0–100), thickness (0–100) and gap (-100 to 100)
  - centre dot, alpha
  - outline (on/off; thickness 0–3, styles 2–4 only)

  Sources disagree on defaults: size is given as 2.5 or 5. **[community wiki, opened]**
- **Valorant** exposes:
  - colour, with outlines (on/off, opacity, thickness)
  - centre dot (on/off, opacity, thickness)
  - inner and outer lines, each with opacity, length, thickness and offset
  - movement error and firing error

  Exact slider ranges are not published in any source I could open. **[community]** Pros mostly use **white, cyan or green**, inner-line length 3–6, thickness 1–3, offset 0–3, opacity 100%, outer lines off and outlines mixed. **[prosettings, opened]**
- **Overwatch 2** exposes:
  - types Circle, Crosshairs, Circle and Crosshairs, and Dot
  - Show Accuracy (dynamic spread), colour, thickness, length, centre gap
  - opacity, outline opacity, dot size, dot opacity
  - **Scale With Resolution**

  **[community]**
- **Valorant enemy highlight** offers Red (default) / Purple (tritanopia) / Yellow (deuteranopia) / Yellow (protanopia). Community guidance is to pick a crosshair colour that contrasts with your enemy colour. **[community]**
- GAG (intermediate): provide a choice of cursor or crosshair colours and designs, with larger sizes and better contrast as the reason. **[GAG, opened]**
- XAG 102 counts targeting icons as important visual elements: **4.5:1** against the **lowest-contrast area** of a non-solid background. **[XAG 102]**

### The palette test (§0 finding 1)
Contrast ratios against the three backgrounds. Sun patches clip to #FFFFFF, so treat that as the worst white.

| Fill | vs #F3F4F4 | vs #FFFFFF | vs Cobalt #244FB9 | vs Navy #111B38 |
|---|---|---|---|---|
| White #FFFFFF | 1.10 | 1.00 | **7.26** | **16.97** |
| Near Black #101116 | **17.11** | **18.85** | 2.60 | 1.11 |
| Cyan #20D4E6 | 1.64 | 1.81 | 4.02 | **9.39** |
| Yellow #FFD400 | 1.30 | 1.43 | **5.07** | **11.85** |
| Green #00FF00 | 1.25 | 1.37 | **5.29** | **12.36** |
| Magenta #FF00FF | 2.85 | 3.14 | 2.31 | **5.41** |

Bold marks a value of 4.5 or more. **[computed]**

- **Default: a white core with a 1 px near-black outline at 100% opacity.** The best contrast is the core on dark backgrounds and the outline on light ones:
  - over white: outline 18.85
  - over cobalt: core 7.26
  - over navy: core 16.97

  Every background passes 4.5:1 with headroom, and cobalt passes the 7:1 high-contrast bar. **[computed, XAG 102]**
- **Outline opacity matters.** A near-black outline at 60% over pure white blends to #707073, which is 4.94:1, still a pass but thin. At 80% it is 10.19. **Keep the default at 100% and the minimum at 60%.** **[computed]**
- **Colour presets:**
  - Yellow and green with an outline also pass everywhere. Their weakest case is cobalt, at 5.07 and 5.29.
  - Cyan with an outline is weakest on cobalt at **4.02**, which fails 4.5 and passes only the 3:1 large-element bar. Keep it as a preset but mark it "low on blue sky" in the picker.
  - Magenta fails on cobalt even with an outline (outline vs cobalt is 2.60). Leave it out.
  - **[computed]**

### Rendering rules
- The crosshair is sized in **physical screen pixels**, not CanvasScaler units. A 1 px outline at 1080p would become 0.67 px at 720p and shimmer or vanish. Snap to the pixel grid and keep the outline at least **1 physical px**. Provide **Scale with resolution** (Overwatch's option) as an opt-in. **[community: Overwatch; judgement]**
- Draw it on a **Screen Space – Overlay** canvas, or after post-processing, so URP bloom and tonemapping cannot brighten the core or wash out the outline against the blown-out sun. **[judgement; not tested in engine]**
- **Dynamic spread** ("chaotic, not precise", GDD): on by default, so bloom is visible. The arms move out by the weapon's current spread angle projected to pixels, opening in **≤ 50 ms** and recovering over the weapon's recovery time. Valorant and CS2 players often turn spread display off, so provide a toggle. **[community, judgement]**
- **Hit marker:**
  - Four diagonal ticks, centred **10 px** from the crosshair centre, each **7 px** long and **2 px** thick, white with a 1 px outline.
  - Show for **100 ms**, then fade over **100 ms**, paired with a short "tick" sound on the Interface bus.
  - **Kill marker:** ticks at **14 px** and **10 px** long, held **250 ms**, with a distinct, lower sound.
  - Do not colour kills in the reserved warm colour (§5), because warm means threat.
  - Tie the hit sound to every hit: XAG 103 asks for at least two channels.

  Source quality for timings is weak. Community guides give 50–100 ms flashes and a distinct tier for headshots and kills (Overwatch, Halo 5 and Titanfall 2 turn markers red on critical hits). **[community, weak; XAG 103; judgement]**
- **Damage direction:** an arc segment on a **120 px** radius ring around the centre, **6 px** thick, in the warning colour with a 1 px dark outline. It shows for **800 ms** and points to the source, plus spatial audio. This follows XAG 102/103's Fenyx and Grounded examples (outlined arrow or halo plus audio). **[XAG 102/103, judgement on size]**
- **Previews:** in the settings preview, draw the crosshair over three live swatches at once: Paper White, Corporate Cobalt and Deep Navy, plus a pure-white "sun" strip (BF2042 pattern). **[XAG 102]**
- **Sharing:** a copyable crosshair code (Valorant and CS2 style) fits a friend-group game. **[judgement; the CS2 share-code mechanism is common knowledge; I did not open a source for it]**

### Recommendation
- **Default crosshair:** Cross + Dot, white #FFFFFF core, **2 px** arms **6 px** long, **4 px** gap, **2 px** dot, and a **1 px Near Black #101116 outline at 100%**.
  - Contrast takes the better of core and outline on each background: over white **18.85** (outline), over cobalt **7.26** (core), over navy **16.97** (core).
  - All three pass XAG 102's 4.5:1, and cobalt also passes 7:1.
  - **[computed, XAG 102]**
- Render in physical pixels on a Screen Space – Overlay canvas, unaffected by post-processing. **[judgement]**
- Spread display on by default, hit marker plus sound on by default. **[community, XAG 103]**
- The settings preview shows white, cobalt, navy and a pure-white strip. **[XAG 102]**

### Crosshair parameter set to expose

| Parameter | Range | Default | Note |
|---|---|---|---|
| Style | Cross / Dot / Cross + Dot / Circle / Circle + Dot | Cross + Dot | Options follow Overwatch's types. **[community]** |
| Colour | White, Yellow #FFD400, Green #00FF00, Cyan #20D4E6, Custom hex | White #FFFFFF | Cyan flagged "low on blue sky". **[computed]** |
| Opacity | 30–100% | 100% | |
| Arm length | 0–20 px | 6 px | Pros run 3–6. **[prosettings]** |
| Arm thickness | 1–6 px | 2 px | Pros run 1–3. A 2 px arm keeps 1.3 px at 720p if scaling is on. |
| Centre gap | 0–20 px | 4 px | Pros run 0–3. Wider here because "chaotic, not precise" means the dot carries precision. |
| Centre dot | on / off | on | |
| Dot size | 1–6 px | 2 px | |
| Outline | on / off | **on** | Required to pass on white. **[computed]** |
| Outline thickness | 1–3 px | 1 px | CS2 range is 0–3. **[community wiki]** |
| Outline opacity | 60–100% | 100% | 4.94:1 at 60% over pure white. **[computed]** |
| Outline colour | Near Black #101116 / Deep Navy #111B38 | Near Black | |
| Circle radius (circle styles) | 8–40 px | 16 px | |
| Show spread (movement + firing) | off / movement / firing / both | both | Valorant has movement and firing error; Overwatch has Show Accuracy. |
| Spread multiplier | 0.0–2.0 | 1.0 | |
| Scale with resolution | on / off | off | Physical-pixel rendering by default. |
| Hit marker | off / on | on | |
| Hit marker sound | 0–100% | 80% | Interface bus. |
| Kill marker | off / on | on | |
| Hide crosshair when scoped | on / off | on | Sniper has its own scope reticle. |

All pixel values are physical pixels at 1080p. **[judgement unless tagged]**

---

## 4. In-game HUD

### What it must show
These are the GDD systems the HUD carries:

- a match clock with a 15:00 hard cap
- each team's floor progress
- checkpoint reveals
- weapon and ammo (pistol/rifle, rocket, throwable, zipline gun)
- health
- respawn countdown (5 s on a floor) and window-exit respawn
- keycards and rare items
- the secretary's lines
- kills and deaths

The art bible's noise budget: "small high-contrast information blocks", and nothing that covers large parts of the frame (§14). GAG (intermediate): avoid putting essential temporary information outside the player's eye-line. **[GDD, art bible, GAG]**

### Layout (1920×1080, safe margin 48 px)

```
┌──────────────────────────────────────────────────────────────┐
│ [clock 12:41]          [RACE TRACKER  you ▮▮▮▮▮░  rival ▮▮▮░]   [feed ×4] │
│                                                              │
│                                                              │
│                         ┼  (crosshair)                       │
│                   [toast: CHECKPOINT · Floor 6]              │
│                                                              │
│              [secretary subtitle, 2 lines max]               │
│ [health ▮▮▮▮▮▮▮ 100]                  [item][ROCKET  1 / 3]  │
└──────────────────────────────────────────────────────────────┘
```

### Recommendation
- **Safe margin:**
  - **48 px** from every edge at 16:9 (32 px at 720p).
  - On ultrawide, the HUD bounds default to the centre 16:9 area (§7).
  - Call of Duty has a **HUD Bounds** setting and Warframe has **Adjust HUD Margins**. Expose a **HUD bounds** slider from 16:9 to full width.
  - **[community, GAG]**
- **Noise budget:** the persistent HUD covers **4% or less** of the 1080p frame (≈ 83,000 px²), crosshair excluded. That allows roughly the four blocks below. Anything bigger is a transient toast that leaves within **2.5 s**. **[art bible §14; the number is judgement]**
- **Chips:**
  - All HUD text sits on a Deep Navy chip at **85%** opacity, with corner radius **2 px** or square (art bible: geometric frames, not bubbly) and padding **8 × 12 px**.
  - At 85%, measured over pure white: white text 10.75, Silver Grey 6.78, cyan 5.95, amber #FF9E1B 5.20.
  - **[computed, XAG 102]**
  - Optional 1 px Cyan top rule on chips as a system line. Decorative, so no contrast rule applies. **[XAG 102 decorative exemption]**
- **Text sizes:**
  - Numerals (clock, ammo, health) **32 px** body height, or **36 px** if a thin weight is used.
  - Labels **20 px**.
  - Subtitles **30 px**, max 2 lines, max **42 characters per line**, bottom edge **160 px** above the screen bottom.
  - Floor 18 px for anything.
  - **[XAG 101, GAG subtitles; line length is judgement under XAG's 80]**
- **Race tracker (top centre, about 520 × 56 px):**
  - Two horizontal floor ladders, 11 segments each (lobby, floors 1–9, boardroom), plus a roof mark.
  - Own team in **cyan** with a ◆ marker; rival in the **rival colour** (§5) with a ▲ marker. That is shape plus colour, never colour alone.
  - Show the rival's exact floor only after a checkpoint reveal (GDD: checkpoints "reveal how far the rival team has got"). Between reveals, show "last seen F4 · 1:12 ago".
  - **[GDD, XAG 103, judgement]**
- **Match clock (top left, 160 × 48 px):**
  - **Counts down** from 15:00, the hard cap. The diagram's "12:41" means 12:41 left.
  - Monospaced numerals (art bible: data type).
  - From **3:00 left** it switches to the warning colour plus a hazard icon.
  - From **0:30 left** it pulses at 1 Hz (0.5 s on, 0.5 s off), well under 3 flashes per second, which is the photosensitivity threshold (WCAG SC 2.3.1; I did not open that page for this brief).
  - The secretary also says it, giving an audio channel.
  - **[XAG 103, judgement]**
- **Health (bottom left, 280 × 48 px):**
  - A segmented bar plus a number.
  - Below **25%**, the bar turns the warning colour and gains a ⚠ glyph, and a low-health tone plays. A heartbeat is a cliché; prefer a clinical "system" beep that fits the corporate tone.
  - **[XAG 103, judgement]**
- **Weapon and ammo (bottom right, 320 × 64 px):** weapon name in caps (art bible: corporate/procedural naming, e.g. "ROCKET · 1 / 3"), plus item slots (keycard, zipline gun) as **40 px** icons. **[judgement]**
- **Kill feed (top right):**
  - **360 px** wide, **4 rows** max, each **36 px** tall, **6 s** life, 200 ms fade.
  - It is event-driven and empty most of the time, so it sits outside both the 4% persistent budget and the 2.5 s toast rule.
  - With 4 full rows it adds about 52k px². Cap it at **2 rows** if playtests show clutter.
  - Names coloured own/rival with a team glyph before the name.
  - Window exits get their own verb ("was relocated via window"), played straight per the art bible.
  - **[judgement]**
- **Toasts:**
  - Checkpoint, door breached, rival reached checkpoint: centre, **120 px** below the crosshair, 30 px text.
  - Slide in 150 ms, hold 2 s, slide out 200 ms.
  - Only one at a time; queue the rest.
  - **[art bible §14 motion, judgement]**
- **Respawn:**
  - After a death on a floor: a full-width "Out of office · back in 5" band at 30% screen height with a 5 s countdown in 48 px numerals.
  - After a window exit: "Returned to Floor 6 checkpoint".
  - Both carry the secretary's line as a subtitle.
  - **[GDD, judgement]**
- **Name tags** (MENU_AND_HUD.md lists these as missing):
  - Own team only, through glass, within 40 m, at **18 px** on a chip.
  - Rival players get no name tags across the gap, to keep sightlines clean.
  - A **zipline raider** inside your tower gets a rival-colour ▲ marker for 2 s on first sight.
  - **[judgement]**
- **Wiring:** every block updates from events (`HealthChanged`, `FloorReached`, `CheckpointRevealed`, `AmmoChanged`), never from polling in Update. **[game-ui-ux]**
- **Hide HUD** toggle for streamers and clips. The GDD names "the people watching" as an audience, and the three moments are trailer and gif material. **[GDD, judgement]**

---

## 5. Reserved warning colour and team colours

### Candidates
Columns 2–4 are WCAG contrast ratios. The last column is the CIELAB ΔE from Corporate Cobalt: normal vision / protanopia / deuteranopia / tritanopia, by Machado 2009 at severity 1.0. **[computed]**

| Candidate | vs Paper White | vs Navy | vs Cobalt | ΔE vs Cobalt (N/P/D/T) |
|---|---|---|---|---|
| Signal Amber #FF9E1B | 1.88 | **8.21** | 3.51 | 139/132/142/80 |
| Amber #FFB000 | 1.66 | **9.26** | 3.96 | 147/137/148/74 |
| Signal Orange #FF6A13 | 2.60 | **5.92** | 2.53 | 134/118/137/103 |
| Safety Yellow #FFD400 | 1.30 | **11.85** | **5.07** | 157/148/155/66 |
| Vermilion #E8412C | 3.65 | 4.22 | 1.81 | 117/94/119/117 |
| Red #E10600 | **4.51** | 3.42 | 1.46 | 131/102/122/126 |
| Magenta #E6007E | 4.08 | 3.77 | 1.61 | 80/**37**/72/112 |
| Cyan #20D4E6 (system) | 1.64 | **9.39** | 4.02 | 83/59/55/48 |

Selected pair differences (ΔE, N/P/D/T):

- Cyan vs Signal Amber #FF9E1B: **114/88/98/96**. Clearly separate for every type.
- Cyan vs Signal Orange: 127/80/95/124.
- Cyan vs Vermilion: 123/64/81/139.
- Cyan vs Magenta: 119/**42/37**/136. Collapses for red-green types.
- Amber #FFB000 vs Orange #FF6A13: 40/24/**15**/39. The two warm colours merge for deuteranopes.
- Signal Amber vs Vermilion: 47/43/**26**/45.
- Cyan vs White: 46/**24**/35/50. Cyan alone cannot mark "own team" against white text.

**[computed; treating ΔE < 30 as "will be confused" is my judgement]**

### What this means
- **Red is a poor fit for this palette.** It fails on navy (3.42) and cobalt (1.46), and protanopes see it as dark olive (#605400). Vermilion has the same problem. **[computed; red–green confusion affects about 8% of men and 0.5% of women, colourblindawareness.org]**
- **Magenta is out.** For protanopes it simulates as a grey-blue (#4F5E80) that sits inside the cobalt and navy family. **[computed]**
- **Amber/orange is the only warm hue that stays distinct from the blue system for all three CVD types**, and it passes on navy. It fails on white and cobalt, so it must always sit on a navy chip or carry a dark outline. On the 85% chip over pure white, Signal Amber reaches 5.20. **[computed]**
- **There is room for one warm hue, not two** (amber vs orange is ΔE 15 for deuteranopes).

### Option A (recommended): one "threat" colour
- **Own team = Cyan #20D4E6 with the ◆ glyph. Rival = Signal Amber #FF9E1B with the ▲ glyph.**
- **The reserved warning colour is the same amber**, so amber means "threat" whether it comes from the rival or from the clock or health. Every danger in this game comes from the rival or the clock.
- Colouring is **relative to the player**: you are always cyan and the rival is always amber, whichever firm you play for. R6 Siege moved to "opponents always red" to "bring consistency to the UI and improve onboarding for newcomers". **[Ubisoft, opened]**
- Contrast:
  - Amber on navy 8.21, on the 85% chip over white 5.20.
  - Cyan on navy 9.39, on the chip 5.95.
  - Both need an outline over open sky (amber vs cobalt 3.51; cyan vs cobalt 4.02).
- Colour-blind check:
  - Cyan vs amber stays at ΔE 88 or more in all three simulations.
  - Each team also always carries a glyph (◆ / ▲) and a position (left or right ladder; own tower and rival tower).
  - **[computed, XAG 103]**
- Cost: amber now carries two meanings, rival and danger. Warnings that are not about the rival use the ⚠ glyph and the 1 Hz pulse to tell them apart. **[judgement]**

### Option B: amber kept for warnings only
- Own team = Cyan ◆; rival = **Paper White / Silver ▲ with a navy outline**; warning = Signal Amber + ⚠.
- Contrast for the rival colour:
  - Silver #C9CED3 is 10.71 on navy, but only 1.44 on Paper White and 1.58 on pure white.
  - Paper White is 15.40 on navy and 1.00 on itself.
  - So on any white surface the rival marker exists only through its navy outline. **[computed]**
- This keeps the warning colour purely reserved (art bible §22 wording).
- But rival vs own is cyan vs white, which is only ΔE 24 for protanopes. Team identity then depends almost entirely on glyph and position.
- That is acceptable here because the towers separate the teams, but weaker for zipline raiders inside your tower. **[computed, judgement]**

### Either option
- Expose **colour presets plus Custom** (pick own, rival and warning colours) with a live preview. XAG 103 says that when colour is the primary channel, players should be able to configure it. Valorant (Red/Purple/Yellow) and R6 (blue/red/orange, more added later) both offer this. **[XAG 103, community, Ubisoft]**
- **Recolour the greybox primary button** from amber to cobalt or electric blue (§0 finding 5).
- Run a **real colour-blind playtest**. XAG 103 says simulation does not replace testing with colour-blind players. **[XAG 103]**

---

## 6. Pause menu (in-match menu)

### What it must do
- **The match does not stop.** The rule "Menu open ⇔ mouse free ⇔ player idle" (MENU_AND_HUD.md) means the player is a sitting target while it is open.
- The menu must:
  - be **instant to open and close**
  - **keep threats visible**
  - offer the friend-group actions mid-match: invite (Lethal Company invites from the pause menu; Peak changes lobby settings there), settings and leave
- GAG (advanced) asks for alternatives where precise timing is essential. Here the alternative is that nothing in the menu is timed. **[repo docs, community, GAG]**

### Recommendation
- **Esc opens and closes on the same key,** with **no animation blocking input**. The panel slides in over **150 ms** ease-out and out over **120 ms**. **[judgement, MENU_AND_HUD.md]**
- **Partial overlay:**
  - A **520 px** wide panel anchored left, full height inside the safe margin.
  - The rest of the screen is the live game with a **20%** navy dim and no blur, so you can still see a rocket coming.
  - A top line in the warning colour: "Match in progress — you are still in the building." **[judgement]**
- **Items, with default focus on the first:**
  1. **Back to work** (resume)
  2. **Invite colleagues** (Steam overlay, host and clients)
  3. **Settings** (pushes the settings screen on the stack)
  4. **Controls** (read-only key reference)
  5. **Leave game**

  A host sees "End the meeting for everyone" instead of Leave, with a confirm stating that the run ends for all players (GDD: no host migration). **[GDD, game-ui-ux screen stack]**
- **Panel content:** player list (name, team glyph, ping in ms), match clock, lobby privacy (host can change it). Buttons **56 px** tall, labels **24 px**. **[WCAG target, XAG 101]**
- **Confirm dialogs:**
  - Two buttons, the destructive one **not** focused by default, and Esc means Cancel.
  - Leaving mid-match states the consequence ("Your team continues without you").
  - **[XAG 101 Grounded delete-dialog example, judgement]**
- **Auto-open** when the window loses focus, the connection drops, or the player has no character (existing rule). **[MENU_AND_HUD.md]**

---

## 7. Lobby and results

### Lobby: what it must do
- The v1 lobby is **diegetic**: the company reception, where talking to the secretary starts the run (GDD). The overlay UI should therefore be minimal: a roster, team assignment, ready state and invite.
- Join-in-progress goes into the lobby only (GDD). Lobby privacy follows the Friends only / Invite only pattern (Peak, Lethal Company). **[GDD, Landfall FAQ, community]**

### Lobby: recommendation
- **Roster panel:**
  - Top right, **440 px** wide, two columns (Firm A and Firm B, named in-world).
  - Up to 4 rows per team at **48 px** each; name **22 px**, ready tick, host crown.
  - Empty slots read **"Vacancy — Invite"** and are clickable. **[GDD 1–4 per team; judgement on copy]**
- **Team switching:** walk to your firm's reception desk, or use the roster's "Transfer" button. Teams of 1v1, 2v1 and so on are allowed (GDD), so imbalance shows a note, not a block. **[GDD]**
- **Starting:** the host talks to the secretary. A **5 s** countdown band ("Meeting starts in 5"), cancellable by the host. The run cannot start while a player is mid-join; show "Waiting for Sam to arrive…". **[judgement]**
- **Invite:** **Invite colleagues** on the roster and in pause both open the Steam overlay. If the overlay is unavailable, explain the friends-list route (current MENU_AND_HUD.md behaviour). **[repo docs, community pattern]**
- **Host left:** every client returns to the title with the status line "The host closed the meeting." (GDD: no migration) Never a silent drop. **[GDD, game-ui-ux every-state]**

### Results: what it must do
- Show who won and why: the roof, or the 15:00 cap with the highest team winning (GDD).
- Celebrate the three moments.
- Offer **rematch** (GDD v1 scope) with the party intact.
- This is where the art bible allows big graphics (§14). **[GDD, art bible]**

### Results: recommendation
- **Sequence**, with any key skipping to the end state:
  - **0–1.5 s:** secretary line plus a full-bleed title card. "QUARTERLY RESULTS". Winner firm in **96 px** display type. Cause line in 28 px ("Reached the roof at 12:31" / "Highest floor at close of quarter").
  - **1.5–4 s:** a **"Minutes of the meeting"** table slides up.
  - **4 s:** buttons appear.
  - **[GDD, art bible §14 document-like reveal; timings are judgement]**
- **Table:**
  - One row per player, **44 px** tall, text **22 px**.
  - Columns: floors climbed, window exits caused, times relocated via window, props displaced, rockets fired.
  - Highlight the top player per column with a ◆ and the cyan/amber team colour.
  - Corporate wording played straight (art bible §14).
  - Numbers in monospaced data type (§13).
  - **[judgement]**
- **Race replay strip:** both towers' floor progress over time as two lines on a **1200 × 160 px** chart, with checkpoint ticks. This is the comeback story. **[judgement]**
- **Buttons:**
  - **Rematch** (default focus; host starts it, clients vote "Ready" and the host sees "3 / 4 ready").
  - **Back to reception** (lobby).
  - **Leave**.
  - **64 px** tall.
  - Auto-return to reception after **60 s** idle so no one is stuck.
  - **[GDD rematch, judgement]**
- **States:** host left during results ("The host closed the meeting" → title); a player left (row greyed with "Left early", which is text, not just grey: XAG 103); a tie at the cap (tie rule still open in the GDD, so show "Draw — tie on floor N"). **[GDD open question, XAG 103]**

---

## 8. Resolution edge cases: 1280×720, 16:10 and ultrawide

### Scaling model
- Use **Unity CanvasScaler: Scale With Screen Size, reference 1920×1080, Screen Match Mode = Expand.** Expand fits the whole reference area on every screen and adds space instead of cropping:
  - 2560×1080 (sold as 21:9, actually 64:27) → canvas **2560×1080**
  - 16:10 1280×800 → canvas **1920×1200**
  - 720p → canvas 1920×1080 at scale 0.667
- The `game-ui-ux` skill's default is "Match 0.5" (and 1.0 for height-critical HUDs). With Match = 1.0, a 16:10 screen would get a canvas only **1728** wide, and a HUD laid out to 1920 would overlap. Expand avoids that. **[game-ui-ux, judgement]**
- HUD blocks anchor to their corners with fixed offsets; nothing uses absolute positions. **[game-ui-ux]**

### 1280×720
- Every size scales by **0.667**.

  | At 1080p | At 720p | Check |
  |---|---|---|
  | 18 px text | 12 px | Steam Deck target (floor 9 px). **[Steam]** |
  | 20 px label | 13.3 px | |
  | 30 px subtitle | 20 px | |
  | 64 px button | 43 px | Above WCAG's 24 px minimum; just under the 44 px enhanced target. **[WCAG]** |
  | 2 px line | 1.33 px | Uneven. Hairlines are 2 px or more at 1080p and snapped. A 1 px line at 1080p (0.67 px at 720p) is not allowed for anything that carries information. **[judgement]** |

- The **crosshair does not scale** (physical pixels, §3), so it stays crisp. **[judgement]**
- The noise budget is a fraction of the screen, so it holds. **[judgement]**
- **Test captures** at 1280×720 and 1280×800 before approving any HUD mockup. **[judgement]**

### Ultrawide 21:9 (2560×1080, 3440×1440) and 32:9
- The camera uses **Hor+**: the vertical FOV is fixed, so the sides show more city. **[judgement]**
- **HUD bounds default = the centre 16:9 area.** Corner blocks sit 48 px inside a 1920-wide centre region, not at the physical screen edges. This follows GAG's eye-line guidance and the common "lock HUD to 16:9" fix. Call of Duty (HUD Bounds) and Warframe (HUD margins) give players a slider for this. **[GAG, community]**
- Expose **HUD bounds: 16:9 / 21:9 / full** (or a 0–100% slider). **[community: Hogwarts Legacy mod and PCGamingWiki proposal recommend a toggle]**
- **Full-screen screens stay usable:** the title, results and lobby overlays are composed in the centre 16:9 and extend backgrounds (sky, white field) to the edges, never stretched. The results table stays at most **1600 px** wide. Valorant's community-reported behaviour (HUD stretched or letterboxed on 21:9) is the thing to avoid. **[community, judgement]**
- **Pause panel** stays anchored to the left edge of the 16:9 centre region, not the physical left edge, so it stays near the eye-line. **[judgement]**

---

## 9. What is not evidenced, and how the numbers were made

### Not evidenced
- **Game UI Database was not accessed.** gameuidatabase.com sits behind a Cloudflare bot check (HTTP 403, "Performing security verification") and I did not try to get round it. No screen-by-screen FPS references from it are in this brief. **Open it manually** for title, settings and HUD references (filters: Screen = Title/Settings/HUD, Genre = FPS).
- **Official Valorant and Overwatch 2 slider ranges are not verified.** Every figure comes from community guides with different scales. The crosshair ranges in §3 are my judgement, informed by CS2's documented ranges and the values pros use.
- **CS2 defaults conflict between sources** (size 2.5 vs 5).
- **Hit-marker timing (50–100 ms) comes from weak sources** (blogs and scripting guides). There is no studio postmortem behind it.
- **The 15 s display-revert convention** is described in community Windows guides, not official Microsoft documentation.
- **R.E.P.O. and Lethal Company flows** come from search summaries of fan guides (the primary pages refused fetches). Content Warning and PEAK were opened.
- **No in-engine test.** Real values under URP bloom, the haze and the sun patches were not measured. The contrast numbers are swatch-to-swatch only. Capture the game at noon-sun and against the cloud sea and re-measure with Colour Contrast Analyser, as XAG 102 recommends.
- **The colour-blind results are simulations.** XAG 103 says simulation does not replace testing with colour-blind players.
- **Not researched:** localisation length expansion, text-to-speech/UI narration, and controller-only crosshair settings.

### Method
- **WCAG 2.x relative luminance contrast.**
- **Chip colours:** alpha-blended in sRGB over the background (navy at α over #FFFFFF / #F3F4F4 / #244FB9).
- **CVD simulation:** Machado, Oliveira & Fernandes 2009 matrices at severity 1.0, applied in linear RGB.
- **ΔE:** CIE76 in CIELAB (D65).
- The script lives only in the session scratchpad; the numbers above are its output.

---

## 10. Sources

Opened and read for this brief:

1. Xbox Accessibility Guideline 101: Text display — https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101
2. Xbox Accessibility Guideline 102: Contrast — https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102
3. Xbox Accessibility Guideline 103: Additional channels for visual and audio cues — https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/103
4. Game Accessibility Guidelines, full list — https://gameaccessibilityguidelines.com/full-list/
5. GAG: Provide a choice of cursor / crosshair colours / designs — https://gameaccessibilityguidelines.com/provide-a-choice-of-cursor-crosshair-colours-designs/
6. Steamworks: Steam Deck compatibility review (text legibility, controller, glyphs) — https://partner.steamgames.com/doc/steamdeck/compat
7. W3C WCAG 2.2 Understanding SC 2.5.8 Target Size (Minimum) — https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
8. Colour Blind Awareness: prevalence — https://www.colourblindawareness.org/colour-blindness/
9. Machado, Oliveira, Fernandes (2009), A Physiologically-based Model for Simulation of Color Vision Deficiency — https://www.inf.ufrgs.br/~oliveira/pubs_files/CVD_Simulation/CVD_Simulation.html
10. Ubisoft, Rainbow Six Siege: High Calibre season (opponents always red) — https://www.ubisoft.com/en-us/game/rainbow-six/siege/news-updates/seasons/highcalibre
11. Counter-Strike Wiki: Crosshair Settings (CS2 cvars and ranges) — https://counterstrikewiki.com/Crosshair_Settings
12. ProSettings: Best VALORANT Crosshair (pro colour and line values) — https://prosettings.net/blog/best-valorant-crosshair/
13. Landfall: PEAK FAQ (lobby mode, kick, no matchmaking) — https://landfall.se/peak-faq
14. GameLeap: Content Warning, how to host and invite friends — https://gameleap.com/articles/content-warning-how-to-host-and-invite-friends

Search summaries only (not opened; treat as unverified):

15. Dot Esports: How to make and join lobbies in R.E.P.O. (fetch refused) — https://dotesports.com/indies/news/how-to-make-and-join-lobbies-in-r-e-p-o
16. Sportskeeda: Lethal Company multiplayer guide (fetch refused) — https://www.sportskeeda.com/esports/lethal-company-multiplayer-guide-how-play-friends
17. esports.gg: How to change enemy color in Valorant — https://esports.gg/guides/valorant/how-to-change-enemy-color-in-valorant/
18. GGRecon: How to change your crosshair in Overwatch 2 (option list) — https://ggrecon.com/guides/overwatch-2-change-crosshair-settings
19. PC Gamer: Overwatch gets an FOV slider after all (103 max) — https://www.pcgamer.com/overwatch-gets-an-fov-slider-after-all
20. TheGamer: Apex Legends best FOV settings (70–110) — https://www.thegamer.com/apex-legends-best-fov-settings/
21. Pro Game Guides: Black Ops 6 HUD settings (HUD Bounds) — https://progameguides.com/call-of-duty/best-hud-settings-presets-and-colors-in-black-ops-6/
22. PCGamingWiki community: centring HUD elements on ultrawide — https://community.pcgamingwiki.com/topic/6072-proposal-evaluate-centering-of-hud-elements-on-ultrawide-displays
23. Riot Support: Supported resolutions in VALORANT — https://support.riotgames.com/ms-my/valorant/support-tools/supported-resolutions-in-valorant
24. Medium (J. Stephenson): A UX analysis of FPS damage indicators — https://medium.com/@jasper.stephenson/a-ux-analysis-of-first-person-shooter-damage-indicators-59ac9d41caf8
25. Siege.gg: Team colours in Rainbow Six Siege X — https://siege.gg/su/team-colors-in-rainbow-six-siege-x-how-to-change-to-purple-green-yellow-and-more

Blocked:

26. Game UI Database — https://www.gameuidatabase.com/ (Cloudflare bot check; not accessed)
