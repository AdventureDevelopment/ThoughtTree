// Statistics of the spike results, shared by the desktop harness (harness/common.mjs) and the on-device runner
// (device.js), so both write the same result format.
export function stats(values) {
  const v = values.filter(x => typeof x === 'number' && Number.isFinite(x)).sort((a, b) => a - b);
  if (v.length === 0) return null;
  const q = p => v[Math.min(v.length - 1, Math.ceil(p * v.length) - 1)];
  const r = x => Math.round(x * 1000) / 1000;
  return { n: v.length, median: r(v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2), p95: r(q(0.95)), min: r(v[0]), max: r(v[v.length - 1]) };
}

/** Flattens numeric fields of a result to dotted paths. */
export function flatten(obj, prefix = '', out = {}) {
  for (const [k, val] of Object.entries(obj || {})) {
    const key = prefix ? prefix + '.' + k : k;
    if (typeof val === 'number') out[key] = val;
    else if (val && typeof val === 'object' && !Array.isArray(val)) flatten(val, key, out);
  }
  return out;
}

export function summarise(samples) {
  const flat = samples.map(s => flatten(s));
  const keys = [...new Set(flat.flatMap(f => Object.keys(f)))].filter(k => !k.startsWith('params.') && !k.startsWith('environment.resources'));
  const out = {};
  for (const k of keys.sort()) { const s = stats(flat.map(f => f[k])); if (s) out[k] = s; }
  return out;
}

/** SplitMix64-based seeded shuffle (record order randomised by the seed, plan section 2). */
export function seededShuffle(list, seedLabel) {
  let h = 14695981039346656037n;
  for (const b of new TextEncoder().encode(seedLabel)) { h ^= BigInt(b); h = (h * 1099511628211n) & 0xffffffffffffffffn; }
  let s = h;
  const next = () => { s = (s + 0x9E3779B97F4A7C15n) & 0xffffffffffffffffn; let z = s; z = ((z ^ (z >> 30n)) * 0xBF58476D1CE4E5B9n) & 0xffffffffffffffffn; z = ((z ^ (z >> 27n)) * 0x94D049BB133111EBn) & 0xffffffffffffffffn; return z ^ (z >> 31n); };
  const a = [...list];
  for (let i = a.length - 1; i > 0; i--) { const j = Number(next() % BigInt(i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}
