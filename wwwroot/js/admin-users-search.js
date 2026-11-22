(() => {
    const input = document.getElementById('userSearch');
    if (!input) return;
    const cards = [...document.querySelectorAll('[data-user-search]')];
    const normalize = value => value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
    const users = cards.map(card => ({ card, text: normalize(card.dataset.userSearch) }));
    const status = document.getElementById('userSearchStatus');
    const empty = document.getElementById('noUserMatches');
    function filter() {
        const query = normalize(input.value.trim());
        let count = 0;
        users.forEach(({ card, text }) => {
            const match = !query || text.includes(query);
            card.hidden = !match;
            if (match) count++;
        });
        status.textContent = query ? `${count} of ${cards.length} users match your search.` : `${cards.length} users shown.`;
        empty.hidden = !query || count > 0;
    }
    input.addEventListener('input', filter);
    filter();
    const form = document.getElementById('usersTools');
    const button = form.querySelector('button');
    form.addEventListener('submit', event => {
        if (form.getAttribute('aria-busy') === 'true') { event.preventDefault(); return; }
        form.setAttribute('aria-busy', 'true');
        button.disabled = true;
        button.querySelector('span').textContent = 'Refreshing…';
    });
    window.addEventListener('pageshow', () => {
        form.removeAttribute('aria-busy');
        button.disabled = false;
        button.querySelector('span').textContent = 'Refresh activity';
        filter();
    });
})();
