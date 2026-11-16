for (const form of document.querySelectorAll('[data-account-search]')) {
    const input = form.elements.accountSearch;
    const results = form.parentElement.querySelector('[data-account-matches]');
    const status = form.querySelector('[data-account-search-status]');
    let timer;
    let activeRequest;
    input.addEventListener('input', () => {
        clearTimeout(timer);
        activeRequest?.abort();
        results.replaceChildren();
        status.textContent = '';
        const term = input.value.trim();
        if (!term) return;
        timer = setTimeout(async () => {
            const request = new AbortController();
            activeRequest = request;
            const url = new URL(form.dataset.lookupUrl, location.href);
            url.searchParams.set('recordId', form.elements.recordId.value);
            url.searchParams.set('accountSearch', term);
            status.textContent = 'Searching accounts…';
            try {
                const response = await fetch(url, { signal: request.signal, headers: { Accept: 'application/json' } });
                if (!response.ok || response.redirected) throw new Error('Search unavailable');
                const accounts = await response.json();
                if (request.signal.aborted) return;
                for (const account of accounts) {
                    const item = document.createElement('li');
                    const link = document.createElement('a');
                    const target = new URL(form.action, location.href);
                    target.searchParams.set('recordId', form.elements.recordId.value);
                    target.searchParams.set('embedded', form.elements.embedded.value);
                    target.searchParams.set('accountId', account.id);
                    target.hash = 'reported-account';
                    link.href = target.href;
                    const avatar = document.createElement('span');
                    avatar.className = 'report-match-avatar';
                    avatar.textContent = account.name.trim().slice(0, 1).toUpperCase() || '?';
                    avatar.setAttribute('aria-hidden', 'true');
                    const photo = document.createElement('img');
                    photo.alt = '';
                    photo.addEventListener('error', () => photo.remove(), { once: true });
                    const photoUrl = new URL(form.dataset.photoUrl, location.href);
                    photoUrl.searchParams.set('userId', account.id);
                    photo.src = photoUrl.href;
                    avatar.append(photo);
                    const details = document.createElement('span');
                    const name = document.createElement('strong');
                    name.textContent = account.name || 'Unnamed account';
                    const email = document.createElement('small');
                    email.textContent = account.email + ' · #' + account.id;
                    details.append(name, email);
                    link.append(avatar, details);
                    item.append(link);
                    results.append(item);
                }
                status.textContent = accounts.length ? '' : 'No matching accounts.';
            } catch {
                if (!request.signal.aborted) status.textContent = 'Search unavailable. Try Find account.';
            }
        }, 300);
    });
}
