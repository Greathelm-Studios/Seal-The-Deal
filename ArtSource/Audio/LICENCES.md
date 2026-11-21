# Sound licences

Every recorded sound in the game has one row here before it ships. The sound generator (`ArtSource/Audio/ui/make_ui_sounds.py`) **refuses to use a recording that has no complete row** (source, licence and URL filled in), so a file can't reach the game without one.

**Decided 2026-11-15 (Adam):** the GDD's "no asset-store packs ship" rule now has one exception for sound: UI and system sounds from 効果音ラボ (soundeffect-lab.info) may ship (docs/GDD.md, art section). Other library sounds are still stand-ins until Adam approves their source.

## How to add a sound

1. Only take sources Adam has said yes to, and only the licence named for them below. CC0 needs no credit; CC-BY needs the author named in the game's credits (fill in the Credit column).
2. Keep the original file unchanged at `ArtSource/Audio/recorded/<UI|Items|Weapons|Movement|Ambience>/<the game's file name>`, for example `ArtSource/Audio/recorded/Weapons/Weapon_PistolShot.wav`. It must be a 16- or 24-bit PCM WAV; convert anything else first.
3. Add its row below. "Changes made" says what the generator does (trim, fade, mono, 48 kHz, level), plus anything done by hand.
4. Trims and fades go in `ArtSource/Audio/recorded/edits.json`; then run `python3 ArtSource/Audio/ui/make_ui_sounds.py`. The slot's file in `Assets/_Project/Audio/` is replaced and the audition page shows it as recorded.
5. Keep the licence text or a saved copy of the source page beside the original when the licence asks for it.

## Approved sources

| Source | Licence allowed | Approved by Adam on | Notes |
|---|---|---|---|
| 効果音ラボ (soundeffect-lab.info) | The site's own terms, https://soundeffect-lab.info/agreement/ : free commercial use, credit optional (we credit it anyway), edits allowed; banned: sharing the files on their own or edited copies of them, direct links to the audio, AI training, trademarking a sound. Building them into the game counts as use, not redistribution. | 2026-11-15 | Audition set: `ArtSource/Audio/candidates/soundeffect-lab/` (98 button/system sounds, `index.csv` has each file's source URL, `audition.html` plays them). Kenney's packs were heard and rejected as too artificial. |

## Files

| File | Slot | Source | Author | Licence | URL | Changes made | Credit |
|---|---|---|---|---|---|---|---|
| `UI_HoverMove.wav` | HoverMove | 効果音ラボ (soundeffect-lab.info), cursor7 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (cursor7) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_Focus.wav` | Focus | 効果音ラボ (soundeffect-lab.info), cursor7 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (cursor7) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json`, -4 dB under hover | 効果音ラボ (optional; credited) |
| `UI_Select.wav` | Select | 効果音ラボ (soundeffect-lab.info), decision30 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision30) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json`, tail cut at 1.0 s | 効果音ラボ (optional; credited) |
| `UI_Confirm.wav` | Confirm | 効果音ラボ (soundeffect-lab.info), decision3 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision3) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_TransitionFrameZoom.wav` | TransitionFrameZoom | 効果音ラボ (soundeffect-lab.info), decision4 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision4) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_PanelOpen.wav` | PanelOpen | 効果音ラボ (soundeffect-lab.info), decision33 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision33) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_Transition.wav` | Transition | 効果音ラボ (soundeffect-lab.info), decision27 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision27) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json`, cut from 3.5 s to 1.2 s | 効果音ラボ (optional; credited) |
| `UI_TabChange.wav` | TabChange | 効果音ラボ (soundeffect-lab.info), decision22 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision22) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_Notification.wav` | Notification | 効果音ラボ (soundeffect-lab.info), warning1 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (warning1) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_PlayerJoined.wav` | PlayerJoined | 効果音ラボ (soundeffect-lab.info), decision35 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision35) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_PlayerLeft.wav` | PlayerLeft | 効果音ラボ (soundeffect-lab.info), decision40 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision40) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_Tip.wav` | Tip | 効果音ラボ (soundeffect-lab.info), decision42 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision42) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
| `UI_Back.wav` | Back | 効果音ラボ (soundeffect-lab.info), decision12 | 効果音ラボ | 効果音ラボ terms, https://soundeffect-lab.info/agreement/ | https://soundeffect-lab.info/sound/button/ (decision12) | MP3 to 16-bit WAV 48 kHz; trim, fade and level per `recorded/edits.json` | 効果音ラボ (optional; credited) |
