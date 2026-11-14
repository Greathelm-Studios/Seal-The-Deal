# UI sound

**Status:** ready for review, **unheard**: these were synthesised by script and never listened to by anyone. Adam judges them by ear.

The game's UI voice: a clean 2001 corporate future. Glassy ticks, soft sine and triangle tones with a fast decay, a breath of filtered noise for air, sparse. Feel (not sound) is taken from how the Wii and DS menus answer every input: short, soft, tonal, each event distinct. Nothing is sampled or copied.

## How it works

```
UiSound (hub, Scripts/UI/Kit, lane 5) --> UiSoundPlayer --> AudioSource pool --> mixer group UI --> Master
                                              ^                                    (exposed UiVolume)
                                         UiSoundSet (events -> clip, volume, pitch variance, minimum gap)
```

- Events: [UiSoundEvent.cs](../../Assets/_Project/Scripts/UI/Sound/UiSoundEvent.cs), 31 of them, names frozen with lane 5.
- Player: [UiSoundPlayer.cs](../../Assets/_Project/Scripts/UI/Sound/UiSoundPlayer.cs). Voice cap 8 (steals the voice nearest its end), 12 ms between any two sounds, a per-event minimum gap (hover 60 ms, slider 35 ms, hit marker 40 ms), nothing plays while the game is unfocused.
- Hover run: each hover in a run climbs the pentatonic scale (C D E G A, via pitch); a pause of 0.6 s starts the run again.
- Slider: one tick clip, pitch follows the value from -5 to +12 semitones.
- Set: [UiSoundSet](../../Assets/_Project/Scripts/UI/Sound/UiSoundSet.cs) asset at `Assets/_Project/Resources/UI/UiSoundSet.asset`, built by **Close the Deal > UI > Set Up Sound**. That menu also sets the clips' import settings: mono, decompress on load, PCM under 0.45 s and ADPCM above, preloaded.
- Mixer: `Assets/_Project/Audio/UI/GameMixer.mixer`: Master with children Music, SFX, Voice, UI. Exposed volumes: `MasterVolume`, `MusicVolume`, `SfxVolume`, `VoiceVolume`, `UiVolume` (dB). A volume slider calls `UiAudio.SetVolume(AudioBus.Ui, 0..1)` (**no settings row calls it yet**: the audio rows are the UI lane's); it writes dB (1 = 0 dB, 0 = -80 dB) and saves to PlayerPrefs; saved volumes are restored after the first scene loads.
- Host authority: sound is presentation only. Nothing here reads or writes game state or the network.

## The sounds

Key: C major pentatonic (C D E G A), so any run of sounds is consonant. Confirms resolve upward; back, error and unready resolve downward. Every clip is scaled to a peak target by class (UI 0.45 = -6.9 dBFS, sting 0.55 = -5.2 dBFS, combat 0.70 = -3.1 dBFS), so none is louder than another by number; RMS varies with length and is printed for review. 16-bit, 48 kHz, mono, under 1.2 s.

| Event | Length | Peak | RMS | Class | Pitch and relation |
|---|---|---|---|---|---|
| `HoverMove` | 100 ms | -6.9 dBFS | -21.5 dBFS | ui | G5; run steps up C D E G A |
| `Focus` | 100 ms | -6.9 dBFS | -20.2 dBFS | ui | E5, softer than hover |
| `Select` | 180 ms | -6.9 dBFS | -20.8 dBFS | ui | C5 to G5, rises a fifth |
| `Confirm` | 260 ms | -6.9 dBFS | -17.9 dBFS | ui | C5 G5 C6, rises (resolves up) |
| `Back` | 200 ms | -6.9 dBFS | -19.2 dBFS | ui | G5 to C5, falls |
| `Error` | 300 ms | -6.9 dBFS | -18.7 dBFS | ui | E4 to C4, falls (dull, soft) |
| `TabChange` | 120 ms | -6.9 dBFS | -23.8 dBFS | ui | E5 glass tick + air |
| `ToggleOn` | 140 ms | -6.9 dBFS | -20.6 dBFS | ui | C5 to G5, quick rise |
| `ToggleOff` | 140 ms | -6.9 dBFS | -19.3 dBFS | ui | G5 to C5, quick fall |
| `SliderTick` | 50 ms | -6.9 dBFS | -21.2 dBFS | ui | C6 tick; runtime pitch follows value |
| `DropdownOpen` | 160 ms | -6.9 dBFS | -21.4 dBFS | ui | air swish up + A5 |
| `DropdownClose` | 140 ms | -6.9 dBFS | -20.9 dBFS | ui | air swish down + E5 |
| `KeyBindListen` | 200 ms | -6.9 dBFS | -19.1 dBFS | ui | E5 double pulse (waiting) |
| `KeyBindSet` | 300 ms | -6.9 dBFS | -21.7 dBFS | ui | C6 ding over G5 |
| `PanelOpen` | 300 ms | -6.9 dBFS | -20.2 dBFS | ui | C5 + G5 rising sweep |
| `PanelClose` | 260 ms | -6.9 dBFS | -19.9 dBFS | ui | G5 + C5 falling sweep |
| `Transition` | 480 ms | -6.9 dBFS | -20.6 dBFS | ui | noise whoosh up; C5 under |
| `Notification` | 460 ms | -6.9 dBFS | -20.2 dBFS | ui | G5 then D6, rises a fifth |
| `LobbyReady` | 220 ms | -6.9 dBFS | -19.4 dBFS | ui | E5 to B5, rises |
| `LobbyUnready` | 200 ms | -6.9 dBFS | -18.2 dBFS | ui | B5 to E5, falls |
| `CountdownTick` | 70 ms | -6.9 dBFS | -22.1 dBFS | ui | E5 tick, one pitch |
| `CountdownGo` | 500 ms | -5.2 dBFS | -17.9 dBFS | sting | C5 E5 G5 C6 flourish, up |
| `MatchWon` | 1100 ms | -5.2 dBFS | -18.4 dBFS | sting | C5 E5 G5 C6 E6 arpeggio, up |
| `MatchLost` | 1000 ms | -5.2 dBFS | -18.3 dBFS | sting | G4 E4 C4, slow fall |
| `FloorOpened` | 650 ms | -5.2 dBFS | -20.1 dBFS | sting | C6 then G6 lift chime over C4 |
| `BriefcaseTaken` | 240 ms | -6.9 dBFS | -21.0 dBFS | ui | clack + G5 to C6 rise |
| `BriefcaseDropped` | 200 ms | -6.9 dBFS | -19.3 dBFS | ui | thud + C4 fall |
| `HandsFull` | 200 ms | -6.9 dBFS | -19.2 dBFS | ui | D4 double bonk, low |
| `HitMarker` | 55 ms | -3.1 dBFS | -16.3 dBFS | combat | G7 tick (3.1 kHz), very short (full auto: 10 per second) |
| `HitMarkerHead` | 55 ms | -3.1 dBFS | -18.2 dBFS | combat | C8 tick (4.2 kHz) over G7: brighter, precision hit |
| `KillConfirm` | 200 ms | -3.1 dBFS | -18.1 dBFS | combat | E7 to B7 ticks + B6 ring (2.6 to 3.9 kHz) |

Combat sounds sit at 2.6 to 4.2 kHz and peak 4 dB hotter than UI sounds so they cut through gunfire; the tick stays short because full auto fires up to 10 hits a second.

## Verification

Scripted Unity run (6000.4.8f1, batch mode, `-executeMethod CloseTheDeal.Editor.Lanes.UiSoundLaneSetup.RunSelfTest`, 2026-11-14, after merging buffer). It enters Play Mode, first checks that nothing plays while the game is unfocused and that a volume saved before the session is restored, then fires every event once through the UI kit's hub and the UI bus and measures the final output at the listener. Peaks read 3 dB under the file peak because a mono clip is centre-panned into stereo. **Measured, not heard.**

```
audioConfig: sampleRate=44100 speakerMode=Stereo dspTime=14.698
hub: UiSound.Player is the sound lane's adapter OK
unfocused: Application.isFocused=False, Play returned False
unfocused: output peak=0.00000 OK silent
saved volume: Interface saved at 0.5 before this session, first sound peak=0.159 (-6.0 dB against full, expect about -6) OK restored
event HoverMove          peak=0.316 rms=0.0410 SIGNAL
event Focus              peak=0.318 rms=0.0451 SIGNAL
event Select             peak=0.318 rms=0.0496 SIGNAL
event Confirm            peak=0.318 rms=0.0755 SIGNAL
event Back               peak=0.318 rms=0.0607 SIGNAL
event Error              peak=0.317 rms=0.0697 SIGNAL
event TabChange          peak=0.318 rms=0.0329 SIGNAL
event ToggleOn           peak=0.318 rms=0.0490 SIGNAL
event ToggleOff          peak=0.318 rms=0.0541 SIGNAL
event SliderTick         peak=0.317 rms=0.0339 SIGNAL
event DropdownOpen       peak=0.316 rms=0.0453 SIGNAL
event DropdownClose      peak=0.313 rms=0.0469 SIGNAL
event KeyBindListen      peak=0.318 rms=0.0609 SIGNAL
event KeyBindSet         peak=0.318 rms=0.0489 SIGNAL
event PanelOpen          peak=0.317 rms=0.0589 SIGNAL
event PanelClose         peak=0.318 rms=0.0580 SIGNAL
event Transition         peak=0.314 rms=0.0555 SIGNAL
event Notification       peak=0.317 rms=0.0611 SIGNAL
event LobbyReady         peak=0.318 rms=0.0595 SIGNAL
event LobbyUnready       peak=0.317 rms=0.0678 SIGNAL
event CountdownTick      peak=0.314 rms=0.0339 SIGNAL
event CountdownGo        peak=0.389 rms=0.0804 SIGNAL
event MatchWon           peak=0.389 rms=0.0807 SIGNAL
event MatchLost          peak=0.388 rms=0.0802 SIGNAL
event FloorOpened        peak=0.388 rms=0.0646 SIGNAL
event BriefcaseTaken     peak=0.317 rms=0.0521 SIGNAL
event BriefcaseDropped   peak=0.317 rms=0.0604 SIGNAL
event HandsFull          peak=0.317 rms=0.0607 SIGNAL
event HitMarker          peak=0.487 rms=0.0612 SIGNAL
event HitMarkerHead      peak=0.488 rms=0.0461 SIGNAL
event KillConfirm        peak=0.478 rms=0.0672 SIGNAL
UI bus at 0 (-80 dB): peak=0.00000 OK silent
UI slider 1.0 peak=0.318, 0.5 peak=0.159: -6.0 dB (expect about -6) OK
SFX bus at 0 while UI plays: peak=0.318 OK UI unaffected
rapid hover: 33132 requests in 1 s, started=17 dropped=33115, output peak=0.343 OK no clipping
```

## Item sounds (world, SFX bus)

The same generator writes the pickups lane's 12 world sounds to `Assets/_Project/Audio/Items/Item_<Slot>.wav` (3D, routed to `Master/SFX` by that lane's setup; slots and timing in ITEMS.md "Item sounds"). **Unheard, like the rest.**

| Slot | Length | Peak | What it is |
|---|---|---|---|
| `PickUp` | 150 ms | -4.4 dBFS | grab: soft noise and a knock |
| `Drop` | 200 ms | -4.4 dBFS | dull knock and a small bounce |
| `Throw` | 200 ms | -4.4 dBFS | whoosh, band falling |
| `ImpactLight` | 120 ms | -1.9 dBFS | thin clack, full strength (game scales by speed) |
| `ImpactHeavy` | 350 ms | -1.9 dBFS | low thud, 140 to 55 Hz, full strength |
| `Arm` | 100 ms | -4.4 dBFS | two-part click |
| `Beep` | 50 ms | -4.4 dBFS | 2.1 kHz |
| `Explosion` | 1100 ms | -0.9 dBFS | crack, boom falling 120 to 32 Hz, rumble tail |
| `VentLoop` | 2000 ms | -6.0 dBFS | gas hiss, seamless (crossfaded seam; set loop on the AudioSource) |
| `VentEnd` | 400 ms | -4.4 dBFS | hiss falling away |
| `WetFloorDown` | 320 ms | -4.4 dBFS | plastic clack and splash |
| `Slip` | 300 ms | -4.4 dBFS | squeak 700 to 1900 Hz and a scuff |

## Weapon sounds (world, SFX bus)

The same generator writes the shooting lane's 15 sounds to `Assets/_Project/Audio/Weapons/Weapon_<Name>.wav`. Events and timing are in WEAPONS.md "Sounds the guns still need"; the shooting lane plays them from its own events (3D at the muzzle or hit point; Hurt, Death and Respawn 2D). **Unheard, like the rest.** A synthesised gunshot is the hardest of these to get right without ears: expect the two shots to need the most retuning.

| Name | Length | Peak | What it is |
|---|---|---|---|
| `PistolShot` | 260 ms | -0.9 dBFS | crack, punch 220 to 70 Hz, 0.2 s room tail |
| `AR15Shot` | 100 ms | -0.9 dBFS | tighter crack; 100 ms so 10 a second stays clean |
| `DryFire` | 60 ms | -6.0 dBFS | dull click |
| `ReloadMagOut` | 110 ms | -4.4 dBFS | slide and release click (15 % into the reload) |
| `ReloadMagIn` | 140 ms | -3.7 dBFS | slide and solid seat (60 %) |
| `ReloadSlide` | 130 ms | -3.7 dBFS | pistol slide: back click, forward clack (85 %) |
| `ReloadCharge` | 210 ms | -3.7 dBFS | rifle charging handle: pull, heavy return (85 %) |
| `ReloadFinished` | 60 ms | -8.0 dBFS | small settled click |
| `Swap` | 220 ms | -6.0 dBFS | swish and catch |
| `ImpactWorld` | 130 ms | -3.1 dBFS | hard tick and debris |
| `ImpactBody` | 120 ms | -3.1 dBFS | dull thud 170 to 85 Hz |
| `GlassShatter` | 700 ms | -1.4 dBFS | crack, 26 falling glass partials, bright wash |
| `Hurt` | 220 ms | -4.4 dBFS | low pulse 120 to 75 Hz, 2D |
| `Death` | 750 ms | -4.4 dBFS | tone falling E4 to C3 over a low drop, 2D |
| `Respawn` | 520 ms | -6.0 dBFS | C5 to G5 rise with air, 2D |

## Regenerating

```bash
python3 ArtSource/Audio/ui/make_ui_sounds.py            # writes the 31 UI WAVs the 12 item WAVs and the 15 weapon WAVs, prints peak and RMS
python3 ArtSource/Audio/ui/make_ui_sounds.py --table    # also prints this table as markdown
```

The output is deterministic (fixed noise seeds): two runs give byte-identical files. Edit the `EVENTS` table (notes, durations, timbre) and re-run, then run **Set Up Sound** if clips were added. That menu only adds: it sets a new clip's import settings once, adds entries for new events, and leaves existing entries, hand-tuned values and the mixer alone.

## Not in this set

Gun shots, reloads, impacts and glass are a later weapons sound pass (see WEAPONS.md). Music and voice have buses but no content yet.
