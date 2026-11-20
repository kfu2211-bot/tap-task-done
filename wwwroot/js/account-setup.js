(() => {
    const root = document.querySelector('[data-account-setup]');
    if (!root) return;
    const dialog = root.querySelector('dialog');
    const openButton = root.querySelector('[data-setup-open]');
    if (!dialog || !openButton) return;
    if (typeof dialog.showModal !== 'function') {
        openButton.hidden = false;
        openButton.addEventListener('click', () => {
            const destination = dialog.querySelector('a');
            if (destination) window.location.assign(destination.href);
        });
        return;
    }
    const key = root.dataset.reminderKey;
    let previousFocus;
    const remember = () => {
        try { localStorage.setItem(key, 'shown'); }
        catch { try { sessionStorage.setItem(key, 'shown'); } catch { /* Keep the popup usable with storage blocked. */ } }
    };
    const open = () => {
        if (dialog.open) return;
        previousFocus = document.activeElement;
        dialog.showModal();
        root.querySelector('#setupTitle')?.focus({ preventScroll: true });
        remember();
    };
    openButton.hidden = false;
    openButton.addEventListener('click', open);
    root.querySelectorAll('[data-setup-close]').forEach(button => button.addEventListener('click', () => dialog.close()));
    dialog.addEventListener('close', () => {
        const target = previousFocus && previousFocus !== document.body && previousFocus.isConnected ? previousFocus : openButton;
        target.focus({ preventScroll: true });
    });
    window.addEventListener('pageshow', event => {
        // Back from Settings may restore a stale dashboard from the browser cache.
        if (event.persisted) window.location.reload();
    });
    let shown = false;
    try { shown = localStorage.getItem(key) === 'shown'; }
    catch { try { shown = sessionStorage.getItem(key) === 'shown'; } catch { } }
    if (root.dataset.autoOpen === 'true' && !shown) open();
})();
