const withdrawalDialog = document.getElementById("withdrawJobDialog");
let pendingWithdrawal;
let confirmedWithdrawal;

document.querySelectorAll("form[data-withdraw-job]").forEach(form => {
    form.addEventListener("submit", event => {
        if (confirmedWithdrawal === form) return;

        event.preventDefault();
        pendingWithdrawal = form;
        withdrawalDialog.querySelector("[data-job-title]").textContent = form.dataset.withdrawJob;
        withdrawalDialog.returnValue = "";
        withdrawalDialog.showModal();
    });
});

withdrawalDialog.addEventListener("close", () => {
    const form = pendingWithdrawal;
    pendingWithdrawal = null;
    if (!form || withdrawalDialog.returnValue !== "withdraw") return;

    confirmedWithdrawal = form;
    try {
        form.requestSubmit();
    } finally {
        confirmedWithdrawal = null;
    }
});
