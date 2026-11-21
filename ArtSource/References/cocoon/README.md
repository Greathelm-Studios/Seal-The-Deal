# Cocoon Shell (AYN Thor): reference for how a finished menu UI is built

Adam's reference point (2026-11-15): "modern UI looks like that in all aspects of this world, look at Cocoon's AYN Thor as a reference point".
This is the craft to learn from, not a look to copy. Nothing here ships; no images are saved here.

**What it is:** a free, open-source Android frontend for retro handhelds, with single- and dual-screen layouts. It ships on the AYN Thor.
- Site: https://cocoon-shell.com/ · GitHub: https://github.com/inssekt/CocoonFE
- Design is by lnkd, development by Moth, sound and music by Anti.
- Interview: https://gardinerbryant.com/building-cocoon-an-interview-with-the-devs/

**Its stated influences are close to ours:**
- the 3DS home screen, "as if Nintendo had kept iterating";
- Frutiger Aero, Xbox 360, Windows Phone tiles, Arc, Pebble.

The design language is called "Silk UI 2". A flatter direction was tried and rejected, so the softness is deliberate.

## The craft that makes it read as a product (the rules to take)

1. **Tokens, applied everywhere.**
   - Radii: panels 20, groups 16, small parts 8 to 12, pills 999.
   - A three-step shadow scale.
   - Raised parts get a "pillowy" bevel: a top inset highlight and a bottom inner shadow.
2. **One signature gradient, not many colours.** Cocoon's is green to teal (`#76d788` to `#00d0b8`). Everything else is near-white `#fafafa` with near-black `#212121` text.
3. **Content is the hero, not decoration.**
   - Real game art fills the screen.
   - A dim adapts to each image.
   - There are almost no lines, labels or ornaments.
4. **A custom icon set**: more than 120 icons in three styles, following the accent colour.
5. **Controller glyphs sit on the buttons themselves**, e.g. a small X on Close. There are no hint bars.
6. **Each screen region has one job**: input on the bottom screen, information on the top.
7. **Motion tells you where things went.**
   - Launch grows the game art across both screens.
   - Suspend collapses the game back into the home screen.
   - The focus cursor is animated.
8. **Sound is its own**: 3DS-style instruments, GameCube-inspired suspend sounds, and seasonal music.
9. **Character in names**: a mascot (Grub), and a bug and nature naming scheme (Pods, Silk, Picnic).
10. **Process**: mockup, then supporter feedback, then build. Many versions per feature on one board, which is our board workflow.

**Critique to avoid:** one reader said it is "starting to feel a bit bloated", and a status-bar redesign had to be walked back. Keep features few.

**Unverified:** that the app uses exactly the site's hex values and fonts (Poppins, Manrope, Inter, Material Symbols Rounded), and the on-device radius values.

## Images to look at (links only)

- Home, light and dark: https://cocoon-shell.com/images/news/3.0/preview1.webp, https://cocoon-shell.com/images/news/3.0/preview2.webp
- Settings: https://cocoon-shell.com/images/news/3.0/settings1.webp
- Glass: https://cocoon-shell.com/images/news/3.0/glass.webp
- Icons: https://cocoon-shell.com/images/news/3.1/icons.webp
- Motion: https://cdn.gardinerbryant.com/content/images/2026/10/game-launch.gif, https://cdn.gardinerbryant.com/content/images/2026/10/suspend-resume.gif
- Designer's mockup, buttons with glyphs, and the rejected flat direction: https://cdn.gardinerbryant.com/content/images/2026/08/image-57.png, image-64.png, image-65.png
