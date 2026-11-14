const pendingForms = new Map();

document.addEventListener("submit", event => {
    if (pendingForms.has(event.target)) {
        event.preventDefault();
        event.stopImmediatePropagation();
    }
}, true);

document.addEventListener("submit", event => {
    const form = event.target;
    const button = event.submitter || form.querySelector('button[type="submit"], button:not([type]), input[type="submit"]');
    const method = button?.getAttribute("formmethod") || form.method;
    if (event.defaultPrevented || method.toLowerCase() !== "post" || button?.disabled || form.getAttribute("aria-busy") === "true") return;

    pendingForms.set(form, button);
    form.setAttribute("aria-busy", "true");
    if (button) {
        button.setAttribute("aria-disabled", "true");
        button.dataset.pendingLabel = form.dataset.submitPending || "Loading…";
    }
});

window.addEventListener("pageshow", () => {
    for (const [form, button] of pendingForms) {
        form.removeAttribute("aria-busy");
        if (button) {
            button.removeAttribute("aria-disabled");
            delete button.dataset.pendingLabel;
        }
    }
    pendingForms.clear();
});
