# Decisions

**Status:** Current · **Last updated:** 2026-11-13

Technical and process decisions, newest first. One line each: date, what, who, link.
Design decisions live in [GDD.md](GDD.md), not here. Rules: [AGENTS.md](../AGENTS.md) §12.

- 2026-11-13 — The ragdoll chases animation clips (CC0 Quaternius library, retargeted in Blender, imported as Generic) instead of the hand-written procedural pose; this reverses the no-clips choice below. Still local to each client, nothing synced. — Alex. [systems/CHARACTER_ANIMATION.md](systems/CHARACTER_ANIMATION.md).
- 2026-11-13 — FishNet stays as the plumbing (connections, spawning, prediction); we do not write our own netcode stack. Where the game is unusual we add our own layer on top, one system at a time. A full replacement would need evidence from a real session and Adam's agreement. — Harvey, on his agent's recommendation. [systems/NETCODE.md](systems/NETCODE.md).
- 2026-11-12 — The player's body is an active ragdoll (rigidbodies and motorised joints chasing the procedural pose), run in a physics scene of its own so FishNet's re-simulation and the predicted capsule cannot touch it; local to each client, nothing synced. — Alex. [systems/CHARACTER_ANIMATION.md](systems/CHARACTER_ANIMATION.md).
- 2026-11-12 — Players are animated procedurally with no clips and no new package: a script poses the humanoid skeleton from the body's movement, local to each client, nothing synced. — Alex. [systems/CHARACTER_ANIMATION.md](systems/CHARACTER_ANIMATION.md).
- 2026-11-12 — Prop motion moves from one FishNet NetworkTransform per prop to Switchboard, a per-player byte-capped stream that favours nearby floors. Host authority is unchanged. The greybox prop prefabs use it from 2026-11-13; compiled, not yet seen from a client. — Harvey's agent (technical call, see owner-defers note). [systems/SWITCHBOARD.md](systems/SWITCHBOARD.md).
- 2026-10-03 — PC quality runs with vSync on and two shadow cascades; runtime-built towers are static-batched. — Agent (technical call, see owner-defers note). [systems/PERFORMANCE.md](systems/PERFORMANCE.md).
- 2026-10-03 — Every tool reads `AGENTS.md` as its single entry point; shared notes, decisions and the workboard live in plain markdown under `docs/`. Collaborators' agents report to their own human; release risks go to Adam. — Adam. [AGENTS.md](../AGENTS.md) §1, §12.
- 2026-09-28 — Netcode is FishNet + FishySteamworks, host-authoritative P2P over Steam. — Agent, on Adam's lead. [systems/NETCODE.md](systems/NETCODE.md).
