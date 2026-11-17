document.querySelectorAll('.identity-review').forEach(form => {
    let submitting = false;
    const approve = form.querySelector('button[value="approved"]');
    const match = form.elements.namedItem('matchConfirmed');
    const faceVisible = form.elements.namedItem('faceVisibleConfirmed');
    if (approve && match && faceVisible && !match.disabled) {
        const updateApproval = () => {
            approve.disabled = submitting || !match.checked || !faceVisible.checked;
        };
        match.addEventListener('change', updateApproval);
        faceVisible.addEventListener('change', updateApproval);
        updateApproval();
    }
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
