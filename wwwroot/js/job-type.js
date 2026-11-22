(() => {
    document.querySelectorAll('[data-job-type-field]').forEach(root => {
        const type = root.querySelector('[name="jobType"]');
        const end = root.querySelector('[data-job-end]');
        const form = type.form;
        function update() {
            const multi = type.value === 'multi-day';
            end.required = true;
            for (const control of form.querySelectorAll('#preferredDateInput, #preferredTimeInput')) control.required = true;
            const date = form.querySelector('#preferredDateInput');
            const time = form.querySelector('#preferredTimeInput');
            const nativeStart = form.querySelector('input[name="schedule"][type="datetime-local"]');
            const start = nativeStart?.value || (date?.value && time?.value ? date.value + 'T' + time.value : '');
            end.min = start;
            let error = '';
            if (end.value && start) {
                if (end.value <= start) error = 'Choose a finish after the work start.';
                else if (multi && end.value.slice(0, 10) <= start.slice(0, 10)) error = 'Multi-Day Job must finish on a later date.';
                else if (!multi && end.value.slice(0, 10) !== start.slice(0, 10)) error = 'Short and Long Jobs must finish on their start date.';
            }
            end.setCustomValidity(window.ttsTranslate?.(error) || error);
        }
        type.addEventListener('change', update);
        end.addEventListener('input', update);
        form.querySelectorAll('#preferredDateInput, #preferredTimeInput, input[name="schedule"]').forEach(control => control.addEventListener('input', update));
        form.addEventListener('reset', () => setTimeout(update, 0));
        window.addEventListener('site-language-changed', update);
        update();
    });
})();
