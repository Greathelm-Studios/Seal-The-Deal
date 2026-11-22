# Weapons

**Status:** Current (greybox guns; combat HUD and synthesised stand-in sounds wired, not yet judged in a multi-player playtest) · **Last updated:** 2026-11-21

## What the player sees

A player carries up to two guns, on **1** and **2**. **Left mouse** fires, **R** reloads.

The target is **tight gunplay for fast movement** (owner, 2026-11-12): the first shot lands on the crosshair, recoil is a pattern a player can learn, and running or jumping barely costs accuracy.

- **Pistol**: one shot per press (up to 400 a minute; a press made a moment early is remembered, never swallowed), 34 damage, 12 rounds. **Three hits kill.** Dead on; each shot kicks the view up about a degree.
- **AR-15**: fires while held at 600 a minute, 20 damage, 30 rounds. **Five hits kill, 0.4 s at full rate.** The view climbs almost straight for eight shots, then sweeps left, then right, and a longer burst keeps sweeping. It is the same path every burst, so pulling down and against the sweep keeps it on target. The view only settles once the trigger rests, and the pattern starts over after 0.35 s of rest.

The crosshair moves with the kick and the shot goes where it points. On top of the pattern there is a small random stray that builds through a burst (at most 0.7° on the AR-15, 0.6° on the pistol when tapped faster than about three a second) and only settles once the trigger has rested for the pattern's reset time; it is zero on a rested first shot.

The shooter sees the shot at once: muzzle flash, tracer, view kick, the round gone from the magazine. The hit itself is the host's call; when the host agrees, the shooter gets a hit confirmation (and hit marker) and, on a kill, a separate kill confirmation.
A hit flashes the target red and rocks it away from the shot (`Health` flinch); by default it does not shove a living target, so being shot never takes a player's movement away (`Hit Shove` on the gun turns that on). The killing shot throws the body hard, **13 m/s with lift by default, which carries a body standing roughly 1.3 to 6 m from a window out through it** (closer than that it hits the 0.6 m sill first).
A dead player falls flat, cannot steer, shoot, reload or switch, and comes back **5 seconds** later on the floor they died on with full magazines.
Bullets always pass through glass and break it on the way ([GLASS.md](GLASS.md)). Bullets also wear down locked doors via `DoorDamage` on the door prefab.
While the menu is open (the mouse is free) the fire, reload and slot buttons do nothing.

## Feel reference: NorseFPS

Adam points at NorseFPS (his other project, `Greathelm-Studios`) as the bar for gun feel. It is a single-machine game; in Seal the Deal every feel element runs on the shooter's own machine and the host still decides the hit.

1. **A big kick that comes back, and a small climb that stays.** Each shot snaps the camera up by several degrees, but only about a tenth of it stays as aim drift. The kick settles just before the next shot is allowed, so tapping at the gun's rate always lands on a still view.
2. **Authored recovery curves with overshoot (`DecayingImpulse`).** Every kick holds its peak for one drawn frame (`_fresh`), drops fast, swings slightly past rest, and settles. These are curves, not springs, so the attack looks the same at any frame rate.
3. **The gun out-moves the camera.** On each shot the model in the hand flips several times further than the view, pushes back several centimetres, rolls and twists, and settles more slowly than the camera.
4. **Same-frame firing plus a trigger buffer** (`PressBufferSeconds = 0.15 s`), so no click is lost.
5. **Strong hit confirmation:** ticks that spread as they fade, a cyan variant for a head hit, and a larger longer electric variant for a kill, matched sounds, and damage direction markers.
6. **Muzzle light and flash:** a point light plus a short, size- and spin-varied flash, and a tracer from the visible muzzle to the true end point, misses included.
7. **Spread that rewards tapping, and an honest crosshair** (`WeaponHolder.CurrentSpreadDegrees`) whose gap matches the real cone.
8. **A clean firing pose:** firing damps bobbing for `0.25 s`, adds a short rotational camera shake (strongest shake wins so bursts do not stack up), and punches field of view outward on the snap curve (restoring base FOV cleanly even if movement FOV changes alongside it).

**Not built from NorseFPS:** kill hit-stop (slowing shared networked time is unsuitable; a view-only pause is a later option), stick aim assist, and shell casings.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Whether a shot happened, what it hit, the damage | **Host.** The owner's game only asks | Owner to host: one `ServerFire` per shot, reliable, ~27 B payload (slot, shot number, eye position, 32-bit packed aim, ticks since last shot, viewed host tick, stance flags) | Per shot |
| Tracer for everyone else | Host | `RpcShot`: end point + surface byte, 13 B, unreliable, `ExcludeOwner = true` | Per shot |
| Hit confirmation | Host | `TargetHitConfirmed` to the shooter only: 1-byte flags (killed, precision), reliable | Per hit |
| Which gun is in each slot, which slot is out | Host | Three 1-byte SyncVars (`_slot0`, `_slot1`, `_active`), to everyone | On change |
| Magazines, last shot number answered, reloads completed | Host | One packed 8-byte SyncVar (`_ownerState`), **to the owner only** (prevents parts arriving out of sync) | On change (each shot, each reload) |
| Health, dead flag, knocked-out flag, last attacker | Host | Four SyncVars on `Health`, to everyone | On change |
| Hit, death (throw impulse), respawn, knockout, recovery | Host | One RPC each (`RpcDamaged` unreliable with 32-bit packed direction; `RpcDied`, `RpcRespawned`, `RpcKnockedOut`, `RpcRecovered` reliable) | Per event |
| Reload started (for other players' sound and animation) | Host | `RpcReloadStarted` (`ExcludeOwner = true`) | Per reload |
| Muzzle flash, tracer, view kick, gun model, hit flash, flinch | Each machine, locally | Nothing | Local only |

## Architecture and design rationale

### Prediction, ammo sync, and host validation (`WeaponHolder`)

- **Owner prediction:** The owner predicts its own shot (flash, tracer, kick), magazine count, reload start, and weapon switch (`_switchTrustedUntil = 0.4 s` before yielding to host `_active`). The host's own local player skips the duplicate client trace in `FireShot` and executes directly through `HandleFireRequest` so only one `Fired` event fires.
- **Packed `_ownerState`:** Both slot magazines, `_hostAckSeq`, and `_hostReloadsDone` share one 64-bit `SyncVar<ulong>` so the owner's ammo count (`OwnerAmmo`) never flickers upward between a shot ack and a magazine update. `HandleFireRequest` always calls `PushOwnerState()`, even when a shot is refused, so the owner's pending count reconciles instead of staying one short.
- **Host checks on every shot (`HostMayFire`):** Verifies the gun is in hand, the player is alive, `FireHeld` is false, equip/reload timers have passed (within `TimingSlackTicks = 9`), the magazine is non-empty, and fire rate has elapsed (within `RateSlackSeconds = 0.1 s` jitter slack while maintaining exact long-run interval).
- **Sequential shot numbers (`_hostAckSeq`):** Shots arrive reliably in order. Any `seq != _hostAckSeq + 1` is refused so a client cannot cherry-pick spread seeds.
- **Stance & rest validation:** Spread is deterministic from `ShotSeed(seq)` and shot timing (`Settled`). The host caps claimed rest ticks at `tick - _hostLastShotTick + TimingSlackTicks`, and `ValidateStance` merges client stance with the host's `PlayerMotor` stance (host airborne/moving overrides a client claiming still) so a client cannot spoof standing-still accuracy.
- **Eye origin validation (`TrustedOrigin`):** Accepts the shooter's claimed eye position if it is within `_originTolerance` (2.5 m) of the segment from the host's eye along the body's velocity over `lagTicks`, AND `ShotResolver.ClearPath` finds no solid wall or locked door between the host's eye and the claimed eye (glass is ignored). Otherwise fires from the host's eye.

### Lag allowance and mathematical capsule trace (`ShotResolver`)

- **Why mathematical capsules instead of colliders:** `ShotResolver.Trace` tests `Health.All` directly with analytical ray-capsule math (`RayCapsule`) rather than moving scene colliders to rewind positions.
- **Position history and respawn guard (`Health`):** The host records feet positions in a 64-tick ring buffer (`HostPostTick`) and records `_aliveFromTick` on spawn/respawn. `WasAliveAt(tick)` rejects rewound ticks from before the target's latest respawn so a shooter cannot hit a player at their old corpse.
- **Lag window (`SeenTick`, `InterpolationTicks = 3`):** Clients report `LastPacketTick.RemoteTick - 3` (covering FishNet's interpolation holdback). The host clamps `viewTick` between `tick - allowance` (`LagAllowanceSeconds = 0.25 s`) and `tick`. `NearestBody` tests `rewindTick - 1`, `rewindTick`, and `rewindTick + 1` (`RewindSlackTicks = 1`), widening moving targets slightly to favour the shooter.
- **Glass penetration (`ShotResolver.Trace` / `ClearPath`):** Rays step `PaneSkin = 0.002 m` past each `GlassPane` hit up to `MaxPanesPerShot = 8` unique panes (deduplicating hollow meshes that hit front and back faces). Once the glass budget is exceeded, the shot stops in the glass. `BreakCrossedPanes` breaks recorded panes on the host.

### Health visuals, HUD, sounds, tracers, and doors

- **Hit flash & SRP Batcher (`Health.ApplyFlash`):** Saves any existing `MaterialPropertyBlock` (such as eye card properties) before applying `_flashColour`, and calls `SetPropertyBlock(null)` on restore when no block was previously set so URP SRP Batcher eligibility is restored.
- **Dead steering lock (`Health.DeadTick`):** Re-applies `MoveState.Knocked` if a blast overwrites the motor's shorter knockback timer while the player is still dead.
- **Weapon view (`WeaponView`):** Runs at `[DefaultExecutionOrder(1000)]` after `PlayerCamera` so view kick, shake, death slump, and FOV punch apply cleanly on top of the player's look without accumulating across frames. Caches instantiated weapon view prefabs per `WeaponDefinition` to avoid allocations on switch.
- **Pools and culling:** `TracerPool` caches material fade modes (`WidthOnly` on opaque URP Unlit `Tracer.mat` to avoid gradient rebuilds; `PropertyBlockAlpha` / `VertexColor` on transparent materials), capping at 64 live tracers and 32 pooled per prefab. `LocalEffects` pools impact particles up to 32 live and culls beyond 60 m. `CombatSounds` pools 32 3D `AudioSource`s with farthest-voice stealing, 60 m distance culling, and single-instance `GlassPane.AnyBroken` registration.
- **Combat HUD (`CombatHud`):** Isolates hit-marker ticks in a child `Canvas` (`HitMarkerCanvas`) so tick alpha/position updates do not dirty the main HUD canvas; subscribes to `LocalDeath.Down` in `Awake` (not `OnEnable`) because the HUD object is disabled while the menu is open; caches uppercased weapon strings per `WeaponDefinition`.
- **Doors (`DoorDamage`):** Accumulates bullet damage on the host via `IDamageable` and calls `BreakableDoor.TakeBlast()` each time `_damagePerBlast` is banked; clears banked damage on pool re-enable (`OnEnable`).

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored:** every field on `WeaponDefinition` (damage, falloff, fire rate, spread, bloom, recoil pattern, loop point, recovery curves, magazine, reload/draw times, shoves, sounds, prefabs) under [Assets/_Project/Profiles/Weapons/](../../Assets/_Project/Profiles/Weapons/); `WeaponCatalog` order; `Health`, `WeaponView`, `WeaponHolder`, `CombatHud`, `CombatSoundSet`, `BlastProfile`; gun models in [Assets/_Project/Prefabs/Weapons/](../../Assets/_Project/Prefabs/Weapons/).
**Derived:** ammo, bloom, cooldowns, health, dead state, position history: all on runtime components, never written back to assets.

`WeaponCatalog` identifies guns over the network by byte index. **Add new guns at the end; never reorder.**

## For other systems

- **Pickups:** Call `WeaponHolder.Equip(definition)` on the host (first empty slot, or replaces active slot when full; `Equip(definition, slot)` to target a slot) and `Unequip()` / `Unequip(slot)`.
- **Holding fire:** Set `WeaponHolder.FireHeld` (static predicate checked on owner and host) to keep the trigger up and block firing while carried items or lobby rules apply.
- **Starting guns:** Place `StartingLoadout` in the scene. Without one, players start empty-handed.
- **Damageable objects:** Implement `CloseTheDeal.Core.IDamageable` on the hit collider, its parents, or its attached `Rigidbody`.
- **Explosions:** `Knockback.Explode(..., attackerClientId)` applies `BlastProfile.Damage`, throws players and `Prop`s, triggers `BreakableDoor.TakeBlast()`, and breaks glass via `GlassRegistry.BreakInRadius`.
- **Game mode:** `Health.OverrideNextRespawn(position)` on the host overrides the next respawn point once; `Health.AnyDied` carries the attacker's client id.

## Combat HUD and sound contracts

Find the local player's `WeaponHolder` and `Health` on `PlayerMotor.Local`.

| Element | Source | Owner / Timing |
|---|---|---|
| Ammo | `WeaponHolder.AmmoChanged(inMagazine, magazineSize)`, `AmmoInMagazine` | Host count, owner prediction on top |
| Reserve ammo | None (reloads always fill the magazine) | Design (Adam) |
| Slots | `WeaponHolder.WeaponChanged(definition, slot)`, `WeaponInSlot(0/1)`, `ActiveSlot` | Host (slots), owner (predicted switch) |
| Reload / Equip | `ReloadStarted(seconds)`, `ReloadFinished`, `ReloadCancelled`, `State`, `StateProgress` | Owner shows, host confirms completion |
| Empty trigger | `DryFired` | Owner only |
| Hit / Kill confirm | `HitConfirmed(point, damage, killed, precision)`, `KillConfirmed(point)` | Host only; `CombatHud` suppresses downgrade while kill marker is active |
| Damage direction | `Health.Damaged(health, DamageInfo)` | Host (unreliable, packed direction) |
| Health / Death | `HealthChanged`, `Died`, `Respawned`, `IsDead`, `SecondsUntilRespawn` | Host |
| Crosshair spread | `WeaponHolder.CurrentSpreadDegrees` | Owner |

**Sounds (`CombatSounds` + `CombatSoundSet` + `WeaponDefinition`):** Plays 3D shot, impact (world/body), dry fire, weapon draw, 3-stage reload (15%, 60%, 85%, cancelled on `ReloadCancelled`/`Died`), and glass shatter (`GlassPane.AnyBroken`), plus 2D hurt, death, and respawn sounds. Hit marker and kill ticks play through `UiSound` (`HitMarker`, `HitMarkerHead`, `KillConfirm`).

## For a first-person arms model (viewmodel)

Untick **Draw Held Gun** on `WeaponView` when a viewmodel (`PlayerViewmodel`) draws the gun. Tracers, impacts, view kick, shake, and FOV punch stay active; `WeaponView` skips the first-person gun model and flash. Call `WeaponView.SetExternalMuzzle(muzzleTransform)` so tracers originate from the viewmodel's muzzle.

## Lane-local inputs

Defined in code in `WeaponHolder.WeaponInput` until folded into the shared actions asset:
- Fire: `<Mouse>/leftButton`, `<Gamepad>/rightTrigger`
- Reload: `<Keyboard>/r`, `<Gamepad>/buttonNorth` (Y)
- Weapon Slot 1: `<Keyboard>/1`, `<Gamepad>/dpad/left`
- Weapon Slot 2: `<Keyboard>/2`, `<Gamepad>/dpad/right`

## Scripts

Under [Assets/_Project/Scripts/Combat/](../../Assets/_Project/Scripts/Combat/):
- `WeaponDefinition.cs`, `WeaponCatalog.cs`, `CombatSoundSet.cs`, `BlastProfile.cs`: Tuning assets.
- `WeaponHolder.cs`, `ShotResolver.cs`, `Health.cs`: Networked slots, prediction, host validation, lag rewind, health, respawn.
- `WeaponView.cs`, `TracerPool.cs`, `LocalEffects.cs`, `DecayingImpulse.cs`, `CombatHud.cs`, `CombatSounds.cs`: Local visuals, HUD, and audio.
- `StartingLoadout.cs`, `MovingTarget.cs`, `DoorDamage.cs`, `Knockback.cs`: Scene loadout, moving practice dummy, door bullet damage, shared explosion/shove math.
- `Assets/_Project/Scripts/Core/IDamageable.cs`: Shared host-only damage interface.
- Editor setup: [ShootingLaneSetup.cs](../../Assets/_Project/Editor/Lanes/ShootingLaneSetup.cs).

## Known limitations

- **Not yet judged in a multi-player playtest.** Combat HUD and synthesised placeholder gun/impact sounds are wired, but sound quality and fast-player lag compensation have not been evaluated in a live multi-player match.
- **Ragdoll death and knocked-out stun:** `PlayerRagdoll` subscribes to `Health.Died`, `Health.Respawned`, `Health.KnockedOut`, and `Health.Recovered` (when no `PlayerRagdoll` is active, `Health` falls back to tipping `_tipPivot` and `WeaponView` falls back to its camera slump). See [HIT_REACTIONS.md](HIT_REACTIONS.md).
- **Ammo is endless:** reloads always fill the magazine (no reserve pool).
- **Friendly fire is on.** Team damage filtering awaits Adam's design call.
- **Fallback respawn** uses last grounded position sampled 4 times/sec; window exits require match-mode `OverrideNextRespawn`.
- **A dead player can still look around**, and their capsule still blocks movement until respawn.
- **Lag allowance trusts the shooter's reported `viewTick`**, capped at 0.25 s, and spread seed comes from `OwnerId` + `seq` (known to the client). Acceptable for friends-only lobbies.
- **Third-person gun aim** points along the body rather than vertical pitch.
- **GDD mismatch:** [GDD.md](../GDD.md) still describes combat as "chaotic rather than precise"; the current weapons implement tight gunplay per the owner's newer direction.
- **Bullets do not push physics props.**
