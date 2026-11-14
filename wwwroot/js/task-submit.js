document.addEventListener("submit", event => {
    const form = event.target;
    if (!form.matches("form[data-submit-pending]") || event.defaultPrevented) return;
    if (form.dataset.submitting === "true") {
        event.preventDefault();
        return;
    }

    const button = event.submitter || form.querySelector("button");
    form.dataset.submitting = "true";
    form.setAttribute("aria-busy", "true");
    if (button) {
        button.dataset.idleLabel = button.textContent;
        button.textContent = form.dataset.submitPending;
        button.disabled = true;
    }
});

window.addEventListener("pageshow", () => {
    document.querySelectorAll("form[data-submitting]").forEach(form => {
        delete form.dataset.submitting;
        form.removeAttribute("aria-busy");
        const button = form.querySelector("[data-idle-label]");
        if (button) {
            button.textContent = button.dataset.idleLabel;
            delete button.dataset.idleLabel;
            button.disabled = false;
        }
    });
});
