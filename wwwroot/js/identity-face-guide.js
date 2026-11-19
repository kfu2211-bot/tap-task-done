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
    function handCoversFace(hands, face) {
        const xs = oval.map(i => face[i].x), ys = oval.map(i => face[i].y);
        const x1 = Math.min(...xs), x2 = Math.max(...xs), y1 = Math.min(...ys), y2 = Math.max(...ys);
        return hands.some(hand => {
            const hx = hand.map(p => p.x), hy = hand.map(p => p.y);
            return Math.max(...hx) >= x1 && Math.min(...hx) <= x2 && Math.max(...hy) >= y1 && Math.min(...hy) <= y2;
        });
    }
    class FaceGuide {
        constructor(video, steps, onReady) {
            this.video = video; this.steps = steps; this.onReady = onReady;
            this.canvas = document.createElement('canvas');
            this.light = document.createElement('canvas'); this.light.width = this.light.height = 32;
            this.lightContext = this.light.getContext('2d', { willReadFrequently: true });
            this.visibility = new window.IdentityFaceVisibility();
            this.scan = document.createElement('canvas');
        }
        async load() {
            if (this.detector && this.hands && this.visibility.session) return;
            const { FaceLandmarker, HandLandmarker, FilesetResolver } = await import('/vendor/mediapipe/vision_bundle.mjs');
            const files = await FilesetResolver.forVisionTasks('/vendor/mediapipe');
            this.detector ??= await FaceLandmarker.createFromOptions(files, {
                baseOptions: { modelAssetPath: '/vendor/mediapipe/face_landmarker.task', delegate: 'CPU' },
                runningMode: 'VIDEO', numFaces: 2, minFaceDetectionConfidence: .7,
                minFacePresenceConfidence: .7, minTrackingConfidence: .7
            });
            this.hands ??= await HandLandmarker.createFromOptions(files, {
                baseOptions: { modelAssetPath: '/vendor/mediapipe/hand_landmarker.task', delegate: 'CPU' },
                runningMode: 'VIDEO', numHands: 2, minHandDetectionConfidence: .5, minHandPresenceConfidence: .5
            });
            await this.visibility.load();
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
            this.canvas.width = this.scan.width;
            this.canvas.height = this.scan.height;
            this.canvas.getContext('2d').drawImage(this.scan, 0, 0);
            return new Promise(resolve => this.canvas.toBlob(resolve, 'image/jpeg', .85));
        }
        brightEnough() {
            const w = this.scan.width, h = this.scan.height, side = Math.min(w, h) * .5;
            this.lightContext.drawImage(this.scan, (w - side) / 2, (h - side) / 2, side, side, 0, 0, 32, 32);
            const pixels = this.lightContext.getImageData(0, 0, 32, 32).data;
            let total = 0;
            for (let i = 0; i < pixels.length; i += 4) total += .2126 * pixels[i] + .7152 * pixels[i + 1] + .0722 * pixels[i + 2];
            const average = total / (pixels.length / 4);
            return average > 35 && average < 235;
        }
        async inspect() {
            const scale = Math.min(1, 640 / Math.max(this.video.videoWidth, this.video.videoHeight));
            this.scan.width = Math.round(this.video.videoWidth * scale);
            this.scan.height = Math.round(this.video.videoHeight * scale);
            this.scan.getContext('2d').drawImage(this.video, 0, 0, this.scan.width, this.scan.height);
            const now = performance.now();
            const result = this.detector.detectForVideo(this.scan, now);
            if (result.faceLandmarks.length !== 1) return { error: result.faceLandmarks.length ? 'Only one person should be in the camera.' : 'No clear face detected. Uncover your whole face and look at the camera.' };
            const face = result.faceLandmarks[0];
            const p = pose(face, this.scan.width, this.scan.height);
            if (!p) return { error: 'Keep your whole face inside the circle. Adjust your distance.' };
            if (!this.brightEnough()) return { error: 'Use even lighting. Avoid dark rooms and bright backlight.' };
            if (handCoversFace(this.hands.detectForVideo(this.scan, now).landmarks, face)) return { error: 'Move your hands away from your face. Keep your eyes, nose and mouth visible.' };
            return { error: await this.visibility.check(this.scan, face), p };
        }
        block(error, resetProgress = !error.startsWith('Keep both eyes open')) {
            this.onReady(false); this.heldSince = null;
            // Do not keep completed evidence when a covering or lost face appears.
            if (resetProgress) {
                this.index = 0; this.frames = [];
                document.getElementById('captureFiles').replaceChildren();
            }
            this.update(error);
        }
        async captureSelfie() {
            if (!this.running || this.captureRequested || !this.complete) return null;
            const version = this.version;
            this.captureRequested = true; this.onReady(false);
            if (this.saving) await new Promise(resolve => { this.onIdle = resolve; });
            if (!this.running || version !== this.version || !this.complete) { this.captureRequested = false; return null; }
            this.saving = true; this.onReady(false);
            try {
                const { error, p } = await this.inspect();
                if (!this.running || version !== this.version) return null;
                if (error) { this.block(error); return null; }
                if (!matches('front', p)) { this.update('Look straight ahead before capturing.'); return null; }
                // Encode the exact frame inspected above, never a later unchecked frame.
                return await this.snapshot();
            } catch {
                if (version === this.version) this.block('Automatic face checks failed. Restart movements to try again.');
                return null;
            } finally { if (version === this.version) this.saving = false; this.captureRequested = false; }
        }
        async tick() {
            if (!this.running) return;
            this.animation = requestAnimationFrame(() => this.tick());
            if (this.saving || this.captureRequested || this.video.readyState < 2 || this.lastVideoTime === this.video.currentTime) return;
            if (this.lastDetection && performance.now() - this.lastDetection < 100) return;
            this.lastDetection = performance.now();
            this.lastVideoTime = this.video.currentTime;
            if (performance.now() - this.started > 120000) {
                this.stop(); this.update('Session paused. Choose Restart movements to try again.'); return;
            }
            const checkVersion = this.version;
            try {
                this.saving = true;
                const version = this.version;
                const { error, p } = await this.inspect();
                if (!this.running || version !== this.version) return;
                if (error) { this.block(error); return; }
                if (this.index === this.steps.length) {
                    this.onReady(matches('front', p));
                    this.update(matches('front', p) ? 'Face checks passed. Look forward and capture your selfie.' : 'Look straight ahead to enable Capture selfie.');
                    return;
                }
                const step = this.steps[this.index];
                const instructions = { front: 'Look straight ahead', left: 'Turn your face toward the dot on the left', right: 'Turn your face toward the dot on the right', tilt: 'Gently tilt your head sideways toward either shoulder' };
                this.onReady(false);
                if (!matches(step, p)) { this.heldSince = null; this.update(instructions[step]); return; }
                this.heldSince ??= performance.now();
                this.update(`${instructions[step]} · Hold still`);
                if (performance.now() - this.heldSince < 700) return;
                const blob = await this.snapshot();
                if (!this.running || version !== this.version) return;
                if (!blob) throw new Error('capture');
                const transfer = new DataTransfer();
                transfer.items.add(new File([blob], `movement-${this.index}.jpg`, { type: 'image/jpeg' }));
                const input = document.createElement('input');
                input.type = 'file'; input.name = `captureStep${this.index}`; input.files = transfer.files;
                document.getElementById('captureFiles').append(input);
                this.frames.push(blob); this.index++; this.heldSince = null;
            } catch { this.stop(); this.update('Face tracking stopped. Restart movements or try another browser.'); }
            finally {
                if (checkVersion === this.version) this.saving = false;
                this.onIdle?.(); this.onIdle = null;
            }
        }
        get complete() { return this.index === this.steps.length && this.frames.length === this.steps.length; }
    }
    if (typeof module !== 'undefined') module.exports = { pose, matches, handCoversFace, FaceGuide };
    else window.IdentityFaceGuide = FaceGuide;
})();
