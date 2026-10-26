# Six-stage task progress

Before deploying, select the intended schema and run mysql_task_progress.sql. It creates task_progress_events without rewriting tasks or existing statuses. This release also needs the existing task_destinations and mobile_location_updates tables. No live schema change or deployment has been performed automatically.

Booking accepted (assigned) -> On the way -> Arrived -> Work in progress -> Awaiting client confirmation -> Completed.

Only the accepted freelancer can start travel, mark arrival, start work or mark work finished. Only the owning client can confirm completion. Server-side state validation and a locked task row prevent skipping, repeat submissions or conflicting status updates. Start travel requires a saved destination pin. Completion requires a note of 1–1000 characters; an optional JPG/PNG/WEBP photo up to 5 MB is encrypted using the existing persistent Data Protection setup. Completion photos can be read only by assigned participants, with private/no-store headers.

Tracking contains the action form for the current participant/stage. The legacy My Jobs and My Tasks completion paths use the same server workflow. New accepted bookings/offers and subsequent progress changes record UTC timestamps. Old in-progress jobs remain in progress; timestamps for events predating this release are left blank, not fabricated. New history displays in Philippine time. Completed work remains viewable in Tracking, and existing Reviews requires a completed task.

Location writes, both browser and mobile, are allowed only while on-the-way. Arrival, work start, finish, cancellation, dispute or completion prevents further writes. Arrival is explicitly confirmed by the freelancer; GPS does not automatically assert arrival. A saved last-known position can remain visible with its timestamp/status, but is not labeled live. Travel sharing consent still survives refresh for the same tab/user/task; Stop sharing disables auto-resume.

Travel, arrival, work start, finished work and confirmation notify the other assigned participant. Client view checks task status with the existing location poll and refreshes when status changes (unless editing a destination or completion-note draft). Refresh keeps the existing sharing-consent behavior.

Deployment smoke test with two accounts:
1. Accept booking/offer. Client saves destination. Freelancer opens Tracking.
2. Start travel -> allow GPS -> client sees travel status and location. Check refresh and Stop sharing.
3. Mark arrived -> verify travel sharing stops and subsequent coordinate POSTs are rejected.
4. Start work -> finish with a completion note and optional image. Client can read those privately.
5. Client confirms completion -> both see Completed and can review. Freelancer cannot self-confirm.
6. Test repeated/out-of-order submissions, unrelated accounts and cancelled tasks. Older paused tasks remain read-only; the task-dispute feature has been retired (see task_edit_and_retirement.md).

Local/isolated DB integration tests were not run against the configured remote database. The user chose to perform live acceptance testing after their own schema setup/deployment. Build, transition-policy checks and actual Razor UI checks run without production data.
