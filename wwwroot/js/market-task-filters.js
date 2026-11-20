(() => {
    const section = document.getElementById('my-tasks-section');
    if (!section) return;

    const filters = section.querySelector('.task-filters');
    const cards = [...section.querySelectorAll('.task-card')];
    const count = section.querySelector('#task-filter-count');
    const empty = section.querySelector('.task-filter-empty');
    const typeFilter = section.querySelector('#myTasksTypeFilter');
    const closedFilter = section.querySelector('#clientClosedFilter');
    const groups = {
        'needs-confirmation': ['pending-confirmation', 'awaiting-confirmation'],
        open: ['open'],
        scheduled: ['scheduled'],
        active: ['booking-pending', 'assigned', 'on-the-way', 'arrived', 'in-progress', 'awaiting-confirmation', 'disputed'],
        completed: ['completed'],
        closed: ['cancelled', 'canceled', 'expired', 'moderated']
    };

    section.querySelectorAll('.edit-task').forEach(editor => {
        const confirm = editor.closest('.task-card').querySelector('[data-confirm-task]');
        editor.addEventListener('toggle', () => { if (confirm) confirm.hidden = editor.open; });
    });
    function openLinkedTask() {
        const id = window.location.hash.slice(1);
        const card = cards.find(task => task.id === id);
        if (!card) return;

        if (card.hidden) {
            if (typeFilter) typeFilter.value = '';
            filters.querySelector('[data-filter="all"]').click();
        }
        card.querySelector('.task-disclosure').open = true;
        card.scrollIntoView({ block: 'start' });
    }

    filters.hidden = false;
    window.addEventListener('hashchange', openLinkedTask);

    filters.addEventListener('click', event => {
        const button = event.target.closest('button[data-filter]');
        if (!button || !filters.contains(button)) return;
        const selected = button.dataset.filter;
        if (closedFilter) closedFilter.parentElement.hidden = selected !== 'closed';
        let visible = 0;
        for (const card of cards) {
            const statusMatch = selected === 'all' ||
                (selected === 'new-offers' ? card.dataset.pendingOffers === 'true' :
                    (groups[selected] || []).includes(card.dataset.taskStatus));
            const closedMatch = selected !== 'closed' || !closedFilter || closedFilter.value === 'all' ||
                card.dataset.taskStatus === closedFilter.value || closedFilter.value === 'cancelled' && card.dataset.taskStatus === 'canceled';
            const matches = statusMatch && closedMatch && (!typeFilter?.value || card.dataset.jobType === typeFilter.value);
            card.hidden = !matches;
            if (matches) visible++;
        }
        for (const option of filters.querySelectorAll('button[data-filter]')) {
            option.setAttribute('aria-pressed', String(option === button));
        }
        count.textContent = `${visible} ${visible === 1 ? 'task' : 'tasks'}`;
        empty.hidden = visible !== 0 || cards.length === 0;
        const noTasks = section.querySelector('.market-empty');
        if (noTasks) noTasks.hidden = selected !== 'all';
    });
    typeFilter?.addEventListener('change', () => filters.querySelector('button[aria-pressed="true"]')?.click());
    closedFilter?.addEventListener('change', () => filters.querySelector('button[data-filter="closed"]')?.click());
    const requestedFilter = new URLSearchParams(location.search).get('filter');
    const filterKey = requestedFilter === 'needs-action' ? 'needs-confirmation' : requestedFilter === 'cancelled' ? 'closed' : requestedFilter;
    const initialButton = [...filters.querySelectorAll('button[data-filter]')]
        .find(button => button.dataset.filter === filterKey);
    (initialButton || filters.querySelector('[data-filter="all"]'))?.click();
    if (filterKey === "new-offers" || filterKey === "needs-confirmation") {
        cards.filter(card => !card.hidden).forEach(card => {
            card.querySelector(".task-disclosure").open = true;
        });
    }
    openLinkedTask();
})();
const cancelTaskDialog = document.getElementById("cancelTaskDialog");
if (cancelTaskDialog) {
    let pendingTaskForm;
    let confirmedTaskForm;

    document.querySelectorAll("form[data-cancel-task]").forEach(form => {
        form.addEventListener("submit", event => {
            if (confirmedTaskForm === form) return;
            event.preventDefault();
            pendingTaskForm = form;
            cancelTaskDialog.querySelector("[data-task-title]").textContent = form.dataset.cancelTask;
            cancelTaskDialog.returnValue = "";
            cancelTaskDialog.showModal();
        });
    });

    cancelTaskDialog.addEventListener("close", () => {
        const form = pendingTaskForm;
        pendingTaskForm = null;
        if (!form || cancelTaskDialog.returnValue !== "cancel-task") return;

        confirmedTaskForm = form;
        try {
            form.requestSubmit();
        } finally {
            confirmedTaskForm = null;
        }
    });
}
