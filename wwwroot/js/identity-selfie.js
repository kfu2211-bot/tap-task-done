(() => {
    const form = document.getElementById('identityForm');
    if (!form) return;
    const el = id => document.getElementById(id);
    const video = el('selfieVideo'), preview = el('selfiePreview'), file = el('selfieFile');
    const review = el('identityReview'), message = el('selfieMessage');
    let stream, selfieUrl, idUrl, reviewed = false, opening = false;
    function stop() { stream?.getTracks().forEach(t => t.stop()); stream = null; video.srcObject = null; video.hidden = true; el('captureSelfie').hidden = true; }
    function invalidate() { reviewed = false; review.hidden = true; el('reviewIdentity').hidden = false; }
    async function open() {
        if (opening) return;
        if (!el('selfieConsent').checked) { message.textContent = 'Please agree to identity review before opening the camera.'; return; }
        opening = true;
        try {
            stop();
            if (!navigator.mediaDevices?.getUserMedia) throw new Error('unsupported');
            const next = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user', width: { ideal: 960 }, height: { ideal: 720 } }, audio: false });
            if (!el('selfieConsent').checked || document.hidden) { next.getTracks().forEach(t => t.stop()); return; }
            stream = next; video.srcObject = stream; video.hidden = false; await video.play();
            preview.hidden = true; file.value = ''; invalidate();
            el('captureSelfie').hidden = false; el('openCamera').hidden = true; el('retakeSelfie').hidden = true;
            message.textContent = 'Keep your full face in frame, then capture your selfie.';
        } catch { stop(); el('openCamera').hidden = false; message.textContent = 'Camera could not open. Check camera permission and use HTTPS (or localhost). You can also open this page in your phone’s browser.'; }
        finally { opening = false; }
    }
    el('openCamera').addEventListener('click', open);
    el('retakeSelfie').addEventListener('click', open);
    el('captureSelfie').addEventListener('click', async () => {
        if (!stream || !video.videoWidth) return;
        const canvas = document.createElement('canvas');
        const scale = Math.min(1, 1280 / Math.max(video.videoWidth, video.videoHeight));
        canvas.width = Math.round(video.videoWidth * scale); canvas.height = Math.round(video.videoHeight * scale);
        canvas.getContext('2d').drawImage(video, 0, 0, canvas.width, canvas.height);
        const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .88));
        if (!blob || !el('selfieConsent').checked) return;
        const transfer = new DataTransfer(); transfer.items.add(new File([blob], 'selfie.jpg', { type: 'image/jpeg' })); file.files = transfer.files;
        if (selfieUrl) URL.revokeObjectURL(selfieUrl);
        selfieUrl = URL.createObjectURL(blob); preview.src = selfieUrl; preview.hidden = false;
        stop(); invalidate(); el('retakeSelfie').hidden = false;
        message.textContent = 'Selfie captured. Retake if unclear, or review your submission.';
    });
    form.addEventListener('input', invalidate);
    el('selfieConsent').addEventListener('change', () => {
        if (!el('selfieConsent').checked) { stop(); file.value = ''; preview.hidden = true; el('openCamera').hidden = false; el('retakeSelfie').hidden = true; }
    });
    el('reviewIdentity').addEventListener('click', () => {
        if (!form.reportValidity()) return;
        if (!file.files.length) { message.textContent = 'Capture a selfie before reviewing.'; return; }
        const id = el('governmentId').files[0];
        if ([...form.querySelectorAll('input[type=file]')].some(i => [...i.files].some(f => f.size > 5 * 1024 * 1024))) { message.textContent = 'Each attachment must be no larger than 5 MB.'; return; }
        if (idUrl) URL.revokeObjectURL(idUrl);
        idUrl = URL.createObjectURL(id);
        const container = el('idPreview'); container.replaceChildren();
        if (/image\/(jpeg|png)/.test(id.type)) { const img = document.createElement('img'); img.src = idUrl; img.alt = 'Government ID selected for submission'; container.append(img); }
        else { const link = document.createElement('a'); link.href = idUrl; link.target = '_blank'; link.rel = 'noopener'; link.textContent = 'Open selected government ID: ' + id.name; container.append(link); }
        el('reviewSummary').textContent = 'ID ending in ' + el('idNumber').value.trim().slice(-4) + ' · Selfie captured · Manual admin review';
        reviewed = true; review.hidden = false; el('reviewIdentity').hidden = true; review.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    });
    el('editIdentity').addEventListener('click', invalidate);
    form.addEventListener('submit', event => {
        if (!reviewed || !file.files.length || !el('selfieConsent').checked) { event.preventDefault(); message.textContent = 'Capture your selfie and review the submission first.'; return; }
        stop(); el('submitIdentity').disabled = true; el('submitIdentity').textContent = 'Submitting…';
    });
    window.addEventListener('pagehide', stop);
    document.addEventListener('visibilitychange', () => { if (document.hidden) { stop(); if (!file.files.length) el('openCamera').hidden = false; } });
})();
