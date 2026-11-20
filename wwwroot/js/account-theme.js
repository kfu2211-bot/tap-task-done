(() => {
    function apply(value) { document.documentElement.dataset.theme = value === 'dark' ? 'dark' : 'light'; }
    try { apply(localStorage.getItem('tts-theme')); }
    catch { apply('light'); }
    // Embedded admin pages and other open tabs must follow the same selection.
    window.addEventListener('storage', event => {
        if (event.key === 'tts-theme' || event.key === null) apply(event.newValue);
    });
})();
