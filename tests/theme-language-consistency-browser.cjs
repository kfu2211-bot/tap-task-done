const fs = require('node:fs'), assert = require('node:assert/strict');
const { chromium } = require('C:/Users/Mr_Jh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const read = path => fs.readFileSync(path, 'utf8');
// Use the real page stylesheet order, including legacy inline rules.
function styles(page) {
    const source = read(`Pages/${page}.cshtml`);
    const head = source.slice(0, source.indexOf('</head>')).replace('<partial name="_AccountTheme" />', read('Pages/Shared/_AccountTheme.cshtml'));
    return [...head.matchAll(/href="[~]?(\/css\/[^"?]+\.css)"/g)].map(m => read('wwwroot' + m[1])).join('\n') + '\n' +
        [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)].map(m => m[1].replaceAll('@@', '@')).join('\n');
}
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const page = await browser.newPage(), errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.route('http://consistency.test/**', r => r.fulfill({ contentType: 'text/html', body: '<html><body></body></html>' }));
        await page.goto('http://consistency.test/');
        for (const name of ['MarketTasks', 'TaskDetails', 'JobRecord']) {
            const badge = name === 'MarketTasks' ? 'offer-status' : name === 'TaskDetails' ? 'task-detail-status' : 'record-status';
            const wrapper = name === 'MarketTasks' ? 'market-content' : name === 'TaskDetails' ? 'task-detail-main' : 'task-reuse-page';
            const card = name === 'MarketTasks' ? 'task-card' : name === 'TaskDetails' ? 'task-detail-card' : 'task-record';
            for (const theme of ['light', 'dark']) {
                await page.setContent(`<html data-theme="${theme}"><meta name="viewport" content="width=device-width,initial-scale=1"><style>${styles(name)}</style><body class="${name === 'JobRecord' ? 'task-reuse-screen' : ''}"><main class="${wrapper}"><article class="${card}">${['pending', 'accepted', 'withdrawn', 'cancelled'].map(s => `<span class="${badge}" data-status="${s}">${s}</span>`).join('')}</article></main></body></html>`);
                await page.addScriptTag({ content: read('wwwroot/js/workflow-design-system.js') });
                const tones = await page.locator('.workflow-status').evaluateAll(es => es.map(e => ({ color: getComputedStyle(e).color, background: getComputedStyle(e).backgroundColor })));
                assert.equal(new Set(tones.map(t => t.background)).size, 4, `${name}/${theme}: distinct status colors`);
                const luminance = color => {
                    const a = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; });
                    return a[0] * .2126 + a[1] * .7152 + a[2] * .0722;
                };
                for (const tone of tones) { const a = luminance(tone.color), b = luminance(tone.background); assert((Math.max(a, b) + .05) / (Math.min(a, b) + .05) >= 4.5, `${name}/${theme}: readable badge`); }
            }
        }
        await page.setContent('<html data-theme="dark"><body><div id="headerAccountMenu"><button class="header-theme-toggle">Dark mode</button></div><main class="task-reuse-page"><h1>Task history #123</h1><p data-stale-agreement>New agreement terms are available. <a href="#">Reload and review the latest version</a></p><p class="job-location-status">Recent freelancer location · 23/11/2026, 10:00 PM PHT · accuracy ±20 m · Not live</p><p id="pin" role="status">Finding the address for this pin…</p><p translate="no" id="user">New agreement terms are available.</p><button title="Fullscreen map">Map</button></main><div class="admin-sessions-page"><h1 id="sessionsTitle">User Sessions</h1><p class="note">Sign-in history and site presence · Philippine time (UTC+8)</p></div><div class="users-page"><p class="session-feedback">Open to load sessions.</p><div class="history-content"><p id="adminNote">Continue</p></div></div><p id="faceStepCount">3 of 5 steps saved</p></body></html>');
        await page.evaluate(() => localStorage.setItem('tts-language', 'fil'));
        for (const js of ['site-language-fil', 'site-language']) await page.addScriptTag({ content: read(`wwwroot/js/${js}.js`) });
        assert.equal(await page.locator('.task-reuse-page h1').textContent(), 'Kasaysayan ng gawain #123');
        assert.equal(await page.locator('#sessionsTitle').textContent(), 'Mga session ng user');
        assert.equal(await page.locator('.session-feedback').textContent(), 'Buksan para i-load ang mga session.');
        assert.equal(await page.locator('#adminNote').textContent(), 'Continue', 'Admin-entered notes remain original');
        assert.equal(await page.locator('#faceStepCount').textContent(), 'Na-save ang 3 sa 5 hakbang');
        assert.match(await page.locator('[data-stale-agreement]').innerText(), /May bagong detalye/);
        assert.match(await page.locator('.job-location-status').textContent(), /katumpakan ±20 m · Hindi live/);
        assert.equal(await page.locator('#pin').textContent(), 'Hinahanap ang address ng pin na ito…');
        assert.equal(await page.locator('#user').textContent(), 'New agreement terms are available.');
        assert.equal(await page.locator('button[title]').getAttribute('title'), 'Palakihin ang mapa');
        await page.evaluate(() => document.querySelector('#pin').textContent = 'Work destination confirmed. This pin will be saved with your task or proposal.');
        await page.waitForFunction(() => document.querySelector('#pin').textContent.startsWith('Kumpirmado'));
        await page.locator('#headerAccountMenu [data-language="en"]').click();
        assert.equal(await page.locator('.task-reuse-page h1').textContent(), 'Task history #123');
        assert.equal(await page.locator('#faceStepCount').textContent(), '3 of 5 steps saved');
        assert.match(await page.locator('.job-location-status').textContent(), /accuracy ±20 m · Not live/);
        assert.equal(await page.locator('button[title]').getAttribute('title'), 'Fullscreen map');
        await page.addScriptTag({ content: read('wwwroot/js/map-theme.js') });
        const mapResult = await page.evaluate(async () => {
            const calls = [], listeners = {}; let restored = 0;
            const map = { on: (event, handler) => listeners[event] = handler, setStyle: style => { calls.push(style); listeners['style.load'](); } };
            const initial = window.ttsMapTheme.style();
            window.ttsMapTheme.bind(map, () => restored++);
            document.documentElement.dataset.theme = 'light'; await new Promise(r => setTimeout(r, 0));
            document.documentElement.dataset.theme = 'dark'; await new Promise(r => setTimeout(r, 0));
            listeners.remove(); document.documentElement.dataset.theme = 'light'; await new Promise(r => setTimeout(r, 0));
            return { initial, calls, restored };
        });
        assert.match(mapResult.initial, /\/dark$/);
        assert.deepEqual(mapResult.calls.map(s => s.split('/').pop()), ['liberty', 'dark']);
        assert.equal(mapResult.restored, 2, 'Restore route overlays after each style change; disconnect when removed');
        assert.deepEqual(errors, []);
        console.log('PASS: actual-page status CSS/contrast in light+dark; Filipino dynamic copy and English restoration; user content preserved; map theme switching restores overlays. Component fixtures, no database writes.');
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
