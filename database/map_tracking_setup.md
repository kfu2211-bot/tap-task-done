# Task map and freelancer location

No deployment or production database changes are performed by this integration.

1. On the application database, run `database/mysql_mobile_location.sql` once. It creates `mobile_location_updates` without replacing existing data. This table is shared by the existing mobile location API and the web tracker. Use the correct database connection/schema first.
2. No Google Cloud account, API key, Map ID, billing, or map environment variables are needed. MapLibre GL JS 5.6.1 and its license are bundled in wwwroot/lib/maplibre-gl. The map loads the OpenFreeMap Liberty style (https://tiles.openfreemap.org/styles/liberty), including its sources, glyphs and sprites, over the internet. Attribution stays visible on the map.
3. Restart the local app and hard-refresh Tracking (Ctrl+F5). If deploying later, include the bundled static assets. Test with two separate signed-in accounts: assigned freelancer clicks Start work, allows location permission, and keeps Tracking open. The assigned client opens the same task. A third account must not read or post coordinates for it.

The page sends coordinates at most once per 15 seconds while receiving device updates; the server limits web writes to one per 10 seconds per assigned freelancer/task. The client polls every 15 seconds. Only an accepted freelancer can send coordinates, and only while the task is in progress. Razor POST antiforgery protection applies. The server supplies the stored UTC timestamp. Location reads are private and uncached.

The freelancer can stop sharing. Leaving the page stops device watching; this is not background GPS tracking. Finishing/cancelling/disputing a job prevents further web writes; both viewers retain a clearly dated last known location. Updates older than 60 seconds are labeled Not live. Coordinates report device location and accuracy, not verified attendance; device permissions, signal and connectivity affect results.

Task addresses remain text and are not automatically geocoded. The map opens over the Philippines until a freelancer coordinate is received. It does not invent a task destination pin, route, or ETA. Coordinate history is stored in the table; establish an appropriate retention/cleanup policy before broad production rollout.

Map provider: https://openfreemap.org/quick_start/
Map library: https://maplibre.org/maplibre-gl-js/docs/

OpenFreeMap public map hosting needs no API key and currently has no map-view/request limits. Availability is not guaranteed. Browser WebGL support and internet connectivity are required. Our coordinate endpoint and database operate independently of the external map tiles. Remove obsolete GoogleMaps configuration if no other feature uses it.


## Destination and road routes

Run database/mysql_task_destinations.sql on the correct application database. Clients set and save an exact work destination by clicking the map in Tracking. Only the owning client can change it while assigned/in-progress; assigned participants can read it. The text address is not geocoded automatically. No destination is inferred from a client device GPS.

MapLibre displays the road route returned by an OSRM-compatible service. Development defaults to https://router.project-osrm.org for light testing. Production needs Routing__BaseUrl pointing to your own OSRM server or a provider permitting your production usage (base URL without /route/v1/driving). Without configuration, pins still work and the UI explains that road routing is unavailable. The public demo is not a guaranteed production service. Routes use the driving profile and no live traffic. Route distance is labeled as the value at calculation, not a constantly updated remaining distance.

Both endpoint coordinates are sent to the routing service from the viewing browser; disclose this to users and choose a suitable provider. Include this in the service privacy information before production rollout. An initial route is requested when fresh GPS and destination exist. A new route is requested after a destination change or an off-route deviation exceeding max(60m, 2x GPS accuracy). Requests are at least 30 seconds apart per page; fixes worse than 100m accuracy defer routing. Stale locations or ended jobs do not trigger rerouting. Failed/no-route responses clear obsolete lines and retry after the cooldown. No history trail, turn-by-turn navigation or live traffic is claimed.

OSRM API: https://project-osrm.org/docs/v26.6.1/http
Demo server policy: https://routing.openstreetmap.de/about.html


## Refresh and sharing consent

Sharing intent is saved in sessionStorage for the signed-in user and task in the current tab. Refreshing Tracking resumes device watching when the task remains in progress and browser permission permits it. Stop sharing or a permission error clears this intent; the initial shareLocation query flag is removed after use so Stop is not undone by refresh. Leaving the page stops the watcher; reopening the same task in the same tab resumes if intent is still on. Closing the tab clears tab-scoped intent. The header logout clears saved sharing intent. This is not background tracking.


## Map display and remaining route

The map has a fullscreen button with native fullscreen where supported and an expanded viewport fallback for mobile browsers. Exit with the same button or Escape. Routine legend, route summary, GPS metadata and sharing helper paragraphs are hidden; actionable errors and destination-edit instructions remain visible. Attribution stays inside the map.

An initial road route is shown to either participant even with an older or less accurate fix, labeled as last-known or approximate. Fresh accurate GPS is still required for automatic rerouting. The rendered line is trimmed to the freelancer projection along the route on each valid location update; passed segments disappear in both views. Progress does not move backwards on the same route due to GPS jitter. A newly calculated route resets progress. This is approximate navigation based on reported GPS, not verified road attendance.
