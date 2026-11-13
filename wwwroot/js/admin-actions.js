document.addEventListener("DOMContentLoaded", function () {
    const staffForm = document.querySelector(".admin-staff-form");
    if (staffForm) {
        const button = staffForm.querySelector('button[type="submit"]');
        const label = button.textContent;
        staffForm.addEventListener("submit", event => {
            if (button.disabled) {
                event.preventDefault();
                return;
            }
            button.disabled = true;
            button.textContent = "Saving access…";
            staffForm.setAttribute("aria-busy", "true");
        });
        window.addEventListener("pageshow", () => {
            button.disabled = false;
            button.textContent = label;
            staffForm.removeAttribute("aria-busy");
        });
    }
    const read = function (key, fallback) { try { const value = JSON.parse(localStorage.getItem(key) || "null"); return value === null ? fallback : value; } catch { return fallback; } };
    const save = function (key, value) { localStorage.setItem(key, JSON.stringify(value)); };
    const notify = function (message) { const notices = read("tapTaskDoneNotifications", []); notices.unshift({ message: message, time: "Just now", read: false }); save("tapTaskDoneNotifications", notices.slice(0, 30)); };
    const activity = function (action, detail) { const entries = read("tapTaskDoneAdminActivity", []); entries.unshift({ time: "Just now", action: action, detail: detail }); save("tapTaskDoneAdminActivity", entries.slice(0, 50)); };

    const inbox = document.getElementById("notifications");
    const entries = [...inbox.querySelectorAll("[data-inbox-key]")];
    const storageKey = "tapTaskDoneAdminInboxRead:" + inbox.dataset.adminId;
    const stored = read(storageKey, []);
    const seen = new Set(Array.isArray(stored) ? stored : []);
    const filters = [...inbox.querySelectorAll("[data-inbox-filter]")];
    const markAll = document.getElementById("adminInboxReadAll");
    let unreadOnly = false;

    function refreshAlertCount() {
        let unreadCount = 0;
        let visibleCount = 0;
        for (const entry of entries) {
            const unread = !seen.has(entry.dataset.inboxKey);
            entry.classList.toggle("is-unread", unread);
            entry.querySelector(".admin-inbox-unread").hidden = !unread;
            entry.hidden = unreadOnly && !unread;
            if (unread) unreadCount++;
            if (!entry.hidden) visibleCount++;
        }
        document.getElementById("adminAlertCount").textContent = unreadCount;
        markAll.disabled = unreadCount === 0;
        const empty = document.getElementById("adminInboxEmpty");
        empty.hidden = visibleCount > 0;
        empty.textContent = unreadOnly ? "No unread notifications." : "No notifications yet.";
    }

    function markRead(items) {
        items.forEach(entry => seen.add(entry.dataset.inboxKey));
        try { save(storageKey, entries.map(entry => entry.dataset.inboxKey).filter(key => seen.has(key))); }
        catch { document.getElementById("adminInboxStatus").textContent = "Read status could not be saved in this browser."; }
        refreshAlertCount();
    }

    filters.forEach(button => button.addEventListener("click", () => {
        unreadOnly = button.dataset.inboxFilter === "unread";
        filters.forEach(filter => filter.setAttribute("aria-pressed", String(filter === button)));
        refreshAlertCount();
    }));
    entries.forEach(entry => entry.addEventListener("click", () => markRead([entry])));
    markAll.addEventListener("click", () => markRead(entries));
    document.getElementById("adminAlerts").addEventListener("click", () =>
        document.querySelector('[data-admin-view="notifications"]').click());
    refreshAlertCount();
    window.addEventListener("pageshow", refreshAlertCount);

    // Prevent accidental high-impact review actions and notify the normal user view.
    ["approveAction", "rejectAction"].forEach(function (id) {
        document.getElementById(id).addEventListener("click", function (event) {
            const decision = id === "approveAction" ? "confirm this approval or resolution" : "confirm this rejection or dismissal";
            if (!window.confirm("Do you want to " + decision + "?")) { event.stopImmediatePropagation(); return; }
            window.setTimeout(function () {
                const latest = read("tapTaskDoneAdminActivity", [])[0];
                if (latest) notify("Admin update: " + latest.detail);
                refreshAlertCount();
            }, 20);
        }, true);
    });

    document.getElementById("exportActivity").addEventListener("click", function () { const rows = window.databaseAdminActivity || []; const csv = ["Time UTC,Admin,Action,Details"].concat(rows.map(function (row) { return [row.time, row.admin, row.action, row.detail].map(function (value) { let text = String(value || ""); if (/^[\s]*[=+@\-]/.test(text)) text = "'" + text; return '"' + text.replaceAll('"', '""') + '"'; }).join(","); })).join("\n"); const link = document.createElement("a"); link.href = URL.createObjectURL(new Blob([csv], { type: "text/csv" })); link.download = "tap-task-done-admin-activity.csv"; link.click(); URL.revokeObjectURL(link.href); });
    document.getElementById("activitySearch").addEventListener("input", function () { const query = this.value.toLowerCase(); document.querySelectorAll("#activityList .activity-row").forEach(function (row) { row.hidden = !row.textContent.toLowerCase().includes(query); }); });

    // Adds specific actions to every account in the local Users view.
    function addUserActions() {
        document.querySelectorAll("#userList .local-user").forEach(function (row) {
            if (row.querySelector(".local-user-extras")) return;
            const email = row.querySelector("small")?.textContent || ""; const name = row.querySelector("strong")?.textContent || "User";
            const extra = document.createElement("div"); extra.className = "local-user-extras";
            const warn = document.createElement("button"); warn.type = "button"; warn.textContent = "Warn";
            warn.addEventListener("click", function () { if (!window.confirm("Send a local warning to " + name + "?")) return; notify("Admin warning: Please follow the Tap, Task, Done community rules."); activity("Warned user", "Sent a local warning to " + name + "."); alert("Local warning recorded for " + name + "."); });
            const restrict = document.createElement("button"); restrict.type = "button"; restrict.textContent = "Restrict";
            restrict.addEventListener("click", function () { if (!window.confirm("Restrict " + name + " from new activity?")) return; const users = read("tapTaskDoneLocalUsers", []); const user = users.find(function (item) { return item.email === email; }); if (user) { user.status = "Restricted"; save("tapTaskDoneLocalUsers", users); } notify("Your account has been restricted pending a safety review."); activity("Restricted account", "Restricted " + name + " from new activity."); document.getElementById("userSearch").dispatchEvent(new Event("input")); });
            extra.append(warn, restrict); row.appendChild(extra);
        });
    }
    new MutationObserver(addUserActions).observe(document.getElementById("userList"), { childList: true }); addUserActions();

    // Local case ownership, priority, escalation, and direct task/chat preview.
    document.addEventListener("click", function (event) {
        const reportButton = event.target.closest("#reportQueue .review-button");
        if (!reportButton) return;
        window.setTimeout(function () {
            if (document.getElementById("caseWorkflow")) return;
            const controls = document.createElement("div"); controls.id = "caseWorkflow"; controls.className = "case-workflow";
            controls.innerHTML = '<label>Assigned to<select id="caseOwner"><option>Safety Moderator</option><option>System Admin</option></select></label><label>Priority<select id="casePriority"><option>Normal</option><option>High</option><option>Urgent</option></select></label><button id="escalateCase" type="button">Escalate to Super Admin</button><button id="openTaskChat" type="button">Open related task / chat</button>';
            document.getElementById("modalDescription").after(controls);
            document.getElementById("escalateCase").onclick = function () { activity("Escalated report", "Escalated the current case to Super Admin."); notify("Your safety report was escalated for priority review."); alert("Case escalated locally."); };
            document.getElementById("openTaskChat").onclick = function () { alert("Local preview: related task and chat details will open here once task messages are connected to a database."); };
        }, 0);
    }, true);

});
