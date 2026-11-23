const fs=require('fs'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
 for(const language of ['en','fil'])for(const theme of ['light','dark']) {
 const html=fs.readFileSync(`obj/schedule-email-previews/${language}-${theme}-schedule_overdue.html`,'utf8');
 await page.setContent(html);
 assert.equal(await page.locator('html').getAttribute('lang'),language);
 assert.equal(await page.locator('a').count(),1);assert.equal(await page.locator('a').getAttribute('href'),'https://example.test/Tracking?taskId=90041');
 assert.equal(await page.locator('script').count(),0);
 for(const width of [360,390,768]) {
 await page.setViewportSize({width,height:1100});
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`Email must fit ${width}px ${language}/${theme}`);
 const cta=await page.locator('a').boundingBox();assert(cta.height>=44,'CTA supports touch');
 }
 await page.setViewportSize({width:390,height:1100});
 fs.mkdirSync('obj/schedule-email-previews',{recursive:true});
 await page.screenshot({path:`obj/schedule-email-previews/${language}-${theme}.png`,fullPage:true});
 }
 let requests=[];await page.route('http://preferences.test/**',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><html><body></body></html>'}));
 await page.goto('http://preferences.test/');await page.evaluate(()=>{localStorage.setItem('tts-language','fil');document.documentElement.dataset.theme='dark';window.fetch=async(url,options)=>{window.preferenceCalls??=[];window.preferenceCalls.push({url,options});return {ok:true,redirected:false};};});
 await page.addScriptTag({content:fs.readFileSync('wwwroot/js/email-presentation.js','utf8')});await page.waitForFunction(()=>window.preferenceCalls?.length===1);
 assert.match(await page.evaluate(()=>window.preferenceCalls[0].url),/language=fil&theme=dark/);
 await page.evaluate(()=>{localStorage.setItem('tts-language','en');window.dispatchEvent(new Event('site-language-changed'));});await page.waitForFunction(()=>window.preferenceCalls?.length===2);
 assert.match(await page.evaluate(()=>window.preferenceCalls[1].url),/language=en&theme=dark/);
 assert.deepEqual(errors,[]);console.log('PASS: generated HTML emails fit mobile/tablet in both languages/themes; safe task content; one task-specific CTA; account preference sync. No real emails sent.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
