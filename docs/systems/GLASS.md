<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex: "i also wanna add a stylised reflection or sheen to the windows/glass"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the kit's glass (AGENTS.md §7): what the player sees, where its
              files are, how to tune it, and what has not been checked.
-->
# Glass

**Status:** Current (on the kit's one glass material; not judged in play) · **Last updated:** 2026-11-12

Glass is almost clear, with a pair of hard diagonal light streaks, one wide and one thin, drawn across each pane.
The streaks slide over the pane as the viewer walks past, the way a reflection would, and the glass goes pale and more solid when seen nearly edge-on.
Nothing is actually reflected: the art bible rules out mirror glass because players have to read each other through two panes.

## Files

- Shader: [GlassSheen.shader](../../Assets/_Project/Art/Glass/GlassSheen.shader), `Close the Deal/Glass Sheen`. Unlit and transparent; it casts no shadow, so sunlight passes through onto the floor.
- Material: [Glass_Clear.mat](../../Assets/_Project/Art/Kit/Materials/Glass_Clear.mat), used by every window, glass partition and stair-cage panel in the kit.
- [KitImport.cs](../../Assets/_Project/Editor/Art/KitImport.cs) gives the glass material this shader on import. It sets only the tint, so streak and edge settings changed in the Inspector survive a re-import.

## Tuning

| Inspector value | What it does |
|---|---|
| Glass tint | Colour, and in its alpha how solid the pane is. Keep the alpha near 0.05. |
| Streak colour | Colour, and in its alpha how strong the streaks are. |
| Streak tilt | Degrees from vertical. |
| Distance between streak pairs | Metres along the glass. Larger = fewer streaks. |
| Width of the wide / thin streak | Metres. Set the thin one to 0 for a single streak. |
| How far the streaks slide | 0 paints them onto the glass; higher makes them drift as the viewer moves. |
| Colour at grazing angles, and how close to edge-on | The pale edge. |

## Known limitations

- **A streak can cross a player.** From across the street a streak sometimes sits over someone standing behind the glass. They stay readable through it at the default strength, but it is a visibility cost; streak strength and spacing are the controls.
- **It assumes panes are thin closed boxes**, as the kit's are, and draws only the face turned toward the viewer. A pane modelled as a single one-sided sheet would be invisible from behind.
- **Not checked:** Play Mode, how the sliding looks in motion, and the stair cage's curved panels up close. Only still renders were looked at.
