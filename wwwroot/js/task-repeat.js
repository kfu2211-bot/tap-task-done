(() => {
    const form = document.querySelector('[data-task-repeat]');
    if (!form) return;
    let saving = false;
    form.addEventListener('submit', async event => {
        if (event.defaultPrevented) return;
        event.preventDefault();
        if (saving) return;
        saving = true;
        const button = form.querySelector('button[type="submit"]');
        const message = form.querySelector('[data-repeat-message]');
        const label = button.textContent;
        button.disabled = true;
        button.textContent = 'Saving…';
        message.hidden = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form),
                headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(30000) });
            let target;
            if (response.headers.get('content-type')?.includes('application/json')) {
                const result = await response.json();
                if (!response.ok || !result.success) throw new Error(result.message || 'Could not save this request.');
                target = new URL(result.redirectUrl, location.href);
            } else if (response.ok && response.redirected && new URL(response.url).pathname === '/MarketTasks') {
                target = new URL(response.url);
            } else {
                const html = new DOMParser().parseFromString(await response.text(), 'text/html');
                const notice = html.querySelector('#findFreelancerSection p[role="status"], #findJobsSection > p[role="status"]');
                throw new Error(notice?.textContent.trim() || 'Check your dates, destination, and freelancer availability. Refresh your sign-in in another tab if needed.');
            }
            if (target.origin !== location.origin || target.pathname !== '/MarketTasks') throw new Error('Could not confirm where this request was saved.');
            location.assign(target.href);
        } catch (error) {
            message.textContent = (error.name === 'TimeoutError' ? 'Saving timed out. Check My Tasks before retrying.' : error.message) + ' Your entries are still here.';
            message.hidden = false;
            message.tabIndex = -1;
            message.focus();
            message.scrollIntoView({ block: 'center' });
        } finally {
            saving = false;
            button.disabled = false;
            button.textContent = label;
        }
    });
})();
