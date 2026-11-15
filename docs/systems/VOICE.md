<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-15
  Prompt    : Harvey: "Add VOIP/Proximity chat to the game.", then "Make our own custom VOIP for this."
  Modified  : Claude Fable 5.2, 2026-11-15. Opus, RNNoise, the low-delay playback, the microphone
              picker and the settings rows (Harvey: "The quality isnt great / We probably need noise
              suppression / There's a delay", then "do the delay and mic picker and do the settings in
              the settings menu", "go grab that", "Yes on the RNNoise").
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : How proximity voice chat works, who owns what, and what has and has not been checked.
-->
# VOICE

**Status:** Target (runs for one player and the mic test has been heard by Harvey in its first form; **the Opus and RNNoise version has not been listened to, and nothing has been tried with two players**) · **Last updated:** 2026-11-15

## What the player hears

Players talk to each other by voice, and a voice comes from the speaker's head. It is at full volume within 3 m, fades in a straight line and is gone at 24 m, and it pans left and right as the speaker moves. Both teams hear each other: there are no team channels. The settings screen's **Voice** slider sets how loud everyone is.

The microphone is open from the start and sends only when the player is speaking. **V** mutes and unmutes it. In push-to-talk mode the microphone is open only while **V** is held. V can be rebound under Settings, Controls (**Mute / Push to talk**).

Keyboard, fan and room noise is taken out of a voice before it is sent.

**Settings, Audio** has four voice rows under the volumes:

| Row | What it does |
|---|---|
| Microphone | Which microphone to record from. **System default** follows Windows. A saved microphone that is unplugged falls back to the default and comes back when it is plugged in again |
| Microphone mode | **Open microphone**, **Push to talk** or **Off** |
| Noise suppression | On by default |
| Hear my own voice | A mic test: plays the player's voice back flat, after the same cleaning and compression other players get. It works on the title screen with no game running, is not saved, and switches off when the tab is left |

The capture, the packets, who hears whom and the playback are our own code. The compression is Opus and the noise removal is RNNoise, both free libraries shipped in the project. It needs no Steam and no voice service, so it works in local test mode too. A player with no microphone hears everyone and is not heard.

## Who owns what

| Thing | Owner |
|---|---|
| Recording, cleaning and compressing a player's voice | That player's machine |
| Who hears a voice | The host. It passes a voice on only to players whose body is within 28 m of the speaker's body |
| How much voice a player may send | The host: 8,000 bytes a second each, more is dropped |
| Where a voice plays from, and its fade | Each listening machine, from the speaker's body |
| Microphone, mode, noise suppression and mute | The speaker's machine only (`PlayerPrefs`), never sent |

Nothing is predicted or reconciled and no gameplay reads voice. A player with no body (not yet spawned) is not heard and hears nobody.

## How a word travels

1. The speaker's machine records the chosen microphone at 48 kHz and cuts it into 20 ms pieces.
2. Each piece is cleaned by RNNoise, which also says how likely it is to be speech. With an open microphone only speech is sent, and the gate stays open 0.35 s after the last of it so the ends of words are kept. With noise suppression off, or where the RNNoise library is missing, the gate goes by loudness instead. A held push-to-talk key sends everything.
3. The piece is compressed with Opus at 32,000 bits a second and numbered. A packet is about 82 bytes, 50 a second: about 4.1 kB a second while someone talks, nothing while they are quiet. Each packet also carries a small spare copy of the piece before it.
4. It goes to the host as a `VoiceFrame` broadcast, unreliable.
5. The host checks its size and the sender's allowance, then sends a `VoiceRelay` (the speaker's client id and the same bytes) to each other player in range.
6. Each listener throws away a piece that arrives late or twice. If one to three pieces were lost, it fills the gap: the last lost piece is rebuilt from the spare copy in the packet that did arrive, and any before it are filled by carrying the voice on. Then the piece is decompressed and added to that speaker's buffer.
7. The buffer holds 60 ms before it starts, so uneven arrival does not crackle, and never more than 300 ms, so a voice cannot fall behind the mouth. The sound is heard about a fiftieth of a second after it leaves the buffer.

## Scripts

- [VoiceChat.cs](../../Assets/_Project/Scripts/Voice/VoiceChat.cs): the whole path above. It creates itself at start-up and survives scene changes, so no scene or prefab holds it.
- [VoiceCodec.cs](../../Assets/_Project/Scripts/Voice/VoiceCodec.cs): `VoiceEncoder` and `VoiceDecoder`, the only code that touches the Opus library.
- [VoiceCleaner.cs](../../Assets/_Project/Scripts/Voice/VoiceCleaner.cs): the only code that touches the RNNoise library.
- [VoiceSpeaker.cs](../../Assets/_Project/Scripts/Voice/VoiceSpeaker.cs): one player's voice in the world: the buffer, the 3D audio source and the "is speaking" flag.
- [VoiceSettings.cs](../../Assets/_Project/Scripts/Voice/VoiceSettings.cs): the saved choices and the mic test switch.
- [AudioPage.cs](../../Assets/_Project/Scripts/UI/Kit/AudioPage.cs) and [UiKitSettingsBuilder.cs](../../Assets/_Project/Editor/UI/UiKitSettingsBuilder.cs): the four settings rows, and the builder that makes them in `SettingsScreen.prefab`.
- [InputSystem_Actions.inputactions](../../Assets/InputSystem_Actions.inputactions): a `Voice` action in the Player map, bound to V. It is read from the player's own copy of the actions, so a rebinding is followed.

For UI work: `VoiceChat.Instance.IsSpeaking(clientId)`, `.MicOpen`, `.Transmitting`, `.Available`, `.Devices` and `.CanSuppressNoise`, and `VoiceSettings.Changed`.

## Libraries

Both are vendor code under `Assets/Plugins/` and are never edited in place.

| Library | What | Where it came from | Licence |
|---|---|---|---|
| Concentus 2.2.2 | Opus in plain C#, one DLL, runs on every platform | The `netstandard2.0` build from the official NuGet package | BSD 3-clause, in `Assets/Plugins/Concentus/LICENSE.txt` |
| RNNoise 0.1.1 | Noise suppression, a native DLL for 64-bit Windows only | Built on Harvey's PC from Xiph's source, see `Assets/Plugins/RNNoise/README.md` | BSD 3-clause, in `Assets/Plugins/RNNoise/LICENSE.txt` |

Both licences ask that their text ships with the game. That is not done yet: see Known limitations.

## Tuning

The ranges, bitrate, gate, buffer times and the host's cap are named constants at the top of `VoiceChat.cs` and `VoiceSpeaker.cs`, each with what it controls and why it has that value. They are constants and not Inspector fields because the object is made at start-up and has no prefab to hold them.

## Checked

2026-11-15, in the Editor, in `Lane_Integration.unity`:

- The project compiles with no errors, and the console had no errors from voice.
- **Delay.** The first version played through a streamed clip. Measured, Unity asked that clip for 0.8 s of sound before playing any. The new playback was timed with a test tone pushed into a speaker: sound reached the listener 10 to 18 ms later, in six runs.
- **Distance and direction**, with the same test tone and a meter on the listener: flat it peaked at 0.25; 2 m to the right it peaked at 0.50 (all in one ear); at 13.5 m, halfway down the fade, 0.25; at 30 m, 0.00.
- **Opus** loads and runs: 84 bytes a piece on average for a test tone, 1.0 ms to compress a piece and 0.26 ms to decompress one, a nonsense packet decodes to nothing and a lost piece is filled in.
- **RNNoise** loads and runs. Steady white noise came out 3.8 dB quieter and was never called speech (0.44 at most against the 0.5 needed), so the gate stayed shut on it.
- **Mic test with no game running:** on the title screen the microphone started at 48 kHz and a quiet room sent nothing.
- **Settings rows**, seen in a screenshot and driven from a script: all four show, stepping Microphone to the Yeti restarted the recording on that microphone, the mode and the test toggle changed their values, and Reset to defaults lit up. The Controls tab shows **Mute / Push to talk** on V.
- Harvey listened to the first version (ADPCM, streamed clip) and it worked, with the three complaints that led to this one.

**Not checked:** nobody has listened to the Opus and RNNoise version, so how it sounds, how well real noise (a keyboard, a fan) is removed and how the delay feels are unjudged. It has not been run with a second player, so the host passing a voice on and the filling of lost pieces on a real connection are unseen. Not tried over Steam or in a build, and RNNoise's DLL has not been seen to load in a build.

## Known limitations

- **No way to see who is talking.** There is no microphone icon and no speaking marker over heads. The values exist for the UI to read.
- **The licence texts are not shown in the game yet.** Opus and RNNoise both need their notices shipped with a release, in a credits screen or a file beside the game.
- **Noise suppression is Windows 64-bit only.** On anything else voice still works and the gate goes by loudness.
- **`KitOffice.unity` has no Audio Listener**, so nothing at all is heard in that scene, voice included. The lane scenes have one.
- **Walls and floors do not muffle a voice.** Only distance counts, so a player one floor up is heard as clearly as one across the room.
- **Dead and waiting players** are treated like anyone else with a body. Whether the dead should hear or be heard is a design question for Adam.
- **Both teams hear each other.** Team-only or radio voice is not built; that is a design question too.
- **No per-player mute**, and no check of Steam's own block list.
- **The host carries every voice.** 8 players all talking in one room is about 8 x 7 x 4.1 kB a second out of the host at worst. Not measured.
- **No echo removal.** A player on speakers with an open microphone will send other players' voices back to them. Headphones or push-to-talk avoid it.
- **The microphone itself adds delay** that this code cannot remove: Unity's recording runs a little behind the real microphone. Not measured.
- **The Audio tab has no "Changed on this tab" list**, as on Controls: nine rows fill the page.
