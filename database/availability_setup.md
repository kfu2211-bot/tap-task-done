# Automatic freelancer availability

Manual availability remains Available, Busy, or Day off. Active accepted work overrides it:

| Task status | Directory status |
| --- | --- |
| assigned | Manual preference |
| on-the-way | Busy · On the way |
| arrived | Busy · Arrived |
| in-progress | Working |
| awaiting-confirmation, completed, cancelled | Manual preference |

Finishing work releases the freelancer immediately; client confirmation remains required for completion and reviews. Busy providers remain visible. New booking requests and overlapping travel/work are blocked on the server. Provider cards refresh availability every 15 seconds while visible.

No new schema is required for computed availability. Completion reminders require the existing task_progress_events and marketplace_notifications tables. The background worker checks every 10 minutes and creates one in-app reminder after 24 hours awaiting confirmation. It does not send email or automatically complete work. A sleeping host delays reminders until the app is running again.

## Manual acceptance checks

1. Accept a booking: manual availability remains unchanged.
2. Start travel: directory shows Busy · On the way; another client cannot book that provider.
3. Mark arrival then start work: status becomes Busy · Arrived then Working.
4. Attempt starting a second accepted job: server rejects it.
5. Finish with a completion note: task awaits client confirmation; provider returns to their manual preference and can take new work when Available.
6. Client confirms: task completes and reviews become available.
7. Check cancellation and a manual Day off preference: cancellation releases the active override but preserves Day off.
8. Leave a finished task unconfirmed for 24 hours: one reminder appears when the worker next runs; repeated passes do not duplicate it.
