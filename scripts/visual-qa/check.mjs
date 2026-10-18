import { chromium } from 'playwright';
import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Visual QA uses HTML rendered by real MVC HTTP integration tests on a disposable database.
// The snapshot server never executes POST requests or connects to application data.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const snapshots = path.resolve(root, 'TestResults/sprint14-html');
const output = path.resolve(root, 'docs/evidencias/sprint14');
await fs.mkdir(output, { recursive: true });
const mime = { '.html': 'text/html; charset=utf-8', '.css': 'text/css', '.js': 'text/javascript', '.svg': 'image/svg+xml', '.jpg': 'image/jpeg' };
const server = http.createServer(async (req, res) => {
  if (req.method !== 'GET') { res.writeHead(405); res.end(); return; }
  try {
    const url = new URL(req.url, 'http://localhost');
    const isSnapshot = url.pathname.startsWith('/qa/');
    const base = isSnapshot ? snapshots : path.join(root, 'src/MinhaBelem.Web/wwwroot');
    const relative = isSnapshot ? url.pathname.slice(4) : url.pathname.slice(1);
    const file = path.resolve(base, relative);
    if (!file.startsWith(base + path.sep)) throw new Error('Invalid path');
    const contents = await fs.readFile(file);
    res.writeHead(200, { 'Content-Type': mime[path.extname(file)] || 'application/octet-stream' }); res.end(contents);
  } catch { res.writeHead(404); res.end('Not found'); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const port = server.address().port;
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const results = [];
const sizes = { desktop: [1440, 1000], notebook: [1280, 800], tablet: [768, 1024], mobile: [390, 844] };
const cultures = ['pt-BR', 'en', 'fr', 'es', 'ru', 'zh-Hans', 'ja'];
try {
  for (const culture of cultures) {
    const files = (await fs.readdir(path.join(snapshots, culture))).filter(x => x.endsWith('.html'));
    for (const [device, [width, height]] of Object.entries(sizes)) {
      const page = await browser.newPage({ viewport: { width, height }, locale: culture });
      // Avoid external fonts/network variability during layout checks.
      await page.route(/^https?:\/\/(?!127\.0\.0\.1)/, route => route.abort());
      for (const file of files) {
        await page.goto(`http://127.0.0.1:${port}/qa/${culture}/${file}`, { waitUntil: 'load' });
        await page.evaluate(() => window.scrollTo(0, 0));
        await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
        const layout = await page.evaluate(() => ({
          width: innerWidth, scrollWidth: document.documentElement.scrollWidth,
          h1: document.querySelector('h1')?.textContent?.trim(),
          lang: document.documentElement.lang,
          brokenImages: [...document.images].filter(img => !img.complete || img.naturalWidth === 0).map(img => img.getAttribute('src')),
          overflow: [...document.querySelectorAll('body *')].filter(el => {
            const rect = el.getBoundingClientRect();
            if (getComputedStyle(el).position === 'absolute' || el.closest('.table-scroll,.categories')) return false;
            return rect.width > 0 && rect.right > innerWidth + 4;
          }).slice(0, 5).map(el => ({ tag: el.tagName, class: el.className, width: Math.round(el.getBoundingClientRect().width) }))
        }));
        results.push({ culture, device, file, ...layout });
        if (culture === 'pt-BR' && ['home.html', 'admin.html', 'admin-radar.html', 'agenda.html', 'explore.html', 'conta-login.html', 'mobilidade.html'].includes(file)) {
          await page.screenshot({ path: path.join(output, `${file.replace('.html', '')}-${device}.jpg`), type: 'jpeg', quality: 82, fullPage: device === 'mobile' });
        }
        if (file === 'home.html' && device === 'mobile') {
          const toggle = page.locator('.menu-toggle');
          await toggle.click();
          if (await toggle.getAttribute('aria-expanded') !== 'true') throw new Error('Mobile menu did not open');
          await page.keyboard.press('Escape');
          if (await toggle.getAttribute('aria-expanded') !== 'false') throw new Error('Escape did not close menu');
        }
      }
      console.log(`${culture} ${device}: ${files.length} pages checked`);
      await page.close();
    }
  }
} finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
const failures = results.filter(x => x.scrollWidth > x.width + 4 || x.brokenImages.length > 0);
await fs.writeFile(path.join(output, 'layout-results.json'), JSON.stringify({ mode: 'Rendered MVC snapshots; functional HTTP coverage in Sprint14Tests', checked: results.length, failures, results }, null, 2));
console.log(JSON.stringify({ checked: results.length, failures: failures.map(x => ({ culture: x.culture, device: x.device, file: x.file, width: x.width, scrollWidth: x.scrollWidth, brokenImages: x.brokenImages, overflow: x.overflow })) }));
process.exitCode = failures.length ? 1 : 0;
