const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async () => {
    const browser = await chromium.launch({headless:true,channel:'msedge'});
    try {
        const page = await browser.newPage({viewport:{width:1440,height:1100}});
        const errors = []; page.on('pageerror', e => errors.push(e.message));
        let calls = 0, fail = false;
        await page.route('http://users.test/**', async route => {
            const pathname = new URL(route.request().url()).pathname;
            if (pathname === '/fixture') return route.fulfill({contentType:'text/html',body:fs.readFileSync('obj/users-preview.html','utf8')});
            const file = path.resolve('wwwroot', '.' + pathname);
            if (file.startsWith(path.resolve('wwwroot') + path.sep) && fs.existsSync(file) && fs.statSync(file).isFile())
                return route.fulfill({body:fs.readFileSync(file),contentType:file.endsWith('.js')?'application/javascript':file.endsWith('.css')?'text/css':'image/svg+xml'});
            return route.fulfill({status:204});
        });
        await page.route('**/AdminUsers?**', async route => {
            const url = new URL(route.request().url());
            if (url.searchParams.get('handler') !== 'Sessions') return route.continue();
            calls++;
            if (fail) return route.fulfill({status:503,body:'Unavailable'});
            assert.equal(url.searchParams.get('userId'),'1');
            const n = Number(url.searchParams.get('pageNumber'));
            await route.fulfill({contentType:'application/json',body:JSON.stringify({hasNext:n===1,rows:[{timeIn:'Nov 22, 2026 8:00 AM',lastActive:'Nov 22, 2026 9:00 AM',timeOut:'—',status:n===1?'Online':'<script>unsafe</script>'}]})});
        });
        await page.goto('http://users.test/fixture');
        assert.equal(await page.locator('.user-card').count(),2);
        assert.equal(await page.locator('input[type="search"]').count(),0);
        assert.equal(await page.locator('.session-summary').count(),2);
        assert.equal(calls,0,'History should load only when opened');
        const history = page.locator('.session-history').first();
        await history.locator('summary').click();
        await page.waitForFunction(()=>document.querySelectorAll('.session-entry').length===1);
        assert.equal(calls,1);
        fail = true;
        await history.locator('button').click();
        await page.waitForFunction(()=>document.querySelector('.session-more').textContent.includes('Retry'));
        assert.equal(await history.locator('.session-entry').count(),1,'Error retains existing history');
        fail = false;
        await history.locator('button').click();
        await page.waitForFunction(()=>document.querySelectorAll('.session-entry').length===2);
        assert.equal(await history.locator('script').count(),0,'Session values render as text');
        assert.match(await history.textContent(),/<script>unsafe<\/script>/);
        assert.equal(await history.locator('button').isHidden(),true);
        await history.locator('summary').click(); await history.locator('summary').click();
        assert.equal(calls,3,'Reopening does not duplicate requests');
        await page.screenshot({path:'obj/users-unified-desktop.png',fullPage:true});
        await page.setViewportSize({width:390,height:844});
        assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),'No horizontal overflow on mobile');
        await page.screenshot({path:'obj/users-unified-mobile.png',fullPage:true});
        assert.deepEqual(errors,[]);
        console.log('PASS: actual Razor cards, no search, lazy history, pagination, retry, text escaping, mobile layout; fixture also checks EF query translation without database access.');
    } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
