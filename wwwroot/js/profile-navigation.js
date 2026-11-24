// Preserve the originating workspace, filters and card anchor on profile links.
(() => {
    if (location.pathname.toLowerCase() === '/reviewprofile') return;
    function update() {
        document.querySelectorAll('a[href*="/ReviewProfile"]').forEach(link => {
            const url = new URL(link.href, location.origin);
            if (url.origin !== location.origin || url.pathname.toLowerCase() !== '/reviewprofile' || url.searchParams.has('returnUrl')) return;
            url.searchParams.set('returnUrl', location.pathname + location.search + location.hash);
            link.href = url.pathname + url.search + url.hash;
        });
    }
    update();
    new MutationObserver(update).observe(document.body, { childList: true, subtree: true });
})();
