(() => {
    document.addEventListener('click', event => {
        const link = event.target.closest('a[href^="#work-details-"]');
        if (!link) return;
        const section = document.getElementById(link.getAttribute('href').slice(1));
        if (link.hasAttribute('data-review-details') && section) {
            event.preventDefault();
            section.scrollIntoView({block:'start',behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'auto':'smooth'});
            section.setAttribute('tabindex','-1'); section.focus({preventScroll:true}); return;
        }
        const editor = section?.querySelector('[data-agreement-editor]');
        if (!editor) return;
        event.preventDefault(); editor.open = true;
        editor.scrollIntoView({block:'start',behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'auto':'smooth'});
        editor.querySelector('input:not([type="hidden"])')?.focus({preventScroll:true});
    });
})();
