(() => {
    if (window.top !== window.self) return;
    let pending = false;
    async function heartbeat() {
        if (document.hidden || pending) return;
        pending = true;
        try {
            await fetch('/api/user-activity/heartbeat', { method: 'POST', credentials: 'same-origin', headers: { 'X-TTS-Activity': '1' } });
        } catch { /* A lost connection naturally ages the session to inactive. */ }
        finally { pending = false; }
    }
    heartbeat();
    setInterval(heartbeat, 60000);
    document.addEventListener('visibilitychange', heartbeat);
})();
