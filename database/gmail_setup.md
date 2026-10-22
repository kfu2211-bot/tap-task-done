# Gmail email verification and password reset

1. Apply mysql_email_verification.sql and mysql_password_reset.sql to the correct MySQL database. Back it up first.
2. Use a dedicated Google account. Turn on Google 2-Step Verification and generate an app password if Google offers it for that account. Never commit it to source control.
3. Configure PasswordReset:BaseUrl to the app's public HTTPS origin (or http://localhost:5133 for local development). Never use localhost for links sent to other devices.
4. Set PasswordReset:Smtp:Host=smtp.gmail.com, PasswordReset:Smtp:Port=587, PasswordReset:Smtp:Username and PasswordReset:Smtp:Sender to the Gmail address, and PasswordReset:Smtp:Password to the app password. Use .NET user secrets locally or environment variables in hosting (double underscores in place of colons).
5. Only after database and SMTP are working, set EmailVerification:Enabled=true and restart. Existing users must verify their registered email after signing in; accounts with inaccessible email need a secure manual recovery process. Existing sessions will be signed out until verified.
6. Test registration, resend, expiry, login, password reset, replay and old-session invalidation using a real mailbox. Check spam. Gmail sending limits and delivery may vary; use a transactional email service for production scale.

Password resets are issued only to verified email addresses while verification is enabled. Do not enable verification until email delivery is working.
