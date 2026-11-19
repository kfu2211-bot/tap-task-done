document.querySelectorAll('.identity-review').forEach(form => {
    const approve = form.querySelector('button[value="approved"]');
    if (!approve) return;
    let submitting = false;
    const fields = name => form.elements.namedItem(name);
    const checks = ['matchConfirmed', 'faceVisibleConfirmed', 'documentConfirmed', 'movementConfirmed'].map(fields);
    const number = fields('numberOnDocument'), type = fields('typeOnDocument'), note = fields('note');
    const feedback = document.createElement('p');
    feedback.className = 'identity-review-feedback';
    feedback.setAttribute('role', 'status'); feedback.setAttribute('aria-live', 'polite');
    feedback.tabIndex = -1; feedback.hidden = true;
    form.querySelector('.identity-actions').before(feedback);
    const updateApproval = () => {
        approve.disabled = submitting || checks.some(input => !input?.checked || input.disabled) || !number?.value.trim() || !type?.value;
    };
    [...checks, number, type].forEach(input => input?.addEventListener('input', updateApproval));
    updateApproval();
    function show(message, error = false) {
        feedback.textContent = message; feedback.hidden = false;
        feedback.classList.toggle('is-error', error);
        feedback.focus(); feedback.scrollIntoView({ block: 'nearest', behavior: 'auto' });
    }
    note?.addEventListener('input', () => { note.setCustomValidity(''); note.removeAttribute('aria-invalid'); });
    form.addEventListener('submit', async event => {
        const button = event.submitter;
        if (!button || button.name !== 'decision') return;
        event.preventDefault();
        if (submitting) return;
        if (button.value !== 'approved' && !note.value.trim()) {
            note.setCustomValidity('Explain what the user needs to correct before requesting resubmission or rejecting.');
            note.setAttribute('aria-invalid', 'true');
            show('Add a review reason above. The request has not been changed.', true);
            note.focus(); note.reportValidity(); note.setCustomValidity(''); return;
        }
        note.setCustomValidity('');
        if (!form.reportValidity()) return;
        const data = new FormData(form);
        data.set('decision', button.value);
        const buttons = [...form.querySelectorAll('button')];
        const previousStates = buttons.map(item => item.disabled);
        const originalLabel = button.textContent;
        submitting = true; buttons.forEach(item => { item.disabled = true; });
        button.classList.add('is-submitting'); button.textContent = 'Saving decision…';
        form.setAttribute('aria-busy', 'true');
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 30000);
        try {
            const response = await fetch(form.action, { method: 'POST', body: data, headers: { Accept: 'application/json' }, credentials: 'same-origin', signal: controller.signal });
            if (!response.headers.get('content-type')?.includes('application/json'))
                throw new Error(response.status === 400 ? 'Your review session expired. Reload the page and try again.' : 'The server could not confirm this decision. Refresh the queue before trying again.');
            const result = await response.json();
            if (!response.ok || !result.success) { show(result.message || 'The decision could not be saved. Try again.', true); return; }
            const card = form.closest('.identity-request');
            const confirmation = document.createElement('p');
            confirmation.className = 'identity-notice'; confirmation.setAttribute('role', 'status'); confirmation.tabIndex = -1;
            confirmation.textContent = result.message;
            card.replaceWith(confirmation);
            const count = document.querySelector('.identity-count');
            if (count) count.textContent = `${document.querySelectorAll('.identity-request').length} requests`;
            confirmation.focus(); confirmation.scrollIntoView({ block: 'nearest', behavior: 'auto' });
        } catch (error) {
            show(error.name === 'AbortError' ? 'Saving timed out. Refresh the queue to check whether it was saved before trying again.' : (error.message || 'Connection lost. Refresh the queue to check whether the decision was saved.'), true);
        } finally {
            clearTimeout(timeout); submitting = false;
            buttons.forEach((item, index) => { item.disabled = previousStates[index]; });
            button.classList.remove('is-submitting'); button.textContent = originalLabel;
            form.removeAttribute('aria-busy'); updateApproval();
        }
    });
});
window.addEventListener('pageshow', event => { if (event.persisted) window.location.reload(); });
