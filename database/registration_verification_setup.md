# Registration email-code verification

Flow: Create account → Verify email (six-digit code) → Email verified → Go to login. Verification never creates a full login cookie by itself. Government-ID/selfie verification is separate.

## Prepare and activate

1. Back up the intended database. Select `USE defaultdb;` on Aiven, or `USE taptaskdone;` on local MySQL.
2. Ensure `mysql_email_verification.sql` has been applied. Run the entire `mysql_registration_email_codes.sql`. It creates `registration_email_codes` and ensures a unique email index on `tbl_user.userName` to enforce concurrent registration/email-change uniqueness. It never merges/deletes accounts. **If duplicate_email_groups is nonzero, resolve duplicates manually before activation.** Do not mark old accounts verified just to bypass this requirement.
3. Confirm email actually reaches a test inbox **from the app's deployed host**, using the existing `PasswordReset:Smtp` settings. An external SMTP tester does not establish delivery from Render. Existing SMTP timeouts must be resolved first.
4. Set `EmailVerification:Enabled` to `true` in `appsettings.json`, then restart/deploy. It remains staged `false` until database/email prerequisites are ready, preserving the existing behavior rather than locking users out. Check `appsettings.Local.json` and environment configuration for an overriding value.
5. If also enabling new-device protection, apply `mysql_login_security.sql` and set `LoginSecurity:Enabled=true` as described in `login_security_setup.md`.

When verification is enabled, existing accounts without a matching `verified_emails` row also need to verify. Correct email/password sends them into the same verification flow. After enabling, existing authenticated cookies for unverified accounts are rejected by middleware. New-device OTP and mobile token exchange also enforce email eligibility.

## Code/session rules

- Codes use a cryptographically random six-digit value, expire after 10 minutes, and allow five valid-format attempts. Resend/change-email invalidates the previous code and starts a new attempt counter.
- A browser must hold the random, HttpOnly pending-verification cookie as well as know the email code. Only token/code hashes are stored. Password hashes/versions bind the pending flow to the credentials used at registration/login. No code is put into a URL or application logs.
- Pending browser access expires after 24 hours; the user resumes through normal email/password login. Code expiry does not force re-entry of all registration details. Reopening login in the same pending browser reuses that flow; it does not silently send another email.
- Resend has a **server-enforced 60-second cooldown**, also shown by a client countdown. At most five sends per account per 15-minute window, shared by resend, corrected-email and fresh pending-browser issuance. Existing IP rate limits also apply. A missing browser cookie cannot send codes just by supplying someone's email address.
- Changing a typo requires the pending cookie and registration password. The new email must not belong to another account. It remains a candidate until a code sent there is successfully verified. Then the account login email is atomically updated. If the user leaves before verification, they still resume using the original login email. This is not a change-email feature for already verified accounts.
- Verification, duplicate checks, username update, verified-email record and first-login proof are committed together while locking the account. Send/cooldown changes also lock that account. The database unique email index is still mandatory for races involving different accounts.
- Successful verification removes the pending cookie and shows a success page with **Go to login**. No Cookies/AdminCookie authentication is issued. Old verification links now require returning to login for a code; no new verification links are generated.
- To avoid two immediate email challenges, a separate single-use HttpOnly proof allows the **same browser's first password login within 10 minutes** to skip the new-device code if new-device protection is enabled. It is password-version/account/email-bound and consumed within the sign-in transaction. It does not create 30-day trust, authorize another browser or skip password/age/suspension checks. Lost/expired proof falls back to the normal new-device verification.
- Delivery failure leaves the account pending. The pending browser may retry after cooldown; there is no auto-verification or authenticated fallback. Failed SMTP sends count toward the send limits to limit abuse.

## Forgot password

With email verification enabled, only a registered account with a matching verified email can receive a reset code or complete a reset. Unknown and pending accounts get the same generic code-entry page/message and a dummy browser challenge without a usable database reset record. Delivery errors use that same public response; safe operational diagnostics remain in server logs. This removes explicit existence messages; synchronous SMTP timing can still differ and needs a queued delivery design if stronger timing resistance is required.

Registration still reports a duplicate email to guide legitimate users to resume login; this is a deliberate existing product behavior, not a claim that every public endpoint is enumeration-proof.

## Acceptance checklist

- Check schema/indexes on an isolated database; no automated test should point at Aiven production.
- Fresh registration stores a password hash and unverified account, then opens Verify email without authenticated Cookies/AdminCookie.
- Wrong/expired/replayed code fails; fifth wrong guess blocks the code. Tampered/missing cookie fails. Concurrent correct confirmations produce one verification result.
- Refresh preserves pending flow and server cooldown. Resend before 60 seconds fails even with JavaScript disabled; fresh-browser attempts/change email share the account send limit.
- Correcting email requires the password; an already registered email fails; username stays unchanged until the corrected mailbox code succeeds. Old code then fails. Concurrent claims for the same email are rejected by the unique index.
- Successful code shows Email verified → Go to login. Same-browser first login consumes one-time proof; a second browser still requires a device code. The proof cannot be replayed after logout or password change, and is not 30-day trust.
- Pending users cannot use password reset, obtain a mobile token or access authenticated features. Verified users can recover using the existing code flow.
- Test delivery from actual hosting, permission/config overrides, database race behavior, desktop/mobile layout and keyboard interaction. Build/policy/render tests do not establish live email delivery or database concurrency behavior.

Expired challenges are rejected even while their rows remain. Verified code rows retain the verified state/short-lived first-login proof; any cleanup must preserve the account's authoritative `verified_emails` row and must not delete live pending-session/send-limit state.

Reference: [OWASP Email Validation and Verification Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Email_Validation_and_Verification_Cheat_Sheet.html).
