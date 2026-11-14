# ITEMS

**Status:** Current · **Last updated:** 2026-11-13 · **Verified against:** the pickups lane commit

## What happens in the game

Look at something within reach and a prompt names it: **F · Pick up Chair**. Press **F** and it is in your hands.

A player has **one hands slot**. While it is full: **left mouse** throws, **G** drops, **right mouse** uses the item if it has a use. Full hands cannot shoot.

- **Furniture** (chair, monitor, filing cabinet, cubicle panel) is carried in front of you. Heavy things slow you down and shorten your jump. A thrown chair shoves whoever it hits and hurts them by how fast and how heavy it was. A desk or copier is too heavy and the prompt says so.
- **Cubicle panel:** stays solid in your hands, so it is moving cover. It stops shots and other players; it does not stop you.
- **Grenade Case:** right mouse arms it. It beeps faster and faster for 3.5 seconds, then explodes wherever it is, throwing players and furniture and hurting anything in the blast. Hold it too long and it goes off in your hands.
- **Fire Extinguisher:** right mouse pulls the pin. It pours a wall of smoke for 6 seconds and cannot be stopped. Carried, it lays smoke where you walk. Let go, the gas drives it spinning round the floor into whoever is in the way.
- **Wet Floor Sign:** right mouse puts it down ahead of you and the floor around it is wet. Anyone who runs through, from either firm, goes flying; walking is safe. Picking the sign up, or knocking it away, clears the trap.
- **Pistol and AR-15:** real pickups with their own definitions. In `Lane_Integration.unity` taking one puts it straight into the weapon slots; in a scene without the integration glue they are carried and thrown like any object. See "Guns" below.

Anything knocked out of your hands (a blast, a heavy hit, leaving the game) stays in the level where it fell. Anything that falls out of the building is put back where it started two seconds later. A used-up item (an exploded case, an empty extinguisher) comes back fresh at its starting place after the time on its definition.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Who holds an item | **Host.** A client asks; the host checks reach, weight, empty hands and that the item is still loose, and the first valid request wins | One value per item: the holder (`Pickup`) | On change |
| Out of play / back in play | Host | One flag per item (`Pickup`) | On change |
| A loose item's motion | Host; it is an ordinary prop ([PROPS.md](PROPS.md)) | Switchboard `NetBody` ([SWITCHBOARD.md](SWITCHBOARD.md)) | While moving, inside Switchboard's budget |
| A carried item's solid part | Host moves it to the carrier's hands each tick | Switchboard `NetBody`, like any moving prop | While the carrier moves |
| A carried item's look | Each machine, locally: the visual child follows the carrier's view (own camera) or body (other players) every frame | Nothing | — |
| Throw, drop, use | Owner asks (one message each, the throw carries the aim direction); host does it | Arrives as holder change plus motion | On press |
| Impact damage and shove | Host only, from the host's own collision | Damage is whatever the target syncs; shove rides the player's body state | On hit |
| Grenade Case fuse | Host | The tick it goes off | On arm |
| Explosion | Host: `Knockback.Explode` plus `ItemBlast.Damage` | One message with the point, for the flash and sound | Once |
| Extinguisher smoke | Host owns the timing and the motion | The tick the smoke stops, and an empty flag. The smoke itself is drawn locally: no particle is ever sent | On pull |
| Wet floor | Host decides every slip | One flag (down or not); one message per slip for sound | On change |
| Carry slowdown | Derived on every machine from what is held, so the owner's prediction and the host use the same speeds | Nothing extra | — |

A refused request is answered to that player only, with the reason (`DenyReason`), so the UI can say "Someone else got it".

**From a client:** see [HANDOVER_pickups.md](../HANDOVER_pickups.md) for exactly what has been seen from a second machine. Not yet seen over Steam or with real latency.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored**, all under `Assets/_Project/Profiles/Items/`:

- `Item_*.asset` (`ItemDefinition`): name, kind, weight, how it sits in the hands, throw speed and tumble, the word for its use, and how long a used-up one takes to come back.
- `DefaultCarry.asset` (`CarryProfile`): reach, the weight limit (35 kg) and slowdown, what is kept of the carrier's speed on release, the impact rules for thrown objects, and the fall-out-of-the-building rule.
- `GrenadeCase.asset`, `GrenadeCaseBlast.asset`, `Extinguisher.asset`, `WetFloor.asset`: each gadget's own numbers.

**Derived at runtime:** who holds what, fuses, the slowed copy of the movement profile (the authored movement profile is never written), each item's starting place (where the host first spawned it).

An item's weight is authored twice, on its definition and as its prefab body's mass. Keep them equal: the definition drives the weight limit and the slowdown, the body mass drives the impact.

## Rules

- **Weight:** up to 3 kg carries free. From there to the 35 kg limit, speed falls evenly to 45% and jump height to 50%.
- **Impact:** only a thrown object hurts, for 3 seconds after the throw or until it has hit someone. Damage = (impact speed − 4 m/s) × kg × 0.35. Shove = impact speed × kg × 0.08 m/s, capped at 12.
- **Prop cap:** every item is a prop and counts toward the 16 per floor in [PROPS.md](PROPS.md). Nothing here spawns extra objects: an explosion scatters what is already on the floor, smoke is local particles, the puddle is part of the sign. Using an item up hides it and later puts it back; it is not despawned and respawned unless its definition says it never returns.
- **Out of the building:** a loose item more than 6 m below where it started is taken out of play and is back at its starting place 2 seconds later.
- **Layers:** a carried panel's solid part sits on the **Player** layer while carried, so its carrier's own footing and ledge-climbing never treat it as ground, and it ignores its carrier's body. It goes back to the **Prop** layer when let go.
- **Grabbing:** in reach (2.5 m + 0.75 m allowance for a joining player's lag) and nothing solid between the eye and the item that is not itself a pickup: no grabbing through walls, floors or locked doors. A carried item is also held back by locked doors as by walls, so it is never let go on the far side. The player who let go of something waits 0.6 s (`RetakeSeconds`) before taking that same thing back; anyone else may catch it at once.
- **Out of play mid-use:** an armed Grenade Case that falls out of the building is disarmed, and a venting extinguisher stops, before they are put back.
- **In the hands (drawn only):** a taken item eases from where it lay into the hands in 0.12 s (light) to 0.24 s (at the weight limit), easing out. In the carrier's own view it trails a fast turn, swinging round the eye, more for heavier things (follow 28/s light, 9/s heavy) and never more than 10°. Other players see it level at the carrier's chest with no lag. All on the carry profile, "Feel in the hands".
- **Telling the Grenade Case from a firm's briefcase:** it is never called a briefcase in any text a player reads; it is red with a yellow hazard band, a keypad and a warning light, where the objective is the firm's own case; and it beeps and flashes from the moment it is armed.

## Any loose prop

Any loose object can be picked up and thrown, not only the items above (Alex's direction, 2026-11-13; not yet a GDD decision): monitors, tablets, plant pots, chairs, anything that is its own networked prop and weighs no more than the 35 kg limit. There is one pickup system: this one. Nothing new is synced for it.

**What a prop needs** (Close the Deal > Items > **Make Selected Prefabs Throwable** adds whatever is missing to the selected prefabs and logs what it did):

- Its own prefab, not merged into a desk or a floor piece, with its pivot at its base or centre.
- One simple collider (a box or a capsule; a mesh collider must be convex) on the **Prop** layer.
- A `Rigidbody` whose **mass is its real weight in kg**: it decides whether it can be lifted, how much it slows the carrier, how fast it is thrown and how much it hurts. The tool guesses one from its size (40 kg per cubic metre) and says so; set the real one.
- `NetworkObject`, Switchboard's `NetBody` (the default profile), `Prop` and `Pickup`. **Set Up Pickups** also gives a `Pickup` to every prefab under `Assets/_Project` that has a `Prop` and none.

**What is worked out at runtime** (`LooseItemDefinition`), when its `Pickup` has no definition:

| Thing | From | Rule |
|---|---|---|
| Name in the prompt | The prefab's name | `SM_PlantPot_02` reads "Plant Pot": prefixes `SM_ PF_ Prop_ Kit_ Loose_` and digits dropped, words split. Name the prefab what players should read |
| Weight | `Rigidbody.mass` | Over the carry profile's limit: "Too heavy" |
| Throw speed | Weight | 15 m/s at 1 kg or less, falling evenly to 5 m/s at the limit (carry profile, "Any loose prop") |
| Tumble | Weight | 2.2 turns/s light, 0.3 at the limit |
| Damage and shove | Weight and impact speed | The same rule as every thrown thing (Rules, below) |
| Hold pose | The size of what it draws | Centred, its near face 0.55 m ahead of the eye, its base 0.3 m + 0.7 × its height below the eye; wall clearance is half its footprint |
| Carry rules | The players' `CarryProfile` | Unless its `Pickup` names its own |

A hand-made `ItemDefinition` on the `Pickup` replaces all of it. Props sharing a name, mass and size share one worked-out definition.

A prop with no `Visual` child shows a draw-only copy of its meshes in the hands, made the first time it is carried, while its own meshes are hidden; its solid part stays where the host puts it, as for every item. Skinned meshes are not copied.

**Prop budget:** unchanged. Making a prop throwable adds no object: every loose object already counts toward the 16 props per floor ([PROPS.md](PROPS.md)). More throwable objects in a floor means more props in that floor, inside the same cap; a floor that wants more must take some away.

**Bandwidth:** per prop, two values that change only when it is taken, let go, used up or comes back (the holder, about 2 bytes, and the out-of-play flag, 1 byte), plus one message per throw, drop or hard landing. Its motion goes through Switchboard's per-player byte budget whether it was thrown or knocked, so more throwables raise nothing past that ceiling; under load the least important bodies are sent less often.

## UI contract

What a HUD reads for items. All of it is events: nothing here needs checking every frame. Types are in `CloseTheDeal.Items`. "Owner" means only on the machine whose player it is; "every machine" means on the host and every client.

| HUD element | Read from | Type | Fires | Where |
|---|---|---|---|---|
| Which hands are mine | `ItemCarrier.LocalChanged`, `ItemCarrier.Local` | `Action<ItemCarrier>` (null when the player leaves) | When this client's player spawns or goes | This machine |
| The thing in reach, its name and verb | `ItemCarrier.PromptChanged` | `Action<PromptKind, ItemDefinition>`: `PickUp` ("F · Pick up " + `DisplayName`), `TooHeavy` (`DisplayName` + " · Too heavy"), `None` (item null) | When the look target, its kind, or the hands change; `None` while the menu is open, the hands are full, or another system claims F (briefcase, lobby desk) | Owner |
| The hands slot | `ItemCarrier.HeldChanged`, `ItemCarrier.Held` | `Action<Pickup>` (null = empty) | Taken, thrown, dropped, placed, knocked loose, used up | Every machine |
| What is in the slot | `Pickup.Definition` | `DisplayName`, `UseVerb` (empty = no right-mouse action), `Kind`, `WeightKg`; `Pickup.IsDerived` for a loose prop with no definition | Read when `HeldChanged` fires | Every machine |
| How heavy it is to carry | `ItemCarrier.Load` | `float` 0..1 (0 up to 3 kg, 1 at the 35 kg limit) | Changes only with `HeldChanged` | Every machine |
| Refusals, with reasons | `ItemCarrier.Denied` | `Action<DenyReason>`: `Gone`, `HandsFull`, `TooHeavy`, `TooFar`, `KnockedAbout`, `CannotUseHere`, `TooSoon` | Once per refused request | Owner |
| Losing a contested grab | `Denied` with `DenyReason.Gone` | as above | When the host gave it to someone else first | Owner |
| How it left the hands | `Pickup.Released` | `Action<Pickup, ReleaseKind>`: `Dropped`, `Thrown`, `Placed`, `Knocked` | Only a real let-go: a gun taken into the slots or a case that went off empties the hands through `HeldChanged` alone | Every machine |
| A throw landing | `Pickup.Impact` | `Action<Pickup, float>` impact speed, m/s | Each hard landing while thrown | Every machine |
| Gadget state | `GrenadeCase` Armed, Beeped, Exploded(point); `FireExtinguisher` PinPulled, RanEmpty; `WetFloorSign` PutDown, Cleared, Slipped(point) | C# events and UnityEvents | As named | Every machine |

**Refusal wording** (the voice is corporate and plain; the UI lane may restyle, not reword the reasons): `Gone` "Someone else got it", `HandsFull` "Hands full · G to drop", `TooHeavy` "Too heavy to lift", `TooFar` "Too far away", `KnockedAbout` (no text: the player is tumbling), `CannotUseHere` "No room to put it down here", `TooSoon` (no text: the player let go of it under 0.6 s ago and cannot snatch it straight back; anyone else can).

**Throw charge:** none. Every item has one throw strength (`Definition.ThrowSpeed`), so there is no charge to show. A throw arc, if the UI wants one, is drawn locally from the camera's forward, `ThrowSpeed`, the carrier's velocity × `CarryProfile.InheritVelocity`, gravity, and the release point `ItemCarrier.GetHoldPose(item, false, …)`: no synced state is needed.

**Keys:** Interact F / pad north, Throw left mouse / right trigger, Drop G / d-pad down, Use right mouse / left trigger (`ItemCarrier.CreateInput`, until they move to the shared actions asset).

**Sounds:** the world sounds (pick-up, throw, impact, beep, hiss) belong to the item and play on every machine from the events above. The HUD's own sounds use the UI lane's `UiSoundEvent`: `HandsFull` for a hands-full refusal, `Error` for the other refusals; a prompt appearing is a quiet `Focus`.

`ItemPromptLabel` is this lane's stand-in two lines of text for the test scene; the revamped HUD replaces it and reads exactly the rows above.

## Item sounds

Every sound an item makes in the world, as an event with its timing. All play at the item's position (3D) on every machine, from events each machine already receives; none is sent as sound. They play through `ItemSoundSet.Output`, the game mixer's **SFX** bus, so the settings' sound-effects volume applies. The set is `Profiles/Items/ItemSounds.asset`, linked from `DefaultCarry.asset`; an item definition may name its own set.

**Clips are made by the UI sound lane's generator** (`ArtSource/Audio/ui/make_ui_sounds.py`, the one place sounds are made) as `Assets/_Project/Audio/Items/Item_<Slot>.wav`. **Set Up Pickups** fills any empty slot from that file and the bus from `Assets/_Project/Audio/UI/GameMixer.mixer` once they exist; it never changes a slot someone set.

| Slot | Event | When and how long | Notes |
|---|---|---|---|
| `PickUp` | `Pickup.Taken` | Once, when the item reaches a player's hands | Short grab, about 0.15 s. Same for every item |
| `Drop` | `Pickup.Released` (Dropped, Placed, Knocked) | Once, at let-go | Not played when an item is used up (a gun into the slots, a case gone off) |
| `Throw` | `Pickup.Released` (Thrown) | Once, at let-go | Whoosh, about 0.2 s |
| `ImpactLight` / `ImpactHeavy` | `Pickup.Impact(speed)` | Each hard landing of a thrown item, at most once per contact | Heavy at 6 kg and up (`HeavyFromKg`). Volume rises with impact speed from 2 m/s to full at 14 m/s (`FullVolumeImpactSpeed`), never below 20% |
| `Arm` | `GrenadeCase.Armed` | Once, on right mouse | A click just before the first beep |
| `Beep` | `GrenadeCase.Beeped` | Fuse 3.5 s. First beep at arming; the gap shrinks evenly with the fuse burnt, from 0.6 s to 0.08 s; about 13 beeps in all | Must be under 0.06 s. Played on the case's own source, so it follows a thrown case |
| `Explosion` | `GrenadeCase.Exploded(point)` | Once, at the blast point | Long tail is fine (about 1 s) |
| `VentLoop` | `FireExtinguisher.PinPulled` to `RanEmpty` | Loops for exactly the vent time, 6 s, on the extinguisher's own source | Must loop without a click. Keeps playing after a drop: the gas still drives it round the floor until it is empty, by design |
| `VentEnd` | `FireExtinguisher.RanEmpty` | Once, when the loop stops | Last gasp, about 0.4 s |
| `WetFloorDown` | `WetFloorSign.PutDown` | Once, when the sign is set down | Plastic clack and a splash |
| `Slip` | `WetFloorSign.Slipped(point)` | Once per slip, at the runner | Squeak, about 0.3 s |
| (HUD) hands full | `ItemCarrier.Denied(HandsFull)` | Owner only | The UI's `UiSoundEvent.HandsFull`, on the UI bus, not this set |

**When the vent loop stops:** when it runs empty (6 s after the pin); when it is used up or comes back; and on every machine when the extinguisher is despawned (the towers rebuilt for a new match) or that machine leaves the game (`OnStopNetwork`). Dropping it, or its carrier dying or leaving, does not stop it: it keeps venting on the floor until empty. If a match ends without despawning the props, a venting extinguisher finishes its 6 s.

**No stand-ins:** the code-made tones are gone. An empty slot plays nothing. The twelve clips are the UI sound lane's, synthesised and measured; nobody has judged them by ear yet.

## Guns

`ItemCarrier.ServerItemTaken` fires on the host whenever a player takes any item, with the carrier and the pickup. In `Lane_Integration.unity`, `GunPickups` (Integration) listens: a gun goes into the weapon slots and the pickup is used up (guns never return). The rules (refill, free slot, swap) are in [INTEGRATION.md](INTEGRATION.md). Without that glue a gun is carried and thrown like any object.

## Hooks for sound, effects and UI

Every moment has a C# event and a UnityEvent on the prefab, firing on every machine unless marked:

- `Pickup`: Taken, Released (dropped / thrown / placed / knocked; not when it is used up), Impact (speed), Returned.
- `ItemCarrier`: HeldChanged, PromptChanged (owner), Denied (owner), `LocalChanged` (static), `ServerItemTaken` (host, static).
- Shared hands (static, set by the integration glue): `ItemCarrier.HandsTakenElsewhere` (true refuses an item as hands full, on the owner's prompt and key and on the host) and `InteractClaimedElsewhere` (owner: something else answers F this frame).
- `GrenadeCase`: Armed, Beeped, Exploded (point). `FireExtinguisher`: PinPulled, RanEmpty. `WetFloorSign`: PutDown, Cleared, Slipped (point).

Sounds: see "Item sounds" above. `ItemPromptLabel` is the lane's own two lines of on-screen text, in the lane's test scene and the integration scene until the revamped HUD replaces it.

## Scripts

Under [Assets/_Project/Scripts/Items/](../../Assets/_Project/Scripts/Items/):

- `ItemDefinition.cs`, `CarryProfile.cs`, `GrenadeCaseProfile.cs`, `ExtinguisherProfile.cs`, `WetFloorProfile.cs`: the tuning assets.
- `Pickup.cs`: on every item, next to `Prop`. Holder and out-of-play state, release, impact, the fall rule, and showing the item in the hands.
- `ItemCarrier.cs`: on `Player.prefab`. The lane's inputs, the look prompt, the requests to the host, the host's rules, dropping when knocked or leaving.
- `ItemBehaviour.cs`: base for what a particular item does. `GrenadeCase.cs`, `FireExtinguisher.cs`, `WetFloorSign.cs`.
- `LooseItemDefinition.cs`: the definition worked out for a loose prop that has none. `HandsCopy.cs`: the draw-only copy shown in the hands for a prop with no Visual child.
- `ItemBlast.cs`: damage in a radius. `MotorSpeedOverride.cs`: the carry slowdown. `ItemLayers.cs`: layer masks. `ItemSoundSet.cs`, `ItemAudio.cs`: the item sounds and how they play.
- `DamageDummy.cs`: the practice target. `ItemPromptLabel.cs`: the on-screen prompt.

Shared with other lanes: [Core/IDamageable.cs](../../Assets/_Project/Scripts/Core/IDamageable.cs).

Setup: **Close the Deal > Lanes > Set Up Pickups** ([PickupsLaneSetup.cs](../../Assets/_Project/Editor/Lanes/PickupsLaneSetup.cs)) creates any missing profile and item prefab (and swaps an older item prefab's NetworkTransform for a `NetBody`), adds `Pickup` to the greybox furniture prefabs and `ItemCarrier` to `Player.prefab` where missing, and builds `Assets/_Project/Scenes/Lanes/Lane_Pickups.unity`. That scene holds a real `TowerBuilder` whose profile (`Profiles/Items/LaneTowerFrame.asset`) stacks empty floors (`Prefabs/Items/LaneEmptyFloor.prefab`): Switchboard needs a built tower to stream props, and the hand-built office slice sits inside that tower's box. It never changes an asset or component that already exists. **Run Pickups Check** on the same menu plays that scene by script and writes a report and frames.

## Known limitations

- **Carry slowdown is a labelled workaround.** `PlayerMotor` has no public way to take a different movement profile and this lane could not edit it, so `MotorSpeedOverride` swaps a slowed runtime copy in through the motor's private `_profile` field. The real fix is one public method on `PlayerMotor`; then the reflection goes. If the field is renamed, carrying stops slowing players and an error says so once.
- **Death drops what is carried** only because a dead player is knocked for the whole wait and the knock drops it (`Drop When Knocked` on the carry profile). Untick that and death keeps the item in the hands.
- **Other players' carried items do not tilt** with where that player looks up or down: the host does not know a remote player's pitch. They see it level at chest height.
- **A carried panel is a moving wall on the host.** It pushes loose props and other players out of its way and cannot be pushed back. Whether that is fun or an exploit needs a playtest.
- **Smoke only blocks sight.** It is drawn per machine, so the exact puffs differ between players; the cloud's place and timing are the same.
- **Throwing has one strength per item.** No charge, no lob.
- Items are placed by hand in the lane scene. Putting them in the tower means a `PropMarker` per item in the floor templates, inside the per-floor cap.
- The sounds are generated stand-ins and there are no hand or arm visuals: the item floats in view.
