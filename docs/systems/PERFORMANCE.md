# Performance

**Status:** Current (mitigations implemented in code and settings; **not yet measured with the Unity Profiler in a multi-player build**) · **Last updated:** 2026-11-22

## What the player sees

A steady frame rate locked to the monitor (vSync on) on the PC quality level. Initial frame-drop mitigations were made from inspecting the project on 2026-10-03, followed by runtime pooling, distance culling, Switchboard prop streaming, and FishySteamworks allocation fixes. Nothing here has been measured in a Profiler yet, so every cost note below comes from reading code and settings rather than Profiler captures (AGENTS.md §8).

## Implemented mitigations

| Area | Change | Where | Why |
|---|---|---|---|
| Frame pacing | vSync on (was off with no frame cap) | `ProjectSettings/QualitySettings.asset` (PC level) | Prevents uneven frame intervals against the 60 Hz fixed tick (which ran 0 or 2 physics steps per frame and read as stutter). |
| Floor draw calls | Measured, not yet reduced: about 2,700 a generated floor | [FLOOR_GEN.md](FLOOR_GEN.md) | The tower mode's runtime static batching went with it on 2026-11-22. Generated floors could be merged the same way once the kit models are readable. |
| Shadow cost | Cascades 4 to 2; 4096 atlas, near cascade 15%, 30 m distance | `Assets/Settings/PC_RPAsset.asset` | Halves shadow caster redraws while keeping 2048 px per tile and 4.5 m near-cascade resolution (+~24 MB shadow atlas memory). |
| Full-screen GPU cost | MSAA 4x to 2x; Opaque Texture off; soft shadows High to Medium; additional-light shadows off; SSAO half-res, Medium samples, Low blur | `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/PC_Renderer.asset` | Fill-rate cuts made from reading settings on 2026-11-22, not profiled. No shader or feature reads the opaque copy (UI glass blur takes its own). Only directional lights cast shadows, so the 2048 extra-light atlas was unused; re-enable `Additional Lights > Cast Shadows` before giving a lamp shadows. The 4096 main shadow map is kept (row above). |
| Prop bandwidth & sync | Switchboard replaced per-prop `NetworkTransform` | `Assets/_Project/Scripts/Net/Switchboard/` | 13 B/prop, per-client byte cap (default 12 KB/s), floor-band priority, sleep fast-path, single per-tick packing cache, client `Update()` sleep when idle. |
| Transport hot paths | Pinned outbound buffer, direct pointer reads, `KeyCollection` iteration, `ByteArrayPool` return, lazy receive buffers | `Assets/_Project/Plugins/FishySteamworks/` (`FORK.md`) | Eliminates per-packet `GCHandle.Alloc` / `Array.Resize`, `Marshal.PtrToStructure` boxing, 60 Hz enumerator boxing, local packet pool leaks, and 1 MB unused buffer on `ClientHostSocket`. |
| Combat & VFX pooling | Pooled tracers, particles, shards, splats, audio voices, cached `PlayerMotor.All` / `Health.All` | `TracerPool`, `LocalEffects`, `GlassShards`, `BloodSplatSystem`, `BloodDropBurst`, `CombatSounds`, `VoiceChat` | Zero steady-state allocations during bursts; 60 m camera culling on tracers, impacts, blood, and combat audio; sub-canvas isolation on `CombatHud` hit marker; `Health` restores `SetPropertyBlock(null)` after hit flashes for SRP Batcher eligibility. |

## Still unmeasured / suspected

1. **Client reconcile physics replays:** On a client, each reconcile rewinds and re-runs `Physics.Simulate` across every tick since the host snapshot. At 100 ms+ ping this runs multiple full physics steps per tick across all colliders and props. Needs Profiler measurement (`Physics.Simulate` calls/frame) in a two-machine build.
2. **Full-screen renderer cost:** the 2026-11-22 MSAA/SSAO/soft-shadow cuts have not been A/B profiled. Other suspects from the same read: the `Office Sky` shader (~50 noise octaves per visible sky pixel), overlapping `BloodSplat` decals (per-pixel loops, transparent), five 4096² marking textures in `Art/Signs/` (~100 MB VRAM), and 63 realtime unbaked spot lights in `KitOffice.unity` (not in the build).
3. **Far-floor prop sleeping/freezing:** Verify under multi-player blasts that sleeping/frozen props do not wake unnecessary `Physics.Simulate` islands or Switchboard bursts.
4. **Editor vs. build:** Scene view and Inspector repaints distort Editor frame times; judge performance only in a Development Build.

## How to measure

Make a Development Build with Autoconnect Profiler, run host plus one or more clients, and stand in the tower during a fight. Record main-thread CPU time, GPU frame time, draw call count (`Batches`), `Physics.Simulate` count/time, and GC allocations per frame here before making further cuts.

## Known limitations

- Merging still tower parts runs once per tower build; anything movable must stay out of a template's Never Moves list.
- Profiler numbers have not yet been captured for any scene or player count.
