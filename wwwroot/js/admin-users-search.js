(() => {
    const input = document.getElementById('userSearch');
    if (!input) return;
    const cards = [...document.querySelectorAll('[data-user-search]')];
    const normalize = value => value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
    const users = cards.map(card => ({ card, text: normalize(card.dataset.userSearch) }));
    const status = document.getElementById('userSearchStatus');
    const empty = document.getElementById('noUserMatches');
    const presence = document.getElementById('userPresence');
    const clear = document.getElementById('clearUserFilters');
    const t = text => window.ttsTranslate?.(text) || text;
    function filter() {
        const query = normalize(input.value.trim());
        const activity = presence?.value || 'all';
        let count = 0;
        users.forEach(({ card, text }) => {
            const match = (!query || text.includes(query)) && (activity === 'all' || card.dataset.userPresence === activity);
            card.hidden = !match;
            if (match) count++;
        });
        status.textContent = t('Showing {count} of {total} accounts.').replace('{count}', count).replace('{total}', cards.length);
        empty.hidden = count > 0 || cards.length === 0;
        if (clear) clear.disabled = !query && activity === 'all';
    }
    input.addEventListener('input', filter);
    presence?.addEventListener('change', filter);
    clear?.addEventListener('click', () => { input.value = ''; if(presence) presence.value = 'all'; filter(); input.focus(); });
    window.addEventListener('admin-user-activity-updated', filter);
    window.addEventListener('site-language-changed', filter);
    filter();
    const form = document.getElementById('usersTools');
    const button = form?.querySelector('button[type="submit"]');
    if (!form || !button) return;
    form.addEventListener('submit', event => {
        if (form.getAttribute('aria-busy') === 'true') { event.preventDefault(); return; }
        form.setAttribute('aria-busy', 'true');
        button.disabled = true;
        button.querySelector('span').textContent = t('Refreshing…');
    });
    window.addEventListener('pageshow', () => {
        form.removeAttribute('aria-busy');
        button.disabled = false;
        button.querySelector('span').textContent = t('Refresh activity');
        filter();
    });
})();
