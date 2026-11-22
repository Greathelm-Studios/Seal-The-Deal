# Handover: briefcase game mode (game mode 1)

**Archived 2026-11-22:** the briefcase mode landed; current doc [MATCH.md](../systems/MATCH.md).

**Status:** Target · **Last updated:** 2026-11-12 · **From:** Adam / Claude Code (game mode lane, branch `claude/lane-gamemode-29a356`)

## Goal

Two rival law firms race for one shared briefcase; the firm whose player carries it onto either roof wins. Nobody climbs above the highest floor the briefcase has been carried onto. At 15:00 the firm holding it (or that carried it last) wins. The system is in [systems/MATCH.md](../systems/MATCH.md).

```
Match (host-owned, synced on change)

 Waiting ──Start (host menu) or HostStartRun (lobby)──▶ Countdown 5 s ──▶ Running ──carrier on either roof──▶ Ended
   ▲                                                                       │      ──clock 15:00───────────▶ (winner, reason)
   └──────────────── Rematch (host): new seed, everyone to reception, case to the plaza ◀───────────────────────┘

The one briefcase (never despawned)

 Dropped ──F, anyone in reach──▶ Carried ──G / knocked / fell out / left game──▶ Dropped
    └── outside buildings, plaza and bridges 2 s ──▶ Returning ──3 s──▶ Dropped on the highest checkpoint's skybridge (or the plaza)

The open floor (host-owned, only rises during a match)

 0 (ground) ──carrier stands on floor N+1──▶ N+1 ──▶ … ──▶ roof
 Gates above the open floor are shut in both towers; the carrier walks through them; everyone else is told why.
```

## Where it stands

**Ready for review**, not approved. **Gameplay is frozen** (Adam, 2026-11-14: "Don't worry about any more gameplay or mechanics being worked on"): the rules, gates, tower and netcode stay as they are; only UI work continues in this lane.

Open, in priority order:
1. **Gates or locked doors** (below): Adam's call; both are in the build today.
2. **Results screen:** lane 5's to build. This lane hands over `MatchDirector.Snapshot`, `ReasonLine(result, firm)` and `OnMatchEnded` (MATCH.md, UI contract); the menu's result section (`MatchMenuPanel`) stays until that screen lands, then goes.
3. **720p captions:** the kit's smallest captions render about 9 px at 720p; waiting on lane 5's type scale.
4. **Menu section move:** lane 5's in-game menu moves `MatchSection` onto its glass pane (`KitUI/InGameMenu/Content/MatchSlot`); after that lands, point `EnsureMenuSection` at that slot for fresh scenes (agreed on AgentHub #529, #530).
5. **Held-object seat:** the case sits small in the lower left (gun hand is the right, AgentHub #492, #535). Harvey's viewmodel can give the left arm a seat on the case later.
6. **Player Settings name:** `productName` is still `CloseTheDeal`, `companyName` `DefaultCompany` (built executable name, shared PlayerPrefs file). Renaming moves saved settings: Adam's call.
7. **The roof win is a text card:** no roof moment yet (out of scope while gameplay is frozen; a UI-only stamp before the menu opens is the smallest fix).

Done on the UI side (2026-11-14): kit HUD and menu section; contrast measured from real pixels; cap warning on an amber plate; 21:9 and reduced-motion captures; the kit's edge marker for the off-screen briefcase; wiring guards so a stale scene can't stop a spawn; `Lane_Integration.unity` rebuilt twice and proven in a macOS build; Set Up Integration made add-only for `Player.prefab`.

## What was checked

- **Compile:** `node ~/.claude/skills/compile-check/compile-check.mjs`: PASS, 0 errors.
- **Solo Play mode, host only** (headless editor, scripted driver; clip `gamemode_shared_briefcase.mp4` in the lane sessions' shared notes folder): 26/26 checks passed.
  - The case spawns on the plaza exactly 6 m from both window walls.
  - Reception → plaza over the sill on real physics.
  - Pick up; checkpoint saves firm A's respawn and the case's return floor.
  - **Crossed the Checkpoint 1 skybridge into Building B on real physics** (over both sills, between the pillars), empty-handed.
  - Carried it onto the **rival tower's roof**: win for the carrier's firm.
  - A fall while the result is showing is caught.
  - Rematch resets checkpoints and returns the case to the plaza.
  - Roof without the case: notice, no win.
  - Carrier blasted over the bridge rail: case dropped, player back on Checkpoint 1, case back on the bridge centre 2 s + 3 s later, still exactly one case.
  - Clock with the case held: holder's firm wins. Dropped: last carrier's firm wins. Never carried: "No deal."
  - `HostStartRun` moved the host to firm 1 and started a run from Building B's reception.
- **Host + one client, then a second client** (two editors on one Mac over loopback, not Steam):
  - **Steal:** the host carrier was knocked, the client of the rival firm picked the case up and won on Building B's roof. Both machines showed "Halloran Voss sealed the deal."
  - **Same-tick grab:** both players beside the case, both requests handled in the same tick: exactly one got it (the first handled).
  - The client saw the case as amber "The rival firm has it, Checkpoint 1, the rival tower" when it joined mid-match.
  - **Carrier quit mid-carry:** case dropped where they stood, one case.
  - **Mid-match joiner:** spawned on their firm's Checkpoint 1.
  - **Host left:** the client's match closed and its HUD hid.
- **Complexity:** `lizard -l csharp Assets/_Project/Scripts/Match`: highest CC 10 (`MatchDirector.RaiseBriefcaseEvent`), nothing at 11+.
- **Bandwidth, 8 players:** match state is a few bytes per event, nothing per frame. The worst steady cost is the loose case moving at 30/s to 7 clients, about **4 KB/s** from the host, and zero once it settles.
- **Floor gate, solo and host + clients:** a player without the case is refused with the "shut" line and stopped on physics at a shut gate (host and the client's own body both 2.45 m from the stairwell, gate at 2.75 m; last client correction 0.03 m). The carrier is exempt on both machines. Each carrier arrival opens the floor in both towers on every machine. A carrier knocked off the bridge leaves the floor open and respawns at or below it. A rematch shuts every gate again. A late joiner got open floor 4 with the right gates shut.
- **Not seen:**
  - The carrier walking through a shut gate on key presses (the exemption was checked directly, and arrival was teleported).
  - Climbing the real stairs and mantling onto a bridge by key press: the climb was teleported, and the crossing was driven by knockback shoves.
  - Steal by a real client key press (the client's grab was its own F request; the host knock was scripted).
  - Steal while the carrier is on a bridge.
  - Steam, and real latency.
  - Pad bindings.
  - A run in a build.

## Rules for Adam to confirm (most important first)

0. **The floor gate, as read from your words** "players can only up to different floors if anyone with the brief case is on said floor everyone can move up": the open floor is the highest floor the briefcase has been carried onto; nobody in either tower can go above it; a carrier standing on a new floor opens it for both towers for the rest of the match; dropping it never closes a floor. Only the carrier can pass a shut gate; anyone else is told why. Interpretation.

1. **Interact is F, not E.** You said E, but E is lean-right in the shared controls and the shared Interact is already F. It is one constant in `FirmMember` to switch.
2. **One shared briefcase; anyone can take it; carrying it onto either roof wins for the carrier's firm.** Your words; the "either roof" part is the orchestrator's default.
3. **It spawns on a plaza between the two towers**, level with both receptions and the same distance from each. Assumption.
4. **Skybridges on every checkpoint floor**, built only in the lane scene (and any scene where you run Add GameMode To Open Scene). The tower geometry is unchanged. **Crossing is a design decision**: the zipline gun is not built. Assumption.
5. **A case that leaves the buildings comes back** after 2 s + 3 s to the skybridge of the highest checkpoint floor it has been on, or to the plaza. Assumption.
6. **A firm's respawn point moves when any of its players stands on a checkpoint floor of their own tower.** The briefcase no longer saves anything per firm. Assumption.
7. **At 15:00:** the firm holding the case wins; if it lies loose, the firm that carried it last; if nobody ever picked it up, no winner. Proposal.
8. **Firm names: one source of truth, proposed.** The lobby (`LobbyRules.asset`: Cobalt, Cyan) and the match (`Profiles/Match/Firm_*.asset`: Ashcombe & Pryce, Halloran Voss) name the same two firms by index. Proposal: the two `FirmDefinition` assets in `Profiles/Match/` become the only place a firm's name and colour live, and `LobbyRules` points at them instead of holding its own strings. Which names go in is your call; this lane does not pick them.
9. **Timings:** countdown 5 s, stray 2 s plus return 3 s, fall 1.2 s.
10. **Rematch builds new towers from a new seed.**
11. **Teams balance by head count** when nobody chooses them; a lobby can set them with `HostStartRun`.
12. **The host starts the match** from the menu until the lobby lane calls `HostStartRun`.

GDD mismatches (the GDD is the authority on design; your 2026-11-12 words win for this mode; GDD not edited):

- GDD: "first team to reach the roof wins". Here reaching a roof needs the briefcase.
- GDD: at the cap "the team that has climbed highest wins". Here the case's holder or last carrier wins.
- GDD: the secretary starts the run. Here the host's menu button does, or the lobby.
- GDD: killed on a floor respawns there after 5 s. There is no health in this branch; only the window-exit-to-checkpoint rule is built.
- The brief says checkpoints on floors 3, 6 and 9. The tower inserts a checkpoint floor after every 3 offices, so they are stack floors 4, 8 and 12 (named Checkpoint 1–3).
- The game's name is now "Seal the Deal"; the greybox menu still says "SEAL THE DEAL" (`GreyboxUiSetup.cs`, not this lane's).

## First 30 seconds for a stranger (UX slice, 2026-11-13)

- **Briefcase beacon:** a soft column of light over the case, drawn through floors and walls within 30 m (`Beacon Range` on the match profile), white while loose, cyan or amber while a firm carries it; hidden from the carrier. Seen in a headless capture through the reception glass. Not judged by a person.
- **First shut gate of the match** explains the rule ("Nobody climbs higher than the briefcase has been. Whoever carries it up there opens the floor for everyone, in both towers."); later ones get the short line. Host and client seen.
- **Who has it:** `OnBriefcaseStolen(byFirm, fromFirm)` when it passes between firms; `OnFloorOpened(floor, byFirm)` names who opened a floor; `OnCapWarning` at 60 s left. Host and client seen, except the cap warning (only reachable by running the clock down; not seen).
- **A late joiner is not shown news from before they arrived** (no "Floor 4 opened" on join); they read the current state. Seen on a late joiner.
- **Shots pass through shut gates** (`Let Shots Through` on `MatchGates`, on): a weapon-mask ray passes the panel, bodies are still held. Not checked with a client pressing fire.
- **Doorways:** the middle reception bay lets players out to the plaza and back over the sill on physics; a glazed bay holds. The kit glass's diagonal streaks are strong now that every window has glass: a look call for the glass owner.
- **Every match input answers (2026-11-13):** a shut gate pulses brighter the moment you walk into it and plays the refusal sound; the case squashes and settles when it changes hands; the countdown, a floor opening, the case taken or dropped, a checkpoint, the cap warning and the result each play their sound from lane 9's set (`MatchSounds`). Sounds were seen starting in a headless run (the sound player's focus check bypassed for the test); nobody has heard them. After merge, run **Add GameMode To Open Scene** on `Lane_Integration.unity` to give its Match object `MatchSounds`.
- The UI contract a HUD reads (state, events, types, when they fire, sound mapping) is in [MATCH.md](../systems/MATCH.md). The current greybox `MatchHud` / `MatchMenuPanel` are replaced once the UI foundation lands.

## Match HUD on the UI kit (2026-11-14)

The greybox HUD and the menu's match section are rebuilt from the UI kit (lane 5's Plate, StatusLine, KeyHint and Button prefabs, `UiTokens`, `UiMotion`, `UiSound`). Ready for review; seen headless in `Lane_Integration.unity` at 1920×1080 and 1280×720 on the host, a client and a late joiner (captures: `gamemode-hud-captures/` in the lane sessions' shared notes folder).
- Results: lane 5 owns the full results screen; this lane keeps the data (`Snapshot`, `ReasonLine`, `OnMatchEnded`) and the menu's result section until that screen lands; lane 11's `MatchRecords` holds per-player stats. Agreed on AgentHub (#460, #471).
- The off-screen half of the briefcase marker is the kit's shared edge marker (lane 5, not landed); this lane's marker shows only while the case is on screen.
- A player who joins while a result is up is shown the result card (quietly, no win sound).
- Contrast, measured from capture pixels with the HUD rendered without post-processing (as the overlay canvas is in game): every line passes 4.5:1 on the 0.9 Paper plate (Navy 14–15:1, Ink2 captions and "Floor 1, your tower" 7.2:1, shut ladder floors in Ink3 5.5:1). The pale look in earlier captures was the capture method (HUD through the post-processed camera), not the tokens.
- The cap warning was amber text on white (1.7:1); it is now an amber plate with Navy text (8.2:1), with "QUARTER CLOSING" in words. Seen on host and client with a 75 s runtime profile copy (the asset is untouched).
- Seen at 21:9 (2560×1080) and with reduced motion (the banner placed without its slide).
- The carried briefcase sits small (0.55) in the lower left; the gun keeps the lower right (lane 2, AgentHub #492).
- Still not seen: a person judging motion or sound.
- At 720p the kit's smallest captions (13 px at 1080p) render about 9 px: legibility is borderline; that is the kit's type scale (lane 5).

### Proposal: who owns Player.prefab (review ED-5, ED-7)
- Nobody regenerates it. Every lane tool only adds to it: a missing component or child, an empty value filled; anything already set (by a person or another tool) is reported, never overwritten (AGENTS.md §4).
- The greybox **Rebuild Player Prefab** keeps a list of lane components it must carry over (`FirmMember`, `CarryAnchor`, `PlayerOutfit` and its view, health, weapon holder and view, hands, the STDMC model) and re-adds them, or refuses while they are present. Not changed here: Alex's sessions own the generator.
- Set Up Integration already follows this (2026-11-14). Its old rewrite came from the lobby's `PatchPlayerPrefab`, which re-seats an existing outfit: it deleted the `Outfit` figure and re-pointed `PlayerOutfit._view` at the STDMC model. The dead-zone, blast and mask lines in that diff were only Unity dropping YAML for fields the code no longer has. Now the integration tool skips the lobby patch when `PlayerOutfit` exists and warns if any lane step changes the prefab. Proven on buffer's prefab: byte-identical after a full run.

### Assumptions made while Adam was away (2026-11-14)
- The briefcase seats on the left, so it never overlaps the gun; no shared held-object socket exists yet. If Harvey's `PlayerViewmodel.GunMount` becomes one, move onto it.
- `Lane_Integration.unity` is committed from this lane for the spawn fix (asked by the orchestrator). It was rebuilt with Set Up Integration, then Add GameMode To Open Scene. The gaps lane's objects are left out on purpose: they install themselves at run time once that lane's installer is merged (AgentHub #504).
- `MatchHud` and `MatchMenuPanel` check their slots on wake. On a scene whose match UI predates the code they log one error naming the empty slots and the menu to run, then switch off; the player still spawns (seen on buffer's stale scene). The roster events reach each listener separately, so one faulty listener can't abort `FirmMember.OnStartClient`.
- Set Up Integration rewrites `Player.prefab` through the greybox generator and drops other lanes' fields (input dead zones, blast profile, masks, the viewmodel). That rewrite was discarded and is not part of this commit; the builder needs fixing (integration owner).

## Gates or locked doors (your call)

Both now hold a team on a floor: the GDD's breakable locked door on each flight, and the floor gate that only the briefcase opens.

| Option | What it plays like | What it costs |
|---|---|---|
| **Gates only** (doors removed from the templates) | The climb is paced by the briefcase alone: the carrier leads, everyone follows a floor at a time. Fights are about the case, not about a door. | One template-tool change. The GDD's pacing table assumed doors; a team carrying the case could sprint a floor in seconds, so floors would hold a team less long. |
| **Doors only** (gates off: remove `MatchGates` from the Match object) | The GDD as written: break the door to climb, the briefcase only matters at the roof. | Your rule "only up to where the briefcase has been" goes. A team can leave the case behind and race to the roof, then has to fetch it. |
| **Both** (now) | The carrier must also break each door; the floor above stays shut until they step on it. | The slowest climb: door time plus escort. With 4.5 m floors and 9 offices it may run past the 12-minute target. Untested in play. |

Suggestion: gates only, if the briefcase is the game; doors only if the race is. Shots pass through gates now (an Inspector switch on `MatchGates`), so cross-gap fire works either way.

## Risks and known issues

- **Gates and the GDD's breakable locked doors overlap:** see the options table above.
- **If the stair-cage glass becomes breakable** (shooting lane), players could leave the stairs above the open floor through a broken pane.

- **Key clash with the pickups lane:** both use G (and d-pad down) to drop, so one press would drop an item and the briefcase together. Their Interact is E. Settle it in the shared actions asset at merge.
- **Player.prefab:** Alex's animation work rebuilds it with the STDMC model under `Graphics/Lean/Body`. Set Up GameMode adds `CarryAnchor` under `Graphics/Lean` and `FirmMember` on the root, so it survives that hierarchy, but re-run it after any Player.prefab rebuild.
- **The bridge sits in the middle window bay,** which is clear at the default 28 m width. Another `TowerProfile` width can put a pillar in it.
- **A briefcase wedged unreachably inside a building has no recovery.**
- **The gap counts as a fall** except the plaza and bridges, so a future zipline crossing over 1.2 s will send its rider back.
- **Blasts don't move the briefcase yet:** wire `Briefcase.Throw` into `Knockback.Explode`.
- **The setup tool assigns FishNet scene ids through a FishNet internal**, `NetworkObject.CreateSceneId`, by reflection. A FishNet update could break it; the tool then logs what to do by hand.
- Setup on an edited `Player.prefab` logs "Left Player.prefab alone: it has been edited". That is the guard's check, not a failure.
- **The one thing that still makes it feel like a demo:** the win is a silent text card. No roof moment, no secretary line, no sound, and nothing carries into the next match.

## Since the buffer merge

This branch now contains `buffer` (merge "Merge the integration scene and the four lane joins into buffer"). The integration session changed this lane's code: a `HandsFull` veto in `MatchDirector` (the briefcase and a carried item share the hands), Inspector-set button labels in `MatchMenuPanel`, `FirmMember` reading the shared `PlayerInputReader.Captured` gate, internal helpers in `GameModeLaneSetup`; the lobby desk now calls `HostStartRun`, and 8 s after a result everyone returns to reception (`docs/systems/INTEGRATION.md`). Compile check after the merge: 0 errors. Not re-run in Play Mode on this branch.

## Merge steps

1. Merge this branch. Expect conflicts in:
   - `Player.prefab` and `Assets/DefaultPrefabObjects.asset`: take the other side.
   - `docs/systems/NETCODE.md` owner table and `docs/DECISIONS.md` top line: keep both lines.
2. Open the merged project and run **Seal the Deal > Lanes > Set Up GameMode**. It re-adds `FirmMember` and `CarryAnchor` to `Player.prefab` and rebuilds the lane scene if it is missing; FishNet re-adds the briefcase to its prefab list.
3. To play the mode in another scene with the tower and game UI, open it and run **Seal the Deal > Lanes > Add GameMode To Open Scene** (adds the match, plaza and skybridges, HUD and menu section).
4. **Lobby lane:** call `MatchDirector.Instance.HostStartRun(firmByClientId)` from `RunStartRequested`: a client id → firm (0 = Building A, 1 = Building B) map. It moves those players and their `TeamSpawner` seats, then starts the run.
5. Fold the lane-local inputs into the shared actions asset (movement lane):
   - Interact: F / pad North.
   - Drop: G / d-pad down.
   - Then point `FirmMember` at the shared actions and settle the clash with the pickups lane.

## How to test it by hand

Open `Scenes/Lanes/Lane_GameMode.unity`, press Play, then:

1. **Host game**, then **Esc** and **Start the match**. You should see a 5-4-3-2-1 countdown.
2. Climb over the reception window sill onto the plaza (jump at it and hold forward). Press **F** at the brown case. It turns cyan in your hand, and the top-right row says "You have it".
3. Climb to Checkpoint 1. The bottom line says your firm comes back there.
4. Go to the window and climb onto the skybridge; cross into the rival tower.
5. Press **G** to drop the case, go up to a roof without it: "The roof only counts with the briefcase."
6. Fetch it and carry it onto either roof: "Ashcombe & Pryce sealed the deal." and **Rematch**.
7. Gate: in a new match, climb the stairs without the case. At the top a frosted panel stops you and the bottom line says "Floor 1 is shut. Whoever carries the briefcase up there opens it for everyone." Carry the case up: the panel vanishes in both towers and the briefcase row says "open up to Floor 1".

## Next steps

Adam plays the lane scene and confirms the rules above (the gate reading first), then a two-PC Steam session.
