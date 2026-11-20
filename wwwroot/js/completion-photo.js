(() => {
    document.querySelectorAll('[data-completion-upload]').forEach(root => {
        const input = root.querySelector('input[type=file]');
        const preview = root.querySelector('.completion-upload-preview');
        const video = preview.querySelector('video');
        const feedback = root.querySelector('.completion-upload-feedback');
        const label = root.querySelector('[data-upload-label]');
        let url;
        function clear(resetInput = true) {
            video.pause();
            video.removeAttribute('src');
            video.load();
            if (url) URL.revokeObjectURL(url);
            url = null;
            preview.hidden = true;
            if (resetInput) input.value = '';
            label.textContent = 'Choose a video';
            feedback.hidden = true;
        }
        input.addEventListener('change', () => {
            const file = input.files[0];
            clear(false);
            if (!file) return;
            if (!['video/mp4', 'video/webm'].includes(file.type) || file.size > 10 * 1024 * 1024) {
                clear();
                feedback.textContent = 'Choose an MP4 or WebM video up to 10 MB.';
                feedback.hidden = false;
                return;
            }
            url = URL.createObjectURL(file);
            video.src = url;
            root.querySelector('[data-upload-name]').textContent = file.name;
            root.querySelector('[data-upload-size]').textContent = (file.size / (1024 * 1024)).toFixed(1) + ' MB';
            label.textContent = 'Change video';
            preview.hidden = false;
        });
        video.addEventListener('error', () => {
            clear();
            feedback.textContent = 'This video cannot be previewed in this browser. Choose another MP4 or WebM video.';
            feedback.hidden = false;
        });
        root.closest('form').addEventListener('submit', event => {
            const note = event.target.elements.namedItem('note');
            if (!note.value.trim() && !input.files.length) {
                event.preventDefault();
                feedback.textContent = 'Add a completion note or choose a video before marking work finished.';
                feedback.hidden = false;
                note.focus();
            }
        });
        root.querySelector('[data-upload-remove]').addEventListener('click', () => { clear(); input.focus(); });
        window.addEventListener('pagehide', () => { if (url) URL.revokeObjectURL(url); });
    });
})();
