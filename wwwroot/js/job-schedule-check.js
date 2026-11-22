(() => {
    // Advisory preview only: the POST handlers always recheck under database locks.
    for (const form of document.querySelectorAll('form[method="post"]')) {
        const url = new URL(form.action, location.href);
        const handler = url.searchParams.get('handler');
        const kind = url.pathname === '/BookingRequests' && handler === 'Decide' ? 'booking'
            : url.pathname === '/MarketTasks' && handler === 'Accept' ? 'offer' : null;
        if (!kind) continue;
        const id = form.querySelector('input[name="id"]')?.value;
        const accept = kind === 'booking' ? form.querySelector('button[value="accept"]') : form.querySelector('button');
        if (!id || !accept) continue;
        const notice = document.createElement('p');
        notice.className = 'flow-notice'; notice.setAttribute('role', 'status'); notice.hidden = true;
        form.before(notice);
        let blocked = false;
        async function check() {
            try {
                const response = await fetch(`/api/jobs/schedule-check?kind=${kind}&id=${encodeURIComponent(id)}`, { credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(10000) });
                if (!response.ok || response.redirected) return;
                const result = await response.json();
                blocked = !result.canAccept;
                notice.dataset.flowMessage = result.message || '';
                notice.textContent = window.ttsTranslate?.(result.message) || result.message;
                notice.hidden = !blocked;
                accept.dataset.scheduleBlocked = String(blocked);
                accept.disabled = blocked || form.closest('[data-work-lock-state]')?.dataset.workLockState === 'true';
            } catch { /* POST checks remain authoritative if preview is unavailable. */ }
        }
        form.addEventListener('submit', event => {
            if (blocked && (kind === 'offer' || event.submitter === accept)) { event.preventDefault(); notice.scrollIntoView({ block: 'center', behavior: 'smooth' }); }
        });
        window.addEventListener('site-language-changed', () => { notice.textContent = window.ttsTranslate?.(notice.dataset.flowMessage) || notice.dataset.flowMessage; });
        window.addEventListener('focus', check);
        setInterval(() => { if (!document.hidden) check(); }, 30000);
        check();
    }
})();
