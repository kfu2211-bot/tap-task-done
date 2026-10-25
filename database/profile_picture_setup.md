# Encrypted account profile pictures

Run `mysql_user_profile_picture.sql` on the intended database **before deploying the updated code**. It adds a nullable `ProfilePicture MEDIUMBLOB` to `tbl_user` and does not replace existing account data. The EF user model maps this column, so updated user queries require it. Local and live databases need the same schema change; applying it locally does not change Aiven.

Profile pictures are protected with ASP.NET Core Data Protection using purpose `TapTaskDone.ProfilePicture.v1` plus the owning user ID. Only ciphertext is stored in the single BLOB column. MIME type is determined from supported image signatures after decryption; uploads accept JPG, PNG and WEBP up to 5 MB. The endpoint uses the authenticated user's ID, not an ID supplied by the browser. Upload and removal require Razor antiforgery validation. GET decrypts server-side and returns the image with private/no-store and nosniff headers. Missing photos fall back to initials.

Production uploads require the existing persistent encrypted Data Protection setup (`IdentityVerification__Enabled=true`, the configured certificate/password, and `data_protection_keys`). Keep the same certificate, password, application name and existing key ring across deployments; preserve old keys. Do not regenerate or delete keys to fix a photo issue. Back up encrypted photos together with the key ring and its protecting certificate. Local development uses the normal development Data Protection key store. Restoring encrypted rows without the corresponding keys cannot decrypt them.

Settings saves photos immediately through `/ProfilePhoto?handler=Upload` and removes them through `/ProfilePhoto?handler=Remove`. The account avatar and Settings preview read `/ProfilePhoto` after refresh/sign-in, including on other devices. Work preference submission stays separate. Old device-only photos are not uploaded automatically; reselect the image once to save it to the account. No photo bytes are saved in browser localStorage by the new flow.

No deployment or live schema change is performed by this implementation.
