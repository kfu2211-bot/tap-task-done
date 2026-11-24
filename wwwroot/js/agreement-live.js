(() => {
    const panel=document.querySelector('[data-agreement-task]');if(!panel&&!document.querySelector('[data-flow-slot]'))return;
    let pending=false;
    async function refresh(){
        if(document.hidden||pending)return;pending=true;
        try{
            const response=await fetch(location.href,{credentials:'same-origin',cache:'no-store'});if(!response.ok||response.redirected)return;
            const page=new DOMParser().parseFromString(await response.text(),'text/html');
            // Refresh recovery state without discarding drafts, uploads or the current map.
            document.querySelectorAll('[data-flow-slot]').forEach(slot=>{
                const updated=page.querySelector('[data-flow-slot="'+slot.dataset.flowSlot+'"]');
                if(!updated||slot.dataset.flowKey===updated.dataset.flowKey||slot.querySelector('[aria-busy="true"]'))return;
                const dirty=[...slot.querySelectorAll('input:not([type=hidden]),textarea,select')].some(input=>input.type==='file'?input.files.length:input.type==='checkbox'?input.checked!==input.defaultChecked:input.value!==input.defaultValue);
                if(dirty)return;
                const opened=[...slot.querySelectorAll('details')].map(e=>e.open);
                const replacement=document.importNode(updated,true);
                replacement.querySelectorAll('details').forEach((e,i)=>{if(opened[i])e.open=true;});
                slot.replaceWith(replacement);
            });
            if(!panel)return;
            const next=page.querySelector('[data-agreement-task]');if(!next||next.dataset.agreementTask!==panel.dataset.agreementTask)return;
            if(next.dataset.agreementVersion!==panel.dataset.agreementVersion){
                if(!panel.querySelector('[data-stale-agreement]')){const note=document.createElement('p');note.dataset.staleAgreement='true';note.setAttribute('role','status');note.append(document.createTextNode('New agreement terms are available. '));const link=document.createElement('a');link.href=location.href;link.textContent='Reload and review the latest version';note.append(link);panel.prepend(note);}
                panel.querySelectorAll('button[type=submit]').forEach(b=>b.disabled=true);
                document.querySelector('.tracking-progress-form button[type=submit]')?.setAttribute('disabled','');return;
            }
            const current=panel.querySelector('[data-agreement-summary]'),updated=next.querySelector('[data-agreement-summary]');
            // Compare text, not antiforgery token values, to avoid needless DOM replacement.
            if(current&&updated&&current.textContent!==updated.textContent)current.replaceChildren(...[...updated.childNodes].map(n=>document.importNode(n,true)));
            const step=document.querySelector('[data-tracking-next-step]'),newStep=page.querySelector('[data-tracking-next-step]');
            if(step&&newStep&&step.dataset.taskState===newStep.dataset.taskState) {
                const actions=document.querySelector('.tracking-current-actions'),newActions=page.querySelector('.tracking-current-actions');
                // Refresh only pre-travel controls; never replace an active upload or the map.
                if(step.dataset.blocked!==newStep.dataset.blocked || step.dataset.ready!==newStep.dataset.ready || actions?.textContent!==newActions?.textContent) {
                    step.replaceWith(document.importNode(newStep,true));
                    if(actions && newActions) {
                        const hasDraft=[...actions.querySelectorAll('textarea,input[type=file]')].some(e=>e.type==='file'?e.files.length:e.value!==e.defaultValue);
                        // Active arrival/upload controls have their own listeners. Preserve their nodes.
                        if(newStep.dataset.taskState!=='assigned') {
                            const blocked=newStep.dataset.blocked==='true';
                            actions.classList.toggle('job-flow-actions-paused',blocked);
                            actions.querySelectorAll('form button[type=submit]').forEach(button=>{
                                if(blocked){if(!button.hasAttribute('data-flow-disabled'))button.dataset.flowDisabled=String(button.disabled);button.disabled=true;}
                                else if(button.hasAttribute('data-flow-disabled')){button.disabled=button.dataset.flowDisabled==='true';delete button.dataset.flowDisabled;}
                            });
                        }
                        else if(!hasDraft)actions.replaceWith(document.importNode(newActions,true));
                        else if(newStep.dataset.blocked==='true')actions.querySelectorAll('button[type=submit]').forEach(b=>b.disabled=true);
                    }
                }
            }
            const action=document.querySelector('.tracking-progress-form input[name=action]'),newAction=page.querySelector('.tracking-progress-form input[name=action]');
            if(action&&newAction&&action.value===newAction.value){const button=action.closest('form').querySelector('button[type=submit]'),newButton=newAction.closest('form').querySelector('button[type=submit]');if(button&&newButton)button.disabled=newButton.disabled;}
        }catch{/* Keep unsaved proposals and current map during connection errors. */}finally{pending=false;}
    }
    setInterval(refresh,5000);window.addEventListener('focus',refresh);
})();
