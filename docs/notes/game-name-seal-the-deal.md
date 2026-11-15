---
name: game-name-seal-the-deal
description: The game is called "Seal the Deal", not "Close the Deal"; use it in everything a player or the store sees
metadata:
  type: project
---

The game's name is **Seal the Deal**. "Close the Deal" was the working title and is wrong from 2026-11-12 on.

Adam said so on 2026-11-12, in capitals: "MAJOR UPDATE THE NAME IS SEAL THE DEAL IT NEEDS TO BE DRILLED INTO MEMOERY".

**Why:** the owner renamed the game. The repo and its folder were already `Seal-The-Deal`; the docs, the art bible and the code still carry the old title.

**How to apply:**

- Every player-facing or store-facing string says "Seal the Deal": the title lockup ("SEAL THE DEAL"), menus, UI mockups, window title, product name, store copy, marketing, new docs.
- Never write "Close the Deal" in new work. If a brief, doc or reference says it, read it as "Seal the Deal" and report the stale text.
- Still carrying the old name on 2026-11-12, to be changed in their own pass rather than piecemeal: `AGENTS.md`, `CLAUDE.md`, `docs/GDD.md` (owner-only), the art bible text in `ArtSource/Kit/concepts/Office/refs/art_bible.txt`, the C# namespace `CloseTheDeal.*`, the editor menu root `Close the Deal >`, and the shader names `Close the Deal/…`. Do not rename the namespace, menus or shaders inside a feature branch: it touches every file and breaks merges. Do it as one dedicated change when no lanes are open.
- The tagline "Terms and Conditions Apply." was not changed by this decision.
