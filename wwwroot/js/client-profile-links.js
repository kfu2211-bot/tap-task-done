(() => {
    function updateClientLinks() {
        document.querySelectorAll('a[href*="/ReviewProfile"]').forEach(link => {
            if (link.dataset.profileChip === 'true') return;
            const url = new URL(link.href, location.origin);
            if (url.searchParams.get('role') !== 'client') return;
            const id = url.searchParams.get('id');
            if (!/^\d+$/.test(id || '')) return;
            const name = link.textContent.trim();
            link.dataset.profileChip = 'true';
            link.classList.add('client-profile-link');

            const avatar = document.createElement('span');
            avatar.className = 'client-profile-avatar';
            avatar.setAttribute('aria-hidden', 'true');
            avatar.textContent = Array.from(name)[0]?.toUpperCase() || '?';
            const photo = document.createElement('img');
            photo.src = '/ProfilePhoto?providerId=' + encodeURIComponent(id);
            photo.alt = '';
            photo.loading = 'lazy';
            photo.addEventListener('error', () => photo.remove(), { once: true });
            photo.src = "/ProfilePhoto?handler=Client&userId=" + encodeURIComponent(id);
            avatar.append(photo);

            const copy = document.createElement('span');
            copy.className = 'client-profile-copy';
            const label = document.createElement('small');
            label.textContent = 'Client';
            const title = document.createElement('strong');
            title.textContent = name;
            title.dataset.accountId = id;
            copy.append(label, title);
            const arrow = document.createElement('span');
            arrow.className = 'client-profile-arrow';
            arrow.textContent = '↗';
            arrow.setAttribute('aria-hidden', 'true');
            link.replaceChildren(avatar, copy, arrow);
        });
    }
    updateClientLinks();
    new MutationObserver(updateClientLinks).observe(document.body, { childList: true, subtree: true });
})();
