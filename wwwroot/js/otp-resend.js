(() => {
    for (const form of document.querySelectorAll('[data-resend-seconds]')) {
        const button = form.querySelector('button[type="submit"]');
        const label = form.querySelector('[data-resend-label]');
        if (!button || !label) continue;
        const end = Date.now() + Math.max(0, Number(form.dataset.resendSeconds) || 0) * 1000;
        function tick() {
            if (form.dataset.submitting === 'true') return;
            const seconds = Math.max(0, Math.ceil((end - Date.now()) / 1000));
            button.disabled = seconds > 0;
            const prefix = window.ttsTranslate?.('Available in') || 'Available in';
            label.textContent = seconds ? `${prefix} ${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`
                : window.ttsTranslate?.('You can request a new code.') || 'You can request a new code.';
        }
        form.addEventListener('submit', event => {
            if (Date.now() < end) event.preventDefault();
            else if (!event.defaultPrevented) form.dataset.submitting = 'true';
        });
        window.addEventListener('site-language-changed', tick);
        tick(); setInterval(tick, 1000);
    }
    window.addEventListener('pageshow', event => { if (event.persisted) location.reload(); });
})();
