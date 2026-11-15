<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-15
  Prompt    : Harvey: "Add VOIP/Proximity chat to the game.", then "Make our own custom VOIP for this."
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : How proximity voice chat works, who owns what, and what has and has not been checked.
-->
# VOICE

**Status:** Target (compiles and runs for one player; **not yet heard by a person or tried with two players**) · **Last updated:** 2026-11-15

## What the player hears

Players talk to each other by voice, and a voice comes from the speaker's head. It is at full volume within 3 m, fades in a straight line and is gone at 24 m, and it pans left and right as the speaker moves. Both teams hear each other: there are no team channels. The settings screen's **Voice** slider sets how loud everyone is.

The microphone is open from the start. **V** mutes and unmutes it. In push-to-talk mode the microphone is open only while **V** is held.

Voice is all our own: the microphone is read through Unity and the sound is compressed, sent and played by code in this repository. It needs no Steam, no voice service and no added package, so it works in local test mode too. A player with no microphone hears everyone and is not heard.

## Who owns what

| Thing | Owner |
|---|---|
| Recording and compressing a player's voice | That player's machine |
| Who hears a voice | The host. It passes a voice on only to players whose body is within 28 m of the speaker's body |
| How much voice a player may send | The host: 14,000 bytes a second each, more is dropped |
| Where a voice plays from, and its fade | Each listening machine, from the speaker's body |
| Microphone mode and mute | The speaker's machine only (`PlayerPrefs`), never sent |

Nothing is predicted or reconciled and no gameplay reads voice. A player with no body (not yet spawned) is not heard and hears nobody.

## How a word travels

1. The speaker's machine records the microphone at 16 kHz and cuts it into 40 ms pieces. With an open microphone, pieces quieter than a set level are dropped (a noise gate that stays open 0.35 s after the last loud piece), so silence and room noise are not sent. Push-to-talk sends everything while the key is held.
2. Each piece is compressed to a quarter of its size by our own codec (IMA ADPCM, 4 bits a sample) and numbered. A packet is 325 bytes, 25 a second: about 8 kB a second while someone talks, nothing while they are quiet. Every packet carries what it needs to be decoded alone, so a lost one costs only its own 40 ms.
3. It goes to the host as a `VoiceFrame` broadcast, unreliable.
4. The host checks its size and the sender's allowance, then sends a `VoiceRelay` (the speaker's client id and the same bytes) to each other player in range.
5. Each listener throws away a piece that arrives late or twice, decompresses the rest and adds it to that speaker's buffer. The buffer holds 80 ms before it starts, so uneven arrival does not crackle, and never more than 400 ms, so a voice cannot fall behind the mouth.

## Scripts

- [VoiceChat.cs](../../Assets/_Project/Scripts/Voice/VoiceChat.cs): the whole path above. It creates itself at start-up and survives scene changes, so no scene or prefab holds it.
- [VoiceCodec.cs](../../Assets/_Project/Scripts/Voice/VoiceCodec.cs): the compression, in one file with no library.
- [VoiceSpeaker.cs](../../Assets/_Project/Scripts/Voice/VoiceSpeaker.cs): one player's voice in the world: the buffer, the 3D audio source and the "is speaking" flag.
- [VoiceSettings.cs](../../Assets/_Project/Scripts/Voice/VoiceSettings.cs): microphone mode (`Open`, `PushToTalk`, `Off`), mute, and `HearSelf`, a mic test that plays your own voice back flat after compression.
- [InputSystem_Actions.inputactions](../../Assets/InputSystem_Actions.inputactions): a `Voice` action in the Player map, bound to V. It is read from the player's own copy of the actions, so a rebinding is followed.

For UI work: `VoiceChat.Instance.IsSpeaking(clientId)`, `.MicOpen`, `.Transmitting` and `.Available`, and `VoiceSettings.Changed`.

## Tuning

The ranges, buffer times and the host's cap are named constants at the top of `VoiceChat.cs` and `VoiceSpeaker.cs`, each with what it controls and why it has that value. They are constants and not Inspector fields because the object is made at start-up and has no prefab to hold them.

## Checked

2026-11-15, in the Editor, hosting alone in `KitOffice.unity`:

- The project compiles with no errors.
- The microphone starts at 16 kHz and keeps recording while connected.
- With an open microphone, a quiet room (level about 0.0001) sends nothing and a louder moment (0.03) is sent: the noise gate does its job.
- The `Voice` action is found on the player's own input actions, and a V press in push-to-talk mode sends sound.
- With `HearSelf` on, the sound goes through compression and back and plays on a speaker routed to the mixer's Voice group.
- `VoiceCodec.cs` was also run on its own outside Unity on one second of mixed tones: 323 bytes for 640 samples, 29 dB signal-to-noise, and broken packets decode to nothing.
- The console had no errors or warnings from voice.

**Not checked:** nobody has listened to it, so how it sounds is unjudged. It has not been run with a second player, so the host passing a voice on, the distance cut-off, the fade and the left and right placement are all unseen. Not tried over Steam or in a build.

Seen in passing, not voice's doing as far as read: while hosting in `KitOffice.unity` Unity logged "There are no audio listeners in the scene" every frame. With no listener nothing is heard at all, voice included. Not looked into.

## Known limitations

- **No way to see or change it in game.** There is no microphone icon, no speaking marker over heads, no settings row for microphone mode or push-to-talk, and V is not in the Controls list. The values exist for the UI to read; the screens are the UI work's.
- **Walls and floors do not muffle a voice.** Only distance counts, so a player one floor up is heard as clearly as one across the room.
- **Dead and waiting players** are treated like anyone else with a body. Whether the dead should hear or be heard is a design question for Adam.
- **Both teams hear each other.** Team-only or radio voice is not built; that is a design question too.
- **No per-player mute**, and no check of Steam's own block list.
- **It costs more bandwidth than a voice library would.** About 8 kB a second per talking player; Opus would be about a third of that for the same quality, but it is a native library to build and ship. The host carries every voice, so 8 players all talking in one room is about 8 x 7 x 8 kB a second out of the host at worst. Not measured.
- **No echo or noise removal.** A player on speakers with an open microphone will send other players' voices back to them. Headphones or push-to-talk avoid it. The noise gate only drops quiet sound; it does not clean loud sound.
- **Speech quality is wideband telephone**, 16 kHz, with a faint hiss from the compression on quiet voices. Not judged by ear yet.
- **The default microphone only.** There is no device picker.
