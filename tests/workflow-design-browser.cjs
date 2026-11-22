const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const read = name => fs.readFileSync(name,'utf8');
(async () => {
    const browser = await chromium.launch({headless:true,channel:'msedge'});
    try {
        const page = await browser.newPage();
        const errors=[]; page.on('pageerror',e=>errors.push(e.message));
        const completion = read('Pages/Shared/_CompletionOptions.cshtml');
        const actions = labels=>`<div class="work-primary-actions">${labels.map(label=>`<a class="work-button ${['Review schedule','View history'].includes(label)?'':'work-button-secondary'}" href="#">${label}</a>`).join('')}</div>`;
        const stars = `<fieldset class="review-star-input"><legend data-i18n>Rating</legend><div class="review-star-options">${[1,2,3,4,5].map(i=>`<label><input type="radio" name="rating" value="${i}" required><span aria-hidden="true">★</span><span class="review-star-name">${i} stars</span></label>`).join('')}</div></fieldset>`;
        await page.route('http://design.test/**',route=>route.fulfill({contentType:'text/html',body:`<!doctype html><html><meta charset="utf-8"><body class="my-jobs-page"><div id="headerAccountMenu"><button class="header-theme-toggle">Dark mode</button></div><div id="fixture" style="width:calc(100% - 32px);max-width:1100px;margin:24px auto"><article class="job-card"><h2>Sample job</h2><div id="freelancer-actions">${actions(['View record','Message client','Review schedule'])}</div><div class="work-secondary-actions"><details class="work-editor"><summary>Withdraw from this job</summary><form><label>Reason<textarea></textarea></label></form></details><a class="work-text-action" href="#">Request cancellation in Tracking</a></div></article><section class="work-schedule-notice"><div class="work-section-heading"><h3 data-i18n>Arrival schedule</h3><span class="work-status">Scheduled</span></div><p class="work-arrival-time">Nov 23, 2026 · 11:27 PM PHT</p><a id="reschedule" href="#work-details-1">Request reschedule</a></section><div id="work-details-1"><section class="work-agreement work-agreement-panel"><header class="work-section-heading"><h3 data-i18n>Review &amp; confirm work details</h3><span class="work-status">Confirmation pending</span></header><dl class="work-agreement-grid"><div><dt>Agreed price</dt><dd>₱500</dd></div><div><dt>Job type</dt><dd>Short Job</dd></div><div><dt>Arrival date and time</dt><dd>Nov 23, 2026 · 11:27 PM PHT</dd></div><div><dt>Expected finish</dt><dd>Nov 23, 2026 · 11:50 PM PHT</dd></div><div class="work-field-wide"><dt>Work scope</dt><dd translate="no">SALA: Show this original note</dd></div></dl><div class="work-confirmations"><div><strong>Client</strong><span>Waiting for confirmation</span></div><div data-confirmed="true"><strong>Freelancer</strong><span>Confirmed</span></div></div><details data-agreement-editor class="work-editor"><summary>Request reschedule or other changes</summary><form class="work-form"><label>Final price<input name="price" value="500"></label><label>Arrival time<input type="datetime-local"></label></form></details></section></div><form id="finish" class="tracking-progress-form">${completion}<button class="work-button" type="submit" data-i18n>Mark work finished</button></form><section class="work-completion-details"><h3 data-i18n>Completion details</h3><p class="work-help" data-i18n>No note or video was added. The client can still review the work before confirming completion.</p></section><div id="client-actions">${actions(['View conversation','View history','View record','Create similar task','Book again'])}</div><div class="task-review">${stars}</div></div></body></html>`}));
        await page.goto('http://design.test/');
        for(const name of ['settings','light-system','my-jobs','account-theme','theme-consistency','job-type','task-rating','review-stars','completion-photo','workflow-polish']) await page.addStyleTag({content:read(`wwwroot/css/${name}.css`)});
        for(const name of ['site-language-fil','site-language','completion-photo','workflow-polish']) await page.addScriptTag({content:read(`wwwroot/js/${name}.js`)});
        await page.setViewportSize({width:1500,height:1000});
        const rows = await page.locator('#freelancer-actions .work-button').evaluateAll(nodes=>nodes.map(n=>Math.round(n.getBoundingClientRect().top)));
        assert.equal(new Set(rows).size,1,'Freelancer primary actions must share one row');
        const clientRows = await page.locator('#client-actions .work-button').evaluateAll(nodes=>nodes.map(n=>Math.round(n.getBoundingClientRect().top)));
        assert.equal(new Set(clientRows).size,1,'Client history/rebook actions must share one row');
        assert.equal(await page.locator('#finish details').getAttribute('open'),null);
        assert.equal(await page.locator('#finish').evaluate(form=>form.checkValidity()),true,'Note and video must not be required');
        assert.equal(await page.locator('#finish').evaluate(form=>!new Event('submit',{bubbles:true,cancelable:true}).defaultPrevented && form.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}))),true,'Upload script must allow completion with no attachments');
        await page.locator('#reschedule').click();
        assert.equal(await page.locator('[data-agreement-editor]').evaluate(el=>el.open),true);
        assert.equal(await page.locator('input[name="price"]').evaluate(el=>el===document.activeElement),true);
        await page.locator('input[name="rating"][value="3"]').check();
        assert.equal(await page.locator('input[name="rating"][value="3"]').isChecked(),true);
        for(const theme of ['light','dark']) {
            await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
            const chosen=await page.locator('input[name="rating"][value="2"]').evaluate(el=>getComputedStyle(el.parentElement).color);
            const empty=await page.locator('input[name="rating"][value="4"]').evaluate(el=>getComputedStyle(el.parentElement).color);
            assert.equal(chosen,theme==='dark'?'rgb(244, 197, 92)':'rgb(156, 103, 9)'); assert.notEqual(chosen,empty);
            const button=await page.locator('#freelancer-actions a').first().evaluate(el=>({fg:getComputedStyle(el).color,bg:getComputedStyle(el).backgroundColor}));
            assert.notEqual(button.fg,button.bg,'Action text must stay visible');
            if(theme==='dark') {
                assert.equal(await page.locator('.work-text-action').evaluate(el=>getComputedStyle(el).color),'rgb(129, 219, 178)');
                assert.equal(await page.locator('.work-secondary-actions summary').evaluate(el=>getComputedStyle(el).color),'rgb(237, 245, 239)');
            }
        }
        await page.locator('#headerAccountMenu [data-language="fil"]').click();
        assert.equal(await page.locator('#freelancer-actions a').last().innerText(),'Suriin ang iskedyul');
        assert.match(await page.locator('#finish summary').innerText(),/Magdagdag ng detalye/);
        assert.match(await page.locator('.work-agreement-panel .work-status').innerText(),/Hinihintay/);
        assert.match(await page.locator('[translate="no"]').innerText(),/SALA: Show this original note/);
        for(const width of [390,768,1500]) {
            await page.setViewportSize({width,height:1000});
            assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,`No horizontal overflow at ${width}px`);
        }
        await page.setViewportSize({width:1500,height:1000});
        await page.locator('#headerAccountMenu [data-language="en"]').click();
        await page.evaluate(()=>document.documentElement.dataset.theme='light');
        fs.mkdirSync('obj/workflow-design-previews',{recursive:true});
        await page.screenshot({path:'obj/workflow-design-previews/light-desktop.png',fullPage:true});
        await page.evaluate(()=>document.documentElement.dataset.theme='dark');
        await page.screenshot({path:'obj/workflow-design-previews/dark-desktop.png',fullPage:true});
        assert.deepEqual(errors,[]);
        console.log('PASS: one-row action groups, optional completion, reschedule disclosure/focus, gold star selection, Filipino/English, light/dark and mobile layout. Rendered component fixtures, no live account changes.');
    } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1});
