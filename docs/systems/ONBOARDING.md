# ONBOARDING

**Status:** Current (branch `claude/lane-gaps-5e5605`, not merged) · **Last updated:** 2026-11-14

## What the player sees

A new player is told the rules they can't guess, once each, the first time each comes up: what reception is for, that one briefcase decides the match, that carrying it opens floors, how to get it back from the rival, what happens when they die and when they fall out, and that their matches are kept. Each hint shows once per player and is remembered in their profile ([PROFILE.md](PROFILE.md)), so a second session doesn't repeat them.

Nothing draws hints yet: the UI kit's hint panel listens to `FirstRunHints.HintShown`. **A hint is only marked seen when something is listening**, so a build without the panel never uses them up.

Key help (move, jump, aim, fire) is the movement lane's controls help, not here. The first shut gate already explains itself (game mode's `FirstGateRefusal`), so it isn't repeated.

## Who owns what

Local only. Every trigger is read from state that is already synced: the local player in reception, the match phase, the briefcase status, the local body's death, the host's fall notice, and the profile recording a match.

## Contract

`FirstRunHints.HintShown` (static `Action<HintLine>`: `Id`, `Text` with keys filled in, `Seconds`). Hints that arrive together queue, one at a time, **Gap Seconds** apart. `ProfileStore.Current.HasSeen(id)` tells whether one was shown.

## Designer values

`Assets/_Project/Profiles/Onboarding/OnboardingHints.asset`: per hint an **Id** (what the profile remembers; renaming it shows it again once), **When**, **Text** (`{interact}`, `{drop}` become the keys) and **Seconds**; plus **Gap Seconds**. On the **Records** object, `FirstRunHints` has **Reception Check Seconds**. Every shipped hint is a proposal for Adam.

## Scripts

[Assets/_Project/Scripts/Onboarding/](../../Assets/_Project/Scripts/Onboarding/): `OnboardingHints.cs` (data), `FirstRunHints.cs` (triggers and queue).

## Known limitations

- Keys are the keyboard labels from `FirmMember` (F, G); a pad player reads keyboard keys.
- Hints are per PC profile, not per Steam account.
