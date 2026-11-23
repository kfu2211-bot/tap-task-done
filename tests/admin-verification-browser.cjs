const fs=require('fs'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 let card=fs.readFileSync('Pages/Shared/_AdminAccountVerification.cshtml','utf8').replace(/^@model.*\r?\n/,'').replace('@if (Model.CanManage) {','').replace(/} else \{ <p data-i18n>Only administrators[\s\S]*?<\/p> \}/,'');
 card=card.replaceAll('@(Model.State.EmailVerified ? "completed" : "pending")','completed').replaceAll('@(Model.State.IdentityStatus == "approved" ? "completed" : "pending")','completed').replaceAll('@Model.State.EmailVerified.ToString().ToLowerInvariant()','true').replaceAll('@Context.Request.Query["embedded"]','false');
 for(const [a,b] of Object.entries({'@Model.State.EmailLabel':'Verified','@Model.State.IdentityLabel':'Verified','@Model.State.IdentityStatus':'approved','@Model.State.Revision':'fixture-revision','@Model.State.Email':'sample@example.test','@Model.UserId':'101','@Model.Name':'Sample Account'})) card=card.replaceAll(a,b);
 assert(!card.includes('@Model'),'Fixture renders all model values');
 const dialog=fs.readFileSync('Pages/Shared/_AdminVerificationDialog.cshtml','utf8');
 await page.route('http://admin.test/**',r=>r.fulfill({contentType:'text/html',body:`<!doctype html><html><meta charset="utf-8"><body style="margin:0"><div id="headerAccountMenu"><button class="header-theme-toggle">Dark mode</button></div><main style="max-width:1000px;margin:20px auto;padding:16px"><h1>Users</h1><article class="user-card"><h2>Sample Account</h2>${card}</article>${dialog}</main></body></html>`}));await page.goto('http://admin.test/');
 for(const c of ['admin-users','light-system','admin-design','account-theme','workflow-polish','workflow-design-system','admin-account-verification'])await page.addStyleTag({content:fs.readFileSync(`wwwroot/css/${c}.css`,'utf8')});
 for(const j of ['site-language-fil','site-language','admin-account-verification'])await page.addScriptTag({content:fs.readFileSync(`wwwroot/js/${j}.js`,'utf8')});
 await page.evaluate(()=>{const form=document.querySelector('[data-verification-form]');window.verificationPosts=[];form.addEventListener('submit',event=>{if(form.elements.confirmChange.value==='true'){event.preventDefault();window.verificationPosts.push(Object.fromEntries(new FormData(form)));}});});
 await page.locator('[data-account-verification]').evaluate(e=>e.open=true);
 await page.locator('[name=change]').selectOption('unverify');await page.locator('[name=reason]').fill('Testing override <script>window.bad=true</script>');
 await page.locator('[data-verification-form] button[type=submit]').click();await page.waitForFunction(()=>document.getElementById('adminVerificationDialog').open);
 assert.equal(await page.locator('[data-verification-reason]').innerText(),'Testing override <script>window.bad=true</script>');assert.equal(await page.evaluate(()=>window.bad),undefined);
 await page.locator('#adminVerificationDialog button[value=cancel]').click();assert.equal(await page.evaluate(()=>window.verificationPosts.length),0);assert.equal(await page.locator('[name=confirmChange]').inputValue(),'false');
 await page.locator('[data-verification-form] button[type=submit]').click();await page.locator('#adminVerificationDialog button[value=confirm]').click();await page.waitForFunction(()=>window.verificationPosts.length===1);
 assert.equal(await page.evaluate(()=>window.verificationPosts[0].confirmChange),'true');assert.equal(await page.evaluate(()=>window.verificationPosts[0].type),'identity');assert.equal(await page.evaluate(()=>window.verificationPosts[0].change),'unverify');
 await page.evaluate(()=>window.dispatchEvent(new Event('pageshow')));assert.equal(await page.locator('[name=confirmChange]').inputValue(),'false');
 await page.locator('[name=type]').selectOption('email');assert.match(await page.locator('[data-verification-impact]').innerText(),/sessions/);
 await page.locator('[name=change]').selectOption('verify');assert.match(await page.locator('[data-verification-impact]').innerText(),/OTP/);
 fs.mkdirSync('obj/admin-verification-previews',{recursive:true});
 for(const language of ['en','fil']){await page.locator(`#headerAccountMenu [data-language="${language}"]`).click();if(language==='fil')assert.match(await page.locator('.admin-verification>summary strong').innerText(),/Pagpapatunay/);
 for(const theme of ['light','dark'])for(const width of [390,768,1500]){
 await page.setViewportSize({width,height:1000});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`No overflow ${language}/${theme}/${width}`);
 const surfaces=await page.locator('.admin-verification').evaluate(e=>({bg:getComputedStyle(e).backgroundColor,fg:getComputedStyle(e.querySelector('summary strong')).color}));
 assert.notEqual(surfaces.bg,surfaces.fg,'Verification heading must remain visible');
 if(theme==='dark')assert.equal(surfaces.bg,'rgb(28, 41, 35)','Verification card uses dark surface');
 if(width===1500)await page.screenshot({path:`obj/admin-verification-previews/${language}-${theme}.png`,fullPage:true});
 }}
 assert.deepEqual(errors,[]);console.log('PASS: real verification partial/dialog support review, cancel, confirm, safe reasons, account/type selection, bilingual effects, dark/light and mobile widths. No account changes or live database writes.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
