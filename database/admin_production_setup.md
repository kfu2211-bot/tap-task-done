# Admin verification and safety reports

## Account management

Before deploying the shared local/production Users moderation page, back up the
target database and apply `mysql_admin_account_moderation.sql`. It creates
`admin_account_restrictions` and `admin_account_actions` without changing account
records. Both tables are required by login, active-session suspension checks,
marketplace restrictions, admin history, and user account notices.

Verify both tables exist before deploying. Then test Warn, Restrict, Suspend, and
Restore using a dedicated non-staff test account. Check that suspension blocks
login and existing sessions, restrictions prevent new marketplace commitments,
and the user can read the admin reason in Notifications. Staff accounts and
self-moderation remain blocked by the server. Do not test on real user accounts.

## Identity verification and reports

The admin queue can list request metadata while document review is paused. A count of
0 means no pending requests; a dash means the database count could not be loaded.
Document submission, downloads, and review require secure storage configuration.

Before enabling document verification on the hosting service:

1. Back up the target database. Review and apply the identity schema in
   `mysql_identity_verification.sql`, then `mysql_identity_resources.sql`, and
   `mysql_data_protection_keys.sql`. The identity script's local-development warning
   reflects the original rollout; production use additionally requires the encrypted
   key configuration below and an approved deployment of the application.
2. Configure `IdentityVerification__Enabled=true`,
   `IdentityVerification__CertificateBase64` (a password-protected PFX containing its
   private key), and `IdentityVerification__CertificatePassword` in the hosting
   service's secret settings. Use the existing `ConnectionStrings__ApplicationDatabase`.
3. Preserve the certificate, its password, and the data protection key table across
   redeployments. Back them up securely; losing them can make documents unreadable.
4. Apply `mysql_safety_reports.sql`, then `mysql_safety_report_evidence.sql` for
   report attachments. Existing reports remain visible if attachment loading fails;
   the admin page shows a warning rather than claiming there are no attachments.
5. Verify an empty queue, a test submission, document download, and an admin decision
   in a staging environment before rollout.

These are preparation instructions. No production settings or database changes are
performed by the application fixes, and no deployment has been initiated.
