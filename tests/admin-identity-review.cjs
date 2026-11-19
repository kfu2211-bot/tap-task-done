const fs = require('node:fs'), vm = require('node:vm'), assert = require('node:assert/strict');
const source = fs.readFileSync('wwwroot/js/admin-identity-review.js', 'utf8');
function fixture(response) {
    const input = value => ({value,checked:false,disabled:false,addEventListener(){},setCustomValidity(v){this.validity=v;},setAttribute(){},removeAttribute(){},focus(){},reportValidity(){return !this.validity;}});
    const buttons = ['resubmit','rejected','approved'].map(value=>({name:'decision',value,disabled:value==='approved',textContent:value,classList:{add(){},remove(){}}}));
    const fields={note:input(''),numberOnDocument:input(''),typeOnDocument:input('')};
    for(const key of ['matchConfirmed','faceVisibleConfirmed','documentConfirmed','movementConfirmed'])fields[key]=input('');
    let submit,removed=false,calls=0,posted;
    const feedback={setAttribute(){},classList:{toggle(){}},focus(){},scrollIntoView(){}};
    const count={textContent:'1 requests'};
    const form={elements:{namedItem:key=>fields[key]},action:'https://example.test/AdminIdentity?handler=Review',
        querySelector:selector=>selector.includes('approved')?buttons[2]:{before(){}},querySelectorAll:()=>buttons,
        addEventListener:(name,fn)=>{if(name==='submit')submit=fn;},reportValidity:()=>true,setAttribute(){},removeAttribute(){},
        closest:()=>({replaceWith(){removed=true;}})};
    const document={querySelectorAll:selector=>selector==='.identity-review'?[form]:removed?[]:[{}],querySelector:()=>count,createElement:()=>feedback};
    class Data {constructor(){this.values={__RequestVerificationToken:'csrf-token',id:'123',note:fields.note.value};}set(k,v){this.values[k]=v;}}
    vm.runInNewContext(source,{document,window:{addEventListener(){}},FormData:Data,AbortController,setTimeout,clearTimeout,
        fetch:async(_,options)=>{calls++;posted=options.body.values;return response;}});
    return {fields,buttons,feedback,count,submit:(index=0)=>submit({submitter:buttons[index],preventDefault(){}}),get calls(){return calls;},get removed(){return removed;},get posted(){return posted;}};
}
(async()=>{
    let f=fixture();await f.submit();assert.equal(f.calls,0);assert.match(f.feedback.textContent,/review reason/);assert(!f.removed);assert(!f.buttons[0].disabled);
    f=fixture({ok:true,headers:{get:()=> 'application/json'},json:async()=>({success:true,message:'Resubmission requested.'})});
    f.fields.note.value='Upload a readable ID.';await f.submit();assert.equal(f.posted.decision,'resubmit');assert.equal(f.posted.__RequestVerificationToken,'csrf-token');assert(f.removed);assert.equal(f.count.textContent,'0 requests');
    f=fixture({ok:false,headers:{get:()=> 'application/json'},json:async()=>({success:false,message:'Request was already reviewed.'})});
    f.fields.note.value='Upload a readable ID.';await f.submit();assert(!f.removed);assert.match(f.feedback.textContent,/already reviewed/);assert.equal(f.fields.note.value,'Upload a readable ID.');assert(!f.buttons[0].disabled);assert.equal(f.buttons[0].textContent,'resubmit');
    f=fixture({ok:false,status:400,headers:{get:()=> 'text/html'}});f.fields.note.value='Retry documents.';await f.submit();assert(!f.removed);assert.match(f.feedback.textContent,/session expired/);assert(!f.buttons[0].disabled);
    console.log('PASS: empty reason blocked inline; confirmed resubmission removes pending card; server/session errors retain form and reset saving state; CSRF and decision preserved');
})().catch(error=>{console.error(error);process.exitCode=1;});
