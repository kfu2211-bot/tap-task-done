const workRole = document.getElementById("WorkRole");
const freelancerPreferences = document.getElementById("freelancerPreferences");

if (workRole && freelancerPreferences) {
    const form = workRole.form;
    // Validate before agreements or navigation, keeping the entire draft on screen.
    form.noValidate = true;
    const errorSummary = document.getElementById("workPreferencesErrors");
    let attemptedSave = !errorSummary.hidden;
    const reveal = field => {
        for (let parent = field.parentElement; parent; parent = parent.parentElement) {
            if (parent instanceof HTMLDetailsElement) parent.open = true;
        }
        field.focus();
        field.scrollIntoView({ block: "center", behavior: "auto" });
    };
    function validateDraft(focus = false) {
        form.querySelectorAll(".preference-field-error").forEach(node => node.remove());
        form.querySelectorAll('[aria-invalid="true"]').forEach(field => {
            field.removeAttribute("aria-invalid");
            const ids = (field.getAttribute("aria-describedby") || "").split(" ").filter(id => !id.startsWith("preference-error-"));
            if (ids.length) field.setAttribute("aria-describedby", ids.join(" "));
            else field.removeAttribute("aria-describedby");
        });
        const errors = [];
        const add = (field, message, container = field.closest("label") || field.parentElement) => {
            errors.push({ field, message });
            const note = document.createElement("p");
            note.id = `preference-error-${errors.length}`;
            note.className = "preference-field-error";
            note.textContent = message;
            container.append(note);
            field.setAttribute("aria-invalid", "true");
            field.setAttribute("aria-describedby", `${field.getAttribute("aria-describedby") || ""} ${note.id}`.trim());
        };
        if (workRole.value !== "client") {
            for (const [name, message, selector] of [
                ["Services", "Choose at least one service you offer.", ".service-picker"],
                ["Areas", "Choose at least one service area.", ".service-area-picker"]
            ]) {
                if (!form.querySelector(`input[name="${name}"]:checked`)) add(form.querySelector(`input[name="${name}"]`), message, form.querySelector(selector));
            }
            for (const input of freelancerPreferences.querySelectorAll("input:not([type=checkbox]), select")) {
                if (input.closest("[hidden]")) continue;
                if (!input.validity.valid) add(input, input.validationMessage);
            }
            if (otherInput.required && otherInput.value.includes(",")) add(otherInput, "Use a service name without commas.");
            if (otherInput.required && otherInput.value && !otherInput.value.trim()) add(otherInput, "Enter your service name.");
            for (const input of form.querySelectorAll(".specialty-custom:not([hidden]) input")) {
                if (!input.closest("[hidden]") && input.value && !input.value.trim()) add(input, "Enter your custom service name.");
            }
            const start = form.elements.PreferredStart;
            const end = form.elements.PreferredEnd;
            if (Boolean(start.value) !== Boolean(end.value) || (start.value && end.value && start.value >= end.value)) {
                add(!start.value ? start : end, "Enter both times, with the end after the start, or leave both blank.");
            }
        }
        const list = errorSummary.querySelector("ul");
        list.replaceChildren();
        errors.forEach(({ field, message }) => {
            const item = document.createElement("li");
            const link = document.createElement("a");
            if (!field.id) field.id = `preference-field-${list.children.length}`;
            link.href = `#${field.id}`;
            link.textContent = message;
            link.addEventListener("click", event => { event.preventDefault(); reveal(field); });
            item.append(link);
            list.append(item);
        });
        errorSummary.hidden = errors.length === 0;
        if (focus && errors.length) {
            errors.forEach(({ field }) => {
                for (let parent = field.parentElement; parent; parent = parent.parentElement) {
                    if (parent instanceof HTMLDetailsElement) parent.open = true;
                }
            });
            errorSummary.focus();
            errorSummary.scrollIntoView({ block: "center" });
        }
        return errors.length === 0;
    }
    form.addEventListener("submit", event => {
        attemptedSave = true;
        if (validateDraft(true)) return;
        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);
    const availability = document.getElementById("availability");

    function openLinkedAvailability() {
        if (!(availability instanceof HTMLDetailsElement) || location.hash !== "#availability" || workRole.value === "client") return;
        availability.open = true;
        availability.scrollIntoView({ block: "start" });
    }

    window.addEventListener("hashchange", openLinkedAvailability);
    openLinkedAvailability();
    let revealingInvalidField = false;
    form.addEventListener("invalid", event => {
        if (revealingInvalidField) return;
        revealingInvalidField = true;
        queueMicrotask(() => { revealingInvalidField = false; });
        const section = event.target.closest(".settings-disclosure");
        if (section) section.open = true;
        const serviceGroup = event.target.closest("[data-specialty-category]");
        if (serviceGroup) serviceGroup.open = true;
    }, true);
    const save = form.querySelector('button[type="submit"]');
    const help = document.getElementById("workRoleHelp");
    const agreementDialog = document.getElementById("roleAgreementDialog");
    const agreements = [...agreementDialog.querySelectorAll("[data-agreement-role]")];
    const confirmAgreement = agreementDialog.querySelector("[data-agreement-confirm]");
    const requiredAgreements = () => agreements.filter(section =>
        section.dataset.accepted !== "true" &&
        (workRole.value === "both" || section.dataset.agreementRole === workRole.value));
    const agreementChecked = section => section.querySelector("input").checked;
    let submitter;

    form.addEventListener("submit", event => {
        if (availabilityOnlyChanged()) return;
        const missing = requiredAgreements();
        if (missing.every(agreementChecked)) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        submitter = event.submitter;
        agreements.forEach(section => {
            section.hidden = !missing.includes(section);
            section.querySelector("input").checked = false;
        });
        confirmAgreement.disabled = true;
        agreementDialog.showModal();
    }, true);

    agreementDialog.addEventListener("change", () => {
        confirmAgreement.disabled = !requiredAgreements().every(agreementChecked);
    });
    agreementDialog.querySelector("[data-agreement-cancel]").addEventListener("click", () => agreementDialog.close());
    agreementDialog.addEventListener("close", () => {
        agreements.forEach(section => { section.querySelector("input").checked = false; });
        update();
    });
    confirmAgreement.addEventListener("click", () => {
        if (!requiredAgreements().every(agreementChecked) || !validateDraft(true)) return;
        agreementDialog.close();
        form.requestSubmit(submitter ?? save);
    });
    const descriptions = {
        client: "Post tasks and book freelancers.",
        freelancer: "Find jobs and receive booking requests.",
        both: "Post tasks, book freelancers, and accept work."
    };
    const snapshot = () => JSON.stringify(Array.from(new FormData(form)));
    let initial = snapshot();
    const availabilityFields = new Set(["Days", "Availability", "PreferredStart", "PreferredEnd"]);
    const withoutAvailability = value => JSON.stringify(JSON.parse(value).filter(([name]) => !availabilityFields.has(name)));
    const availabilityOnlyChanged = () => snapshot() !== initial && withoutAvailability(snapshot()) === withoutAvailability(initial) && workRole.value !== "client";
    let savingAvailability = false;
    const savedMessage = document.getElementById("profileSavedMessage");
    form.addEventListener("submit", async event => {
        if (event.defaultPrevented || !availabilityOnlyChanged()) return;
        event.preventDefault();
        if (savingAvailability) return;
        savingAvailability = true;
        const submitted = snapshot();
        const originalLabel = save.textContent;
        save.disabled = true;
        save.textContent = "Saving…";
        savedMessage.textContent = "Saving availability…";
        savedMessage.setAttribute("role", "status");
        const controller = new AbortController();
        const timeout = globalThis.setTimeout(() => controller.abort(), 30000);
        try {
            const url = new URL(form.action, location.href);
            url.searchParams.set("handler", "Availability");
            const response = await fetch(url, { method: "POST", body: new FormData(form), headers: { Accept: "application/json" }, signal: controller.signal });
            if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Could not save availability. Refresh your sign-in in another tab, then try again.");
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || "Availability could not be saved. Please try again.");
            initial = submitted;
            savedMessage.textContent = result.message;
        } catch (error) {
            savedMessage.setAttribute("role", "alert");
            savedMessage.textContent = error.name === "AbortError" ? "Saving timed out. Your selections are still here; please try again." : error.message;
        } finally {
            globalThis.clearTimeout(timeout);
            savingAvailability = false;
            save.textContent = originalLabel;
            update();
        }
    });
    const services = Array.from(form.querySelectorAll('input[name="Services"]'));
    const selectAllCategories = document.getElementById("selectAllServiceCategories");
    const specialtyGroups = [...form.querySelectorAll("[data-specialty-category]")];
    const areas = Array.from(form.querySelectorAll('input[name="Areas"]'));
    const allAreas = document.getElementById("selectAllServiceAreas");
    const selectedServices = document.getElementById("selectedServices");
    const otherField = document.getElementById("otherServiceField");
    const otherInput = otherField.querySelector("input");
    const selectAllDays = document.getElementById("selectAllDays");
    const days = [...form.querySelectorAll('#weeklyAvailability input[name="Days"]')];
    if (selectAllDays) selectAllDays.addEventListener("click", function () {
        const select = days.some(day => !day.checked);
        days.forEach(day => { day.checked = select; });
        update();
    });

    const listedSpecialties = group => [...group.querySelectorAll('input[name="SpecialtyChoices"]:not([data-custom-specialty])')];
    for (const group of specialtyGroups) {
        group.querySelector("[data-toggle-specialties]")?.addEventListener("click", () => {
            const listed = listedSpecialties(group);
            const select = listed.some(input => !input.checked);
            const choices = select ? listed : group.querySelectorAll('input[name="SpecialtyChoices"]');
            choices.forEach(input => { input.checked = select; });
            update();
        });
    }
    function update() {
        if (selectAllDays) selectAllDays.textContent = days.every(day => day.checked) ? "Clear all" : "Select all";
        freelancerPreferences.hidden = workRole.value === "client";
        const otherSelected = services.some(service => service.checked && service.value === "Other/General");
        otherInput.required = otherSelected && workRole.value !== "client";
        const selectedCategories = new Set(services.filter(service => service.checked).map(service => service.value));
        selectAllCategories.textContent = selectedCategories.size === services.length ? "Clear all" : "Check all";
        for (const group of specialtyGroups) {
            group.hidden = !selectedCategories.has(group.dataset.specialtyCategory);
            if (group.hidden) group.open = false;
            const count = group.querySelector("[data-specialty-count]");
            if (group === otherField) {
                count.textContent = otherInput.value.trim() ? "Name entered" : "Add service";
                continue;
            }
            group.querySelector("[data-toggle-specialties]").textContent =
                listedSpecialties(group).every(input => input.checked) ? "Clear all" : "Select all";
            const custom = group.querySelector(".specialty-custom");
            custom.hidden = !group.querySelector("[data-custom-specialty]").checked;
            custom.querySelector("input").required = !freelancerPreferences.hidden && !group.hidden && !custom.hidden;
            count.textContent = group.querySelectorAll('input[name="SpecialtyChoices"]:checked').length + " selected";
        }
        allAreas.textContent = areas.every(area => area.checked) ? "Clear all" : "Select all " + areas.length;
        selectedServices.textContent = services.filter(service => service.checked).length + " selected";
        help.textContent = descriptions[workRole.value] || "";
        save.disabled = savingAvailability || (!attemptedSave && snapshot() === initial && requiredAgreements().length === 0);
        if (attemptedSave) validateDraft();
    }

    selectAllCategories.addEventListener("click", () => {
        const select = services.some(service => !service.checked);
        services.forEach(service => { service.checked = select; });
        update();
    });
    allAreas.addEventListener("click", () => {
        const select = areas.some(area => !area.checked);
        areas.forEach(area => { area.checked = select; });
        update();
    });
    form.addEventListener("input", update);
    form.addEventListener("change", update);
    window.addEventListener("pageshow", update);
    update();
    if (attemptedSave) {
        validateDraft(true);
        // A server/storage failure may not correspond to a client-side field error.
        if (errorSummary.querySelectorAll("p").length > 1) {
            errorSummary.hidden = false;
            errorSummary.focus();
            save.disabled = false;
        }
    }
}
