# Confirmed offenses and orange-warning recovery

Apply `mysql_account_offenses.sql` to the intended database **before deploying** the new code. Existing `mysql_admin_account_moderation.sql` and `mysql_task_progress.sql` tables are also required. This task has not run migrations, inserted live test records, pushed, or deployed.

| Lifetime confirmed offense | Automatic penalty | Recovery after suspension |
| --- | --- | --- |
| 1 | Written warning | None |
| 2 | Orange account warning | 3 completed tasks |
| 3 | 3-day suspension | 6 completed tasks |
| 4 | 7-day suspension | 10 completed tasks |
| 5 | Permanent suspension | No task-based recovery |

The admin reviews evidence, supplies a distinct incident reference (e.g. report:123) and reason, confirms the review checkbox and the displayed next penalty. A user-row lock serializes decisions. The unique user/incident index prevents repeat submissions from counting the same reference twice; administrators must reuse the same reference for the same real incident. The server rejects stale penalty previews. Reports alone never increment an offense.

Warnings do not block booking, posting or offers. A yellow triangle with exclamation mark and orange styling appears beside marked account names in the header, settings, provider cards/profile, conversations, offers, client job names and Tracking. Only the owner/admin sees the recovery count. Public warning responses contain no incident descriptions or offense counts. Warning display refreshes every 30 seconds while the page is visible.

Recovery uses distinct client-confirmed completed task events after the most recent offense, or after the temporary suspension ends. Both client and accepted freelancer receive at most one qualifying credit per task. Client & Freelancer accounts can recover in either role. Completion while suspended does not count. Cancellation does not count and does not reset the streak; a new confirmed offense resets it by establishing a new starting timestamp and higher target. Finishing work without client confirmation does not count. Clearing the badge preserves the offense count and audit history.

Temporary suspensions expire by UTC comparison at request time, including when the host was asleep. Login displays the Philippine end time and a countdown; server time is authoritative. Permanent suspension has no automatic expiry. Existing sessions are signed out on the next request. Web/mobile login and protected marketplace actions check the same policy. No automatic task cancellation or reassignment is performed when a participant is suspended; admins should review any ongoing work.

Admin `Lift penalty after review` requires a reason and removes the current penalty without erasing the lifetime offense history. A fifth-offense lift is an exceptional admin decision, not task-based recovery; the account remains at the final offense level and further numbered offenses are disabled pending a separate policy decision.

## Existing moderation data

Legacy warnings/restrictions/suspensions are kept in their original history. They are not guessed to be confirmed numbered incidents. Existing restrictions become non-blocking orange warnings with 3-task recovery starting at their recorded UpdatedAt timestamp. Legacy indefinite suspensions remain blocked until reviewed/lifted; the administrator must lift one before starting new numbered offenses. New counted decisions supersede prior non-blocking restrictions. If historical incidents must count, an administrator should review them explicitly rather than treating old clicks as proven offenses.

## Acceptance checks

Use synthetic accounts/tasks in a separate database. Do not run these checks on real customer records.

1. Confirm #1 → written notice, no badge. Confirm #2 → badge, 0/3 and normal marketplace access.
2. Submit the same incident twice → one offense only; stale next-number submission → rejected.
3. Confirm 3 distinct task completions → badge clears; duplicate completion cannot add extra credit.
4. At recovery 2/3, confirm a new incident → #3, suspended for 3 days, then 0/6. Verify one instant before expiry is blocked and exact expiry permits access.
5. Repeat for #4: 7 days then 0/10. Check #5 remains suspended indefinitely.
6. Count client completion and assigned freelancer completion, including Both role without double counting. Unrelated tasks, cancelled work, pre-penalty completions and work-finished-only events must not count.
7. Admin/self/staff and missing-reason/evidence/reference checks; audit and account notices retain reasons. Notification bell includes unread account decisions and opens the correct account-notice handler.
8. Verify a normal user cannot call admin review endpoints. Check badge accessibility and mobile layout; confirm other users cannot read recovery counts or incident reasons through public warning endpoints.

Local automated validation covers pure policy boundaries, SQL translation and synthetic browser UI. MySQL transaction/unique-index integration and live deployment require separate testing after schema setup.
