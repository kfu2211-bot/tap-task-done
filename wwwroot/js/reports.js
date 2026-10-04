document.addEventListener("DOMContentLoaded", function () {
    const key = "tapTaskDoneReports";
    const form = document.getElementById("reportForm");
    const history = document.getElementById("reportHistory");
    const evidenceInput = document.getElementById("reportEvidence");
    const evidencePreview = document.getElementById("evidencePreview");
    let selectedEvidence = [];

    const params = new URLSearchParams(window.location.search);
    document.getElementById("reportTarget").value = params.get("provider") || "";
    document.getElementById("reportTask").value = params.get("task") || "";

    function read() {
        try {
            const reports = JSON.parse(localStorage.getItem(key) || "[]");
            return Array.isArray(reports) ? reports : [];
        } catch (error) { return []; }
    }

    function render() {
        const reports = read().slice().reverse();
        history.textContent = "";
        if (!reports.length) { history.textContent = "No reports submitted yet."; return; }
        reports.forEach(function (report) {
            const item = document.createElement("article");
            item.className = "report-item";
            item.innerHTML = "<div><strong></strong><span></span></div><b></b><p></p>";
            item.querySelector("strong").textContent = report.target;
            item.querySelector("span").textContent = report.reason + " · " + report.createdAt;
            item.querySelector("b").textContent = report.status;
            item.querySelector("p").textContent = report.description;
            if (report.evidenceNames && report.evidenceNames.length) {
                const evidence = document.createElement("small");
                evidence.textContent = "Evidence attached: " + report.evidenceNames.join(", ");
                item.appendChild(evidence);
            }
            history.appendChild(item);
        });
    }

    function renderEvidencePreview() {
        evidencePreview.textContent = "";
        selectedEvidence.forEach(function (file, index) {
            const item = document.createElement("div");
            item.className = "evidence-item";
            const image = document.createElement("img");
            const label = document.createElement("span");
            const remove = document.createElement("button");
            image.src = URL.createObjectURL(file);
            image.alt = "Selected evidence preview";
            label.textContent = file.name;
            remove.type = "button";
            remove.textContent = "×";
            remove.setAttribute("aria-label", "Remove " + file.name);
            remove.addEventListener("click", function () {
                selectedEvidence.splice(index, 1);
                renderEvidencePreview();
            });
            item.append(image, remove, label);
            evidencePreview.appendChild(item);
        });
    }

    evidenceInput.addEventListener("change", function () {
        const files = Array.from(evidenceInput.files || []);
        const validFiles = files.filter(function (file) {
            return ["image/jpeg", "image/png", "image/webp"].includes(file.type) && file.size <= 5 * 1024 * 1024;
        });
        if (validFiles.length !== files.length) window.alert("Use JPG, PNG, or WEBP images up to 5 MB each.");
        selectedEvidence = validFiles.slice(0, 3);
        if (validFiles.length > 3) window.alert("You can attach up to 3 images.");
        renderEvidencePreview();
        evidenceInput.value = "";
    });

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        const reports = read();
        reports.push({
            id: "report-" + Date.now(),
            target: document.getElementById("reportTarget").value.trim(),
            task: document.getElementById("reportTask").value.trim(),
            reason: document.getElementById("reportReason").value,
            description: document.getElementById("reportDescription").value.trim(),
            evidenceNames: selectedEvidence.map(function (file) { return file.name; }),
            status: "Pending",
            createdAt: new Date().toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })
        });
        localStorage.setItem(key, JSON.stringify(reports));
        document.getElementById("reportSavedMessage").textContent = selectedEvidence.length
            ? "Report submitted with " + selectedEvidence.length + " evidence image(s). Status: Pending."
            : "Report submitted. Status: Pending.";
        form.reset();
        selectedEvidence = [];
        renderEvidencePreview();
        render();
    });

    render();
});
