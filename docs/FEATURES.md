# Seal the Deal Features and System Docs

**Status:** Current · **Last updated:** 2026-11-22

Master index of every gameplay, networking, UI, art, and tooling feature in **Seal the Deal**, linking each feature to its owning system doc, code folder, current verification state, and open gaps. For design scope and roadmap rules, see [GDD.md](GDD.md); for technical decisions, see [DECISIONS.md](DECISIONS.md); for active branch claims and release risks, see [WORKBOARD.md](WORKBOARD.md).

## Core Match and Towers

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Briefcase and match phases (countdown, running, result, rematch) shared by game modes | Main mode | Current | Compiled; played inside the floor race on the host and seen from a client before the tower code was removed, host only since | [MATCH.md](systems/MATCH.md) | `Scripts/Match/` | Needs a real playtest |
| Floor race mode (one briefcase, hidden elevator each floor, 10 generated floors to the manager's office, team-wipe respawn, cash rewards) | Main mode | Current | Compiled; a full match driven by script on the host alone, then again with a development build as client on the same PC: a steal, a floor change, a wipe and respawn and the floor 10 win all seen from the client, both logs clean ([screenshots](proof/floor_race_2026-11-22/)); not played by a person, not over Steam, one player a firm | [FLOOR_RACE.md](systems/FLOOR_RACE.md) | `Scripts/FloorRace/`, `Scripts/Match/MatchRuleset.cs`, `Scenes/FloorRace.unity` | Needs a real playtest with full firms over Steam; cash is local and unspent; open rules listed in the doc; client check not repeated since the tower code was removed |
| Floor generator (seed and floor number to a KitOffice office plan: ring, named rooms with two exits, lifts, fair starts, authored spawn, loot and prop spots) | Main mode | Current | Compiled; 2,000 floors swept through its checks in the Editor; 16 layouts looked at as drawings; built at runtime in Editor Play Mode (8 to 12 ms a floor, about 3,000 draw calls); KitOffice-style ceilings, room and bounce lights, colour, signs, slogans, the ring band and desk dressing added 2026-11-24 and looked at in captures beside KitOffice; 5,217 spawn spots on 80 floors tested clear of furniture; a client build built the same floors as the host before the dressing, not since; not walked by hand, cost not measured in a build | [FLOOR_GEN.md](systems/FLOOR_GEN.md) | `Scripts/FloorGen/`, `Scripts/FloorRace/`, `Editor/FloorRace/` | Furniture patterns are a code table; walls only on the 4 m grid; no breakable glass; merging pieces needs Read/Write on the kit models |
| Gun pickups, shared hands and team seats | Main mode | Current | Compiled; gun pickups spawn in the floor race, not picked up in a check | [INTEGRATION.md](systems/INTEGRATION.md) | `Scripts/Integration/`, `Scripts/Tower/TeamSpawner.cs` | The reception lobby flow has no scene; INTEGRATION.md mostly describes the removed scene |

## Player, Movement and Viewmodel

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Predicted movement (walk, sprint, crouch, slide, lean, mantle) | v1 | Current | Compiled; solo scripted checks | [PLAYER_MOVEMENT.md](systems/PLAYER_MOVEMENT.md) | `Scripts/Player/` | Confirm feel and 8-player reconcile bandwidth in live 4v4 |
| Active ragdoll chasing animation clips (`RagdollWorld`) | v1 | Current | Compiled; solo frame strips | [CHARACTER_ANIMATION.md](systems/CHARACTER_ANIMATION.md) | `Scripts/Player/` | Needs live playtest; Kevin Iglesias clips need licence/replacement decision |
| True-POV first-person arms and held weapon (`PlayerViewmodel`) | v1 | Current | Compiled; solo F5 polish; pistol grip hand seen in play as host, standing (2026-11-21) | [VIEWMODEL.md](systems/VIEWMODEL.md) | `Scripts/Player/Viewmodel/` | `WeaponView` now drives it (kick, slide, reload) and no longer draws its own gun when it is present (2026-11-22, compiled, unplayed); `Equip` still not called, so the AR-15 shows as the pistol; left hand still the crumpling mitten |
| Head-mounted animated eye cards | v1 | Current | Compiled; solo checked | [CHARACTER_EYES.md](systems/CHARACTER_EYES.md) | `Scripts/Player/` | Needs visual check in multiplayer |
| Hit flinch reactions, death ragdoll, knocked-out stun and test dummy/bench | v1 | Current | Compiled; flinch tested on dummy; death/knockout compiled only | [HIT_REACTIONS.md](systems/HIT_REACTIONS.md) | `Scripts/Player/`, `Scripts/Combat/` | Needs live Play Mode and multi-client verification; confirm final knockout threshold/duration with Adam |

## Combat, Glass and Effects

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Hitscan guns (pistol, AR-15), bloom, reload, lag rewind | v1 | Current | Compiled; solo/host lane checks | [WEAPONS.md](systems/WEAPONS.md) | `Scripts/Combat/` | Soft muzzle flash, flying tracer, impact burst and bullet holes added 2026-11-22 (compiled, offscreen renders only, unplayed); decide friendly fire on/off |
| Knockback blasts, health, regeneration and respawn; weapon test scene | v1 | Current | Compiled; host checks; regeneration and the WeaponTest roof range seen in the editor as host (2026-11-22) | [WEAPONS.md](systems/WEAPONS.md) | `Scripts/Combat/`, `Editor/Dev/WeaponTestScene.cs` | Confirm blast damage/knockback balance in 4v4; regeneration is not in the GDD (Adam to confirm the 5 s wait and 10/s rate); regeneration and targets not yet seen from a client |
| Breakable glass panes and shard bursts | v1 | Current | Compiled; host lane checks; host-checked in KitOffice (shots and thrown bodies, every kit glass piece) | [GLASS.md](systems/GLASS.md) | `Scripts/Glass/` | Needs multi-client shatter test after `OverlapSphere` blast fix, and in KitOffice; kit frames stop blocking once their pane breaks |
| Local blood splat projector shader and drop bursts | v1 | Current | Compiled; host seen on 1 machine | [BLOOD.md](systems/BLOOD.md) | `Scripts/Effects/` | Needs art-direction sign-off and multi-player visual check |

## Items, Props and Lift

| Feature | Scope | Status | Verification | Doc | Code | Open Gaps / Decisions |
|---|---|---|---|---|---|---|
| Pickups, carrying, throwing, and loose furniture | v1 | Current | Compiled; solo/host lane checks | [ITEMS.md](systems/ITEMS.md) | `Scripts/Items/` | `DevCarry` now stands down when `ItemCarrier` is on the player (retire it once nothing needs it); gun pickups in KitOffice/Editor not compiled or played; test Switchboard `ForceSync` from client |
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
| Teammate nameplates | v1 | Current | Compiled; lane checked before the tower mode was removed | [NAMEPLATES.md](systems/NAMEPLATES.md) | `Scripts/Nameplates/` | Marked-raider tags are off (no home building in the floor race) |
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
