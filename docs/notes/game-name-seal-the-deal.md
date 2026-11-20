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
- 2026-11-20, text pass on `Alex_Env`: docs, READMEs, the art bible, the editor menu root (now `Seal the Deal >`), component and asset menus, log hints and the generated title text now say "Seal the Deal". Records stay as written: generation prompts, `REQUEST.md` files, research notes and quoted commit subjects.
- Still old on purpose, for one dedicated change when no lanes are open: the C# namespace `CloseTheDeal.*` (serialized in prefabs and scenes), the shader names `Close the Deal/…` (code finds them by name, so a branch that adds a lookup with the old name would silently get no shader), `productName: CloseTheDeal` (changing it moves players' save folder), `docs/GDD.md` (owner-only), the "Close the Deal patch" markers in `Plugins/FishySteamworks`, and title text already saved in scenes (it updates when the UI is rebuilt).
- A branch that still adds menu items under `Close the Deal/` will show a second menu root after merging: rename them to `Seal the Deal/` when merging.
- The tagline "Terms and Conditions Apply." was not changed by this decision.
