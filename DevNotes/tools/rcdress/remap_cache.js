#!/usr/bin/env node
// remap_cache.js - carry the Android dressing colours (rcdress platecol, RrRecCenterDressData) over to the cache layout.
//
// The world's Rec Center now builds from the July 2025 cache layout (RecCenter_Cache_poly.rrsk, same frame as Android
// level7). Its atlas materials' pieces are new records ("G plate" fitted rectangles and "G poly" planar polygons), so the
// Android data, keyed by the old layout's plate centres, no longer matches. For every atlas record of the new layout this
// finds the old plates (same material, same plane) it covers or that cover it and takes the palette entry of the largest
// area (else the nearest coplanar old plate within 12 cm, else the nearest one facing about the same way within 25 cm). Pieces that were dress boxes in the old layout (no plates to
// inherit from) get rules: the cafe tables' tops (the table-top cyan), the cafe back counter (teal body, cream top
// 8.6 cm), the cafe counter's upward faces above 0.5 m (cream).
//
// Numbers only in, numbers only out (record centres as the layout prints them -> palette index).
//
// usage: node remap_cache.js <old layout .rrsk|.bytes> <old RrRecCenterDressData.cs> <new layout .rrsk> <out .cs> [stats.tsv]
'use strict';
const fs = require('fs');
const zlib = require('zlib');

function readText(p) {
  let b = fs.readFileSync(p);
  if (b[0] === 0x1f && b[1] === 0x8b) b = zlib.gunzipSync(b);
  return b.toString('utf8');
}
const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const norm = (a) => { const l = Math.sqrt(dot(a, a)) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };
function qrot(q, v) {
  const [x, y, z, w] = q;
  const t = [2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])];
  return [v[0] + w * t[0] + (y * t[2] - z * t[1]), v[1] + w * t[1] + (z * t[0] - x * t[2]), v[2] + w * t[2] + (x * t[1] - y * t[0])];
}
function kv(p, key) {
  const k = key + '=';
  for (const t of p) if (t.startsWith(k)) return t.substring(k.length);
  return null;
}

function parseRecords(text, mats) {
  const out = [];
  for (let line of text.split('\n')) {
    line = line.replace(/\r$/, '');
    if (!(line.startsWith('G plate ') || line.startsWith('G poly '))) continue;
    const p = line.split(' ');
    if (!mats.has(p[2])) continue;
    const r = { kind: p[1], mat: p[2], key: p[3] + ',' + p[4] + ',' + p[5], c: [+p[3], +p[4], +p[5]] };
    if (r.kind === 'plate') {
      const q = p.slice(6, 10).map(Number);
      const e = (kv(p, 'e') || '0,0').split(',').map(Number);
      r.u = qrot(q, [1, 0, 0]); r.v = qrot(q, [0, 1, 0]); r.n = qrot(q, [0, 0, 1]);
      r.ex = e[0]; r.ey = e[1]; r.area = 4 * e[0] * e[1]; r.poly = null;
    } else {
      const n = norm(kv(p, 'n').split(',').map(Number));
      const o = kv(p, 'o').split(',').map(Number);
      r.n = n;
      const a = Math.abs(n[0]) < 0.9 ? [1, 0, 0] : [0, 1, 0];
      r.u = norm(cross(a, n)); r.v = cross(n, r.u);
      r.poly = [];
      for (let i = 0; i + 2 < o.length; i += 3) {
        const d = sub([o[i], o[i + 1], o[i + 2]], r.c);
        r.poly.push([dot(d, r.u), dot(d, r.v)]);
      }
      let ar = 0;
      for (let i = 0; i < r.poly.length; i++) {
        const [x1, y1] = r.poly[i], [x2, y2] = r.poly[(i + 1) % r.poly.length];
        ar += x1 * y2 - x2 * y1;
      }
      r.area = Math.abs(ar) / 2;
      r.ex = Math.max(...r.poly.map(t => Math.abs(t[0]))); r.ey = Math.max(...r.poly.map(t => Math.abs(t[1])));
    }
    out.push(r);
  }
  return out;
}

function inside(r, pt, margin) {
  const d = sub(pt, r.c);
  const x = dot(d, r.u), y = dot(d, r.v);
  if (Math.abs(x) > r.ex + margin || Math.abs(y) > r.ey + margin) return false;
  if (!r.poly) return true;
  let c = false;
  const n = r.poly.length;
  for (let i = 0; i < n; i++) {
    const [x1, y1] = r.poly[i], [x2, y2] = r.poly[(i + 1) % n];
    if ((y1 > y) !== (y2 > y) && x < (x2 - x1) * (y - y1) / (y2 - y1 + 1e-12) + x1) c = !c;
  }
  if (c) return true;
  for (let i = 0; i < n; i++) {
    const [x1, y1] = r.poly[i], [x2, y2] = r.poly[(i + 1) % n];
    const dx = x2 - x1, dy = y2 - y1, L = dx * dx + dy * dy;
    const t = L < 1e-12 ? 0 : Math.max(0, Math.min(1, ((x - x1) * dx + (y - y1) * dy) / L));
    if ((x - x1 - t * dx) ** 2 + (y - y1 - t * dy) ** 2 <= margin * margin) return true;
  }
  return false;
}
const coplanar = (a, b, tol) => Math.abs(dot(a.n, b.n)) > 0.97 && Math.abs(dot(sub(b.c, a.c), a.n)) < tol;

function parseDressData(cs) {
  const mats = [...cs.match(/Materials\s*=\s*\{([\s\S]*?)\};/)[1].matchAll(/"([^"]+)"/g)].map(m => m[1]);
  const palBlock = cs.match(/Palettes\s*=\s*\{([\s\S]*?)\n\s*\};/)[1];
  const pals = [...palBlock.matchAll(/new\[\]\s*\{([^}]*)\}/g)].map(m => m[1].split(',').map(s => s.trim().replace(/f$/, '')).filter(s => s).map(Number));
  const platesBlock = cs.match(/Plates\s*=\s*\{([\s\S]*?)\n\s*\};/)[1];
  const table = [...platesBlock.matchAll(/"([^"]*)"/g)].map(m => {
    const t = new Map();
    for (const e of m[1].split(';')) {
      const q = e.split(',');
      if (q.length === 4) t.set(q[0] + ',' + q[1] + ',' + q[2], parseInt(q[3], 36));
    }
    return t;
  });
  return { mats, pals, table };
}

function spots(text, prefix) {
  const out = [];
  for (const line of text.split('\n')) {
    if (!line.startsWith('G spot ' + prefix)) continue;
    const p = line.replace(/\r$/, '').split(' ');
    const piv = kv(p, 'pivot'), size = kv(p, 'size');
    out.push({ name: p[2], c: [+p[3], +p[4], +p[5]], pivot: piv ? piv.split(',').map(Number) : null, size: size ? size.split(',').map(Number) : null });
  }
  return out;
}

function main() {
  const [oldPath, csPath, newPath, outPath, statsPath] = process.argv.slice(2);
  if (!outPath) { console.error('usage: node remap_cache.js <old layout> <old DressData.cs> <new layout> <out.cs> [stats.tsv]'); process.exit(2); }
  const { mats, pals, table } = parseDressData(fs.readFileSync(csPath, 'utf8'));
  const oldText = readText(oldPath), newText = readText(newPath);
  const matSet = new Set(mats);
  const old = parseRecords(oldText, matSet), nw = parseRecords(newText, matSet);
  const mi = new Map(mats.map((m, i) => [m, i]));
  const byMat = new Map();
  let oldk = 0;
  for (const r of old) {
    const k = table[mi.get(r.mat)].get(r.key);
    if (k === undefined) continue;
    r.k = k; oldk++;
    if (!byMat.has(r.mat)) byMat.set(r.mat, []);
    byMat.get(r.mat).push(r);
  }
  const CAFE = 'RecCenter_CafeAssets_Mat';
  function entry(m, rgb) {
    const p = pals[mi.get(m)];
    for (let i = 0; i < p.length; i += 3) if ([0, 1, 2].every(j => Math.abs(p[i + j] - rgb[j]) < 0.004)) return i / 3;
    p.push(...rgb);
    return p.length / 3 - 1;
  }
  const kTop = entry(CAFE, [0.212, 0.780, 0.848]), kTeal = entry(CAFE, [0.154, 0.706, 0.773]), kCream = entry(CAFE, [0.787, 0.694, 0.591]);
  const tables = spots(newText, '[Table_Cafe_RecCenter]').concat(spots(newText, '[RecCenter_Cafe_Table]'));
  const back = spots(newText, '[RecCenter_Cafe_BackCounter]'), counter = spots(newText, '[RecCenter_CafeCounter]');
  const inBox = (s, p, pad = 0.02) => [0, 1, 2].every(i => Math.abs(p[i] - s.c[i]) <= s.size[i] / 2 + pad);

  const res = mats.map(() => new Map());
  const how = new Map();
  for (const r of nw) {
    let k = null, why = null;
    if (r.mat === CAFE) {
      for (const t of tables) {
        const pv = t.pivot;
        if (Math.hypot(r.c[0] - pv[0], r.c[2] - pv[2]) < 0.9 && r.c[1] >= pv[1] + 0.70 && r.c[1] <= pv[1] + 0.81) { k = kTop; why = 'table top'; break; }
      }
      if (k === null) for (const s of back) {
        if (!inBox(s, r.c)) continue;
        const top = s.c[1] + s.size[1] / 2;
        if (r.c[1] >= top - 0.086) { k = kCream; why = 'back counter top'; } else { k = kTeal; why = 'back counter body'; }
        break;
      }
      if (k === null) for (const s of counter) {
        if (inBox(s, r.c) && r.n[1] > 0.9 && r.c[1] > 0.5) { k = kCream; why = 'counter top'; break; }
      }
    }
    if (k === null) {
      const votes = new Map();
      for (const o of byMat.get(r.mat) || []) {
        if (Math.abs(o.c[0] - r.c[0]) > 12 || Math.abs(o.c[2] - r.c[2]) > 12) continue;
        if (!coplanar(r, o, 0.015)) continue;
        if (inside(r, o.c, 0.01) || inside(o, r.c, 0.01)) votes.set(o.k, (votes.get(o.k) || 0) + Math.max(o.area, 1e-4));
      }
      if (votes.size) { k = [...votes.entries()].sort((a, b) => b[1] - a[1])[0][0]; why = 'covered'; }
      else {
        let best = null;
        for (const o of byMat.get(r.mat) || []) {
          if (!coplanar(r, o, 0.03)) continue;
          const d = Math.hypot(o.c[0] - r.c[0], o.c[1] - r.c[1], o.c[2] - r.c[2]);
          if (d < 0.12 && (!best || d < best[0])) best = [d, o.k];
        }
        if (best) { k = best[1]; why = 'nearest'; }
        else {
          // A piece the old layout did not cut the same way (curved, finer): the nearest old plate facing about the same
          // way within 25 cm (colours on these atlases come in patches larger than that).
          for (const o of byMat.get(r.mat) || []) {
            if (Math.abs(dot(o.n, r.n)) < 0.7) continue;
            const d = Math.hypot(o.c[0] - r.c[0], o.c[1] - r.c[1], o.c[2] - r.c[2]);
            if (d < 0.25 && (!best || d < best[0])) best = [d, o.k];
          }
          if (best) { k = best[1]; why = 'near'; }
        }
      }
    }
    if (k !== null) res[mi.get(r.mat)].set(r.key, k);
    const hk = r.mat + '\t' + (why || 'none');
    how.set(hk, (how.get(hk) || 0) + 1);
  }

  const f = (v) => { let s = v.toFixed(3).replace(/0+$/, '').replace(/\.$/, ''); if (s === '' || s === '-0') s = '0'; return s + 'f'; };
  const L = [];
  L.push('// GENERATED by DevNotes/tools/rcdress/remap_cache.js: the Android palettes (rcdress platecol on level 7) carried over to the July');
  L.push('// 2025 cache layout (Layouts/RecCenter_Builtin = refdata/apk/jt/RecCenter_Cache_poly.rrsk, same frame). Do not edit by hand.');
  L.push('// Numbers only: per level material a palette of the colours the level shows on its pieces (measured; no texture, UV or vertex');
  L.push('// data is here) and, per "G plate" / "G poly" record of the layout, its centre exactly as the layout prints it -> its palette entry.');
  L.push('namespace SomniumSpace.Worlds.RecRoomDreamport.Places.RecCenter');
  L.push('{');
  L.push('    internal static class RrRecCenterDressData');
  L.push('    {');
  L.push('        /// <summary>The level materials whose pieces are recoloured, in the order of <see cref="Palettes"/> and <see cref="Plates"/>.</summary>');
  L.push('        public static readonly string[] Materials =');
  L.push('        {');
  L.push(mats.map(m => '            "' + m + '"').join(',\n'));
  L.push('        };');
  L.push('');
  L.push('        /// <summary>Per material: r,g,b triples (0..1, the texture\'s stored sRGB x the material colour), the measured plate colours\' cluster means.</summary>');
  L.push('        public static readonly float[][] Palettes =');
  L.push('        {');
  L.push(pals.map(p => { const t = []; for (let i = 0; i < p.length; i += 3) t.push(f(p[i]) + ', ' + f(p[i + 1]) + ', ' + f(p[i + 2])); return '            new[] { ' + t.join(',  ') + ' }'; }).join(',\n'));
  L.push('        };');
  L.push('');
  L.push('        /// <summary>Per material: "x,y,z,k;..." - a "G plate" or "G poly" centre as the layout prints it and its palette entry (base 36).</summary>');
  L.push('        public static readonly string[] Plates =');
  L.push('        {');
  L.push(res.map(d => '            "' + [...d.entries()].map(([key, v]) => key + ',' + v.toString(36)).join(';') + '"').join(',\n'));
  L.push('        };');
  L.push('    }');
  L.push('}');
  fs.writeFileSync(outPath, L.join('\n') + '\n');
  let total = 0; for (const d of res) total += d.size;
  console.log('old plates with an entry: ' + oldk + '; new atlas records: ' + nw.length + '; keys assigned: ' + total + '; table top k=' + kTop + ', teal k=' + kTeal + ', cream k=' + kCream);
  for (const [k, n] of [...how.entries()].sort()) console.log('  ' + k + '\t' + n);
  if (statsPath) {
    const s = ['material\tkind\tkey\tk'];
    for (const r of nw) { const v = res[mi.get(r.mat)].get(r.key); s.push(r.mat + '\t' + r.kind + '\t' + r.key + '\t' + (v === undefined ? '' : v)); }
    fs.writeFileSync(statsPath, s.join('\n') + '\n');
  }
}
main();
