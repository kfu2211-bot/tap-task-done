(() => {
    let pending = false, timer;
    function badge(name, warning) {
        if (!name) return;
        const existing = name.querySelector(':scope > .account-warning-symbol');
        if (!warning) { existing?.remove(); return; }
        if (existing) return;
        const span = document.createElement('span'); span.className = 'account-warning-symbol';
        span.title = 'Account warning'; span.setAttribute('role', 'img'); span.setAttribute('aria-label', 'Account warning');
        span.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 2 21h20Z" fill="#facc15" stroke="#c7770b" stroke-width="1.5"/><path d="M12 9v5" stroke="#7c3d10" stroke-width="2" stroke-linecap="round"/><circle cx="12" cy="17.5" r="1" fill="#7c3d10"/></svg>';
        name.append(span);
    }
    function names() {
        document.querySelectorAll('.freelancer-card[data-id]').forEach(c => { const n = c.querySelector('.freelancer-info strong'); if (n) n.dataset.accountId = c.dataset.id; });
        document.querySelectorAll('[data-provider-status]').forEach(c => { const n = c.parentElement.querySelector('strong'); if (n) n.dataset.accountId = c.dataset.providerStatus; });
        return [...document.querySelectorAll('[data-account-id]')];
    }
    async function refresh() {
        if (pending || document.hidden) return;
        pending = true;
        try {
            const ownResponse = await fetch('/api/account-warning', { credentials:'same-origin', cache:'no-store' });
            if (ownResponse.ok && !ownResponse.redirected) {
                const own = await ownResponse.json();
                document.querySelectorAll('.header-account-name, #sidebarName, [data-own-account-name]').forEach(n => badge(n, own.warning));
                if (/\/(settings|dashboard|notifications)\/?$/i.test(location.pathname)) {
                    let notice = document.getElementById('accountRecoveryNotice');
                    if (own.warning) {
                        if (!notice) { notice = document.createElement('section'); notice.id = 'accountRecoveryNotice'; notice.className = 'account-recovery-notice'; notice.setAttribute('role','status'); const main = document.querySelector('main'); (main?.querySelector('header, .topbar') || main)?.insertAdjacentElement('afterend',notice); }
                        if (notice) {
                            const text = `Account warning · Recovery progress: ${own.completed} of ${own.target} completed tasks. Only client-confirmed completions after the latest penalty count. Offense history remains after the symbol clears.`;
                            if (notice.dataset.copy !== text) { notice.replaceChildren(document.createTextNode(text)); notice.dataset.copy=text; }
                        }
                    } else notice?.remove();
                }
            }
            const elements = names(), ids = [...new Set(elements.map(n => n.dataset.accountId).filter(id => /^\d+$/.test(id)))];
            for (let i=0;i<ids.length;i+=20) {
                const response = await fetch('/api/profile-warnings?ids='+ids.slice(i,i+20).join(','), { credentials:'same-origin', cache:'no-store' });
                if (!response.ok || response.redirected) continue;
                for (const status of await response.json()) elements.filter(n => n.dataset.accountId===String(status.id)).forEach(n => badge(n,status.warning));
            }
        } catch { /* Preserve the most recently loaded status during connection failures. */ }
        finally { pending = false; }
    }
    function schedule() { clearTimeout(timer);timer=setTimeout(refresh,250); }
    const observer=new MutationObserver(records=>{
        if (records.some(r=>r.type==='attributes' && r.oldValue !== r.target.getAttribute('data-account-id') || [...r.addedNodes].some(n=>n.nodeType===1 && (n.matches('[data-account-id], .freelancer-card, .header-account-name') || n.querySelector('[data-account-id]'))))) schedule();
    });
    observer.observe(document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['data-account-id'],attributeOldValue:true});
    window.addEventListener('account-header-ready',schedule);window.addEventListener('focus',schedule);
    setInterval(refresh,30000);refresh();
})();
