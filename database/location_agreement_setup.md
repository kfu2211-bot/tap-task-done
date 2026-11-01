# Task location, private negotiation and final work agreement

## Database rollout

For local Workbench testing, use `mysql_work_agreements_local.sql` while connected to the local MySQL instance. It selects `taptaskdone`, creates destination storage if absent, and temporarily disables only the connection's SQL_SAFE_UPDATES for the intentional migration updates (Workbench error 1175). It restores the original setting at the end. If a run stops on another error, restore safe mode with `SET SESSION SQL_SAFE_UPDATES = 1;` or rerun the corrected local script to completion. This does not change global server settings. Separately verify `appsettings.Local.json` targets localhost/taptaskdone; a localhost website may otherwise still use Aiven.

Before deploying this code, select the intended schema and review/run `mysql_work_agreements.sql`. Existing marketplace tables, `task_destinations`, `task_progress_events`, and moderation tables are prerequisites. If destination storage is missing, run `mysql_task_destinations.sql` first. No production database, credentials or deployment was changed during implementation.

The update creates `task_conversations` and versioned `work_agreements`, adds nullable `ConversationId` to messages and notifications, and adds `marketplace_tasks.IsPrivate`. It keeps existing tables and records.

The migration recognizes existing direct bookings from their pending status or their existing direct-booking offer marker, and marks them private. Previously withdrawn direct requests that were reopened are closed as cancelled so they cannot become public jobs. Review that data correction before applying the script.

Existing offer/booking relationships get one private conversation per task/provider. Only unambiguous legacy messages are assigned to the existing accepted pair. If a task had a different previous assigned freelancer, its old unscoped messages remain untouched with NULL ConversationId for manual review; guessing their recipient could expose private messages. Such messages remain stored but do not appear in new threads. The script reports their count. Back up the database before a rollout and inspect that count; do not randomly assign unscoped messages to a new applicant.

## User flow

1. Client enters task or direct-booking details and selects a work location via explicit search, optional device location, or map click/drag. Address edits invalidate the previous confirmation. Pin confirmation is required before submitting. Draft editing and destination updates are saved atomically.
2. Draft task can be edited before confirmation/publication. New published tasks and direct bookings already have destination coordinates. Existing older assigned jobs can set final terms and a pin in Tracking.
3. Public listings show only `Bacolor, Pampanga` in the location field. Exact coordinates/address are available only to the owner and the selected freelancer. Users should not put exact private addresses or phone numbers in public descriptions. A direct booking is private to its addressed freelancer; withdrawing it never publishes it.
4. Freelancer can use Ask about this task before offering. Client and the addressed freelancer can discuss a pending direct request. Threads/messages are scoped to a task AND freelancer, so other applicants cannot read them. Once another provider is assigned, unselected conversations become read-only. Notifications route to the correct thread.
5. Accepting an offer/booking selects the freelancer and creates initial, unconfirmed work terms. Both parties review price, scope, schedule and destination in Tracking. They each press I agree to version N. Chat alone is not agreement confirmation.
6. Both confirmations atomically update the active work price, accepted offer price, schedule, scope and destination. Start travel is enabled only then; server validation also prevents bypassing this gate. Client confirmation of work completion remains separate.
7. A changed proposal creates a new immutable terms snapshot/version; its two confirmation timestamps start empty. Confirming an older version is rejected. UI refreshes the confirmation summary every 10 seconds; a new version requires reload/review and preserves unsaved draft fields until then. Agreement changes can be proposed while assigned or on the way, before arrival/work. During a pending travel amendment, pause travel; arrival/start work are blocked until both confirm. Current route/destination stays unchanged until both agree; normal map polling then reloads the agreed pin.
8. Travel, arrival, start work, mark finished, client completion confirmation and offense recovery keep their existing actor/status rules. Location sharing remains permission-based, foreground-only and at the existing send/poll intervals. It stops on arrival. Completing work still releases automatic Busy/Working status before client confirmation.

The old one-sided Tracking destination POST now returns 409; changes go through mutual agreement. The Open location in Maps link is removed. Embedded Tracking map search pans to a blue search-result pin without changing the agreed red destination. Recenter/fullscreen and existing routing/rerouting remain available.

## Map search configuration

`Geocoding:BaseUrl` is in `appsettings.json`, defaulting to `https://photon.komoot.io`. It is an interchangeable Photon-compatible service URL, not an API secret. Text search uses a server proxy, a 12-hour bounded cache and serialized requests with at least one second between upstream requests. A separate search rate limit prevents misuse. Requests happen only on Search clicks, not while typing. Device coordinates are not sent to the search service for automatic reverse geocoding.

Search/map defaults focus the Bacolor area using envelope latitude 14.92–15.10 and longitude 120.54–120.73. This is a service-area envelope, not an official municipal-boundary polygon; manual pin selection/confirmation remains necessary. Search results and current-location coordinates outside it are rejected. Street/place coverage and device accuracy vary. If search fails or permission is denied, manual pin selection remains available when the map loads; search results/device coordinates can still be confirmed if WebGL fails.

Photon's public demo API permits reasonable project traffic but does not guarantee availability; for larger use configure a suitable hosted/self-hosted instance. Reference: https://github.com/komoot/photon and https://github.com/komoot/photon/blob/master/docs/api-v1.md . Map tiles remain MapLibre/OpenFreeMap; routing remains the existing OSRM configuration. No live traffic or background GPS tracking is added.

## Validation performed

- Build and JavaScript syntax checks passed.
- No-database checks cover missing/nonfinite/outside-envelope pins; agreement validation, two-party confirmation and stale version rules; private messaging access policy; scoped SQL translation; unique relationship/version model; server-side invalid input handling and blocking the old destination mutation handler.
- Geocoder parsing/filtering/caching tested with a synthetic HTTP handler; one public Bacolor query also returned actual upstream results. No user location or private search was sent.
- Synthetic browser checks cover required pin, address invalidation, search result confirmation, permission denial/current location, draft pin seed, direct booking picker, mobile layout, public address masking, private conversation IDs, disabled/enabled Start travel, live counterpart confirmation, stale terms handling and removal of the external Maps link. Map imagery in the test fixture is a local blank style; it is not a live routing or street-data test.
- MySQL migrations, transaction races and multi-account integration were not executed against the configured live database. Validate those on an isolated database before production use.

## Two-account acceptance checklist after schema setup

- Create/edit/publicly confirm a task with a pin. Confirm listing exposes neither exact address nor coordinates. A third/unselected account must receive no exact-destination access.
- Two freelancers ask about it; verify distinct threads and messages, including forged conversation/task IDs. After one is assigned, the other thread is archived.
- Send a direct booking, discuss before accepting, select the provider, negotiate and confirm the same version from both accounts. Confirm replayed/old versions and one-sided agreement cannot start travel.
- Start travel, propose a different destination, check route stays at old destination until both approve, then route reloads the agreed pin. Confirm arrival is blocked while amendment is unconfirmed.
- Verify private withdrawal cancels rather than publishes. Check completion, reviews, notifications and offense-recovery counts still work.
