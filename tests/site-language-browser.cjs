const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async () => {
    const browser = await chromium.launch({headless:true,channel:'msedge'});
    try {
        const page = await browser.newPage();
        const errors = []; page.on('pageerror', error => errors.push(error.message));
        await page.route('http://language.test/**', route => route.fulfill({contentType:'text/html',body:`<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Task record - Tap, Task, Done</title></head><body>
          <div class="registration-page"><div class="register-box"><div class="signup-eyebrow" data-i18n>JOIN THE COMMUNITY</div><h1>Create an Account</h1><p class="signup-subtitle" data-i18n>Start finding trusted local help today.</p>
          <form id="registerForm"><label>NAME<input id="name" placeholder="Your full name" value="My original name" required></label><label>DATE OF BIRTH</label><button type="button" id="month">Month</button><div id="option" data-i18n>January</div><input id="email" type="email" required><button type="button" data-password-toggle="password" aria-label="Show password">Show</button><p id="dynamic" role="status"></p></form></div></div>
          <span class="job-status">Task: Expired</span><p class="compact-page-description">Jobs matching your skills</p><span class="job-reference">Task #35 · Client: Anne</span>
          <div id="headerAccountMenu"><button class="header-theme-toggle">Dark mode</button></div><div class="thread-message"><p>Show</p></div><article class="jobs-card"><h3>Cleaning</h3><p>My original task description</p></article><p class="review-profile-feedback">Continue</p><span data-no-translate>Month</span></body></html>`}));
        await page.addInitScript(()=>localStorage.setItem('tts-language','fil'));
        await page.goto('http://language.test/');
        await page.addScriptTag({content:fs.readFileSync('wwwroot/js/site-language-fil.js','utf8')});
        await page.addScriptTag({content:fs.readFileSync('wwwroot/js/site-language.js','utf8')});
        await page.waitForFunction(()=>document.querySelector('h1').textContent==='Gumawa ng account');
        assert.equal(await page.locator('#name').getAttribute('placeholder'),'Buong pangalan mo');
        assert.equal(await page.locator('#name').inputValue(),'My original name');
        assert.equal(await page.locator('.signup-eyebrow').textContent(),'SUMALI SA KOMUNIDAD');
        assert.match(await page.locator('.job-status').textContent(),/^Task: Nag-expire/);
        assert.equal(await page.locator('.job-reference').textContent(),'Task #35 · Kliyente: Anne');
        assert.equal(await page.locator('.thread-message p').textContent(),'Show');
        assert.equal(await page.locator('.jobs-card h3').textContent(),'Cleaning');
        assert.equal(await page.locator('.review-profile-feedback').textContent(),'Continue');
        await page.evaluate(()=>{document.querySelector('#dynamic').textContent='Your entries are still here. Nothing has been reset.';document.querySelector('#month').textContent=document.querySelector('#option').textContent;});
        await page.waitForFunction(()=>document.querySelector('#dynamic').textContent.startsWith('Nandito pa'));
        await page.locator('#email').evaluate(input=>input.reportValidity());
        assert.equal(await page.locator('#email').evaluate(input=>input.validationMessage),'Punan ang field na ito.');
        await page.locator('.auth-language-picker summary').click();
        await page.locator('.auth-language-picker [data-language="en"]').click();
        assert.equal(await page.locator('h1').textContent(),'Create an Account');
        assert.equal(await page.locator('#name').getAttribute('placeholder'),'Your full name');
        assert.equal(await page.locator('#month').textContent(),'January');
        assert.equal(await page.locator('#email').evaluate(input=>input.validity.customError),false);
        assert.equal(await page.locator('#dynamic').textContent(),'Your entries are still here. Nothing has been reset.');
        assert.deepEqual(errors,[]);
        await page.evaluate(() => new Promise(resolve => setTimeout(resolve, 30)));
        assert.equal(await page.locator('#headerAccountMenu .account-language-options').count(),1);
        console.log('PASS: saved Filipino preference, signup, placeholders, status prefixes, dynamic messages, copied month labels, validation, English restore and untouched user content.');
    } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1});
