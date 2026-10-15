document.addEventListener("DOMContentLoaded", function () {
    const verificationQueue = document.getElementById("verificationQueue");
    const reportQueue = document.getElementById("reportQueue");
    const activityList = document.getElementById("activityList");
    const modal = document.getElementById("reviewModal");
    const modalTitle = document.getElementById("modalTitle");
    const modalDescription = document.getElementById("modalDescription");
    const modalType = document.getElementById("modalType");
    let selected = null;

    const demoVerifications = [
        { id: "demo-v-1", name: "John Santos", email: "john.santos@gmail.com", type: "Government ID", time: "2 hours ago", initials: "JS" },
        { id: "demo-v-2", name: "Maria Lopez", email: "maria.lopez23@gmail.com", type: "Passport", time: "5 hours ago", initials: "ML", alt: true },
        { id: "demo-v-3", name: "Daniel Tan", email: "danieltan.work@gmail.com", type: "Driver's License", time: "1 day ago", initials: "DT", amber: true }
    ];
    const demoReports = [
        { id: "demo-r-1", target: "Ryan Cruz", reason: "Scam or fake account", time: "3 hours ago", status: "Urgent", initials: "RC", urgent: true },
        { id: "demo-r-2", target: "Kevin Palma", reason: "Harassment or abusive behavior", time: "6 hours ago", status: "In review", initials: "KP", amber: true },
        { id: "demo-r-3", target: "Nina Torres", reason: "Inappropriate content", time: "1 day ago", status: "In review", initials: "NT", alt: true }
    ];

    function read(key) { try { const value = JSON.parse(localStorage.getItem(key) || "[]"); return Array.isArray(value) ? value : []; } catch (_) { return []; } }
    function write(key, value) { localStorage.setItem(key, JSON.stringify(value)); }
    function account() { try { return JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}"); } catch (_) { return {}; } }
    function activities() { const saved = read("tapTaskDoneAdminActivity"); return saved.length ? saved : [
        { time: "2 hours ago", action: "Approved verification", detail: "Approved John Santos's government ID." },
        { time: "4 hours ago", action: "Opened report", detail: "Opened a safety report for Ryan Cruz." },
        { time: "6 hours ago", action: "Resolved report", detail: "Marked a report on Kevin Palma as resolved." }
    ]; }
    function addActivity(action, detail) { const items = activities(); items.unshift({ time: "Just now", action: action, detail: detail }); write("tapTaskDoneAdminActivity", items.slice(0, 20)); }

    function localVerification() {
        if (localStorage.getItem("tapTaskDoneVerificationStatus") !== "pending") return [];
        const user = account(); const name = user.nickname || user.name || "Current demo user";
        return [{ id: "local-verification", name: name, email: user.email || "No email saved", type: "Government ID", time: "Just now", initials: name.split(/\s+/).map(function (part) { return part[0]; }).join("").slice(0, 2).toUpperCase(), local: true }];
    }
    function localReports() { return read("tapTaskDoneReports").filter(function (item) { return item.status !== "Resolved" && item.status !== "Dismissed"; }).map(function (item) { return { id: item.id, target: item.target || "Unnamed user", reason: item.reason || "Other", time: item.createdAt || "Recently", status: item.status || "Pending", initials: (item.target || "UR").slice(0, 2).toUpperCase(), local: true }; }); }
    function avatarClass(item) { return item.amber ? "avatar amber" : item.alt ? "avatar alt" : "avatar"; }
    function statusClass(value) { return value === "Urgent" ? "status urgent" : value === "Resolved" || value === "Approved" ? "status resolved" : "status"; }
    function verificationRow(item) { const row = document.createElement("div"); row.className = "queue-row verification"; row.innerHTML = '<div class="person"><span class="' + avatarClass(item) + '"></span><div><strong></strong><small></small></div></div><span></span><span></span><span class="status">Pending</span><button class="review-button" type="button">Review</button>'; row.querySelector(".avatar").textContent = item.initials; row.querySelector("strong").textContent = item.name; row.querySelector("small").textContent = item.email; row.children[1].textContent = item.type; row.children[2].textContent = item.time; row.querySelector("button").addEventListener("click", function () { openReview("verification", item); }); return row; }
    function reportRow(item) { const row = document.createElement("div"); row.className = "queue-row report"; row.innerHTML = '<div class="person"><span class="avatar"></span><div><strong></strong><small>Safety report</small></div></div><span></span><span></span><span></span><button class="review-button" type="button">Open</button>'; row.querySelector(".avatar").className = avatarClass(item); row.querySelector(".avatar").textContent = item.initials; row.querySelector("strong").textContent = item.target; row.children[1].textContent = item.reason; row.children[2].textContent = item.time; const badge = row.children[3]; badge.className = statusClass(item.status); badge.textContent = item.status; row.querySelector("button").addEventListener("click", function () { openReview("report", item); }); return row; }
    function renderQueues() { const verifications = localVerification().concat(demoVerifications); const reports = localReports().concat(demoReports); verificationQueue.textContent = ""; reportQueue.textContent = ""; verifications.forEach(function (item) { verificationQueue.appendChild(verificationRow(item)); }); reports.forEach(function (item) { reportQueue.appendChild(reportRow(item)); }); document.getElementById("pendingVerificationCount").textContent = verifications.length + 9; document.getElementById("newReportCount").textContent = reports.length + 1; document.getElementById("urgentCount").textContent = reports.filter(function (item) { return item.status === "Urgent"; }).length + 2; }
    function renderActivity() { activityList.textContent = ""; activities().slice(0, 5).forEach(function (item) { const row = document.createElement("div"); row.className = "activity-row"; row.innerHTML = '<span></span><span class="activity-admin"><span>AD</span>Admin</span><strong></strong><span></span>'; row.children[0].textContent = item.time; row.children[2].textContent = item.action; row.children[3].textContent = item.detail; activityList.appendChild(row); }); }
    function openReview(type, item) { selected = { type: type, item: item }; modalType.textContent = type === "verification" ? "IDENTITY VERIFICATION" : "SAFETY REPORT"; modalTitle.textContent = type === "verification" ? "Review " + item.name : "Report about " + item.target; modalDescription.textContent = type === "verification" ? "Demo view: inspect the submitted identity document before choosing an outcome. Real document access will be added with secure private storage." : "Demo view: review the reason, evidence, and related task before resolving or dismissing this report."; modal.classList.remove("hidden"); }
    function closeReview() { modal.classList.add("hidden"); selected = null; }
    document.getElementById("modalClose").addEventListener("click", closeReview); modal.addEventListener("click", function (event) { if (event.target === modal) closeReview(); });
    document.getElementById("approveAction").addEventListener("click", function () { if (!selected) return; const outcome = selected.type === "verification" ? "Approved verification" : "Resolved report"; const who = selected.type === "verification" ? selected.item.name : selected.item.target; if (selected.item.local && selected.type === "verification") localStorage.setItem("tapTaskDoneVerificationStatus", "approved"); if (selected.item.local && selected.type === "report") { const reports = read("tapTaskDoneReports"); const report = reports.find(function (item) { return item.id === selected.item.id; }); if (report) report.status = "Resolved"; write("tapTaskDoneReports", reports); } addActivity(outcome, outcome + " for " + who + "."); closeReview(); renderQueues(); renderActivity(); });
    document.getElementById("rejectAction").addEventListener("click", function () { if (!selected) return; const outcome = selected.type === "verification" ? "Rejected verification" : "Dismissed report"; const who = selected.type === "verification" ? selected.item.name : selected.item.target; if (selected.item.local && selected.type === "verification") localStorage.setItem("tapTaskDoneVerificationStatus", "rejected"); if (selected.item.local && selected.type === "report") { const reports = read("tapTaskDoneReports"); const report = reports.find(function (item) { return item.id === selected.item.id; }); if (report) report.status = "Dismissed"; write("tapTaskDoneReports", reports); } addActivity(outcome, outcome + " for " + who + "."); closeReview(); renderQueues(); renderActivity(); });
    document.querySelectorAll(".stat-card").forEach(function (button) { button.addEventListener("click", function () { const target = document.getElementById(button.dataset.target); if (target) target.scrollIntoView({ behavior:"smooth", block:"start" }); }); });
    document.querySelector(".mobile-menu").addEventListener("click", function () { document.querySelector(".admin-sidebar").classList.toggle("open"); });
    renderQueues(); renderActivity();
});
