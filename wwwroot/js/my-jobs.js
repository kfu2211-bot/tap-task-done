document.addEventListener("DOMContentLoaded", function () {
    const tasksKey = "tapTaskDoneTasks";
    const jobsList = document.getElementById("jobsList");
    let filter = "open";
    let account = {};
    try { account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}"); } catch (error) { }
    const freelancerName = account.nickname || account.name || "Local freelancer";
    const freelancerService = account.service || account.profession || "Freelancer";

    function tasks() { try { const saved = JSON.parse(localStorage.getItem(tasksKey) || "[]"); return Array.isArray(saved) ? saved : []; } catch (error) { return []; } }
    function save(items) { localStorage.setItem(tasksKey, JSON.stringify(items)); }
    function notify(message, href) { let notices = []; try { notices = JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch (error) { } notices.unshift({ id:"notice-"+Date.now(), message:message, href:href || "/MyJobs", read:false, createdAt:"Just now" }); localStorage.setItem("tapTaskDoneNotifications", JSON.stringify(notices.slice(0,30))); }
    function mine(task) { return task.providerEmail === account.email || task.providerName === freelancerName; }
    function statusLabel(task) { return ({open:"Open for offers", assigned:"Active job", completed:"Waiting for client", "client-confirmed":"Ready for client review", reviewed:"Completed"})[task.status] || task.status; }

    function render() {
        const all = tasks(); jobsList.textContent = "";
        const visible = all.filter(function (task) {
            if (filter === "open") return task.status === "open" && !mine(task);
            if (filter === "active") return mine(task) && (task.status === "assigned" || task.status === "completed" || task.status === "client-confirmed");
            return mine(task) && task.status === "reviewed";
        });
        if (!visible.length) { jobsList.innerHTML = '<div class="jobs-empty">No jobs here yet. Switch to Open jobs to look for available tasks.</div>'; return; }
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
        if (task.status === "open") offerControls(task, card, actions);
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
        if (task.status === "assigned") { const progress = document.createElement("button"); progress.className="job-primary"; progress.textContent = task.freelancerProgress === "in-progress" ? "Mark work finished" : "Start work"; progress.addEventListener("click", function () { update(task.id, function(item) { item.freelancerProgress = item.freelancerProgress === "in-progress" ? "completed" : "in-progress"; if (item.freelancerProgress === "completed") item.status="completed"; }); localStorage.setItem("tapTaskDoneTrackingStatus", task.freelancerProgress === "in-progress" ? "in-progress" : "completed"); notify(task.freelancerProgress === "in-progress" ? "Work started for " + task.title + "." : "Work finished for " + task.title + ".", "/Tracking"); render(); }); actions.append(progress); }
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
    function update(id, change) { const all=tasks(); const item=all.find(function(task){return task.id===id;}); if(item){change(item); save(all); if(item.providerEmail===account.email||item.providerName===freelancerName)localStorage.setItem("tapTaskDoneCurrentTask",JSON.stringify(item));} }
    document.querySelectorAll(".job-tabs button").forEach(function(button){button.addEventListener("click",function(){filter=button.dataset.filter;document.querySelectorAll(".job-tabs button").forEach(function(item){item.classList.toggle("selected",item===button);});render();});}); render();
});


