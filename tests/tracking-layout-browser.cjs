const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const source=fs.readFileSync('Pages/Tracking.cshtml','utf8');
 assert(source.indexOf('class="tracking-location"')<source.indexOf('Arrival schedule'),'Schedule must follow map section');
 assert(source.indexOf('Arrival schedule')<source.indexOf('<partial name="_WorkAgreement"'),'Agreement follows schedule');
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
 const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const agreement=ready=>`<section class="work-agreement-panel" data-agreement-task="1" data-agreement-version="1"><div data-agreement-summary><h3 data-i18n>Review &amp; confirm work details</h3><p>${ready?'Both confirmed':'Confirmation pending'}</p></div><dl class="work-agreement-grid"><div><dt>Agreed price</dt><dd>₱700</dd></div><div><dt>Arrival date and time</dt><dd>Nov 23, 2026 · 10:00 AM PHT</dd></div><div class="work-field-wide"><dt>Destination</dt><dd>Bacolor, Pampanga</dd></div></dl><details data-agreement-editor class="work-editor"><summary>Request reschedule</summary><input name="price"></details></section>`;
 const step=ready=>`<section class="tracking-next-step" data-tracking-next-step data-task-state="assigned" data-ready="${ready}"><h4 data-i18n>Next step</h4><strong data-i18n>${ready?'Both confirmed':'Confirm work details first'}</strong><p data-i18n>Travel unlocks after both participants confirm the same work details.</p><a class="work-button" href="#work-details-1" data-review-details data-i18n>Review details</a></section>`;
 const actions=ready=>`<div class="tracking-actions tracking-current-actions">${ready?'<form class="tracking-progress-form"><input name="action" value="travel"><button type="submit">Start travel &amp; share location</button></form>':'<p>Both participants must confirm work details before travel.</p>'}</div>`;
 const html=`<!doctype html><html><meta charset="utf-8"><body style="margin:0"><div class="db-tracking" style="max-width:1200px;margin:auto"><article class="db-tracking-card"><h2>Cleaning</h2><div class="tracking-detail-layout tracking-workspace"><section class="tracking-panel tracking-progress-column"><h4>Task progress</h4><ol class="tracking-timeline"><li>Booking accepted</li><li>On the way</li><li>Arrived</li><li>Work in progress</li><li>Client review</li><li>Completed</li></ol>${step(false)}${actions(false)}</section><aside class="tracking-aside tracking-work-column"><section class="tracking-location"><h4>Task location</h4><p>Bacolor, Pampanga</p><div class="job-location-map" style="background:#cadfd1;display:grid;place-items:center">Map</div></section><section class="work-schedule-notice"><h3 data-i18n>Arrival schedule</h3><p>Nov 23, 2026 · 10:00 AM PHT</p></section><div id="work-details-1">${agreement(false)}</div></aside></div></article></div></body></html>`;
 await page.route('http://tracking.test/**',r=>r.fulfill({contentType:'text/html',body:html}));await page.goto('http://tracking.test/');
 for(const css of ['tracking','tracking-database','light-system','account-theme','workflow-polish']) await page.addStyleTag({content:fs.readFileSync(`wwwroot/css/${css}.css`,'utf8')});
 await page.addScriptTag({content:fs.readFileSync('wwwroot/js/workflow-polish.js','utf8')});
 await page.locator('[data-review-details]').click();assert.equal(await page.locator('[data-agreement-editor]').evaluate(e=>e.open),false,'Review must not open the change editor');
 await page.evaluate(()=>{window.setInterval=fn=>{window.pollAgreement=fn;return 1;};});
 await page.addScriptTag({content:fs.readFileSync('wwwroot/js/agreement-live.js','utf8')});
 await page.evaluate(response=>{window.fetch=async()=>({ok:true,redirected:false,text:async()=>response});},`<html><body>${agreement(true)}${step(true)}${actions(true)}</body></html>`);
 await page.evaluate(()=>window.pollAgreement());assert.equal(await page.locator('.tracking-progress-form button').count(),1,'Travel appears after both confirmations');
 fs.mkdirSync('obj/tracking-layout-previews',{recursive:true});
 for(const width of [1500,768,390]) for(const theme of ['light','dark']) {
 await page.setViewportSize({width,height:1100});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`No overflow at ${width}/${theme}`);
 const left=await page.locator('.tracking-progress-column').boundingBox(),right=await page.locator('.tracking-work-column').boundingBox();
 if(width===1500)assert(right.x>left.x+left.width,'Desktop columns stay side by side');else assert(right.y>=left.y+left.height,'Narrow screens stack naturally');
 const map=await page.locator('.job-location-map').boundingBox(),schedule=await page.locator('.work-schedule-notice').boundingBox(),review=await page.locator('#work-details-1').boundingBox();assert(schedule.y>=map.y+map.height&&review.y>=schedule.y+schedule.height,'Map, schedule, review order');
 if(width===1500)await page.screenshot({path:`obj/tracking-layout-previews/${theme}.png`,fullPage:true});
 }
 assert.deepEqual(errors,[]);console.log('PASS: map/schedule/review order; responsive light/dark layout; review focus; confirmation polling unlocks travel without replacing the map. Component fixtures only.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
