// Poly Haven (polyhaven.com) assets for model scripts: free CC0 PBR textures, HDRIs and models, through its public
// API (api.polyhaven.com, no key). Run through mdl.mjs:
//
//   node mdl.mjs polyhaven search <words...> [--type textures|hdris|models] [--limit 10]
//   node mdl.mjs polyhaven get <slug> --out <dir> [--res 1k|2k|4k|8k] [--type textures|hdris|models]
//
// get writes <out>/<slug>/ with the maps (textures: base colour, OpenGL normal, roughness, metallic when the
// material has one, AO), or the .hdr (HDRIs), or the glTF (models), and <slug>.json describing them, which
// modelkit's texture() reads. Downloads are checked against Poly Haven's MD5s and skipped when already there.
// Poly Haven's terms ask API users to identify themselves, hence the User-Agent.
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const API = 'https://api.polyhaven.com';
const HEADERS = { 'User-Agent': 'blender-modeling-skill/1.0' };
const TYPES = ['textures', 'hdris', 'models'];
const TYPE_IDS = ['hdris', 'textures', 'models'];  // Poly Haven's numeric asset types: 0 HDRI, 1 texture, 2 model
// Texture maps we use, by Poly Haven's file key: Unity and Blender both take OpenGL-style normals (Y up).
const TEXTURE_MAPS = { Diffuse: 'base', nor_gl: 'normal', Rough: 'roughness', Metal: 'metallic', AO: 'ao' };

async function api(path) {
  const r = await fetch(`${API}${path}`, { headers: HEADERS });
  if (!r.ok) throw new Error(`Poly Haven ${path}: HTTP ${r.status}`);
  return r.json();
}

function option(args, name, fallback) {
  const i = args.indexOf(`--${name}`);
  if (i < 0) return fallback;
  const value = args[i + 1];
  args.splice(i, 2);
  if (value === undefined) throw new Error(`--${name} needs a value`);
  return value;
}

async function search(args) {
  const type = option(args, 'type', 'textures');
  const limit = Number(option(args, 'limit', '10'));
  if (!TYPES.includes(type)) throw new Error(`--type is ${type}; use one of ${TYPES.join(', ')}`);
  const words = args.map((w) => w.toLowerCase()).filter(Boolean);
  if (!words.length) throw new Error('Say what to search for, such as: polyhaven search rusty metal');
  const assets = await api(`/assets?type=${type}`);
  // Score each asset by how many of the words its name, tags and categories contain.
  const scored = Object.entries(assets).map(([slug, a]) => {
    const hay = [slug, a.name, ...(a.tags || []), ...(a.categories || [])].join(' ').toLowerCase();
    return { slug, a, score: words.filter((w) => hay.includes(w)).length };
  }).filter((x) => x.score > 0)
    .sort((x, y) => y.score - x.score || (y.a.download_count || 0) - (x.a.download_count || 0))
    .slice(0, limit);
  if (!scored.length) return console.log(`No ${type} match "${words.join(' ')}".`), 0;
  for (const { slug, a } of scored) {
    const size = a.dimensions ? `, ${(a.dimensions[0] / 1000).toFixed(2)} x ${(a.dimensions[1] / 1000).toFixed(2)} m` : '';
    console.log(`${slug.padEnd(36)} ${a.name}${size} [${(a.categories || []).slice(0, 4).join(', ')}]`);
  }
  return 0;
}

async function download(url, path, md5) {
  if (existsSync(path) && createHash('md5').update(readFileSync(path)).digest('hex') === md5) return 'kept';
  const r = await fetch(url, { headers: HEADERS });
  if (!r.ok) throw new Error(`${url}: HTTP ${r.status}`);
  const data = Buffer.from(await r.arrayBuffer());
  const got = createHash('md5').update(data).digest('hex');
  if (md5 && got !== md5) throw new Error(`${url}: checksum ${got}, expected ${md5}`);
  writeFileSync(path, data);
  return `${(data.length / 1024 / 1024).toFixed(1)} MB`;
}

async function get(args) {
  const out = option(args, 'out', null);
  const res = option(args, 'res', '1k');
  let type = option(args, 'type', null);
  const slug = args[0];
  if (!slug || !out) throw new Error('Usage: polyhaven get <slug> --out <dir> [--res 1k]');
  const info = await api(`/info/${slug}`).catch(() => null);
  if (!info || info.type === undefined) throw new Error(`No Poly Haven asset called ${slug}; find one with: polyhaven search <words>`);
  type ??= TYPE_IDS[info.type];
  const files = await api(`/files/${slug}`);
  const dir = resolve(out, slug);
  mkdirSync(dir, { recursive: true });
  const meta = { slug, name: info.name, type, resolution: res, license: 'CC0 1.0', source: `https://polyhaven.com/a/${slug}`,
    authors: Object.keys(info.authors || {}), maps: {} };
  const take = async (key, entry, name) => {
    const pick = entry?.[res];
    if (!pick) throw new Error(`${slug} has no ${res} ${key}; it has ${Object.keys(entry || {}).join(', ')}`);
    const file = Object.values(pick)[0];
    const path = join(dir, file.url.split('/').pop());
    const how = await download(file.url, path, file.md5);
    console.log(`  ${name.padEnd(10)} ${path} (${how})`);
    return path;
  };
  if (type === 'textures') {
    meta.size_m = (info.dimensions || [1000, 1000]).map((d) => Math.round(d) / 1000);
    for (const [key, name] of Object.entries(TEXTURE_MAPS)) {
      if (!files[key]) continue;
      const jpg = Object.fromEntries(Object.entries(files[key]).map(([r, f]) => [r, f.jpg ? { jpg: f.jpg } : f]));
      meta.maps[name] = await take(key, jpg, name);
    }
  } else if (type === 'hdris') {
    meta.maps.hdri = await take('hdri', Object.fromEntries(Object.entries(files.hdri).map(([r, f]) => [r, { hdr: f.hdr }])), 'hdri');
  } else {
    const gltf = files.gltf?.[res]?.gltf;
    if (!gltf) throw new Error(`${slug} has no ${res} glTF`);
    meta.maps.gltf = join(dir, gltf.url.split('/').pop());
    console.log(`  gltf       ${meta.maps.gltf} (${await download(gltf.url, meta.maps.gltf, gltf.md5)})`);
    for (const [rel, inc] of Object.entries(gltf.include || {})) {
      const path = join(dir, ...rel.split('/'));
      mkdirSync(resolve(path, '..'), { recursive: true });
      await download(inc.url, path, inc.md5);
    }
  }
  const metaPath = join(dir, `${slug}.json`);
  writeFileSync(metaPath, JSON.stringify(meta, null, 2));
  console.log(`${info.name} (${type}, CC0) -> ${metaPath}`);
  if (type === 'textures') console.log(`  real-world size ${meta.size_m.join(' x ')} m. In a model script: texture('Name', r'${metaPath}')`);
  return 0;
}

export async function polyhaven(args) {
  const [sub, ...rest] = args;
  if (sub === 'search') return search(rest);
  if (sub === 'get') return get(rest);
  throw new Error('Usage: polyhaven search <words...> | polyhaven get <slug> --out <dir> [--res 1k]');
}
