const workRole = document.getElementById("WorkRole");
const freelancerPreferences = document.getElementById("freelancerPreferences");

if (workRole && freelancerPreferences) {
    const form = workRole.form;
    const save = form.querySelector('button[type="submit"]');
    const help = document.getElementById("workRoleHelp");
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
    if (selectAllDays) selectAllDays.addEventListener("click", function () {
        form.querySelectorAll('#weeklyAvailability input[name="Days"]').forEach(function (day) { day.checked = true; });
        update();
    });

    function update() {
        freelancerPreferences.hidden = workRole.value === "client";
        const otherSelected = services.some(service => service.checked && service.value === "Other/General");
        otherField.hidden = !otherSelected;
        otherInput.required = otherSelected && workRole.value !== "client";
        allAreas.checked = areas.every(area => area.checked);
        allAreas.indeterminate = !allAreas.checked && areas.some(area => area.checked);
        selectedServices.replaceChildren(...services.filter(service => service.checked).map(service => {
            const chip = document.createElement("button");
            chip.type = "button";
            chip.textContent = (service.value === "Other/General" ? otherInput.value.trim() || "Other / General" : service.value) + " ×";
            chip.setAttribute("aria-label", "Remove " + service.value);
            chip.addEventListener("click", () => {
                service.checked = false;
                update();
                selectedServices.parentElement.querySelector("summary").focus();
            });
            return chip;
        }));
        help.textContent = descriptions[workRole.value] || "";
        save.disabled = snapshot() === initial;
    }

    form.addEventListener("input", event => {
        if (event.target === allAreas) {
            areas.forEach(area => { area.checked = allAreas.checked; });
        }
        update();
    });
    form.addEventListener("change", update);
    window.addEventListener("pageshow", update);
    update();
}
