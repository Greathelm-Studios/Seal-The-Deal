#!/usr/bin/env node
// AI route: generate a motion from text with the Uthana API, download it as GLB, then retarget it
// with retarget.py --source <file> --clips all. UNTESTED: written from Uthana's docs (Oct 2026)
// without an account or any API call. See references/ai-route.md before first use.
//
//   node ai_generate.mjs --prompt "a person vaulting over a low wall" [--length 4] [--fps 30]
//                        [--in-place] [--model 2.0|3.0] [--out <file.glb>] [--confirm-spend]
//
// Without --confirm-spend it is a dry run: it prints the request and the estimated cost and makes
// no network call. Generation is billed per second of output, so only pass --confirm-spend when the
// user has approved that cost. Needs UTHANA_API_KEY in the environment (never on the command line).
import { mkdir, writeFile } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { homedir } from 'node:os';

const API = 'https://uthana.com';
const DEFAULT_CHARACTER = 'cXi2eAP19XwQ'; // Uthana's default character "Tar", per their quickstart
// Billed rates from uthana.com/pricing (checked 2026-10-16). Re-check before relying on them.
const MODELS = {
  '2.0': { graphql: 'text-to-motion-bucmd', usdPerSecond: 0.03, minLen: 0.25, maxLen: 10 },
  '3.0': { graphql: 'text-to-motion-3.0', usdPerSecond: 0.1, minLen: 4, maxLen: 10 },
};

function parse(argv) {
  const o = { prompt: null, length: 4, fps: 30, inPlace: false, model: '2.0', out: null, confirm: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--prompt') o.prompt = argv[++i];
    else if (a === '--length') o.length = Number(argv[++i]);
    else if (a === '--fps') o.fps = Number(argv[++i]);
    else if (a === '--in-place') o.inPlace = true;
    else if (a === '--model') o.model = argv[++i];
    else if (a === '--out') o.out = argv[++i];
    else if (a === '--confirm-spend') o.confirm = true;
    else throw new Error(`Unknown argument ${a}`);
  }
  if (!o.prompt) throw new Error('--prompt is required');
  const m = MODELS[o.model];
  if (!m) throw new Error(`--model must be one of ${Object.keys(MODELS).join(', ')}`);
  if (!(o.length >= m.minLen && o.length <= m.maxLen)) throw new Error(`--length must be ${m.minLen}-${m.maxLen} s for model ${o.model}`);
  if (![24, 30, 60].includes(o.fps)) throw new Error('--fps must be 24, 30 or 60 (Uthana download options)');
  const cache = process.env.CHARACTER_ANIMATION_CACHE || join(homedir(), '.cache', 'character-animation');
  const slug = o.prompt.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '').slice(0, 40);
  o.out = o.out || join(cache, 'ai', `${slug}.glb`);
  return o;
}

const auth = (key) => ({ Authorization: `Basic ${Buffer.from(`${key}:`).toString('base64')}` });

async function graphql(key, query, variables) {
  const res = await fetch(`${API}/graphql`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...auth(key) },
    body: JSON.stringify({ query, variables }),
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok || json.errors) throw new Error(`Uthana GraphQL error (HTTP ${res.status}): ${JSON.stringify(json.errors || json)}`);
  return json.data;
}

async function generate(key, o) {
  const m = MODELS[o.model];
  if (o.model === '3.0') {
    // Asynchronous: create a job, then poll until FINISHED or FAILED.
    const d = await graphql(key,
      'mutation($p: String!, $m: String!, $l: Float) { create_text_to_motion_job(prompt: $p, model: $m, length: $l) { job { id status } } }',
      { p: o.prompt, m: m.graphql, l: o.length });
    const id = d.create_text_to_motion_job.job.id;
    console.log(`Created job ${id}; polling.`);
    for (let i = 0; i < 120; i++) {
      await new Promise((r) => setTimeout(r, 5000));
      const j = (await graphql(key, 'query($id: String!) { job(job_id: $id) { id status result } }', { id })).job;
      if (j.status === 'FINISHED') { console.log(`Job finished: ${JSON.stringify(j.result)}`); return j.result?.result?.id; }
      if (j.status === 'FAILED') throw new Error(`Uthana job failed: ${JSON.stringify(j.result)}`);
    }
    throw new Error('Uthana job did not finish within 10 minutes');
  }
  const d = await graphql(key,
    'mutation($p: String!, $m: String, $l: Float) { create_text_to_motion(prompt: $p, model: $m, length: $l, foot_ik: true) { motion { id name } } }',
    { p: o.prompt, m: m.graphql, l: o.length });
  return d.create_text_to_motion.motion.id;
}

async function main() {
  const o = parse(process.argv.slice(2));
  const m = MODELS[o.model];
  const cost = (o.length * m.usdPerSecond).toFixed(2);
  const query = new URLSearchParams({ fps: String(o.fps), in_place: String(o.inPlace) });
  console.log(`Prompt: "${o.prompt}"  model ${o.model} (${m.graphql}), ${o.length}s, ${o.fps} fps, ${o.inPlace ? 'in place' : 'with root motion'}`);
  console.log(`Estimated cost: about $${cost} (${o.length}s x $${m.usdPerSecond}/s, per uthana.com/pricing)`);
  console.log(`Output: ${o.out}`);
  if (!o.confirm) {
    console.log('DRY RUN: nothing was sent. Re-run with --confirm-spend once the user has approved the cost.');
    return;
  }
  const key = process.env.UTHANA_API_KEY;
  if (!key) throw new Error('UTHANA_API_KEY is not set. See references/ai-route.md.');
  const motionId = await generate(key, o);
  if (!motionId) throw new Error('Uthana returned no motion id');
  console.log(`Generated motion ${motionId} (billed); downloading.`);
  const url = `${API}/motion/file/motion_viewer/${DEFAULT_CHARACTER}/${motionId}/glb/motion.glb?${query}`;
  const res = await fetch(url, { headers: auth(key) });
  if (!res.ok) throw new Error(`Download failed: HTTP ${res.status}`);
  await mkdir(dirname(o.out), { recursive: true });
  await writeFile(o.out, Buffer.from(await res.arrayBuffer()));
  console.log(`Saved ${o.out} (motion ${motionId}).`);
  console.log(`Next: node ca.mjs inspect "${o.out}", then node ca.mjs retarget --source "${o.out}" --clips all --character <your character> --out <work.blend>`);
}

main().catch((e) => {
  console.error(`ERROR: ${e.message}`);
  process.exitCode = 1;
});
