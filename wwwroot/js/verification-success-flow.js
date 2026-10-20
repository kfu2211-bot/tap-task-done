(function () {
    "use strict";

    const requestKey = "tapTaskDoneVerificationRequests";
    const statusKey = "tapTaskDoneVerificationStatus";

    function readRequests() {
        try {
            const requests = JSON.parse(localStorage.getItem(requestKey) || "[]");
            return Array.isArray(requests) ? requests : [];
        } catch (error) {
            return [];
        }
    }

    function latestRequest() {
        let account = {};
        try { account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}"); } catch (error) { }
        return readRequests().find(function (item) { return item.email && String(item.email).toLowerCase() === String(account.email || "").toLowerCase(); }) || {};
    }

    function documentNames(request) {
        const names = Array.isArray(request.fileNames) ? request.fileNames : [
            request.governmentIdName,
            request.certificationName,
            request.supportingDocumentName
        ];
        return names.filter(Boolean);
    }

    function setFormLocked(locked) {
        const card = document.querySelector(".verification-card");
        if (!card) return;

        card.classList.toggle("verification-form-locked", locked);
        card.querySelectorAll("input, button").forEach(function (control) {
            control.disabled = locked;
        });
    }

    function hideSuccess() {
        const panel = document.getElementById("verificationSuccessFlow");
        if (panel) panel.remove();
    }

    function unlockForNewDocuments() {
        hideSuccess();
        setFormLocked(false);

        const statusTitle = document.getElementById("statusTitle");
        const statusDescription = document.getElementById("statusDescription");
        const statusIcon = document.getElementById("statusIcon");
        const submit = document.getElementById("submitVerification");
        if (statusTitle) statusTitle.textContent = "Submit updated documents";
        if (statusDescription) statusDescription.textContent = "Choose your updated files, then send them for a new review.";
        if (statusIcon) statusIcon.textContent = "!";
        if (submit) {
            submit.textContent = "Submit for Verification";
            submit.style.opacity = "1";
            submit.style.cursor = "pointer";
        }
        document.querySelector(".verification-card")?.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    function showSuccess(kind) {
        const request = latestRequest();
        const status = String(request.status || "").toLowerCase().replace("appeal pending", "pending");
        if (status !== "pending" && status !== "approved") return;

        const existing = document.getElementById("verificationSuccessFlow");
        if (existing) existing.remove();
        document.getElementById("verificationAppeal")?.remove();

        const isAppeal = kind === "appeal" || String(request.status || "").toLowerCase().includes("appeal");
        const names = documentNames(request);
        const fileMarkup = names.length
            ? names.map(function (name) { return '<li><span class="submitted-document-check">✓</span><span>' + escapeHtml(name) + '</span></li>'; }).join("")
            : '<li><span class="submitted-document-check">✓</span><span>Documents submitted for review</span></li>';

        const panel = document.createElement("section");
        panel.id = "verificationSuccessFlow";
        panel.className = "verification-success-flow";
        panel.innerHTML =
            '<div class="verification-success-banner">' +
                '<div class="verification-success-icon">✓</div>' +
                '<div><p class="verification-success-eyebrow">SUBMISSION RECEIVED</p>' +
                '<h3>' + (isAppeal ? "Appeal submitted for review" : "Documents submitted for review") + '</h3>' +
                '<p>We will notify you after an admin reviews your request.</p></div>' +
            '</div>' +
            '<section class="submitted-documents-card">' +
                '<div class="submitted-documents-heading"><div><h3>Submitted documents</h3><p>Only file names are saved in this demo; the admin cannot view the files.</p></div><span class="pending-review-badge">' + (status === "approved" ? "Approved" : "Pending review") + '</span></div>' +
                '<ul class="submitted-documents-list">' + fileMarkup + '</ul>' +
            '</section>' +
            '<button type="button" class="submit-new-documents" id="submitNewDocuments">Submit new documents</button>';

        const statusCard = document.getElementById("verificationStatus");
        if (statusCard) statusCard.after(panel);
        setFormLocked(true);
        document.getElementById("submitNewDocuments")?.addEventListener("click", unlockForNewDocuments);
    }

    function escapeHtml(value) {
        const element = document.createElement("span");
        element.textContent = value;
        return element.innerHTML;
    }

    document.addEventListener("DOMContentLoaded", function () {
        showSuccess();
    });
    document.addEventListener("tapTaskDone:verification-submitted", function () {
        showSuccess("submission");
    });
    document.addEventListener("tapTaskDone:appeal-submitted", function () {
        showSuccess("appeal");
    });
})();
