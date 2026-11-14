// node holes.js <outdir> [step] (run from the preview's work folder after run.sh, like shoot.js): drives the camera along every road at car-eye height, looking four ways against a magenta sky with no
// fog; any magenta is a hole in the mine. Writes holes.json (worst views first) and pictures of the worst.
const { chromium } = require('playwright');
const http = require('http'), fs = require('fs'), path = require('path');
const dir = __dirname, out = process.argv[2], step = +(process.argv[3] || 6);
fs.mkdirSync(out, { recursive: true });
const server = http.createServer((req, res) => {
  const f = path.join(dir, decodeURIComponent(req.url.split('?')[0]));
  if (!fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  res.writeHead(200, {'Content-Type': f.endsWith('.js') ? 'text/javascript' : f.endsWith('.html') ? 'text/html' : 'application/json'});
  fs.createReadStream(f).pipe(res);
}).listen(8768);
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await browser.newPage({ viewport: { width: 640, height: 360 } });
  page.on('pageerror', e => console.log('error:', e.message));
  await page.goto('http://localhost:8768/view.html');
  await page.waitForFunction(() => window.ready === true, null, { timeout: 120000 });
  const views = await page.evaluate((step) => {
    const res = [];
    const c2 = document.createElement('canvas'); c2.width = 320; c2.height = 180; const ctx = c2.getContext('2d', { willReadFrequently: true });
    const gl = document.querySelector('canvas');
    for (const [name, v] of Object.entries(window.SCENE.roads)) {
      const P = []; for (let i = 0; i < v.length; i += 3) P.push([v[i], v[i + 1], v[i + 2]]);
      for (let i = 1; i < P.length - 1; i += step) {
        const a = P[i], b = P[i + 1];
        let fx = b[0] - a[0], fz = b[2] - a[2]; const n = Math.hypot(fx, fz) || 1; fx /= n; fz /= n;
        const dirs = { ahead: [fx, fz], back: [-fx, -fz], left: [-fz, fx], right: [fz, -fx] };
        for (const [dn, d] of Object.entries(dirs)) {
          const eye = [a[0], a[1] + 4, a[2]], at = [a[0] + d[0] * 20, a[1] + 3, a[2] + d[1] * 20];
          window.shot(eye, at, { dark: true, bg: 0xff00ff, hide: ['ground', 'hill top'], head: 60 });
          ctx.drawImage(gl, 0, 0, 320, 180);
          const px = ctx.getImageData(0, 0, 320, 180).data; let m = 0;
          for (let k = 0; k < px.length; k += 4) if (px[k] > 200 && px[k + 1] < 60 && px[k + 2] > 200) m++;
          if (m > 0) res.push({ road: name, i, dir: dn, magenta: m, eye, at });
        }
      }
    }
    return res.sort((p, q) => q.magenta - p.magenta);
  }, step);
  fs.writeFileSync(path.join(out, 'holes.json'), JSON.stringify(views, null, 1));
  // in the open (the yard, and looking back up the slope at the drift mouth) the sky is meant to show
  const under = views.filter(v => v.eye[1] < -12 && !(v.road === 'slope' && v.dir === 'back'));
  console.log(under.length + ' views underground with holes' + (under.length ? ', worst: ' + under.slice(0, 5).map(v => v.road + ' ' + v.i + ' ' + v.dir + ' (' + v.magenta + ')').join(', ') : ''));
  await page.setViewportSize({ width: 1280, height: 720 });
  for (let k = 0; k < Math.min(12, views.length); k++) {
    const v = views[k];
    await page.evaluate(([e, a]) => window.shot(e, a, { dark: true, bg: 0xff00ff, hide: ['ground', 'hill top'], head: 60 }), [v.eye, v.at]);
    await page.screenshot({ path: path.join(out, 'hole_' + k + '.png') });
  }
  console.log(views.length + ' views with holes');
  await browser.close(); server.close();
})();
