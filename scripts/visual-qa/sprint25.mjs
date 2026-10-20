import { chromium } from 'playwright';
import fs from 'node:fs/promises';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const snapshots = path.join(root, process.env.QA_SNAPSHOTS || 'TestResults/sprint25-after-html');
const output = path.join(root, process.env.QA_OUTPUT || 'docs/evidencias/sprint25/after');
await fs.mkdir(output, { recursive: true });
const axe = await fs.readFile(path.join(root, 'scripts/visual-qa/node_modules/axe-core/axe.min.js'), 'utf8');
const mime = { '.html':'text/html; charset=utf-8', '.css':'text/css', '.js':'text/javascript', '.svg':'image/svg+xml', '.webp':'image/webp', '.jpg':'image/jpeg' };
const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://localhost');
    const pathname = decodeURIComponent(url.pathname);
    const base = pathname.startsWith('/qa/') ? snapshots : path.join(root,'src/MinhaBelem.Web/wwwroot');
    const file = path.resolve(base, pathname.startsWith('/qa/') ? pathname.slice(4) : pathname.slice(1));
    if (req.method !== 'GET' || !file.startsWith(base + path.sep)) throw Error('Invalid request');
    const contents = await fs.readFile(file);
    res.writeHead(200, {'Content-Type':mime[path.extname(file)] || 'application/octet-stream'});
    res.end(contents);
  } catch { res.writeHead(404); res.end(); }
});
await new Promise(resolve => server.listen(0,'127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const browser = await chromium.launch({ channel:'msedge', headless:true });
const cultures = process.env.QA_REGRESSION_ONLY ? [] : (process.env.QA_CULTURES?.split(',') || ['pt-BR','en','fr','es','ru','zh-Hans','ja']);
const widths = process.env.QA_WIDTHS?.split(',').map(Number) || [320,360,390,430,768,1024,1440];
const results = [], interactions = [];
async function load(page,culture,file,theme) {
  await page.goto(`${origin}/qa/${culture}/${file}`,{waitUntil:'load'});
  await page.evaluate(theme => {
    localStorage.setItem('minhabelem_theme',theme);
    document.documentElement.dataset.theme=theme;
    document.body.classList.toggle('dark-theme',theme==='dark');
  },theme);
  await page.addStyleTag({content:'*,*::before,*::after{transition:none!important;animation:none!important;scroll-behavior:auto!important}'});
  if(process.env.QA_TEXT_SCALE) await page.addStyleTag({content:`html{font-size:${Number(process.env.QA_TEXT_SCALE)}%}`});
  await page.evaluate(async()=> { await Promise.all([...document.images].map(img=> {img.loading='eager';return img.decode().catch(()=>{});})); });
}
async function inspect(page,meta,withAxe) {
  await page.evaluate(async()=> { await Promise.all([...document.images].map(img=> {img.loading='eager';return img.decode().catch(()=>{});})); });
  const layout = await page.evaluate(() => ({width:innerWidth,scrollWidth:document.documentElement.scrollWidth,
    brokenImages:[...document.images].filter(i=>!i.complete||!i.naturalWidth).map(i=>i.getAttribute('src')),
    overflow:[...document.querySelectorAll('body *')].filter(e=> {const r=e.getBoundingClientRect();return r.width&&r.right>innerWidth+2&&!e.closest('.table-scroll,.categories');}).slice(0,6).map(e=>e.className),
    h1:document.querySelector('h1')?.textContent.trim(),
    unknownKeys:[...document.querySelectorAll('h1,h2,a,label')].map(e=>e.textContent.trim()).filter(t=>/^(City[A-Z]\w*|TransitStops|AgendaToday|EstacaoDocasPhoto)$/.test(t))
  }));
  let violations=[];
  if(withAxe) {
    await page.evaluate(axe);
    violations = await page.evaluate(async()=> (await axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}})).violations.map(v=>({id:v.id,impact:v.impact,description:v.description,nodes:v.nodes.map(n=>({target:n.target,summary:n.failureSummary}))})));
  }
  results.push({...meta,...layout,violations});
}
try {
  for (const culture of cultures) {
    for (const width of widths) {
      const page = await browser.newPage({viewport:{width,height:Number(process.env.QA_HEIGHT)||900},locale:culture,colorScheme:'dark'});
      await page.route(/^https?:\/\/(?!127\.0\.0\.1)/,route=>route.abort());
      for (const theme of ['light','dark']) {
        await load(page,culture,'home.html',theme);
        await inspect(page,{culture,width,theme,file:'home.html'},culture==='pt-BR'||[390,1440].includes(width));
        if (culture==='pt-BR' && [360,390,768,1440].includes(width)) {
          await page.screenshot({path:path.join(output,`home-${width}-${theme}.jpg`),type:'jpeg',quality:85});
          if(width<500) await page.screenshot({path:path.join(output,`home-${width}-${theme}-full.jpg`),type:'jpeg',quality:85,fullPage:true});
        }
      }
      if(width<1200) {
        await page.locator('.mobile-tab-more').click();
        if(!await page.locator('#main-navigation').isVisible()) throw Error(`Menu hidden: ${culture}/${width}`);
        if(await page.locator('.menu-toggle').getAttribute('aria-expanded')!=='true') throw Error('Menu state mismatch');
        await page.locator('.nav-dropdown-btn').click();
        if(!await page.locator('#more-navigation a[href="/mobilidade/de-para"]').isVisible()) throw Error('De-Para inaccessible');
        if(culture==='pt-BR'&&width===390) await inspect(page,{culture,width,theme:'dark',file:'home-menu.html'},true);
        if(culture==='pt-BR'&&width===390) await page.screenshot({path:path.join(output,'menu-390-dark.jpg'),type:'jpeg',quality:85,fullPage:false});
        await page.keyboard.press('Escape');
        await page.keyboard.press('Escape');
        if(await page.locator('.mobile-tab-more').getAttribute('aria-expanded')!=='false') throw Error('Menu did not close');
        if(!await page.locator('.mobile-tab-more').evaluate(e=>e===document.activeElement)) throw Error('Focus not restored');
      } else {
        await page.locator('.nav-dropdown-btn').click();
        if(!await page.locator('#more-navigation').isVisible()) throw Error('Desktop disclosure hidden');
        if(culture==='pt-BR'&&width===1440) await inspect(page,{culture,width,theme:'dark',file:'home-menu.html'},true);
        await page.keyboard.press('Escape');
        if(await page.locator('.nav-dropdown-btn').getAttribute('aria-expanded')!=='false') throw Error('Desktop Escape failed');
      }
      // A saved light theme must override a dark OS preference after reload.
      await page.evaluate(()=>localStorage.setItem('minhabelem_theme','light'));
      await page.reload({waitUntil:'load'});
      if(await page.locator('html').getAttribute('data-theme')!=='light') throw Error('Light preference was lost');
      await page.locator('.theme-toggle').click();
      if(await page.locator('html').getAttribute('data-theme')!=='dark') throw Error('Theme toggle failed');
      if(culture==='pt-BR'&&width===390) {
        await page.locator('.header [data-open-a11y]').click();
        if(!await page.locator('#a11y-dialog').isVisible()) throw Error('Accessibility panel hidden');
        await inspect(page,{culture,width,theme:'dark',file:'home-preferences.html'},true);
        await page.keyboard.press('Escape');
        if(!await page.locator('.header [data-open-a11y]').evaluate(e=>e===document.activeElement)) throw Error('Panel focus not restored');
        const photo=page.locator('[data-lightbox="home-photos"]').first();
        await photo.focus(); await page.keyboard.press('Enter');
        if(!await page.locator('#mb-lightbox').isVisible()) throw Error('Keyboard gallery failed');
        await page.keyboard.press('Tab');
        if(!await page.evaluate(()=>document.getElementById('mb-lightbox').contains(document.activeElement))) throw Error('Gallery focus escaped');
        await page.keyboard.press('Escape');
        if(!await photo.evaluate(e=>e===document.activeElement)) throw Error('Gallery focus not restored');
      }
      interactions.push({culture,width,menu:true,escape:true,themePersistence:true});
      await page.close();
    }
    console.log(`${culture}: home, themes and navigation checked at ${widths.length} widths`);
  }
  if(!process.env.QA_HOME_ONLY) {
    const files=(await fs.readdir(path.join(snapshots,'pt-BR'))).filter(f=>f.endsWith('.html')&&f!=='home.html');
    const page=await browser.newPage({viewport:{width:390,height:844}});
    await page.route(/^https?:\/\/(?!127\.0\.0\.1)/,route=>route.abort());
    for(const file of files) for(const theme of ['light','dark']) {
      await load(page,'pt-BR',file,theme);
      await inspect(page,{culture:'pt-BR',width:390,theme,file},file==='home-data.html');
      if(file==='home-data.html') await page.screenshot({path:path.join(output,`home-data-390-${theme}.jpg`),type:'jpeg',quality:85,fullPage:true});
    }
    await page.close();
    console.log(`${files.length} remaining MVC pages checked in both themes at 390px`);
  }
} finally {
  await browser.close(); await new Promise(resolve=>server.close(resolve));
  const failures=results.filter(r=>r.scrollWidth>r.width+2||r.brokenImages.length||r.unknownKeys.length||r.violations.length);
  await fs.writeFile(path.join(output,'results.json'),JSON.stringify({method:'Real MVC snapshots from disposable database. No POST or external integrations executed by snapshot server.',checked:results.length,interactions,failures,results},null,2));
  console.log(JSON.stringify({checked:results.length,failures:failures.map(r=>({file:r.file,culture:r.culture,width:r.width,theme:r.theme,overflow:r.overflow,violations:r.violations.map(v=>v.id)}))}));
  if(failures.length) process.exitCode=1;
}
