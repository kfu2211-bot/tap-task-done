(() => {
    document.querySelectorAll('input[name="estimatedMinutes"]').forEach(input => {
        const initialValue = input.value;
        const wrapped = input.closest('label');
        if (wrapped) {
            const container = document.createElement('div');
            container.className = wrapped.className;
            container.append(...wrapped.childNodes);
            wrapped.replaceWith(container);
        }
        const root = document.createElement('fieldset');
        root.className = 'job-duration-picker';
        const legend = document.createElement('legend');
        legend.textContent = 'Estimated work duration (optional)';
        const select = document.createElement('select');
        select.setAttribute('aria-label', 'Estimated work duration');
        [['', 'Not sure yet'], ['15', '15 minutes'], ['30', '30 minutes'], ['60', '1 hour'], ['90', '1 hour 30 minutes'], ['120', '2 hours'], ['180', '3 hours'], ['240', '4 hours'], ['480', '8 hours'], ['custom', 'Custom duration']]
            .forEach(([value, text]) => select.add(new Option(text, value)));
        const custom = document.createElement('div');
        custom.className = 'job-duration-custom';
        const hours = document.createElement('input'), minutes = document.createElement('input');
        for (const [control, title, max] of [[hours, 'Hours', 720], [minutes, 'Minutes', 59]]) {
            control.type = 'number'; control.min = '0'; control.max = String(max); control.step = '1';
            control.inputMode = 'numeric';
            const label = document.createElement('label'); label.append(title, control); custom.append(label);
        }
        const help = document.createElement('small');
        help.textContent = 'Choose how long the work may take, not the start time.';
        root.append(legend, select, custom, help);
        const oldLabel = input.id && document.querySelector(`label[for="${input.id}"]`);
        if (oldLabel) oldLabel.remove();
        // Remove the old duration-only caption; keep the actual server-bound value.
        if (input.previousSibling?.nodeType === Node.TEXT_NODE && input.previousSibling.textContent.includes('Estimated duration')) input.previousSibling.remove();
        const caption = input.previousElementSibling;
        if (caption?.tagName === 'SPAN' && caption.textContent.includes('Estimated work duration')) caption.remove();
        input.before(root);
        input.type = 'hidden'; input.removeAttribute('id');
        function update() {
            const active = select.value === 'custom'; custom.hidden = !active;
            hours.disabled = minutes.disabled = !active;
            hours.required = minutes.required = active;
            const total = Number(hours.value || 0) * 60 + Number(minutes.value || 0);
            minutes.setCustomValidity(active && (total < 15 || total > 43200) ? 'Choose a duration from 15 minutes to 720 hours.' : '');
            input.value = active ? String(total) : select.value;
        }
        function restore(value = initialValue) {
            select.value = [...select.options].some(option => option.value === value) ? value : 'custom';
            hours.value = String(Math.floor(Number(value || 0) / 60)); minutes.value = String(Number(value || 0) % 60);
            update();
        }
        select.addEventListener('change', update); hours.addEventListener('input', update); minutes.addEventListener('input', update);
        input.form?.addEventListener('reset', () => setTimeout(restore, 0));
        restore();
    });
})();
