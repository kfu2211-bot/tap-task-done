(() => {
    'use strict';
    const root = document.querySelector('[data-job-location]');
    if (!root) return;
    const status = root.querySelector('[data-location-status]');
    const mapStatus = root.querySelector('[data-map-status]');
    const sharingStatus = root.querySelector('[data-sharing-status]');
    const share = root.querySelector('[data-share]');
    const stop = root.querySelector('[data-stop]');
    const routeStatus = root.querySelector('[data-route-status]');
    const pick = root.querySelector('[data-pick-destination]');
    const saveDestination = root.querySelector('[data-save-destination]');
    const cancelDestination = root.querySelector('[data-cancel-destination]');
    let destination, destinationMarker, candidate, editing = false, route, routing = false, lastRouteAt = 0, routeDestination = '', routeGeneration = 0;
    let routeProgress = 0, remainingCoordinates = null;
    const taskId = Number(root.dataset.taskId);
    const consentKey = 'tts-location-sharing:' + root.dataset.viewerId + ':' + taskId;
    const consent = () => { try { return sessionStorage.getItem(consentKey); } catch { return null; } };
    const remember = value => { try { sessionStorage.setItem(consentKey, value); } catch { /* Browser storage may be disabled. */ } };
    const endpoint = `/Tracking?handler=Location&taskId=${taskId}`;
    let active = root.dataset.active === 'true';
    let map, marker, latest, watchId = null, lastSent = 0, sending = false, disposed = false, pollTimer, sampleTimer;
    let centered = false, sharing = false, recenterButton;
    let following = root.dataset.freelancer === "true", previousPosition, heading = 0;
    function setStatus(element, text, visible = false) {
        if (!element) return;
        element.textContent = text;
        element.hidden = !visible || !text;
    }
    const message = (text, visible = false) => setStatus(sharingStatus, text, visible);
    function stopSharing(text, preserveConsent = false) {
        if (!preserveConsent) remember("off");
        sharing = false;
        clearInterval(sampleTimer);
        if (watchId !== null) navigator.geolocation.clearWatch(watchId);
        watchId = null;
        if (share) { share.hidden = !active; share.disabled = false; }
        if (stop) stop.hidden = true;
        message(text, /denied|unavailable|session ended/.test(text));
    }
    function render() {
        if (recenterButton) {
            recenterButton.disabled = !latest;
            recenterButton.title = latest ? (following ? 'Following freelancer. Drag map to explore.' : 'Recenter and follow freelancer') : 'Waiting for freelancer location';
            recenterButton.setAttribute('aria-pressed', String(following));
        }
        if (!latest) {
            setStatus(status, active ? 'No location shared yet. Waiting for the freelancer.' : 'Location sharing starts when the freelancer starts work.');
            return;
        }
        const date = new Date(latest.recordedAtUtc.endsWith('Z') ? latest.recordedAtUtc : latest.recordedAtUtc + 'Z');
        const fresh = active && Date.now() - date.getTime() < 60000;
        setStatus(status, `${fresh ? 'Recent freelancer location' : 'Last known freelancer location'} · ${date.toLocaleString()}${latest.accuracyMeters == null ? '' : ` · accuracy ±${Math.round(latest.accuracyMeters)} m`}${fresh ? '' : ' · Not live'}`);
        if (!map) return;
        const position = [latest.longitude, latest.latitude];
        if (!marker) {
            const popup = new maplibregl.Popup({ offset: 28 }).setText('Freelancer — last reported location');
            const car = document.createElement('div');
            car.className = 'job-freelancer-car';
            car.innerHTML = '<svg viewBox="0 0 40 60" width="36" height="54" aria-hidden="true"><ellipse cx="20" cy="32" rx="17" ry="26" fill="#193e30" opacity=".16"/><rect x="3" y="13" width="5" height="12" rx="2" fill="#193e30"/><rect x="32" y="13" width="5" height="12" rx="2" fill="#193e30"/><rect x="3" y="38" width="5" height="12" rx="2" fill="#193e30"/><rect x="32" y="38" width="5" height="12" rx="2" fill="#193e30"/><rect x="7" y="3" width="26" height="52" rx="9" fill="#19765a" stroke="white" stroke-width="2.5"/><path d="M11 16 Q20 11 29 16 L27 26 H13Z" fill="#d8f1e7"/><rect x="12" y="28" width="16" height="12" rx="3" fill="#e7f0e5"/><path d="M13 43 H27 L29 48 Q20 51 11 48Z" fill="#193e30"/><path d="M11 7h5m8 0h5" stroke="#fff7d1" stroke-width="3"/><path d="M10 51h6m8 0h6" stroke="#ef6767" stroke-width="2"/></svg>';
            marker = new maplibregl.Marker({ element: car, rotationAlignment: 'map', pitchAlignment: 'map' }).setLngLat(position).setPopup(popup).addTo(map);
            marker.getElement().setAttribute('aria-label', 'Freelancer — last reported location');
        } else marker.setLngLat(position);
        const moved = !previousPosition || position[0] !== previousPosition[0] || position[1] !== previousPosition[1];
        if (previousPosition && moved) {
            const lat1 = previousPosition[1] * Math.PI / 180, lat2 = position[1] * Math.PI / 180;
            const delta = (position[0] - previousPosition[0]) * Math.PI / 180;
            const meters = Math.hypot((position[0] - previousPosition[0]) * 111320 * Math.cos(lat1), (position[1] - previousPosition[1]) * 111320);
            if (meters > Math.max(10, latest.accuracyMeters || 0)) {
                heading = (Math.atan2(Math.sin(delta) * Math.cos(lat2), Math.cos(lat1) * Math.sin(lat2) - Math.sin(lat1) * Math.cos(lat2) * Math.cos(delta)) * 180 / Math.PI + 360) % 360;
            }
        }
        marker.setRotation(heading);
        if (following && fresh && moved && !editing) followFreelancer();
        else if (!centered) { map.jumpTo({ center: position, zoom: 15 }); centered = true; }
        previousPosition = position;
        drawRoute();
        updateRoute();
    }
    async function poll() {
        try {
            const response = await fetch(endpoint, { cache: 'no-store', headers: { Accept: 'application/json' } });
            if (!response.ok || response.redirected) throw new Error('Unable to load location. Check your connection or sign in again.');
            const result = await response.json();
            active = result.active;
            latest = result.location;
            if (!active) stopSharing('Sharing is stopped because this job is not in progress.');
            await loadDestination();
            render();
        } catch (error) { setStatus(status, error.message, true); }
        if (!disposed) pollTimer = setTimeout(poll, 15000);
    }
    async function send(position) {
        if (!active || !sharing || sending || disposed || Date.now() - lastSent < 15000) return;
        sending = true;
        lastSent = Date.now();
        try {
            const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
            const response = await fetch(endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
                body: JSON.stringify({ taskId, latitude: position.coords.latitude, longitude: position.coords.longitude, accuracyMeters: position.coords.accuracy }) });
            if (response.status === 404 || response.redirected) { stopSharing('Sharing stopped. The job changed or your session ended.'); return; }
            if (response.status === 429) return;
            if (!response.ok) throw new Error('Location could not be saved. Check your connection and try sharing again.');
            latest = { latitude: position.coords.latitude, longitude: position.coords.longitude, accuracyMeters: position.coords.accuracy, recordedAtUtc: new Date().toISOString() };
            render();
            if (sharing) message('Sharing your location with the assigned client. Updates about every 15 seconds.');
        } catch (error) { message(error.message, true); }
        finally { sending = false; }
    }
    function startSharing() {
        if (!active || sharing || disposed) return;
        if (!window.isSecureContext || !navigator.geolocation) { message('Location sharing requires HTTPS and browser location access.', true); return; }
        sharing = true;
        remember("on");
        share.hidden = true;
        stop.hidden = false;
        message('Waiting for location permission and a GPS fix…');
        watchId = navigator.geolocation.watchPosition(send, error => {
            stopSharing(error.code === 1 ? 'Location access was denied. Allow location in your browser settings, then try again.' : 'Your location is unavailable. Check device location settings and try again.');
        }, { enableHighAccuracy: true, maximumAge: 0, timeout: 20000 });
        sampleTimer = setInterval(() => {
            if (active && sharing && !sending) navigator.geolocation.getCurrentPosition(send,
                () => message('Waiting for a fresh location fix. The map shows the last reported position.'),
                { enableHighAccuracy: true, maximumAge: 0, timeout: 12000 });
        }, 15000);
    }
    share?.addEventListener('click', startSharing);
    stop?.addEventListener('click', () => stopSharing('Location sharing stopped. The client can still see the last reported location and its timestamp.'));
    window.addEventListener('pagehide', () => { disposed = true; clearTimeout(pollTimer); stopSharing('Location sharing stopped.', true); });
    window.addEventListener('pageshow', event => { if (event.persisted) { disposed = false; poll(); if (share && active && consent() === 'on') startSharing(); } });

    function showDestination(value) {
        if (!map || !value) return;
        const point = [value.longitude, value.latitude];
        if (!destinationMarker) destinationMarker = new maplibregl.Marker({ color: '#dc3838' })
            .setLngLat(point).setPopup(new maplibregl.Popup({ offset: 28 }).setText('Client-selected work destination')).addTo(map);
        else destinationMarker.setLngLat(point);
    }
    function trimTravelledRoute() {
        const coordinates = route?.geometry?.coordinates;
        if (!coordinates?.length) { remainingCoordinates = null; routeProgress = 0; return []; }
        if (!latest || !active || (latest.accuracyMeters || 0) > 100) return remainingCoordinates || coordinates;
        const stamp = new Date(latest.recordedAtUtc.endsWith('Z') ? latest.recordedAtUtc : latest.recordedAtUtc + 'Z').getTime();
        if (Date.now() - stamp > 60000) return remainingCoordinates || coordinates;
        const point = [latest.longitude, latest.latitude];
        const scaleX = 111320 * Math.cos(point[1] * Math.PI / 180), scaleY = 111320;
        let bestDistance = Infinity, bestProgress = routeProgress, projection;
        for (let i = Math.floor(routeProgress); i < coordinates.length - 1; i++) {
            const a = coordinates[i], b = coordinates[i + 1];
            const ax = (a[0] - point[0]) * scaleX, ay = (a[1] - point[1]) * scaleY;
            const dx = (b[0] - a[0]) * scaleX, dy = (b[1] - a[1]) * scaleY;
            const length = dx * dx + dy * dy;
            const t = Math.max(0, routeProgress - i, Math.min(1, length ? -(ax * dx + ay * dy) / length : 0));
            const distance = Math.hypot(ax + t * dx, ay + t * dy);
            if (distance < bestDistance) {
                bestDistance = distance; bestProgress = i + t;
                projection = [a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])];
            }
        }
        if (projection && bestDistance <= Math.max(60, (latest.accuracyMeters || 0) * 2)) {
            routeProgress = bestProgress;
            remainingCoordinates = [projection, ...coordinates.slice(Math.min(Math.floor(bestProgress) + 1, coordinates.length - 1))];
        }
        return remainingCoordinates || coordinates;
    }
    function drawRoute() {
        if (!map || !map.isStyleLoaded()) return;
        const data = { type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: trimTravelledRoute() } };
        if (map.getSource('job-route')) map.getSource('job-route').setData(data);
        else {
            map.addSource('job-route', { type: 'geojson', data });
            map.addLayer({ id: 'job-route', type: 'line', source: 'job-route', layout: { 'line-join': 'round', 'line-cap': 'round' }, paint: { 'line-color': '#19765a', 'line-width': 5, 'line-opacity': 0.85 } });
        }
    }
    function distanceToRoute(point, coordinates) {
        const scaleX = 111320 * Math.cos(point[1] * Math.PI / 180), scaleY = 111320;
        let best = Infinity;
        for (let i = 1; i < coordinates.length; i++) {
            const a = [(coordinates[i - 1][0] - point[0]) * scaleX, (coordinates[i - 1][1] - point[1]) * scaleY];
            const b = [(coordinates[i][0] - point[0]) * scaleX, (coordinates[i][1] - point[1]) * scaleY];
            const dx = b[0] - a[0], dy = b[1] - a[1], length = dx * dx + dy * dy;
            const t = length ? Math.max(0, Math.min(1, -(a[0] * dx + a[1] * dy) / length)) : 0;
            best = Math.min(best, Math.hypot(a[0] + t * dx, a[1] + t * dy));
        }
        return best;
    }
    async function loadDestination() {
        try {
            const response = await fetch('/Tracking?handler=Destination&taskId=' + taskId, { cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error();
            const value = await response.json();
            const changed = JSON.stringify(value) !== JSON.stringify(destination);
            destination = value;
            if (changed) { routeGeneration++; route = null; routeDestination = ''; drawRoute(); }
            if (!editing) showDestination(destination);
            if (!destination && !editing) setStatus(routeStatus, pick ? 'Set the exact destination pin to show a road route.' : 'Waiting for the client to set the work destination pin.');
            else if (destination && !latest && !editing) setStatus(routeStatus, 'Destination saved. Waiting for the freelancer’s location to calculate a route.');
        } catch { setStatus(routeStatus, 'Unable to load destination. Please check destination storage and try again.', true); }
    }
    async function updateRoute() {
        if (!map || !latest || !destination || routing || editing) return;
        const base = root.dataset.routeUrl;
        if (!base) { setStatus(routeStatus, 'Road routing is not configured for this environment.', true); return; }
        const stamp = new Date(latest.recordedAtUtc.endsWith('Z') ? latest.recordedAtUtc : latest.recordedAtUtc + 'Z').getTime();
        const staleLocation = !active || Date.now() - stamp > 60000;
        const approximateLocation = (latest.accuracyMeters || 0) > 100;
        // A client opening the page still needs an initial route from the last known position.
        // Only automatic rerouting waits for a fresh, accurate fix.
        if (route && (staleLocation || approximateLocation)) {
            setStatus(routeStatus, staleLocation ? 'Last known route — location is not live.' : 'Approximate route — waiting for accurate GPS.', true);
            return;
        }
        const key = destination.longitude + ',' + destination.latitude;
        const offRoute = route && distanceToRoute([latest.longitude, latest.latitude], remainingCoordinates || route.geometry.coordinates) > Math.max(60, (latest.accuracyMeters || 0) * 2);
        if (Date.now() - lastRouteAt < 30000) return;
        if (route && key === routeDestination && !offRoute) return;
        routing = true;
        lastRouteAt = Date.now();
        const generation = routeGeneration;
        setStatus(routeStatus, route ? 'Route changed. Recalculating…' : 'Calculating road route…');
        try {
            const coords = latest.longitude + ',' + latest.latitude + ';' + key;
            const response = await fetch((base.endsWith('/') ? base.slice(0, -1) : base) + '/route/v1/driving/' + coords + '?overview=full&geometries=geojson', { signal: AbortSignal.timeout(12000) });
            if (!response.ok) throw new Error();
            const result = await response.json();
            if (generation !== routeGeneration || disposed) return;
            if (result.code !== 'Ok' || !result.routes?.[0]?.geometry?.coordinates?.length) throw new Error();
            route = result.routes[0]; routeDestination = key; routeProgress = 0; remainingCoordinates = null;
            drawRoute();
            setStatus(routeStatus, staleLocation ? 'Last known route — location is not live.' : approximateLocation ? 'Approximate route — waiting for accurate GPS.' : '', staleLocation || approximateLocation);
            if (!following && !root.dataset.routeFitted) {
                const bounds = new maplibregl.LngLatBounds();
                route.geometry.coordinates.forEach(point => bounds.extend(point));
                map.fitBounds(bounds, { padding: 45, maxZoom: 16, duration: 500 });
                root.dataset.routeFitted = 'true';
            }
        } catch { if (generation === routeGeneration) { route = null; drawRoute(); setStatus(routeStatus, 'Route unavailable. Retrying shortly.', true); } }
        finally { routing = false; }
    }
    function endEditing() {
        editing = false; candidate = null;
        saveDestination.hidden = true; cancelDestination.hidden = true; pick.disabled = false;
        if (destination) showDestination(destination);
        else if (destinationMarker) { destinationMarker.remove(); destinationMarker = null; }
        updateRoute();
    }
    pick?.addEventListener('click', () => {
        pauseFollowing();
        editing = true; pick.disabled = true; cancelDestination.hidden = false;
        setStatus(routeStatus, 'Click the exact work location on the map, then Save destination.', true);
    });
    cancelDestination?.addEventListener('click', () => { endEditing(); setStatus(routeStatus, 'Destination selection cancelled.'); });
    saveDestination?.addEventListener('click', async () => {
        if (!candidate) return;
        saveDestination.disabled = true;
        try {
            const response = await fetch('/Tracking?handler=Destination', { method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: root.querySelector('input[name="__RequestVerificationToken"]').value }, body: JSON.stringify({ taskId, ...candidate }) });
            if (!response.ok || response.redirected) throw new Error();
            destination = await response.json(); routeGeneration++; route = null; lastRouteAt = 0; delete root.dataset.routeFitted; drawRoute();
            endEditing(); setStatus(routeStatus, 'Work destination saved.'); updateRoute();
        } catch { setStatus(routeStatus, 'Destination could not be saved. Check your session and destination storage.', true); }
        finally { saveDestination.disabled = false; }
    });
    function followFreelancer() {
        if (!map || !latest) return;
        centered = true;
        map.easeTo({ center: [latest.longitude, latest.latitude], zoom: 17, pitch: 45, bearing: heading, duration: 800 });
    }
    function pauseFollowing() {
        following = false;
        root.dataset.routeFitted = 'true';
        if (recenterButton) {
            recenterButton.setAttribute('aria-pressed', 'false');
            recenterButton.title = 'Recenter and follow freelancer';
        }
    }
    function initializeMap() {
        if (!window.maplibregl) {
            setStatus(mapStatus, 'Map library could not load. Refresh the page to try again. Location sharing can still be used.', true);
            return;
        }
        try {
            map = new maplibregl.Map({
                container: root.querySelector('[data-map]'),
                style: 'https://tiles.openfreemap.org/styles/liberty',
                center: [121.774, 12.8797], zoom: 5,
                attributionControl: { customAttribution: '<a href="https://project-osrm.org/" target="_blank" rel="noopener noreferrer">OSRM</a> · <a href="https://www.openstreetmap.org/" target="_blank" rel="noopener noreferrer">Fix map</a>' }
            });
            map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');
            map.addControl({
                onAdd(currentMap) {
                    const container = document.createElement('div');
                    container.className = 'maplibregl-ctrl maplibregl-ctrl-group job-map-fullscreen-control';
                    const button = document.createElement('button');
                    button.type = 'button';
                    const canvas = root.querySelector('[data-map]');
                    let expanded = false, previousOverflow = '';
                    const update = value => {
                        if (value && !expanded) previousOverflow = document.body.style.overflow;
                        const wasExpanded = expanded;
                        expanded = value;
                        canvas.classList.toggle('job-map-expanded', expanded);
                        if (expanded || wasExpanded) document.body.style.overflow = expanded ? 'hidden' : previousOverflow;
                        button.setAttribute('aria-label', expanded ? 'Exit fullscreen map' : 'Fullscreen map');
                        button.title = expanded ? 'Exit fullscreen map' : 'Fullscreen map';
                        button.setAttribute('aria-pressed', String(expanded));
                        button.innerHTML = expanded
                            ? '<svg viewBox="0 0 24 24" width="19" height="19" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M9 3v6H3m18 0h-6V3M3 15h6v6m6 0v-6h6"/></svg>'
                            : '<svg viewBox="0 0 24 24" width="19" height="19" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M9 3H3v6m12-6h6v6M3 15v6h6m12-6v6h-6"/></svg>';
                        requestAnimationFrame(() => currentMap.resize());
                    };
                    this.onFullscreen = () => { if (!document.fullscreenElement) update(false); };
                    this.onKeydown = event => { if (event.key === 'Escape' && expanded && !document.fullscreenElement) update(false); };
                    button.addEventListener('click', async () => {
                        if (expanded) {
                            if (document.fullscreenElement && document.exitFullscreen) await document.exitFullscreen().catch(() => {});
                            update(false);
                        } else {
                            update(true);
                            if (canvas.requestFullscreen) await canvas.requestFullscreen().catch(() => {});
                            currentMap.resize();
                        }
                    });
                    document.addEventListener('fullscreenchange', this.onFullscreen);
                    document.addEventListener('keydown', this.onKeydown);
                    container.append(button);
                    this.container = container;
                    this.restore = () => { if (expanded) update(false); };
                    update(false);
                    return container;
                },
                onRemove() {
                    document.removeEventListener('fullscreenchange', this.onFullscreen);
                    document.removeEventListener('keydown', this.onKeydown);
                    this.restore(); this.container.remove();
                }
            }, 'top-right');
            map.addControl({
                onAdd(currentMap) {
                    const container = document.createElement('div');
                    container.className = 'maplibregl-ctrl maplibregl-ctrl-group job-map-recenter';
                    recenterButton = document.createElement('button');
                    recenterButton.type = 'button';
                    recenterButton.disabled = !latest;
                    recenterButton.title = 'Waiting for freelancer location';
                    recenterButton.setAttribute('aria-label', 'Recenter on freelancer location');
                    recenterButton.innerHTML = '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><circle cx="12" cy="12" r="7"/><circle cx="12" cy="12" r="2"/><path d="M12 1v4m0 14v4M1 12h4m14 0h4"/></svg>';
                    recenterButton.addEventListener('click', () => {
                        if (!latest) return;
                        centered = true;
                        root.dataset.routeFitted = 'true';
                        following = true;
                        recenterButton.setAttribute('aria-pressed', 'true');
                        recenterButton.title = 'Following freelancer. Drag map to explore.';
                        followFreelancer();
                    });
                    container.append(recenterButton);
                    this.container = container;
                    return container;
                },
                onRemove() { this.container.remove(); recenterButton = null; }
            }, 'top-right');
            map.on('dragstart', pauseFollowing);
            map.on('zoomstart', event => { if (event.originalEvent) pauseFollowing(); });
            map.on('rotatestart', event => { if (event.originalEvent) pauseFollowing(); });
            map.on('click', event => {
                if (!editing) return;
                candidate = { latitude: event.lngLat.lat, longitude: event.lngLat.lng };
                showDestination(candidate);
                saveDestination.hidden = false;
                setStatus(routeStatus, 'Destination preview. Save to share this pin with the freelancer.', true);
            });
            map.on('load', () => {
                setStatus(mapStatus, 'Car: freelancer · Red pin: work destination · Green line: suggested driving route.');
                render();
                map.resize();
                drawRoute();
            });
            map.on('error', () => {
                setStatus(mapStatus, 'Some map details could not load. Check your internet connection. Location sharing is separate from the map.', true);
            });
            render();
        } catch {
            map = null;
            setStatus(mapStatus, 'Map unavailable. Enable WebGL or hardware acceleration in your browser, then refresh. Location sharing can still be used.', true);
        }
    }
    initializeMap();
    poll();
    const autoStart = new URLSearchParams(location.search).get('shareLocation') === '1';
    if (autoStart) {
        const url = new URL(location.href); url.searchParams.delete('shareLocation');
        history.replaceState(history.state, '', url);
    }
    if (share && (consent() === 'on' || (autoStart && consent() !== 'off'))) startSharing();
})();
