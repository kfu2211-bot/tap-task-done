(() => {
    document.querySelectorAll('[data-completion-upload]').forEach(root => {
        const input = root.querySelector('input[type=file]');
        const preview = root.querySelector('.completion-upload-preview');
        const image = preview.querySelector('img');
        const feedback = root.querySelector('.completion-upload-feedback');
        const label = root.querySelector('[data-upload-label]');
        let url;
        function clear(resetInput = true) {
            if (url) URL.revokeObjectURL(url);
            url = null;
            image.removeAttribute('src');
            preview.hidden = true;
            if (resetInput) input.value = '';
            label.textContent = 'Choose a photo';
            feedback.hidden = true;
        }
        input.addEventListener('change', () => {
            const file = input.files[0];
            clear(false);
            if (!file) return;
            if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024) {
                clear();
                feedback.textContent = 'Choose a JPG, PNG or WEBP photo up to 5 MB.';
                feedback.hidden = false;
                return;
            }
            url = URL.createObjectURL(file);
            image.src = url;
            root.querySelector('[data-upload-name]').textContent = file.name;
            root.querySelector('[data-upload-size]').textContent = file.size < 1024 * 1024 ? `${Math.max(1, Math.round(file.size / 1024))} KB` : `${(file.size / (1024 * 1024)).toFixed(1)} MB`;
            label.textContent = 'Change photo';
            preview.hidden = false;
        });
        image.addEventListener('error', () => { clear(); feedback.textContent = 'This image could not be previewed. Choose another photo.'; feedback.hidden = false; });
        root.querySelector('[data-upload-remove]').addEventListener('click', () => { clear(); input.focus(); });
        window.addEventListener('pagehide', () => { if (url) URL.revokeObjectURL(url); });
    });
})();
