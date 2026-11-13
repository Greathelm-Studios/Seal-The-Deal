# Bean Battles: lobby and customisation research

**Date:** 2026-11-12 · **Status:** research for the lobby lane; nothing here is a design decision (the GDD owns design).

## Which game

**Bean Battles** by Gupa Games, Steam app 765410, released 22 Aug 2018, Unity, £0.89, "Very Positive" (94% of 6,084 English reviews on the day read).
The store page calls it a battle royale cut down to "the starting zone": several short rounds, 2 to 16 players, kills count as much as surviving.
This is the game the owner means by "bean battle": it is the only Steam game of that name, and it is a friend-group shooter with a customisable character, which is what the brief asks about.

## What was verified, and from where

| Finding | Source | Status |
|---|---|---|
| The host decides the rules: weapons and equipment, player count, number of rounds | Store page, "Customization" | Verified |
| "Dozens of unlockable cosmetic items" | Store page | Verified |
| Cosmetics come as **skins** (whole looks such as "Visor", "Baby"), **titles**, **icons**, and "community hoodies" whose print can be swapped | Gupa patch notes v2.11 and v2.13 | Verified |
| There are **XP and Coins**; events run "3x XP and Coins" | Gupa patch notes v2.13 | Verified that they exist. How fast they are earned and what each buys: **not verified** |
| Some cosmetics are **time-limited** or come from a one-off act (a title for adding an email to the account; a tournament winner's prize item; holiday items) | Patch notes v2.11; community guide, update 1.17 | Verified |
| **Clothing changes the hitbox.** A large Christmas tree cosmetic let players clip through thin walls, and the guide says a bigger outfit makes wall-squeezing easier | Community guide by d3Nk (movement section, update 1.17) | Verified as a player report |
| Matches can be listed or hidden; tournament matches were joinable only by code | Community guide, updates 2.01–2.06 | Verified as a player report |
| The lobby is a place players run around in between rounds | Not found in any page that could be opened | **Not verified.** Steam discussion search returned nothing for "lobby ready"; no page read describes the waiting space |
| How players ready up and who starts the round | Not found | **Not verified.** The store page only says the host picks player count and rounds, which implies the host starts |
| Where customisation happens (a menu or a place in the world) | Not found | **Not verified** |

Pages that could not be used: the Steam achievements page rendered no list without a sign-in, and the Steam discussion search returned no results for either query.

## What to take

- **The host owns the rules and the start.** One person sets the game and presses go. It fits our host-authoritative stack and needs no voting UI.
- **Cosmetics as a few clear categories**, each a whole readable change (a skin, a hoodie print), not sliders. The art bible §07 asks for the same thing.
- **Earned by playing.** Unlocks tied to play give a reason for a second session. We have no XP or currency, so ours counts runs started, which is data that exists today.
- **Short loop back to the waiting space.** Their rounds are minutes long and return players to the same group; our lobby should be somewhere a group is happy to stand in between runs.

## What not to take

- **No cosmetic may change the hitbox or the silhouette class.** Their tree costume became an exploit. Ours change colour and, later, swap parts inside the same silhouette (art bible §07); the collider never reads the outfit.
- **No time-limited or account-linked unlocks** (email sign-up titles, event-only skins). We have no account system and nothing paid; fear-of-missing-out items are out of scope.
- **No currency.** A coin economy needs a server we don't have; a locally stored balance would be trivially edited and would mean nothing.
- **Their content.** Beans, their skins, names, icons, hoodie prints, UI and maps are theirs. We take the pattern only.

## What we do instead where nothing could be verified

The lobby-as-a-place, the ready-up and the customisation corner in this lane follow the GDD ("each run starts in a company reception with the secretary") and the art bible, not Bean Battles: a reception desk for ready and start, a sign-in stand per firm, a tailor's corner for the outfit. See [LOBBY.md](../systems/LOBBY.md) and [CUSTOMISATION.md](../systems/CUSTOMISATION.md).

## Sources

- Store page: https://store.steampowered.com/app/765410/Bean_Battles/
- Gupa Games patch notes v2.13 (15 Jul 2025): https://gupagames.com/news/6876c3586b728ab41235ca90
- Gupa Games patch notes v2.11 (1 Jul 2024): https://gupagames.com/news/6682fb56f57b18e1ef77ad9c
- Community guide "Comprehensive Guide to Bean Battles" by d3Nk: https://steamcommunity.com/sharedfiles/filedetails/?id=1820960790
- SteamDB listing (price, engine, release date): https://steamdb.info/app/765410
