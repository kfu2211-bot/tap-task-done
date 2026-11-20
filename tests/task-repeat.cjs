const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const script = fs.readFileSync('wwwroot/js/task-repeat.js', 'utf8');
function fixture(response) {
    let submit, destination;
    const button = { textContent: 'Save new draft', disabled: false };
    const message = { hidden: true, focus() {}, scrollIntoView() {} };
    const form = { action: 'https://app.test/MarketTasks?handler=Create', draft: 'Keep my edits',
        addEventListener: (_, fn) => { submit = fn; },
        querySelector: selector => selector.includes('button') ? button : message };
    vm.runInNewContext(script, {
        document: { querySelector: () => form }, URL, FormData: class {},
        AbortSignal: { timeout() {} }, fetch: async () => { if (response instanceof Error) throw response; return response; },
        DOMParser: class { parseFromString() { return { querySelector: () => null }; } },
        location: { href: 'https://app.test/RepeatTask?sourceTaskId=17', origin: 'https://app.test', assign: url => { destination = url; } }
    });
    return { button, message, form, submit: () => submit({ defaultPrevented: false, preventDefault() {} }), destination: () => destination };
}
const json = (ok, result) => ({ ok, headers: { get: () => 'application/json' }, json: async () => result });
(async () => {
    let f = fixture(json(true, { success: true, redirectUrl: '/MarketTasks?view=mine&reviewTask=99#task-99' }));
    await f.submit(); assert.match(f.destination(), /reviewTask=99/);
    f = fixture(json(false, { success: false, message: 'Choose a future schedule.' }));
    await f.submit(); assert.match(f.message.textContent, /future schedule/); assert.equal(f.form.draft, 'Keep my edits'); assert.equal(f.button.disabled, false); assert.equal(f.destination(), undefined);
    f = fixture(new Error('Connection failed'));
    await f.submit(); assert.match(f.message.textContent, /entries are still here/); assert.equal(f.button.disabled, false);
    f = fixture({ ok: true, redirected: true, url: 'https://app.test/MarketTasks?view=mine', headers: { get: () => 'text/html' } });
    await f.submit(); assert.match(f.destination(), /MarketTasks/);
    f = fixture({ ok: true, redirected: true, url: 'https://app.test/FindPost?providerId=2', headers: { get: () => 'text/html' }, text: async () => '' });
    await f.submit(); assert.equal(f.destination(), undefined); assert.match(f.message.textContent, /availability/);
    console.log('PASS: repost redirect, new booking redirect, validation, unavailable provider and interrupted connection preserve form edits.');
})().catch(error => { console.error(error); process.exitCode = 1; });
