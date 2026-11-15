# Seal the Deal: UI feel research (Wii and DS system menus, taken as feel only)

Written 2026-11-13. Research only: nothing in `Assets/` was touched, no media was saved.
Brief from the owner: take how Nintendo's system menus **animate, sound and feel**, not their style, and apply it to our look ([References/ui/README.md](../../References/ui/README.md), [style_research.md](style_research.md)).
We take principles and behaviours only. No Nintendo assets, sounds, music, layouts, icons or marks, and our design is not described as, or built as, a copy of theirs.

**Tags.** [sourced: Sn] = a source below says it. [unverified] = widely repeated or remembered, but no source opened this session confirms it. [proposal] = our number or rule, for the owner to decide.
No claim here is tagged [measured from footage]: no video was watched or frame-counted for this note. **No Nintendo timing in ms was found in any source**, so every ms in Part 2 is ours.

---

## Part 1: what the Nintendo menus do

### 1.1 Wii Menu and the channel grid

- **Layout as "rows of panes" modelled on TVs in a shop window.** Kuroume: "Lots of screens, lots of channels, all lined up." The Disc Channel was deliberately not made bigger than the rest ("too orthodox", Tamaki). [sourced: S3]
- **Grid size.** Four pages of 4 × 3 channel slots (48), plus/minus on the remote scroll between pages. [sourced: S7]
- **The panes are live.** Buttons look like small TV screens showing animated images; they have a volumetric look that suggests depth. [sourced: S1] Some channels update what their pane shows when online (weather, My Nintendo). [sourced: S2, reader comment, weak]
- **Hover.** On highlight a channel changes size and contour colour, and its motion reads "like poking a box", as if pushed by a physical force. [sourced: S1] Any overshoot or wobble on hover, and its timing, is [unverified].
- **Hover label.** A description appears over a button after the cursor rests on it; it pops on and off in a single frame, and its sound makes it feel as if it grows toward you. [sourced: S1]
- **Page arrows.** Small arrows at the left and right edges pulse gently while idle. [sourced: S1] Their pulse rate is [unverified].
- **Clock and date** sit on a lower band below the grid. [sourced: S1, S7] Any idle motion of the clock (blinking colon etc.) is [unverified].
- **Zoom into a channel and back out.** Selecting a channel pane enlarges it into a full-screen preview before you confirm "Start"; backing out shrinks it back into the grid. [unverified: no source opened describes it or its timing.] This is the idea behind our window-zoom transition (2.2), drawn our own way.

### 1.2 The Wii pointer

- **Rotation with the remote's roll.** "The pointer accurately responds to twisting." [sourced: S2] The remote reports its orientation to gravity, which software uses for the pointer's angle. [sourced: WiiBrew search excerpt only, not opened; treat as unverified]
- **Trail.** The cursor is a stylised hand that leaves a fast-fading trace as it moves, read as fingertips touching a surface. [sourced: S1]
- **Open, pointing and grab hand states; the hand's drop shadow.** [unverified] Widely remembered; no source opened this session describes them.
- **Off-screen.** The pointer vanishes when the remote is aimed off the screen, and the system menus give no clue where it left. A 2006 review called this the main pointer annoyance. [sourced: S15] **Lesson: never lose the cursor or focus; always show where it is.**
- **Tethered pointers (later).** Skyward Sword's item ring tied its finger icon to a "string" so it never leaves the screen; the team first found it unsightly, then "very comfortable" in play. [sourced: S12] A game, not the system menu, but the same lesson as above.

### 1.3 Haptics and the remote speaker

- **Hover rumble.** Crossing onto a button gives a short staccato rumble, like stumbling over something. [sourced: S1] Rumble is subtle and marks each button you pass; the menus could be used almost without looking. [sourced: S2]
- Nintendo support tells owners to test rumble by pointing at the Wii button in the Menu's bottom-left corner, where the remote "should rumble slightly". [sourced: S16, search excerpt; the page itself returned 404]
- **Speaker.** The remote's own speaker plays a sound each time you change its volume in settings. [sourced: S17, search excerpt, not opened] Which menu sounds play from the remote speaker is [unverified].
- **Feel of a click.** Rumble, cursor motion and button motion together make the sensation of a click. [sourced: S1]

### 1.4 Mii Channel

- Director Nogami: faces aimed at a person's character, not realism; few options on purpose. [sourced: S4]
- Miis "struggle when you pick them up" (a reader comment, not the article). [sourced: S2, weak] Wandering, idle animation and the grab feel are otherwise [unverified].
- Music by Kazumi Totaka (2006). [sourced: S18, search excerpt] Nothing about its musical structure is sourced here, and we take nothing from it.

### 1.5 Wii Shop Channel

- **Design rule: strip it down.** UI designer Haruhana: "eliminating all unnecessary elements and left only what was truly needed." Everything had to read on a 15-inch screen. [sourced: S5]
- **Waiting is designed.** Six download animations; the coin count tracks download speed, so the wait carries information. Interrupted downloads resume. [sourced: S5]
- **Music was a decision**, commissioned from Totaka after the team asked whether it needed music at all. [sourced: S5]
- **A distinct sound per meaning.** The Shop's page script exposes separate effects: Hover/Target, Decide (select), Cancel (back), Choice Change (moving between options), Error, plus success sounds for points added and download finished, and a loading loop. [sourced: S6, community documentation from the sound archive's names]
- **Conflict to note:** S1 says the Wii Menu's only interface sound is the one on the hover label. S6 lists a hover sound, but for the Shop Channel. Both may be true of their own screens; I could not settle it for the Wii Menu itself.

### 1.6 DS, DSi and 3DS menus

- **DSi Menu goal**: "letting you do what you want in an instant" (Iwata); rows of icons readable at a glance because many downloads were expected (Matsushima). [sourced: S8]
- **The "Nintendo touch"** (DSi browser team, Furukawa): "how satisfying the unit's response is when you press a button" and the scroll speed. Screens don't just switch; their images "rapidly shrink or enlarge", which "really changes the way it feels" (Shirakawa). Scrolling works even with the stylus at an angle. [sourced: S10] **This is the plainest developer statement of the feel we want.**
- **Sound vocabulary (a homebrew replica's model, not Nintendo's files).** The TWiLight Menu++ DSi/3DS theme has separate slots for: startup jingle, back, launch, select (cursor moves), **wrong (you hit the end of a page)**, switch (page change), and, DSi theme only, **stop (the cursor stops moving)**. [sourced: S14] That a replica needs these slots suggests the originals distinguish them; exact behaviour of Nintendo's own menus is [unverified].
- **3DS HOME Menu**: reachable at any time from the HOME button; the game pauses underneath. [sourced: S11] Nothing on its animation or sound timing was found.
- **DSi Sound** was built for play: pitch and speed changed with the stylus "like tape recorders" (Miyamoto). [sourced: S9]
- **Switch OS (later, for the principle):** sound effects are paired with on-screen motion so actions are easier to follow; "rhythmic" sound effects and no background music; animations kept as short as possible while still feeling responsive. [sourced: S13, CEDEC 2018 as reported]

### 1.7 Input feel

- **Large targets.** Buttons are big enough to hit easily with the pointer. [sourced: S2]
- **Forgiving stylus.** DSi scrolling works "the way you want" even with the stylus held at an angle. [sourced: S10]
- **Cursor magnetism or snapping** toward Wii Menu buttons: [unverified]. **Stylus and D-pad parity** on the DS menus (both reach every item): [unverified].

### 1.8 Sound across the menus

- Wii Menu theme: gentle, high, soft-timbred, overlapping echoing lines. [sourced: S1] Shop and Mii music were by Totaka. [sourced: S5, S18]
- **Hover and select sounds tuned to the music's key**: [unverified]. The one page that seemed to claim it was an empty "coming soon" page (S19). No developer statement was found. We adopt tuning as our own rule (2.3), not as a Nintendo fact.
- Silence rules (when menus stay quiet): [unverified]. No source covers rate limits or held repeats.

### 1.9 The principle under all of it: everything answers you

Every input gets at least two answers at once: motion plus sound (S1, S13), motion plus rumble on Wii (S1, S2). Waits carry information (S5). Edges answer too: the end of a page has its own sound (S14). The one failure all sources point at is the cursor going missing (S15). **What makes it feel good is that each control answers on more than one channel, with short motion, and the player never has to wonder where they are.**

### 1.10 What I could not verify

Any Nintendo duration or frame count; hand-cursor states and shadow; hover wobble or overshoot; press squash on buttons; a selection pulse or shine; page-scroll motion between grid pages; cursor magnetism; channel zoom in and out; page-arrow pulse rate; clock idle motion; Mii wandering; key-tuned sound effects; silence and repeat rules; which sounds go to the remote speaker; 3DS/DSi animation timing. **To close these, someone would frame-count captured footage at 60 fps (frames ÷ 60 × 1000 = ms).** That was out of scope (no media saved).

---

## Part 2: translation for Seal the Deal (our look, their feel)

Every number and rule in Part 2 is a **[proposal]** for the owner, except values marked **(§8)**, which are kept from [style_research.md](style_research.md) §8.

### 2.1 Principles we take

1. **Everything answers, on two channels.** Every hover, focus, press, confirm, back and refusal gets motion plus sound; pads add a haptic only where it carries meaning.
2. **Hover lifts, press stamps.** A hovered plate rises (a 2 px offset hairline shadow appears); a pressed plate stamps down (shadow to 0, 2 px shift). One physical metaphor, our paper-and-desk one.
3. **Go into the window.** Screens open by zooming into a sky pane of the title frame and close by pulling back out, so the player always knows where they are.
4. **Never lose the player's place.** The cursor and the focus brackets are always visible, edges answer with a bump, focus is never hidden mid-navigation.
5. **Sounds are one instrument in one key.** All pitched UI sounds sit on one root and intervals above or below it; office materials (pen, stamp, paper, lift) make the body.
6. **Short and interruptible.** User-initiated motion ends in 300 ms or less and can be overridden at any frame; input never waits for animation.
7. **The waiting is designed.** Loading, joining and searching show progress honestly (linear only here) and in our voice.
8. **Precise, not bouncy.** The art bible (§14) and [style_research.md](style_research.md) §8 rule out bounce. We take Nintendo's warmth through sound and responsiveness, not wobble.

### 2.2 Motion

**One spring notation: damping ratio ζ + response T (seconds).** Mass 1, so in Unity:
`k = (2π / T)²`, `c = 4π·ζ / T`; integrate on unscaled time (menus run while the game runs). At ζ = 1.0 there is no overshoot and the value settles within 2 % after about **0.93 × T**.
Springs are used **so motion can be interrupted mid-flight and keep its velocity**, not to bounce. That is how this note reconciles with §8's "no springs, no overshoot": ζ = 1.0 everywhere. **The one underdamped spring is the error shake (ζ 0.25)**, the single exception to §8's "no overshoot": a refusal is meant to wobble.

Easing tokens (cubic-bezier, for tweens): **OUT** (0.2, 0, 0, 1); **OUT-EXPO** (0.16, 1, 0.3, 1); **IN** (0.4, 0, 1, 1); **IN-OUT** (0.65, 0, 0.35, 1); **LINEAR** only for progress, the clock and idle drift. Exits run at about 70 % of the entry with IN (kept from §8). All values at 1920 × 1080 reference; scale px with the canvas.

| Event | What animates | Duration | Easing / spring | Notes |
|---|---|---|---|---|
| Cursor hover enter | Corner brackets close 6 → 0 px; 2 px offset hairline shadow grows 0 → 2 px (lift); fill to hover tint | 100 ms | OUT | No scale change on text. Shadow is navy at low alpha, never black fill. |
| Cursor hover exit | Reverse | 70 ms | IN | Leaving a padded hit area, not the plate edge (2.4). |
| Focus move (keys / pad) | **Selection changes on the input frame.** Only the bracket cluster and chevron cue travel to the new item | settles ≈ 110 ms | spring ζ 1.0, T 0.12 s | Interrupted by the next repeat without restarting. Overrides §8 (140 ms ease-in-out) and ux_research (≤ 120 ms ease-out); reason: held repeats at 10 / s must not queue. |
| Press down | Shadow 2 → 0 px, plate shifts +2 px right and down, fill to navy | 40 ms | OUT | The "stamp". Fires on button down. Overrides §8's 60 ms linear: no linear motion. |
| Press up | Plate returns to hover lift | settles ≈ 150 ms | spring ζ 1.0, T 0.16 s | Interruptible if pressed again. |
| Confirm | Cyan ◆ ticks on at the plate's left edge (scale 0 → 1, square, no radius); the press-up runs; the next screen starts at once | 120 ms | OUT | Transition starts on release, not after this tick. |
| Back | Current panel leaves 16 px toward its origin + fade; the previous one enters from the opposite side | 140 ms out, 200 ms in, overlapped (ends ≤ 260 ms) | IN / OUT-EXPO | Esc, B / Circle, right-click on empty space. |
| Error / refusal | Amber plate fades in behind the refused control; the control shakes horizontally (6 px peak, ≈ 3 cycles, decays) | 240 ms | spring impulse ζ 0.25, T 0.08 s | **Exception to §8's no-overshoot.** Navy text on amber, never amber text. Use for "Stairs locked", "Lobby full", rebind conflict. One shake per input; held input repeats only the sound rate-limited (2.3). |
| Tab switch | 2 px cobalt rule travels under the new tab; content swaps with 12 px slide in the travel direction + fade | rule settles ≈ 150 ms; content 160 ms (out 110 ms) | spring ζ 1.0, T 0.16 s; content OUT / IN | Content of old tab exits while the rule is moving. |
| Panel / modal open | (§8) sequence kept: 24 px slide + fade, hairline draw from anchor (16 ms stagger), document reveal ≤ 6 lines | (§8) 200 ms slide, hairline 160 ms, reveal 16 ms stagger; ends ≤ 280 ms | OUT-EXPO; hairline and reveal OUT | Modal behind-veil is **paper white at 60 %**, not a dark dim (no dark panels). |
| Panel / modal close | Slide 16 px + fade; veil out | 140 ms | IN | **One value for every panel, the in-game Esc panel included** (ux_research's 150 / 120 ms is overridden for consistency). Input works from frame 0. |
| Screen to screen ("go into the window") | The chosen sky pane of the title frame scales from its rect to full screen; mullions slide out past the screen edges; new screen's plates enter on top of the now-full sky | pane 240 ms; new plates start at 120 ms, 160 ms each (ends 280 ms) | pane IN-OUT; plates OUT | The sky is continuous, so the player "steps through" a window. Back out = reverse at 240 ms. Each top-level screen owns one pane (Host = left, Join = centre, Settings = right) **[proposal]**. |
| System transition (match start, results) | Thresholded mask wipe | (§8) 400–600 ms | IN-OUT | **Exception to 300 ms: not user-initiated** (it waits on the network). |
| Toast / banner in | Slide 16 px down + fade | 160 ms | OUT | Hold: 2 s info, 4 s event; never stacks over 4 rows (ux_research). |
| Toast / banner out | Slide 8 px up + fade | 110 ms | IN | ux_research had 200 ms out; changed to the 70 % rule. |
| HUD clock tick | Digits replace instantly every second (mono face, no motion) | 0 ms | none | Each new minute: odometer roll of the minute digit, (§8) 220 ms OUT. Last 10 s: the plate's 1 px keyline goes amber; no flashing. |
| Briefcase tracker change | Old plate slides 8 px out + fade; new plate (cyan ◆ ours / amber ▲ rival) in; one edge flash when the rival takes it | 110 ms out, 200 ms in; flash (§8) 50 ms in, 250 ms out | IN / OUT | System event, so easing not spring. Flash once, never loops (< 3 Hz rule). |
| Floor opened | Floor numeral odometer roll; banner toast | (§8) 220 ms | OUT | Pairs with the lift chime. |
| Pickup prompt appear | "F · Pick up" plate fades in and rises 4 px | 120 ms in, 80 ms out | OUT / IN | Shows after 100 ms of steady aim, hides after 150 ms of lost aim (hysteresis, so it never flickers at an edge). Silent on appear. |
| Idle: clouds | Cloud layers drift right inside each pane, each pane at its own speed (parallax) | continuous | LINEAR | 6, 8 and 10 px / s (back to front) at 1080p. **Slow enough that you notice only after looking away and back.** |
| Idle: sheen | One light band crosses one pane's glass | 1.6 s travel, every 9–14 s (random) | IN-OUT | Peak 12 % white. Never crosses text. |
| Idle: focus breathing | Focused button's brackets breathe 1 px out and back | 2.4 s period, sine | sine | Starts after 3 s with no input; stops on the first input frame. |
| Reduced motion (setting) | Every translate or scale becomes an opacity change of the same duration; screen zoom becomes a 160 ms cross-fade; shake removed (amber plate + 2 px keyline instead); springs snap with an 80 ms fade; idle drift, sheen and breathing off | as above | OUT / IN | Sound is unchanged: it still carries the answer. |

**Exceptions to the 300 ms cap (all not user-initiated):** system transition 400–600 ms; idle cloud drift, sheen 1.6 s and breathing 2.4 s; toast hold times; the clock's last-10-s state.

### 2.3 Sound

**One instrument, one key [proposal].** Pick the menu music's key first; every pitched UI sound sits on that key's root **R** or a consonant interval from it. If the music is in D, R = D6 (≈ 1175 Hz) for the small ticks. Changing the music's key means re-pitching the set, so keep each sound as a pitched layer (a sine or soft bell partial, pitch-set in the synth) over an unpitched material layer (the office object).
Materials: pen click, rubber stamp, paper slide, card divider, latch, lift chime, desk bell muffled in cloth. **All recorded or synthesised fresh; no existing game or system sound and no melody is referenced or reproduced.**
Loudness is relative to Confirm = 0 dB on the Interface bus. Variation: 3 takes of the material layer, picked at random; **the pitched layer never randomises** (that is what puts sounds out of tune).

| Event | Character (for a sound designer or synth pass) | Pitch | Level | Silence and rate rules |
|---|---|---|---|---|
| Hover enter | Soft paper tick: 2 ms attack, 25 ms, band-passed noise around 3 kHz with a faint sine body | R | −16 dB | Max 1 per 60 ms; a fast mouse sweep plays at most ~8 / s. None on disabled items. None on hover exit. **[proposal; §8 had "none", keep that as the fallback if it tires]** |
| Focus move (keys / pad) | Dry pen-cap tick: 1 ms attack, 30 ms | R | −12 dB | Held repeat: one per step up to 10 / s, then every other step; the first and last steps always play. Silent when focus moves by code (screen open, default focus). |
| Focus stops after a held repeat | Softer tick, 40 ms | major third below R | −18 dB | Only after ≥ 3 repeats, so a single step stays a single sound. |
| End of list / edge bump | Muffled desk-bell tap, 60 ms, 4 px nudge on the brackets | minor second above R (deliberately unresolved) | −14 dB | Max 1 per 250 ms when held against the edge. |
| Press down | Pen click, two clicks 12 ms apart, 30 ms total | unpitched | −8 dB | On button down, before the action. |
| Confirm | Rubber stamp: felt thump (≈ 120 Hz, 60 ms) + paper slap + a bell partial | perfect fifth above R, 160 ms decay | 0 dB (reference) | On release / submit. Ducks the music 4 dB for 300 ms. |
| Back / cancel | Paper slid back across a desk, 110 ms, filtered noise sweeping down | perfect fourth below R, short partial | −6 dB | Same sound for Esc, B / Circle and on-screen Back. |
| Error / refusal | Dull rubber stamp twice, 70 ms apart, pressed hard; no buzzer | tritone below R, short | −4 dB | Max 1 per 400 ms. Never on hover of a disabled item, only on pressing it. |
| Tab switch | Card-index divider flick, 40 ms | major second above R | −10 dB | Bumpers held: rate-limited like focus. |
| Panel / modal open | Paper slide out of a folder, 180 ms, noise sweep up | unpitched | −10 dB | — |
| Panel / modal close | Same material, 120 ms, sweep down | unpitched | −12 dB | Not played when Back already played on the same frame. |
| Screen zoom in / out | Soft air push with a faint glass ring at the end, 260 ms | ring at R one octave down | −8 dB | — |
| Toast / banner | One clean bell partial, 200 ms | major sixth above R | −10 dB | One per 1.5 s; queued toasts arrive silent. |
| Briefcase: we take it | Latch clack + two rising partials | R then fifth above, 80 ms apart | −2 dB | Gameplay-critical; also a voice line (ux_research). |
| Briefcase: rival takes it | Latch clack + two falling partials | fifth above R then R | −2 dB | Plus the edge flash (2.2). Amber state is never sound-only. |
| Floor opened | Lift chime, two notes, 600 ms decay | R and major third above | 0 dB | World-anchored at the stair door as well. |
| Clock, last 10 s | Wall-clock tick, 20 ms | R one octave down; last 3 s up a fifth | −14 dB | Only in the last 10 s; muted when the in-game panel is open. |
| Pickup (the action, not the prompt) | Briefcase handle or object grab, 80 ms | unpitched | −6 dB | Prompt appearing is silent. |
| Menu ambience **[proposal]** | Quiet office air: HVAC hum, distant lift, a far keyboard, under a sparse music bed in the chosen key | — | −24 dB | Music stops on match load; no music under the in-game Esc panel (the game keeps running). |

Global silence rules: no UI sound while the window is unfocused; no sound for focus changes the player did not cause; at most one UI sound starts per frame (the higher-priority one wins: error > confirm > back > press > focus > hover).
Pad speaker (DualSense): **not used for UI [proposal]**: the PC version has no Wii-style remote speaker to lean on, and sound from two places splits attention.

### 2.4 Cursor and focus

**Mouse**
- **Custom cursor [proposal]:** a navy arrow with a 1 px white keyline, 24 px at 1080p, set as a **hardware cursor** (`Cursor.SetCursor`) so it never lags a frame. `Cursor.SetCursor` does not scale with resolution, so ship each texture at 24, 36 and 48 px and pick by screen height. Three textures: idle; over interactive (the arrow gains a small bracket and a 2 px hairline shadow, our version of a pointer that "lifts"); pressed (shadow gone, shifted 1 px). No rotation: a mouse has no roll.
- **No positional magnetism with a mouse.** Moving a PC player's pointer is hostile. Forgiveness comes from hit areas instead.
- **Hit area padding:** every control's hit rect extends **8 px** past its plate; gaps between controls stay ≥ 16 px so padded areas never overlap. Hover stays on until the cursor leaves the padded rect by a further **4 px** (hysteresis), **but entering another control's padded rect takes hover at once**; hysteresis applies only when leaving into empty space, so two controls are never hovered together.
- Cursor hides during play; it reappears in the centre of the menu, never off-screen, when a menu opens.

**Keyboard**
- Focus ring and brackets show only after a key or pad input ("hidden focus" while the mouse is in use); the first arrow press shows focus on the current item without moving it.
- Arrows move, Enter / Space confirm, Esc back, Tab follows reading order.
- Repeat **[proposal]:** our own repeat, not the OS one: 400 ms initial delay, then every 100 ms; on lists over 12 items, every 60 ms after 1 s held.

**Gamepad**
- **Snapping focus:** the next item is the nearest centre inside a 45° cone in the pushed direction; no wrap at list ends (edge bump instead, the DS "end of page" idea).
- Stick: navigation threshold 0.5, release below 0.3 (hysteresis); same 400 ms delay and 100 ms / 60 ms repeat as keys.
- **Rumble on focus moves: off by default [proposal].** Reason: the Wii needed hover rumble because a pointer in the air has no physical edges (S1, S2); a d-pad or stick step is already a physical click, and buzzing at 10 / s on held repeats is noise. **On: one 30 ms low-intensity pulse (0.15, low-frequency motor) on Confirm and a 60 ms double pulse on Error**, behind a "Menu vibration" setting that also covers the focus option.
- Glyphs switch with the last device used (Xbox / PlayStation / keys), within one frame.

### 2.5 Idle life rules

- **May move when nothing is touched (title and lobby only):** clouds in the panes (6–10 px / s), one sheen band every 9–14 s, focus breathing after 3 s idle, and the halftone and microtype staying still. Lobby: the "Searching" or "Waiting for players" line may use a looping chevron-row step (3 chevrons, 400 ms per step, LINEAR); it is a progress indicator, not decoration.
- **Must never move:** any text, numbers, buttons' positions, the title lockup, the HUD in play (clock, tracker, health, crosshair, prompts) except when its value changes. **No idle motion on the HUD at all.**
- Idle motion pauses when the window loses focus and when reduced motion is on.
- Sanity check: if the owner notices the clouds moving within 2 s of looking at the title screen, they are too fast.

### 2.6 What makes it feel cheap

- Linear easing on anything that moves (not progress).
- No pressed state, or a press that only fires on release.
- Pitched sounds out of key with the music, or pitch-randomised on every play.
- Everything overshooting or bouncing; several things competing at once.
- The same sound for confirm and back, or a buzzer for errors.
- Hover sounds machine-gunning on a mouse sweep; sounds on code-driven focus.
- Input locked while a panel animates; queued focus moves on held repeat.
- The cursor or focus going missing, or focus landing on nothing after a screen change.
- Hard cuts between screens; a default OS cursor over a styled screen.
- Motion durations that differ between similar controls.

### 2.7 First 10 things to build (priority)

1. A `UiFeel` asset: the easing tokens, spring (ζ, T) presets and durations above, read by every control. One place to tune.
2. A tiny critically-damped spring helper on unscaled time (the `k` / `c` formula above), interruptible.
3. Button states: hover lift, press stamp (down on press), release spring, disabled with reason, focus brackets.
4. The UI sound set at R with the interval table, routed to an Interface mixer bus, with the rate limiter and priority rule.
5. Focus navigation: snapping cone, repeat timing, hidden-focus switching, edge bump.
6. Hardware cursor with three textures and padded hit areas with hysteresis.
7. Panel open / close and Back sequence (one timing set for all panels, Esc included).
8. "Go into the window" transition from the title frame panes.
9. Error / refusal: amber plate + decaying shake + double-stamp sound.
10. Title idle life (cloud parallax, sheen, breathing) and the reduced-motion setting that turns it off.

### 2.8 Changes to style_research.md §8, for the owner

| §8 said | Now | Why |
|---|---|---|
| No springs, no spring notation | Springs at ζ 1.0 (no overshoot) for interruptible moves | Held repeats and fast clicks must interrupt without restarting. |
| Focus move 140 ms ease-in-out | Selection instant; brackets spring ζ 1.0, T 0.12 s | Input state never waits; visuals catch up. |
| Press 60 ms linear down, 120 ms ease-out up | 40 ms OUT down; release spring ζ 1.0, T 0.16 s | No linear motion; release interruptible. |
| No overshoot anywhere | Error shake only, ζ 0.25 | A refusal should visibly wobble; nothing else does. |
| Hover: no sound | Very quiet rate-limited tick **[proposal]** | Everything answers; keep "none" as fallback. |
| (ux_research) Esc panel 150 / 120 ms | 200 / 140 ms like every panel | One timing set. |
| (ux_research) Toast out 200 ms | 110 ms | Exits at about 70 % of entry. |

Kept from §8: hairline draw, panel slide, document reveal, floor odometer, warning flash, hit marker, system transition, exits at 70 % with ease-in, menus on unscaled time.

---

## Sources

Opened and read this session:
- S1 Altug Isigan, "Sense and Sensibility in the Nintendo Wii", Game Developer, 2011-04-14: https://www.gamedeveloper.com/design/sense-and-sensibility-in-the-nintendo-wii
- S2 Jonathan Metts, "Praise for the Wii Menu", Nintendo World Report, 2008-09-16: https://www.nintendoworldreport.com/blog/16696/praise-for-the-wii-menu
- S3 Iwata Asks: Wii Channels, part 2: https://nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-Wii/Iwata-Asks-Wii-Channels/2-Redefining-the-Game-User-Relationship/2-Redefining-the-Game-User-Relationship-213553.html
- S4 Iwata Asks: Wii Channels, part 4 (Mii Channel): https://nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-Wii/Iwata-Asks-Wii-Channels/4-Aiming-For-Worldwide-Mii-Communication/4-Aiming-For-Worldwide-Mii-Communication-213650.html
- S5 "The Developers Talk About the Wii Shop Channel" (Nintendo, 2011), community mirror: https://interviews.rc24.xyz/story_shop.html
- S6 Open Shop Channel docs, Wii Shop Channel JS sound API: https://docs.oscwii.org/wii-shop-channel/js/sound
- S7 Wikipedia, "Wii Menu" (read in two parts; neither covered sound or transitions): https://en.wikipedia.org/wiki/Wii_Menu
- S8 Iwata Asks: Nintendo DSi, vol. 2 part 1: https://www.nintendo.com/en-gb/Iwata-Asks-Nintendo-DSi/Volume-2-DSi-Internal-Software/1-Presenting-a-New-DS/1-Presenting-a-New-DS-1049333.html
- S9 Iwata Asks: Nintendo DSi, vol. 2 part 3: https://nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-Nintendo-DSi/Volume-2-DSi-Internal-Software/3-Playing-with-Sound-and-Music/3-Playing-with-Sound-and-Music-1049337.html
- S10 Iwata Asks: Nintendo DSi Browser, part 2: https://www.nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-Nintendo-DSi/Volume-3-Nintendo-DSi-Browser/2-A-Browser-with-the-Nintendo-Touch/2-A-Browser-with-the-Nintendo-Touch-1049344.html
- S11 Iwata Asks: Nintendo 3DS, vol. 6 part 7: https://www.nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-Nintendo-3DS/Vol-6-Nintendo-3DS-Pre-installed-Software/7-To-Many-People/7-To-Many-People-224042.html
- S12 Iwata Asks: Skyward Sword, vol. 1 part 3: https://www.nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-The-Legend-of-Zelda-Skyward-Sword/Vol-1-Wii-MotionPlus-Inspires-New-Controls/3-Selecting-Items-Without-Looking/3-Selecting-Items-Without-Looking-236196.html
- S13 Nintendo Wire on the CEDEC 2018 Switch OS talk: https://nintendowire.com/news/2018/08/22/nintendo-talks-about-the-design-of-the-switchs-os-at-cedec-2018/
- S14 DS-Homebrew wiki, TWiLight Menu++ DSi/3DS custom SFX (a homebrew replica): https://wiki.ds-homebrew.com/twilightmenu/custom-dsi-3ds-sfx
- S15 Kyle Orland, "Dude, where's my cursor", Engadget, 2006-11-11: https://www.engadget.com/2006-11-11-dude-wheres-my-cursor-wii-annoyance-1.html

Search excerpts only (page not read in full):
- S16 Nintendo support, "Wii Remote doesn't rumble" (404 when fetched): https://nintendo.com/en-gb/Support/Wii/Troubleshooting/Wii-Remote-Controllers-amp-Sensor-Bar/Wii-Remote-doesn-t-Rumble/Wii-Remote-doesn-t-Rumble-244315.html
- S17 Nintendo support, "Adjust volume of internal speaker": https://nintendo.com/en-gb/Support/Wii/Setup-amp-Connect/Set-Up-and-Sync-the-Wii-Remote/Adjust-volume-of-internal-speaker/Adjust-volume-of-internal-speaker-244431.html
- S18 Square Enix Music Online, Kazumi Totaka credits: https://squareenixmusic.com/composers/nintendo/totaka.shtml

Failed or empty:
- S19 gamejuice.pages.dev "UI audio" (an empty "coming soon" page): https://gamejuice.pages.dev/articles/ui-audio-silent-interfaces
- iwataasks.nintendo.com Wii Channels part 1 and DSi vol. 1 part 2: certificate errors (the nintendo.com/en-gb mirrors were used instead where found).
