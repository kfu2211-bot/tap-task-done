# Classroom search and matching data

Open `/FindPost?demo=1` after signing in, or click **Open demo search data** on Find & Post. This read-only mode supplies 13 clearly marked sample freelancers and 8 sample open jobs to the existing page. It exercises the same service/name matching, category aliases, location, maximum-budget and availability filters as the real directory. Negative sample IDs are never real database accounts/tasks.

No database rows, accounts, bookings, offers, messages, reviews, ratings or notifications are created. Real records are unchanged. Real pages elsewhere continue using the database. Sample profiles cannot be booked, offers are omitted, and task posting is disabled in demo mode. Exit through **Back to real records**.

Suggested teacher checks:

| Search | Location | Max budget | Availability | Expected demo results |
|---|---|---:|---|---|
| Cleaning | Bacolor | 700 | Available | Ana Reyes and Marco Cruz |
| Repairs | Bacolor | 900 | Available | Carlo Garcia (plumbing repairs alias) |
| Gardening | Bacolor | 500 | Available | Eli Mendoza |
| Gardening | Bacolor | 800 | Day off | Faye Flores |
| Ivan | Bacolor | 400 | Available | Ivan Aquino |
| Cleaning | Bacolor | 100 | Available | No results |

Use **Find jobs** to view the 8 sample tasks; they have no actionable offer form. No fake completed jobs or ratings are supplied. The mode is intended to test search/matching, not booking, tracking, email delivery or transactions.

The samples are bundled with code; deploy the updated build yourself to make this mode available on the live site. No live deployment or database seeding is performed automatically.

All sample providers and tasks use Bacolor, Pampanga, matching the target service area. Existing user-entered locations are not rewritten.
