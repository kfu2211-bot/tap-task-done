document.querySelectorAll('.session-history').forEach(panel => {
    const feedback = panel.querySelector('.session-feedback');
    const entries = panel.querySelector('.session-entries');
    const more = panel.querySelector('.session-more');
    let page = 1;
    let loaded = false;
    let busy = false;
    async function load() {
        if (busy) return;
        busy = true;
        more.disabled = true;
        panel.setAttribute('aria-busy', 'true');
        feedback.textContent = 'Loading sessions…';
        try {
            const url = new URL('/AdminUsers', window.location.origin);
            url.search = new URLSearchParams({ handler: 'Sessions', userId: panel.dataset.userId, pageNumber: page });
            const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Sessions unavailable');
            const data = await response.json();
            data.rows.forEach(row => {
                const item = document.createElement('section');
                item.className = 'session-entry';
                [['Time In', row.timeIn], ['Last Active', row.lastActive], ['Time Out', row.timeOut], ['Status', row.status]].forEach(([label, value]) => {
                    const field = document.createElement('div');
                    const title = document.createElement('strong');
                    title.textContent = label;
                    const text = document.createElement('span');
                    text.textContent = value;
                    field.append(title, text);
                    item.append(field);
                });
                entries.append(item);
            });
            loaded = true;
            page++;
            feedback.textContent = entries.childElementCount ? 'Philippine time (UTC+8). ~ indicates an estimated time out.' : 'No recorded sessions yet. Tracking starts with the next login.';
            more.textContent = 'Load more sessions';
            more.hidden = !data.hasNext;
        } catch {
            feedback.textContent = 'Unable to load sessions. Please try again.';
            more.textContent = 'Retry loading sessions';
            more.hidden = false;
        } finally {
            busy = false;
            more.disabled = false;
            panel.removeAttribute('aria-busy');
        }
    }
    panel.addEventListener('toggle', () => { if (panel.open && !loaded) load(); });
    more.addEventListener('click', load);
});
