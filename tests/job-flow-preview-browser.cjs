const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const read = path => fs.readFileSync(path, 'utf8');
(async () => {
    const browser = await chromium.launch({headless:true, channel:'msedge'});
    try {
        const page = await browser.newPage();
        const errors = []; page.on('pageerror', error => errors.push(error.message));
        let mode = 'cooldown', conflict = true;
        await page.route('http://flow.test/**', async route => {
            const url = new URL(route.request().url());
            if (url.pathname === '/api/jobs/schedule-check') return route.fulfill({contentType:'application/json', body:JSON.stringify({canAccept:!conflict,message:conflict?'This schedule conflicts with accepted work or an unfinished schedule. Agree on different times before accepting.':''})});
            if (route.request().method() === 'POST') return route.fulfill({status:mode==='failure'?503:429,contentType:'application/json',body:JSON.stringify(mode==='failure'?{message:'Unable to send a sign-in code. Try again later. No new session was created.'}:{message:mode==='limit'?'Too many verification requests. Wait 15 minutes before trying again.':'Wait until the countdown ends before requesting another code.',retryAfterSeconds:1,reason:mode==='limit'?'otp_request_limit':'otp_cooldown'})});
            return route.fulfill({contentType:'text/html',body:`<!doctype html><html><meta charset="utf-8"><body><div id="headerAccountMenu"><button class="header-theme-toggle">Dark mode</button></div><main style="max-width:600px;margin:20px auto;padding:16px"><h1>Log in to your account</h1><form id="loginForm" action="/Login?handler=Login"><input id="loginEmail" type="email" value="person@example.test"><input id="loginPassword" type="password" value="test-only"><button type="submit"><span class="login-button-label">Log in</span><span class="login-spinner" hidden></span></button><p id="loginStatus" role="status"></p></form><div class="login-box"><form data-resend-seconds="10"><button type="submit">Resend code</button><small data-resend-label></small></form><form data-resend-seconds="15"><button type="submit">Resend code</button><small data-resend-label></small></form></div><h2>Booking requests</h2><form method="post" action="/BookingRequests?handler=Decide"><input name="id" value="42" type="hidden"><button name="decision" value="accept">Accept request</button><button name="decision" value="decline">Decline</button></form><section class="flow-notice"><strong data-i18n>No cancellation decision after 24 hours.</strong><p data-i18n>You can request admin review. No automatic cancellation or penalty applies.</p><button class="flow-button" data-i18n>Ask admin for review</button></section></main></body></html>`});
        });
        await page.addInitScript(()=>localStorage.setItem('tts-language','fil'));
        await page.goto('http://flow.test/');
        for (const css of ['account-theme','theme-consistency','site-language','job-flow-feedback']) await page.addStyleTag({content:read(`wwwroot/css/${css}.css`)});
        for (const js of ['site-language-fil','site-language','job-schedule-check','otp-resend']) await page.addScriptTag({content:read(`wwwroot/js/${js}.js`)});
        const loginSource = read('Pages/Login.cshtml').match(/<script>\s*(const loginForm[\s\S]+?)<\/script>/)[1];
        await page.addScriptTag({content:loginSource});
        assert.equal(await page.locator('[data-resend-seconds="10"] button').isDisabled(),true);
        assert.equal(await page.locator('[data-resend-seconds="15"] button').isDisabled(),true);
        assert.match(await page.locator('[data-resend-seconds="10"] small').innerText(),/Maaaring humiling muli sa 00:(10|09)/);
        assert.match(await page.locator('[data-resend-seconds="15"] small').innerText(),/00:(15|14)/);
        await page.waitForFunction(()=>document.querySelector('button[value="accept"]').disabled);
        assert.equal(await page.locator('button[value="decline"]').isEnabled(), true);
        assert.match(await page.locator('[data-flow-message]').innerText(),/May kasabay/);
        assert.match(await page.locator('.flow-notice strong').innerText(),/Wala pang desisyon/);
        await page.locator('#loginForm button').click();
        await page.waitForFunction(()=>document.getElementById('loginStatus').textContent.includes('Hintaying'));
        assert.equal(await page.locator('#loginForm button').isDisabled(),true);
        assert.equal(await page.locator('#loginPassword').inputValue(),'test-only');
        await page.waitForFunction(()=>!document.querySelector('#loginForm button').disabled);
        mode = 'limit'; await page.locator('#loginForm button').click();
        await page.waitForFunction(()=>document.getElementById('loginStatus').classList.contains('flow-limit'));
        assert.match(await page.locator('#loginStatus').innerText(),/Masyadong maraming/);
        await page.waitForFunction(()=>!document.querySelector('#loginForm button').disabled);
        mode = 'failure'; await page.locator('#loginForm button').click();
        await page.waitForFunction(()=>document.getElementById('loginStatus').classList.contains('flow-danger'));
        assert.equal(await page.locator('#loginForm button').isEnabled(),true);
        conflict = false;
        await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
        await page.waitForFunction(()=>!document.querySelector('button[value="accept"]').disabled);
        for (const width of [390, 1280]) for (const theme of ['light', 'dark']) {
            await page.setViewportSize({width,height:900});
            await page.evaluate(value=>document.documentElement.dataset.theme=value,theme);
            const colors = await page.locator('section.flow-notice p').evaluate(el=>({text:getComputedStyle(el).color,bg:getComputedStyle(el.closest('section')).backgroundColor}));
            assert.notEqual(colors.text, colors.bg);
            assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
            if (theme==='dark') assert.equal(colors.bg,'rgb(56, 47, 30)');
        }
        await page.locator('#headerAccountMenu [data-language="en"]').click();
        assert.equal(await page.locator('section.flow-notice strong').innerText(),'No cancellation decision after 24 hours.');
        assert.deepEqual(errors,[]);
        console.log('PASS: real login script distinguishes cooldown, limit and email failure; 10/15-second resend timers, preserved inputs, booking conflict preview/decline/recheck, Filipino/English, dark/light and mobile widths. Mock requests only.');
    } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
