const assert = require('node:assert/strict');
const Countdown = require('../wwwroot/js/identity-capture-countdown.js');
let now=0,ready=true,captured=0,ticks=[],jobs=new Map(),next=1;
const countdown = new Countdown({canCapture:()=>ready,onTick:n=>ticks.push(n),onCapture:()=>captured++,now:()=>now,schedule:fn=>{const id=next++;jobs.set(id,fn);return id;},cancel:id=>jobs.delete(id)});
function advance(ms){now+=ms;const pending=[...jobs.values()];jobs.clear();pending.forEach(fn=>fn());}
countdown.update(true);assert.equal(ticks.at(-1),3);
advance(1000);assert.equal(ticks.at(-1),2);assert.equal(captured,0);
advance(1000);assert.equal(ticks.at(-1),1);assert.equal(captured,0);
advance(1000);assert.equal(captured,1);
countdown.update(true);advance(4000);assert.equal(captured,1,'capture fires only once');
countdown.update(false);ready=true;countdown.update(true);advance(1000);ready=false;advance(100);
assert(!countdown.active);assert.equal(captured,1,'failed face checks cancel automatic capture');
ready=true;countdown.update(true);assert.equal(ticks.at(-1),3);advance(3000);assert.equal(captured,2,'countdown restarts at 3 after face becomes clear');
countdown.update(false);ready=true;countdown.update(true);countdown.reset();advance(5000);assert.equal(captured,2,'closing or pausing camera cancels pending capture');
console.log('PASS: 3-2-1, capture once, cancel on failed checks, resume countdown and camera stop');
