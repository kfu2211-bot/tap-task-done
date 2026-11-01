(() => {
    const panel=document.querySelector('[data-agreement-task]');if(!panel)return;
    let pending=false;
    async function refresh(){
        if(document.hidden||pending)return;pending=true;
        try{
            const response=await fetch(location.href,{credentials:'same-origin',cache:'no-store'});if(!response.ok||response.redirected)return;
            const page=new DOMParser().parseFromString(await response.text(),'text/html');
            const next=page.querySelector('[data-agreement-task]');if(!next||next.dataset.agreementTask!==panel.dataset.agreementTask)return;
            if(next.dataset.agreementVersion!==panel.dataset.agreementVersion){
                if(!panel.querySelector('[data-stale-agreement]')){const note=document.createElement('p');note.dataset.staleAgreement='true';note.setAttribute('role','status');note.append(document.createTextNode('New agreement terms are available. '));const link=document.createElement('a');link.href=location.href;link.textContent='Reload and review the latest version';note.append(link);panel.prepend(note);}
                panel.querySelectorAll('button[type=submit]').forEach(b=>b.disabled=true);
                document.querySelector('.tracking-progress-form button[type=submit]')?.setAttribute('disabled','');return;
            }
            const current=panel.querySelector('[data-agreement-summary]'),updated=next.querySelector('[data-agreement-summary]');
            // Compare text, not antiforgery token values, to avoid needless DOM replacement.
            if(current&&updated&&current.textContent!==updated.textContent)current.replaceChildren(...[...updated.childNodes].map(n=>document.importNode(n,true)));
            const action=document.querySelector('.tracking-progress-form input[name=action]'),newAction=page.querySelector('.tracking-progress-form input[name=action]');
            if(action&&newAction&&action.value===newAction.value){const button=action.closest('form').querySelector('button[type=submit]'),newButton=newAction.closest('form').querySelector('button[type=submit]');if(button&&newButton)button.disabled=newButton.disabled;}
        }catch{/* Keep unsaved proposals and current map during connection errors. */}finally{pending=false;}
    }
    setInterval(refresh,10000);window.addEventListener('focus',refresh);
})();
