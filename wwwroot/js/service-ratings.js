(() => {
 const normalize = text => text.normalize('NFKD').replace(/[\u0300-\u036f]/g,'').toLowerCase();
 const t = text => window.ttsTranslate?.(text) || text;
 document.querySelectorAll('[data-service-ratings]').forEach(panel=>{
  const results=panel.querySelector('[data-rating-results]');
  const cards=[...panel.querySelectorAll('[data-rating-card]')];
  const search=panel.querySelector('[data-rating-search]'),stars=panel.querySelector('[data-rating-stars]'),category=panel.querySelector('[data-rating-category]'),sort=panel.querySelector('[data-rating-sort]');
  function refresh(){
   const query=normalize(search.value.trim());let count=0;
   const order=[...cards].sort((a,b)=>{
    const diff=sort.value==='highest'?Number(b.dataset.rating)-Number(a.dataset.rating):sort.value==='lowest'?Number(a.dataset.rating)-Number(b.dataset.rating):0;
    if(diff)return diff;const first=BigInt(a.dataset.created),second=BigInt(b.dataset.created);if(first!==second)return first>second?-1:1;
    return BigInt(a.dataset.reviewId)>BigInt(b.dataset.reviewId)?-1:1;
   });
   order.forEach(card=>{card.hidden=!!(query&&!normalize(card.textContent).includes(query)||stars.value&&card.dataset.rating!==stars.value||category.value&&card.dataset.category!==category.value);if(!card.hidden)count++;results.append(card);});
   panel.querySelector('[data-rating-count]').textContent=t('Showing {count} of {total} ratings.').replace('{count}',count).replace('{total}',cards.length);
   panel.querySelector('[data-rating-no-matches]').hidden=count>0||cards.length===0;
  }
  search.addEventListener('input',refresh);[stars,category,sort].forEach(input=>input.addEventListener('change',refresh));
  panel.querySelector('[data-rating-reset]').addEventListener('click',()=>{search.value='';stars.value='';category.value='';sort.value='newest';refresh();search.focus();});
  window.addEventListener('site-language-changed',refresh);refresh();
 });
 document.addEventListener('keydown',event=>{if(event.key==='Escape')document.querySelectorAll('.work-more-actions[open]').forEach(menu=>{menu.open=false;menu.querySelector('summary').focus();});});
})();
