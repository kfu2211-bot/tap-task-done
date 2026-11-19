document.querySelectorAll('.identity-review').forEach(form => {
    let submitting = false;
    const approve = form.querySelector('button[value="approved"]');
    const match = form.elements.namedItem('matchConfirmed');
    const faceVisible = form.elements.namedItem('faceVisibleConfirmed');
    const documentChecked = form.elements.namedItem('documentConfirmed');
    const movement = form.elements.namedItem('movementConfirmed');
    const number = form.elements.namedItem('numberOnDocument');
    const type = form.elements.namedItem('typeOnDocument');
    if (approve && match && faceVisible && !match.disabled) {
        const updateApproval = () => {
            approve.disabled = submitting || !match.checked || !faceVisible.checked || !documentChecked?.checked || !movement?.checked || !number?.value.trim() || !type?.value;
        };
        match.addEventListener('change', updateApproval);
        faceVisible.addEventListener('change', updateApproval);
        [documentChecked, movement, number, type].forEach(input => input?.addEventListener('input', updateApproval));
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
