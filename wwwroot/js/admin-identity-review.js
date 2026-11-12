document.querySelectorAll('.identity-review').forEach(form => {
    let submitting = false;
    form.addEventListener('submit', event => {
        if (submitting) { event.preventDefault(); return; }
        const button = event.submitter;
        if (!button || button.name !== 'decision') return;
        submitting = true;
        const decision = document.createElement('input');
        decision.type = 'hidden';
        decision.name = button.name;
        decision.value = button.value;
        form.appendChild(decision);
        form.querySelectorAll('button').forEach(item => { item.disabled = true; });
        button.classList.add('is-submitting');
        button.textContent = 'Saving decision…';
        form.setAttribute('aria-busy', 'true');
    });
});
window.addEventListener('pageshow', event => { if (event.persisted) window.location.reload(); });
