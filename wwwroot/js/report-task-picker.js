(() => {
    const select = document.getElementById('reportTask');
    const summary = document.getElementById('reportTaskSelection');
    if (!select || !summary) return;
    const update = () => {
        const option = select.selectedOptions[0];
        summary.hidden = !option || !option.value;
        if (summary.hidden) return;
        document.getElementById('reportTaskTitle').textContent = option.dataset.title || '';
        document.getElementById('reportTaskMeta').textContent = `#${option.value} · Freelancer: ${option.dataset.freelancer || 'No freelancer assigned'} · ${option.dataset.status || ''} · ${option.dataset.date || ''}`;
    };
    select.addEventListener('change', update);
    update();
})();
