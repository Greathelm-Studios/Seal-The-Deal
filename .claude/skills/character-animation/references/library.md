# Library sources

`node scripts/ca.mjs setup` re-creates everything below in the cache folder (`~/.cache/character-animation`, or `CHARACTER_ANIMATION_CACHE`). Nothing here needs an account or payment: on itch.io's pay-what-you-want pages the script takes the same route as the "No thanks, just take me to the downloads" link.

| Pack | Page | File fetched | Licence |
|---|---|---|---|
| Universal Animation Library 1 (Quaternius) | https://quaternius.itch.io/universal-animation-library | `Universal Animation Library[Standard].zip` (16 MB) | CC0 1.0 |
| Universal Animation Library 2 (Quaternius) | https://quaternius.itch.io/universal-animation-library-2 | `Universal Animation Library 2[Standard].zip` (19 MB), includes a female mannequin | CC0 1.0 |
| Test character, optional (`--test-character`) | https://quaternius.itch.io/lowpoly-animated-knight | `Knight Character Animated by Quaternius.zip` (8.5 MB) | CC0 1.0 |

Each pack ships an in-place file (`UALn_Standard.glb`) and a root-motion file (`UALn_Standard_RM.glb`); the catalogue points at both. The Standard tier is the free one; Pro and Source tiers are paid and not needed.

Downloaded files are treated as data: they are unpacked and imported, never executed. Clips can also be browsed at https://quaternius.com/animviewer.html.

## Catalogue fields

`<cache>/catalogue.json`: `id` (`ual1/Walk_Loop`), `name`, `pack`, `frames`, `fps` (30), `duration_s`, `loop` (name ends in Loop), `pose_closes` (first and last pose match within 3 degrees), `tags`, `root_motion_available`, `root_travel_m`, `speed_m_s`, `file_inplace`, `file_root_motion`. The file holds absolute paths for this machine; rebuild it with `node scripts/ca.mjs catalogue` after moving the cache.
