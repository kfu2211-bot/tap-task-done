const assert=require('node:assert/strict');
const {FaceGuide}=require('../wwwroot/js/identity-face-guide.js');
const resume={hidden:true};global.document={getElementById:()=>resume};
let loaded=0,ticked=0,closed=0;
const guide={index:5,frames:[1,2,3,4,5],version:0,running:true,update(message){this.message=message;},stop(){this.running=false;this.version++;},
 detector:{close(){closed++;}},hands:{close(){closed++;}},visibility:{session:{async release(){closed++;}}},
 async load(){loaded++;},tick(){ticked++;}};
(async()=>{
 await FaceGuide.prototype.recover.call(guide);
 assert.equal(guide.index,5);assert.equal(guide.frames.length,5);assert.equal(closed,3);assert.equal(loaded,1);assert.equal(ticked,1);assert(guide.running);assert(resume.hidden);
 guide.recoveryAttempts=2;await FaceGuide.prototype.recover.call(guide);assert(!resume.hidden);assert.equal(ticked,1,'persistent failure waits for explicit resume');
 await FaceGuide.prototype.recover.call(guide,true);assert.equal(ticked,2);assert.equal(guide.frames.length,5);
 guide.load=async()=>{throw Error('offline');};await FaceGuide.prototype.recover.call(guide,true);assert(!resume.hidden);assert.match(guide.message,/could not reconnect/);assert.equal(guide.frames.length,5);
 console.log('PASS: native recovery retains 5/5 frames, reloads models, resumes checks, bounds retries and keeps manual resume available');
})().catch(error=>{console.error(error);process.exitCode=1;});
