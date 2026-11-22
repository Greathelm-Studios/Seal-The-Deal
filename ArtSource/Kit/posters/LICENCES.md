# Poster art from outside the project

Every poster picture in [art/](art/) is a wallpaper from outside the project; none is AI-generated (Alex, 2026-11-22: "ONLY use aero archive wallpapers no ai").
The GDD says shipped art is original, so each row is a stand-in until Adam decides whether outside art may ship (see the Release risks in [docs/WORKBOARD.md](../../../docs/WORKBOARD.md)).

All of them come from [Frutiger Aero Wallpapers](https://knightanni.itch.io/frutiger-aero-wallpapers) by **KnightAnNi** (itch.io), the 4K pack `Frutiger_Aero_4k.zip`, downloaded 2026-11-22.
Licence: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). The page also says no generative AI was used.
Each one is a 4:3 window of the 3840 × 2160 original scaled to 1024 × 768: 2880 × 2160 starting at the x given, except LOYALTY, which is a closer 1920 × 1440 window starting at (1920, 720).
The pack has nine wallpapers for twelve posters, so three are used twice with a different window.

| File | Poster | Source image | Crop starts at x |
|---|---|---|---|
| `art/Poster_Art_01.png` | TEAMWORK | `frutigerAero_Wallpaper_Bubbles_Blue_noRays_4k.jpg` | 960 |
| `art/Poster_Art_02.png` | SYNERGY | `frutigerAero_Wallpaper_Bubbles_Blue_4k.jpg` | 960 |
| `art/Poster_Art_03.png` | OVERTIME | `frutigerAero_Wallpaper_Bubbles_Purple_noRays_4k.jpg` | 960 |
| `art/Poster_Art_04.png` | PASSION | `frutigerAero_Wallpaper_Hearts_4k.jpg` | 480 |
| `art/Poster_Art_05.png` | OPPORTUNITY | `frutigerAero_Wallpaper_Bubbles_Yellow_noRays_4k.jpg` | 960 |
| `art/Poster_Art_06.png` | FLEXIBILITY | `frutigerAero_Wallpaper_Green_Person_4k.jpg` | 480 |
| `art/Poster_Art_07.png` | INNOVATION | `frutigerAero_Wallpaper_Bubbles_Purple_4k.jpg` | 960 |
| `art/Poster_Art_08.png` | BALANCE | `frutigerAero_Wallpaper_Hearts_4k.jpg` | 960 |
| `art/Poster_Art_09.png` | FAMILY | `frutigerAero_Wallpaper_Green_NoPerson_4k.jpg` | 480 |
| `art/Poster_Art_10.png` | MORALE | `frutigerAero_Wallpaper_Bubbles_Yellow_4k.jpg` | 480 |
| `art/Poster_Art_11.png` | EXCELLENCE | `frutigerAero_Wallpaper_Green_NoPerson_4k.jpg` | 960 |
| `art/Poster_Art_12.png` | LOYALTY | `frutigerAero_Wallpaper_Bubbles_Blue_4k.jpg` | 1920 (closer window) |

After changing a picture, run `make_posters.py`, then copy `../signs/textures/Poster_NN.png` over `Assets/_Project/Art/Signs/Poster_NN.png` (or rebuild the scene): the game shows the copy in `Assets`.

CC0 needs no credit, but crediting KnightAnNi in the game's credits is a courtesy.
