# Seal the Deal Features and System Docs

**Status:** Current · **Last updated:** 2026-11-21

Master index of every gameplay, networking, UI, art, and tooling feature in **Seal the Deal**, linking each feature to its owning system doc, code folder, current verification state, and open gaps. For design scope and roadmap rules, see [GDD.md](GDD.md); for technical decisions, see [DECISIONS.md](DECISIONS.md); for active branch claims and release risks, see [WORKBOARD.md](WORKBOARD.md).

## Core Match and Towers

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Briefcase match loop (countdown, live, overtime, sudden death) | v1 | Current | Compiled; host tested in integration check | [MATCH.md](systems/MATCH.md) | `Scripts/Match/` | Needs live 2v2 and 4v4 Steam playtest; GDD core loop still reflects old roof race |
| Floor gates and tower crossings (skybridges) | v1 | Current | Compiled; host tested in integration check | [MATCH.md](systems/MATCH.md) | `Scripts/Match/` | Crossing art and placement pass |
| Seeded twin-tower generation and static batching | v1 | Current | Compiled; host tested | [TOWER.md](systems/TOWER.md) | `Scripts/Tower/` | Replace greybox floor templates with dressed KitOffice floors |
| Team spawn assignment and reception integration | v1 | Current | Compiled; host tested | [INTEGRATION.md](systems/INTEGRATION.md) | `Scripts/Integration/` | Confirm starting loadout and drop/interact rules with Adam |

## Player, Movement and Viewmodel

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Predicted movement (walk, sprint, crouch, slide, lean, mantle) | v1 | Current | Compiled; solo scripted checks | [PLAYER_MOVEMENT.md](systems/PLAYER_MOVEMENT.md) | `Scripts/Player/` | Confirm feel and 8-player reconcile bandwidth in live 4v4 |
| Active ragdoll chasing animation clips (`RagdollWorld`) | v1 | Current | Compiled; solo frame strips | [CHARACTER_ANIMATION.md](systems/CHARACTER_ANIMATION.md) | `Scripts/Player/` | Needs live playtest; Kevin Iglesias clips need licence/replacement decision |
| True-POV first-person arms and held weapon (`PlayerViewmodel`) | v1 | Current | Compiled; solo F5 polish; pistol grip hand seen in play as host, standing (2026-11-21) | [VIEWMODEL.md](systems/VIEWMODEL.md) | `Scripts/Player/Viewmodel/` | `Player.prefab` still runs `WeaponView` alongside `PlayerViewmodel` (needs Adam's call); left hand still the crumpling mitten |
| Head-mounted animated eye cards | v1 | Current | Compiled; solo checked | [CHARACTER_EYES.md](systems/CHARACTER_EYES.md) | `Scripts/Player/` | Needs visual check in multiplayer |
| Hit flinch reactions and test dummy/bench | v1 | Current | Compiled; tested on dummy | [HIT_REACTIONS.md](systems/HIT_REACTIONS.md) | `Scripts/Player/` | Player knockdown rules are Target; `PlayerHits` stand-in RPC gated to dev/editor |

## Combat, Glass and Effects

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Hitscan guns (pistol, AR-15), bloom, reload, lag rewind | v1 | Current | Compiled; solo/host lane checks | [WEAPONS.md](systems/WEAPONS.md) | `Scripts/Combat/` | Unify `WeaponView` with `PlayerViewmodel`; decide friendly fire on/off |
| Knockback blasts and health/respawn | v1 | Current | Compiled; host checks | [WEAPONS.md](systems/WEAPONS.md) | `Scripts/Combat/` | Confirm blast damage/knockback balance in 4v4 |
| Breakable glass panes and shard bursts | v1 | Current | Compiled; host lane checks | [GLASS.md](systems/GLASS.md) | `Scripts/Glass/` | Needs multi-client shatter test after `OverlapSphere` blast fix |
| Local blood splat projector shader and drop bursts | v1 | Current | Compiled; host seen on 1 machine | [BLOOD.md](systems/BLOOD.md) | `Scripts/Effects/` | Needs art-direction sign-off and multi-player visual check |

## Items, Props and Lift

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Pickups, carrying, throwing, and loose furniture | v1 | Current | Compiled; solo/host lane checks | [ITEMS.md](systems/ITEMS.md) | `Scripts/Items/` | Retire `DevCarry` once unified; test Switchboard `ForceSync` from client |
| Gadgets (Grenade Case, Fire Extinguisher, Wet Floor Sign, Panel) | v1 | Current | Compiled; solo/host lane checks | [ITEMS.md](systems/ITEMS.md) | `Scripts/Items/` | Needs 2v2/4v4 client verification |
| Physics prop spawning, 16/floor cap, and far-floor freezing | v1 | Current | Compiled only after freezing pass | [PROPS.md](systems/PROPS.md) | `Scripts/Props/` | Needs live 4v4 prop physics + Switchboard test |
| Deterministic networked elevator (`KitOffice`) | Roadmap / Kit | Target | Compiled; host script test | [ELEVATOR.md](systems/ELEVATOR.md) | `Scripts/Lift/` | Not wired into match towers or floor gates yet |

## Networking, Switchboard and Voice

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| FishNet + FishySteamworks P2P lobby and transport | v1 | Current | Compiled; local test | [NETCODE.md](systems/NETCODE.md) | `Scripts/Net/`, `Plugins/FishySteamworks/` | `NET-2` open: host accepts any Steam P2P connection without lobby/version auth |
| Switchboard budgeted prop streaming | v1 | Target | Compiled only | [SWITCHBOARD.md](systems/SWITCHBOARD.md) | `Scripts/Net/Switchboard/` | Never tested from a remote client over Steam |
| Proximity voice chat (Opus + RNNoise + lock-free speaker) | v1 (added) | Target | Compiled; early v1 heard solo | [VOICE.md](systems/VOICE.md) | `Scripts/Voice/` | Opus/RNNoise build unheard; needs 2+ player Steam test, credits/licences, team/dead talk calls |
| Performance budgets and 8-player scaling guards | v1 | Current | Static audit + compile only | [PERFORMANCE.md](systems/PERFORMANCE.md) | Cross-cutting | No 4v4 or min-spec profiler measurements yet |

## Lobby, Customisation and Meta

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Reception lobby, firm sign-in, ready board, host kick | v1 | Current | Compiled; lane checked | [LOBBY.md](systems/LOBBY.md) | `Scripts/Lobby/` | Needs live Steam lobby soak test |
| Fitting room and 8-byte synced outfits | v1 | Current | Compiled; lane checked | [CUSTOMISATION.md](systems/CUSTOMISATION.md) | `Scripts/Customisation/` | Confirm unlock thresholds and cosmetic catalogue |
| Match records, death card, and background profile saves | v1 | Current | Compiled; lane checked | [RECORDS.md](systems/RECORDS.md), [PROFILE.md](systems/PROFILE.md) | `Scripts/Records/`, `Scripts/Profile/` | Verify background file flush on quit |
| Teammate nameplates and marked-raider tags | v1 | Current | Compiled; lane checked | [NAMEPLATES.md](systems/NAMEPLATES.md) | `Scripts/Nameplates/` | Verify readability across tower gap |
| Secretary narration subtitles and first-run hints | v1 | Current | Compiled; lane checked | [NARRATION.md](systems/NARRATION.md), [ONBOARDING.md](systems/ONBOARDING.md) | `Scripts/Narration/`, `Scripts/Onboarding/` | Needs voice/audio assets for secretary lines |

## UI, HUD and Audio

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| UI Kit screens, settings, focus frame, and sub-canvases | v1 | Current | Compiled; preview captures | [UI_KIT.md](systems/UI_KIT.md) | `Scripts/UI/Kit/` | Rebuild `SettingsScreen.prefab` from merged builder after branch merges |
| UI glass blur render feature and backdrop | v1 | Current | Compiled; preview captures | [UI_GLASS.md](systems/UI_GLASS.md) | `UI/Glass/` | Check blur cost on min-spec GPU |
| Live-sky title screen and screen transitions | v1 | Current | Compiled; headless check | [UI_TITLE.md](systems/UI_TITLE.md) | `Scripts/UI/Title/` | Not yet played by a person on gamepad or client |
| UI sound set, join/leave/tip cues, and audio meter | v1 | Current | Compiled; Adam's chosen sounds landed on main | [UI_SOUND.md](systems/UI_SOUND.md) | `Scripts/UI/Sound/` | Verify final levels in live 2v2 |
| Spatial audio system, Steam Audio HRTF, and wall/floor occlusion | v1 | Current | Compiled only (unheard) | [AUDIO.md](systems/AUDIO.md) | `Scripts/Audio/`, `Plugins/SteamAudio/` | Needs headphone listening pass for voice HRTF + occlusion cutoffs; ship Apache-2.0 licence notice |
| In-game HUD, menu, and prediction debug overlay | v1 | Current | Compiled; preview captures | [MENU_AND_HUD.md](systems/MENU_AND_HUD.md) | `Scripts/UI/` | Confirm final HUD kit parity on `Lane_Integration` |

## Art, Lighting and Build

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Procedural office sky shader | v1 | Current | Compiled; visually inspected | [SKY.md](systems/SKY.md) | `Art/Sky/` | Frame cost unmeasured |
| Character cel/toon shader and outline baking | v1 | Current | Compiled; visually inspected | [CHARACTER_SHADER.md](systems/CHARACTER_SHADER.md) | `Art/Toon/`, `Editor/Art/` | Lock final thresholds and team cue on body |
| Two-tier floor reflection probes and bounce lighting | v1 | Current | Compiled; KitOffice inspected | [FLOOR_REFLECTIONS.md](systems/FLOOR_REFLECTIONS.md) | `Editor/Art/` | Not yet baked into match tower templates |
| Office look pass, animated kit shaders, and kit builders | v1 | Current | Compiled; KitOffice inspected | [OFFICE_LOOK.md](systems/OFFICE_LOOK.md) | `Scripts/Art/`, `Editor/Art/` | Settle third-party pack clearances (Kenney, MrEliptik) |
| Build info and standalone build tool | v1 | Current | Mac build reported; Windows untested | [BUILD.md](systems/BUILD.md) | `Scripts/Build/`, `Editor/Build/` | Needs Windows player build, Steam app id, credits/licences screen |
