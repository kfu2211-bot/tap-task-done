const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('wwwroot/js/post-task-review.js', 'utf8');
async function scenario(fetch, prevented = false) {
 const button = { disabled: false, textContent: 'Review task' };
 const error = { hidden: true, scrollIntoView() {} };
 let handler, redirected;
 const fields = { title: 'Keep my task', schedule: '2026-12-01T10:00', token: 'antiforgery' };
 const form = { action: '/MarketTasks?handler=Create', reportValidity: () => true, querySelectorAll: () => [button], addEventListener: (_, callback) => handler = callback, fields };
 class FormData { constructor(form) { this.fields = { ...form.fields }; } }
 vm.runInNewContext(source, { document: { getElementById: id => id === 'databasePostTaskForm' ? form : error }, fetch, FormData, AbortController, URL, TypeError, location: { origin: 'https://test.local', assign: value => redirected = value }, setTimeout, clearTimeout });
 await handler({ defaultPrevented: prevented, preventDefault() {} });
 assert.equal(button.disabled, false);
 assert.equal(button.textContent, 'Review task');
 assert.equal(fields.title, 'Keep my task');
 return { error, redirected };
}
(async () => {
 let result = await scenario(async (_, request) => { assert.equal(request.body.fields.token, 'antiforgery'); return { ok: true, headers: new Headers({ 'content-type': 'application/json' }), json: async () => ({ success: true, redirectUrl: '/MarketTasks?reviewTask=4' }) }; });
 assert.equal(result.redirected, 'https://test.local/MarketTasks?reviewTask=4');
 result = await scenario(async () => ({ ok: false, headers: new Headers({ 'content-type': 'application/json' }), json: async () => ({ success: false, message: 'Choose a valid date' }) }));
 assert.equal(result.error.textContent, 'Choose a valid date'); assert.equal(result.error.hidden, false); assert.equal(result.redirected, undefined);
 result = await scenario(async () => { throw new TypeError('network'); });
 assert.match(result.error.textContent, /Connection interrupted/);
 result = await scenario(async () => ({ status: 400, headers: new Headers() }));
 assert.match(result.error.textContent, /session may have expired/);
 await scenario(async () => { throw new Error('must not submit'); }, true);
 console.log('PASS: review redirect, validation, interrupted connection, expired session, preserved fields/token, and earlier validation cancellation.');
})().catch(error => { console.error(error); process.exitCode = 1; });
