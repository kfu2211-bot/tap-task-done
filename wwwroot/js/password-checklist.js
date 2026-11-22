(() => {
    document.querySelectorAll('[data-password-rules]').forEach(root => {
        const input = root.querySelector('input');
        const panel = root.querySelector('[data-password-checklist-panel]');
        const form = input.form;
        const confirm = form.querySelector('[name="ConfirmPassword"]');
        const feedback = form.querySelector('[data-password-match]');
        input.setAttribute('aria-describedby', panel.id);
        function position() {
            panel.classList.toggle('is-floating', innerWidth - root.getBoundingClientRect().right >= 280);
        }
        function update() {
            const value = input.value;
            const rules = {length:value.length >= 8 && value.length <= 256,upper:/[A-Z]/.test(value),lower:/[a-z]/.test(value),number:/[0-9]/.test(value),special:/[\x21-\x2F\x3A-\x40\x5B-\x60\x7B-\x7E]/.test(value)};
            let count = 0;
            panel.querySelectorAll('[data-password-rule]').forEach(item => {
                const passed = rules[item.dataset.passwordRule];
                item.classList.toggle('passed', passed);
                item.querySelector('span').textContent = passed ? '✓' : '○';
                if (passed) count++;
            });
            panel.querySelector('[data-password-rule-summary]').textContent = value ? `${count} of 5 requirements met` : 'Enter a strong password';
            if (confirm) {
                const mismatch = Boolean(confirm.value && confirm.value !== value);
                confirm.setCustomValidity(mismatch ? (window.ttsTranslate?.('The passwords do not match.') || 'The passwords do not match.') : '');
                if (feedback) {
                    feedback.textContent = confirm.value ? (mismatch ? 'The passwords do not match.' : 'Passwords match.') : '';
                    feedback.dataset.state = mismatch ? 'error' : 'success';
                }
            }
        }
        root.addEventListener('focusin', () => { position(); panel.hidden = false; update(); });
        root.addEventListener('focusout', event => { if (!root.contains(event.relatedTarget)) panel.hidden = true; });
        input.addEventListener('input', update);
        confirm?.addEventListener('input', update);
        form.addEventListener('reset', () => setTimeout(update, 0));
        form.addEventListener('submit', event => {
            update();
            if (!form.checkValidity()) { event.preventDefault(); form.reportValidity(); }
        });
        window.addEventListener('resize', position);
        window.addEventListener('site-language-changed', update);
        update();
    });
})();
