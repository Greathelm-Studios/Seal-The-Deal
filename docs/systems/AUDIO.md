# Spatial Audio and Sound System

**Status:** Current (compiled; not yet listened to by a human in Play Mode or multiplayer) · **Last updated:** 2026-11-22

## What the player hears

Every 3D world sound (proximity voice chat, gunshots, bullet impacts, shattering glass, thrown items, gadget loops, and other players' footsteps) is placed in 3D space using **Steam Audio HRTF** binaural spatialisation, so players on headphones can tell whether a voice or gunshot is in front, behind, above, or below them.

Walls, concrete floors, and unbroken glass windows muffle 3D sounds behind them using a smooth low-pass filter and volume drop. When a window shatters or a speaker steps into line of sight, the sound opens back up smoothly. Local feedback (your own footsteps, hurt/death/respawn cues, UI sounds, and the mic self-test) stays flat 2D.

## Ownership and netcode boundary (AGENTS.md §3)

Spatial audio, voice pooling, HRTF, and wall/floor occlusion are purely local client-side presentation. Nothing in `CloseTheDeal.Audio` sends network traffic or writes gameplay state.

## Architecture and rationale

```
CombatSounds / ItemAudio / MovementSounds / VoiceSpeaker
        │
        ▼
  SoundSystem (DontDestroyOnLoad, BeforeSceneLoad)
   ├── Shared 3D one-shot pool (32 voices, priority + distance stealing, zero-reaching rolloff)
   ├── Shared 2D flat one-shot source (local body feedback)
   ├── Fallback AudioListener (only enabled when a scene has no active listener)
   ├── Cached GameMixer.mixer group lookup (Master / SFX / Voice / Music / UI)
   ├── SteamAudioSource attachment (binaural HRTF; Bilinear for voices, Nearest for SFX)
   └── SpatialOcclusion (zero-alloc Physics.RaycastNonAlloc -> AudioLowPassFilter + gain)
```

### Why one central SoundSystem

Previously, `CombatSounds`, `ItemAudio`, `MovementSounds`, and `VoiceSpeaker` each rolled their own `AudioSource` pools, listener lookups, and rolloff curves (`Logarithmic` never reached zero, and ~95 total `AudioSource`s competed for Unity's default 32 real voices). `SoundSystem` unifies them under one pool, one zero-reaching custom rolloff curve (cached by quantized near/far distance so hot paths allocate nothing), one listener tracker, and raises `ProjectSettings/AudioManager.asset` `m_RealVoiceCount` to 64.

### Voice priority budget

| Priority (lower = higher) | Source |
|---|---|
| 32 | Proximity voice chat (`VoiceSpeaker`) — never stolen by SFX |
| 64 | Combat shots, impacts, glass shatter (`CombatSounds`) and local 2D flat feedback |
| 96 | Persistent gadget loops and beeps (`FireExtinguisher`, `GrenadeCase`) |
| 112 | Item one-shots (pickups, drops, throws, landings) |
| 144 | Other players' footsteps, jumps, landings, and slides (`MovementSounds`) |
| 200 | Menu ambient bed (`UiSoundPlayer`) |

### Steam Audio HRTF with `VoiceSpeaker`

`ProjectSettings/AudioManager.asset` selects **Steam Audio Spatializer** and **Steam Audio Ambisonic Decoder**. `SoundSystem` attaches `SteamAudioSource` (`directBinaural = true`) to every 3D pooled and persistent source.

For `VoiceSpeaker`, `AudioSource.spatializePostEffects` is set to `true` so `OnAudioFilterRead` produces the decoded Opus voice waveform **before** Steam Audio convolves it with the HRTF impulse response. `VoiceSpeaker` uses `HRTFInterpolation.Bilinear` for smooth panning as speakers move; pooled one-shots use `HRTFInterpolation.Nearest` for lower CPU cost.

### Wall and floor occlusion (`SpatialOcclusion`)

Because match towers are procedurally assembled from floor templates and glass panes shatter at runtime, occlusion uses Unity's live physics colliders instead of baked Steam Audio static meshes:

- **Layer mask:** excludes `Player`, `Prop`, `Viewmodel`, `Ignore Raycast`, `UI`, and `TransparentFX`, and ignores triggers (`QueryTriggerInteraction.Ignore`). Hits walls, floors, ceilings, closed doors, and unbroken `GlassPane` colliders.
- **Zero-allocation barrier count:** `Physics.RaycastNonAlloc` into a shared 8-hit buffer, sorted in place; hits separated by at least `MinBarrierSeparation` (0.25 m) count as distinct barriers so two sides of one wall never double-count.
- **One-shots:** sampled once at spawn on the pooled voice's `AudioLowPassFilter` and volume scale.
- **Persistent 3D sources** (`VoiceSpeaker`, `MovementSounds`, `FireExtinguisher`, `GrenadeCase`): staggered at 12 Hz per source, smoothly interpolating cutoff (`22000 Hz` clear -> `1600 Hz` 1 wall -> `650 Hz` 2+ walls) and gain (`1.0` -> `0.60` -> `0.28`). When clear (`>= 21500 Hz`), the `AudioLowPassFilter` is disabled for zero DSP cost and zero coloration.
- **Component order on `VoiceSpeaker`:** `AudioSource` -> `VoiceSpeaker` (`OnAudioFilterRead`) -> `AudioLowPassFilter` -> `SpatialOcclusion`, so the low-pass filter muffles the real decoded voice rather than the DC carrier clip.

### Fallback `AudioListener`

Scenes without a camera listener (`KitOffice.unity`, `Lane_Lobby.unity`, `Lane_Title.unity`) automatically activate `SoundSystem`'s fallback `AudioListener` (following `Camera.main`) and disable it the moment any real scene listener becomes active, calling `SteamAudioManager.NotifyAudioListenerChanged()`.

## Scripts

- [SoundSystem.cs](../../Assets/_Project/Scripts/Audio/SoundSystem.cs): Singleton manager, pooled 3D/2D playback, cached mixer lookup, Steam Audio setup, and fallback listener.
- [SpatialOcclusion.cs](../../Assets/_Project/Scripts/Audio/SpatialOcclusion.cs): Zero-allocation raycast occlusion and `AudioLowPassFilter` smoothing for persistent and one-shot sources.
- [SpatialAudioProfile.cs](../../Assets/_Project/Scripts/Audio/SpatialAudioProfile.cs): Designer-owned tuning asset (optional; defaults apply if no asset exists).
- [SpatialAudioProfileMenu.cs](../../Assets/_Project/Editor/Audio/SpatialAudioProfileMenu.cs): Menu **Seal the Deal > Audio > Create Spatial Audio Profile** (`Assets/_Project/Resources/Audio/SpatialAudioProfile.asset`).

## Libraries

| Library | What | Platforms included | Licence |
|---|---|---|---|
| Steam Audio 4.8.1 | Valve HRTF binaural spatializer (`phonon`, `audioplugin_phonon`) | Windows x86_64 and macOS (`Assets/Plugins/SteamAudio/Binaries/`) | Apache 2.0, `Assets/Plugins/SteamAudio/LICENSE.md` and `THIRDPARTY.md` |

Unused platform binaries (Windows x86, Linux, Android, iOS, HTML5, TrueAudio Next) were omitted to keep the repository lean.

## Tuning

Run **Seal the Deal > Audio > Create Spatial Audio Profile** to create `Assets/_Project/Resources/Audio/SpatialAudioProfile.asset` and adjust pool size, occlusion cutoffs/gains, smoothing time, check rate, or toggle Steam Audio HRTF / bilinear voice interpolation in the Inspector.

## Known limitations

- **Not yet listened to in Play Mode or multiplayer.** Verified by headless Unity compile check only; HRTF balance, voice `spatializePostEffects` clarity, and occlusion cutoff frequencies need a listening pass on headphones.
- **Licence notice not yet in-game.** Steam Audio's Apache 2.0 notice (`LICENSE.md` and `THIRDPARTY.md`) must ship in the credits screen or beside the build before release.
- **No room reverb or diffraction.** Occlusion uses direct line-of-sight raycasts, not multi-path diffraction around doorways or baked office reverb.
- **Windows x64 and macOS only.** Linux/Steam Deck builds would need `libphonon.so` and `libaudioplugin_phonon.so` extracted from `steamaudio_unity_4.8.1.zip`.
