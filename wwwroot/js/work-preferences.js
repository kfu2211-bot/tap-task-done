const workRole = document.getElementById("WorkRole");
const freelancerPreferences = document.getElementById("freelancerPreferences");

if (workRole && freelancerPreferences) {
    const form = workRole.form;
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
        if (!requiredAgreements().every(agreementChecked) || !form.reportValidity()) return;
        agreementDialog.close();
        form.requestSubmit(submitter ?? save);
    });
    const descriptions = {
        client: "Post tasks and book freelancers.",
        freelancer: "Find jobs and receive booking requests.",
        both: "Post tasks, book freelancers, and accept work."
    };
    const snapshot = () => JSON.stringify(Array.from(new FormData(form)));
    const initial = snapshot();
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
        save.disabled = snapshot() === initial && requiredAgreements().length === 0;
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
}
