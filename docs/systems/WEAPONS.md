# WEAPONS

**Status:** Current (greybox guns, no sound or HUD yet) · **Last updated:** 2026-11-12 · **Verified against:** the shooting lane commit, see [HANDOVER_shooting.md](../HANDOVER_shooting.md) for what was and was not seen running

## What happens in the game

A player carries up to two guns, on **1** and **2**. **Left mouse** fires, **R** reloads.

The target is **tight gunplay for fast movement** (owner, 2026-11-12): the first shot lands on the crosshair, recoil is a pattern a player can learn, and running or jumping barely costs accuracy.

- **Pistol**: one shot per press (up to 400 a minute; a press made a moment early is remembered, never swallowed), 34 damage, 12 rounds. **Three hits kill.** Dead on; each shot kicks the view up about a degree.
- **AR-15**: fires while held at 600 a minute, 20 damage, 30 rounds. **Five hits kill, 0.4 s at full rate.** The view climbs almost straight for eight shots, then sweeps left, then right, and a longer burst keeps sweeping. It is the same path every burst, so pulling down and against the sweep keeps it on target. The view only settles once the trigger rests, and the pattern starts over after 0.35 s of rest.

The crosshair moves with the kick and the shot goes where it points. On top of the pattern there is a small random stray that builds through a burst (at most 0.7° on the AR-15, 0.6° on the pistol); it is zero on a rested first shot.

The shooter sees the shot at once: muzzle flash, tracer, view kick, the round gone from the magazine. The hit itself is the host's call; when the host agrees, the shooter gets a hit confirmation (the hook for a hit marker) and, on a kill, a separate kill confirmation.
A hit flashes the target red; by default it does not shove a living target, so being shot never takes a player's movement away (`Hit Shove` on the gun turns that on). The killing shot throws the body hard, **13 m/s with lift by default, which carries a body standing roughly 1.3 to 6 m from a window out through it** (closer than that it hits the 0.6 m sill first).
A dead player falls flat, can't steer, shoot, reload or switch, and comes back **5 seconds** later on the floor they died on with full magazines.
Bullets always pass through glass and break it on the way ([GLASS.md](GLASS.md)). Bullets also wear down locked doors once `DoorDamage` is on the door prefab.
While the menu is open (the mouse is free) the fire, reload and slot buttons do nothing.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Whether a shot happened, what it hit, the damage | **Host.** The owner's game only asks | Owner → host: one `ServerFire` per shot, reliable, about 27 B of payload (slot, shot number, eye position, aim as two 16-bit angles, ticks since the last shot, the host tick the shooter was looking at, moving/airborne flags) | Per shot |
| Tracer for everyone else | Host | `RpcShot`: where the shot ended + whether it hit, 13 B, unreliable, to everyone but the shooter | Per shot |
| Hit confirmation | Host | `TargetHitConfirmed` to the shooter only: point, damage, killed; reliable | Per hit |
| Which gun is in each slot, which slot is out | Host | Three one-byte SyncVars, to everyone | On change |
| Magazines, last shot number answered, reloads completed | Host | One packed 8-byte SyncVar, **to the owner only** | On change (each shot, each reload) |
| Health, dead flag, last attacker | Host | Three SyncVars on `Health`, to everyone | On change |
| Hit, death (with the throw: the ragdoll trigger and impulse), respawn | Host | One RPC each to all clients; hits unreliable, death and respawn reliable | Per event |
| Reload started (for other players' sound and animation) | Host | One RPC to everyone but the shooter | Per reload |
| Muzzle flash, tracer, view kick, gun model, hit flash, falling over | Each machine, locally | Nothing | — |

**What the owner predicts:** its own shot (flash, tracer, kick), its ammo count, the start of a reload, and a weapon switch. The ammo shown is the host's last count minus the owner's shots the host has not answered yet, so it never flickers upward. A reload only *finishes* when the host says the magazine is full, so it ends one trip to the host late (a few tens of ms) and can never give ammo the host didn't.

**What the host checks on every shot:** the gun is the one in hand, the player is alive, the magazine isn't empty, no reload is running, and the shot isn't faster than the gun (a tenth of a second of slack for network jitter; the long-run rate is exact). A refused shot is still answered so the owner's count corrects itself.

**Stray** is worked out the same way on both ends from the shot number and the owner's own shot timing, so the tracer the shooter sees is the path the host traced. The host never lets the owner claim the gun was more rested than the host saw. The moving and airborne accuracy multipliers use the owner's own word for whether it was moving or in the air (the host's copy of the body is a few ticks behind, and guessing differently would split the two tracers); both default to almost nothing, so there is little to gain by lying.

**Recoil** is the owner's view: the kick moves the camera, and the aim the owner sends is where the camera points. The host never sees "recoil", only the aim.

**Lag allowance (favour the shooter):** each shot carries the host tick the shooter's screen was showing when they aimed: on the host its own latest tick; on a client the newest host tick it has heard, less the two ticks FishNet holds remote bodies back to smooth them. The host keeps half a second of every body's position and tests the shot against where each target stood on that tick, never further back than `Lag Allowance Seconds` on the player's `WeaponHolder` (0.25 s). The host's own shots are judged on the spot, not sent round through the network. The shooter's eye is taken from their own game as long as it is within `Origin Tolerance` (2.5 m) of where the host has them. Bodies are tested as capsules by maths, not by their colliders, so nothing is moved to rewind them.

**What a player on a 100 ms connection sees:** other bodies are drawn about 85 ms behind where the host has them (half the round trip plus two ticks), which is 0.8 m for a target sprinting at 9 m/s and 2 m at 24 m/s. They aim at the body on their screen and the host tests the shot there; the host still decides. The cost falls on the target: they can be hit up to that long after reaching cover, never more than the 0.25 s cap. **Measured so far: the client's hits on runners are not landing reliably yet** (see the handover).

**Bandwidth, 8 players all holding the AR-15 trigger:** 10 shots a second each. Up to the host: 8 × 10 × ~33 B ≈ 2.6 KB/s in total. Down from the host: each client gets 7 other players' tracers (7 × 10 × ~20 B ≈ 1.4 KB/s) plus its own ammo updates and hit confirmations (10 × ~12 B + up to 10 × ~25 B ≈ 0.4 KB/s), about **1.8 KB/s per client, 13 KB/s leaving the host**. Hits add one ~45 B message per hit to each client. Nothing here is sent per frame; a player not shooting sends nothing. These are payload sizes plus an estimate of FishNet's per-message header, not measured on the wire.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored:** every field on a `WeaponDefinition` (damage and falloff, fire rate, first-shot stray, stray growth, the moving and airborne multipliers, the recoil pattern with its loop point, reset and recovery, magazine, reload and draw times, shoves) (one asset per gun in [Assets/_Project/Profiles/Weapons/](../../Assets/_Project/Profiles/Weapons/)), the order of the `WeaponCatalog`, health and respawn values on the player's `Health`, feel values on its `WeaponView`, the gun models in [Assets/_Project/Prefabs/Weapons/](../../Assets/_Project/Prefabs/Weapons/).
**Derived:** ammo, bloom, cooldowns, health, who is dead: all on the components, never written back to an asset.

The catalog names guns on the network by their place in the list. **Add new guns at the end; never reorder.**

## For other systems

- **Pickups** call `WeaponHolder.Equip(definition)` on the host (first empty slot, or over the gun in hand when both are full; `Equip(definition, slot)` to choose) and `Unequip()` / `Unequip(slot)`. The gun must be in the catalog. A picked-up gun arrives with a full magazine.
- **A scene's starting guns**: a `StartingLoadout` object in the scene. Without one, players start empty-handed.
- **Anything that can be hurt** implements `CloseTheDeal.Core.IDamageable` and is found on the collider a bullet stops on, or its parents. Players and dummies use `Health`, which bullets test separately.
- **Explosions**: `Knockback.Explode(..., attackerClientId)` now also deals `BlastProfile.Damage` (0 by default, so the old test blast is unchanged) and breaks glass in its radius.
- **Game mode**: `Health.OverrideNextRespawn(position)` on the host changes where a dead player comes back (the GDD's "blown out of a window goes to the last checkpoint" needs it). `Health.AnyDied` carries the attacker's client id.
- **Sound, effects, UI** hang on events; none of the assets exist yet:

| Event | Where it fires | For |
|---|---|---|
| `WeaponHolder.Fired(ShotInfo)` | Every machine (`Predicted` on the shooter's) | Gunshot, tracer, casing |
| `WeaponHolder.DryFired` | Shooter | Empty click |
| `WeaponHolder.ReloadStarted(seconds)` | Every machine | Reload sound and animation |
| `WeaponHolder.ReloadFinished` / `ReloadCancelled` | Shooter | Reload end, HUD |
| `WeaponHolder.AmmoChanged(inMagazine, size)` | Shooter | Ammo counter |
| `WeaponHolder.WeaponChanged(definition, slot)` | Every machine | Weapon name, draw sound |
| `WeaponHolder.HitConfirmed(point, damage, killed)` | Shooter | Hit marker |
| `WeaponHolder.KillConfirmed(point)` | Shooter | Kill sound and marker |
| `Health.Damaged` / `Died` / `Respawned` (and static `Any…`) | Host and every client | Hurt sound, ragdoll, kill feed, the secretary |
| `Health.HealthChanged(current, max)` | Every machine | Health bar |

## Lane-local inputs

Defined in code in `WeaponHolder.WeaponInput`, not in the shared actions asset, until the movement lane folds them in:

| Action | Binding |
|---|---|
| Fire | `<Mouse>/leftButton` |
| Reload | `<Keyboard>/r` |
| Weapon Slot 1 | `<Keyboard>/1` |
| Weapon Slot 2 | `<Keyboard>/2` |

## Scripts

Under [Assets/_Project/Scripts/Combat/](../../Assets/_Project/Scripts/Combat/):

- `WeaponDefinition.cs`, `WeaponCatalog.cs`: the tuning assets.
- `WeaponHolder.cs`: on the player. Slots, magazines, fire, reload, switch; the owner's prediction and the host's checks.
- `ShotResolver.cs`: where a shot goes. Passes through glass, tests bodies as capsules (rewound on the host), packs the aim, works out the spread.
- `Health.cs`: on the player and on dummies. Damage, death, respawn, position history for the lag allowance.
- `WeaponView.cs`, `TracerPool.cs`: local visuals only.
- `StartingLoadout.cs`: a scene's starting guns.
- `MovingTarget.cs`: runs a practice dummy back and forth at a set speed.
- `DoorDamage.cs`: lets bullets wear down a `BreakableDoor`.
- `Knockback.cs`, `BlastProfile.cs`: blasts; extended with damage, an attacker, glass, and the shared shove maths.

The interface is [Assets/_Project/Scripts/Core/IDamageable.cs](../../Assets/_Project/Scripts/Core/IDamageable.cs).

Set-up is one editor file, [ShootingLaneSetup.cs](../../Assets/_Project/Editor/Lanes/ShootingLaneSetup.cs): **Close the Deal > Lanes > Set Up Shooting** writes the assets (never over a person's edits), adds `Health`, `WeaponHolder` and `WeaponView` to `Player.prefab` and builds `Assets/_Project/Scenes/Lanes/Lane_Shooting.unity`. **Add Shooting To Open Scene** gives any other scene the glass registry and a starting loadout. **Run Shooting Self-Check** plays the test scene and checks the rules above.

## Known limitations

- **No sound, no HUD, no hit marker on screen, greybox box guns.** The events are there; nothing listens yet.
- **No ragdoll.** A dead body is the capsule tipped flat and thrown; `Health.Died` carries the impulse a ragdoll would use.
- **Ammo is endless**: a reload always fills the magazine. No reserve ammo exists; that is a design question.
- **Friendly fire is on.** Teams exist only as a spawn side so far; nothing here knows who is on which firm.
- **The left-click test blast in `PlayerMotor` is switched off by the set-up menu**, by pointing the motor at a blast profile that does nothing (`Profiles/Weapons/NoTestBlast.asset`) and clearing its two blast masks, because it shares the fire button. It is a workaround: the real fix is for the movement lane to remove the test blast from `PlayerMotor`. Until explosives exist, locked doors are broken with bullets instead (`DoorDamage`, 300 damage by default).
- **Respawn is "last place this player stood on the ground"**, sampled four times a second, not a spawn point on the floor. A player killed after falling to the street respawns in the street. Window exits don't yet go to a checkpoint (no checkpoints exist).
- **A dead player can still look around**, and their body still blocks others until it respawns.
- **Lag allowance trusts the shooter's reported lag**, capped at 0.25 s. A cheating client could always claim the cap; acceptable for a friends-only game.
- **The gun in other players' hands does not follow where they look up or down**; it points along the body.
- **GDD mismatch:** [GDD.md](../GDD.md) still says combat "should feel chaotic rather than precise" and lists the rifle as the reliable gun. The guns are built to the owner's newer words (tight gunplay); the GDD needs his edit.
- **The pattern's shot count lives on the shooter's game only.** Other players' guns show no kick.
- **Tuned on paper and against dummies, not in a playtest.** Every number above is a starting point on the gun assets.
- Bullets don't push furniture.
