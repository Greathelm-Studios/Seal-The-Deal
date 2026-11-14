# Workboard

Who is working on what right now. Rules: [AGENTS.md](../AGENTS.md) §12. A line counts once it is pushed.

## Release risks

Anything that threatens 7 Oct or 29 Oct, scope creep, or a change to netcode authority. Adam reads this. Date each line and say who raised it; Adam removes it once handled.

- 2026-10-03, Adam's Claude session: **v1 now supports up to 8 players (1–4 per team), not 2v2.** Netcode and props sync were budgeted for 4; 8 doubles player traffic and the bodies throwing furniture. Needs a 4v4 test over Steam well before 29 Oct. Locked doors and central spiral stairs are new floor-template work too.
- 2026-11-13, Harvey's Claude session: **prop sync on main is new and has never been seen from a client.** Props now move through Switchboard instead of FishNet's NetworkTransform ([systems/SWITCHBOARD.md](systems/SWITCHBOARD.md)). It compiles; nobody has played it. Needs one editor-plus-build session with furniture being blasted before anything is built on it. Host authority is unchanged. To go back, revert the commit "Move prop sync to Switchboard".

- 2026-11-14, Harvey's Claude session: **main cannot take harvey-netcode or Alex_Env until two asset questions are settled.** Both branches carry Alex's commits "stuff" and "Give the character a sheet-matched idle, real side runs, breathing and a head-first look", which main does not have. They include the Kenney Blaster Kit and the MrEliptik Office Pack under `Assets/ThirdParty/` (is each cleared to ship, and credited?) and five Kevin Iglesias run clips that [systems/CHARACTER_ANIMATION.md](systems/CHARACTER_ANIMATION.md) says are not cleared for a public repository. They also carry render caches, compiled Python files and long tool logs under `ArtSource/Kit` that look accidental. Harvey held the merge. Needs a yes or no from Adam on the packs and clips, and a tidy of the stray files, before either branch is merged.

## In progress

| Who (human / tool) | Task | Branch | Claimed scenes and prefabs | Since |
|---|---|---|---|---|
| Alex / Codex | Animated STDMC eye cards | Alex_Env | STDMC_Toon.prefab | 2026-11-12 |
| Alex / Claude Code | First-person viewmodel (arms + pistol), slice 1 of weapons; firing and pickups wait on Adam's lanes 2 and 3 | Alex_Env | Player.prefab (via the greybox tool), ViewmodelArms.prefab | 2026-11-13 |
