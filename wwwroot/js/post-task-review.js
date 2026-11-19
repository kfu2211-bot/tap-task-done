(() => {
    const form = document.getElementById('databasePostTaskForm');
    const error = document.getElementById('taskReviewError');
    if (!form || !error) return;
    let saving = false;
    form.addEventListener('submit', async event => {
        if (event.defaultPrevented) return;
        event.preventDefault();
        if (saving || !form.reportValidity()) return;
        saving = true;
        error.hidden = true;
        const buttons = [...form.querySelectorAll('button[type="submit"], input[type="submit"]')];
        const states = buttons.map(button => ({ button, disabled: button.disabled, text: button.textContent }));
        buttons.forEach(button => { button.disabled = true; button.textContent = 'Saving draft…'; });
        const controller = new AbortController();
        const timer = globalThis.setTimeout(() => controller.abort(), 30000);
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form), headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.headers.get('content-type')?.includes('application/json')) {
                throw new Error(response.status === 400 || response.redirected
                    ? 'Your session may have expired. Open this site in another tab to sign in, then try again here.'
                    : 'Could not save your task. Your details are still here; please try again.');
            }
            const result = await response.json();
            if (!response.ok || !result.success || !result.redirectUrl) throw new Error(result.message || 'Could not save your task. Please try again.');
            const destination = new URL(result.redirectUrl, location.origin);
            if (destination.origin !== location.origin) throw new Error('Could not open the review. Please try again.');
            location.assign(destination.href);
        } catch (failure) {
            error.textContent = failure.name === 'AbortError'
                ? 'Saving took too long. Your details are still here. Try again; an existing draft will be reused.'
                : failure instanceof TypeError ? 'Connection interrupted. Your details are still here; please try again.' : failure.message;
            error.hidden = false;
            error.scrollIntoView({ behavior: 'smooth', block: 'center' });
        } finally {
            globalThis.clearTimeout(timer);
            saving = false;
            states.forEach(({ button, disabled, text }) => { button.disabled = disabled; button.textContent = text; });
        }
    });
})();
