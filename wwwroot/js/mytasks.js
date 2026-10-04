document.addEventListener("DOMContentLoaded", function () {
    const tasksKey = "tapTaskDoneTasks";
    const selectedKey = "tapTaskDoneSelectedTaskId";
    const taskList = document.getElementById("myTaskList");
    const confirmButton = document.getElementById("confirmDetailsButton");
    const confirmTaskModal = document.getElementById("confirmTaskModal");
    const cancelConfirmTaskButton = document.getElementById("cancelConfirmTaskButton");
    const submitConfirmTaskButton = document.getElementById("submitConfirmTaskButton");
    let selectedTask = null;
    let activeFilter = "all";

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element) element.textContent = value || "—";
    }

    function taskSignature(task) {
        return [task.title, task.description, task.location, task.budget, task.schedule].join("|").toLowerCase();
    }

    function statusWeight(status) {
        return { "assigned": 5, "completed": 4, "client-confirmed": 4, "open": 3, "pending-confirmation": 2 }[status] || 1;
    }

    function removeDuplicateTasks(tasks) {
        const uniqueTasks = new Map();
        tasks.forEach(function (task) {
            if (!task || typeof task !== "object") return;
            if (!task.id) task.id = "task-" + Date.now() + "-" + Math.random().toString(16).slice(2);
            const key = taskSignature(task);
            const existing = uniqueTasks.get(key);
            if (!existing || statusWeight(task.status) >= statusWeight(existing.status)) {
                uniqueTasks.set(key, task);
            }
        });
        return Array.from(uniqueTasks.values());
    }

    function readTasks() {
        let tasks = [];
        try { tasks = JSON.parse(localStorage.getItem(tasksKey) || "[]"); } catch (error) { tasks = []; }
        if (!Array.isArray(tasks)) tasks = [];

        // Preserve tasks created before the multiple-task feature.
        try {
            const legacyTask = JSON.parse(localStorage.getItem("tapTaskDoneCurrentTask") || "null");
            if (legacyTask) {
                const matchingTask = tasks.find(function (task) { return taskSignature(task) === taskSignature(legacyTask); });
                if (!matchingTask) {
                    legacyTask.id = legacyTask.id || "legacy-" + Date.now();
                    tasks.unshift(legacyTask);
                }
            }
        } catch (error) { }

        return removeDuplicateTasks(tasks);
    }

    function saveTasks(tasks) {
        localStorage.setItem(tasksKey, JSON.stringify(removeDuplicateTasks(tasks)));
    }

    function taskStatusLabel(status) {
        const labels = {
            "pending-confirmation": "Waiting for confirmation",
            "open": "Open for freelancer offers",
            "assigned": "Provider assigned",
            "completed": "Waiting for completion review",
            "client-confirmed": "Ready for rating"
            ,"cancelled": "Cancelled"
        };
        return labels[status] || "Draft";
    }

    function renderTaskList(tasks) {
        taskList.textContent = "";
        if (!tasks.length) {
            taskList.innerHTML = "<p class='task-card-meta'>You have not posted a task yet.</p>";
            return;
        }
        const filteredTasks = tasks.filter(function (task) {
            if (activeFilter === "open") return task.status === "open" || task.status === "pending-confirmation";
            if (activeFilter === "active") return task.status === "assigned";
            if (activeFilter === "completed") return task.status === "completed" || task.status === "client-confirmed";
            return task.status !== "cancelled";
        });
        if (!filteredTasks.length) {
            taskList.innerHTML = "<p class='task-card-meta'>No tasks in this filter.</p>";
            return;
        }
        filteredTasks.slice().reverse().forEach(function (task) {
            const card = document.createElement("button");
            card.type = "button";
            card.className = "my-task-card" + (selectedTask && task.id === selectedTask.id ? " selected" : "");
            const title = document.createElement("strong");
            title.textContent = task.title || "Untitled task";
            const meta = document.createElement("span");
            meta.className = "task-card-meta";
            meta.textContent = (task.location || "Location not set") + " · " + (task.budget || "Budget not set");
            const status = document.createElement("span");
            status.className = "task-card-status";
            status.textContent = taskStatusLabel(task.status);
            card.append(title, meta, status);
            card.addEventListener("click", function () {
                localStorage.setItem(selectedKey, task.id);
                selectedTask = task;
                renderAll();
            });
            taskList.append(card);
        });
    }

    function renderProviderAssignment(task) {
        const title = document.getElementById("providerSectionTitle");
        const message = document.getElementById("providerAssignmentMessage");
        const offers = document.getElementById("providerOffers");
        const providerButton = document.getElementById("providerButton");
        offers.textContent = "";
        providerButton.style.display = task.status === "assigned" ? "flex" : "none";

        if (task.status === "assigned") {
            title.textContent = "Assigned provider";
            message.textContent = (task.providerName || "Your provider") + " accepted this task. It is now active in Tracking.";
            setText("providerName", task.providerName || "Ofrac");
            setText("providerService", task.providerService || "Service provider");
            setText("providerReviews", task.providerReviews || "New provider");
            return;
        }
        if (task.status !== "open") {
            title.textContent = "Provider assignment";
            message.textContent = "Confirm the details first so freelancers can send an offer.";
            return;
        }
        const bookingRequests = Array.isArray(task.bookingRequests) ? task.bookingRequests.filter(function (request) { return request.status === "pending"; }) : [];
        if (bookingRequests.length) {
            title.textContent = "Booking request sent";
            message.textContent = "Waiting for " + bookingRequests.map(function (request) { return request.providerName; }).join(", ") + " to accept or decline. In this local demo, open Find Jobs and select that freelancer in the demo inbox.";
            return;
        }
        title.textContent = "Freelancer offers";
        const offersForTask = Array.isArray(task.offers) ? task.offers : [];
        if (!offersForTask.length) {
            message.textContent = "Your task is open. Waiting for a freelancer to send an offer.";
            return;
        }
        message.textContent = "Choose the freelancer you want to assign to this task.";
        offersForTask.forEach(function (offer) {
            const row = document.createElement("div");
            row.className = "provider-offer-row";
            const info = document.createElement("div");
            const name = document.createElement("strong");
            name.textContent = offer.name;
            const details = document.createElement("span");
            details.textContent = offer.service + " · " + offer.rating + " · " + (offer.price || "Budget offer") + " · " + offer.message;
            info.append(name, details);
            const accept = document.createElement("button");
            accept.type = "button";
            accept.textContent = "Accept offer";
            accept.addEventListener("click", function () {
                task.status = "assigned";
                task.providerName = offer.name;
                task.providerService = offer.service;
                task.providerReviews = offer.rating;
                const tasks = readTasks().map(function (item) { return item.id === task.id ? task : item; });
                saveTasks(tasks);
                localStorage.setItem("tapTaskDoneCurrentTask", JSON.stringify(task));
                localStorage.setItem("tapTaskDoneTrackingStatus", "on-the-way");
                let notifications = [];
                try { notifications = JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch (error) { notifications = []; }
                notifications.push({ id: "notice-" + Date.now(), message: "Offer accepted: " + offer.name + " for " + task.title, read: false });
                localStorage.setItem("tapTaskDoneNotifications", JSON.stringify(notifications.slice(-30)));
                window.location.href = "/Tracking";
            });
            const decline = document.createElement("button");
            decline.type = "button";
            decline.className = "decline-offer-button";
            decline.textContent = "Decline";
            decline.addEventListener("click", function () {
                task.offers = task.offers.filter(function (item) { return item.id !== offer.id; });
                saveTasks(readTasks().map(function (item) { return item.id === task.id ? task : item; }));
                renderAll();
            });
            const actions = document.createElement("div");
            actions.className = "offer-actions";
            actions.append(accept, decline);
            row.append(info, actions);
            offers.append(row);
        });

        if (task.status === "pending-confirmation" || task.status === "open") {
            const cancel = document.createElement("button");
            cancel.type = "button";
            cancel.className = "cancel-task-button";
            cancel.textContent = "Cancel task";
            cancel.addEventListener("click", function () {
                if (!window.confirm("Cancel this task? Freelancers will no longer be able to send offers.")) return;
                task.status = "cancelled";
                saveTasks(readTasks().map(function (item) { return item.id === task.id ? task : item; }));
                renderAll();
            });
            offers.append(cancel);
        }
    }

    function renderDetails(task) {
        setText("taskTitle", task.title);
        setText("taskDescription", task.description);
        setText("taskLocation", task.location);
        setText("taskBudget", task.budget);
        setText("taskSchedule", task.schedule || "To be scheduled");
        setText("serviceFee", task.budget || "₱0");
        setText("platformFee", "₱30.00");
        const amount = Number(String(task.budget || "").replace(/[^0-9.]/g, "")) || 0;
        setText("totalPrice", "₱" + (amount + 30).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
        confirmButton.disabled = task.status === "open" || task.status === "assigned";
        confirmButton.style.opacity = confirmButton.disabled ? "0.6" : "1";
        confirmButton.textContent = task.status === "open" ? "Waiting for offers" : task.status === "assigned" ? "Provider assigned" : "Confirm details";
        renderProviderAssignment(task);
    }

    function renderAll() {
        const tasks = readTasks();
        saveTasks(tasks);
        const selectedId = localStorage.getItem(selectedKey);
        selectedTask = tasks.find(function (task) { return task.id === selectedId; }) || tasks[tasks.length - 1] || null;
        if (selectedTask) localStorage.setItem(selectedKey, selectedTask.id);
        renderTaskList(tasks);
        if (selectedTask) renderDetails(selectedTask);
    }

    function openConfirmTaskModal() {
        if (!selectedTask) return;
        setText("confirmModalTitle", selectedTask.title);
        setText("confirmModalCategory", selectedTask.category || selectedTask.providerService || "Other / General");
        setText("confirmModalLocation", selectedTask.location);
        setText("confirmModalBudget", selectedTask.budget);
        confirmTaskModal.classList.remove("hidden");
    }

    function confirmTaskDetails() {
        if (!selectedTask) return;
        selectedTask.status = "open";
        selectedTask.offers = Array.isArray(selectedTask.offers) ? selectedTask.offers : [];
        const tasks = readTasks().map(function (task) { return task.id === selectedTask.id ? selectedTask : task; });
        saveTasks(tasks);
        confirmTaskModal.classList.add("hidden");
        renderAll();
    }

    confirmButton.addEventListener("click", openConfirmTaskModal);
    cancelConfirmTaskButton.addEventListener("click", function () { confirmTaskModal.classList.add("hidden"); });
    submitConfirmTaskButton.addEventListener("click", confirmTaskDetails);
    confirmTaskModal.addEventListener("click", function (event) { if (event.target === confirmTaskModal) confirmTaskModal.classList.add("hidden"); });

    document.querySelectorAll("#taskFilters button").forEach(function (button) {
        button.addEventListener("click", function () {
            activeFilter = button.dataset.filter;
            document.querySelectorAll("#taskFilters button").forEach(function (item) { item.classList.toggle("selected", item === button); });
            renderAll();
        });
    });

    renderAll();
});
