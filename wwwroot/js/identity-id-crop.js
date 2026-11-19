(() => {
    const el = id => document.getElementById(id);
    const editor = el('idCropEditor'), file = el('governmentId');
    if (!editor || !file) return;
    const preview = el('idCropPreview'), controls = el('idCropControls'), message = el('idCropMessage');
    const edges = ['Left', 'Right', 'Top', 'Bottom'];
    let source, selected, version = 0, applying = false;
    function bounds() {
        const trim = edge => Number(el('crop' + edge).value) / 100;
        return [source.naturalWidth * trim('Left'), source.naturalHeight * trim('Top'),
            source.naturalWidth * (1 - trim('Left') - trim('Right')), source.naturalHeight * (1 - trim('Top') - trim('Bottom'))];
    }
    function draw() {
        if (!source) return;
        const [x,y,w,h] = bounds(), scale = Math.min(1, 480 / w, 320 / h);
        preview.width = Math.max(1, Math.round(w * scale)); preview.height = Math.max(1, Math.round(h * scale));
        preview.getContext('2d').drawImage(source, x, y, w, h, 0, 0, preview.width, preview.height);
        edges.forEach(edge => { el('crop' + edge + 'Value').value = el('crop' + edge).value + '%'; });
    }
    async function load() {
        if (applying) return;
        const next = file.files[0]; if (selected === next && source) return;
        const current = ++version; source = null; selected = next; controls.hidden = preview.hidden = true;
        if (!next || !/^image\/(jpeg|png)$/.test(next.type)) { message.textContent = 'Crop is available for JPG and PNG photos. PDFs use their first page.'; return; }
        if (next.size > 5 * 1024 * 1024) { message.textContent = 'Choose a photo no larger than 5 MB.'; return; }
        const url = URL.createObjectURL(next);
        try {
            const image = new Image(); image.src = url; await image.decode();
            if (current !== version || file.files[0] !== next) return;
            source = image; edges.forEach(edge => { el('crop' + edge).value = '0'; });
            controls.hidden = preview.hidden = false; draw(); message.textContent = 'Adjust the trims, then use the cropped photo.';
        } catch { message.textContent = 'This photo could not be opened. Choose another JPG or PNG.'; }
        finally { URL.revokeObjectURL(url); }
    }
    editor.addEventListener('toggle', () => { if (editor.open) load(); });
    file.addEventListener('change', () => { ++version; source = null; selected = null; if (editor.open) load(); });
    edges.forEach(edge => el('crop' + edge).addEventListener('input', draw));
    el('resetIdCrop').addEventListener('click', () => { edges.forEach(edge => { el('crop' + edge).value = '0'; }); draw(); });
    el('applyIdCrop').addEventListener('click', async () => {
        if (!source || applying) return;
        applying = true; const original = file.files[0], current = version;
        el('applyIdCrop').disabled = true;
        try {
            const [x,y,w,h] = bounds(), scale = Math.min(1, 1800 / w, 1800 / h), canvas = document.createElement('canvas');
            canvas.width = Math.max(1, Math.round(w * scale)); canvas.height = Math.max(1, Math.round(h * scale));
            const ctx = canvas.getContext('2d'); ctx.fillStyle = 'white'; ctx.fillRect(0,0,canvas.width,canvas.height);
            ctx.drawImage(source, x,y,w,h,0,0,canvas.width,canvas.height);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .92));
            if (version !== current || file.files[0] !== original) return;
            if (!blob || blob.size > 5 * 1024 * 1024) throw new Error();
            const transfer = new DataTransfer(); transfer.items.add(new File([blob], 'cropped-id.jpg', {type:'image/jpeg'})); file.files = transfer.files;
            // Existing ID-change handlers clear OCR receipts and camera evidence.
            file.dispatchEvent(new Event('input', {bubbles:true}));
            source = null; selected = null; ++version; controls.hidden = preview.hidden = true;
            message.textContent = 'Cropped photo selected. Open camera to check it.'; editor.open = false;
        } catch { message.textContent = 'Could not crop this photo. Try again or use the original.'; }
        finally { applying = false; el('applyIdCrop').disabled = false; }
    });
})();
