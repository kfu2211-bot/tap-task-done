# Password reset setup

Run `mysql_password_reset.sql` and `mysql_password_reset_codes.sql` for the target database. The local development database has been initialized separately; each deployment needs its own table.

Configure these settings using environment variables or an ignored development-only `appsettings.Local.json`; never commit SMTP credentials:

- `PasswordReset__BaseUrl`: the trusted application origin, for example `http://localhost:5133` for local development. Production requires HTTPS and must use the live app's own URL.
- `PasswordReset__Smtp__Host`: SMTP server host.
- `PasswordReset__Smtp__Port`: STARTTLS port, normally 587.
- `PasswordReset__Smtp__Sender`: permitted sender email address.
- `PasswordReset__Smtp__Username`: SMTP username.
- `PasswordReset__Smtp__Password`: SMTP credential or application password, not the email account's normal password.

The PasswordReset:Smtp section in appsettings.json applies to local and published builds; configured environment values override JSON. Gmail uses implicit TLS on 465 or STARTTLS on 587. Render Free blocks both ports; see gmail_setup.md. Do not paste credentials into chat, commit them, or put them in tracked appsettings files. The recovery page remains unavailable until configured. Before production, verify delivery, restrict the SMTP account, confirm session invalidation, protect request logs from reset URLs, and review per-account abuse protection and uniform response timing. Account email addresses are currently not verified during registration; consider email verification before production.
