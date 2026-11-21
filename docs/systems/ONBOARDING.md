# ONBOARDING

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

First-run hints explain non-obvious game rules once per player: reception's purpose, that one briefcase decides the match, that carrying it opens floors, how to steal it back, what happens on death and fall-out, and that career matches are saved. Seen hint IDs persist in `ProfileStore` ([PROFILE.md](PROFILE.md)) so returning sessions never repeat them.

Hints are only marked seen when a UI listener is subscribed to `FirstRunHints.HintShown`, so scenes without a hint panel never consume hints unseen. Movement/combat key help and the first shut-gate explanation (`FirstGateRefusal`, [MATCH.md](MATCH.md)) live in their own systems.

## Ownership and netcode boundary (`AGENTS.md §3`)

Local only: nothing is sent over the network. Triggers read already-synced local state (`LobbyRoom` containment, `MatchDirector` phase and briefcase picks, `Health.AnyDied`, `FirmMember.OnNotice`, and `ProfileStore.MatchRecorded`).

## Architecture and design rationale

- **Queued delivery (`FirstRunHints`):** Simultaneous triggers queue into `_queue` (deduplicated by hint `Id` and `ProfileStore.Current.HasSeen`) and dequeue one at a time separated by `line.Seconds + OnboardingHints.GapSeconds`.
- **Hint contract:** `FirstRunHints.HintShown` (`Action<HintLine>` with `Id`, formatted `Text`, and `Seconds`).

## Files and tuning

- Scripts under [Assets/_Project/Scripts/Onboarding/](../../Assets/_Project/Scripts/Onboarding/): `OnboardingHints.cs`, `FirstRunHints.cs` (hosted on `Prefabs/Gaps/Records.prefab`, `ReceptionCheckSeconds = 0.5 s`).
- Tuning: `Assets/_Project/Profiles/Onboarding/OnboardingHints.asset` (per-hint `Id`, `When`, `Text` supporting `{interact}` and `{drop}`, `Seconds`, plus `GapSeconds`). Shipped lines are proposals for Adam.

## Known limitations

- Hint key tokens currently format keyboard labels from `FirmMember` (`F`, `G`) rather than switching dynamically for gamepad users.
- Seen hints are stored per PC profile rather than per Steam account.
