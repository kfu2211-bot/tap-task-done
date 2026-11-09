const workRole = document.getElementById("WorkRole");
const freelancerPreferences = document.getElementById("freelancerPreferences");

if (workRole && freelancerPreferences) {
    const updateVisibility = () => {
        freelancerPreferences.hidden = workRole.value === "client";
    };
    workRole.addEventListener("change", updateVisibility);
    updateVisibility();
}
