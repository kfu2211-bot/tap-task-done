document.addEventListener("DOMContentLoaded", function () {
    const existing = window.tapTaskDoneGetAdminSession();
    if (existing && existing.keepSignedIn) { window.location.replace("/Admin"); return; }
    const form = document.getElementById("adminLoginForm"), error = document.getElementById("adminLoginError");
    form.addEventListener("submit", function (event) {
        event.preventDefault();
        const email = document.getElementById("adminEmail").value.trim().toLowerCase(), password = document.getElementById("adminPassword").value, account = window.tapTaskDoneGetAdminAccount();
        if (email !== String(account.email).toLowerCase() || password !== account.password) { error.textContent = "Incorrect admin email or password."; return; }
        window.tapTaskDoneStartAdminSession(account, document.getElementById("keepAdminSignedIn").checked);
        window.location.replace("/Admin");
    });
});
