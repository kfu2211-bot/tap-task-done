(() => {
    const badges = document.querySelectorAll('[data-provider-status]');
    if (!badges.length) return;
    let pending = false;
    async function refresh() {
        if (document.hidden || pending) return;
        pending = true;
        try {
            const response = await fetch('/api/providers/availability', { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) return;
            const statuses = new Map((await response.json()).map(p => [String(p.id), p.availability]));
            badges.forEach(b => { if (statuses.has(b.dataset.providerStatus)) b.textContent = statuses.get(b.dataset.providerStatus); });
        } catch { /* Keep the last successfully loaded status. */ }
        finally { pending = false; }
    }
    setInterval(refresh, 15000);
    window.addEventListener('focus', refresh);
})();
