(() => {
    const input = document.getElementById("reportTarget");
    const results = document.getElementById("reportProviderMatches");
    const status = document.getElementById("reportProviderStatus");
    if (!input || !results || !status) return;

    let timer;
    let request;

    function clear() {
        results.replaceChildren();
        results.hidden = true;
        status.textContent = "";
    }

    input.addEventListener("input", () => {
        clearTimeout(timer);
        request?.abort();
        clear();
        const term = input.value.trim();
        if (!term) return;

        timer = setTimeout(async () => {
            const current = new AbortController();
            request = current;
            try {
                const url = new URL(location.pathname, location.origin);
                url.searchParams.set("handler", "Providers");
                url.searchParams.set("q", term);
                const response = await fetch(url, { signal: current.signal });
                if (!response.ok || response.redirected) throw new Error("Search unavailable");
                const matches = await response.json();
                if (current.signal.aborted) return;
                status.textContent = matches.length
                    ? "Select a public freelancer profile, or enter the name yourself."
                    : "No matching public profiles. You can still enter the name.";
                for (const match of matches) {
                    const button = document.createElement("button");
                    button.type = "button";
                    const avatar = document.createElement("span");
                    avatar.className = "report-provider-avatar";
                    avatar.setAttribute("aria-hidden", "true");
                    avatar.textContent = match.name.charAt(0).toUpperCase();
                    const image = document.createElement("img");
                    image.alt = "";
                    image.src = "/ProfilePhoto?providerId=" + encodeURIComponent(match.id);
                    image.addEventListener("load", () => avatar.replaceChildren(image));
                    const name = document.createElement("strong");
                    name.textContent = match.name;
                    const service = document.createElement("small");
                    service.textContent = match.service;
                    const details = document.createElement("span");
                    details.append(name, service);
                    button.append(avatar, details);
                    button.addEventListener("click", () => {
                        input.value = match.name.slice(0, input.maxLength);
                        clear();
                        input.focus();
                    });
                    results.append(button);
                }
                results.hidden = !matches.length;
            } catch {
                if (!current.signal.aborted) status.textContent = "Search is unavailable. You can still enter the name.";
            }
        }, 220);
    });

    input.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        clearTimeout(timer);
        request?.abort();
        clear();
    });
})();
