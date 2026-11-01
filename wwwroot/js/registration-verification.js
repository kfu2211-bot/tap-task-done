(() => {
    document.querySelectorAll('[data-resend-seconds]').forEach(form => {
        const button = form.querySelector('button[type=submit]');
        const label = form.querySelector('[data-resend-label]');
        const end = Date.now() + Math.max(0, Number(form.dataset.resendSeconds) || 0) * 1000;
        const tick = () => {
            const seconds = Math.max(0, Math.ceil((end - Date.now()) / 1000));
            button.disabled = seconds > 0;
            label.textContent = seconds ? `Resend available in ${seconds}s` : "Didn't receive a code? Request a new one.";
            return seconds;
        };
        if (tick()) { const timer = setInterval(() => { if (!tick()) clearInterval(timer); }, 1000); }
    });
})();
