// Preview only: the same text rewrite as the world's RrRecCenterDress (palette plates, curtains, chairs, counters, drops),
// in node, so gtscene render can draw the result. Usage: node preview_rewrite.js <layout.rrsk> <RrRecCenterDressData.cs> <out.rrsk>
const fs = require('fs');
const [src, data, out] = process.argv.slice(2);
const cs = fs.readFileSync(data, 'utf8');
const mats = [...cs.match(/Materials =\s*\{([^}]*)\}/)[1].matchAll(/"([^"]+)"/g)].map(m => m[1]);
const pals = [...cs.match(/Palettes =\s*\{([\s\S]*?)\};/)[1].matchAll(/new\[\] \{([^}]*)\}/g)].map(m => m[1].split(',').map(x => parseFloat(x)));
const plates = [...cs.match(/Plates =\s*\{([\s\S]*?)\};/)[1].matchAll(/"([^"]*)"/g)].map(m => m[1]);
const short = { RecCenter_CafeAssets_Mat: 'Cafe', Couch_Minimalist_Brown_Instanced_Mat: 'Couch', RecCenter_Marquee_Mat: 'Marquee', Goblet_Clock_Mat: 'Clock', LockerFountainStageLight_mat: 'Fountain', Quest_Poster_Case_Mat: 'PosterCase', RecCenter_ProjectionScreen_Mat: 'Screen' };
const wood = c => c[0] > c[1] && c[1] > c[2] && c[0] - c[2] > 0.25 && c[0] > 0.35 && c[0] < 0.85;
const names = {}, lut = {};
mats.forEach((m, i) => { const p = pals[i]; names[m] = []; for (let k = 0; k < p.length / 3; k++) { const c = p.slice(k * 3, k * 3 + 3); names[m].push('RcPal' + (wood(c) ? '_Wood' : '') + '_' + short[m] + '_' + k); }
  lut[m] = new Map(plates[i].split(';').map(e => { const q = e.split(','); return [q.slice(0, 3).join(' '), parseInt(q[3], 36)]; })); });
const qmul = (a, b) => [a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1], a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0], a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3], a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]];
const rot = (q, v) => { const [x, y, z, w] = q; const tx = 2 * (y * v[2] - z * v[1]), ty = 2 * (z * v[0] - x * v[2]), tz = 2 * (x * v[1] - y * v[0]); return [v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx)]; };
const ez = d => { const a = d * Math.PI / 360; return [0, 0, Math.sin(a), Math.cos(a)]; };
const N = v => (Math.round(v * 1e4) / 1e4).toString();
const chairs = [[-6.486, -0.6854, 11.307, 0.0034, 0.7071, 0.0019, 0.7071], [-0.052, 3.143, -18.942, 0, -0.8816, 0, 0.472], [-17.265, 0.097, 8.259, 0, -0.4047, 0, 0.9145], [-16.382, 0.097, 6.404, 0, 0.107, 0, 0.9943], [1.676, 3.169, -18.71, 0, -0.6495, 0, 0.7604], [-22.02, 0.067, -3.445, 0, -0.5308, 0, -0.8475], [-21.838, 0.077, -0.161, 0, 0.4997, 0, -0.8662], [-7.814, -0.6854, 12.33, 0.0038, 1, -0.0011, 0], [-23.835, 0.087, -3.272, 0, 0.9355, 0, 0.3532]];
const parts = [[-0.007, 0.403, 0, 0.378, 0.082, 0.397, 0, 1], [0.351, 0.765, 0, 0.084, 0.322, 0.397, -10, 1], [0.455, 0.655, 0, 0.025, 0.33, 0.42, -12, 0], [0.09, 0.30, 0, 0.42, 0.035, 0.42, 0, 0], [0.056, 0.66, -0.4475, 0.433, 0.025, 0.072, 0, 0], [0.056, 0.66, 0.4475, 0.433, 0.025, 0.072, 0, 0], [-0.2765, 0.33, -0.448, 0.035, 0.33, 0.035, 0, 0], [-0.2765, 0.33, 0.448, 0.035, 0.33, 0.035, 0, 0], [0.401, 0.31, -0.448, 0.035, 0.31, 0.035, 0, 0], [0.401, 0.31, 0.448, 0.035, 0.31, 0.035, 0, 0]];
const drop = new Set(['RecRoomLogo_box', 'reccenter.StaticObjects.Props.Curtains-0_box', 'RecCenter_Chair_LOD0_box', 'Clock_Housing_LOD0_box', 'RecCenter_Cafe_Table_LOD0_box', 'RecCenter_CafeCounter_box', 'RecCenter_Cafe_BackCounter']);
const o = []; let rec = 0;
for (let l of fs.readFileSync(src, 'utf8').split('\n')) {
  const p = l.split(' ');
  if (l.startsWith('G plate ')) { if (p[2] === 'Curtain_Red_Mat') continue; if (lut[p[2]] && lut[p[2]].has(p[3] + ' ' + p[4] + ' ' + p[5])) { p[2] = names[p[2]][lut[p[2]].get(p[3] + ' ' + p[4] + ' ' + p[5])]; l = p.join(' '); rec++; } }
  if (l.startsWith('G dress ') && drop.has(p[2])) continue;
  o.push(l);
}
const pm = (n, c) => `G platemat ${n} 0 0 0 0 0 0 1 col=${c.map(N).join(',')},1 tex=- scale=1,1 inst=0 avg=${c.map(N).join(',')} itex=0`;
mats.forEach((m, i) => names[m].forEach((n, k) => o.push(pm(n, pals[i].slice(k * 3, k * 3 + 3)))));
o.push(pm('RcPal_Wood_Chair', [0.578, 0.4, 0.249]), pm('RcPal_Chair_Cushion', [0.831, 0.831, 0.831]));
for (const c of chairs) { const P = c.slice(0, 3), Q = c.slice(3); for (const q of parts) { const w = rot(Q, q.slice(0, 3)); const r = qmul(Q, ez(q[6]));
  o.push(`G dress RcChair_box ${N(P[0] + w[0])} ${N(P[1] + w[1])} ${N(P[2] + w[2])} ${r.map(N).join(' ')} e=${q.slice(3, 6).map(N).join(',')} c=1,1,1,1 m=${q[7] ? 'RcPal_Chair_Cushion' : 'RcPal_Wood_Chair'}`); } }
fs.writeFileSync(out, o.join('\n'));
console.log('recoloured', rec);
