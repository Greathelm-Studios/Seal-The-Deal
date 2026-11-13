# SWITCHBOARD

**Status:** Current · **Last updated:** 2026-11-13 · **Verified against:** a headless compile only. **Not yet played, on one machine or two.**

Switchboard is the game's own layer on top of FishNet for moving physics objects between players. It does not change who decides anything: the host still simulates every prop (AGENTS.md §3). It changes how the host tells everyone else.

## What happens in the game

Furniture thrown by a blast looks the same on every screen, and the host's upload stays inside a fixed limit however much is flying.
The furniture you can actually see (your own floor, the floors next to it, and the rival floors facing you through the windows) updates 30 times a second.
Furniture far above or below you still moves, but updates a few times a second, so a chair tumbling ten floors up costs almost nothing.
When too much moves at once, nothing freezes: the nearest furniture keeps its rate and the far furniture waits its turn.
A late joiner, or a player who lost packets, has every prop quietly put right within about a minute without anyone asking.

There is nothing to switch on. Press **Host game** in the menu (see [NETCODE.md](NETCODE.md), "Testing it") and the furniture is already on Switchboard: the first prop to appear starts the stream.

## Why it exists

Props are the GDD's biggest technical risk, and the old route (one FishNet NetworkTransform per prop) had no ceiling: each moving prop cost about 40 bytes, 30 times a second, to every player, wherever that player was.
A boardroom blast with 8 players could ask the host for more upload than a home connection has.
Switchboard gives the host one number it never exceeds per player, and spends it where players are looking.

| | NetworkTransform per prop | Switchboard |
|---|---|---|
| Bytes per prop update | about 40 | 13 |
| Worst case to one player | grows with every moving prop | fixed: `Bytes Per Second Per Player` (default 12 000) |
| Worst case host upload, 7 clients | unbounded | 84 KB/s (about 0.7 Mbit/s) |
| Far floors | full rate | a tenth of the rate |

These numbers are the design's arithmetic, not measurements.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Every streamed body's motion | **Host.** Only the host simulates | Position and rotation, host to each client, in one unreliable packet per client | Up to 30 packets/s per client, capped in bytes |
| Which bodies go in a packet | **Host**, per client, from that client's floor | — | Every send |
| Resting pose | **Host** | The final pose, repeated 3 times, then silence | When a body stops |
| Healing | **Host** | A few still bodies per second, in rotation | 8 bodies/s per client by default |
| Smoothing | Each client, for display only | Nothing | Every frame while a body has unplayed poses |

Clients never send anything to Switchboard. A client's copy of a body is kinematic and only follows.

## How it works

### Floor bands

The two towers are a grid: tower A or B, floor 0 to the roof. Every body and every player is in one cell.
For each client the host puts each body in a band:

- **Near**: same tower and within `Near Floors Same Tower` (default 1), or the rival tower and within `Near Floors Rival Tower` (default 2, because you see up and down across the street).
- **Far**: everything else.

A body's cell comes from its position in the Tower object's local space: the sign of local Z picks the tower, local Y divided by the floor height picks the floor. Something that falls into the street belongs to the nearer tower's ground floor.

### Priority and the budget

Each client has a ledger with one running score per body.
On every send, each body that is moving (or still owes its resting pose) gains its band's weight: 1 for Near, `Far Weight` (default 0.1) for Far.
A body is due once its score reaches 1. Due bodies go into the packet highest score first until the client's byte allowance for this send is used, and a sent body's score returns to 0.
So with room to spare a Near body goes every send and a Far body every tenth send; with no room to spare, bodies wait in proportion and none is starved.

The allowance is a token bucket: each send adds `Bytes Per Second Per Player / Sends Per Second` bytes, the bucket holds at most two sends' worth, and one packet never exceeds 1100 bytes (under Steam's unreliable message limit).
Whatever is left after the due bodies is spent on healing: still bodies, taken in rotation, at most `Heal Bodies Per Second`.

### The packet

One FishNet broadcast per client per send, on the unreliable channel:

- 4 bytes: the host tick the poses were captured at.
- 1 byte: how many bodies follow.
- 13 bytes per body: FishNet object id (2), flags (1; bit 0 = at rest), pose (10).

A pose is the position in Tower-local space as three 16-bit numbers across a fixed box around both towers (16 m of margin on every side, about 2 mm steps), and the rotation as a 32-bit smallest-three quaternion (about 0.1°).
A body thrown outside the box is shown at the edge of the box until it comes back.

### On the client

Each body keeps its last few poses and plays them back slightly in the past so motion is smooth despite uneven arrival.
The delay adapts: two send intervals plus twice the measured arrival jitter, kept between `Smoothing Delay Floor` and `Ceiling` (50 and 250 ms).
A body whose newest pose arrived after a long gap (a Far body) holds, then covers the distance over one delay's worth of time, so it glides rather than jumps.
A body with nothing left to play switches its per-frame work off.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored:** everything on `Assets/_Project/Profiles/DefaultSwitchboard.asset` (the budget, rates, band sizes, smoothing bounds).
**Derived:** each body's cell and band, the ledgers, the client's delay. Tower dimensions are read from `TowerProfile` and never written.

## Scripts

Under [Assets/_Project/Scripts/Net/Switchboard/](../../Assets/_Project/Scripts/Net/Switchboard/):

- `SwitchboardProfile.cs`: the tuning asset.
- `FloorBands.cs`: `TowerFrame` (world position to tower cell, and the quantisation box) and the band rule. Pure.
- `PoseCodec.cs`: pose to and from 10 bytes. Pure.
- `PriorityLedger.cs`: the per-client scores and the "highest due first" pick. Pure, no allocation after construction.
- `NetBody.cs`: on every streamed object, in place of NetworkTransform. The host reads its pose; clients play poses back. It holds the profile and starts the stream if the scene has none.
- `PropStream.cs`: one per scene, created at run time on its own `Switchboard` object by the first body. Host: capture, bands, ledgers, budget, send. Client: receive and hand poses to bodies.
- `SwitchboardStats.cs`: live counters for the debug readout (a third line bottom-left: prop traffic in kB/s, updates a second, and how many props are moving or the smoothing delay).

## Setting it up

The greybox prop tool ([GreyboxPropSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxPropSetup.cs)) builds every prop prefab with a `NetBody` pointing at `DefaultSwitchboard.asset`, and creates that asset if it is missing. No scene needs anything added.

A prop prefab made by hand needs two things: a `NetBody` with its **Profile** set to `Assets/_Project/Profiles/DefaultSwitchboard.asset`, and no FishNet NetworkTransform. A prop with both logs a warning and is left out of the stream; a `NetBody` with no profile logs a warning and does not move on other players' screens.

## Known limitations

- **Not seen running.** Nothing here has been played, on one machine or two. Until it has been watched from a client it is a release risk (see the workboard).
- Anything that carries or moves a prop must do it on the host. Clients' props are followers and are overwritten by the next pose.
- Needs a built tower. In a scene without a `TowerBuilder` the stream stays idle and props do not move on clients.
- The budget is fixed per player. It does not yet shrink for a player whose connection is struggling (see Target, below).
- The host's own props are not smoothed; they are the simulation.

## Target, not built

- **Line quality feedback.** Read each client's Steam connection quality and shrink that client's budget when it drops. Needs the connection handle exposed from the FishySteamworks fork (a new entry in its `FORK.md`).
- **Host seat.** Before a run starts, each lobby member publishes its Steam relay ping location as lobby member data; the lobby works out which member would give everyone the lowest worst-case ping and offers them the host seat. Lobby only: there is still no host migration during a run (GDD). Touches the lobby flow, so it waits for the lobby work to land.
