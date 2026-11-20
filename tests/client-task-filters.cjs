const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const cards = ['cancelled', 'expired', 'assigned', 'completed'].map((status, index) => ({ id: `task-${index}`, dataset: { taskStatus: status, jobType: 'short' }, hidden: false, querySelector: () => ({}), scrollIntoView() {} }));
const buttons = ['all', 'closed', 'active', 'completed'].map(key => ({ dataset: { filter: key }, aria: 'false', setAttribute(_, value) { this.aria = value; }, closest() { return this; }, click() { onClick({ target: this }); } }));
let onClick;
const filters = { hidden: true, addEventListener: (_, fn) => { onClick = fn; }, contains: () => true,
    querySelectorAll: () => buttons,
    querySelector: selector => selector.includes('aria-pressed') ? buttons.find(b => b.aria === 'true') : buttons.find(b => selector.includes(`"${b.dataset.filter}"`)) };
const closed = { value: 'all', parentElement: { hidden: true }, addEventListener(_, fn) { this.change = fn; } };
const type = { value: '', addEventListener(_, fn) { this.change = fn; } };
const count = {}, empty = {};
const section = { querySelector: selector => ({ '.task-filters': filters, '#task-filter-count': count, '.task-filter-empty': empty, '#myTasksTypeFilter': type, '#clientClosedFilter': closed })[selector],
    querySelectorAll: selector => selector === '.task-card' ? cards : [] };
vm.runInNewContext(fs.readFileSync('wwwroot/js/market-task-filters.js', 'utf8'), {
    document: { getElementById: id => id === 'my-tasks-section' ? section : null },
    window: { location: { hash: '' }, addEventListener() {} }, location: { search: '?filter=cancelled' }, URLSearchParams
});
assert.deepEqual(cards.map(c => !c.hidden), [true, true, false, false]);
assert.equal(closed.parentElement.hidden, false); assert.equal(count.textContent, '2 tasks');
closed.value = 'expired'; closed.change(); assert.deepEqual(cards.map(c => !c.hidden), [false, true, false, false]); assert.equal(count.textContent, '1 task');
type.value = 'long'; type.change(); assert.equal(count.textContent, '0 tasks'); assert.equal(empty.hidden, false);
buttons[0].click(); assert.equal(closed.parentElement.hidden, true);
console.log('PASS: legacy Cancelled link opens Closed, expired subfilter and job type combine, counts and empty state update.');
