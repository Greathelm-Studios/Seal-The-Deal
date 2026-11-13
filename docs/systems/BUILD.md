# BUILD

**Status:** Current (branch `claude/lane-gaps-5e5605`, not merged) · **Last updated:** 2026-11-14

## What the player sees

A standalone game that boots straight into reception (`Lane_Integration.unity`, the whole game today), and a version line a menu can show: `v0.1.0 · build 52 · 3f2a9c1`. The first line of every log names the build, OS, CPU and GPU, so a sent `Player.log` says what it came from.

## Building

Menu: **Close the Deal > Build > Standalone (macOS)** or **(Windows)**, into `Builds/<platform>/` (gitignored).

Shell, Unity closed on that checkout:

```
/Applications/Unity/Hub/Editor/6000.4.8f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath <checkout> -executeMethod CloseTheDeal.Editor.Build.StandaloneBuild.BuildFromCommandLine -buildTarget mac -buildOut <folder> -logFile <folder>/build.log
```

`-buildTarget windows` for Windows; `-buildNumber N` to set the number (default: the commit count). Exit code 0 = built, 2 = failed. `<folder>/build_report.txt` has the size and time, or the reason it failed.

## Rules the script keeps

- **Changes no project setting.** The scene list goes to this build only; `EditorBuildSettings` (which lists only `Greybox.unity`) is left alone. The version is Player Settings' `bundleVersion`, which Steam lobbies compare on join, so it is never bumped by a build.
- **The stamp is written into the built player** (`StreamingAssets/build_info.json`), not into `Assets/`, so building never dirties the tree.
- A missing platform module or scene fails with that reason, not a Unity stack.

## Runtime contract

`BuildInfo` (namespace `CloseTheDeal.Build`), static: `Version`, `BuildNumber` (0 in the editor), `Commit`, `BuiltUtc`, `IsStamped`, `Display` (one short line for a menu corner). The UI kit's main menu shows `Display`; that is lane 5's to place.

## Known limitations

- **This Mac's Unity has only the macOS player.** A Windows build fails with "no StandaloneWindows64 player installed" until Windows Build Support is added in Unity Hub. Steam's audience is mostly on Windows.
- **The build carries the template identity:** product name `CloseTheDeal`, company `DefaultCompany`, the URP template's bundle id, and the Unity splash. They are Adam's to set (docs/GAPS_2026-11-14.md question 3).
- **No `steam_appid.txt` is put next to the player**: there is no app id yet, so a build runs in local test mode only.
- Unsigned and not notarised: macOS Gatekeeper will warn on another Mac.
