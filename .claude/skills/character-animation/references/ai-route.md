# AI route: Uthana text-to-motion

**Status: untested.** Written from Uthana's public docs (uthana.com/docs/api, read 2026-10-16) without an account, a key or any API call. Expect to fix small details (argument names) on first real use.

## Why Uthana

Its API documents text-to-motion with a length control, FBX and GLB downloads with `fps` (24, 30, 60) and `in_place` options, retargeting onto an uploaded character, and clear per-second prices. The alternative, Viggle's PINOC API (docs.viggle.ai), also does text-to-motion but exports GLB only on Mixamo or MetaHuman skeletons, and its own pages disagree about FBX and API availability. Its Mixamo-skeleton GLB would still work with `retarget --source`, so it is a fallback.

## Enabling it (the user does this)

1. Sign up at https://uthana.com/user/signup and add pay-as-you-go credits (uthana.com/pricing; new subscriptions stopped in August 2026). No free tier is documented.
2. Copy the API key from https://uthana.com/app/account.
3. Store it as a user environment variable, not in a file or a command line. Windows: Settings, System, About, Advanced system settings, Environment Variables, New: `UTHANA_API_KEY`. macOS or Linux: `export UTHANA_API_KEY=...` in the shell profile. Restart the terminal or agent.

## Cost (uthana.com/pricing, 2026-10-16)

Billed per second of generated motion; downloads are free.

| Model | Price | Length |
|---|---|---|
| 1.0 | $0.02/s | (not used by the script) |
| 2.0 (default, synchronous) | $0.03/s | 0.25 to 10 s |
| 3.0 (asynchronous job) | $0.10/s | 4 to 10 s |

A 4-second clip on 2.0 is about $0.12. Always show the dry-run estimate and get the user's approval before `--confirm-spend`.

## Running it

```
node scripts/ca.mjs ai --prompt "a person vaulting over a low wall" --length 4 --in-place            # dry run, prints cost
node scripts/ca.mjs ai --prompt "a person vaulting over a low wall" --length 4 --in-place --confirm-spend
node scripts/ca.mjs inspect <cache>/ai/a_person_vaulting_over_a_low_wall.glb
node scripts/ca.mjs retarget --source <that glb> --clips all --character <character> --out <dir>/work.blend
node scripts/ca.mjs retarget --source <that glb> --clips all --add-to <dir>/work.blend   # or: into a work file that already has library clips
```

With `--add-to`, the AI clip sits in the same work file as the library clips, so `edit` can trim it, loop it, or `transition` from a library idle into it, and one export carries the whole moveset.

Then preview, export and verify as usual. The clip is downloaded on Uthana's default character "Tar" (id `cXi2eAP19XwQ`); its bone names are not documented, so run `inspect` on the download and write a `--source-map` file if it reports missing bones. Uthana can also retarget onto an uploaded character server side (docs: capabilities/retargeting), which would skip local retargeting; not wired in.

## What the script sends

- `POST https://uthana.com/graphql`, HTTP Basic auth with the key as username and an empty password.
- Model 2.0: `create_text_to_motion(prompt, model: "text-to-motion-bucmd", length, foot_ik: true) { motion { id name } }`.
- Model 3.0: `create_text_to_motion_job(prompt, model: "text-to-motion-3.0", length) { job { id status } }`, then polls `job(job_id)` every 5 s until `FINISHED` and reads `job.result.result.id`.
- Download: `GET https://uthana.com/motion/file/motion_viewer/<character>/<motion>/glb/motion.glb?fps=30&in_place=true|false`.

Known doc inconsistency: the 2.0 IK flag is named `internal_ik` in one place and `retargeting_ik` in another; the script leaves it at its default.
