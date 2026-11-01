(() => {
    'use strict';
    const source = document.querySelector('[data-code-source]');
    if (source) {
        const form = source.form;
        const group = document.createElement('div');
        group.className = 'verification-code-cells';
        group.setAttribute('role', 'group');
        group.setAttribute('aria-labelledby', 'verification-code-label');
        const cells = Array.from({ length: 6 }, (_, index) => {
            const cell = document.createElement('input');
            cell.type = 'text'; cell.inputMode = 'numeric'; cell.maxLength = 6;
            cell.pattern = '[0-9]'; cell.required = true;
            cell.autocomplete = index === 0 ? 'one-time-code' : 'off';
            cell.id = 'verification-digit-' + index;
            cell.setAttribute('aria-label', `Digit ${index + 1} of 6`);
            cell.setAttribute('aria-describedby', 'verification-expiry');
            cell.spellcheck = false;
            group.append(cell);
            return cell;
        });
        const sync = () => { source.value = cells.map(cell => cell.value).join(''); };
        const distribute = (text, start) => {
            const digits = text.replace(/[^0-9]/g, '');
            if (!digits) { cells[start].value = ''; sync(); return; }
            // Full OTP autofill/paste replaces the entire code even if focus is in a later cell.
            if (digits.length >= 6) start = 0;
            const chunk = digits.slice(0, 6 - start);
            [...chunk].forEach((digit, offset) => { cells[start + offset].value = digit; });
            sync();
            const next = cells[Math.min(start + chunk.length, 5)];
            next.focus(); next.select();
        };
        source.insertAdjacentElement('afterend', group);
        const initial = source.value.replace(/[^0-9]/g, '').slice(0, 6);
        [...initial].forEach((digit, index) => { cells[index].value = digit; });
        source.type = 'hidden'; source.removeAttribute('autofocus'); source.required = false;
        document.querySelector('label[for="Code"]')?.setAttribute('for', cells[0].id);
        cells.forEach((cell, index) => {
            cell.addEventListener('focus', () => cell.select());
            cell.addEventListener('input', () => distribute(cell.value, index));
            cell.addEventListener('paste', event => {
                event.preventDefault(); distribute(event.clipboardData?.getData('text') || '', index);
            });
            cell.addEventListener('keydown', event => {
                if (event.key === 'Backspace' && !cell.value && index > 0) {
                    event.preventDefault(); cells[index - 1].value = ''; cells[index - 1].focus(); sync();
                } else if (event.key === 'ArrowLeft' && index > 0) { event.preventDefault(); cells[index - 1].focus(); }
                else if (event.key === 'ArrowRight' && index < 5) { event.preventDefault(); cells[index + 1].focus(); }
            });
        });
        form.addEventListener('submit', event => {
            sync();
            if (!/^[0-9]{6}$/.test(source.value)) { event.preventDefault(); cells.find(cell => !cell.value)?.focus(); }
        });
        form.addEventListener('reset', () => { cells.forEach(cell => { cell.value = ''; }); source.value = ''; });
        cells[Math.min(initial.length, 5)].focus();
    }
    document.querySelectorAll('[data-resend-seconds]').forEach(form => {
        const button = form.querySelector('button[type=submit]');
        const label = form.querySelector('[data-resend-label]');
        const end = Date.now() + Math.max(0, Number(form.dataset.resendSeconds) || 0) * 1000;
        const tick = () => {
            if (form.dataset.submitting === 'true') return 0;
            const seconds = Math.max(0, Math.ceil((end - Date.now()) / 1000));
            button.disabled = seconds > 0;
            const minutes = String(Math.floor(seconds / 60)).padStart(2, '0');
            label.textContent = seconds ? `Available in ${minutes}:${String(seconds % 60).padStart(2, '0')}` : 'You can request a new code.';
            return seconds;
        };
        if (tick()) { const timer = setInterval(() => { if (!tick()) clearInterval(timer); }, 1000); }
    });
    document.querySelectorAll('.email-verification-card form').forEach(form => {
        form.addEventListener('submit', event => {
            if (event.defaultPrevented) return;
            if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
            form.dataset.submitting = 'true';
            const button = form.querySelector('button[type=submit]');
            button.disabled = true;
            button.textContent = form.hasAttribute('data-code-form') ? 'Verifying…' : 'Sending…';
            form.setAttribute('aria-busy', 'true');
        });
    });
    window.addEventListener('pageshow', event => {
        if (event.persisted) location.reload(); // Refresh server-owned cooldown and pending state after browser Back.
    });
})();
