document.addEventListener("DOMContentLoaded", function () {
    const tasksKey = "tapTaskDoneTasks";
    const jobsList = document.getElementById("jobsList");
    let filter = "requests";
    let account = {};
    try { account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}"); } catch (error) { }
    const freelancerName = account.nickname || account.name || "Local freelancer";
    const freelancerService = account.service || account.profession || "Freelancer";

    function tasks() { try { const saved = JSON.parse(localStorage.getItem(tasksKey) || "[]"); return Array.isArray(saved) ? saved : []; } catch (error) { return []; } }
    function save(items) { localStorage.setItem(tasksKey, JSON.stringify(items)); }
    function notify(message, href) { let notices = []; try { notices = JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch (error) { } notices.unshift({ id:"notice-"+Date.now(), message:message, href:href || "/MyJobs", read:false, createdAt:"Just now" }); localStorage.setItem("tapTaskDoneNotifications", JSON.stringify(notices.slice(0,30))); }
    function mine(task) {
        const providerEmail = String(task.providerEmail || "").trim().toLowerCase();
        const accountEmail = String(account.email || "").trim().toLowerCase();
        if (providerEmail) return !!accountEmail && providerEmail === accountEmail;
        const providerName = String(task.providerName || "").trim().toLowerCase();
        const accountName = String(account.nickname || account.name || "").trim().toLowerCase();
        return !!providerName && !!accountName && providerName === accountName;
    }
    function ownPostedTask(task) {
        const clientEmail = String(task.clientEmail || "").trim().toLowerCase();
        const accountEmail = String(account.email || "").trim().toLowerCase();
        return !!clientEmail && !!accountEmail && clientEmail === accountEmail;
    }
    function myOffer(task) {
        const email = String(account.email || "").trim().toLowerCase();
        return (Array.isArray(task.offers) ? task.offers : []).find(function (offer) {
            return email && String(offer.email || "").trim().toLowerCase() === email;
        });
    }
    function statusLabel(task) { return ({open:"Open for offers", assigned:"Active job", completed:"Waiting for client", "client-confirmed":"Ready for client review", reviewed:"Completed"})[task.status] || task.status; }

    function render() {
        const all = tasks(); jobsList.textContent = "";
        const requests = filter === "requests" ? all.flatMap(function (task) { return (Array.isArray(task.bookingRequests) ? task.bookingRequests : []).filter(function (request) { return task.status === "open" && request.status === "pending" && request.providerEmail && String(request.providerEmail).toLowerCase() === String(account.email || "").toLowerCase(); }).map(function (request) { return { task: task, request: request }; }); }) : [];
        requests.forEach(function (entry) {
            const panel = document.createElement("article"); panel.className = "job-card";
            const title = document.createElement("h2"); title.textContent = "Booking request: " + entry.task.title;
            const info = document.createElement("p"); info.textContent = (entry.task.clientName || "Client") + " · " + (entry.task.location || "Location not set") + " · " + (entry.task.budget || "Budget not set");
            const actions = document.createElement("div"); actions.className = "job-actions";
            ["Accept request", "Decline"].forEach(function (label) {
                const button = document.createElement("button"); button.type = "button"; button.textContent = label; button.className = label === "Accept request" ? "job-primary" : "job-danger";
                button.addEventListener("click", function () {
                    const allTasks = tasks(); const task = allTasks.find(function (item) { return item.id === entry.task.id; });
                    const request = task && Array.isArray(task.bookingRequests) && task.bookingRequests.find(function (item) { return item.id === entry.request.id; });
                    if (!task || task.status !== "open" || !request || request.status !== "pending" || String(request.providerEmail).toLowerCase() !== String(account.email || "").toLowerCase()) return;
                    if (label === "Accept request" && account.availability !== "Available") { window.alert("Set your availability to Available in Settings before accepting."); return; }
                    request.status = label === "Accept request" ? "accepted" : "declined";
                    if (request.status === "accepted") { task.status = "assigned"; task.providerName = freelancerName; task.providerEmail = account.email; task.providerService = freelancerService; task.providerReviews = "New freelancer"; task.freelancerProgress = "on-the-way"; task.bookingRequests.forEach(function (other) { if (other.id !== request.id && other.status === "pending") other.status = "closed"; }); localStorage.setItem("tapTaskDoneCurrentTask", JSON.stringify(task)); }
                    save(allTasks); notify("Booking request " + request.status + " for " + task.title + ".", "/MyJobs"); render();
                }); actions.append(button);
            }); panel.append(title, info, actions); jobsList.append(panel);
        });
        const visible = all.filter(function (task) {
            if (filter === "requests") return false;
            if (filter === "offers") return task.status === "open" && !ownPostedTask(task) && !!myOffer(task);
            if (filter === "active") return mine(task) && (task.status === "assigned" || task.status === "completed" || task.status === "client-confirmed");
            return mine(task) && task.status === "reviewed";
        });
        if (!visible.length && !requests.length) {
            const emptyText = {
                requests: ["No booking requests yet", "Browser-saved booking requests for your account will appear here."],
                offers: ["No browser-saved offers yet", "View your database offers in Find Jobs, or explore available tasks."],
                active: ["No browser-saved active jobs", "Jobs accepted in the older browser-saved flow will appear here."],
                completed: ["No browser-saved completed jobs", "Completed jobs from the older browser-saved flow will appear here."]
            }[filter];
            const empty = document.createElement("div"); empty.className = "jobs-empty";
            const icon = document.createElement("span"); icon.className = "jobs-empty-icon"; icon.setAttribute("aria-hidden", "true"); icon.textContent = "▤";
            const title = document.createElement("h3"); title.textContent = emptyText[0];
            const description = document.createElement("p"); description.textContent = emptyText[1];
            empty.append(icon, title, description);
            if (filter === "offers") { const link = document.createElement("a"); link.href = "/FindPost?mode=jobs"; link.textContent = "Go to Find Jobs →"; empty.append(link); }
            jobsList.append(empty); return;
        }
        visible.slice().reverse().forEach(function (task) { jobsList.append(createCard(task)); });
    }
    function createCard(task) {
        const card = document.createElement("article"); card.className = "job-card";
        card.innerHTML = '<div class="job-card-head"><div><h2></h2><p></p></div><span class="job-status"></span></div><div class="job-meta"><span></span><span></span><span></span></div><div class="job-actions"></div>';
        card.querySelector("h2").textContent = task.title || "Untitled task";
        card.querySelector("p").textContent = task.description || "No description added.";
        card.querySelector(".job-status").textContent = statusLabel(task);
        const meta = card.querySelectorAll(".job-meta span"); meta[0].textContent = task.location || "Location to confirm"; meta[1].textContent = task.budget || "Budget to confirm"; meta[2].textContent = task.schedule || "Schedule later";
        const actions = card.querySelector(".job-actions");
        if (filter === "offers") {
            const offer = myOffer(task);
            card.querySelector(".job-status").textContent = "Offer sent · Waiting for client";
            const note = document.createElement("div"); note.className = "job-notice";
            note.textContent = "Your offer: " + (offer.price || "Budget offer") + " · " + (offer.message || "No message");
            card.append(note);
        }
        else if (mine(task)) activeControls(task, card, actions);
        return card;
    }
    function offerControls(task, card, actions) {
        const fields = document.createElement("div"); fields.className = "job-offer-fields"; fields.innerHTML = '<input placeholder="Your price offer (optional)" /><input placeholder="Short message to the client" />'; card.append(fields);
        const offer = document.createElement("button"); offer.className = "job-primary"; offer.textContent = "Send offer";
        offer.addEventListener("click", function () { if (account.availability && account.availability !== "Available") { const notice=document.createElement("div");notice.className="job-notice";notice.textContent="Your availability is set to " + account.availability + ". Change it in Settings before sending an offer.";card.append(notice);return;} const all = tasks(); const latest = all.find(function (item) { return item.id === task.id; }); if (!latest || latest.status !== "open") return; latest.offers = Array.isArray(latest.offers) ? latest.offers : []; if (latest.offers.some(function (item) { return item.email === account.email; })) { offer.textContent="Offer already sent"; offer.disabled=true; return; } const values = fields.querySelectorAll("input"); latest.offers.push({id:"offer-"+Date.now(),name:freelancerName,email:account.email,service:freelancerService,rating:"New freelancer",price:values[0].value.trim() || "Budget offer",message:values[1].value.trim() || "I am available to help with this task."}); save(all); notify("Offer sent for " + latest.title + ".", "/MyJobs"); render(); }); actions.append(offer);
    }
    function activeControls(task, card, actions) {
        if (task.status === "reviewed") {
            if (task.freelancerReview) { const done = document.createElement("div"); done.className="job-notice"; done.textContent="You reviewed this client: " + task.freelancerReview.rating + " / 5."; card.append(done); return; }
            const rate = document.createElement("button"); rate.className="job-primary"; rate.textContent="Rate client"; rate.addEventListener("click", function(){ showClientReview(task, card); }); actions.append(rate); return;
        }
        if (task.status === "assigned") { const progress = document.createElement("button"); progress.className="job-primary"; progress.textContent = task.freelancerProgress === "in-progress" ? "Mark work finished" : "Start work"; progress.addEventListener("click", function () { const nextProgress = task.freelancerProgress === "in-progress" ? "completed" : "in-progress"; update(task.id, function(item) { item.freelancerProgress = nextProgress; if (nextProgress === "completed") item.status="completed"; }); localStorage.setItem("tapTaskDoneTrackingStatus", nextProgress); notify(nextProgress === "in-progress" ? "Work started for " + task.title + "." : "Work finished for " + task.title + ".", "/Tracking"); render(); }); actions.append(progress); }
        if (task.status === "client-confirmed") { const note = document.createElement("div"); note.className="job-notice"; note.textContent="Client confirmed completion. Their review is now available in the local flow."; card.append(note); }
        const changes = document.createElement("button"); changes.textContent="Request change"; changes.addEventListener("click", function () { showChangeForm(task, card); });
        const cancel = document.createElement("button"); cancel.className="job-danger"; cancel.textContent="Request cancellation"; cancel.addEventListener("click", function () { update(task.id,function(item){ item.cancellationRequest={from:freelancerName,reason:"Freelancer requested cancellation",status:"pending"}; }); notify("Cancellation request sent for " + task.title + ".", "/MyTasks"); render(); }); actions.append(changes,cancel);
    }
    function showClientReview(task, card) {
        if (card.querySelector(".job-change-panel")) return;
        const panel=document.createElement("div"); panel.className="job-change-panel"; panel.innerHTML='<input type="number" min="1" max="5" placeholder="Rating from 1 to 5" /><textarea placeholder="Feedback for the client (optional)"></textarea><button type="button" class="job-primary">Submit client review</button>';
        panel.querySelector("button").addEventListener("click", function(){const inputs=panel.querySelectorAll("input,textarea"); const rating=Number(inputs[0].value); if(rating<1||rating>5){inputs[0].focus();return;} const review={reviewer:"freelancer",client:task.clientName || "Client",task:task.title,rating:rating,feedback:inputs[1].value.trim(),createdAt:"Just now"}; let reviews=[];try{reviews=JSON.parse(localStorage.getItem("tapTaskDoneFreelancerReviews")||"[]");}catch(error){} reviews.unshift(review);localStorage.setItem("tapTaskDoneFreelancerReviews",JSON.stringify(reviews));update(task.id,function(item){item.freelancerReview=review;});notify("Your review for the client was submitted.","/MyJobs");render();}); card.append(panel);
    }
    function showChangeForm(task, card) { if (card.querySelector(".job-change-panel")) return; const panel=document.createElement("div"); panel.className="job-change-panel"; panel.innerHTML='<input type="date" /><input placeholder="New budget (optional)" /><textarea placeholder="Explain the schedule, budget, or task-detail change"></textarea><button type="button" class="job-primary">Send change request</button>'; panel.querySelector("button").addEventListener("click",function(){const values=panel.querySelectorAll("input,textarea"); const note=values[2].value.trim(); if(!note){values[2].focus();return;} update(task.id,function(item){item.changeRequest={date:values[0].value,budget:values[1].value.trim(),note:note,from:freelancerName,status:"pending"};}); notify("Change request sent for " + task.title + ".", "/MyTasks"); render();}); card.append(panel); }
    function update(id, change) { const all=tasks(); const item=all.find(function(task){return task.id===id;}); if(item){change(item); save(all); if(mine(item))localStorage.setItem("tapTaskDoneCurrentTask",JSON.stringify(item));} }
    document.querySelectorAll(".job-tabs button").forEach(function(button){button.addEventListener("click",function(){filter=button.dataset.filter;document.querySelectorAll(".job-tabs button").forEach(function(item){item.classList.toggle("selected",item===button);item.setAttribute("aria-pressed", item === button ? "true" : "false");});render();});}); render();
});
