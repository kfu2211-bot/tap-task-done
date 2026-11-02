# Safety report review update

Before deploying, select the intended database and run `mysql_safety_report_review.sql`. It adds PublicMessage and ReviewConfirmed to safety_report_actions without exposing old internal notes.

Admin: review evidence, related work, and both explanations through the existing support process. Report targets are free text; confirm the actual user in Users before recording an offense using report:ID. A status update never applies a penalty or issues a refund.

Every status change requires a reporter-facing message. Closing additionally requires an internal reason and evidence-review attestation. Closed reports cannot be reopened; new evidence may be submitted as a follow-up referencing the original report.

Reporter updates appear in their own report history. Email delivery is best effort after the transaction commits. Linked-task reports also create a bell notification; reports without a task receive history and email updates. Internal notes are never copied to reporter updates or email. There is no automatic fact-finding or in-app mediation conversation in this update.

Validation used synthetic records and rule checks only. No live schema changes, emails, report decisions, or penalties were executed. Run an end-to-end check with a designated test report after applying the migration.
