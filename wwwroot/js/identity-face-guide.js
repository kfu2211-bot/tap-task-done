/* MediaPipe runs locally. Pose checks guide capture; they do not authenticate identity. */
(() => {
    const oval = [10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400, 377, 152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103, 67, 109];
    function pose(points, width, height) {
        const side = Math.min(width, height);
        const project = p => ({ x: (p.x * width - (width - side) / 2) / side, y: (p.y * height - (height - side) / 2) / side });
        const face = oval.map(i => project(points[i]));
        if (face.some(p => Math.hypot(p.x - .5, p.y - .5) > .47)) return null;
        const xs = face.map(p => p.x), ys = face.map(p => p.y);
        const span = Math.max(...xs) - Math.min(...xs);
        if (span < .27 || span > .78 || Math.abs((Math.min(...ys) + Math.max(...ys)) / 2 - .5) > .15) return null;
        const a = project(points[33]), b = project(points[263]), nose = project(points[1]);
        const distance = Math.hypot(b.x - a.x, b.y - a.y);
        if (distance < .08) return null;
        return { yaw: (nose.x - (a.x + b.x) / 2) / distance, roll: Math.atan2(b.y - a.y, b.x - a.x) };
    }
    function matches(step, p) {
        if (!p) return false;
        if (step === 'front') return Math.abs(p.yaw) < .18 && Math.abs(p.roll) < .13;
        if (step === 'left') return p.yaw > .26 && p.yaw < .85 && Math.abs(p.roll) < .22;
        if (step === 'right') return p.yaw < -.26 && p.yaw > -.85 && Math.abs(p.roll) < .22;
        return step === 'tilt' && Math.abs(p.roll) > .22 && Math.abs(p.roll) < .5 && Math.abs(p.yaw) < .2;
    }
    class FaceGuide {
        constructor(video, steps, onReady) {
            this.video = video; this.steps = steps; this.onReady = onReady;
            this.canvas = document.createElement('canvas');
            this.light = document.createElement('canvas'); this.light.width = this.light.height = 32;
            this.lightContext = this.light.getContext('2d', { willReadFrequently: true });
        }
        async load() {
            if (this.detector) return;
            const { FaceLandmarker, FilesetResolver } = await import('/vendor/mediapipe/vision_bundle.mjs');
            const files = await FilesetResolver.forVisionTasks('/vendor/mediapipe');
            this.detector = await FaceLandmarker.createFromOptions(files, {
                baseOptions: { modelAssetPath: '/vendor/mediapipe/face_landmarker.task', delegate: 'CPU' },
                runningMode: 'VIDEO', numFaces: 2, minFaceDetectionConfidence: .7,
                minFacePresenceConfidence: .7, minTrackingConfidence: .7
            });
        }
        reset() {
            this.stop(); this.index = 0; this.frames = []; this.heldSince = null;
            this.started = performance.now(); this.lastVideoTime = -1; this.saving = false;
            document.getElementById('captureFiles').replaceChildren();
            this.update('Center your face and look straight ahead.');
        }
        start() { this.reset(); this.running = true; this.tick(); }
        stop() { this.running = false; this.version = (this.version || 0) + 1; cancelAnimationFrame(this.animation); this.onReady(false); }
        update(text) {
            const instruction = document.getElementById('faceInstruction');
            if (instruction.textContent !== text) instruction.textContent = text;
            document.getElementById('faceProgress').value = this.index;
            document.getElementById('faceStepCount').textContent = `${this.index} of ${this.steps.length} movements completed`;
            document.getElementById('faceTarget').dataset.pose = this.steps[this.index] || 'front';
        }
        async snapshot() {
            const scale = Math.min(1, 640 / Math.max(this.video.videoWidth, this.video.videoHeight));
            this.canvas.width = Math.round(this.video.videoWidth * scale);
            this.canvas.height = Math.round(this.video.videoHeight * scale);
            this.canvas.getContext('2d').drawImage(this.video, 0, 0, this.canvas.width, this.canvas.height);
            return new Promise(resolve => this.canvas.toBlob(resolve, 'image/jpeg', .85));
        }
        brightEnough() {
            const w = this.video.videoWidth, h = this.video.videoHeight, side = Math.min(w, h) * .5;
            this.lightContext.drawImage(this.video, (w - side) / 2, (h - side) / 2, side, side, 0, 0, 32, 32);
            const pixels = this.lightContext.getImageData(0, 0, 32, 32).data;
            let total = 0;
            for (let i = 0; i < pixels.length; i += 4) total += .2126 * pixels[i] + .7152 * pixels[i + 1] + .0722 * pixels[i + 2];
            const average = total / (pixels.length / 4);
            return average > 35 && average < 235;
        }
        tick() {
            if (!this.running) return;
            this.animation = requestAnimationFrame(() => this.tick());
            if (this.saving || this.video.readyState < 2 || this.lastVideoTime === this.video.currentTime) return;
            if (this.lastDetection && performance.now() - this.lastDetection < 100) return;
            this.lastDetection = performance.now();
            this.lastVideoTime = this.video.currentTime;
            if (performance.now() - this.started > 120000) {
                this.stop(); this.update('Session paused. Choose Restart movements to try again.'); return;
            }
            try {
                const result = this.detector.detectForVideo(this.video, performance.now());
                let error = '';
                if (result.faceLandmarks.length !== 1) error = result.faceLandmarks.length ? 'Only one person should be in the camera.' : 'No face detected. Face the camera in good light.';
                const p = !error && pose(result.faceLandmarks[0], this.video.videoWidth, this.video.videoHeight);
                if (!error && !p) error = 'Keep your whole face inside the circle. Adjust your distance.';
                if (!error && !this.brightEnough()) error = 'Use even lighting. Avoid dark rooms and bright backlight.';
                if (error) { this.heldSince = null; this.onReady(false); this.update(error); return; }
                if (this.index === this.steps.length) {
                    this.onReady(matches('front', p));
                    this.update(matches('front', p) ? 'Movements complete. Look forward and capture your selfie.' : 'Look straight ahead to enable Capture selfie.');
                    return;
                }
                const step = this.steps[this.index];
                const instructions = { front: 'Look straight ahead', left: 'Turn your face toward the dot on the left', right: 'Turn your face toward the dot on the right', tilt: 'Gently tilt your head sideways toward either shoulder' };
                this.onReady(false);
                if (!matches(step, p)) { this.heldSince = null; this.update(instructions[step]); return; }
                this.heldSince ??= performance.now();
                this.update(`${instructions[step]} · Hold still`);
                if (performance.now() - this.heldSince < 700) return;
                this.saving = true;
                const version = this.version;
                this.snapshot().then(blob => {
                    if (!this.running || version !== this.version) return;
                    if (!blob) throw new Error('capture');
                    const transfer = new DataTransfer();
                    transfer.items.add(new File([blob], `movement-${this.index}.jpg`, { type: 'image/jpeg' }));
                    const input = document.createElement('input');
                    input.type = 'file'; input.name = `captureStep${this.index}`; input.files = transfer.files;
                    document.getElementById('captureFiles').append(input);
                    this.frames.push(blob); this.index++; this.heldSince = null; this.saving = false;
                }).catch(() => { if (version === this.version) { this.stop(); this.update('Capture failed. Restart movements to try again.'); } });
            } catch { this.stop(); this.update('Face tracking stopped. Restart movements or try another browser.'); }
        }
        get complete() { return this.index === this.steps.length && this.frames.length === this.steps.length; }
    }
    if (typeof module !== 'undefined') module.exports = { pose, matches };
    else window.IdentityFaceGuide = FaceGuide;
})();
