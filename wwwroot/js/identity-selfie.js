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
    let checkingId = false, checkedFile, checkedNumber, checkedType;
    const idNumber = el('idNumber'), documentType = el('documentType'), governmentId = el('governmentId');
    function updateNumberField() {
        const option = documentType.selectedOptions[0];
        const label = option?.dataset.numberLabel || 'ID number';
        el('idNumberLabel').textContent = label;
        idNumber.placeholder = `Enter your ${label}`;
        el('idNumberHelp').textContent = option?.dataset.numberHelp || 'Select your ID type to see which number to enter.';
    }
    documentType.addEventListener('change', updateNumberField);
    updateNumberField();
    function idChecked() {
        return !!el('idCheckToken').value && checkedFile === governmentId.files[0] && checkedNumber === idNumber.value && checkedType === documentType.value;
    }
    async function checkId() {
        if (checkingId) return false;
        if (idChecked()) return true;
        if (!documentType.reportValidity() || !idNumber.reportValidity() || !governmentId.reportValidity() || !consent.reportValidity()) return false;
        const selectedFile = governmentId.files[0], number = idNumber.value, type = documentType.value;
        checkingId = true; el('openCamera').disabled = true;
        el('openCamera').textContent = 'Checking ID…';
        el('idCheckMessage').textContent = 'Reading your uploaded ID and checking its full number…';
        el('idCheckMessage').dataset.state = 'loading';
        el('idCheckToken').value = '';
        const controller = new AbortController();
        const checkTimeout = setTimeout(() => controller.abort(), 35000);
        try {
            const data = new FormData();
            data.set('__RequestVerificationToken', form.elements.namedItem('__RequestVerificationToken').value);
            data.set('governmentId', selectedFile); data.set('idNumber', number); data.set('documentType', type); data.set('consent', 'true');
            const url = new URL(form.action); url.searchParams.set('handler', 'CheckId');
            const response = await fetch(url, { method: 'POST', body: data, credentials: 'same-origin', signal: controller.signal });
            if (!response.headers.get('content-type')?.includes('application/json')) {
                const messages = {
                    400: 'Your page session expired. Reload the page and try again.',
                    401: 'Your sign-in session expired. Sign in again.',
                    403: 'Your session cannot check this ID. Reload the page or sign in again.',
                    413: 'The upload is too large. Choose an ID file no larger than 5 MB.',
                    429: 'Too many requests. Wait a moment and try again.',
                    502: 'The ID-checking server is unavailable. Wait a moment and try again.',
                    503: 'The ID-checking server is busy. Wait a moment and try again.',
                    504: 'The server took too long to read your ID. Try a smaller, clear JPG or PNG.'
                };
                const error = new Error();
                error.userMessage = response.redirected ? 'Your sign-in session may have expired. Reload the page and sign in again.'
                    : messages[response.status] || 'The server could not complete the ID check. Reload the page and try again.';
                throw error;
            }
            const result = await response.json();
            if (selectedFile !== governmentId.files[0] || number !== idNumber.value || type !== documentType.value || !consent.checked) return false;
            el('idCheckMessage').textContent = result.message;
            el('idCheckMessage').dataset.state = response.ok && result.success ? 'success' : 'error';
            if (!response.ok || !result.success || !result.token) return false;
            checkedFile = selectedFile; checkedNumber = number; checkedType = type; el('idCheckToken').value = result.token;
            return true;
        } catch (error) {
            el('idCheckMessage').textContent = error.name === 'AbortError' ? 'ID reading took too long. Try a smaller, clear JPG or PNG and open the camera again.'
                : error.userMessage || 'Could not reach the ID-checking server. Check your connection and try again.';
            el('idCheckMessage').dataset.state = 'error'; return false;
        }
        finally { clearTimeout(checkTimeout); checkingId = false; el('openCamera').disabled = false; el('openCamera').textContent = 'Open camera'; }
    }
    [idNumber, documentType, governmentId].forEach(input => input.addEventListener('input', () => {
        el('idCheckToken').value = ''; checkedFile = null;
        preview.hidden = true; file.value = ''; stop(); guide.reset(); invalidate();
        el('idCheckMessage').textContent = 'ID details changed. Open camera to check them again.';
        el('idCheckMessage').dataset.state = 'neutral';
    }));
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
            if (!await checkId()) return;
            if (version !== cameraVersion || !consent.checked || document.hidden) return;
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
            message.textContent = 'Keep your hands away from your face and follow the circle.';
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
