(() => {
    const form = document.getElementById('identityForm');
    if (!form) return;
    const el = id => document.getElementById(id);
    const video = el('selfieVideo'), preview = el('selfiePreview'), file = el('selfieFile');
    const review = el('identityReview'), message = el('selfieMessage');
    const consent = el("selfieConsent"), capture = el("captureSelfie");
    let guideReady = false;
    const guide = new window.IdentityFaceGuide(video, JSON.parse(el('captureSteps').value), value => { guideReady = value; updateCapture(); });
    let encoding = false;
    function canCapture() {
        return Boolean(stream?.active && video.videoWidth && consent.checked && guideReady && guide.complete && !encoding);
    }
    function updateCapture() { capture.disabled = !canCapture(); }
    video.addEventListener("loadeddata", updateCapture);
    let stream, selfieUrl, idUrl, reviewed = false, opening = false;
    let cameraVersion = 0;
    function stop() {
        cameraVersion++;
        guide.stop();
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
        video.srcObject = null;
        video.hidden = true;
        capture.hidden = true;
        updateCapture();
        el('selfieCamera').hidden = preview.hidden;
        el('openCamera').hidden = !preview.hidden;
        el('retakeSelfie').hidden = preview.hidden;
    }
    function invalidate() { reviewed = false; review.hidden = true; el('reviewIdentity').hidden = false; }
    async function open() {
        if (opening) return;
        if (!el('selfieConsent').checked) { message.textContent = 'Please agree to identity review before opening the camera.'; return; }
        stop();
        opening = true;
        const version = cameraVersion;
        try {
            message.textContent = 'Loading automatic face, glasses, mask and hand checks on your device. The first load may take a moment.';
            preview.hidden = true; file.value = ''; invalidate(); guide.reset();
            await guide.load();
            if (version !== cameraVersion || !consent.checked || document.hidden) return;
            if (!navigator.mediaDevices?.getUserMedia) throw new Error('unsupported');
            const next = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user', width: { ideal: 960 }, height: { ideal: 720 } }, audio: false });
            if (version !== cameraVersion || !consent.checked || document.hidden) { next.getTracks().forEach(t => t.stop()); return; }
            stream = next; video.srcObject = stream; video.hidden = false; el('selfieCamera').hidden = false; await video.play();
            if (version !== cameraVersion) return;
            if (!next.active || !consent.checked || document.hidden) { stop(); return; }
            next.getVideoTracks().forEach(track => track.addEventListener("ended", () => {
                if (stream !== next) return;
                stop();
                message.textContent = "Camera disconnected. Open the camera to try again.";
            }, { once: true }));
            preview.hidden = true; file.value = ''; invalidate();
            capture.hidden = false; updateCapture(); el('openCamera').hidden = true; el('retakeSelfie').hidden = true;
            guide.start();
            message.textContent = 'Remove glasses and any face covering, keep your hands away, and follow each prompt. Capture unlocks after automatic checks pass.';
        } catch {
            if (version !== cameraVersion) return;
            stop();
            message.textContent = 'Camera or face tracking could not start. Check camera permission, use HTTPS (or localhost), and try a recent Chrome, Edge or Safari browser.'; }
        finally { opening = false; }
    }
    el('openCamera').addEventListener('click', open);
    el('retakeSelfie').addEventListener('click', open);
    el('restartFaceGuide').addEventListener('click', () => {
        if (!stream?.active || preview.hidden === false) return;
        guide.start(); invalidate(); message.textContent = 'Movements restarted. Follow the new prompts.';
    });
    el('captureSelfie').addEventListener('click', async () => {
        if (!canCapture()) return;
        const capturedStream = stream;
        encoding = true;
        updateCapture();
        const version = cameraVersion;
        message.textContent = 'Checking this selfie for glasses, face covering and visible eyes…';
        const blob = await guide.captureSelfie();
        encoding = false;
        updateCapture();
        if (stream !== capturedStream || cameraVersion !== version || !consent.checked) return;
        if (!blob) { message.textContent = 'Selfie blocked. Follow the camera instruction and complete the face checks again.'; return; }
        const transfer = new DataTransfer(); transfer.items.add(new File([blob], 'selfie.jpg', { type: 'image/jpeg' })); file.files = transfer.files;
        if (selfieUrl) URL.revokeObjectURL(selfieUrl);
        selfieUrl = URL.createObjectURL(blob); preview.src = selfieUrl; preview.hidden = false;
        stop(); invalidate(); el('retakeSelfie').hidden = false;
        message.textContent = 'Selfie captured. Retake if unclear, or review your submission.';
    });
    form.addEventListener('input', invalidate);
    form.addEventListener('change', invalidate);
    el('selfieConsent').addEventListener('change', () => {
        if (!consent.checked) { preview.hidden = true; file.value = ''; stop(); guide.reset(); }
    });
    el('reviewIdentity').addEventListener('click', () => {
        if (!form.reportValidity()) return;
        if (!file.files.length || !guide.complete) { message.textContent = 'Complete the guided movements and capture a selfie before reviewing.'; return; }
        const id = el('governmentId').files[0];
        if ([...form.querySelectorAll('input[type=file]')].some(i => [...i.files].some(f => f.size > 5 * 1024 * 1024))) { message.textContent = 'Each attachment must be no larger than 5 MB.'; return; }
        if (idUrl) URL.revokeObjectURL(idUrl);
        idUrl = URL.createObjectURL(id);
        const container = el('idPreview'); container.replaceChildren();
        if (/image\/(jpeg|png)/.test(id.type)) { const img = document.createElement('img'); img.src = idUrl; img.alt = 'Government ID selected for submission'; container.append(img); }
        else { const link = document.createElement('a'); link.href = idUrl; link.target = '_blank'; link.rel = 'noopener'; link.textContent = 'Open selected government ID: ' + id.name; container.append(link); }
        el('reviewSummary').textContent = el('documentType').value + ' · ID ending in ' + el('idNumber').value.trim().replace(/[ -]/g, '').slice(-4) + ' · 5 movement photos and selfie captured · Manual admin review';
        reviewed = true; review.hidden = false; el('reviewIdentity').hidden = true; review.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    });
    el('editIdentity').addEventListener('click', invalidate);
    form.addEventListener('submit', event => {
        if (!reviewed || !file.files.length || !guide.complete || !el('selfieConsent').checked) { event.preventDefault(); message.textContent = 'Complete the guided movements, capture your selfie and review the submission first.'; return; }
        stop(); el('submitIdentity').disabled = true; el('submitIdentity').textContent = 'Submitting…';
    });
    window.addEventListener('pagehide', stop);
    document.addEventListener('visibilitychange', () => { if (document.hidden && (stream || opening)) {
            stop();
            message.textContent = 'Camera paused. Open the camera again to continue.';
        } });
})();
