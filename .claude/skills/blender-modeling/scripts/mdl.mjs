#!/usr/bin/env node
// One entry point for the blender-modeling skill. Finds Blender and runs the Blender scripts in this
// folder headless. Never prompts, so agents can run it.
//
//   node mdl.mjs blender                                   print the Blender that will be used
//   node mdl.mjs build <model.py> --out <dir>/name.blend [--set KEY=VALUE ...]
//   node mdl.mjs check --blend <file.blend> [--max-tris N] [--watertight] [--json out.json]
//   node mdl.mjs preview --blend <file.blend> --out <dir> [--cell 480x480] [--colliders] [--uv] [--reference [view=]image ...]
//   node mdl.mjs unwrap --blend <built.blend> --out <dir>/name.blend [--mode atlas|tiling|trim] [--density PX_PER_M]
//                       [--size 1024] [--padding PX] [--lightmap] [--trim Material=v0:v1,...] [--tight]
//   node mdl.mjs bake --blend <built.blend> --out <dir>/name.blend [--size 2048]
//   node mdl.mjs export --blend <file.blend> --out <dir> [--name Name] [--split]
//   node mdl.mjs verify --manifest <dir>/manifest.json
//   node mdl.mjs polyhaven search <words...> [--type textures|hdris|models] [--limit 10]
//   node mdl.mjs polyhaven get <slug> --out <dir> [--res 1k]   free CC0 textures, HDRIs and models from polyhaven.com
//
// Environment: BLENDER_PATH (Blender executable).
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, dirname, delimiter, resolve } from 'node:path';
import { homedir } from 'node:os';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { polyhaven } from './polyhaven.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));

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
  // Arguments go in MDL_ARGS, not after '--': Blender's add-ons parse those too.
  const r = spawnSync(blender, ['-b', '--factory-startup', '-noaudio', '--python-exit-code', '1', '-P', join(HERE, script)], {
    env: { ...process.env, MDL_ARGS: JSON.stringify(args) },
    stdio: ['ignore', 'pipe', 'pipe'],
    encoding: 'utf8',
    maxBuffer: 256 * 1024 * 1024,
  });
  // Blender is chatty; pass through our own lines and anything that looks like a problem.
  const keep = (line) =>
    line &&
    !/^(Blender \d|Blender quit|Read prefs|Read blend|Warning: Unable to open|Color management|\s*$|Fra:|Saved: |\s+Time:|found bundled python|\d\d:\d\d:\d\d \| INFO|[\d:.]+\s+\w+\s+\| (Saved|Read blend)|FBX version|FBX export|\s*[Ee]xport finished|FBX import|Info: )/.test(line);
  const out = (r.stdout || '').split(/\r?\n/).filter(keep);
  if (out.length) console.log(out.join('\n'));
  const err = (r.stderr || '').split(/\r?\n/).filter((l) => l.trim());
  if (err.length) console.error(err.join('\n'));
  if (r.error) throw r.error;
  return r.status ?? 1;
}

const SCRIPTS = { build: 'build.py', check: 'check.py', preview: 'preview.py', unwrap: 'unwrap.py', bake: 'bake.py', export: 'export.py', verify: 'verify.py' };

// Blender on Windows can't use paths past MAX_PATH (260), and the scripts write files inside --out folders, so
// leave room for their names. Without this, a deep temp folder fails halfway with "filename too long".
const PATH_ARGS = new Set(['--script', '--out', '--blend', '--manifest', '--json']);
const MAX_WIN_PATH = 200;

function checkPathLengths(args) {
  if (process.platform !== 'win32') return;
  for (let i = 0; i < args.length - 1; i++) {
    if (!PATH_ARGS.has(args[i])) continue;
    const full = resolve(args[i + 1]);
    if (full.length > MAX_WIN_PATH) {
      throw new Error(`${args[i]} is ${full.length} characters long; Blender on Windows fails past about 260 with the ` +
        `files it writes inside. Use a folder with a path under ${MAX_WIN_PATH} characters, such as %TEMP%\\mdl.\n  ${full}`);
    }
  }
}

function main() {
  const [cmd, ...rest] = process.argv.slice(2);
  if (!cmd || cmd === 'help' || cmd === '--help') {
    console.log(readFileSync(fileURLToPath(import.meta.url), 'utf8').split('\n').slice(1, 17).join('\n').replace(/^\/\/ ?/gm, ''));
    return 0;
  }
  if (cmd === 'blender') return console.log(findBlender()), 0;
  if (cmd === 'polyhaven') return polyhaven(rest);
  if (cmd === 'build' && rest[0] && !rest[0].startsWith('--')) rest.unshift('--script');
  if (SCRIPTS[cmd]) return checkPathLengths(rest), runBlender(SCRIPTS[cmd], rest);
  console.error(`Unknown command ${cmd}. Run: node mdl.mjs help`);
  return 1;
}

const fail = (e) => {
  console.error(`ERROR: ${e.message}`);
  process.exitCode = 1;
};
try {
  const code = main();
  // polyhaven downloads, so it returns a promise; everything else finishes before returning.
  if (code instanceof Promise) code.then((c) => { process.exitCode = c ?? 0; }, fail);
  else process.exitCode = code ?? 0;
} catch (e) {
  fail(e);
}
