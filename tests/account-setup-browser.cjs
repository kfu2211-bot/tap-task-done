const assert = require('node:assert/strict');
const { chromium } = require('playwright');
(async () => {
 const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "chrome" });
 try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await context.route('**/*', route => route.request().url().startsWith('http://127.0.0.1:5183/') ? route.continue() : route.abort());
  await context.addInitScript(() => localStorage.setItem("tapTaskDoneAccount", JSON.stringify({name:"Preview User",email:"preview@example.test"})));
  const page = await context.newPage();
  await page.goto('http://127.0.0.1:5183/fixture/missing');
  const dialog = page.locator('.setup-dialog');
  await dialog.waitFor({ state: 'visible' });
  assert.equal(await dialog.locator('.setup-step').count(), 3);
  assert.equal(await page.locator('#setupTitle').evaluate(element => element === document.activeElement), true);
  assert.equal(await dialog.getByRole('link', { name: 'Set role', exact: true }).getAttribute('href'), '/Settings#work-preferences');
  assert.equal(await dialog.getByRole('link', { name: 'Add number', exact: true }).getAttribute('href'), '/Settings?view=profile&editPhone=true#contact-details');
  await page.screenshot({ path: 'obj/account-setup-desktop.png' });
  await page.keyboard.press('Escape');
  await dialog.waitFor({ state: 'hidden' });
  assert.equal(await page.locator('[data-setup-open]').evaluate(element => element === document.activeElement), true);
  await page.reload();
  assert.equal(await dialog.isVisible(), false, 'Dismissed prompt stays closed for the same sign-in');
  { const otherTab = await context.newPage();
  await otherTab.goto('http://127.0.0.1:5183/fixture/missing');
  assert.equal(await otherTab.locator('.setup-dialog').isVisible(), false, 'Same sign-in does not repeat the popup in another tab');
  await otherTab.close(); }
  await page.getByRole('button', { name: 'Continue setup' }).click();
  await dialog.waitFor({ state: 'visible' });
  await dialog.getByRole('button', { name: 'Mamaya na' }).click();
  await page.goto('http://127.0.0.1:5183/fixture/pending');
  assert.equal(await dialog.isVisible(), false, 'Pending-only setup does not interrupt users');
  { const otherTab = await context.newPage();
  await otherTab.goto('http://127.0.0.1:5183/fixture/missing');
  assert.equal(await otherTab.locator('.setup-dialog').isVisible(), false, 'Same sign-in does not repeat the popup in another tab');
  await otherTab.close(); }
  await page.getByRole('button', { name: 'Continue setup' }).click();
  await dialog.waitFor({ state: 'visible' });
  assert.equal(await dialog.locator('.setup-status').nth(1).textContent(), 'Under review');
  assert.equal(await dialog.locator('progress').getAttribute('value'), '2');
  await page.goto('http://127.0.0.1:5183/fixture/complete');
  assert.equal(await page.locator('[data-account-setup]').count(), 0);
  await page.goto('http://127.0.0.1:5183/fixture/unavailable');
  assert.equal(await dialog.isVisible(), false);
  { const otherTab = await context.newPage();
  await otherTab.goto('http://127.0.0.1:5183/fixture/missing');
  assert.equal(await otherTab.locator('.setup-dialog').isVisible(), false, 'Same sign-in does not repeat the popup in another tab');
  await otherTab.close(); }
  await page.getByRole('button', { name: 'Continue setup' }).click();
  assert.equal(await dialog.locator('a').count(), 0, 'Unavailable status does not suggest unverified actions');
  for (const width of [320, 390, 768]) {
   const mobile = await browser.newContext({ viewport: { width, height: 844 } });
   await mobile.route('**/*', route => route.request().url().startsWith('http://127.0.0.1:5183/') ? route.continue() : route.abort());
   await mobile.addInitScript(() => localStorage.setItem("tapTaskDoneAccount", JSON.stringify({name:"Preview User",email:"preview@example.test"})));
   const p = await mobile.newPage();
   await p.goto('http://127.0.0.1:5183/fixture/missing');
   await p.locator('.setup-dialog').waitFor({ state: 'visible' });
   const size = await p.locator('.setup-dialog').evaluate(el => ({ x:el.getBoundingClientRect().x, right:el.getBoundingClientRect().right, overflow:el.scrollWidth > el.clientWidth }));
   assert(size.x >= 0 && size.right <= width && !size.overflow, `Dialog fits ${width}px without horizontal overflow`);
   if (width === 390) {
    await p.screenshot({ path:'obj/account-setup-mobile.png' });
    await p.evaluate(() => document.documentElement.dataset.theme = 'dark');
    await p.screenshot({ path:'obj/account-setup-dark-mobile.png' });
   }
   await p.getByRole('button', {name:'Mamaya na'}).click();
   assert.equal(await p.locator('.setup-dialog').isVisible(),false);
   await mobile.close();
  }
  const blocked = await browser.newContext();
  await blocked.addInitScript(() => localStorage.setItem("tapTaskDoneAccount", JSON.stringify({name:"Preview User"})));
  await blocked.addInitScript(() => { for (const method of ['getItem', 'setItem']) { const original = Storage.prototype[method]; Storage.prototype[method] = function(key, ...args) { if (key.startsWith('fixture-setup-')) throw new DOMException('Blocked'); return original.call(this, key, ...args); }; } });
  const blockedPage = await blocked.newPage();
  await blockedPage.goto('http://127.0.0.1:5183/fixture/missing');
  await blockedPage.locator('.setup-dialog').waitFor({ state:'visible' });
  await blockedPage.getByRole('button', {name:'Mamaya na'}).click();
  await blocked.close();
  console.log('PASS: actual Razor dashboard rendering, 3 destinations, first-open/dismiss/reopen, focus/Escape, pending/complete/unavailable, mobile widths 320/390/768, dark screenshot and blocked storage.');
 } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode=1; });




