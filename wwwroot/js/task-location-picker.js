(() => {
    'use strict';
    const valid = (lat, lon) => Number.isFinite(lat) && Number.isFinite(lon) && lat >= 14.92 && lat <= 15.10 && lon >= 120.54 && lon <= 120.73;
    async function search(query) {
        const response = await fetch('/api/places/search?q=' + encodeURIComponent(query), { credentials:'same-origin', cache:'no-store' });
        if(response.redirected || response.status===401)throw new Error('Sign in again to search locations.');
        if(response.status===429)throw new Error('Too many searches. Try again shortly.');
        const result = await response.json();
        if (!response.ok) throw new Error(result.message || 'Location search is unavailable.');
        return result;
    }
    document.querySelectorAll('[data-location-picker]').forEach(root => {
        const address=root.querySelector('[data-location-address]'), lat=root.querySelector('[data-location-latitude]'), lon=root.querySelector('[data-location-longitude]');
        const status=root.querySelector('[data-location-status]'), results=root.querySelector('[data-location-results]'), confirm=root.querySelector('[data-location-confirm]'), notice=root.querySelector('[data-location-alert]');
        let map, marker, candidate=lat.value&&lon.value&&valid(Number(lat.value),Number(lon.value)) ? [Number(lon.value),Number(lat.value)] : null;
        let searching=false, generation=0, mapFailed=false, searchTimer, reverseGeneration=0, reverseController;
        const areas=['Solana Casa Real','Cabalantian','Parulog','Cabetican','Magliman'];
        if(candidate) { root.classList.add('location-confirmed');confirm.textContent='Pin confirmed';status.textContent='Saved work destination. Change it only if needed, then confirm the new pin.'; }
        function invalidate() { lat.value='';lon.value='';root.classList.remove('location-confirmed');confirm.textContent='Confirm this pin'; }
        function warn(message) { notice.textContent=message;notice.hidden=false;root.classList.add('location-needs-confirmation'); }
        function useMapAddress(point, message) {
            const center = map?.project(point);
            if (center && map.loaded()) {
                const features = map.queryRenderedFeatures([
                    [center.x - 28, center.y - 28],
                    [center.x + 28, center.y + 28]
                ]);
                const nearby = features.find(feature =>
                    ['transportation_name', 'poi'].includes(feature.sourceLayer) &&
                    typeof feature.properties?.name === 'string' && feature.properties.name.trim());
                if (nearby) {
                    address.value = ('Near ' + nearby.properties.name + ', Bacolor, Pampanga').slice(0, 120);
                    status.textContent = 'Nearby map label suggested. Check or edit the address before confirming.';
                    return;
                }
            }
            status.textContent = message;
        }
        function pick(point, label) {
            invalidate();
            reverseController?.abort();
            generation++; clearTimeout(searchTimer); results.replaceChildren();
            if(!valid(point[1],point[0])) { candidate=null; reverseGeneration++; status.textContent='Choose a work location in the Bacolor service area.';return; }
            candidate=point;
            if(label) { reverseGeneration++; address.value=label; status.textContent='Check the marker and address, then press Confirm this pin.'; }
            else {
                const current=++reverseGeneration; address.value='';
                reverseController=new AbortController();
                const controller=reverseController, timeout=setTimeout(()=>controller.abort(),12000);
                status.textContent='Finding the address for this pin…';
                fetch('/api/places/reverse?lat='+encodeURIComponent(point[1])+'&lon='+encodeURIComponent(point[0]),{credentials:'same-origin',cache:'no-store',signal:controller.signal})
                    .then(r=>{
                        if(r.redirected || r.status===401) throw new Error('Sign in again to look up the address.');
                        if(r.status===429) throw new Error('Too many lookups. Wait a moment, then select the pin again.');
                        if(!r.ok) throw new Error('Reverse lookup failed: ' + r.status);
                        return r.json();
                    })
                    .then(place=>{if(current!==reverseGeneration)return; if(place?.label){address.value=place.label;status.textContent='Approximate address found. Check it and confirm the pin.';}else useMapAddress(point, 'No address found for this pin. Enter a nearby landmark or the work address, then confirm.');})
                    .catch(error=>{
                        if(current!==reverseGeneration)return;
                        if(error instanceof Error && !controller.signal.aborted &&
                            (error.message.startsWith('Sign in') || error.message.startsWith('Too many'))) {
                            status.textContent=error.message;
                            return;
                        }
                        useMapAddress(point, 'Address lookup unavailable. Enter the work address or a nearby landmark, then confirm.');
                    })
                    .finally(()=>clearTimeout(timeout));
            }
            if(map) {
                if(!marker) { marker=new maplibregl.Marker({color:'#dc3838',draggable:true}).setLngLat(point).addTo(map);marker.on('dragend',()=>{const p=marker.getLngLat();pick([p.lng,p.lat]);}); }
                else marker.setLngLat(point);
                map.jumpTo({center:point,zoom:16});
            }
        }
        address.addEventListener('input',()=>{generation++;reverseGeneration++;reverseController?.abort();invalidate();results.replaceChildren();clearTimeout(searchTimer);const query=address.value.trim();if(candidate)status.textContent='Address edited. Check the pin and confirm your work location.';if(!query)return;for(const name of areas.filter(x=>x.toLowerCase().includes(query.toLowerCase()))){const b=document.createElement('button');b.type='button';b.textContent=name+', Bacolor, Pampanga';b.addEventListener('click',()=>{address.value=b.textContent;results.replaceChildren();root.querySelector('[data-location-search]').click();});results.append(b);}if(query.length>=3)searchTimer=setTimeout(()=>root.querySelector('[data-location-search]').click(),700);});
        root.querySelector('[data-location-search]').addEventListener('click',async event=>{
            const query=address.value.trim();if(query.length<3){status.textContent='Enter an address or landmark with at least 3 characters.';return;}
            if(searching)return;searching=true;event.currentTarget.disabled=true;const request=++generation;
            status.textContent='Searching the Bacolor area…';results.replaceChildren();
            try {
                const places=await search(query);if(request!==generation)return;
                status.textContent=places.length?'Choose the correct result, then check its pin.':'No matching place found. Try a nearby landmark, current location, or place the pin manually.';
                for(const p of places) {const b=document.createElement('button');b.type='button';b.textContent=p.label;b.addEventListener('click',()=>{pick([p.longitude,p.latitude],p.label);results.replaceChildren();});results.append(b);}
            } catch(error){if(request===generation)status.textContent=error.message+' Use current location or select the pin manually.';}
            finally{searching=false;root.querySelector('[data-location-search]').disabled=false;}
        });
        root.querySelector('[data-location-current]').addEventListener('click',event=>{
            if(!navigator.geolocation){status.textContent='Current location is unavailable. Search or place the pin manually.';return;}
            const button=event.currentTarget;button.disabled=true;status.textContent='Requesting your current location. Allow access if this is the work site.';
            navigator.geolocation.getCurrentPosition(position=>{button.disabled=false;pick([position.coords.longitude,position.coords.latitude]);},()=>{button.disabled=false;status.textContent='Location access was denied or unavailable. You can still search or select a pin manually.';},{enableHighAccuracy:true,timeout:15000,maximumAge:0});
        });
        confirm.addEventListener('click',()=>{
            if(!candidate||!valid(candidate[1],candidate[0])||!address.value.trim()){warn('Choose a location on the map or search for an address first, then confirm the pin.');return;}
            lat.value=candidate[1].toFixed(6);lon.value=candidate[0].toFixed(6);
            notice.hidden=true;root.classList.remove('location-needs-confirmation');root.classList.add('location-confirmed');confirm.textContent='Pin confirmed';
            status.textContent='Work destination confirmed. This pin will be saved with your task or proposal.';
        });
        root.closest('form')?.addEventListener('submit',event=>{
            if(!lat.value||!lon.value||!valid(Number(lat.value),Number(lon.value))){event.preventDefault();warn('Please select your work location and confirm the pin before continuing.');root.scrollIntoView({behavior:'smooth',block:'center'});confirm.focus({preventScroll:true});}
        });
        address.addEventListener('invalid',()=>warn('Enter the work address, select its location and confirm the pin before continuing.'));
        function init() {
            if(map||mapFailed||!root.getBoundingClientRect().width)return;
            if(!window.maplibregl){mapFailed=true;status.textContent='Map could not load. You can still search or use current location and confirm the result.';return;}
            try {
                map=new maplibregl.Map({container:root.querySelector('[data-location-map]'),style:'https://tiles.openfreemap.org/styles/liberty',center:candidate||[120.653,15.000],zoom:candidate?16:13});
                map.addControl(new maplibregl.NavigationControl({showCompass:false}));map.addControl(new maplibregl.FullscreenControl());
                map.on('click',e=>pick([e.lngLat.lng,e.lngLat.lat]));
                map.on('error',()=>{status.textContent=(lat.value&&lon.value?'Your destination pin is still confirmed. ':'')+'Some map details could not load. You can search or use current location instead.';});
                if(candidate){marker=new maplibregl.Marker({color:'#dc3838',draggable:true}).setLngLat(candidate).addTo(map);marker.on('dragend',()=>{const p=marker.getLngLat();pick([p.lng,p.lat]);});}
                new ResizeObserver(()=>map.resize()).observe(root);
            }catch{mapFailed=true;status.textContent='Interactive map unavailable. Search or use current location, then confirm its result.';}
        }
        const observer=new IntersectionObserver(entries=>{if(entries.some(e=>e.isIntersecting))init();});observer.observe(root);
    });
    // Search on Tracking only pans the map; changing the destination requires a new agreement.
    document.querySelectorAll('[data-job-location]').forEach(root=>root.addEventListener('job-map-ready',event=>{
        const map=event.detail.map, panel=root.querySelector('[data-map-search]');if(!panel)return;
        const input=panel.querySelector('input'), results=panel.querySelector('.location-search-results'), status=panel.querySelector('[role=status]');let marker;
        panel.querySelector('button').addEventListener('click',async()=>{
            if(input.value.trim().length<3){status.textContent='Enter an address or landmark.';return;}
            const button=panel.querySelector('button');button.disabled=true;results.replaceChildren();status.textContent='Searching…';
            try{const places=await search(input.value.trim());status.textContent=places.length?'Select a result to view it. The agreed destination is unchanged.':'No results. Try a nearby landmark.';
                for(const p of places){const b=document.createElement('button');b.type='button';b.textContent=p.label;b.addEventListener('click',()=>{marker?.remove();marker=new maplibregl.Marker({color:'#3282b8'}).setLngLat([p.longitude,p.latitude]).addTo(map);map.flyTo({center:[p.longitude,p.latitude],zoom:16});results.replaceChildren();});results.append(b);}
            }catch(error){status.textContent=error.message;}finally{button.disabled=false;}
        });
    }));
})();
