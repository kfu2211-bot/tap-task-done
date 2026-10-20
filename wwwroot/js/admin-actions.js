document.addEventListener("DOMContentLoaded", function () {
    const read = function (key, fallback) { try { const value = JSON.parse(localStorage.getItem(key) || "null"); return value === null ? fallback : value; } catch { return fallback; } };
    const save = function (key, value) { localStorage.setItem(key, JSON.stringify(value)); };
    const notify = function (message) { const notices = read("tapTaskDoneNotifications", []); notices.unshift({ message: message, time: "Just now", read: false }); save("tapTaskDoneNotifications", notices.slice(0, 30)); };
    const activity = function (action, detail) { const entries = read("tapTaskDoneAdminActivity", []); entries.unshift({ time: "Just now", action: action, detail: detail }); save("tapTaskDoneAdminActivity", entries.slice(0, 50)); };

    function refreshAlertCount() {
        const reports = read("tapTaskDoneReports", []).concat(read("tapTaskDoneDemoReports", []));
        document.getElementById("adminAlertCount").textContent = reports.filter(function (report) { return report.status === "Urgent"; }).length;
    }
    document.getElementById("adminAlerts").addEventListener("click", function () { document.querySelector('[data-admin-view="reports"]').click(); });
    refreshAlertCount();

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

    function renderStaff() {
        const staff = read("tapTaskDoneLocalStaff", [{ name: "System Admin", role: "Super Admin", status: "Active" }, { name: "Safety Moderator", role: "Moderator", status: "Active" }]);
        save("tapTaskDoneLocalStaff", staff);
        const host = document.getElementById("staffList"); host.innerHTML = "";
        staff.forEach(function (member, index) {
            const row = document.createElement("div"); row.className = "staff-row";
            row.innerHTML = "<div><strong></strong><small></small></div><span></span><button type=\"button\"></button>";
            row.querySelector("strong").textContent = member.name; row.querySelector("small").textContent = member.role; row.querySelector("span").textContent = member.status;
            const button = row.querySelector("button"); button.textContent = member.status === "Active" ? "Disable" : "Enable";
            button.addEventListener("click", function () { if (!window.confirm("Change this local staff account status?")) return; staff[index].status = member.status === "Active" ? "Disabled" : "Active"; save("tapTaskDoneLocalStaff", staff); activity("Updated staff account", staff[index].name + " is now " + staff[index].status + "."); renderStaff(); });
            host.appendChild(row);
        });
    }

    const account = window.tapTaskDoneGetAdminAccount();
    document.getElementById("adminDisplayName").value = account.name || "System Admin";
    document.getElementById("adminAccountForm").addEventListener("submit", function (event) {
        event.preventDefault(); const name = document.getElementById("adminDisplayName").value.trim(); const newPassword = document.getElementById("adminNewPassword").value;
        if (!name) return; if (newPassword && newPassword.length < 8) { document.getElementById("adminAccountSaved").textContent = "Password must be at least 8 characters."; return; }
        const updated = Object.assign({}, account, { name: name }); if (newPassword) updated.password = newPassword;
        localStorage.setItem("tapTaskDoneAdminAccount", JSON.stringify(updated)); localStorage.setItem("tapTaskDoneAdminSession", JSON.stringify({ name: updated.name, email: updated.email, role: "admin", keepSignedIn: true }));
        document.querySelector(".admin-user strong").textContent = updated.name; document.getElementById("adminGreeting").textContent = updated.name.split(" ")[0]; document.getElementById("adminNewPassword").value = ""; document.getElementById("adminAccountSaved").textContent = "Admin account saved locally."; activity("Updated admin account", "Updated the local admin profile.");
    });
    renderStaff();
    const staffPanel = document.getElementById("staffPanel");
    staffPanel.insertAdjacentHTML("beforeend", '<form id="addStaffForm" class="admin-account-form"><label>Staff name<input id="newStaffName" required /></label><label>Role<select id="newStaffRole"><option>Moderator</option><option>Super Admin</option></select></label><button class="primary-action" type="submit">Add local staff</button></form>');
    document.getElementById("addStaffForm").addEventListener("submit", function (event) { event.preventDefault(); const name = document.getElementById("newStaffName").value.trim(); if (!name) return; const staff = read("tapTaskDoneLocalStaff", []); staff.push({ name: name, role: document.getElementById("newStaffRole").value, status: "Active" }); save("tapTaskDoneLocalStaff", staff); activity("Added staff account", "Added " + name + " as local " + staff[staff.length - 1].role + "."); document.getElementById("newStaffName").value = ""; renderStaff(); });

    function adminNotices() { return read("tapTaskDoneAdminNotices", []); }
    function addAdminNotice(message, view) { const items = adminNotices(); items.unshift({ message: message, view: view || "overview", time: "Just now", read: false }); save("tapTaskDoneAdminNotices", items.slice(0, 40)); }
    function renderAdminNotices() {
        const host = document.getElementById("adminNotificationList"); if (!host) return;
        const items = adminNotices(); host.innerHTML = "";
        items.forEach(function (item) { const row = document.createElement("div"); row.className = "admin-notice" + (item.read ? "" : " unread"); row.innerHTML = "<div><strong></strong><small></small></div><button type=\"button\">Open</button>"; row.querySelector("strong").textContent = item.message; row.querySelector("small").textContent = item.time; row.querySelector("button").addEventListener("click", function () { item.read = true; save("tapTaskDoneAdminNotices", items); const link = document.querySelector('[data-admin-view="' + item.view + '"]'); if (link) link.click(); renderAdminNotices(); }); host.appendChild(row); }); document.getElementById("adminNotificationEmpty").hidden = items.length !== 0;
    }
    const initialRequests = read("tapTaskDoneVerificationRequests", []).filter(function (x) { return ["Pending", "Appeal pending"].includes(x.status); }).length;
    const initialReports = read("tapTaskDoneReports", []).filter(function (x) { return !["Resolved", "Dismissed"].includes(x.status); }).length;
    if (initialRequests) addAdminNotice(initialRequests + " local verification request(s) need review.", "verifications");
    if (initialReports) addAdminNotice(initialReports + " local safety report(s) need review.", "reports");
    document.getElementById("markAdminNoticesRead").addEventListener("click", function () { const items = adminNotices(); items.forEach(function (item) { item.read = true; }); save("tapTaskDoneAdminNotices", items); renderAdminNotices(); });
    document.getElementById("exportActivity").addEventListener("click", function () { const rows = read("tapTaskDoneAdminActivity", []); const csv = ["Time,Action,Details"].concat(rows.map(function (row) { return [row.time, row.action, row.detail].map(function (value) { return '"' + String(value || "").replaceAll('"', '""') + '"'; }).join(","); })).join("\n"); const link = document.createElement("a"); link.href = URL.createObjectURL(new Blob([csv], { type: "text/csv" })); link.download = "tap-task-done-admin-activity.csv"; link.click(); URL.revokeObjectURL(link.href); });
    document.getElementById("activitySearch").addEventListener("input", function () { const query = this.value.toLowerCase(); document.querySelectorAll("#activityList .activity-row").forEach(function (row) { row.hidden = !row.textContent.toLowerCase().includes(query); }); });
    document.getElementById("adminNavigation").addEventListener("click", function (event) { const link = event.target.closest('[data-admin-view="notifications"]'); if (link) { window.setTimeout(function () { document.getElementById("adminPageTitle").textContent = "Notifications"; renderAdminNotices(); }, 0); } });
    renderAdminNotices();

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

    // Local role preview makes the difference between Super Admin and Moderator visible.
    document.getElementById("adminAccountPanel").insertAdjacentHTML("beforeend", '<label class="role-preview">Preview role<select id="rolePreview"><option value="Super Admin">Super Admin</option><option value="Moderator">Moderator</option></select></label><button id="sendResetLink" class="review-button" type="button">Create local password-reset notice</button>');
    document.getElementById("rolePreview").addEventListener("change", function () { const moderator = this.value === "Moderator"; document.getElementById("adminAccountForm").querySelectorAll("input,button").forEach(function (element) { element.disabled = moderator; }); document.getElementById("staffPanel").style.opacity = moderator ? ".55" : "1"; document.getElementById("staffPanel").style.pointerEvents = moderator ? "none" : "auto"; document.getElementById("adminAccountSaved").textContent = moderator ? "Moderator preview: review tools only; staff/settings changes are disabled." : "Super Admin preview restored."; });
    document.getElementById("sendResetLink").addEventListener("click", function () { notify("Admin password reset requested. Use the local Admin Settings page to choose a new password."); activity("Created password reset notice", "Created a local password-reset notice for the admin account."); alert("Local reset notice created. This becomes a real email flow after database/email setup."); });
});
