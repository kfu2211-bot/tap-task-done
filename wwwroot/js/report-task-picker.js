(() => {
    const select = document.getElementById('reportTask');
    if (!select) return;

    const picker = document.createElement('details');
    picker.className = 'related-task-picker';
    const trigger = document.createElement('summary');
    trigger.setAttribute('aria-describedby', 'reportTaskHelp');
    const list = document.createElement('div');
    list.className = 'related-task-options';
    picker.append(trigger, list);

    const rows = Array.from(select.options, option => {
        const row = document.createElement('button');
        row.type = 'button';
        const copy = document.createElement('span');
        const title = document.createElement('strong');
        title.textContent = option.value ? `${option.dataset.title} #${option.value}` : option.textContent;
        copy.append(title);
        row.append(copy);
        if (option.value) {
            const meta = document.createElement('small');
            meta.textContent = `${option.dataset.date} · ${option.dataset.freelancer}`;
            copy.append(meta);
            const status = document.createElement('span');
            status.className = 'related-task-status';
            status.dataset.status = option.dataset.status;
            status.textContent = option.dataset.status;
            row.append(status);
        }
        const check = document.createElement('span');
        check.className = 'related-task-check';
        check.setAttribute('aria-hidden', 'true');
        check.textContent = '✓';
        row.append(check);
        row.addEventListener('click', () => {
            select.value = option.value;
            select.dispatchEvent(new Event('change', { bubbles: true }));
            picker.open = false;
            trigger.focus();
        });
        list.append(row);
        return { row, option, title };
    });
    function update() {
        for (const { row, option, title } of rows) {
            row.setAttribute('aria-pressed', String(option.selected));
            if (option.selected) trigger.textContent = title.textContent;
        }
    }

    select.after(picker);
    select.hidden = true;
    select.classList.add('related-task-native');
    const label = document.querySelector('label[for="reportTask"]');
    label.addEventListener('click', event => {
        event.preventDefault();
        trigger.focus();
        picker.open = true;
    });
    picker.addEventListener('keydown', event => {
        if (event.key !== 'Escape' || !picker.open) return;
        picker.open = false;
        trigger.focus();
    });
    document.addEventListener('pointerdown', event => {
        if (!picker.contains(event.target) && !label.contains(event.target)) picker.open = false;
    });
    select.addEventListener('change', update);
    select.form.addEventListener('reset', () => setTimeout(update, 0));
    update();
})();
