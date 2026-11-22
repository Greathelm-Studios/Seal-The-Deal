# BUILD

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

A standalone Seal the Deal build that boots straight into the kit office (`KitOffice.unity`), plus a version line in the menu corner (`v0.1.0 · build 52 · 3f2a9c1`). The first line of every log records the build, OS, CPU, and GPU so any submitted `Player.log` identifies its exact build.

## Ownership and netcode boundary

`BuildInfo` is purely local client/editor metadata. Player Settings' `bundleVersion` (`Application.version`) is compared by Steam lobbies on join, so builds never mutate it.

## Architecture and design rationale

- **No project settings mutated on build:** `StandaloneBuild` passes the scene list directly to the build pipeline while leaving `EditorBuildSettings` untouched (`KitOffice.unity` is also the editor build scene).
- **Non-dirtying build stamp:** Build metadata (`buildNumber`, `commit`, `builtUtc`, `target`) is written into the built player's `StreamingAssets/build_info.json`, never into `Assets/`.
- **Log-on-launch and domain-reload safety:** `BuildInfo` resets static caches under `RuntimeInitializeLoadType.SubsystemRegistration` and logs platform diagnostics on `BeforeSceneLoad`.

## Files and tuning

- [BuildInfo.cs](../../Assets/_Project/Scripts/Build/BuildInfo.cs): namespace `CloseTheDeal.Build`, static API (`Version`, `BuildNumber`, `Commit`, `BuiltUtc`, `IsStamped`, `Display`).
- Editor build menu: **Seal the Deal > Build > Standalone (macOS)** or **(Windows)** into `Builds/<platform>/` (gitignored), or CLI via `-executeMethod CloseTheDeal.Editor.Build.StandaloneBuild.BuildFromCommandLine -playerTarget <mac|windows> -buildOut <folder>`.

## Known limitations

- On a macOS host without Windows Build Support installed, Windows standalone builds fail until that Unity Hub module is installed.
- Player Settings still carries template identity (`CloseTheDeal` product name, `DefaultCompany`, URP bundle ID, Unity splash) pending owner sign-off.
- No `steam_appid.txt` is placed alongside the player yet, so standalone builds run in local test mode only.
- Builds are unsigned and un-notarised, so macOS Gatekeeper warns on other machines.
