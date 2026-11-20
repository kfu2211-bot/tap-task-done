const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const code = fs.readFileSync('wwwroot/js/work-preferences.js', 'utf8');
function fixture(saved, fail = false) {
    const listeners = [];
    const node = () => ({ textContent: '', hidden: false, disabled: false, addEventListener() {}, setAttribute() {} });
    const save = node(), message = node(), customInput = { value: '', required: false };
    const other = { ...node(), querySelector: () => customInput };
    const days = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'].map(value => ({ name:'Days', value, checked: saved.includes(value) }));
    const role = { value: 'freelancer' };
    const sections = ['client', 'freelancer'].map(agreementRole => ({ dataset:{ agreementRole, accepted:'true' }, querySelector:() => ({ checked:false }) }));
    const dialog = { ...node(), querySelectorAll: () => sections, querySelector: () => node() };
    const fields = { WorkRole:'freelancer', Services:'Cleaning', Areas:'Cabetican', PreferredStart:'10:00', PreferredEnd:'22:00', Availability:'Available', __RequestVerificationToken:'test-token' };
    let payload, redirect;
    const form = { action:'https://test.local/Settings', addEventListener:(type, callback) => { if(type==='submit') listeners.push(callback); }, querySelector: () => save,
        querySelectorAll: selector => selector.includes('weeklyAvailability') ? days : [], reportValidity:() => true };
    role.form = form;
    const elements = { WorkRole:role, freelancerPreferences:node(), availability:node(), workRoleHelp:node(), roleAgreementDialog:dialog, otherServiceField:other, profileSavedMessage:message };
    class FormData {
        constructor() { this.entries = [...Object.entries(fields), ...days.filter(day => day.checked).map(day => ['Days',day.value])]; }
        [Symbol.iterator]() { return this.entries[Symbol.iterator](); }
    }
    vm.runInNewContext(code, { document:{ getElementById:id => elements[id] ?? node() }, window:{ addEventListener() {} }, location:{ hash:'', href:'https://test.local/Settings#availability' }, HTMLDetailsElement: class {}, FormData, URL, AbortController, setTimeout, clearTimeout, queueMicrotask,
        fetch: async (url, request) => {
            assert.equal(url.searchParams.get('handler'), 'Availability');
            payload = [...request.body];
            assert.equal(payload.find(([key]) => key === '__RequestVerificationToken')[1], 'test-token');
            if (fail) throw new Error('Network interrupted');
            saved.splice(0, saved.length, ...payload.filter(([key]) => key === 'Days').map(([,value]) => value));
            return { ok:true, headers:new Headers({'content-type':'application/json'}), json:async () => ({success:true,message:'Availability saved.'}) };
        } });
    return { days, fields, save, message, payload:() => payload, async submit() { const event = { defaultPrevented:false, preventDefault(){ this.defaultPrevented=true; }, stopImmediatePropagation(){} }; for(const callback of listeners) await callback(event); return event; } };
}
(async () => {
    const stored = ['Mon','Tue','Wed','Thu','Fri','Sat'];
    const first = fixture(stored);
    first.days[6].checked = true;
    await first.submit();
    assert.equal(stored.length,7);
    assert.equal(first.message.textContent,'Availability saved.');
    assert.equal(fixture(stored).days[6].checked,true, 'Sunday remains checked on reload from saved days');
    const failed = fixture(stored,true);
    failed.days[6].checked = false;
    await failed.submit();
    assert.equal(failed.days[6].checked,false,'Failed save preserves pending selection');
    assert.equal(failed.save.disabled,false,'Failed save allows retry');
    assert.equal(stored.length,7,'Failed save does not pretend persistence');
    const combined = fixture(stored);
    combined.days[6].checked = false;
    combined.fields.Services = 'Repairs';
    assert.equal((await combined.submit()).defaultPrevented,false,'Other preference edits keep the full save path');
    assert.equal(combined.payload(),undefined);
    const clear = fixture(stored);
    clear.days.forEach(day => day.checked=false);
    await clear.submit();
    assert.equal(stored.length,0,'All days can be cleared');
    console.log('PASS: Sunday save/reload, failed save preserves selections and retry, combined edits retain full-save path, all days cleared.');
})().catch(error => { console.error(error); process.exitCode=1; });
