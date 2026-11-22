---
name: driving-unity-from-the-cli
description: An agent can drive the open Unity Editor, and a development build, with the Unity CLI; what works and the traps
metadata:
  type: reference
---

An agent can compile, run editor tools, enter Play Mode, read the console and take Game view screenshots in the Editor a person already has open, with the Unity CLI (`unity status`, `unity recompile`, `unity command eval --file x.cs`, `unity command start` / `stop`, `unity command game_view --output-path x.png`, `unity command console.get`). The project has the `com.unity.pipeline` package this needs. The batch-mode compile in AGENTS.md §11 cannot run while an Editor has the project open; this can. Found on 2026-11-22 while building the floor race.

**How to apply:**

- Check `unity command list_open` first: don't replace a scene with unsaved changes.
- A second player for a "seen from a client" check: build a development player (`unity command build.run` with `--scenes` and `--options` as JSON arrays), start it with `-local`, and drive it with `--runtime SealTheDeal`. That needs `settings.runtime_pipeline.set '{"enable-in-development-builds": true}'` before the build; it creates `Assets/Settings/Pipeline/` and changes `ProjectSettings.asset`, so don't commit those and set it back after.
- In the player, `eval` cannot touch members of a `NetworkBehaviour` type (FishNet is not referenced by the script compiler there). Read plain objects and structs, or go through reflection. Take client screenshots with `UnityEngine.ScreenCapture.CaptureScreenshot`.
- In the Editor, Steam is not available, so `SteamLobby` is already in local mode; `lobby.Host()` on the host and `lobby.JoinLocal()` on the client connect the two over Tugboat.
- Play Mode writes to the real profile (`AppData/LocalLow/DefaultCompany/CloseTheDeal/profile.json`). Point it elsewhere for a scripted run with `SessionState.SetString(ProfileStore.EditorPathKey, path)` in the Editor and `-profileSave path` on a build.
- Building a player and running `OfficeLook.Apply` both rewrite files nobody asked to change (URP settings assets, the TMP font atlases, `OfficeLook_Volume.asset`). Stage explicit paths only (§9).
