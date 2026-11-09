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

    function update() {
        freelancerPreferences.hidden = workRole.value === "client";
        help.textContent = descriptions[workRole.value] || "";
        save.disabled = snapshot() === initial;
    }

    form.addEventListener("input", update);
    form.addEventListener("change", update);
    window.addEventListener("pageshow", update);
    update();
}
