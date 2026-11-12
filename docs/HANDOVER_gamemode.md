# Handover: briefcase game mode (game mode 1)

**Status:** Target · **Last updated:** 2026-11-12 · **From:** Adam / Claude Code (game mode lane, branch `claude/lane-gamemode-29a356`)

## Goal

Two rival law firms race up their towers. Each has a briefcase in reception; the first firm to carry its own briefcase onto its own roof wins. At 15:00 the higher briefcase wins. The system is in [systems/MATCH.md](systems/MATCH.md).

```
Match (host-owned, synced on change)

 Waiting ──Start (host, menu)──▶ Countdown 5 s ──▶ Running ──roof with own case──▶ Ended
   ▲                                                  │      ──clock 15:00───────▶ (winner, reason)
   │                                                  │                              │
   └───────────────── Rematch (host): new seed, everyone to reception ◀──────────────┘

Briefcase (one per firm, never despawned)

 Dropped ──F, own firm, in reach──▶ Carried ──G / knocked / fell out / left game──▶ Dropped
    │  ▲                                                                              
    │  └── back at checkpoint after 3 s ── Returning ◀── outside its building 2 s ── Dropped
    └── rival walks into it: kicked (stays Dropped)
```

## Where it stands

**Ready for review**, not approved. Committed on this branch as "Add the briefcase game mode: two firms, a match clock and a roof win".

- Built: firms synced to every client and late joiners, the match flow, the clock, the briefcase (pick up, carry, hand off, knock-loose, disconnect drop, rival kick, return to checkpoint), checkpoints, fall-out respawn, roof win, clock win with the tie rule, rematch, the HUD readout and the menu's match section.
- Test scene: `Scenes/Lanes/Lane_GameMode.unity`: towers of reception, two floors, Checkpoint 1, boardroom, roof.

## What was checked

- **Compile:** `node ~/.claude/skills/compile-check/compile-check.mjs`: PASS, 0 errors. A headless Unity open of the worktree also logged 0 `error CS`.
- **Solo Play mode, host only** (headless editor, scripted driver; clip `gamemode_solo_run.mp4` in the lane sessions' shared notes folder, showing the earlier striped case): 18/18 checks passed.
  - Countdown blocks pick-up; pick up; carry up; Checkpoint 1 saved; G drops.
  - Roof without the case: notice, no win. Roof with it: win at 00:15.
  - Rematch resets to reception with checkpoints cleared.
  - Carrier blasted out of the boardroom window: case dropped, player back on Checkpoint 1 after 1.2 s, case returning 2 s later, back on Checkpoint 1 3 s after that.
  - Clock end, higher floor wins. Clock end with both in reception: "No deal."
  - Leaving clears both briefcases and every player.
- **Host + one client, then a second client** (two editors on one Mac over loopback, not Steam):
  - The client learned its firm. Its HUD tracked the rival case live (Checkpoint 1 → Boardroom → Roof).
  - Same-floor clock tie: both saw "Ashcombe & Pryce, got there first".
  - Same-tick roofs: both saw Halloran Voss win (B reached the boardroom first).
  - Rival's F on the other firm's case: told why, case not taken; walking into it kicked it about 4 m.
  - Carrier quit mid-carry: case dropped where they stood, still exactly two cases.
  - Mid-match joiner: spawned on their firm's saved checkpoint (reception when none).
  - Host left: client's match closed and its HUD hid.
- **Complexity:** `lizard -l csharp Assets/_Project/Scripts/Match`: highest CC 9 (`MatchMenuPanel.Refresh`), nothing at 11+.
- **Bandwidth, 8 players:** match state is a few bytes per event, nothing per frame. The worst steady cost is two loose cases moving at 30/s to 7 clients, about 8 KB/s from the host, and zero once they settle.
- **Not seen:**
  - The climb was teleported. Carrying up the real spiral, through a locked door, or mantling with the case was never played.
  - Steam, and real latency.
  - Pad bindings.
  - The final briefcase look: it was rebuilt to a solid firm colour after the last capture.
  - The rival kick after it gained a line-of-sight check (the kick pass above ran before it), and the fall-out return while waiting in reception or on the results screen (added after the multiplayer run; the solo run passed with it).
  - A run at a frame target in a build.

## Open questions and risks

Rules I had to assume, for Adam to confirm (most important first):

1. **Interact is F, not E.** You said E, but E is lean-right in the shared actions asset, and the asset's own Interact is already F. It is one constant in `FirmMember` to switch.
2. **Each firm has its own briefcase.** A firm wins the moment one of its players carries its own case onto its own roof.
3. **Firm names, proposed:** Ashcombe & Pryce (Building A), Halloran Voss (Building B).
4. **15:00 tie on the same floor goes to the case that reached that floor first** (proposal).
5. **Dead heat** (both cases never left reception, or the same tick): no winner, "No deal." My own invention.
6. **Both roofs in the same tick:** the case that reached the boardroom first wins, then Building A.
7. **Rivals kick, never carry**, a loose briefcase: 0.8 m, 7 m/s, 0.6 s cooldown.
8. **Checkpoints save only when carried onto with the briefcase.** Reaching one empty-handed saves nothing.
9. **Clock win reads the floor the case is on now**, not the highest it reached, so a kick down the stairs costs you.
10. **Timings:** countdown 5 s, stray 2 s plus return 3 s, fall 1.2 s.
11. **Rematch builds new towers from a new seed.**
12. **Teams balance by head count**, and a leaver's place is reused.
13. **The host starts the match** from the menu until the secretary exists.

GDD mismatches (GDD is the authority on design; owner's 2026-11-12 words win for this mode; GDD not edited):

- GDD: "first team to reach the roof wins". Here reaching the roof needs your briefcase.
- GDD: at the cap "the team that has climbed highest wins". Here the highest briefcase wins.
- GDD: the secretary starts the run. Here the host's menu button does.
- GDD: killed on a floor respawns there after 5 s. There is no health in this branch, so only the window-exit-to-checkpoint rule is built.
- The brief says checkpoints on floors 3, 6 and 9. The tower inserts a checkpoint floor after every 3 offices, so they are stack floors 4, 8 and 12 (named Checkpoint 1–3). Same intent, different numbering.

Risks:

- **Key clash with the pickups lane:** both use G (and d-pad down) for Drop, so one press would drop an item and the briefcase together. Their Interact is E. Settle it in the shared actions asset at merge (note in the shared notes folder).
- **A briefcase wedged unreachably inside the building has no recovery.** Only leaving the building sends it back.
- **The gap counts as a fall:** a future zipline crossing over 1.2 s will send the rider back.
- **Blasts don't move the briefcase yet:** wire `Briefcase.Throw` into `Knockback.Explode`.
- **The setup tool assigns FishNet scene ids through a FishNet internal**, `NetworkObject.CreateSceneId`, by reflection. A FishNet update could break it; the tool then logs what to do by hand.
- Setup on the current `Player.prefab` logs "Left Player.prefab alone: it has been edited". That is the guard's check, not a failure; the component is still added.
- **The one thing that still makes it feel like a demo:** the win is a silent text card. No roof moment, no secretary line, no sound, and nothing carries into the next match.

## Merge steps

1. Merge this branch. Expect conflicts in:
   - `Player.prefab` and `Assets/DefaultPrefabObjects.asset`: take the other side.
   - `docs/systems/NETCODE.md` owner table and `docs/DECISIONS.md` top line: keep both lines.
2. Open the merged project and run **Close the Deal > Lanes > Set Up GameMode**. It adds `FirmMember` and `CarryAnchor` back to `Player.prefab` and rebuilds the lane scene if it is missing. FishNet then re-adds the briefcase to its prefab list.
3. To play the mode in another scene with the tower and game UI (for example `Greybox.unity`, once its owner agrees), open it and run **Close the Deal > Lanes > Add GameMode To Open Scene**.
4. Fold the lane-local inputs into the shared actions asset (movement lane):
   - Interact: F / pad North.
   - Drop: G / d-pad down.
   - Then point `FirmMember` at the shared actions and settle the clash with the pickups lane.
5. **Close the Deal > Lanes > Rebuild GameMode Briefcase** rewrites the briefcase prefab, only while nobody has edited it.

## Next steps

Adam plays `Lane_GameMode.unity` (Host game, then Start the match in Esc) and confirms or changes the rules above; then a two-PC Steam session.
