# Free server-side ID number comparison

The Docker image installs Tesseract OCR (English data) and Poppler. No paid API or external document processing service is used. A Docker rebuild/redeploy is required. For local Windows development, install Tesseract and Poppler separately and put `tesseract` and `pdftoppm` on PATH; missing tools block submission rather than bypassing verification.

Before camera capture, Open camera automatically posts the uploaded ID, selected type and entered number to the authenticated, antiforgery-protected CheckId handler. OCR must find a supported document heading and the exact full number following an ID-number label. Numbers from dates or unrelated text do not suffice. Only spaces, hyphens and letter case are normalized; O/0 or I/1 are never silently substituted. Low-confidence OCR lines are discarded in full. Blurry, unsupported layouts and unreadable text require a clearer front image.

A successful check returns an encrypted, twenty-minute receipt bound to the user, selected ID type, normalized number and SHA-256 of the exact uploaded bytes. Changing any ID detail clears the browser receipt and selfie progress. Final submission validates that receipt against the actual upload; missing, expired or altered receipts trigger server OCR again. A client cannot authorize a number match by changing a browser flag.

PDFs are rendered from their first page to a bounded-size PNG before OCR. The PDF text layer is never trusted. Image uploads use JPEG/PNG. Reading has a 25-second process timeout, one OCR request runs at a time per app instance, and Tesseract is restricted to one OpenMP thread. Private temporary directories are outside wwwroot and cleaned after success, failure or cancellation. OCR text and ID numbers are not logged or stored as plaintext. The existing encrypted ID evidence and retention policy remain in effect.

This compares visible text; it does not validate issuer records, detect all altered/forged IDs, or establish identity. Someone editing both the photo and number can still match OCR. Admin document/face review and typed-number confirmation remain required before approval. Do not claim government authentication or guaranteed legitimacy.

Run parser and signed-receipt regression checks with:

```
dotnet run --project tests/identity-ocr/IdentityOcrChecks.csproj -p:UseAppHost=false --no-launch-profile
```

Also test real clear documents, mismatched numbers, wrong types, unclear images and first-page PDFs in the deployed Docker runtime. The implementation workspace has no Tesseract executable, so live engine accuracy cannot be established by parser tests alone. No production documents or verification decisions were used for automated tests.
