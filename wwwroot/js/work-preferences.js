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
    form.addEventListener("invalid", event => {
        const section = event.target.closest(".settings-disclosure");
        if (section) section.open = true;
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

    function update() {
        if (selectAllDays) selectAllDays.textContent = days.every(day => day.checked) ? "Clear all" : "Select all";
        freelancerPreferences.hidden = workRole.value === "client";
        const otherSelected = services.some(service => service.checked && service.value === "Other/General");
        otherField.hidden = !otherSelected;
        otherInput.required = otherSelected && workRole.value !== "client";
        allAreas.textContent = areas.every(area => area.checked) ? "Clear all" : "Select all " + areas.length;
        selectedServices.textContent = services.filter(service => service.checked).length + " selected";
        help.textContent = descriptions[workRole.value] || "";
        save.disabled = snapshot() === initial && requiredAgreements().length === 0;
    }

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
