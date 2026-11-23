// Keep the same map instance, pins and camera when changing the basemap.
(() => {
    const style = () => 'https://tiles.openfreemap.org/styles/' + (document.documentElement.dataset.theme === 'dark' ? 'dark' : 'liberty');
    function bind(map, restoreOverlays = () => {}) {
        let current = style();
        const observer = new MutationObserver(() => {
            const next = style();
            if (next === current) return;
            current = next;
            map.setStyle(next);
        });
        observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
        map.on('style.load', restoreOverlays);
        map.on('remove', () => observer.disconnect());
    }
    window.ttsMapTheme = { style, bind };
})();
