#!/usr/bin/env node
// Downloads the free CC0 animation library (and the optional test character) from itch.io
// into the cache folder, then unpacks each zip into its own folder.
//
//   node setup-library.mjs [--cache <dir>] [--only <pack-id>[,<pack-id>]] [--test-character] [--force]
//
// Uses itch.io's own "No thanks, just take me to the downloads" flow for pay-what-you-want
// pages: nothing is paid and no account is needed. Downloads are untrusted data: they are only
// unpacked here, never executed.
import { mkdir, writeFile, readdir, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { homedir } from 'node:os';
import { execFileSync } from 'node:child_process';

// Each pack: the itch page and the exact upload file name to fetch (the free tier only).
export const PACKS = [
  {
    id: 'ual1',
    page: 'https://quaternius.itch.io/universal-animation-library',
    file: 'Universal Animation Library[Standard].zip',
    licence: 'CC0 1.0 (Quaternius)',
  },
  {
    id: 'ual2',
    page: 'https://quaternius.itch.io/universal-animation-library-2',
    file: /Standard/i,
    licence: 'CC0 1.0 (Quaternius)',
  },
  // Optional: a character on a different (non-UAL) rig, used to test retargeting. Fetched only
  // with --test-character.
  {
    id: 'test-knight',
    page: 'https://quaternius.itch.io/lowpoly-animated-knight',
    file: /Knight/i,
    licence: 'CC0 1.0 (Quaternius)',
    optional: true,
  },
];

const UA = 'Mozilla/5.0 (character-animation skill setup)';

// CHARACTER_ANIMATION_CACHE overrides; default is ~/.cache/character-animation.
export const defaultCache = () =>
  process.env.CHARACTER_ANIMATION_CACHE || join(homedir(), '.cache', 'character-animation');

function parseArgs(argv) {
  const out = { cache: defaultCache(), only: null, force: false, testCharacter: false };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--cache') out.cache = argv[++i];
    else if (argv[i] === '--test-character') out.testCharacter = true;
    else if (argv[i] === '--only') out.only = argv[++i].split(',');
    else if (argv[i] === '--force') out.force = true;
    else throw new Error(`Unknown argument: ${argv[i]}`);
  }
  return out;
}

// Minimal cookie jar: itch ties the CSRF token to its session cookie.
class Session {
  cookies = new Map();
  async request(url, opts = {}) {
    const headers = { 'User-Agent': UA, ...(opts.headers || {}) };
    if (this.cookies.size) headers.Cookie = [...this.cookies].map(([k, v]) => `${k}=${v}`).join('; ');
    let res;
    // itch.io rate-limits bursts of requests (HTTP 429); back off and retry a few times.
    for (let attempt = 0; ; attempt++) {
      res = await fetch(url, { ...opts, headers, redirect: 'follow' });
      if (res.status !== 429 || attempt >= 5) break;
      const wait = Number(res.headers.get('retry-after')) || 30 * (attempt + 1);
      console.log(`[wait] itch.io rate limit, retrying in ${wait}s`);
      await new Promise((r) => setTimeout(r, wait * 1000));
    }
    for (const c of res.headers.getSetCookie?.() ?? []) {
      const [pair] = c.split(';');
      const eq = pair.indexOf('=');
      this.cookies.set(pair.slice(0, eq).trim(), pair.slice(eq + 1).trim());
    }
    if (!res.ok) throw new Error(`${opts.method || 'GET'} ${url} failed: HTTP ${res.status}`);
    return res;
  }
  async post(url, form) {
    return this.request(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'XMLHttpRequest' },
      body: new URLSearchParams(form).toString(),
    });
  }
}

const csrfOf = (html) => html.match(/name="csrf_token" value="([^"]+)"/)?.[1];

async function signedFileUrl(pack) {
  const s = new Session();
  const purchase = await (await s.request(`${pack.page}/purchase`)).text();
  const csrf = csrfOf(purchase);
  if (!csrf) throw new Error(`No CSRF token on ${pack.page}/purchase (page layout changed?)`);
  // Same request the "No thanks, just take me to the downloads" link makes.
  const { url: downloadPage } = await (await s.post(`${pack.page}/download_url`, { csrf_token: csrf })).json();
  if (!downloadPage) throw new Error(`itch.io gave no download page for ${pack.page}`);
  const html = await (await s.request(downloadPage)).text();
  const uploads = [...html.matchAll(/data-upload_id="(\d+)"[\s\S]*?title="([^"]+)" class="name"/g)].map((m) => ({
    id: m[1],
    name: m[2],
  }));
  const match = (n) => (pack.file instanceof RegExp ? pack.file.test(n) : n === pack.file);
  const upload = uploads.find((u) => match(u.name));
  if (!upload) {
    throw new Error(`No upload matching ${pack.file} on ${pack.page}. Available: ${uploads.map((u) => u.name).join(', ')}`);
  }
  const csrf2 = csrfOf(html) || csrf;
  const slug = new URL(pack.page).pathname;
  const fileRes = await s.post(
    `${new URL(pack.page).origin}${slug}/file/${upload.id}?source=game_download&after_download_lightbox=1&as_props=1`,
    { csrf_token: csrf2 },
  );
  const json = await fileRes.json();
  if (!json.url) throw new Error(`itch.io refused the file request: ${JSON.stringify(json.errors || json)}`);
  return { url: json.url, name: upload.name };
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const dlRoot = join(args.cache, 'downloads');
  await mkdir(dlRoot, { recursive: true });
  for (const pack of PACKS) {
    if (args.only && !args.only.includes(pack.id)) continue;
    if (pack.optional && !args.testCharacter && !args.only) continue;
    const dir = join(dlRoot, pack.id);
    if (existsSync(dir) && (await readdir(dir)).length && !args.force) {
      console.log(`[skip] ${pack.id}: ${dir} already populated (use --force to refetch)`);
      continue;
    }
    await mkdir(dir, { recursive: true });
    console.log(`[fetch] ${pack.id}: ${pack.page}`);
    const { url, name } = await signedFileUrl(pack);
    const res = await fetch(url, { headers: { 'User-Agent': UA } });
    if (!res.ok) throw new Error(`Download of ${name} failed: HTTP ${res.status}`);
    const zipPath = join(dir, name.replace(/[\\/:*?"<>|]/g, '_'));
    await writeFile(zipPath, Buffer.from(await res.arrayBuffer()));
    console.log(`[saved] ${zipPath} (${((await stat(zipPath)).size / 1e6).toFixed(1)} MB)`);
    // Windows 10+ ships bsdtar, which unpacks zip files. Call it by full path: Git Bash's GNU tar
    // shadows it on PATH and cannot read zips or drive-letter paths.
    const outDir = join(dir, 'extracted');
    await mkdir(outDir, { recursive: true });
    const tar = process.platform === 'win32' ? join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'tar.exe') : 'tar';
    execFileSync(tar, ['-xf', zipPath, '-C', outDir], { stdio: 'inherit' });
    console.log(`[unzip] ${outDir}`);
  }
  console.log('Done. Next: build the catalogue with build_catalogue.py (see SKILL.md).');
}

main().catch((e) => {
  console.error(`ERROR: ${e.message}`);
  process.exit(1);
});
