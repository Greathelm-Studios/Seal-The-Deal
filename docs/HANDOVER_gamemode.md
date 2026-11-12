# Handover: briefcase game mode (game mode 1)

**Status:** Target · **Last updated:** 2026-11-12 · **From:** Adam / Claude Code (game mode lane, branch `claude/lane-gamemode-29a356`)

## Goal

Two rival law firms race for one shared briefcase; the firm whose player carries it onto either roof wins. At 15:00 the firm holding it (or that carried it last) wins. The system is in [systems/MATCH.md](systems/MATCH.md).

```
Match (host-owned, synced on change)

 Waiting ──Start (host menu) or HostStartRun (lobby)──▶ Countdown 5 s ──▶ Running ──carrier on either roof──▶ Ended
   ▲                                                                       │      ──clock 15:00───────────▶ (winner, reason)
   └──────────────── Rematch (host): new seed, everyone to reception, case to the plaza ◀───────────────────────┘

The one briefcase (never despawned)

 Dropped ──F, anyone in reach──▶ Carried ──G / knocked / fell out / left game──▶ Dropped
    └── outside buildings, plaza and bridges 2 s ──▶ Returning ──3 s──▶ Dropped on the highest checkpoint's skybridge (or the plaza)
```

## Where it stands

**Ready for review**, not approved. Three commits on this branch:

- "Add the briefcase game mode: two firms, a match clock and a roof win" and "Correct the game mode handover's verification notes and doc dates": the first per-firm version, now superseded.
- "Rework the game mode around one shared briefcase, a plaza and skybridges": the current rules.

Not built yet: the floor gate (nobody climbs above the highest floor the briefcase has been carried onto). It is the next slice.

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
- **Not seen:**
  - Climbing the real stairs and mantling onto a bridge by key press: the climb was teleported, and the crossing was driven by knockback shoves.
  - Steal by a real client key press (the client's grab was its own F request; the host knock was scripted).
  - Steal while the carrier is on a bridge.
  - Steam, and real latency.
  - Pad bindings.
  - A run in a build.

## Rules for Adam to confirm (most important first)

1. **Interact is F, not E.** You said E, but E is lean-right in the shared controls and the shared Interact is already F. It is one constant in `FirmMember` to switch.
2. **One shared briefcase; anyone can take it; carrying it onto either roof wins for the carrier's firm.** Your words; the "either roof" part is the orchestrator's default.
3. **It spawns on a plaza between the two towers**, level with both receptions and the same distance from each. Assumption.
4. **Skybridges on every checkpoint floor**, built only in the lane scene (and any scene where you run Add GameMode To Open Scene). The tower geometry is unchanged. **Crossing is a design decision**: the zipline gun is not built. Assumption.
5. **A case that leaves the buildings comes back** after 2 s + 3 s to the skybridge of the highest checkpoint floor it has been on, or to the plaza. Assumption.
6. **A firm's respawn point moves when any of its players stands on a checkpoint floor of their own tower.** The briefcase no longer saves anything per firm. Assumption.
7. **At 15:00:** the firm holding the case wins; if it lies loose, the firm that carried it last; if nobody ever picked it up, no winner. Proposal.
8. **Firm names, proposed:** Ashcombe & Pryce (Building A), Halloran Voss (Building B).
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
- The game's name is now "Seal the Deal"; the greybox menu still says "CLOSE THE DEAL" (`GreyboxUiSetup.cs`, not this lane's).

## Risks and known issues

- **Key clash with the pickups lane:** both use G (and d-pad down) to drop, so one press would drop an item and the briefcase together. Their Interact is E. Settle it in the shared actions asset at merge.
- **Player.prefab:** Alex's animation work rebuilds it with the STDMC model under `Graphics/Lean/Body`. Set Up GameMode adds `CarryAnchor` under `Graphics/Lean` and `FirmMember` on the root, so it survives that hierarchy, but re-run it after any Player.prefab rebuild.
- **The bridge sits in the middle window bay,** which is clear at the default 28 m width. Another `TowerProfile` width can put a pillar in it.
- **A briefcase wedged unreachably inside a building has no recovery.**
- **The gap counts as a fall** except the plaza and bridges, so a future zipline crossing over 1.2 s will send its rider back.
- **Blasts don't move the briefcase yet:** wire `Briefcase.Throw` into `Knockback.Explode`.
- **The setup tool assigns FishNet scene ids through a FishNet internal**, `NetworkObject.CreateSceneId`, by reflection. A FishNet update could break it; the tool then logs what to do by hand.
- Setup on an edited `Player.prefab` logs "Left Player.prefab alone: it has been edited". That is the guard's check, not a failure.
- **The one thing that still makes it feel like a demo:** the win is a silent text card. No roof moment, no secretary line, no sound, and nothing carries into the next match.

## Merge steps

1. Merge this branch. Expect conflicts in:
   - `Player.prefab` and `Assets/DefaultPrefabObjects.asset`: take the other side.
   - `docs/systems/NETCODE.md` owner table and `docs/DECISIONS.md` top line: keep both lines.
2. Open the merged project and run **Close the Deal > Lanes > Set Up GameMode**. It re-adds `FirmMember` and `CarryAnchor` to `Player.prefab` and rebuilds the lane scene if it is missing; FishNet re-adds the briefcase to its prefab list.
3. To play the mode in another scene with the tower and game UI, open it and run **Close the Deal > Lanes > Add GameMode To Open Scene** (adds the match, plaza and skybridges, HUD and menu section).
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

## Next steps

Build the floor gate (slice B), then Adam plays the lane scene and confirms the rules above, then a two-PC Steam session.
