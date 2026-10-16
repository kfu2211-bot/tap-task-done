document.addEventListener("DOMContentLoaded", function () {
    function read(key) { try { return JSON.parse(localStorage.getItem(key) || "[]"); } catch { return []; } }
    function roleLabel(role) { return role === "freelancer" ? "Freelancer" : role === "both" ? "Client & Freelancer" : "Client"; }
    function syncCurrentAccount() {
        let account; try { account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "null"); } catch { return; }
        if (!account || !account.email) return;
        const users = read("tapTaskDoneLocalUsers"); const index = users.findIndex(function (user) { return String(user.email).toLowerCase() === String(account.email).toLowerCase(); });
        const record = { name: account.nickname || account.name || "Current user", email: account.email, status: account.verificationStatus || "Active", role: account.role || "client" };
        if (index < 0) users.unshift(record); else users[index] = Object.assign({}, users[index], record);
        localStorage.setItem("tapTaskDoneLocalUsers", JSON.stringify(users));
    }
    function linkQueues() {
        const reports = read("tapTaskDoneReports");
        document.querySelectorAll("#reportQueue .queue-row").forEach(function (row) {
            const target = row.querySelector(".person strong")?.textContent; const report = reports.find(function (item) { return item.target === target; }); if (!report || !report.reporterEmail) return;
            const person = row.querySelector(".person"); if (!person) return;
            person.querySelector("strong").textContent = report.reporterName || "Current user";
            person.querySelector("small").textContent = report.reporterEmail + " · " + roleLabel(report.reporterRole);
        });
        const requests = read("tapTaskDoneVerificationRequests");
        document.querySelectorAll("#verificationQueue .queue-row").forEach(function (row) {
            const email = row.querySelector("small")?.textContent; const request = requests.find(function (item) { return item.email === email; }); if (!request) return;
            const tag = document.createElement("em"); tag.className = "account-role-tag"; tag.textContent = roleLabel(request.accountRole); row.querySelector(".person div").appendChild(tag);
        });
    }
    syncCurrentAccount(); document.getElementById("userSearch")?.dispatchEvent(new Event("input")); new MutationObserver(linkQueues).observe(document.getElementById("reportQueue"), { childList: true }); new MutationObserver(linkQueues).observe(document.getElementById("verificationQueue"), { childList: true }); linkQueues();
});


