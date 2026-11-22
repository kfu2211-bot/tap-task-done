// Keep presence current without resetting search, forms or expanded panels.
(() => {
    let busy = false;
    async function update() {
        if (document.hidden || busy || (window.frameElement && !window.frameElement.getClientRects().length)) return;
        busy = true;
        try {
            const response = await fetch('/AdminUsers?handler=Activity', { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Activity unavailable');
            const rows = new Map((await response.json()).map(row => [String(row.userId), row]));
            document.querySelectorAll('.user-card[data-user-id]').forEach(card => {
                const row = rows.get(card.dataset.userId);
                const badge = card.querySelector('[data-presence]');
                badge.textContent = row ? row.online ? 'Online' : 'Offline' : 'Not yet detected';
                badge.classList.toggle('is-online', !!row?.online);
                card.querySelector('[data-time-in]').textContent = row?.timeIn ?? '—';
                card.querySelector('[data-last-active]').textContent = row?.lastActive ?? '—';
                card.querySelector('[data-time-out]').textContent = row?.timeOut ?? '—';
            });
        } catch {
            document.querySelectorAll('[data-presence]').forEach(badge => {
                badge.textContent = 'Activity unavailable';
                badge.classList.remove('is-online');
            });
        } finally { busy = false; }
    }
    setInterval(update, 30000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) update(); });
})();

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
