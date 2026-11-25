// Static server for the spike (no dependencies): serves a published build with precompressed Brotli/gzip files,
// the dataset files under /data/, and collects durability acknowledgements and results (plan Q2, Q3).
// Usage: node server.mjs --port 5000 --root ../publish/interpreted/wwwroot [--data ../out] [--log file.jsonl]
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const here = path.dirname(new URL(import.meta.url).pathname);
const port = Number(opt('--port', '5000'));
const root = path.resolve(opt('--root', path.join(here, '../publish/interpreted/wwwroot')));
const data = path.resolve(opt('--data', path.join(here, '../out')));
const logFile = opt('--log', '');
const headersFile = opt('--headers', '');   // staticwebapp.config.json: its globalHeaders are sent with every file

const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm',
  '.json': 'application/json', '.dat': 'application/octet-stream', '.css': 'text/css', '.jsonl': 'application/x-ndjson', '.png': 'image/png', '.ico': 'image/x-icon' };

export function createServer({ port, root, data, onPost, extraHeaders = {} }) {
  const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    if (req.method === 'POST') {
      const chunks = [];
      req.on('data', c => chunks.push(c));
      req.on('end', () => {
        const body = Buffer.concat(chunks).toString('utf8');
        try { onPost?.(url.pathname, body); } catch (e) { console.error(e); }
        res.writeHead(204, { 'access-control-allow-origin': '*' }); res.end();
      });
      return;
    }
    let file;
    if (url.pathname.startsWith('/data/')) file = path.join(data, path.basename(url.pathname));
    else file = path.join(root, decodeURIComponent(url.pathname));
    if (!file.startsWith(root) && !file.startsWith(data)) { res.writeHead(403); res.end(); return; }
    if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
    if (!fs.existsSync(file)) file = path.join(root, 'index.html');
    const ext = path.extname(file);
    const headers = { ...extraHeaders, 'content-type': types[ext] || 'application/octet-stream', 'vary': 'accept-encoding' };
    headers['cache-control'] = ext === '.html' || url.pathname.startsWith('/data/') ? 'no-cache' : (url.pathname.includes('/_framework/') ? 'public, max-age=31536000, immutable' : 'no-cache');
    const ae = String(req.headers['accept-encoding'] || '');
    let send = file;
    if (/\bbr\b/.test(ae) && fs.existsSync(file + '.br')) { send = file + '.br'; headers['content-encoding'] = 'br'; }
    else if (/\bgzip\b/.test(ae) && fs.existsSync(file + '.gz')) { send = file + '.gz'; headers['content-encoding'] = 'gzip'; }
    headers['content-length'] = fs.statSync(send).size;
    res.writeHead(200, headers);
    fs.createReadStream(send).pipe(res);
  });
  return new Promise(resolve => server.listen(port, '127.0.0.1', () => resolve(server)));
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(new URL(import.meta.url).pathname)) {
  const log = logFile ? fs.openSync(logFile, 'a') : null;
  const extraHeaders = headersFile ? JSON.parse(fs.readFileSync(headersFile, 'utf8')).globalHeaders : {};
  await createServer({ port, root, data, extraHeaders, onPost: (p, body) => { if (log) fs.writeSync(log, JSON.stringify({ path: p, body: JSON.parse(body) }) + '\n'); else console.log(p, body.slice(0, 200)); } });
  console.log(`serving ${root} and ${data} on http://localhost:${port}`);
}
