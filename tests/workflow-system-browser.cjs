const fs=require('fs'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 const legend=fs.readFileSync('Pages/Shared/_WorkStatusGuide.cshtml','utf8');
 const labels=['Scheduled','Needs confirmation','Work in progress','Completed','Cancelled','Expired'];
 const badges=labels.map(x=>`<span class="job-status" data-status="${x}">${x}</span>`).join(' ');
 await page.route('http://workflow.test/**',r=>r.fulfill({contentType:'text/html',body:`<!doctype html><html><meta charset="utf-8"><body style="margin:0"><div id="headerAccountMenu"><button class="header-theme-toggle">Dark mode</button></div><main style="max-width:1100px;margin:20px auto;padding:16px"><h1>My Jobs</h1>${legend}<nav class="job-tabs">${['Booking requests','My offers','Scheduled','My active jobs','Completed','Closed'].map(t=>`<a href="#">${t}</a>`).join('')}</nav><article class="job-card"><h2>House cleaning</h2><div id="badges">${badges}</div><p class="home-schedule-status" data-scheduled-status data-status="Scheduled">Scheduled</p><div class="work-primary-actions"><a class="work-button" data-work-primary data-open-offer-editor href="#offer-editor-1" data-i18n>Edit offer</a><a class="work-button work-button-secondary" href="#">Message client</a><a class="work-button work-button-secondary" href="#">View record</a></div><details class="work-editor" id="offer-editor-1"><summary>Edit offer</summary><input name="price"></details></article><section class="workflow-empty" data-empty-kind="records"><h3>No booking requests yet</h3><p>Requests from clients will appear here.</p></section><section class="workflow-empty" data-empty-kind="filtered"><h3 data-i18n>No tasks match these filters</h3><button class="work-button work-button-secondary" data-reset-task-filters data-i18n>Clear filters</button></section><section class="workflow-empty" data-empty-kind="error"><h3>Jobs are unavailable</h3><a class="work-button work-button-secondary" href="#" data-i18n>Try again</a></section><section id="my-tasks-section"><select id="myTasksTypeFilter"><option value="long" selected>Long Job</option><option value="">All</option></select><select id="clientClosedFilter"><option value="expired" selected>Expired</option><option value="all">All</option></select><button data-filter="all" onclick="window.resetDone=true">All</button></section></main></body></html>`}));
 await page.goto('http://workflow.test/');
 for(const name of ['settings','light-system','my-jobs','account-theme','workflow-polish','workflow-design-system'])await page.addStyleTag({content:fs.readFileSync(`wwwroot/css/${name}.css`,'utf8')});
 for(const name of ['site-language-fil','site-language','workflow-design-system'])await page.addScriptTag({content:fs.readFileSync(`wwwroot/js/${name}.js`,'utf8')});
 await page.waitForFunction(()=>document.querySelectorAll('#badges .workflow-status').length===6);
 assert.deepEqual(await page.locator('#badges .workflow-status').evaluateAll(es=>es.map(e=>e.dataset.workState)),['scheduled','pending','active','completed','cancelled','closed']);
 await page.locator('[data-open-offer-editor]').click();assert(await page.locator('#offer-editor-1').evaluate(e=>e.open));assert(await page.locator('input[name=price]').evaluate(e=>e===document.activeElement));
 await page.locator('[data-reset-task-filters]').click();assert.equal(await page.locator('#myTasksTypeFilter').inputValue(),'');assert.equal(await page.locator('#clientClosedFilter').inputValue(),'all');assert(await page.evaluate(()=>window.resetDone));
 fs.mkdirSync('obj/workflow-system-previews',{recursive:true});
 for(const lang of ['en','fil']) {
 await page.locator(`#headerAccountMenu [data-language="${lang}"]`).click();
 if(lang==='fil'){assert.match(await page.locator('.workflow-legend summary').innerText(),/Gabay/);assert.match(await page.locator('[data-scheduled-status]').innerText(),/iskedyul/);}
 await page.locator('.workflow-legend').evaluate(e=>e.open=true);
 for(const theme of ['light','dark'])for(const width of [390,768,1500]){
 await page.setViewportSize({width,height:1100});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`No overflow: ${lang}/${theme}/${width}`);
 const colors=await page.locator('#badges .workflow-status').evaluateAll(es=>es.map(e=>getComputedStyle(e).backgroundColor));assert.equal(new Set(colors).size,6,'Each lifecycle state has its own background');
 const homeColor=await page.locator('[data-scheduled-status]').evaluate(e=>getComputedStyle(e).backgroundColor);assert.equal(homeColor,colors[0],'Home and jobs use the same scheduled color');
 const buttons=await page.locator('.job-tabs a').evaluateAll(es=>es.map(e=>e.getBoundingClientRect().height));assert(buttons.every(h=>h>=44),'Mobile tab touch targets');
 if(width===390)await page.screenshot({path:`obj/workflow-system-previews/${lang}-${theme}-mobile.png`,fullPage:true});
 }
 }
 await page.locator('[data-scheduled-status]').evaluate(e=>e.dataset.status='Schedule overdue');await page.waitForFunction(()=>document.querySelector('[data-scheduled-status]').dataset.workState==='pending');
 assert.deepEqual(errors,[]);console.log('PASS: consistent lifecycle colors across Home/jobs, live status refresh, distinct empty states, offer focus, clear filters, Filipino, dark/light and responsive layouts. Component fixtures only.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
