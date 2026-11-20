const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

(async () => {
    const node = () => ({ textContent: '', hidden: false, href: '' });
    const nodes = Object.fromEntries(['title', 'time', 'link', 'help', 'status'].map(key => [key, node()]));
    const details = { hidden: false, querySelector: selector => nodes[selector.match(/data-scheduled-(\w+)/)[1]] };
    const empty = node(), heading = node();
    const counts = [node(), node(), node()];
    let payload, refresh;
    const overview = { dataset: { freelancerSummary: '/Dashboard?handler=FreelancerSummary' }, querySelectorAll: () => counts };
    const document = {
        hidden: false, addEventListener() {},
        querySelector: selector => ({ '[data-freelancer-summary]': overview, '[data-scheduled-details]': details,
            '[data-scheduled-empty]': empty, '[data-schedule-heading]': heading })[selector]
    };
    vm.runInNewContext(fs.readFileSync('wwwroot/js/freelancer-home-summary.js', 'utf8'), {
        document, window: { addEventListener() {} }, setInterval: callback => { refresh = callback; },
        AbortSignal: { timeout() {} }, fetch: async () => ({ ok: true, redirected: false, json: async () => payload })
    });
    payload = { counts: [0, 1, 0], scheduled: { title: 'Missed job', scheduleText: 'Nov 20, 2026, 9:00 AM', status: 'Schedule overdue', url: '/Tracking?taskId=1' } };
    await refresh();
    assert.equal(heading.textContent, 'Schedule needs attention');
    assert.equal(nodes.status.textContent, 'Schedule overdue');
    assert.equal(nodes.help.hidden, false);
    assert.equal(details.hidden, false);
    assert.equal(nodes.link.href, '/Tracking?taskId=1');
    payload.scheduled = { ...payload.scheduled, status: 'Scheduled' };
    await refresh();
    assert.equal(heading.textContent, 'Scheduled job');
    assert.equal(nodes.help.hidden, true);
    payload.scheduled = null;
    await refresh();
    assert.equal(details.hidden, true);
    assert.equal(empty.hidden, false);
    assert.equal(empty.textContent, 'No confirmed job schedule yet.');
    console.log('PASS: Home refresh preserves overdue job, updates attention status and actions, and clears resolved schedule.');
})().catch(error => { console.error(error); process.exitCode = 1; });
