const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('wwwroot/js/job-offer-form.js', 'utf8');
const element = () => ({ hidden: false, textContent: '', children: [], setAttribute() {}, append(...children) { this.children.push(...children); }, replaceChildren() { this.children = []; } });
function fixture(result) {
    let submit;
    const button = { disabled: false, textContent: 'Send offer' }, feedback = element(), toggle = element();
    const card = { dataset: {}, querySelector: () => feedback, querySelectorAll: () => [form, toggle] };
    const form = { action: '/MarketTasks?handler=Offer', hidden: false, draft: 'My message', closest: () => card,
        addEventListener: (_, handler) => { submit = handler; }, querySelector: () => button };
    vm.runInNewContext(source, {
        document: { querySelectorAll: selector => selector === '.job-offer-toggle' ? [] : [form], createElement: element },
        FormData: class { get() { return '35'; } }, AbortSignal: { timeout() {} },
        fetch: async () => { if (result instanceof Error) throw result; return { redirected: false, ok: result.success, headers: { get: () => 'application/json' }, json: async () => result }; }
    });
    return { form, feedback, button, card, submit: () => submit({ defaultPrevented: false, preventDefault() {}, submitter: button }) };
}
(async () => {
    let f = fixture({ success: true }); await f.submit();
    assert.equal(f.form.hidden, true); assert.match(f.feedback.children[0].textContent, /Pending/);
    assert.equal(f.feedback.children[1].href, '/MyJobs?view=offers&offerFilter=pending#job-35');
    f = fixture({ success: false, message: 'The posted pay has changed.' }); await f.submit();
    assert.equal(f.form.hidden, false); assert.match(f.feedback.textContent, /posted pay/); assert.equal(f.form.draft, 'My message'); assert.equal(f.button.disabled, false);
    f = fixture(new Error('Connection interrupted')); await f.submit(); assert.match(f.feedback.textContent, /entries are still here/); assert.equal(f.card.dataset.offerSending, undefined);
    console.log('PASS: inline offer confirmation, duplicate-control removal, My offers link, and failed-send draft preservation.');
})().catch(error => { console.error(error); process.exitCode = 1; });
