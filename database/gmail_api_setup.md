# Gmail API sender

The app sends verification codes and security mail through Gmail API over HTTPS when `EmailDelivery:Provider` is `GmailApi`. SMTP ports are not used. The sender is `taptaskdone@gmail.com`; Google OAuth support email is a separate setting.

1. Enable Gmail API in the Google Cloud project.
2. Create a dedicated OAuth Web application for this sender. If using Google's OAuth Playground with your own credentials, authorize the redirect URI `https://developers.google.com/oauthplayground`.
3. Configure only `https://www.googleapis.com/auth/gmail.send`. Authorize as `taptaskdone@gmail.com` with offline access to obtain a refresh token. This permission sends mail; inbox-reading access is unnecessary.
4. Copy `gmail_api_settings.example.json` into a private `appsettings.Gmail.json` and replace the placeholders. Never commit the actual credentials. The root private file is ignored by Git and Docker.
5. Locally, put the private JSON in the project root. For Render, upload its contents as a Secret File named `appsettings.Gmail.json`; the app reads `/etc/secrets/appsettings.Gmail.json` at runtime. Restart/deploy only when ready. Environment variables can still override settings if present.
6. Test registration verification, resend, password reset and new-device verification. Google accepting a message does not guarantee inbox delivery; check spam too.

External OAuth apps in Testing normally receive refresh tokens expiring after seven days for Gmail access. Add the sender as a test user for testing; complete Google's production/publishing requirements before relying on persistent delivery. Keep the same valid refresh token until replacing/revoking it deliberately.

Missing configuration fails closed with `gmail_configuration_missing`. Other safe log categories distinguish revoked authorization, disabled API, sender/scope denial, throttling and timeout. Provider responses and email contents are not logged. No automatic SMTP fallback occurs. No database migration is needed for this delivery change.

References: https://developers.google.com/workspace/gmail/api/guides/sending and https://developers.google.com/identity/protocols/oauth2/web-server
