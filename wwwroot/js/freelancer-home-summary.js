(() => {
    const overview = document.querySelector('[data-freelancer-summary]');
    const url = overview?.dataset.freelancerSummary;
    if (!url) return;

    const counts = overview.querySelectorAll('.home-metric-copy strong');
    const scheduledDetails = document.querySelector('[data-scheduled-details]');
    const scheduledEmpty = document.querySelector('[data-scheduled-empty]');
    let refreshing = false;

    async function refresh() {
        if (refreshing || document.hidden) return;
        refreshing = true;
        try {
            const response = await fetch(url, {
                cache: 'no-store',
                signal: AbortSignal.timeout(8000)
            });
            if (!response.ok || response.redirected) return;
            const { counts: summary, scheduled } = await response.json();
            if (!Array.isArray(summary) || summary.length !== counts.length) return;

            counts.forEach((count, index) => {
                const value = String(summary[index]);
                if (count.textContent !== value) count.textContent = value;
            });
            if (scheduledDetails && scheduledEmpty) {
                scheduledDetails.hidden = !scheduled;
                scheduledEmpty.hidden = !!scheduled;
                const attention = scheduled && ['Due now', 'Schedule overdue'].includes(scheduled.status);
                document.querySelector('[data-schedule-heading]').textContent = attention ? 'Schedule needs attention' : 'Upcoming job';
                const help = scheduledDetails.querySelector('[data-scheduled-help]');
                if (help) help.hidden = !attention;
                if (scheduled) {
                    scheduledDetails.querySelector('[data-scheduled-status]').textContent = scheduled.status;
                    scheduledDetails.querySelector('[data-scheduled-status]').dataset.status = scheduled.status;
                    scheduledDetails.querySelector('[data-scheduled-title]').textContent = scheduled.title;
                    scheduledDetails.querySelector('[data-scheduled-time]').textContent = scheduled.scheduleText;
                    scheduledDetails.querySelector('[data-scheduled-link]').href = scheduled.url;
                    const confirmation = scheduledDetails.querySelector('[data-scheduled-confirmation]');
                    const next = scheduledDetails.querySelector('[data-scheduled-next]');
                    if (confirmation) { confirmation.textContent = scheduled.confirmation || ''; confirmation.dataset.ready = String(scheduled.agreementConfirmed === true); }
                    if (next) next.textContent = scheduled.nextStep || '';
                } else {
                    scheduledEmpty.textContent = 'No accepted job schedule yet. Accepted jobs appear here even while work details are awaiting confirmation.';
                }
            }
        } catch {
            // Preserve the last successful count during a temporary connection issue.
        } finally {
            refreshing = false;
        }
    }

    window.addEventListener('focus', refresh);
    window.addEventListener('pageshow', refresh);
    document.addEventListener('visibilitychange', refresh);
    setInterval(refresh, 5000);
})();
