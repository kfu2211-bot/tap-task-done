(() => {
    const scan = document.getElementById('scanGovernmentId');
    if (!scan) return;
    const el = id => document.getElementById(id);
    const type = el('documentType'), input = el('governmentId'), number = el('idNumber');
    const camera = el('idCamera'), video = el('idVideo'), capture = el('captureGovernmentId');
    const preview = el('selectedIdPreview'), message = el('idScanMessage');
    const patterns = {
        'Philippine National ID': /^(?:PCN|PHILSYS CARD NUMBER)[ :#-]*(\d{4}[ -]\d{4}[ -]\d{4}[ -]\d{4})$/im,
        'Philippine Passport': /^PASSPORT (?:NO\.?|NUMBER)[ :#-]*([A-Z][A-Z0-9]{6,9})$/im,
        "Driver's License": /^LICEN[CS]E (?:NO\.?|NUMBER)[ :#-]*([A-Z]\d{2}-?\d{2}-?\d{6})$/im,
        'UMID': /^(?:CRN|COMMON REFERENCE NUMBER)[ :#-]*(\d{4}-?\d{7}-?\d)$/im,
        'PRC ID': /^(?:REGISTRATION|LICEN[CS]E) (?:NO\.?|NUMBER)[ :#-]*(\d{5,10})$/im,
        'Postal ID': /^(?:PRN|POSTAL REFERENCE NUMBER)[ :#-]*([A-Z0-9][A-Z0-9-]{6,24})$/im
    };
    let stream, request = 0, reading = 0, previewUrl, autofilled;

    function closeCamera() {
        request++;
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
        video.srcObject = null;
        camera.hidden = true;
        capture.disabled = true;
        scan.disabled = false;
    }

    async function inspect() {
        const version = ++reading;
        if (number.value === autofilled) {
            number.value = '';
            number.dispatchEvent(new Event('input', { bubbles: true }));
        }
        autofilled = undefined;
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        preview.replaceChildren();
        preview.hidden = true;
        const file = input.files[0];
        message.textContent = '';
        if (!file) return;
        const isImage = /\.(?:jpe?g|png)$/i.test(file.name);
        if ((!isImage && !/\.pdf$/i.test(file.name)) || file.size > 5 * 1024 * 1024) {
            input.value = '';
            message.textContent = 'Choose a JPG, PNG, or PDF no larger than 5 MB.';
            return;
        }
        const label = document.createElement('p');
        label.textContent = file.name;
        preview.append(label);
        preview.hidden = false;
        if (!isImage) {
            message.textContent = 'PDF selected. Enter the ID number shown on your card.';
            return;
        }
        previewUrl = URL.createObjectURL(file);
        const image = document.createElement('img');
        image.alt = 'Selected government ID';
        image.src = previewUrl;
        preview.prepend(image);
        if (!type.value || number.value || !window.TextDetector) {
            message.textContent = !type.value ? 'Select the ID type to continue.' :
                'Check your ID photo and enter or confirm the ID number.';
            return;
        }
        message.textContent = 'Reading the ID number…';
        try {
            await image.decode();
            const blocks = await new window.TextDetector().detect(image);
            if (version !== reading || number.value) return;
            const text = blocks.map(block => block.rawValue).join('\n')
                .replace(/\r/g, '').replace(/([^\n])\n(?=[A-Z0-9])/g, '$1\n').trim();
            const pattern = patterns[type.value];
            const candidates = new Set();
            const lines = text.split('\n').map(line => line.trim());
            lines.forEach((line, index) => {
                const match = line.match(pattern) || (line + ' ' + (lines[index + 1] || '')).trim().match(pattern);
                if (match) candidates.add(match[1]);
            });
            if (candidates.size === 1) {
                number.value = autofilled = [...candidates][0];
                number.dispatchEvent(new Event('input', { bubbles: true }));
                message.textContent = 'ID number filled in. Check it against your card before continuing.';
                return;
            }
        } catch {
            if (version !== reading || number.value) return;
        }
        message.textContent = 'Enter the ID number manually or retake the photo in better lighting.';
    }

    scan.addEventListener('click', async () => {
        if (!type.reportValidity()) return;
        const current = ++request;
        scan.disabled = true;
        camera.hidden = false;
        message.textContent = 'Allow camera access when prompted.';
        try {
            const next = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 } },
                audio: false
            });
            if (current !== request || document.hidden) {
                next.getTracks().forEach(track => track.stop());
                return;
            }
            stream = next;
            video.srcObject = stream;
            await video.play();
            if (current !== request) return;
            capture.disabled = false;
            message.textContent = 'Camera ready.';
        } catch {
            if (current !== request) return;
            closeCamera();
            message.textContent = 'Camera unavailable. Check camera permission or upload your ID.';
            scan.focus();
        }
    });

    capture.addEventListener('click', async () => {
        if (!stream || !video.videoWidth) return;
        const current = request;
        capture.disabled = true;
        try {
            const canvas = document.createElement('canvas');
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            canvas.getContext('2d').drawImage(video, 0, 0);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .88));
            if (current !== request) return;
            if (!blob) throw new Error('Capture failed');
            const transfer = new DataTransfer();
            transfer.items.add(new File([blob], 'government-id.jpg', { type: 'image/jpeg' }));
            input.files = transfer.files;
            closeCamera();
            scan.focus();
            input.dispatchEvent(new Event('change', { bubbles: true }));
        } catch {
            if (current !== request) return;
            capture.disabled = false;
            message.textContent = 'Could not capture the ID. Try again or upload an ID file.';
        }
    });
    el('cancelIdCamera').addEventListener('click', () => {
        closeCamera();
        message.textContent = 'Camera closed.';
        scan.focus();
    });
    input.addEventListener('change', () => {
        closeCamera();
        scan.textContent = 'Scan ID';
        if (input.files[0]?.name === 'government-id.jpg') scan.textContent = 'Retake ID photo';
        void inspect();
    });
    type.addEventListener('change', () => void inspect());
    number.addEventListener('input', () => {
        if (number.value !== autofilled) {
            autofilled = undefined;
            if (message.textContent === 'Reading the ID number…') message.textContent = 'Check the number against your card.';
            reading++;
        }
    });
    ['openCamera', 'retakeSelfie'].forEach(id => el(id).addEventListener('click', closeCamera));
    window.addEventListener('pagehide', closeCamera);
    document.addEventListener('visibilitychange', () => { if (document.hidden) closeCamera(); });
})();
