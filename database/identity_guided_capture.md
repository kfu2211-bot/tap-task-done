# Free guided identity capture

This flow uses locally hosted MediaPipe face/hand landmarks and FaceAttribNet for glasses, sunglasses, mask and eye visibility checks. It requires no paid API, provider account, or API key. All assets under `wwwroot/vendor/mediapipe`, `wwwroot/vendor/face-attributes` and `wwwroot/vendor/onnxruntime` must be included when publishing. `Program.cs` serves `.task` and `.onnx` files as binary model data. The additional local models/runtime add approximately 62 MB to the first camera load; browser caching can reduce subsequent loads.

## User flow

- Choose the actual government ID type and enter the number printed on it.
- Consent to encrypted storage of the full number, ID, selfie and movement photos.
- Complete five guided poses: front, a randomized order of left/right/tilt, and front again. The browser checks one visible face, framing, basic lighting, pose stability, glasses, sunglasses, mask, eye openness and detected hand overlap before capturing each frame. The former readiness checkbox has been removed.
- Accessory predictions >=0.5 block with a specific removal instruction; predictions >=0.2 are uncertain and also block. Both eye-open scores must be >=0.65. These are application policy thresholds, not calibrated guarantees. A blink, lost face, poor framing, detected covering or hand overlap pauses the current step and cancels final capture; earlier accepted movement frames stay in memory and in the form. They are not saved to browser storage and do not survive a page reload. An explicit Restart all steps or an ID change clears them.
- After all five frames are accepted, a continuous clear frontal face starts a Hold still 3–2–1 countdown over the camera preview. Failed checks or stale detections cancel the countdown and restart it only when checks pass again; movement frames remain saved. Final capture rechecks and encodes the exact inspected frame, then closes the camera. Retaking only the final selfie and reopening after a background pause reuse accepted movements. The two-minute prompt no longer discards progress or stops the guide. The signed submission session still expires after twenty minutes, requiring a page reload.

## Review

The admin must read and enter the full number and ID type from the uploaded document. The server compares those values to the encrypted submission, ignoring only spaces, hyphens and letter case for the number. It blocks approval for mismatches, missing movement evidence, missing selfies or incomplete confirmations. Admins inspect the account name, document validity and visible alterations, face match and requested poses. Older pending requests without full numbers or movement evidence require resubmission. Existing approved requests are unchanged.

New evidence uses the existing `identity_resources` table; no migration is needed. The existing retention worker removes all evidence other than consent 30 days after review. Only the last four ID characters remain in request metadata.

## TiDB deployment compatibility

Submission and admin renewal use `REPEATABLE READ`, with `SELECT ... FOR UPDATE` on the account row followed by a locking read of the latest verification request. The account lock also serializes first-time submissions when there is no request row yet. Both paths acquire locks in the same order. TiDB rejects `SERIALIZABLE`, which previously caused the generic save error after a valid camera submission. Deploy the updated application; no schema change or `tidb_skip_isolation_level_check` setting is required.

## Limits

Browser pose/accessory checks and uploaded frames are untrusted evidence for manual review. A client can fabricate uploads. Model predictions can miss thin glasses, unusual coverings, or partially hidden features and can reject clear faces, especially in poor lighting or turned poses. Unknown coverings are not guaranteed to be detected. This flow does not provide certified liveness, presentation attack/deepfake detection, automated face identity matching, OCR, or verification against government records. Passing the camera guide never automatically approves an account or proves that a document is genuine. Do not describe it as a certified identity verification service.

Verification: `node tests/identity-face-visibility.cjs` covers accessory/uncertainty gates, hand overlap, saved progress and resume, exact-frame capture blocking and async session cancellation. `node tests/identity-capture-countdown.cjs` covers countdown timing, cancellation and single capture; `node tests/identity-selfie-auto.cjs` covers countdown-to-photo integration and keeping movements on retake. Browser inference on the upstream public test samples blocked all eight covered/closed-eye samples, and the clear face in the group sample passed the visibility classifier. This small sample does not measure production accuracy. Actual camera testing on supported phones/laptops remains necessary.
