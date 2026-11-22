(() => {
    const roots = [...document.querySelectorAll('[data-work-lock-state]')];
    if (!roots.length) return;
    let pending = false;
    for (const root of roots) {
        if (root.dataset.workLockState === 'true') root.querySelectorAll('[data-work-guard]:disabled').forEach(control => control.dataset.workLockDisabled = 'true');
    }
    async function refresh() {
        if (pending || document.hidden) return;
        pending = true;
        try {
            const response = await fetch('/api/work-eligibility', {cache:'no-store',credentials:'same-origin',signal:AbortSignal.timeout(4000)});
            if (!response.ok || response.redirected) return;
            const result = await response.json();
            if (typeof result.locked !== 'boolean') return;
            for (const root of roots) {
                if (root.dataset.workLockState === String(result.locked)) continue;
                root.dataset.workLockState = String(result.locked);
                root.querySelectorAll('[data-work-guard]').forEach(control => {
                    if (result.locked && !control.disabled) { control.dataset.workLockDisabled = 'true'; control.disabled = true; }
                    else if (!result.locked && control.dataset.workLockDisabled === 'true') { control.disabled = control.dataset.scheduleBlocked === 'true'; delete control.dataset.workLockDisabled; }
                });
                root.querySelectorAll('[data-work-lock-notice]').forEach(notice => notice.hidden = !result.locked);
            }
        } catch { /* Server checks still enforce restrictions during network failures. */ }
        finally { pending = false; }
    }
    setInterval(refresh,5000);
    window.addEventListener('focus',refresh);
    document.addEventListener('visibilitychange',refresh);
})();
