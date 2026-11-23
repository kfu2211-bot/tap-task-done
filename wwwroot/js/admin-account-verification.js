(() => {
 const dialog=document.getElementById('adminVerificationDialog'); if(!dialog)return;
 let pending=null;
 const t=text=>window.ttsTranslate?.(text)||text;
 function impact(form) {
  const type=form.elements.type.value,change=form.elements.change.value;
  if(!change)return 'Choose an action to review its effect.';
  if(type==='email')return change==='verify'?'This manually verifies the current email address. Sign-in OTP remains enabled.':'Email verification will be required again. Active sign-in sessions and trusted devices will be revoked. Existing jobs stay intact.';
  return change==='verify'?'This manually approves identity without claiming document checks. New work still follows all other account restrictions.':'Identity approval will be revoked. New posts, bookings and offers require approval again. Existing jobs stay intact.';
 }
 document.querySelectorAll('[data-verification-form]').forEach(form=>{
  const button=form.querySelector('button[type=submit]');
  function update(){form.querySelector('[data-verification-impact]').textContent=t(impact(form));form.elements.confirmChange.value='false';}
  form.elements.type.addEventListener('change',update);form.elements.change.addEventListener('change',update);update();
  form.addEventListener('submit',event=>{
   if(form.getAttribute('aria-busy')==='true'){event.preventDefault();return;}
   if(form.dataset.reviewed==='true') {delete form.dataset.reviewed;form.setAttribute('aria-busy','true');button.disabled=true;return;}
   event.preventDefault();if(!form.reportValidity())return;
   pending=form;dialog.querySelector('[data-verification-account]').textContent=form.dataset.accountName;
   dialog.querySelector('[data-verification-type]').textContent=t(form.elements.type.value==='email'?'Email':'Identity');
   const current = form.elements.type.value==='email' ? form.dataset.emailVerified==='true'?'Verified':'Not verified' : form.dataset.identityStatus==='approved'?'Verified':form.dataset.identityStatus==='pending'?'Pending review':form.dataset.identityStatus==='resubmit'?'Needs re-verification':'Not verified';
   dialog.querySelector('[data-verification-change]').textContent=t(current)+' → '+t(form.elements.change.value==='verify'?'Verified':'Needs re-verification');
   dialog.querySelector('[data-verification-reason]').textContent=form.elements.reason.value.trim();
   dialog.querySelector('#adminVerificationImpact').textContent=t(impact(form));
   dialog.returnValue='';
   if(window.frameElement){try{dialog.style.top=Math.max(16,window.parent.innerHeight/2-window.frameElement.getBoundingClientRect().top)+'px';dialog.classList.add('is-embedded');dialog.style.maxHeight=Math.max(240,window.parent.innerHeight-32)+'px';}catch{}}
   dialog.showModal();
  });
  window.addEventListener('pageshow',()=>{form.removeAttribute('aria-busy');form.elements.confirmChange.value='false';delete form.dataset.reviewed;button.disabled=false;});
 });
 dialog.addEventListener('close',()=>{
  const form=pending;pending=null;if(!form)return;
  if(dialog.returnValue!=='confirm'){form.querySelector('button[type=submit]').focus();return;}
  form.elements.confirmChange.value='true';form.dataset.reviewed='true';form.requestSubmit(form.querySelector('button[type=submit]'));
 });
 window.addEventListener('site-language-changed',()=>document.querySelectorAll('[data-verification-form]').forEach(form=>form.querySelector('[data-verification-impact]').textContent=t(impact(form))));
})();
