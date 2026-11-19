const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
function fixture(result){
 const nodes=new Map();let camera=0;
 function node(id){if(!nodes.has(id))nodes.set(id,{id,value:'',hidden:false,disabled:false,checked:false,textContent:'',dataset:{},files:[],listeners:{},addEventListener(name,fn){(this.listeners[name]??=[]).push(fn);},reportValidity(){return true;},getTracks(){return[];}});return nodes.get(id);}
 const file={name:'test-id.jpg',size:100,type:'image/jpeg'};node('governmentId').files=[file];
 node('documentType').value="Driver's License";node('documentType').selectedOptions=[{dataset:{numberLabel:'License No.',numberHelp:'Use License No.'}}];
 node('idNumber').value='A01-23-456789';node('captureSteps').value='["front","left","right","tilt","front"]';
 node('selfieConsent').checked=true;node('manualIdReview').value='false';node('continueManualId').hidden=true;
 node('identityForm').elements={namedItem:()=>({value:'csrf'})};node('identityForm').action='https://example.test/IdentityVerification';
 node('selfieVideo').videoWidth=640;node('selfieVideo').videoHeight=480;node('selfieVideo').play=async()=>{};
 const document={hidden:false,getElementById:node,addEventListener(){}};
 class Guide{constructor(v,steps,ready){this.ready=ready;}stop(){this.ready(false);}reset(){}async load(){}start(){}}
 class Form{set(){}}
 vm.runInNewContext(fs.readFileSync('wwwroot/js/identity-selfie.js','utf8'),{document,window:{IdentityFaceGuide:Guide,addEventListener(){}},navigator:{mediaDevices:{getUserMedia:async()=>{camera++;return{active:true,getTracks:()=>[],getVideoTracks:()=>[{addEventListener(){}}]};}}},FormData:Form,URL,AbortController,setTimeout,clearTimeout,fetch:async()=>({ok:true,headers:{get:()=> 'application/json'},json:async()=>result})});
 return{node,get camera(){return camera;},async click(id){for(const fn of node(id).listeners.click||[])await fn();},async input(id){for(const fn of node(id).listeners.input||[])await fn();}};
}
(async()=>{
 let f=fixture({success:false,canManualReview:true,token:'signed-manual',message:'Timed out.'});
 await f.click('openCamera');assert.equal(f.camera,0);assert(!f.node('continueManualId').hidden);
 await f.click('continueManualId');assert.equal(f.camera,1);assert.equal(f.node('manualIdReview').value,'true');assert.match(f.node('idCheckMessage').textContent,/Manual review selected/);
 await f.input('idNumber');assert.equal(f.node('manualIdReview').value,'false');assert.equal(f.node('idCheckToken').value,'');assert(f.node('continueManualId').hidden);
 f=fixture({success:false,canManualReview:false,token:null,message:'Number mismatch.'});await f.click('openCamera');assert.equal(f.camera,0);assert(f.node('continueManualId').hidden);await f.click('continueManualId');assert.equal(f.camera,0);
 f=fixture({success:true,canManualReview:false,token:'signed-matched',message:'Matched.'});await f.click('openCamera');assert.equal(f.camera,1);assert.equal(f.node('manualIdReview').value,'false');
 console.log('PASS: manual route requires explicit choice, mismatch stays blocked, changed ID clears authorization, matched ID opens camera');
})().catch(e=>{console.error(e);process.exitCode=1;});
