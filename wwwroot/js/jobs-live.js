(() => {
    const section = document.getElementById('findJobsSection');
    const specific = document.getElementById('jobsSpecificService');
    if (!section || !specific) return;
    let refreshing = false;

    async function refresh() {
        if (document.hidden || section.classList.contains('hidden') || refreshing) return;
        refreshing = true;
        try {
            const response = await fetch('/FindPost?mode=jobs', { cache: 'no-store', signal: AbortSignal.timeout(8000) });
            if (!response.ok || response.redirected) return;
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            if (!page.querySelector('#jobsCategoryFilter')) return;
            const open = new Set([...page.querySelectorAll('#findJobsSection .jobs-card[id]')].map(card => card.id));
            let changed = false;
            for (const card of section.querySelectorAll('.jobs-card[id]')) {
                if (open.has(card.id)) continue;
                card.remove();
                changed = true;
            }
            if (changed) {
                specific.dispatchEvent(new Event('change'));
                let message = section.querySelector('[data-job-list-update]');
                if (!message) {
                    message = document.createElement('p');
                    message.dataset.jobListUpdate = '';
                    message.setAttribute('role', 'status');
                    specific.closest('.jobs-category-filter').after(message);
                }
                message.textContent = 'The job list was updated. Tasks that have been accepted or closed are no longer available.';
            }
        } catch {
            // Keep the current list during a temporary connection issue.
        } finally {
            refreshing = false;
        }
    }
    setInterval(refresh, 5000);
    window.addEventListener('focus', refresh);
    document.addEventListener('visibilitychange', refresh);
})();
