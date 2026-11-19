# Free server-side ID number comparison

## Number fields

The user form changes its number label and hint for each supported ID:

| ID | Number to enter |
| --- | --- |
| Driver's License | License No.; not LTO client ID or card serial |
| Philippine National ID | 16-digit PhilSys Card Number (PCN); not private PSN or transaction number |
| Philippine Passport | Passport No. on the biodata page |
| UMID | Common Reference Number (CRN); not SSS membership number |
| PRC ID | Registration No. |
| Postal ID | Postal Reference Number (PRN) |

References checked: [LTO license validation](https://portal.lto.gov.ph/ords/f?p=PUBLIC_PORTAL:DIV), [PSA PCN advisory](https://psa.gov.ph/content/psa-begins-issuance-psn-and-philid-successful-registrants), [passport data fields](https://elibrary.judiciary.gov.ph/thebookshelf/showdocs/2/99268), [SSS UMID application](https://www.sss.gov.ph/sss/DownloadContent?fileName=SSSForms_UMID_Application.pdf), [PRC e-PIC guide](https://www.prc.gov.ph/sites/default/files/LERIS-Online-Step-by-Step-Procedure-v8.pdf), [PHLPost application](https://phlpost.gov.ph/wp-content/uploads/2024/10/PID-Application-Form_101024-1-1.pdf).

OCR accepts known labels, a standalone 16-digit PCN line on a recognized National ID, and LTO number/expiry/agency columns where the number is clearly delimited. These rules can still reject unfamiliar or blurry layouts; they do not authenticate government records. Front-end errors distinguish OCR mismatch responses from HTTP/session/server failures and client timeouts. A connection error alone is not evidence of an incorrect number.

## Runtime and matching

The Docker image installs Tesseract OCR (English data), Poppler and ImageMagick. No paid API or external document processing service is used. A Docker rebuild/redeploy is required. For local Windows development, install these tools separately and put `tesseract`, `pdftoppm` and `magick` on PATH. Missing tools block automatic checking; the app never calls Windows' unrelated disk-conversion `convert.exe`.

Before camera capture, Open camera automatically posts the uploaded ID, selected type and entered number to the authenticated, antiforgery-protected CheckId handler. OCR must find a supported document heading and the exact full number following an ID-number label. Numbers from dates or unrelated text do not suffice. Only spaces, hyphens and letter case are normalized; O/0 or I/1 are never silently substituted. A line with an uncertain numeric group or standalone prefix/suffix letter is discarded in full. Alphabetic labels are preserved even when faint, so a clearly read full number is not lost because its label scored lower. A confident different number or recognized wrong ID type is blocked. Missing or ambiguous text is classified as unreadable, not reported as a proven mismatch.

A successful check returns an encrypted, twenty-minute matched receipt bound to the user, selected ID type, normalized number and SHA-256 of the exact uploaded bytes. Changing any ID detail clears the browser receipt and selfie progress. Final submission validates that receipt against the actual upload; missing, expired or altered receipts trigger server OCR again. A client cannot authorize a number match by changing a browser flag.

On server OCR timeout or unreadable text, the server can instead issue a signed **manual-only** receipt with the same bindings. The user must explicitly choose Continue with manual review, then complete the normal camera and selfie flow. Final submission requires both the signed manual receipt and the explicit choice; setting a browser flag alone cannot enable fallback. Busy, unavailable tools and explicit mismatches do not issue fallback receipts. There is no fallback based solely on a browser network timeout.

Both paths create pending requests. Encrypted `identity_resources` evidence stores `ocr-check` as `matched` or `manual`; this is not an approval. Admins see a manual-review notice for the latter, and the existing full-number/type transcription, document and face confirmation requirements remain mandatory for approval. No schema migration is needed. Retention removes the additional evidence with the other reviewed documents.

The optional browser crop editor lets the user trim backgrounds with labelled sliders, preserving all ID edges and text. Applying the crop replaces the selected upload with a JPEG capped at 1800 pixels and clears any prior receipt and camera evidence. The server still processes and hashes the actual uploaded bytes; it does not trust a client crop/optimization flag.

PDFs are rendered from their first page to a PNG capped at 1800 pixels; the PDF text layer is never trusted. ImageMagick auto-orients, bounds JPEG/PNG images to 1800 pixels, removes transparency against white and converts to grayscale before Tesseract. Memory/map/disk/thread limits bound image preparation. Reading retains a 25-second total deadline; one OCR request runs at a time per app instance, and Tesseract is restricted to one OpenMP thread. Stage timings (`pdf-render`, `image-prepare`, `text-read`, `text-read-block`) and total milliseconds are logged without OCR text, numbers or document contents. Private temporary directories, including ImageMagick spill files, are outside wwwroot and cleaned after success, failure or cancellation.

This compares visible text; it does not validate issuer records, detect all altered/forged IDs, or establish identity. Someone editing both the photo and number can still match OCR. Admin document/face review and typed-number confirmation remain required before approval. Do not claim government authentication or guaranteed legitimacy.

Run parser and signed-receipt regression checks with:

```
dotnet run --project tests/identity-ocr/IdentityOcrChecks.csproj -p:UseAppHost=false --no-launch-profile
```

Also run `node tests/identity-manual-flow.cjs` to verify explicit fallback choice, camera gating and clearing authorization after ID changes. Pipeline tests simulate the native tools to verify preprocess-before-OCR ordering, receipt isolation and timeout/mismatch decisions; they do not measure native OCR accuracy or speed. The crop editor was exercised in a browser using a synthetic test image. Test real clear documents, mismatched numbers, unclear images and PDFs in the rebuilt Docker runtime, then inspect stage timings. This workspace has no native OCR/image-preparation tools, so deployment performance and accuracy remain unmeasured. No production documents or verification decisions were used for automated tests.

An unreadable sparse-layout result gets one block-layout OCR retry if enough of the original 25-second deadline remains. Explicit mismatches do not use that fallback. If still unreadable, Continue to camera is the primary manual-review action; Retry ID check remains optional. Repeated retakes/crops are not required for a clear document that will receive manual review.
