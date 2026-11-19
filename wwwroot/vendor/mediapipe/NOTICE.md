# MediaPipe Face Landmarker

Vendored locally so users do not send camera frames to a third party or download executable code from a CDN during verification.

- JavaScript/WASM: `@mediapipe/tasks-vision` version `0.10.22-rc.20250304`, downloaded from https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.22-rc.20250304/
- Model: https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task
- Hand model: https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task (used to block detected hands overlapping the face).
- Upstream: https://github.com/google-ai-edge/mediapipe
- Documentation: https://developers.google.com/edge/mediapipe/solutions/vision/face_landmarker

These assets support face landmarks and guided pose capture. They are not a certified liveness, face identity matching, OCR, or government document authentication service.
