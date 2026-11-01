# New-device verification, login alerts and sessions

Implemented for normal and admin browser sign-ins. No production database changes or deployment are performed by the application.

## Activate after preparation

1. Back up the intended database. Run the entire `mysql_login_security.sql` after selecting `USE defaultdb;` for Aiven or `USE taptaskdone;` for local MySQL. It creates four new tables; no existing task/user data is changed. Do not run the local schema name against Aiven.
2. Confirm actual email delivery **from the deployed application**, not just an external SMTP tester. The same `PasswordReset:Smtp` settings in appsettings are used. Previous production SMTP timeouts must be resolved first. A delivery error fails closed: a new browser gets no login cookie.
3. Change `LoginSecurity:Enabled` from `false` to `true` in `appsettings.json`, then restart/deploy. The staged default is false to avoid locking all users out before tables and delivery are ready. Environment configuration can override appsettings; check for an old override if behavior differs.
4. All old browser cookies without a registered session must sign in again. Admin sessions expire after 30 minutes; regular sessions after 8 hours, both absolute. Password changes invalidate previous sessions and trust even before cleanup.

Do not temporarily bypass the new-device check on delivery errors. To stage/rollback before activation, false preserves the previous login behavior; this intentionally disables the added protection and must not be confused with successful activation.

## Behavior

- Valid email/password and eligible account → code email for an untrusted browser → `/VerifyLogin` → successful code creates the full authenticated session and login alert.
- Six-digit codes expire in 10 minutes, max five valid-format guesses. Only hashes are stored. Challenge identity uses a random 256-bit HttpOnly cookie; the code hash is bound to that secret. A copied database code hash does not reveal the code without the browser secret. Account limit: five challenges per 15 minutes; endpoint IP rate limits also apply.
- Code checking, consumption and session creation share a database transaction and account/challenge row locks; duplicate simultaneous submissions cannot issue two sessions. Wrong guesses are committed and counted. Password changes/suspension/age/email verification are checked again before issuing access.
- Trust is optional for 30 days and bound to a random HttpOnly cookie plus password version. Browser name comes from User-Agent and is only descriptive, never authentication evidence. No IP geolocation or precise location is collected by this feature. Do not trust shared/public browsers.
- Every completed sign-in records an in-app alert; alert email failure does not erase the already authenticated session. The UI reports when the alert email was not delivered. No passwords, OTPs or raw session/trust tokens are written into application logs/database rows.
- Other open workspace screens show an alert banner within about 15 seconds while visible (or on returning focus), plus a bell count within the existing 30-second poll. This is polling, not push notification; closed/background pages cannot guarantee an immediate alert. Admin uses the same security banner and Security page.
- Settings → Security & devices lists active sessions, recent sign-ins and trusted browsers. All list/mutation queries are scoped to the authenticated user. Sensitive actions require the current password and antiforgery validation.
- Sign out one/all-other sessions takes effect on the next server request. Revoking sessions also clears all trusted browsers for that account. Removing only trust leaves an existing session running until it is revoked/expires.
- Secure my account changes the password, revokes **all** sessions including the current one, removes trust and invalidates pending codes. Forgotten password uses the existing recovery flow; successful resets revoke all sessions/trust. Sign-out endpoints revoke the current server session.
- Old mobile tokens are rejected when enabled. `/api/mobile/login` can exchange valid credentials only after the same account completes the browser verification and sends that authenticated Cookies session. The issued bearer is tied to that revocable browser session. A separate native-client OTP UI is not supplied; native clients must use this web-assisted flow rather than bypassing verification with password-only tokens.
- Existing ambiguous/fake location labels are not used for sign-in security. Email verification is an additional check, not phishing-resistant MFA or biometric verification.

## Acceptance checklist (test accounts only)

- Apply schema on an isolated test database and confirm all four tables, indexes and foreign keys; never populate the live database with automated test accounts.
- Untrusted browser: correct password redirects to VerifyLogin with no Cookies/AdminCookie; wrong, expired and reused code cannot log in. Fifth wrong guess blocks that code. Five challenge requests prevent further sends for 15 minutes.
- Two simultaneous valid code submissions produce one session only. Revoke/password-reset concurrent with verification cannot reuse old password credentials/trust.
- With trust checked, logout/login on the same browser skips the code until expiry. A second browser still requires a new code. Cookie removal, trust removal and password reset force a new code.
- Log in from browser B while A is open; A shows a banner without losing its session. Review alert / Not me link leads to Security. Closing the browser does not erase server alert history.
- Revoke B from A: B's next request is rejected. Other users' session IDs cannot be revoked; wrong current password makes no changes. Secure account signs out A and B, clears trust and requires a new code.
- Admin follows OTP and revocation too; ordinary users cannot access admin pages. Verify malformed/mobile legacy tokens cannot bypass the new checks.
- Test actual sender delivery, failure/timeout, browser HTTPS cookie attributes, mobile layout, keyboard focus, concurrent requests and refresh. Build/pure-policy/render checks alone do not establish these deployment properties.

Expired challenges/trust/sessions are rejected in queries even while retained. Schedule bounded maintenance appropriate to your retention policy (e.g. remove expired challenges after a day, expired sessions/trust after 30 days, old alert history after 90 days); no automatic destructive cleanup is introduced here.

Reference: [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html).
