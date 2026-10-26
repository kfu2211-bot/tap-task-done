(() => {
    const section = document.getElementById('my-tasks-section');
    if (!section) return;

    const filters = section.querySelector('.task-filters');
    const cards = [...section.querySelectorAll('.task-card')];
    const count = section.querySelector('#task-filter-count');
    const empty = section.querySelector('.task-filter-empty');
    const groups = {
        'needs-action': ['pending-confirmation', 'awaiting-confirmation'],
        open: ['open'],
        active: ['booking-pending', 'assigned', 'on-the-way', 'arrived', 'in-progress', 'awaiting-confirmation', 'disputed'],
        completed: ['completed'],
        cancelled: ['cancelled']
    };

    section.querySelectorAll('.edit-task').forEach(editor => {
        const confirm = editor.closest('.task-card').querySelector('[data-confirm-task]');
        editor.addEventListener('toggle', () => { if (confirm) confirm.hidden = editor.open; });
    });
    filters.hidden = false;
    filters.addEventListener('click', event => {
        const button = event.target.closest('button[data-filter]');
        if (!button || !filters.contains(button)) return;
        const selected = button.dataset.filter;
        let visible = 0;
        for (const card of cards) {
            const matches = selected === 'all' || (groups[selected] || []).includes(card.dataset.taskStatus);
            card.hidden = !matches;
            if (matches) visible++;
        }
        for (const option of filters.querySelectorAll('button[data-filter]')) {
            option.setAttribute('aria-pressed', String(option === button));
        }
        count.textContent = `${visible} ${visible === 1 ? 'task' : 'tasks'}`;
        empty.hidden = selected === 'all' || visible !== 0;
        const noTasks = section.querySelector('.market-empty');
        if (noTasks) noTasks.hidden = selected !== 'all';
    });
})();
