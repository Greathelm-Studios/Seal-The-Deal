#!/usr/bin/env node
// One entry point for the character-animation skill. Finds Blender, runs the Blender scripts in
// this folder headless, and searches the clip catalogue. Never prompts, so agents can run it.
//
//   node ca.mjs setup [--test-character]      download the library and build the catalogue
//   node ca.mjs blender                       print the Blender that will be used
//   node ca.mjs catalogue                     rebuild <cache>/catalogue.json
//   node ca.mjs search <words...> [--loop] [--tag t] [--limit n] [--json]
//   node ca.mjs inspect <character> [--json out.json]
//   node ca.mjs retarget --character <file> --clips a,b,c --out <work.blend> [...]   (--add-to <work.blend> instead of --character adds to a work file)
//   node ca.mjs export --blend <work.blend> --engine unreal|unity|godot --out <dir> [--split] [--bone-names ue|mixamo|cc] [--no-mesh|--mesh-only]
//   node ca.mjs verify --manifest <dir>/manifest.json
//   node ca.mjs preview --file <file> --out <dir> [--clips a,b] [--frames 6] [--mp4 [--follow]] [--first-person] [--prop p.glb --hand r]
//   node ca.mjs edit --blend <work.blend> list|trim|retime|mirror|transition|layer|pose|loop|foot-lock|hold|ik|delay|exaggerate|aim|directions|style [...]
//   node ca.mjs ai --prompt "..." [--length 4] [--in-place] [--confirm-spend]   (paid; dry run by default)
//
// Environment: BLENDER_PATH (Blender executable), CHARACTER_ANIMATION_CACHE (library cache).
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, dirname, delimiter } from 'node:path';
import { homedir } from 'node:os';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const cacheDir = () => process.env.CHARACTER_ANIMATION_CACHE || join(homedir(), '.cache', 'character-animation');

// Version-aware sort: "Blender 5.2" > "Blender 4.10" > "Blender 4.2".
const versionKey = (s) => (s.match(/\d+(\.\d+)*/)?.[0] ?? '0').split('.').map(Number);
const newestFirst = (a, b) => {
  const [x, y] = [versionKey(a), versionKey(b)];
  for (let i = 0; i < Math.max(x.length, y.length); i++) if ((y[i] ?? 0) !== (x[i] ?? 0)) return (y[i] ?? 0) - (x[i] ?? 0);
  return 0;
};

export function findBlender() {
  const env = process.env.BLENDER_PATH;
  if (env) {
    if (existsSync(env)) return env;
    throw new Error(`BLENDER_PATH is set to ${env}, which does not exist`);
  }
  const exe = process.platform === 'win32' ? 'blender.exe' : 'blender';
  for (const dir of (process.env.PATH || '').split(delimiter)) {
    if (dir && existsSync(join(dir, exe))) return join(dir, exe);
  }
  const candidates = [];
  const scan = (root, sub) => {
    if (!root || !existsSync(root)) return;
    for (const name of readdirSync(root).filter((n) => /blender/i.test(n)).sort(newestFirst)) {
      const p = join(root, name, ...sub);
      if (existsSync(p)) candidates.push(p);
    }
  };
  if (process.platform === 'win32') {
    for (const pf of [process.env.ProgramFiles, process.env['ProgramFiles(x86)'], process.env.LOCALAPPDATA && join(process.env.LOCALAPPDATA, 'Programs')]) {
      if (pf) scan(join(pf, 'Blender Foundation'), [exe]);
    }
    // Steam installs.
    scan(join(process.env['ProgramFiles(x86)'] || 'C:\\Program Files (x86)', 'Steam', 'steamapps', 'common'), [exe]);
  } else if (process.platform === 'darwin') {
    for (const p of ['/Applications/Blender.app', join(homedir(), 'Applications', 'Blender.app')]) {
      const bin = join(p, 'Contents', 'MacOS', 'Blender');
      if (existsSync(bin)) candidates.push(bin);
    }
  } else {
    for (const p of ['/snap/bin/blender', '/usr/bin/blender', '/usr/local/bin/blender', '/opt/blender/blender']) {
      if (existsSync(p)) candidates.push(p);
    }
    scan('/opt', ['blender']);
    scan(join(homedir(), 'Applications'), ['blender']);
  }
  if (candidates.length) return candidates[0];
  throw new Error('Blender not found. Install it (Windows: winget install --id BlenderFoundation.Blender) or set BLENDER_PATH.');
}

function runBlender(script, args) {
  const blender = findBlender();
  // --python-exit-code: without it Blender exits 0 even when the script raises.
  // Arguments go in CA_ARGS, not after '--': Blender's add-ons parse those too, and Cycles exits on
  // anything that could abbreviate one of its options (such as --cycle).
  const r = spawnSync(blender, ['-b', '--factory-startup', '-noaudio', '--python-exit-code', '1', '-P', join(HERE, script)], {
    env: { ...process.env, CA_ARGS: JSON.stringify(args) },
    stdio: ['ignore', 'pipe', 'pipe'],
    encoding: 'utf8',
    maxBuffer: 256 * 1024 * 1024,
  });
  // Blender is chatty; pass through our own lines and anything that looks like a problem.
  const keep = (line) =>
    line &&
    !/^(Blender \d|Blender quit|Read prefs|Read blend|Warning: Unable to open|Color management|\s*$|Fra:|Saved: |\s+Time:|found bundled python|\d\d:\d\d:\d\d \| INFO|[\d:.]+\s+\w+\s+\| (Saved|Read blend|Video append)|FBX version|FBX export|\s*[Ee]xport finished|INFO (Draco|MeshOptimizer)|Info: )/.test(line);
  const out = (r.stdout || '').split(/\r?\n/).filter(keep);
  if (out.length) console.log(out.join('\n'));
  const err = (r.stderr || '').split(/\r?\n/).filter((l) => l.trim());
  if (err.length) console.error(err.join('\n'));
  if (r.error) throw r.error;
  return r.status ?? 1;
}

function search(argv) {
  const words = [];
  const opt = { loop: null, tags: [], limit: 15, json: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--loop') opt.loop = true;
    else if (a === '--no-loop') opt.loop = false;
    else if (a === '--tag') opt.tags.push(argv[++i].toLowerCase());
    else if (a === '--limit') opt.limit = Number(argv[++i]);
    else if (a === '--json') opt.json = true;
    else words.push(...a.toLowerCase().split(/[\s,]+/).filter(Boolean));
  }
  const file = join(cacheDir(), 'catalogue.json');
  if (!existsSync(file)) throw new Error(`No catalogue at ${file}. Run: node ca.mjs setup`);
  const { clips } = JSON.parse(readFileSync(file, 'utf8'));
  const scored = clips
    .filter((c) => opt.loop === null || c.loop === opt.loop)
    .filter((c) => opt.tags.every((t) => c.tags.includes(t)))
    .map((c) => {
      const hay = [c.name.toLowerCase(), ...c.tags];
      let score = words.length ? 0 : 1;
      for (const w of words) {
        if (c.name.toLowerCase().split(/[_\s]/).includes(w)) score += 3;
        else if (c.name.toLowerCase().includes(w)) score += 2;
        if (c.tags.includes(w)) score += 2;
        else if (hay.some((h) => h.includes(w))) score += 1;
      }
      return { c, score };
    })
    .filter((x) => x.score > 0)
    .sort((a, b) => b.score - a.score || a.c.name.localeCompare(b.c.name))
    .slice(0, opt.limit);
  if (opt.json) return console.log(JSON.stringify(scored.map((x) => x.c), null, 2));
  if (!scored.length) return console.log('No matching clips. Try other words, or the AI route (references/ai-route.md).');
  for (const { c } of scored) {
    const motion = c.speed_m_s ? `, root motion ${c.speed_m_s} m/s` : '';
    console.log(`${c.name.padEnd(30)} ${c.pack}  ${c.duration_s.toFixed(2)}s ${String(c.frames).padStart(4)}f  ${c.loop ? 'loop' : 'once'}${motion}  [${c.tags.join(', ')}]`);
  }
}

const SCRIPTS = {
  catalogue: 'build_catalogue.py',
  inspect: 'inspect_character.py',
  retarget: 'retarget.py',
  export: 'export.py',
  verify: 'verify_export.py',
  preview: 'preview.py',
  edit: 'edit.py',
};

function main() {
  const [cmd, ...rest] = process.argv.slice(2);
  if (!cmd || cmd === 'help' || cmd === '--help') {
    console.log(readFileSync(fileURLToPath(import.meta.url), 'utf8').split('\n').slice(1, 17).join('\n').replace(/^\/\/ ?/gm, ''));
    return 0;
  }
  if (cmd === 'blender') return console.log(findBlender()), 0;
  if (cmd === 'search') return search(rest), 0;
  if (cmd === 'ai') return spawnSync(process.execPath, [join(HERE, 'ai_generate.mjs'), ...rest], { stdio: 'inherit' }).status ?? 1;
  if (cmd === 'setup') {
    findBlender(); // fail early, before downloading, if Blender is missing
    const r = spawnSync(process.execPath, [join(HERE, 'setup-library.mjs'), ...rest], { stdio: 'inherit' });
    if (r.status !== 0) return r.status ?? 1;
    return runBlender(SCRIPTS.catalogue, []);
  }
  if (cmd === 'inspect' && rest[0] && !rest[0].startsWith('--')) rest.unshift('--file');
  if (SCRIPTS[cmd]) return runBlender(SCRIPTS[cmd], rest);
  console.error(`Unknown command ${cmd}. Run: node ca.mjs help`);
  return 1;
}

try {
  process.exitCode = main() ?? 0;
} catch (e) {
  console.error(`ERROR: ${e.message}`);
  process.exitCode = 1;
}
