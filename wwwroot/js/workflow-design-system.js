(() => {
 const targets = '.job-status[data-status],.task-status-badge[data-status],.offer-status[data-status],.db-tracking-status[data-status],.work-status[data-status],.work-status[data-ready],[data-scheduled-status],[data-scheduled-confirmation]';
 function tone(value) {
  switch ((value || '').trim().toLowerCase()) {
   case 'completed': case 'both confirmed': return 'completed';
   case 'cancelled': case 'canceled': return 'cancelled';
   case 'expired': case 'withdrawn': case 'declined': case 'rejected': case 'moderated': case 'removed by admin': return 'closed';
   case 'in-progress': case 'work in progress': case 'on-the-way': case 'on the way': case 'arrived': return 'active';
   case 'pending': case 'pending-confirmation': case 'booking-pending': case 'awaiting-confirmation': case 'awaiting client confirmation': case 'paused': case 'confirmation pending': case 'needs confirmation': case 'due now': case 'schedule overdue': case 'disputed': return 'pending';
   case 'open': return 'open';
   default: return 'scheduled';
  }
 }
 function refresh() {
  document.querySelectorAll(targets).forEach(node=>{
   const state = node.hasAttribute('data-ready') ? node.dataset.ready==='true'?'completed':'pending' : tone(node.dataset.status);
   if (!node.classList.contains('workflow-status')) node.classList.add('workflow-status');
   if (node.dataset.workState !== state) node.dataset.workState = state;
  });
 }
 let queued=false;new MutationObserver(()=>{if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;refresh();});}).observe(document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['data-status','data-ready']});
 document.addEventListener('click',event=>{
  const reset=event.target.closest('[data-reset-task-filters]');
  if(reset){const section=document.getElementById('my-tasks-section');const type=section?.querySelector('#myTasksTypeFilter'),closed=section?.querySelector('#clientClosedFilter');if(type)type.value='';if(closed)closed.value='all';section?.querySelector('[data-filter="all"]')?.click();}
  if(event.target.closest('[data-reset-job-filters]'))document.getElementById('jobsFilterReset')?.click();
  const link=event.target.closest('[data-open-offer-editor]');if(link){const editor=document.getElementById(link.getAttribute('href').slice(1));if(editor){event.preventDefault();editor.open=true;editor.querySelector('input:not([type=hidden])')?.focus();}}
 });
 refresh();
})();
