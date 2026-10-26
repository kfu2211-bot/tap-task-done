# ID and camera selfie review

Before deploying, select the intended database and run `mysql_identity_selfie.sql`. This idempotent script adds only `identity_requests.ReviewReadAt` for verification notification read state. Do not copy or replace identity encryption keys when deploying: existing documents rely on persistent Data Protection keys and the configured certificate.

New submissions require a government ID, camera selfie, and explicit consent. Selfies are limited to JPG/PNG and 5 MB; the existing ID and optional documents can also be PDF. `identity_resources` stores encrypted `selfie` and `consent` records with the same request and encryption purpose as existing documents. No new selfie column is necessary. No face templates or face recognition service are used. Camera capture is not liveness detection and cannot establish document authenticity.

Only AdminOnly reviewers can retrieve documents. The admin compares the ID and selfie manually, confirms the review, and approves, rejects, or requests resubmission. Rejection/resubmission requires a note. Older pending requests without a selfie must be resubmitted before approval. Existing approvals remain valid. In-app notifications show the result and link back to verification.

Documents, including legacy document blobs, are purged 30 days after review by a worker checking every 6 hours while the app runs. Pending submissions remain until reviewed. Decision metadata and encrypted consent text remain for audit. Render sleeping/offline periods can delay deletion; backups may retain data under the database provider's backup policy. Plan an operator process for user deletion requests and unreviewed abandoned requests.

## Manual acceptance checks

- Use HTTPS or localhost; allow camera permission. Denial should show a useful message with no submission.
- Capture, retake, review ID/selfie, edit, then submit. A changed field requires review again. Camera closes after capture or leaving/hiding the page.
- Missing consent/selfie and a PDF masquerading as a selfie must be rejected on the server.
- Submitted request remains pending. Check decrypted images side by side as an admin; a normal account must not access the document handler.
- Approve only after confirming manual comparison. Reject/request resubmission with a reason; check the user's notification badge, result and note, and resubmission.
- Verify retention with synthetic data in an isolated database; never age or delete real documents to test it.

No production database migration or deployment was performed by the implementation task.
