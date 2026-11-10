(() => {
    'use strict';
    let photoUrl = null, request = 0;
    const selector = '#profileAvatar, .header-account-avatar, #sidebarAvatar, #userAvatar, .sidebar-account .account-avatar, .sidebar-user .user-avatar';
    function paint() {
        document.querySelectorAll(selector).forEach(avatar => {
            if (!avatar.dataset.photoInitials) avatar.dataset.photoInitials = avatar.textContent.trim() || 'U';
            avatar.replaceChildren();
            avatar.classList.toggle('has-photo', !!photoUrl);
            if (!photoUrl) { avatar.textContent = avatar.dataset.photoInitials; return; }
            const image = document.createElement('img');
            image.src = photoUrl; image.alt = 'Profile photo';
            image.style.cssText = 'width:100%;height:100%;object-fit:cover;border-radius:inherit;display:block;';
            avatar.style.overflow = 'hidden';
            avatar.append(image);
        });
    }
    async function refresh() {
        const version = ++request;
        const response = await fetch('/ProfilePhoto', { credentials: 'same-origin', cache: 'no-store' });
        if (response.redirected || !response.ok) throw new Error('Profile photo could not be loaded.');
        const blob = response.status === 204 ? null : await response.blob();
        if (version !== request) return;
        if (blob && !['image/jpeg', 'image/png', 'image/webp'].includes(blob.type)) throw new Error('Invalid profile photo response.');
        if (photoUrl) URL.revokeObjectURL(photoUrl);
        photoUrl = blob ? URL.createObjectURL(blob) : null;
        paint();
    }
    function ready() {
        const input = document.getElementById('profilePhotoInput');
        const remove = document.getElementById('removePhotoButton');
        const message = document.getElementById('photoMessage');
        refresh().catch(() => { /* Keep account initials if the image is unavailable. */ });
        async function change(handler, file) {
            input.disabled = true; remove.disabled = true;
            message.textContent = handler === 'Upload' ? 'Uploading photo…' : 'Removing photo…';
            try {
                const data = new FormData();
                const token = document.querySelector('#profilePhotoToken input[name="__RequestVerificationToken"]');
                if (!token) throw new Error('Please refresh the page and try again.');
                data.append('__RequestVerificationToken', token.value);
                if (file) data.append('photo', file);
                const response = await fetch('/ProfilePhoto?handler=' + handler, { method: 'POST', credentials: 'same-origin', body: data });
                if (response.redirected) throw new Error('Please sign in again.');
                const result = await response.json().catch(() => ({}));
                if (!response.ok) throw new Error(result.message || 'Photo could not be saved. Please try again.');
                await refresh();
                message.textContent = result.message;
                // Remove obsolete device-only photo data; the database is authoritative.
                try { const account = JSON.parse(localStorage.getItem('tapTaskDoneAccount') || '{}'); delete account.profilePhoto; localStorage.setItem('tapTaskDoneAccount', JSON.stringify(account)); } catch { }
            } catch (error) { message.textContent = error.message; }
            finally { input.disabled = false; remove.disabled = false; input.value = ''; }
        }
        if (!input || !remove || !message) return;
        const crop = document.getElementById('profilePhotoCrop');
        const canvas = crop.querySelector('canvas');
        const context = canvas.getContext('2d');
        const sliders = [...crop.querySelectorAll('input[type="range"]')];
        const [zoom, horizontal, vertical] = sliders;
        const save = crop.querySelector('[data-save-crop]');
        let source;
        function draw() {
            const size = canvas.width;
            const scale = size / Math.min(source.naturalWidth, source.naturalHeight) * Number(zoom.value);
            const width = source.naturalWidth * scale;
            const height = source.naturalHeight * scale;
            context.fillStyle = '#fff';
            context.fillRect(0, 0, size, size);
            context.drawImage(source, (size - width) * horizontal.value / 100,
                (size - height) * vertical.value / 100, width, height);
        }
        sliders.forEach(slider => slider.addEventListener('input', draw));
        crop.addEventListener('close', () => { input.value = ''; source = null; });
        save.addEventListener('click', () => {
            save.disabled = true;
            canvas.toBlob(blob => {
                save.disabled = false;
                if (!crop.open) return;
                if (!blob) { crop.close(); message.textContent = 'Could not crop this photo. Try another image.'; return; }
                crop.close();
                change('Upload', new File([blob], 'profile-photo.jpg', { type: 'image/jpeg' }));
            }, 'image/jpeg', 0.9);
        });
        input.addEventListener('change', async () => {
            const file = input.files?.[0];
            if (!file) return;
            if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024) {
                message.textContent = 'Choose a JPG, PNG or WEBP image up to 5 MB.'; input.value = ''; return;
            }
            input.disabled = true;
            remove.disabled = true;
            const url = URL.createObjectURL(file);
            try {
                source = new Image();
                source.src = url;
                await source.decode();
                sliders.forEach(slider => { slider.value = slider.defaultValue; });
                draw();
                message.textContent = '';
                crop.showModal();
            } catch {
                input.value = '';
                message.textContent = 'This image could not be opened. Try another JPG, PNG or WEBP.';
            } finally {
                URL.revokeObjectURL(url);
                input.disabled = false;
                remove.disabled = false;
            }
        });
        remove?.addEventListener('click', () => change('Remove'));
    }
    window.addEventListener('account-header-ready', paint);
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', ready); else ready();
})();
