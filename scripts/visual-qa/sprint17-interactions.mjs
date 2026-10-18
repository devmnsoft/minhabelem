import { chromium } from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';

// Controlled interaction fixtures. Not evidence of live provider routes or published content.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const results = [];
try {
  const page = await browser.newPage();
  await page.goto('about:blank');
  await page.setContent(`<h1>MinhaBelém</h1><form><input name="Origin" value="PRIVATE_ORIGIN_123"/><textarea>PRIVATE_CONTACT_456</textarea></form>
    <section><h2>21/10/2026</h2><ol class="journey-timeline"><li><h3><a href="/guia/42">Local público</a></h3><p>09:00 → 10:00</p></li></ol></section>
    <button data-share-itinerary data-copied="Copied" data-failed="Failed">Share</button><p data-itinerary-share-status></p>`);
  // Use an actual origin and document with private form fields, but no outgoing share provider.
  await page.route('https://fixture.example/**', route => route.fulfill({ contentType: 'text/html', body: '' }));
  const html = await page.content();
  await page.route('https://fixture.example/itinerary', route => route.fulfill({ contentType: 'text/html', body: html }));
  await page.goto('https://fixture.example/itinerary');
  await page.evaluate(() => {
    Object.defineProperty(navigator, 'share', { value: undefined, configurable: true });
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: async text => { window.sharedPayload = text; } }, configurable: true });
  });
  await page.addScriptTag({ path: path.join(root, 'src/MinhaBelem.Web/wwwroot/js/itinerary-share.js') });
  await page.locator('[data-share-itinerary]').click();
  const payload = await page.evaluate(() => window.sharedPayload);
  assert(payload.includes('https://fixture.example/guia/42'));
  assert(payload.includes('21/10/2026'));
  assert(!payload.includes('PRIVATE_ORIGIN_123') && !payload.includes('PRIVATE_CONTACT_456'));
  results.push({ test: 'share_excludes_private_form_fields', passed: true });

  await page.setContent(`<form id="editorial-batch"><input type="checkbox" id="batch-select-page"/><output id="batch-count"></output>
    <input name="selected" type="checkbox" value="1"/><input name="selected" type="checkbox" value="2"/><textarea name="Reason"></textarea></form>`);
  await page.addScriptTag({ path: path.join(root, 'src/MinhaBelem.Web/wwwroot/js/editorial-batches.js') });
  await page.locator('[name=selected]').first().check();
  assert.equal(await page.locator('#batch-count').textContent(), '1');
  assert(await page.locator('#batch-select-page').evaluate(el => el.indeterminate));
  await page.keyboard.press('Alt+Shift+A');
  assert.equal(await page.locator('#batch-count').textContent(), '2');
  await page.keyboard.press('Escape');
  assert.equal(await page.locator('#batch-count').textContent(), '0');
  await page.locator('[name=Reason]').focus();
  await page.keyboard.press('Alt+Shift+A');
  assert.equal(await page.locator('#batch-count').textContent(), '0');
  results.push({ test: 'batch_selection_keyboard_and_text_field_isolation', passed: true });

  await page.setContent(`<span data-overall-status>Queued</span><span data-completed-count>0</span><span data-failed-count>0</span>
    <progress data-progress-url="https://fixture.example/progress" data-batch-status="queued" data-status-labels='{"completed":"Completed"}'></progress>
    <table><tbody><tr data-batch-item="content:1"><td data-item-status>Pending</td><td data-item-result></td></tr></tbody></table>`);
  await page.route('https://fixture.example/progress', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ status: 'completed', total: 1, finished: 1, items: [{ targetId: 1, targetKind: 'content', status: 'completed', result: '<script>unsafe</script>' }] }) }));
  await page.addScriptTag({ path: path.join(root, 'src/MinhaBelem.Web/wwwroot/js/editorial-progress.js') });
  await page.waitForFunction(() => document.querySelector('progress').value === 1);
  assert.equal(await page.locator('[data-overall-status]').textContent(), 'Completed');
  assert.equal(await page.locator('[data-completed-count]').textContent(), '1');
  assert.equal(await page.locator('[data-item-result]').textContent(), '<script>unsafe</script>');
  assert.equal(await page.locator('[data-item-result] script').count(), 0);
  results.push({ test: 'batch_progress_updates_and_renders_results_as_text', passed: true });
} finally { await browser.close(); }
const destination = path.join(root, 'docs/evidencias/sprint17/interactions.json');
await fs.mkdir(path.dirname(destination), { recursive: true });
await fs.writeFile(destination, JSON.stringify({ mode: 'Controlled DOM fixtures exercising production JavaScript in Edge/Playwright', results }, null, 2));
console.log(JSON.stringify({ passed: results.length }));
