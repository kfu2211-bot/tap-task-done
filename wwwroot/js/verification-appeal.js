document.addEventListener("DOMContentLoaded", function () {
    let account = {}, savedRequests = [];
    try { account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}"); savedRequests = JSON.parse(localStorage.getItem("tapTaskDoneVerificationRequests") || "[]"); } catch (error) { }
    const ownRequest = savedRequests.find(function (request) { return request.email && String(request.email).toLowerCase() === String(account.email || "").toLowerCase(); });
    if (!ownRequest || String(ownRequest.status).toLowerCase() !== "rejected") return;
    const statusCard = document.getElementById("verificationStatus");
    if (!statusCard || document.getElementById("verificationAppeal")) return;
    const panel = document.createElement("section");
    panel.id = "verificationAppeal";
    panel.style.cssText = "margin:16px 0;padding:16px;border:1px solid #8a5a32;border-radius:10px;background:#1d1711;color:#f4ede6;";
    panel.innerHTML = '<strong>Appeal this decision</strong><p style="margin:7px 0 12px;color:#cfbba9;font-size:13px;">Explain why your verification should be reviewed again. You can also submit a new document below.</p><textarea id="appealReason" maxlength="500" placeholder="Write your appeal reason" style="width:100%;min-height:74px;padding:10px;border:1px solid #765943;border-radius:7px;background:#100d0a;color:#fff;"></textarea><button id="submitAppeal" type="button" style="margin-top:10px;padding:9px 13px;border:0;border-radius:7px;background:#e2a649;color:#1c1203;font-weight:800;cursor:pointer;">Submit appeal</button><span id="appealMessage" style="display:block;margin-top:8px;color:#aee3c3;font-size:13px;"></span>';
    statusCard.after(panel);
    document.getElementById("submitAppeal").addEventListener("click", function () {
        const reason = document.getElementById("appealReason").value.trim(); if (!reason) { document.getElementById("appealMessage").textContent = "Please add an appeal reason."; return; }
        let requests = []; try { requests = JSON.parse(localStorage.getItem("tapTaskDoneVerificationRequests") || "[]"); } catch { }
        const target = requests.find(function (request) { return request.id === ownRequest.id && String(request.status).toLowerCase() === "rejected"; });
        if (target) { target.status = "Appeal pending"; target.appealReason = reason; target.appealedAt = "Just now"; localStorage.setItem("tapTaskDoneVerificationRequests", JSON.stringify(requests)); }
        const notices = (() => { try { return JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch { return []; } })(); notices.unshift({ message: "Your verification appeal was submitted for review.", time: "Just now", read: false }); localStorage.setItem("tapTaskDoneNotifications", JSON.stringify(notices.slice(0, 30)));
        localStorage.setItem("tapTaskDoneVerificationStatus", "pending"); document.getElementById("appealMessage").textContent = "Appeal submitted. The status is now pending review."; document.getElementById("submitAppeal").disabled = true;
        document.dispatchEvent(new CustomEvent("tapTaskDone:appeal-submitted"));
    });
});
