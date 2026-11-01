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
        const status=root.querySelector('[data-location-status]'), results=root.querySelector('[data-location-results]'), confirm=root.querySelector('[data-location-confirm]');
        let map, marker, candidate=lat.value&&lon.value&&valid(Number(lat.value),Number(lon.value)) ? [Number(lon.value),Number(lat.value)] : null;
        let searching=false, generation=0, mapFailed=false;
        if(candidate) status.textContent='Saved work destination. Change it only if needed, then confirm the new pin.';
        function invalidate() { lat.value='';lon.value=''; }
        function pick(point, label) {
            invalidate();
            if(!valid(point[1],point[0])) { status.textContent='Choose a work location in the Bacolor service area.';return; }
            candidate=point;
            if(label) address.value=label;
            if(!address.value.trim())address.value='Pinned location, Bacolor, Pampanga';
            if(map) {
                if(!marker) { marker=new maplibregl.Marker({color:'#dc3838',draggable:true}).setLngLat(point).addTo(map);marker.on('dragend',()=>{const p=marker.getLngLat();pick([p.lng,p.lat]);}); }
                else marker.setLngLat(point);
                map.jumpTo({center:point,zoom:16});
            }
            status.textContent='Check the marker and address, then press Confirm this pin.';
        }
        address.addEventListener('input',()=>{generation++;candidate=null;invalidate();results.replaceChildren();status.textContent='Address changed. Search again or select its exact pin on the map.';});
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
            navigator.geolocation.getCurrentPosition(position=>{button.disabled=false;pick([position.coords.longitude,position.coords.latitude],'Current-location pin, Bacolor, Pampanga');},()=>{button.disabled=false;status.textContent='Location access was denied or unavailable. You can still search or select a pin manually.';},{enableHighAccuracy:true,timeout:15000,maximumAge:0});
        });
        confirm.addEventListener('click',()=>{
            if(!candidate||!valid(candidate[1],candidate[0])||!address.value.trim()){status.textContent='Choose a valid work destination and address first.';return;}
            lat.value=candidate[1].toFixed(6);lon.value=candidate[0].toFixed(6);
            status.textContent='Work destination confirmed. This pin will be saved with your task or proposal.';
        });
        root.closest('form')?.addEventListener('submit',event=>{
            if(!lat.value||!lon.value||!valid(Number(lat.value),Number(lon.value))){event.preventDefault();status.textContent='Confirm the exact work destination before submitting.';root.scrollIntoView({behavior:'smooth',block:'center'});confirm.focus();}
        });
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
