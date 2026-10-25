# Gmail email verification and password reset

1. Apply mysql_email_verification.sql, mysql_password_reset.sql and mysql_password_reset_codes.sql to the correct MySQL database before deploying.
2. Use a dedicated Google account. Turn on Google 2-Step Verification and generate an app password if Google offers it for that account. Never commit it to source control.
3. Configure PasswordReset:BaseUrl to the app's public HTTPS origin (or http://localhost:5133 for local development). Never use localhost for links sent to other devices.
4. SMTP settings are read from PasswordReset:Smtp in appsettings.json on both local and published/live builds. The configured host is smtp.gmail.com and port 465 (implicit TLS); port 587 uses STARTTLS. Username and Sender use the Gmail address, and Password uses the Google app password. Keep credentials out of tracked files. Environment variables (double underscores in place of colons) override JSON when present. Development also loads ignored appsettings.Local.json. No other provider settings are read.
5. Only after database and SMTP are working, set EmailVerification:Enabled=true and restart. Existing users must verify their registered email after signing in; accounts with inaccessible email need a secure manual recovery process. Existing sessions will be signed out until verified.
6. Test registration, resend, expiry, login, password reset, replay and old-session invalidation using a real mailbox. Check spam. Gmail sending limits and delivery may vary; use a transactional email service for production scale.

Password resets are issued only to verified email addresses while verification is enabled. Do not enable verification until email delivery is working.


Render Free blocks outbound SMTP on ports 25, 465 and 587, including Gmail. Moving settings into appsettings.json or switching ports does not remove that hosting restriction. Live Gmail SMTP requires hosting that allows outbound SMTP: https://render.com/docs/free. No plan upgrade or production change is automatic.

Connection timeouts are now handled as delivery failures, and ForgotPassword catches TimeoutException defensively instead of showing the generic exception page. Diagnostics record only stage/type, database code and trace ID.
