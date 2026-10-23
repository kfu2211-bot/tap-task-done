# Mailgun password reset codes

Apply mysql_password_reset_codes.sql to the intended database before deploying.
Configure these environment variables on the server, without committing secrets:

- Mailgun__ApiKey: a newly rotated Mailgun sending key.
- Mailgun__Domain: your Mailgun sending domain.
- Mailgun__Sender: the authorized sender address for that domain.

The implementation uses Mailgun's US HTTP API at https://api.mailgun.net, not SMTP.
Sandbox domains can only deliver to authorized and verified test recipients.
Production requires an appropriately verified sending domain and a suitable account plan.
Never commit keys, codes or real IDs. Rotate any exposed API key.

Codes expire in 10 minutes. The browser must keep its secure reset cookie.
There are five attempts per code and ten per account per 15-minute window,
in addition to the per-IP rate limiter. Resending invalidates previous codes.
