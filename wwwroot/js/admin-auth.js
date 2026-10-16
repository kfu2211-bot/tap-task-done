(function () {
    "use strict";
    const accountKey = "tapTaskDoneAdminAccount", sessionKey = "tapTaskDoneAdminSession";
    const demoAccount = { name: "System Admin", email: "admin@tap-task-done.local", password: "AdminDemo!2026", role: "admin" };
    function read(key) { try { return JSON.parse(localStorage.getItem(key) || "null"); } catch (error) { return null; } }
    window.tapTaskDoneGetAdminAccount = function () { const saved = read(accountKey); if (saved && saved.role === "admin" && saved.email && saved.password) return saved; localStorage.setItem(accountKey, JSON.stringify(demoAccount)); return demoAccount; };
    window.tapTaskDoneGetAdminSession = function () { const session = read(sessionKey); return session && session.role === "admin" ? session : null; };
    window.tapTaskDoneStartAdminSession = function (account, keepSignedIn) { const session = { name: account.name, email: account.email, role: "admin", keepSignedIn: !!keepSignedIn }; localStorage.setItem(sessionKey, JSON.stringify(session)); return session; };
    window.tapTaskDoneEndAdminSession = function () { localStorage.removeItem(sessionKey); };
    if (window.location.pathname.toLowerCase() === "/admin" && !window.tapTaskDoneGetAdminSession()) window.location.replace("/AdminLogin");
}());
