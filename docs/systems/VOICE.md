# Voice

**Status:** Target (runs for one player and the first version was heard by Harvey; **the Opus and RNNoise version has not been listened to, and nothing has been tried with two players**) · **Last updated:** 2026-11-21

## What the player hears

Players talk to each other by voice, and a voice comes from the speaker's head. It is at full volume within 3 m, fades in a straight line and is gone at 24 m, and pans left and right as the speaker moves. Both teams hear each other: there are no team channels. The settings screen's **Voice** slider sets how loud everyone is.

The microphone is open from the start and sends only when the player is speaking. **V** mutes and unmutes it. In push-to-talk mode the microphone is open only while **V** is held. V can be rebound under Settings, Controls (**Mute / Push to talk**).

Keyboard, fan and room noise is taken out of a voice before it is sent.

**Settings, Audio** has four voice rows under the volumes:

| Row | What it does |
|---|---|
| Microphone | Which microphone to record from. **System default** follows Windows. A saved microphone that is unplugged falls back to the default and comes back when it is plugged in again |
| Microphone mode | **Open microphone**, **Push to talk** or **Off** |
| Noise suppression | On by default |
| Hear my own voice | A mic test: plays the player's voice back flat, after the same cleaning and compression other players get. It works on the title screen with no game running, is not saved, and switches off when the tab is left |

The capture, packets, proximity relay, and playback are Seal the Deal's own code. Compression is Opus (Concentus) and noise removal is RNNoise, both shipped in `Assets/Plugins/`. It needs no Steam and no external voice service, so it works in local test mode too. A player with no microphone hears everyone and is not heard.

## Who owns what (AGENTS.md §3)

| Thing | Owner |
|---|---|
| Recording, cleaning and compressing a player's voice | That player's machine |
| Who hears a voice | **Host.** Passes a voice on only to players whose body is within 28 m of the speaker's body (and caps each sender at 8,000 bytes/s) |
| Where a voice plays from, and its fade | Each listening machine, from the speaker's body |
| Microphone, mode, noise suppression and mute | The speaker's machine only (`PlayerPrefs`), never sent |

Why the host decides who hears: in a host-authoritative game, sending every voice to every client would let a modified client listen across the whole map regardless of its local volume curve. The 28 m `RelayRange` gives 4 m of margin outside the 24 m `HearRange` so sound is already arriving when a listener steps into fade range.

Nothing is predicted or reconciled and no gameplay reads voice. A player with no body (not yet spawned) is not heard and hears nobody.

## Architecture and rationale

### Self-creation and lifecycle

`VoiceChat` creates itself once at `RuntimeInitializeLoadType.BeforeSceneLoad` (`DontDestroyOnLoad`). Because voice has no authored Inspector values and must exist in every scene with a `NetworkManager`, no scene or prefab holds a reference to it. When `_net` becomes null across a scene change, `VoiceChat` re-hooks `ClientManager` and `ServerManager` broadcasts automatically.

### Microphone capture, resampling, and continuous cleaning

1. **Continuous recording:** While connected (and mode is not `Off`) or during the mic test, `VoiceChat` keeps `Microphone.Start` running continuously and discards frames when muted or idle. Starting a Unity microphone recording on demand takes a noticeable moment, which would swallow the first word after a push-to-talk press.
2. **Stall recovery:** If a frame stall causes more than half of the 1 s looping buffer to accumulate, `VoiceChat` skips ahead to the newest 20 ms frame so delayed audio never builds up.
3. **Downmix and continuous resampling:** `VoiceChat` targets 48 kHz (`SampleRate = 48000`, 20 ms = 960 samples). Multi-channel mics are averaged to mono, and devices that do not run at 48 kHz are linearly resampled to 48 kHz while carrying the previous frame's final sample across boundaries (`_prevMicSample`).
4. **Continuous RNNoise cleaning:** `VoiceCleaner` (64-bit Windows native `rnnoise.dll`) processes 10 ms (480-sample) chunks scaled to 16-bit PCM range (`32768f`). It runs even when nothing will be sent so RNNoise continuously adapts to room noise rather than starting deaf on every press.
5. **Speech gate and Opus encoding:** With open mic, a piece sends when RNNoise's speech probability meets `SpeechChance` (0.5) and RMS meets `SilenceLevel` (0.001), or RMS meets `GateLevel` (0.012) when RNNoise is off or unavailable; the gate stays open `GateHold` (0.35 s) to keep word endings. Held push-to-talk sends everything. After 0.5 s of silence (`EncoderForget`), the Opus encoder resets so prior speech does not colour the next utterance.
6. **Codec wrapper (`VoiceCodec`):** Isolates `Concentus` behind `VoiceEncoder` and `VoiceDecoder` (48 kHz mono, `OPUS_APPLICATION_VOIP`, `OPUS_SIGNAL_VOICE`, 32 kbps, inband FEC at 10% expected loss, complexity 5 so pure-C# Concentus encodes in ~1 ms and decodes in ~0.26 ms).

### Host relay and packet loss recovery

1. Each `VoiceFrame` (unreliable) begins with a 2-byte wrapping 16-bit sequence number followed by one Opus packet (~82 bytes, ~50/s = ~4.1 KB/s while speaking).
2. On the host, `OnFrame` validates packet size (`<= MaxPacketBytes` 400), refills the sender's lazy token bucket (`BytesPerSecondCap = 8000` bytes/s), and broadcasts a single `VoiceRelay` (speaker client id + bytes) to authenticated players within `RelayRange` (28 m) whose body exists.
3. Each listener rejects out-of-order or duplicate sequence numbers (`(short)(number - lastNumber) <= 0`), resets the decoder after 1 s of silence (`NumberForget`), and advances `LastNumber` only after Opus decodes cleanly. Gaps of 1 to 3 (`MaxFill`) lost frames are concealed: earlier missing frames use `Decoder.Conceal`, and the last missing frame before the arrived packet is rebuilt from inband FEC (`previous: true`).

### Low-delay 3D playback (`VoiceSpeaker`)

1. **Carrier clip + `OnAudioFilterRead` instead of streamed `AudioClip`:** Unity reads streamed `AudioClip`s far ahead of the playhead (measured at 0.8 s delay on 16 kHz audio). Instead, `VoiceSpeaker` plays a shared looping 1024-sample all-ones `AudioClip` (`GetSharedCarrier`) on a 3D `AudioSource` (about 20 ms ahead of output) and multiplies the spatially placed output by the voice samples inside `OnAudioFilterRead`.
2. **Custom linear rolloff:** `AudioSource` uses a custom `AnimationCurve` from 1.0 within 3 m (`FullRange`) down to 0.0 at 24 m (`HearRange`), because Unity's default logarithmic curve never reaches zero.
3. **Lock-free SPSC ring buffer and output resampling:** `VoiceSpeaker` uses a power-of-2 ring buffer (`RingCapacity = 16384`, ~341 ms at 48 kHz) synchronized with `Volatile.Read` / `Volatile.Write` between the main thread (`Push`) and audio thread (`OnAudioFilterRead`), publishing `_step` and `_ring` before `_source.Play()`. It waits for `StartDelay` (60 ms) before starting playback to absorb arrival jitter, caps buffered audio at `MaxDelay` (300 ms) by advancing the read cursor if behind, linearly resamples from 48 kHz to `AudioSettings.outputSampleRate` (updating `_step` and the carrier clip on `AudioSettings.OnAudioConfigurationChanged`), and pauses the `AudioSource` after 0.5 s of empty buffer (`PauseSilenceSeconds`).

## Scripts

- [VoiceChat.cs](../../Assets/_Project/Scripts/Voice/VoiceChat.cs): Capture, resampling, gating, host relay, packet sequencing, and speaker tracking (`PlayerMotor.All` and `Health.All` lookup).
- [VoiceCodec.cs](../../Assets/_Project/Scripts/Voice/VoiceCodec.cs): `VoiceEncoder` and `VoiceDecoder`, wrapping Concentus Opus.
- [VoiceCleaner.cs](../../Assets/_Project/Scripts/Voice/VoiceCleaner.cs): P/Invoke wrapper for `rnnoise.dll`.
- [VoiceSpeaker.cs](../../Assets/_Project/Scripts/Voice/VoiceSpeaker.cs): Lock-free ring buffer, 3D carrier `AudioSource`, `OnAudioFilterRead` playback, and speaking detection.
- [VoiceSettings.cs](../../Assets/_Project/Scripts/Voice/VoiceSettings.cs): Local `PlayerPrefs` choices (`Mode`, `Device`, `NoiseSuppression`) plus transient `Muted` and `HearSelf`.
- [AudioPage.cs](../../Assets/_Project/Scripts/UI/Kit/AudioPage.cs) and [UiKitSettingsBuilder.cs](../../Assets/_Project/Editor/UI/UiKitSettingsBuilder.cs): The four Audio tab settings rows.
- [InputSystem_Actions.inputactions](../../Assets/InputSystem_Actions.inputactions): `Player/Voice` action (default V).

For UI work: `VoiceChat.Instance.IsSpeaking(clientId)`, `.MicOpen`, `.Transmitting`, `.Available`, `.Devices`, `.RefreshDevices()`, `.CanSuppressNoise`, and `VoiceSettings.Changed`.

## Libraries

Both are vendor code under `Assets/Plugins/` and are never edited in place:

| Library | What | Where it came from | Licence |
|---|---|---|---|
| Concentus 2.2.2 | Opus in plain C#, one DLL, runs on every platform | `netstandard2.0` build from NuGet | BSD 3-clause, `Assets/Plugins/Concentus/LICENSE.txt` |
| RNNoise 0.1.1 | Noise suppression, native DLL for 64-bit Windows only | Built from Xiph's source, `Assets/Plugins/RNNoise/README.md` | BSD 3-clause, `Assets/Plugins/RNNoise/LICENSE.txt` |

## Tuning

Ranges, bitrate, gate thresholds, buffer delays, and the host cap are named constants at the top of `VoiceChat.cs` and `VoiceSpeaker.cs` because `VoiceChat` is created at startup without a prefab or ScriptableObject.

## Checked

2026-11-15, in the Editor, in `Lane_Integration.unity`:

- Compiled cleanly with no voice console errors.
- **Delay:** Measured 10 to 18 ms across six test-tone runs (compared to ~0.8 s on the initial streamed-clip prototype).
- **Distance and direction:** Flat test tone peaked at 0.25; 2 m right peaked at 0.50 in the right ear; 13.5 m peaked at 0.25; 30 m read 0.00.
- **Opus:** 84 bytes/frame average on a test tone, ~1.0 ms encode and ~0.26 ms decode; invalid packets decode to 0 and lost frames conceal.
- **RNNoise:** Steady white noise attenuated by 3.8 dB and stayed below `SpeechChance` (0.44 max vs 0.5 threshold).
- **Mic test & settings UI:** Title screen mic test recorded at 48 kHz and stayed silent in a quiet room; all four Audio settings rows updated `VoiceSettings` properly.

**Not checked:** Nobody has listened to live speech through the Opus + RNNoise build yet, and nothing has been tested with a second player, over Steam, or in a standalone build (including verifying `rnnoise.dll` loads in a build).

## Known limitations

- **No speaking indicator in HUD or world.** `IsSpeaking(clientId)` and `Transmitting` exist for UI to read, but no icon is drawn yet.
- **Licence notices not yet in-game.** Opus and RNNoise BSD 3-clause notices must ship in a credits screen or text file beside the release build.
- **Noise suppression is Windows 64-bit only.** Other platforms fall back to the loudness gate (`GateLevel`).
- **Spatial audio and occlusion** are handled by [AUDIO.md](AUDIO.md) (`SoundSystem`, Steam Audio HRTF with `spatializePostEffects = true`, `SpatialOcclusion` wall/floor muffling, and fallback `AudioListener` in scenes like `KitOffice.unity`). Not yet listened to by a human on headphones.
- **Dead and waiting players** are treated like any player with a body (design question for Adam).
- **Both teams hear each other.** No team-only or radio channel.
- **No per-player mute** and no Steam block-list check.
- **Host relays all voices.** 8 players talking in one room costs up to ~8 x 7 x 4.1 KB/s host upload (not measured).
- **No acoustic echo cancellation.** Speakers with an open mic will feed other players' voices back.
- **Microphone driver latency** in Unity's recording buffer cannot be removed in script (not measured).
- **Audio tab lacks a "Changed on this tab" indicator** unlike Controls.
