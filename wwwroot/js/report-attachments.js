const reportEvidence = document.getElementById("reportEvidence");
const reportFileSummary = document.getElementById("reportFileSummary");

reportEvidence.addEventListener("change", () => {
    reportFileSummary.textContent = Array.from(reportEvidence.files, file => file.name).join(", ");
});
