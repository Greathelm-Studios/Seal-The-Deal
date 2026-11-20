# UI sound

**Status:** ready for review, **unheard**: these were synthesised by script and never listened to by anyone. Adam judges them by ear.

The game's UI voice: a clean 2001 corporate future. Glassy ticks, soft sine and triangle tones with a fast decay, a breath of filtered noise for air, sparse. Feel (not sound) is taken from how the Wii and DS menus answer every input: short, soft, tonal, each event distinct. Nothing is sampled or copied.

## How it works

```
UiSound (hub, Scripts/UI/Kit, lane 5) --> UiSoundPlayer --> AudioSource pool --> mixer group UI --> Master
                                              ^                                    (exposed UiVolume)
                                         UiSoundSet (events -> clip, volume, pitch variance, minimum gap)
```

- Events: [UiSoundEvent.cs](../../Assets/_Project/Scripts/UI/Sound/UiSoundEvent.cs), 36 of them, names agreed with lane 5. New ones are only ever appended: buttons and the sound set store the number.
- Player: [UiSoundPlayer.cs](../../Assets/_Project/Scripts/UI/Sound/UiSoundPlayer.cs). Voice cap 8 (steals the voice nearest its end), 12 ms between any two sounds, a per-event minimum gap (hover 60 ms, slider 35 ms, hit marker 40 ms), nothing plays while the game is unfocused.
- Two layers: control sounds (hover, focus, press, toggle, tick...) keep 12 ms apart, so one action never stacks two of them; screen sounds (Transition, PanelOpen, PanelClose, TitleAppear and the four transition kinds) play on top, so a button that opens a screen is heard as its Confirm and the screen's whoosh together.
- Title sting: `TitleAppear` plays when the player reaches the title (from boot or back from a game), not when a dialog or Settings closes back onto it.
- Transitions: each of lane 8's overlay transitions plays its own sound when it starts (`UiTransition.cs`): cloud wipe, pane slide, frame zoom (the game starting; this is the game-start sting, so there is no separate one) and the reduced-motion fade.
- Ambient bed: `Assets/_Project/Audio/Ambience/Ambience_MenuBed.wav`, 64 s, seamless, streamed Vorbis on the Music bus. It plays under the title and its screens and in the lobby reception, fading in and out over 1.5 s; it keeps playing from the title into reception and fades when the run starts. It is 64 s, not the few minutes asked for, to keep the file about 6 MB in LFS: someone idling on the title hears it repeat about once a minute. **It ships switched off**: `UiSoundSet.ambienceLevel` is 0; raise it (0.3 is a quiet bed) to hear it. The player's Music slider scales it.
- Hover run: each hover in a run climbs the pentatonic scale (C D E G A, via pitch); a pause of 0.6 s starts the run again.
- Slider: one tick clip, pitch follows the value from -5 to +12 semitones.
- Set: [UiSoundSet](../../Assets/_Project/Scripts/UI/Sound/UiSoundSet.cs) asset at `Assets/_Project/Resources/UI/UiSoundSet.asset`, built by **Seal the Deal > UI > Set Up Sound**. That menu also sets the clips' import settings: mono, decompress on load, PCM under 0.45 s and ADPCM above, preloaded.
- Mixer: `Assets/_Project/Audio/UI/GameMixer.mixer`: Master with children Music, SFX, Voice, UI. Exposed volumes: `MasterVolume`, `MusicVolume`, `SfxVolume`, `VoiceVolume`, `UiVolume` (dB). A volume slider calls `UiAudio.SetVolume(AudioBus.Ui, 0..1)` (**no settings row calls it yet**: the audio rows are the UI lane's); it writes dB (1 = 0 dB, 0 = -80 dB) and remembers the value in PlayerPrefs (written to disk by `UiAudio.Flush()` when the game loses focus or quits, and when the settings screen closes, never per slider step); saved volumes are restored after the first scene loads.
- Host authority: sound is presentation only. Nothing here reads or writes game state or the network.

## The sounds

Key: C major pentatonic (C D E G A), so any run of sounds is consonant. Confirms resolve upward; back, error and unready resolve downward. Every clip is levelled to a **perceived** target, not a peak: the A-weighted level of its loudest 46 ms (UI -14 dB, stings -11, hit ticks -11, kill confirm -9, slider tick -18, countdown tick -15), under a peak ceiling per class (UI 0.70, sting 0.80, combat 0.90). A very short tick can hit its ceiling before its target; the table shows where. 16-bit, 48 kHz, mono, under 1.2 s.

| Event | Length | Peak | Perceived level | Class | Pitch and relation |
|---|---|---|---|---|---|
| `HoverMove` | 100 ms | -4.6 dBFS | -14.0 dB | ui | G5; run steps up C D E G A |
| `Focus` | 100 ms | -4.9 dBFS | -14.0 dB | ui | E5, softer than hover |
| `Select` | 180 ms | -9.4 dBFS | -14.0 dB | ui | C5 to G5, rises a fifth |
| `Confirm` | 260 ms | -10.0 dBFS | -14.0 dB | ui | C5 G5 C6, rises (resolves up) |
| `Back` | 200 ms | -7.7 dBFS | -14.0 dB | ui | G5 to C5, falls |
| `Error` | 300 ms | -3.1 dBFS | -14.0 dB | ui | E4 to C4, falls (dull, soft) |
| `TabChange` | 120 ms | -3.6 dBFS | -14.0 dB | ui | A5 glass tick + air |
| `ToggleOn` | 140 ms | -6.6 dBFS | -14.0 dB | ui | C5 to G5, quick rise |
| `ToggleOff` | 140 ms | -5.3 dBFS | -14.0 dB | ui | A5 to E5, light fall |
| `SliderTick` | 50 ms | -3.1 dBFS | -19.1 dB | ui | C6 tick; runtime pitch follows value |
| `DropdownOpen` | 160 ms | -9.1 dBFS | -14.0 dB | ui | air swish up + A5 |
| `DropdownClose` | 140 ms | -6.8 dBFS | -14.0 dB | ui | air swish down + A5 to D5 |
| `KeyBindListen` | 200 ms | -8.2 dBFS | -14.0 dB | ui | E5 double pulse (waiting) |
| `KeyBindSet` | 300 ms | -9.5 dBFS | -14.0 dB | ui | C6 ding over G5 |
| `PanelOpen` | 300 ms | -7.4 dBFS | -14.0 dB | ui | C5 + G5 rising sweep |
| `PanelClose` | 260 ms | -7.1 dBFS | -14.0 dB | ui | G5 + C5 falling sweep |
| `Transition` | 480 ms | -7.1 dBFS | -14.0 dB | ui | noise whoosh up; C5 under |
| `Notification` | 460 ms | -10.0 dBFS | -14.0 dB | ui | G5 then D6, rises a fifth |
| `LobbyReady` | 240 ms | -10.4 dBFS | -14.0 dB | ui | D5 G5 D6, three steps up |
| `LobbyUnready` | 220 ms | -9.0 dBFS | -14.0 dB | ui | D6 G5 D5, three steps down |
| `CountdownTick` | 70 ms | -3.1 dBFS | -17.7 dB | ui | C6 tick over a C4 knock |
| `CountdownGo` | 500 ms | -4.8 dBFS | -11.0 dB | sting | C5 E5 G5 C6 flourish, up |
| `MatchWon` | 1100 ms | -6.8 dBFS | -11.0 dB | sting | C5 E5 G5 C6 E6 arpeggio, up |
| `MatchLost` | 1000 ms | -1.9 dBFS | -11.4 dB | sting | G4 E4 C4, slow fall |
| `FloorOpened` | 650 ms | -5.7 dBFS | -11.0 dB | sting | C6 then G6 lift chime over C4 |
| `BriefcaseTaken` | 240 ms | -10.6 dBFS | -14.0 dB | ui | clack + G5 to C6 rise |
| `BriefcaseDropped` | 200 ms | -4.6 dBFS | -14.0 dB | ui | thud + C4 fall |
| `HandsFull` | 200 ms | -3.1 dBFS | -15.5 dB | ui | D4 double bonk, low |
| `HitMarker` | 55 ms | -0.9 dBFS | -13.6 dB | combat | G7 tick (3.1 kHz), very short (full auto: 10 per second) |
| `HitMarkerHead` | 55 ms | -0.9 dBFS | -15.7 dB | combat | C8 tick (4.2 kHz) over G7: brighter, precision hit |
| `KillConfirm` | 200 ms | -4.6 dBFS | -9.0 dB | combat | E7 to B7 ticks + B6 ring (2.6 to 3.9 kHz) |
| `TitleAppear` | 1100 ms | -4.0 dBFS | -11.0 dB | sting | C5 E5 G5 bloom to D6 over C4, airy |
| `TransitionCloudWipe` | 300 ms | -8.1 dBFS | -14.0 dB | ui | soft swish across + A5 |
| `TransitionPaneSlide` | 280 ms | -7.7 dBFS | -14.0 dB | ui | three glass taps E6 D6 C6 + slide |
| `TransitionFrameZoom` | 860 ms | -6.1 dBFS | -11.0 dB | sting | rising whoosh landing on C5 G5 C6 E6: the game starts |
| `TransitionFade` | 130 ms | -7.9 dBFS | -14.0 dB | ui | small puff of air (reduced motion) |

Combat sounds sit at 2.6 to 4.2 kHz so they cut through gunfire; the tick stays short because full auto fires up to 10 hits a second.

## Measured against each other

`python3 ArtSource/Audio/ui/make_ui_sounds.py --analyse` prints, for every clip, its perceived level and spectral centroid, and lists UI pairs close enough to be confused (same length within 30 %, band spectrum and level envelope both above 0.93 cosine similarity). These numbers stand in for ears; they do not replace them.

- **First measurement (peak-normalised clips):** the UI set spread over 12.9 dB by perceived level, and seven pairs were flagged as alike (Focus / TabChange / DropdownClose / CountdownTick all sat on E5; Back / ToggleOff; DropdownOpen / LobbyReady; PanelOpen / PanelClose).
- **Now:** every UI clip sits on its target except the two deliberately quieter ticks; stings spread 0.4 dB. TabChange moved to A5, DropdownClose to A5 then D5, ToggleOff to A5 then E5, LobbyReady and LobbyUnready to three steps (D5 G5 D6 and back), CountdownTick to C6 over a C4 knock.
- **Still flagged:** SliderTick / CountdownTick (both C6 ticks; they never play in the same place) and PanelOpen / PanelClose (same notes, opposite glide direction, which this measure cannot see).
- **Shots and the explosion are peak-limited:** a gunshot is a few milliseconds long, so at full scale it still measures about -15 dB (pistol) and -20 dB (AR-15) on a 46 ms window, and the explosion -14 dB. Handling sounds, impacts and glass are set under the shots so the order holds (shot, then glass and impacts, then reloads and swap). How loud a shot is in the game is the playing lane's AudioSource volume and distance curve.

## Verification

Scripted Unity run (6000.4.8f1, batch mode, `-executeMethod CloseTheDeal.Editor.Lanes.UiSoundLaneSetup.RunSelfTest`, 2026-11-14, after merging buffer). It enters Play Mode, first checks that nothing plays while the game is unfocused and that a volume saved before the session is restored, then fires every event once through the UI kit's hub and the UI bus and measures the final output at the listener. Peaks read 3 dB under the file peak because a mono clip is centre-panned into stereo. **Measured, not heard.**

```
audioConfig: sampleRate=44100 speakerMode=Stereo dspTime=21.432
hub: UiSound.Player is the sound lane's adapter OK
unfocused: Application.isFocused=False, Play returned False
unfocused: output peak=0.00000 OK silent
event HoverMove          peak=0.414 rms=0.0537 SIGNAL
event Focus              peak=0.401 rms=0.0571 SIGNAL
event Select             peak=0.240 rms=0.0375 SIGNAL
event Confirm            peak=0.224 rms=0.0530 SIGNAL
event Back               peak=0.293 rms=0.0559 SIGNAL
event Error              peak=0.493 rms=0.1089 SIGNAL
event TabChange          peak=0.463 rms=0.0516 SIGNAL
event ToggleOn           peak=0.331 rms=0.0506 SIGNAL
event ToggleOff          peak=0.384 rms=0.0561 SIGNAL
event SliderTick         peak=0.494 rms=0.0528 SIGNAL
event DropdownOpen       peak=0.247 rms=0.0354 SIGNAL
event DropdownClose      peak=0.322 rms=0.0622 SIGNAL
event KeyBindListen      peak=0.276 rms=0.0533 SIGNAL
event KeyBindSet         peak=0.237 rms=0.0364 SIGNAL
event PanelOpen          peak=0.303 rms=0.0557 SIGNAL
event PanelClose         peak=0.314 rms=0.0572 SIGNAL
event Transition         peak=0.293 rms=0.0546 SIGNAL
event Notification       peak=0.225 rms=0.0433 SIGNAL
event LobbyReady         peak=0.213 rms=0.0412 SIGNAL
event LobbyUnready       peak=0.252 rms=0.0542 SIGNAL
event CountdownTick      peak=0.493 rms=0.0466 SIGNAL
event CountdownGo        peak=0.400 rms=0.0852 SIGNAL
event MatchWon           peak=0.326 rms=0.0665 SIGNAL
event MatchLost          peak=0.564 rms=0.1179 SIGNAL
event FloorOpened        peak=0.368 rms=0.0609 SIGNAL
event BriefcaseTaken     peak=0.208 rms=0.0335 SIGNAL
event BriefcaseDropped   peak=0.411 rms=0.0784 SIGNAL
event HandsFull          peak=0.492 rms=0.0981 SIGNAL
event HitMarker          peak=0.614 rms=0.0741 SIGNAL
event HitMarkerHead      peak=0.617 rms=0.0592 SIGNAL
event KillConfirm        peak=0.404 rms=0.0589 SIGNAL
UI bus at 0 (-80 dB): peak=0.00000 OK silent
saved volume: Interface saved at 0.5 before this session, first sound peak=0.112 against 0.224 at full: -6.0 dB (expect about -6) OK restored
UI slider 1.0 peak=0.224, 0.5 peak=0.112: -6.0 dB (expect about -6) OK
SFX bus at 0 while UI plays: peak=0.224 OK UI unaffected
rapid hover: 27724 requests in 1 s, started=17 dropped=27707, output peak=0.450 OK no clipping
```

## The sound audit

[10_sound_audit.md](../../ArtSource/UI/review/10_sound_audit.md) lists what every menu interaction plays and the fixes made (key-bind listen and set, steppers, slider clicks, screen whooshes). It was read from the code, not run.

## Item sounds (world, SFX bus)

The same generator writes the pickups lane's 12 world sounds to `Assets/_Project/Audio/Items/Item_<Slot>.wav` (3D, routed to `Master/SFX` by that lane's setup; slots and timing in ITEMS.md "Item sounds"). **Unheard, like the rest.**

| Slot | Length | Peak | Perceived level | What it is |
|---|---|---|---|---|
| `PickUp` | 150 ms | -0.9 dBFS | -14.3 dB | grab: soft noise + knock |
| `Drop` | 200 ms | -0.9 dBFS | -16.3 dB | dull knock + small bounce |
| `Throw` | 200 ms | -7.3 dBFS | -14.0 dB | whoosh, band falling |
| `ImpactLight` | 120 ms | -0.9 dBFS | -15.7 dB | thin clack, full strength |
| `ImpactHeavy` | 350 ms | -0.9 dBFS | -16.4 dB | low thud 140 to 55 Hz, full strength |
| `Arm` | 100 ms | -5.1 dBFS | -16.0 dB | two-part click |
| `Beep` | 50 ms | -10.4 dBFS | -13.0 dB | 2.1 kHz, 50 ms |
| `Explosion` | 1100 ms | -0.3 dBFS | -14.2 dB | crack + boom 120 to 32 Hz + rumble tail |
| `VentEnd` | 400 ms | -6.0 dBFS | -14.0 dB | hiss falling away |
| `WetFloorDown` | 320 ms | -2.3 dBFS | -14.0 dB | plastic clack + splash |
| `Slip` | 300 ms | -10.0 dBFS | -13.0 dB | squeak 700 to 1900 Hz + scuff |
| `VentLoop` | 2000 ms | -9.5 dBFS | -17.0 dB | gas hiss, seamless (crossfaded seam; set loop on the AudioSource) |

## Weapon sounds (world, SFX bus)

The same generator writes the shooting lane's 15 sounds to `Assets/_Project/Audio/Weapons/Weapon_<Name>.wav`. Events and timing are in WEAPONS.md "Sounds the guns still need"; the shooting lane plays them from its own events (3D at the muzzle or hit point; Hurt, Death and Respawn 2D). **Unheard, like the rest.** A synthesised gunshot is the hardest of these to get right without ears: expect the two shots to need the most retuning.

| Name | Length | Peak | Perceived level | What it is |
|---|---|---|---|---|
| `PistolShot` | 260 ms | -0.3 dBFS | -15.1 dB | crack + punch 220 to 70 Hz + 0.2 s room tail |
| `AR15Shot` | 100 ms | -0.3 dBFS | -19.6 dB | tighter crack, 100 ms so 10 a second stays clean |
| `DryFire` | 60 ms | -6.5 dBFS | -25.0 dB | dull click |
| `ReloadMagOut` | 110 ms | -8.5 dBFS | -24.0 dB | slide + release click |
| `ReloadMagIn` | 140 ms | -9.3 dBFS | -23.0 dB | slide + solid seat |
| `ReloadSlide` | 130 ms | -10.0 dBFS | -23.0 dB | pistol slide: back click, forward clack |
| `ReloadCharge` | 210 ms | -10.2 dBFS | -23.0 dB | rifle charging handle: pull, heavy return |
| `ReloadFinished` | 60 ms | -10.3 dBFS | -27.0 dB | small settled click |
| `Swap` | 220 ms | -18.7 dBFS | -25.0 dB | swish + catch |
| `ImpactWorld` | 130 ms | -2.8 dBFS | -20.0 dB | hard tick + debris |
| `ImpactBody` | 120 ms | -2.3 dBFS | -21.0 dB | dull thud 170 to 85 Hz |
| `GlassShatter` | 700 ms | -10.7 dBFS | -17.0 dB | crack + 26 falling glass partials + bright wash |
| `Hurt` | 220 ms | -0.9 dBFS | -19.0 dB | low pulse 120 to 75 Hz, 2D |
| `Death` | 750 ms | -3.5 dBFS | -17.0 dB | tone falling E4 to C3 over a low drop, 2D |
| `Respawn` | 520 ms | -11.9 dBFS | -17.0 dB | C5 to G5 rise with air, 2D |

The audition page (`ArtSource/UI/review/10_sound_audition.html`) groups every clip by where it plays and has a 25 s "walk through the menu" (title, settings, hosting into reception with the bed still under it, the run starting, the countdown). Opened in a browser from a local web server on 2026-11-14: all 64 clips (and the bed) loaded, the walk started, the bed faded in to 0.3. Not opened straight from disk.

## Recorded sounds replace synthesised ones

Adam judged the synthesised set "very artificial", so recordings from open-licence libraries will replace most of it, once he approves the sources. No new sounds are synthesised meanwhile (the movement set and any new bed included).

- Every slot keeps its file name and event. A cleared recording put at `ArtSource/Audio/recorded/<UI|Items|Weapons|Movement|Ambience>/<the game's file name>` (16- or 24-bit PCM WAV, any rate, mono or stereo) replaces the synthesised one when the generator runs: trimmed and faded from `ArtSource/Audio/recorded/edits.json`, made mono 48 kHz, levelled to the slot's loudness target, written over the file in `Assets/_Project/Audio/`, so the .meta and every event keep working.
- **A recording with no complete row in [ArtSource/Audio/LICENCES.md](../../ArtSource/Audio/LICENCES.md) is refused** and the run fails.
- What each slot should sound like, its length, loops and variations, a library search phrase and whether the synthesised one may stay: [ArtSource/Audio/SOUND_SHOPPING_LIST.md](../../ArtSource/Audio/SOUND_SHOPPING_LIST.md).
- The movement slots (lane 1's eleven names, 22 files with variations) live in `Assets/_Project/Audio/Movement/` and are written only once a recording arrives.
- The audition page marks every slot synthesised, recorded or missing.
- **The GDD says no asset packs ship** (docs/GDD.md line 144); shipping library sounds needs that changed. Flagged to Adam in LICENCES.md.

## Regenerating

```bash
python3 ArtSource/Audio/ui/make_ui_sounds.py            # writes the 31 UI WAVs the 12 item WAVs and the 15 weapon WAVs, prints peak and RMS
python3 ArtSource/Audio/ui/make_ui_sounds.py --table    # also prints this table as markdown
```

The output is deterministic (fixed noise seeds): two runs give byte-identical files. Edit the `EVENTS` table (notes, durations, timbre) and re-run, then run **Set Up Sound** if clips were added. That menu only adds: it sets a new clip's import settings once, adds entries for new events, and leaves existing entries, hand-tuned values and the mixer alone.

## Not in this set

Gun shots, reloads, impacts and glass are a later weapons sound pass (see WEAPONS.md). Music and voice have buses but no content yet.
