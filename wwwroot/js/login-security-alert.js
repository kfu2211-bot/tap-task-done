(() => {
    if (document.querySelector('[data-signin-alert]')) return;
    let busy = false, lastId = null, dismissTimer;
    const dismissed = new Set();
    try { JSON.parse(sessionStorage.getItem('tts.dismissedSignIns') || '[]').forEach(id => dismissed.add(String(id))); } catch { /* Storage may be unavailable. */ }
    const remember = id => {
        dismissed.add(String(id));
        try { sessionStorage.setItem('tts.dismissedSignIns', JSON.stringify([...dismissed].slice(-100))); } catch { /* In-memory dismissal still works. */ }
    };
    async function check() {
        if (busy || document.hidden) return;
        busy = true;
        try {
            const response = await fetch('/api/security/status', { credentials: 'same-origin', cache: 'no-store' });
            if (response.status === 401) {
                if (!document.querySelector('[data-session-ended]')) {
                    const notice = document.createElement('p');
                    notice.dataset.sessionEnded = 'true';
                    notice.setAttribute('role', 'status');
                    Object.assign(notice.style, {
                        position: 'fixed', bottom: '20px', right: '20px', zIndex: '10000',
                        maxWidth: 'calc(100vw - 40px)', margin: '0', padding: '14px',
                        borderRadius: '10px', background: 'var(--signin-alert-surface, #fff)',
                        color: 'var(--signin-alert-text, #163d30)', boxShadow: '0 4px 20px #163d3033'
                    });
                    const link = document.createElement('a');
                    link.href = '/Login?sessionEnded=1';
                    link.textContent = 'Log in again';
                    link.style.color = 'inherit';
                    notice.append('Your session ended. ', link);
                    document.body.append(notice);
                }
                return;
            }
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) return;
            document.querySelector('[data-session-ended]')?.remove();
            const { alert } = await response.json();
            if (!alert) { document.querySelector('[data-signin-alert]')?.remove(); lastId = null; return; }
            if (lastId === alert.id || dismissed.has(String(alert.id))) return;
            document.querySelector('[data-signin-alert]')?.remove();
            lastId = alert.id;
            clearTimeout(dismissTimer);
            const banner = document.createElement('div');
            banner.dataset.signinAlert = 'true'; banner.setAttribute('role', 'alert');
            Object.assign(banner.style, { position:'fixed', bottom:'20px', right:'20px', zIndex:'10000', width:'360px', maxWidth:'calc(100vw - 32px)', background:'var(--signin-alert-surface, #fff)', color:'var(--signin-alert-text, #163d30)', border:'1px solid var(--signin-alert-border, #cfe0d3)', borderRadius:'14px', padding:'18px', boxShadow:'0 10px 36px #163d3026', font:'13px/1.6 Arial,sans-serif' });
            const dismiss = () => { clearTimeout(dismissTimer); remember(alert.id); banner.remove(); };
            const header = document.createElement('div'); Object.assign(header.style, {display:'flex',alignItems:'center',justifyContent:'space-between',gap:'16px'});
            const title = document.createElement('strong'); title.textContent = 'New sign-in detected'; title.style.fontSize='14px';
            const close = document.createElement('button'); close.type='button'; close.textContent='×'; close.setAttribute('aria-label','Dismiss sign-in notification');
            Object.assign(close.style,{border:'0',background:'var(--signin-alert-control, #edf5ef)',color:'var(--signin-alert-accent, #18765b)',borderRadius:'6px',width:'28px',height:'28px',padding:'0',font:'22px Arial,sans-serif',cursor:'pointer'});
            close.addEventListener('click', dismiss); header.append(title,close);
            const detail = document.createElement('p'); detail.textContent = alert.device; Object.assign(detail.style,{margin:'10px 0 2px',color:'var(--signin-alert-muted, #536d5e)'});
            const time = document.createElement('small'); time.textContent = new Date(alert.createdUtc.endsWith('Z') ? alert.createdUtc : alert.createdUtc + 'Z').toLocaleString('en-PH', { timeZone:'Asia/Manila' }) + ' PHT'; time.style.color='var(--signin-alert-muted, #718578)';
            const link = document.createElement('a'); link.href = '/Security'; link.textContent = 'Review account security';
            Object.assign(link.style,{display:'block',marginTop:'12px',color:'var(--signin-alert-accent, #18765b)',fontWeight:'600',textDecoration:'none'});
            banner.append(header, detail, time, link); banner.addEventListener('click',dismiss);
            document.body.append(banner); dismissTimer = setTimeout(dismiss,10000);
        } catch { /* A temporary outage must not interrupt unsaved work. */ }
        finally { busy = false; }
    }
    check(); setInterval(check, 15000);
    window.addEventListener('focus', check);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) check(); });
})();
