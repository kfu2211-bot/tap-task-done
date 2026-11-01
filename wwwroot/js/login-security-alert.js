(() => {
    if (document.querySelector('[data-signin-alert]')) return;
    let busy = false, lastId = null;
    async function check() {
        if (busy || document.hidden) return;
        busy = true;
        try {
            const response = await fetch('/api/security/status', { credentials: 'same-origin', cache: 'no-store' });
            if (response.redirected || response.status === 401) { location.assign('/Login?sessionEnded=1'); return; }
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) return;
            const { alert } = await response.json();
            if (!alert) { document.querySelector('[data-signin-alert]')?.remove(); lastId = null; return; }
            if (lastId === alert.id) return;
            document.querySelector('[data-signin-alert]')?.remove();
            lastId = alert.id;
            const banner = document.createElement('div');
            banner.dataset.signinAlert = 'true'; banner.setAttribute('role', 'alert');
            Object.assign(banner.style, { position:'fixed', bottom:'20px', right:'20px', zIndex:'10000', maxWidth:'min(420px,calc(100vw - 32px))', background:'#fff8e9', color:'#163d30', border:'1px solid #d7b96c', borderRadius:'12px', padding:'16px', boxShadow:'0 6px 24px #163d3020', font:'14px/1.6 Arial,sans-serif' });
            const title = document.createElement('strong'); title.textContent = 'New sign-in on your account';
            const detail = document.createElement('p'); detail.textContent = alert.device + ' · ' + new Date(alert.createdUtc.endsWith('Z') ? alert.createdUtc : alert.createdUtc + 'Z').toLocaleString('en-PH', { timeZone:'Asia/Manila' }) + ' PHT';
            const link = document.createElement('a'); link.href = '/Security'; link.textContent = 'Review sign-in / Secure my account'; link.style.color='#176b50';
            banner.append(title, detail, link); document.body.append(banner);
        } catch { /* A temporary outage must not interrupt unsaved work. */ }
        finally { busy = false; }
    }
    check(); setInterval(check, 15000);
    window.addEventListener('focus', check);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) check(); });
})();
