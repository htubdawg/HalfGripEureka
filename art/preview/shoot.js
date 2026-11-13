// node shoot.js <outdir>: serves the viewer and takes the preview pictures
const { chromium } = require('playwright');
const http = require('http'), fs = require('fs'), path = require('path');
const dir = __dirname, out = process.argv[2];
const server = http.createServer((req, res) => {
  const f = path.join(dir, decodeURIComponent(req.url.split('?')[0]));
  if (!fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  res.writeHead(200, {'Content-Type': f.endsWith('.js') ? 'text/javascript' : f.endsWith('.html') ? 'text/html' : 'application/json'});
  fs.createReadStream(f).pipe(res);
}).listen(8765);
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('console', m => console.log('page:', m.text()));
  page.on('pageerror', e => console.log('error:', e.message));
  await page.goto('http://localhost:8765/view.html');
  await page.waitForFunction(() => window.ready === true, null, { timeout: 120000 });
  const shots = JSON.parse(fs.readFileSync(path.join(dir, "shots.json"), "utf8"));
  for (const s of shots) {
    await page.evaluate(([e, a, o]) => window.shot(e, a, o), [s.eye, s.at, s.opts || {}]);
    await page.screenshot({ path: path.join(out, s.name + '.png') });
    console.log('shot', s.name);
  }
  await browser.close(); server.close();
})();
