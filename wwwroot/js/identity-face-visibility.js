/* FaceAttribNet: independent eye-open / eyeglasses / mask / sunglasses probabilities.
 * All inference and camera pixels stay on this device. Predictions are fallible.
 */
(() => {
    function assess(values) {
        if (!values || values.length !== 5 || [...values].some(v => !Number.isFinite(v) || v < 0 || v > 1))
            return 'Face visibility could not be checked. Look forward in even lighting.';
        const [leftEye, rightEye, glasses, mask, sunglasses] = values;
        if (mask >= .5) return 'Remove your face mask and uncover your nose and mouth.';
        if (glasses >= .5 || sunglasses >= .5) return 'Remove your eyeglasses or sunglasses before continuing.';
        // Do not accept an uncertain accessory prediction as a clear face.
        if (Math.max(glasses, mask, sunglasses) >= .2)
            return 'Your face may be covered. Remove glasses and any face covering, then improve the lighting.';
        if (leftEye < .65 || rightEye < .65) return 'Keep both eyes open and visible. Remove anything covering your face.';
        return '';
    }
    class FaceVisibility {
        constructor() {
            this.canvas = document.createElement('canvas'); this.canvas.width = this.canvas.height = 128;
            this.context = this.canvas.getContext('2d', { willReadFrequently: true });
        }
        async load() {
            if (this.session) return;
            this.ort = await import('/vendor/onnxruntime/ort.wasm.min.mjs');
            this.ort.env.wasm.wasmPaths = '/vendor/onnxruntime/';
            this.ort.env.wasm.numThreads = 1;
            this.ort.env.wasm.proxy = false;
            this.session = await this.ort.InferenceSession.create('/vendor/face-attributes/face_attrib_net.onnx', { executionProviders: ['wasm'] });
        }
        async check(source, points) {
            if (!this.session || !points || points.length < 468) throw new Error('Face visibility model is not ready');
            const width = source.width, height = source.height;
            const xs = points.map(p => p.x * width), ys = points.map(p => p.y * height);
            const x = Math.max(0, Math.floor(Math.min(...xs))), y = Math.max(0, Math.floor(Math.min(...ys)));
            const w = Math.min(width, Math.ceil(Math.max(...xs))) - x;
            const h = Math.min(height, Math.ceil(Math.max(...ys))) - y;
            if (w < 40 || h < 40) return 'Move closer so your face is clear.';
            // Match upstream preprocessing: face crop, centered RGB letterbox,
            // NCHW float32 in [0,1]. Normalization is already in the model graph.
            const scale = Math.min(128 / w, 128 / h), dw = Math.floor(w * scale), dh = Math.floor(h * scale);
            this.context.fillStyle = '#000'; this.context.fillRect(0, 0, 128, 128);
            this.context.drawImage(source, x, y, w, h, Math.floor((128 - dw) / 2), Math.floor((128 - dh) / 2), dw, dh);
            const pixels = this.context.getImageData(0, 0, 128, 128).data;
            const data = new Float32Array(3 * 128 * 128), plane = 128 * 128;
            for (let i = 0; i < plane; i++) {
                data[i] = pixels[4 * i] / 255;
                data[plane + i] = pixels[4 * i + 1] / 255;
                data[2 * plane + i] = pixels[4 * i + 2] / 255;
            }
            const input = new this.ort.Tensor('float32', data, [1, 3, 128, 128]);
            let output;
            try {
                output = await this.session.run({ [this.session.inputNames[0]]: input });
                return assess(output[this.session.outputNames[0]]?.data);
            } finally {
                input.dispose();
                if (output) Object.values(output).forEach(tensor => tensor.dispose());
            }
        }
    }
    if (typeof module !== 'undefined') module.exports = { assess };
    else window.IdentityFaceVisibility = FaceVisibility;
})();
