const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const nodes=new Map();let countdown,guide,photos=0,stopped=0,clock=10000,capturePromise;
const jobs=new Map();let nextJob=1;
const BaseCountdown=require('../wwwroot/js/identity-capture-countdown.js');
function node(id){
 if(!nodes.has(id))nodes.set(id,{value:'',files:[],checked:false,hidden:false,disabled:false,textContent:'',dataset:{},listeners:{},addEventListener(name,fn){(this.listeners[name]??=[]).push(fn);},reportValidity:()=>true});
 return nodes.get(id);
}
class Countdown extends BaseCountdown {
 constructor(options){
  const original=options.onCapture;
  super({...options,onCapture:()=>{capturePromise=original();return capturePromise;},now:()=>clock,schedule:fn=>{const id=nextJob++;jobs.set(id,fn);return id;},cancel:id=>jobs.delete(id)});
  countdown=this;
 }
}
function advance(ms){clock+=ms;const pending=[...jobs.values()];jobs.clear();pending.forEach(fn=>fn());}
class Guide {
 constructor(video,steps,ready){guide=this;this.ready=ready;this.frames=[];this.index=0;this.complete=false;this.resumes=[];}
 stop(){this.ready(false);}reset(){this.index=0;this.frames=[];this.complete=false;}async load(){}
 start(resume){this.resumes.push(resume);this.frames=[1,2,3,4,5];this.index=5;this.complete=true;this.lastPassedAt=clock;this.ready(true);}
 async captureSelfie(){photos++;this.ready(false);return new Blob(['checked-frame'],{type:'image/jpeg'});}
}
class DataTransfer {constructor(){this.files=[];this.items={add:file=>this.files.push(file)};}}
class FormData {set(){}}
node('captureSteps').value='["front","left","right","tilt","front"]';
node('governmentId').files=[{name:'test.jpg'}];node('idNumber').value='A0123456789';
node('documentType').value="Driver's License";node('documentType').selectedOptions=[{dataset:{numberLabel:'License No.',numberHelp:'Use License No.'}}];
node('selfieConsent').checked=true;node('manualIdReview').value='false';node('continueManualId').hidden=true;
node('identityForm').elements={namedItem:()=>({value:'csrf'})};node('identityForm').action='https://example.test/IdentityVerification';
node('selfieVideo').videoWidth=640;node('selfieVideo').play=async()=>{};
let appendedCountdown=false;
const document={hidden:false,getElementById:id=>id==='selfieCountdown'&&!appendedCountdown?null:node(id),addEventListener(){},
 createElement:()=>({setAttribute(){}}),querySelector:()=>({append(element){nodes.set(element.id,element);appendedCountdown=true;}})};
vm.runInNewContext(fs.readFileSync('wwwroot/js/identity-selfie.js','utf8'),{
 document,window:{IdentityFaceGuide:Guide,IdentityCaptureCountdown:Countdown,addEventListener(){}},
 navigator:{mediaDevices:{getUserMedia:async()=>({active:true,getTracks:()=>[{stop(){stopped++;}}],getVideoTracks:()=>[{addEventListener(){}}]})}},
 FormData,DataTransfer,File,Blob,URL,AbortController,setTimeout,clearTimeout,performance:{now:()=>clock},
 fetch:async()=>({ok:true,headers:{get:()=> 'application/json'},json:async()=>({success:true,token:'matched-receipt',message:'Matched.'})})
});
async function click(id){for(const fn of node(id).listeners.click||[])await fn();}
(async()=>{
 await click('openCamera');assert(countdown.active);assert.equal(photos,0);
 assert(appendedCountdown,'missing countdown markup is repaired instead of crashing tracking');
 assert.match(node('selfieCountdown').textContent,/3/);
 advance(1000);assert.match(node('selfieCountdown').textContent,/2/);
 advance(1000);assert.match(node('selfieCountdown').textContent,/1/);
 advance(1000);await capturePromise;
 assert.equal(photos,1);assert.equal(node('selfieFile').files.length,1);assert.equal(node('selfiePreview').hidden,false);assert.equal(stopped,1);assert.equal(guide.frames.length,5);
 await countdown.onCapture();assert.equal(photos,1,'stopped camera cannot take duplicate photos');
 await click('retakeSelfie');assert.equal(guide.resumes.at(-1),true,'retake keeps the five accepted movement frames');
 guide.ready(false);await countdown.onCapture();assert.equal(photos,1,'failed current checks block countdown completion');
 console.log('PASS: countdown captures the inspected selfie automatically, closes camera, prevents duplicates, keeps movements on retake, and blocks failed checks');
})().catch(error=>{console.error(error);process.exitCode=1;});
