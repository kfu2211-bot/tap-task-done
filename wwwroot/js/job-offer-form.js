document.querySelectorAll('.job-offer-toggle').forEach(toggle => {
    const form = document.getElementById(toggle.getAttribute('aria-controls'));
    if (!form) return;

    const price = form.elements.namedItem('price');
    if (!(price instanceof HTMLInputElement)) return;

    form.hidden = true;
    toggle.hidden = false;
    toggle.addEventListener('click', () => {
        form.hidden = !form.hidden;
        toggle.setAttribute('aria-expanded', String(!form.hidden));
        if (!form.hidden) price.focus({ preventScroll: true });
    });

    const budget = form.querySelector('[data-offer-price]');
    if (budget) {
        budget.hidden = false;
        budget.addEventListener('click', () => {
            price.value = budget.dataset.offerPrice;
            price.focus();
        });
    }
});

document.querySelectorAll('[data-job-offer-submit]').forEach(form => {
    const card = form.closest('[data-job-category], article');
    form.addEventListener('submit', async event => {
        if (event.defaultPrevented) return;
        event.preventDefault();
        if (card.dataset.offerSending === 'true') return;
        card.dataset.offerSending = 'true';
        const button = event.submitter || form.querySelector('button[type="submit"]');
        const label = button.textContent;
        const data = new FormData(form);
        button.disabled = true;
        button.textContent = 'Sending…';
        let feedback = card.querySelector('[data-offer-feedback]');
        if (!feedback) {
            feedback = document.createElement('div');
            feedback.dataset.offerFeedback = '';
            feedback.className = 'offer-submit-feedback';
            card.append(feedback);
        }
        feedback.replaceChildren();
        try {
            const response = await fetch(form.action, { method: 'POST', body: data,
                headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(30000) });
            if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Check your sign-in and identity verification, then try again.');
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || 'Your offer could not be sent.');
            card.querySelectorAll('[data-job-offer-submit], .job-offer-toggle').forEach(node => { node.hidden = true; });
            feedback.setAttribute('role', 'status');
            const text = document.createElement('p');
            text.textContent = 'Offer sent · Pending. Waiting for the client response.';
            const link = document.createElement('a');
            link.className = 'task-chat-action';
            link.href = `/MyJobs?view=offers&offerFilter=pending#job-${encodeURIComponent(data.get('id'))}`;
            link.textContent = 'Manage my offer';
            feedback.append(text, link);
        } catch (error) {
            feedback.setAttribute('role', 'alert');
            feedback.textContent = (error.name === 'TimeoutError' ? 'Sending timed out. Check My offers before retrying.' : error.message) + ' Your entries are still here.';
        } finally {
            delete card.dataset.offerSending;
            button.disabled = false;
            button.textContent = label;
        }
    });
});
