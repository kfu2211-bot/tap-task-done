# Password reset setup

Run `mysql_password_reset.sql` once for the target database. The local development database has been initialized separately; each deployment needs its own table.

Configure these settings using environment variables or an ignored development-only `appsettings.Local.json`; never commit SMTP credentials:

- `PasswordReset__BaseUrl`: the trusted application origin, for example `http://localhost:5133` for local development. Production requires HTTPS and must use the live app's own URL.
- `PasswordReset__Smtp__Host`: SMTP server host.
- `PasswordReset__Smtp__Port`: STARTTLS port, normally 587.
- `PasswordReset__Smtp__Sender`: permitted sender email address.
- `PasswordReset__Smtp__Username`: SMTP username.
- `PasswordReset__Smtp__Password`: SMTP credential or application password, not the email account's normal password.

SMTP must support STARTTLS. Do not paste credentials into chat, commit them, or put them in tracked appsettings files. The recovery page remains unavailable until configured. Before production, verify delivery, restrict the SMTP account, confirm session invalidation, protect request logs from reset URLs, and review per-account abuse protection and uniform response timing. Account email addresses are currently not verified during registration; consider email verification before production.
