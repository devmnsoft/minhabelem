import { chromium } from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const checks = [];
try {
  const page = await browser.newPage();
  const posts = [];
  await page.route('https://fixture.example/**', async route => {
    if (route.request().method() === 'POST') { posts.push(new URLSearchParams(route.request().postData())); await route.fulfill({body:'done'}); }
    else await route.fulfill({contentType:'text/html',body:`<html><body><h1>Fixture</h1><p hidden data-connection-status data-offline="Offline" data-online="Online"></p><iframe name="target"></iframe><form method="post" action="/submit" target="target"><input type="hidden" name="__RequestVerificationToken" value="fixture-token"><button name="action" value="confirm">Confirm</button></form></body></html>`});
  });
  await page.goto('https://fixture.example/');
  await page.addScriptTag({path:path.join(root,'src/MinhaBelem.Web/wwwroot/js/site.js')});
  await page.evaluate(() => { const f=document.querySelector('form');f.requestSubmit(f.querySelector('button'));f.requestSubmit(f.querySelector('button')); });
  await page.waitForFunction(() => document.querySelector('iframe').contentDocument?.body?.textContent === 'done');
  assert.equal(posts.length,1);assert.equal(posts[0].get('action'),'confirm');assert.equal(posts[0].get('__RequestVerificationToken'),'fixture-token');
  checks.push('duplicate POST prevented; submitter and antiforgery preserved');
  const resumedResponse=page.waitForResponse(response => response.url().endsWith('/submit'));
  await page.evaluate(() => {window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true}));const f=document.querySelector('form');f.requestSubmit(f.querySelector('button'));});
  await page.waitForFunction(() => document.querySelector('form').dataset.submitting === 'true');
  await resumedResponse;
  assert.equal(posts.length,2);checks.push('back-forward cache return permits next submission');
  await page.evaluate(() => {window.dispatchEvent(new PageTransitionEvent('pageshow'));Object.defineProperty(navigator,'onLine',{value:false,configurable:true});window.dispatchEvent(new Event('offline'));window.offlinePrevented=!document.querySelector('form').dispatchEvent(new Event('submit',{cancelable:true,bubbles:true}));});
  assert.equal(await page.evaluate(()=>window.offlinePrevented),true);assert.equal(await page.locator('[data-connection-status]').textContent(),'Offline');assert.equal(posts.length,2);checks.push('offline POST prevented with visible message');
  await page.setContent('<button data-guide-map-load data-map-key="fixture-public" data-lat="-1.45" data-lng="-48.5" data-loading="Loading" data-failed="Unavailable">Load</button><p data-guide-map-status></p><div hidden data-guide-map></div>');
  let mapsRequests=0;
  await page.route('https://maps.googleapis.com/**',route=>{mapsRequests++;return route.abort();});
  await page.addScriptTag({path:path.join(root,'src/MinhaBelem.Web/wwwroot/js/guide-map.js')});
  assert.equal(mapsRequests,0);
  await page.locator('[data-guide-map-load]').click();
  await page.waitForFunction(()=>document.querySelector('[data-guide-map-status]').textContent==='Unavailable');
  assert.equal(mapsRequests,1);assert.equal(await page.locator('[data-guide-map]').isVisible(),false);assert.equal(await page.locator('[data-guide-map-load]').isEnabled(),true);
  checks.push('map requested only on click; provider failure leaves retry and fallback');
  const output=path.join(root,'docs/evidencias/sprint18');await fs.mkdir(output,{recursive:true});await fs.writeFile(path.join(output,'interaction-results.json'),JSON.stringify({fixtureOnly:true,passed:checks},null,2));
  console.log(JSON.stringify({passed:checks.length,failed:0}));
} finally {await browser.close();}
