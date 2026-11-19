# Free guided identity capture

This flow uses locally hosted MediaPipe face landmarks. It requires no paid API, provider account, or API key. All model assets under `wwwroot/vendor/mediapipe` must be included when publishing. `Program.cs` serves `.task` files as binary model data.

## User flow

- Choose the actual government ID type and enter the number printed on it.
- Consent to encrypted storage of the full number, ID, selfie and movement photos.
- Complete five guided poses: front, a randomized order of left/right/tilt, and front again. The browser checks one visible face, framing, basic lighting and pose stability before capturing each frame.
- Capture the final selfie and review the submission. HTTPS or localhost and a compatible camera browser are required. A two-minute movement timeout can be restarted; the signed submission session expires after twenty minutes, requiring a page reload.

## Review

The admin must read and enter the full number and ID type from the uploaded document. The server compares those values to the encrypted submission, ignoring only spaces, hyphens and letter case for the number. It blocks approval for mismatches, missing movement evidence, missing selfies or incomplete confirmations. Admins inspect the account name, document validity and visible alterations, face match and requested poses. Older pending requests without full numbers or movement evidence require resubmission. Existing approved requests are unchanged.

New evidence uses the existing `identity_resources` table; no migration is needed. The existing retention worker removes all evidence other than consent 30 days after review. Only the last four ID characters remain in request metadata.

## Limits

Browser pose checks and uploaded frames are untrusted evidence for manual review. A client can fabricate uploads. This flow does not provide certified liveness, presentation attack/deepfake detection, automated face identity matching, OCR, or verification against government records. Passing the camera guide never automatically approves an account or proves that a document is genuine. Do not describe it as a certified identity verification service.
