<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex: "i also wanna add a stylised reflection or sheen to the windows/glass"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the kit's glass (AGENTS.md §7): what the player sees, where its
              files are, how to tune it, and what has not been checked.
-->
# Glass

**Status:** Current (the look: on the kit's one glass material, not judged in play; breaking: see [HANDOVER_shooting.md](../HANDOVER_shooting.md) for what was seen running) · **Last updated:** 2026-11-12

Glass is almost clear, with a pair of hard diagonal light streaks, one wide and one thin, drawn across each pane.
The streaks slide over the pane as the viewer walks past, the way a reflection would, and the glass goes pale and more solid when seen nearly edge-on.
Nothing is actually reflected: the art bible rules out mirror glass because players have to read each other through two panes.

**Glass breaks.** A bullet passes straight through a pane and shatters it on the way; a blast shatters every pane in its radius; a chair, a desk or a thrown player that hits a pane fast enough goes through it and keeps most of its speed.
A broken pane stays broken for the rest of the match and blocks nothing: people, bullets and furniture all pass. A rematch (new towers) starts with every pane whole, on every machine.
The building shell (slabs, sills, pillars, walls) never breaks.

## Breaking: who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Which panes are broken | **Host.** Only the host decides a pane breaks | A set of pane numbers on the scene's `GlassRegistry` (FishNet `SyncHashSet<uint>`): about 5 B per break; the whole set, 4 B per broken pane, to a player who joins later | Only when a pane breaks |
| The panes themselves | Every machine, locally: scene objects, or built with the towers from the seed | Nothing | — |
| Shards | Every machine makes its own | Nothing, never networked | — |

**Pane numbers** come out the same on every machine without being sent: panes placed in the scene are numbered in scene order; panes in the towers by building, floor and their order on the floor, after each machine's own build from the seed.
One bit of each synced entry says which side the shards fly to, so a window shot outward sprays outward on every screen.
A player who joins late receives the set and sees the same holes, with no shards.
On a client, a pane disappears when the host's word arrives, so for a few tens of ms its own body can still bump a pane the host has already broken (the same trade as the locked doors, TOWER.md).

## What counts as glass

- Anything with a **`GlassPane`** component. Put it on the object that has the pane's collider and renderer; or fill its two lists when they sit on other objects.
- Anything drawn with a material listed under **Glass Materials** on the glass profile. The registry gives these a `GlassPane` when the match starts, and a fitted box collider when they have none. This is how the greybox stair cages, glass-room walls and the kit's windows break without the floor templates being touched.

Glass under a networked object (a spawned prop) is not supported: its number would differ between machines.
A scene needs one `GlassRegistry` (**Seal the Deal > Lanes > Add Shooting To Open Scene** adds it); without one nothing breaks and bullets stop at glass.

## Breaking: tuning

One asset, [DefaultGlass.asset](../../Assets/_Project/Profiles/Glass/DefaultGlass.asset):

| Inspector value | What it does |
|---|---|
| Glass Materials | What counts as glass by material alone. |
| Break Speed | How fast something must hit a pane head-on to go through, m/s. 10 by default: a sprint (9) never does it, a blast throw or a killing shot does. |
| Min Break Mass | Lightest thing that can break a pane by impact, kg. |
| Speed Kept | Fraction of its speed a body keeps going through. |
| Through Control Loss Seconds | How long a player who went through can't steer. |
| Shard Effect, Shards Per Square Metre, Max Shards, Shard Speed | The local shard burst. |

## Breaking: for other systems

- Glass implements `CloseTheDeal.Core.IDamageable`: any damage on the host breaks the pane.
- `GlassRegistry.BreakInRadius(point, radius)` on the host is what `Knockback.Explode` calls.
- `GlassPane.AnyBroken(pane, direction)` fires on every machine when a pane shatters (not for a late joiner's already-broken panes): the hook for the glass sound.

## Files

- Shader: [GlassSheen.shader](../../Assets/_Project/Art/Glass/GlassSheen.shader), `Close the Deal/Glass Sheen`. Unlit and transparent; it casts no shadow, so sunlight passes through onto the floor.
- Material: [Glass_Clear.mat](../../Assets/_Project/Art/Kit/Materials/Glass_Clear.mat), used by every window, glass partition and stair-cage panel in the kit.
- [KitImport.cs](../../Assets/_Project/Editor/Art/KitImport.cs) gives the glass material this shader on import. It sets only the tint, so streak and edge settings changed in the Inspector survive a re-import.
- Breaking: [Assets/_Project/Scripts/Glass/](../../Assets/_Project/Scripts/Glass/). `GlassPane.cs` (one pane), `GlassRegistry.cs` (the host's broken set and the numbering), `GlassProfile.cs` (tuning), `GlassShards.cs` (the pooled local shard burst). The shard prefab and its material are in [Assets/_Project/Prefabs/Glass/](../../Assets/_Project/Prefabs/Glass/).

## Tuning

| Inspector value | What it does |
|---|---|
| Glass tint | Colour, and in its alpha how solid the pane is. Keep the alpha near 0.05. |
| Streak colour | Colour, and in its alpha how strong the streaks are. |
| Streak tilt | Degrees from vertical. |
| Distance between streak pairs | Metres along the glass. Larger = fewer streaks. |
| Width of the wide / thin streak | Metres. Set the thin one to 0 for a single streak. |
| How far the streaks slide | 0 paints them onto the glass; higher makes them drift as the viewer moves. |
| Colour at grazing angles, and how close to edge-on | The pale edge. |

## Known limitations

- **Greybox window panes** come from the floor-template tool (TOWER.md): one per bay, except the doorway bay of the lobby and checkpoint floors.
- **Kit glass gets its collider at run time.** The kit's window and partition glass collides only at its frame, so the registry gives each such pane a thin box fitted to what is drawn (`Solidify Glass Without Collider` on the glass profile, on by default): intact glass now stops people and things, as well as breaking. Nothing in the kit or the floor templates is edited. The box is right for panes turned in quarter turns; a pane at another angle gets one a little too big. The lasting fix belongs in the kit import (a glass collider per glass child), which would make this setting unnecessary.
- **A whole pane goes at once.** No cracks, no partial holes; a 4 m partition is one pane.
- **Shards are flat squares that fall through the floor** and make no sound.
- A body only goes through if it hits the glass itself at speed: one standing closer than about 1.3 m to a window is thrown into the 0.6 m sill first (WEAPONS.md).
- A pane with a collider that isn't a box on the pane's own object measures itself from world-aligned bounds, so its shards can spread wrong if it is turned.

- **A streak can cross a player.** From across the street a streak sometimes sits over someone standing behind the glass. They stay readable through it at the default strength, but it is a visibility cost; streak strength and spacing are the controls.
- **It assumes panes are thin closed boxes**, as the kit's are, and draws only the face turned toward the viewer. A pane modelled as a single one-sided sheet would be invisible from behind.
- **Not checked:** Play Mode, how the sliding looks in motion, and the stair cage's curved panels up close. Only still renders were looked at.
