document.addEventListener("DOMContentLoaded", function () {


    /* =====================================================
       ACCOUNT
    ===================================================== */

    const account =
        localStorage.getItem("tapTaskDoneAccount");


    if (!account) {

        window.location.href = "/Login";

        return;

    }


    try {

        const user =
            JSON.parse(account);

        const displayName =
            user.nickname || user.name || "User";


        const sidebarName =
            document.getElementById("sidebarName");


        const sidebarEmail =
            document.getElementById("sidebarEmail");


        const userAvatar =
            document.getElementById("userAvatar");


        if (sidebarName) {

            sidebarName.textContent =
                displayName;

        }


        if (sidebarEmail && user.email) {

            sidebarEmail.textContent =
                user.email;

        }


        if (userAvatar) {

            const nameParts =
                displayName
                    .trim()
                    .split(/\s+/);


            let initials = "";


            if (nameParts.length >= 2) {

                initials =
                    nameParts[0].charAt(0) +
                    nameParts[nameParts.length - 1].charAt(0);

            }
            else {

                initials =
                    nameParts[0].substring(0, 2);

            }


            userAvatar.textContent =
                initials.toUpperCase();

        }

    }
    catch (error) {

        console.error(
            "Unable to load account.",
            error
        );

    }



    /* =====================================================
       MODE BUTTONS
    ===================================================== */

    const findFreelancerButton =
        document.getElementById(
            "findFreelancerButton"
        );


    const postTaskButton =
        document.getElementById(
            "postTaskButton"
        );


    const findJobsButton =
        document.getElementById(
            "findJobsButton"
        );


    const findFreelancerSection =
        document.getElementById(
            "findFreelancerSection"
        );


    const postTaskSection =
        document.getElementById(
            "postTaskSection"
        );


    const findJobsSection =
        document.getElementById(
            "findJobsSection"
        );


    function setActiveMode(button) {

        document
            .querySelectorAll(".mode-button")
            .forEach(function (item) {

                item.classList.remove("active");

            });


        button.classList.add("active");

    }


    function showFindFreelancer() {

        setActiveMode(
            findFreelancerButton
        );


        findFreelancerSection
            .classList.remove("hidden");


        postTaskSection
            .classList.add("hidden");


        findJobsSection
            .classList.add("hidden");

    }


    function showPostTask() {

        setActiveMode(
            postTaskButton
        );


        findFreelancerSection
            .classList.add("hidden");


        postTaskSection
            .classList.remove("hidden");


        findJobsSection
            .classList.add("hidden");

    }


    function showFindJobs() {

        setActiveMode(
            findJobsButton
        );


        findFreelancerSection
            .classList.add("hidden");


        postTaskSection
            .classList.add("hidden");


        findJobsSection
            .classList.remove("hidden");

        renderFindJobs();

    }


    findFreelancerButton
        .addEventListener(
            "click",
            showFindFreelancer
        );


    postTaskButton
        .addEventListener(
            "click",
            showPostTask
        );


    findJobsButton
        .addEventListener(
            "click",
            showFindJobs
        );


    /* =====================================================
       SEARCH
    ===================================================== */

    const serviceSearch =
        document.getElementById(
            "serviceSearch"
        );


    const searchButton =
        document.getElementById(
            "searchButton"
        );


    const freelancerCards =
        document.querySelectorAll(
            ".freelancer-card"
        );


    const noResults =
        document.getElementById(
            "noResults"
        );


    function performSearch() {

        const searchValue =
            serviceSearch
                .value
                .trim()
                .toLowerCase();


        let foundCount = 0;


        freelancerCards.forEach(
            function (card) {

                const service =
                    card.dataset.service
                        .toLowerCase();


                const freelancerName =
                    card
                        .querySelector(
                            ".freelancer-info strong"
                        )
                        .textContent
                        .toLowerCase();


                const matches =
                    searchValue === "" ||
                    service.includes(searchValue) ||
                    freelancerName.includes(searchValue);


                if (matches) {

                    card.style.display =
                        "flex";

                    foundCount++;

                }
                else {

                    card.style.display =
                        "none";

                }

            }
        );


        if (foundCount === 0) {

            noResults.classList.add("show");

        }
        else {

            noResults.classList.remove("show");

        }

    }


    searchButton.addEventListener(
        "click",
        performSearch
    );


    serviceSearch.addEventListener(
        "keydown",
        function (event) {

            if (event.key === "Enter") {

                performSearch();

            }

        }
    );



    /* =====================================================
       CURRENT TASK
    ===================================================== */

    const acceptTaskButton =
        document.getElementById(
            "acceptTaskButton"
        );


    const declineTaskButton =
        document.getElementById(
            "declineTaskButton"
        );


    const taskStatus =
        document.getElementById(
            "taskStatus"
        );


    if (acceptTaskButton && declineTaskButton && taskStatus) acceptTaskButton.addEventListener(
        "click",
        function () {

            taskStatus.textContent =
                "Accepted";


            taskStatus.classList.remove(
                "declined"
            );


            taskStatus.classList.add(
                "accepted"
            );


            showMessage(
                "Task accepted",
                "You accepted the Fix a Leaky Faucet task."
            );

        }
    );


    if (acceptTaskButton && declineTaskButton && taskStatus) declineTaskButton.addEventListener(
        "click",
        function () {

            taskStatus.textContent =
                "Declined";


            taskStatus.classList.remove(
                "accepted"
            );


            taskStatus.classList.add(
                "declined"
            );


            showMessage(
                "Task declined",
                "The current task has been declined."
            );

        }
    );



    /* =====================================================
       FREELANCER DETAILS
    ===================================================== */

    const arrowButtons =
        document.querySelectorAll(
            ".arrow-button"
        );

    const providerDetailPanel = document.getElementById("providerDetailPanel");
    const sendBookingRequestButton = document.getElementById("sendBookingRequestButton");
    let selectedProvider = null;

    function sendBookingRequest(provider) {
        const selectedId = localStorage.getItem("tapTaskDoneSelectedTaskId");
        const tasks = readPostedTasks();
        const task = tasks.find(function (item) { return item.id === selectedId; });
        if (!task) { showMessage("Choose a task first", "Post a task, then select it in My Tasks before sending a booking request."); return; }
        if (task.status !== "open") { showMessage("Task is not ready", "Confirm the task details in My Tasks first. Freelancers can receive requests after the task is open."); return; }
        task.bookingRequests = Array.isArray(task.bookingRequests) ? task.bookingRequests : [];
        if (task.bookingRequests.some(function (request) { return request.providerName === provider.name && request.status === "pending"; })) { showMessage("Request already sent", provider.name + " already has a pending booking request for this task."); return; }
        task.bookingRequests.push({ id: "request-" + Date.now(), providerName: provider.name, providerService: provider.service, providerReviews: provider.rating, price: provider.price, status: "pending" });
        savePostedTasks(tasks);
        addNotification("Booking request sent to " + provider.name + " for " + task.title);
        showMessage("Booking request sent", provider.name + " can now review this request in the Freelancer demo inbox.");
    }


    arrowButtons.forEach(
        function (button) {

            button.addEventListener(
                "click",
                function () {

                    const name = button.dataset.name.replace(/[\u00ad\u200b]/g, "").trim();
                    const card = button.closest(".freelancer-card");
                    const service = card.querySelector(".freelancer-info span").textContent.trim();
                    const rating = "★ " + card.querySelector(".freelancer-rating strong").textContent.trim();
                    const price = card.querySelector(".freelancer-price").textContent.trim();
                    selectedProvider = { name: name, service: service, rating: rating, price: price };
                    document.getElementById("selectedProviderName").textContent = name;
                    document.getElementById("selectedProviderMeta").textContent = service + " · Trusted local freelancer";
                    document.getElementById("selectedProviderRating").textContent = rating;
                    document.getElementById("selectedProviderPrice").textContent = price + " per task";
                    providerDetailPanel.classList.remove("hidden");
                    providerDetailPanel.scrollIntoView({ behavior: "smooth", block: "nearest" });

                }
            );

        }
    );

    if (sendBookingRequestButton) sendBookingRequestButton.addEventListener("click", function () { if (selectedProvider) sendBookingRequest(selectedProvider); });
    const closeProviderDetail = document.getElementById("closeProviderDetail");
    if (closeProviderDetail) closeProviderDetail.addEventListener("click", function () { providerDetailPanel.classList.add("hidden"); });



    /* =====================================================
       POST TASK BUTTON
    ===================================================== */

    const startPostTaskButton =
        document.getElementById(
            "startPostTaskButton"
        );

    const postTaskStart = document.getElementById("postTaskStart");
    const postTaskForm = document.getElementById("postTaskForm");
    const postTaskSuccess = document.getElementById("postTaskSuccess");
    const cancelPostTaskButton = document.getElementById("cancelPostTaskButton");
    const postAnotherTaskButton = document.getElementById("postAnotherTaskButton");
    const viewPostedTaskButton = document.getElementById("viewPostedTaskButton");

    const tasksKey = "tapTaskDoneTasks";

    // A familiar Month / Day / Year picker, followed by a separate time picker.
    // It gives clients full control without relying on a small browser calendar.
    const scheduleValues = { month: "", day: "", year: "", hour: "", minute: "", period: "" };
    const scheduleDropdowns = {
        month: document.getElementById("scheduleMonthDropdown"),
        day: document.getElementById("scheduleDayDropdown"),
        year: document.getElementById("scheduleYearDropdown"),
        hour: document.getElementById("scheduleHourDropdown"),
        minute: document.getElementById("scheduleMinuteDropdown"),
        period: document.getElementById("schedulePeriodDropdown")
    };
    const schedulePlaceholders = { month: "Month", day: "Day", year: "Year", hour: "Hour", minute: "Minute", period: "AM / PM" };
    const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    function setScheduleOptions(dropdown, options) {
        if (!dropdown) return;
        const menu = dropdown.querySelector(".schedule-dropdown-menu");
        menu.textContent = "";
        options.forEach(function (option) {
            const item = document.createElement("button");
            item.type = "button";
            item.className = "schedule-dropdown-option";
            item.dataset.value = option.value;
            item.textContent = option.label;
            menu.append(item);
        });
    }

    function setScheduleLabel(name, label) {
        const dropdown = scheduleDropdowns[name];
        if (!dropdown) return;
        dropdown.querySelector(".schedule-dropdown-button span").textContent = label;
    }

    function setupScheduleDropdown(name, onSelect) {
        const dropdown = scheduleDropdowns[name];
        if (!dropdown) return;
        const button = dropdown.querySelector(".schedule-dropdown-button");
        const menu = dropdown.querySelector(".schedule-dropdown-menu");

        button.addEventListener("click", function (event) {
            event.stopPropagation();
            Object.keys(scheduleDropdowns).forEach(function (key) {
                if (key !== name && scheduleDropdowns[key]) scheduleDropdowns[key].classList.remove("active");
            });
            dropdown.classList.toggle("active");
        });

        menu.addEventListener("click", function (event) {
            const option = event.target.closest(".schedule-dropdown-option");
            if (!option) return;
            scheduleValues[name] = option.dataset.value;
            setScheduleLabel(name, option.textContent);
            dropdown.classList.remove("active");
            if (onSelect) onSelect(option.dataset.value);
        });
    }

    function updateScheduleDays() {
        const month = Number(scheduleValues.month);
        const year = Number(scheduleValues.year) || new Date().getFullYear();
        const days = month ? new Date(year, month, 0).getDate() : 31;
        const options = [];
        for (let day = 1; day <= days; day++) options.push({ value: String(day), label: String(day) });
        setScheduleOptions(scheduleDropdowns.day, options);
        if (Number(scheduleValues.day) > days) {
            scheduleValues.day = "";
            setScheduleLabel("day", schedulePlaceholders.day);
        }
    }

    function resetSchedulePicker() {
        Object.keys(scheduleValues).forEach(function (key) {
            scheduleValues[key] = "";
            setScheduleLabel(key, schedulePlaceholders[key]);
            if (scheduleDropdowns[key]) scheduleDropdowns[key].classList.remove("active");
        });
        updateScheduleDays();
    }

    function selectedSchedule() {
        const selected = Object.keys(scheduleValues).filter(function (key) { return scheduleValues[key] !== ""; });
        if (!selected.length) return { value: "To be scheduled" };
        if (selected.length !== Object.keys(scheduleValues).length) {
            return { error: "Please choose the complete date and time, or leave every schedule field empty." };
        }

        let hour = Number(scheduleValues.hour);
        if (scheduleValues.period === "PM" && hour !== 12) hour += 12;
        if (scheduleValues.period === "AM" && hour === 12) hour = 0;
        const date = new Date(Number(scheduleValues.year), Number(scheduleValues.month) - 1, Number(scheduleValues.day), hour, Number(scheduleValues.minute));
        // This is a local prototype, so preserve the exact date and time chosen
        // by the user. Future-only scheduling can be enforced later with the
        // database and real availability rules.
        return {
            value: date.toLocaleString("en-PH", {
                month: "short", day: "numeric", year: "numeric", hour: "numeric", minute: "2-digit"
            })
        };
    }

    if (scheduleDropdowns.month) {
        setScheduleOptions(scheduleDropdowns.month, monthNames.map(function (name, index) { return { value: String(index + 1), label: name }; }));
        setScheduleOptions(scheduleDropdowns.year, Array.from({ length: 4 }, function (_, index) {
            const year = new Date().getFullYear() + index;
            return { value: String(year), label: String(year) };
        }));
        setScheduleOptions(scheduleDropdowns.hour, Array.from({ length: 12 }, function (_, index) { const hour = index + 1; return { value: String(hour), label: String(hour) }; }));
        setScheduleOptions(scheduleDropdowns.minute, Array.from({ length: 12 }, function (_, index) { const minute = String(index * 5).padStart(2, "0"); return { value: minute, label: minute }; }));
        setScheduleOptions(scheduleDropdowns.period, [{ value: "AM", label: "AM" }, { value: "PM", label: "PM" }]);
        updateScheduleDays();

        setupScheduleDropdown("month", updateScheduleDays);
        setupScheduleDropdown("day");
        setupScheduleDropdown("year", updateScheduleDays);
        setupScheduleDropdown("hour");
        setupScheduleDropdown("minute");
        setupScheduleDropdown("period");
        document.addEventListener("click", function () {
            Object.keys(scheduleDropdowns).forEach(function (key) { if (scheduleDropdowns[key]) scheduleDropdowns[key].classList.remove("active"); });
        });
    }

    function currentFreelancer() {
        try {
            const account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}");
            return {
                name: account.nickname || account.name || "Freelancer",
                service: account.service || account.profession || "Freelancer",
                rating: "New freelancer",
                availability: account.availability || "Available"
            };
        }
        catch (error) {
            return { name: "Freelancer", service: "Freelancer", rating: "New freelancer", availability: "Available" };
        }
    }

    function taskSignature(task) {
        return [task.title, task.description, task.location, task.budget, task.schedule].join("|").toLowerCase();
    }

    function taskStatusWeight(status) {
        return { "assigned": 5, "completed": 4, "client-confirmed": 4, "open": 3, "pending-confirmation": 2 }[status] || 1;
    }

    function removeDuplicatePostedTasks(tasks) {
        const uniqueTasks = new Map();
        tasks.forEach(function (task) {
            if (!task || typeof task !== "object") return;
            const key = taskSignature(task);
            const existing = uniqueTasks.get(key);
            if (!existing || taskStatusWeight(task.status) >= taskStatusWeight(existing.status)) {
                uniqueTasks.set(key, task);
            }
        });
        return Array.from(uniqueTasks.values());
    }

    function readPostedTasks() {
        try {
            const tasks = JSON.parse(localStorage.getItem(tasksKey) || "[]");
            if (!Array.isArray(tasks)) return [];
            return removeDuplicatePostedTasks(tasks);
        }
        catch (error) {
            return [];
        }
    }

    function savePostedTasks(tasks) {
        localStorage.setItem(tasksKey, JSON.stringify(removeDuplicatePostedTasks(tasks)));
    }

    function addNotification(message) {
        let notifications = [];
        try { notifications = JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch (error) { notifications = []; }
        notifications.push({ id: "notice-" + Date.now(), message: message, read: false, createdAt: new Date().toLocaleTimeString([], { hour: "numeric", minute: "2-digit" }) });
        localStorage.setItem("tapTaskDoneNotifications", JSON.stringify(notifications.slice(-30)));
    }

    function showPostTaskForm() {
        postTaskStart.classList.add("hidden");
        postTaskSuccess.classList.add("hidden");
        postTaskForm.classList.remove("hidden");
    }

    function showPostTaskStart() {
        postTaskForm.classList.add("hidden");
        postTaskSuccess.classList.add("hidden");
        postTaskStart.classList.remove("hidden");
    }

    if (startPostTaskButton && postTaskForm && postTaskSuccess) {
        startPostTaskButton.addEventListener("click", showPostTaskForm);

        cancelPostTaskButton.addEventListener("click", showPostTaskStart);

        postTaskForm.addEventListener("submit", function (event) {
            event.preventDefault();

            if (!postTaskForm.checkValidity()) {
                postTaskForm.reportValidity();
                return;
            }

            const title = document.getElementById("taskTitleInput").value.trim();
            const description = document.getElementById("taskDescriptionInput").value.trim();
            const location = document.getElementById("taskLocationInput").value.trim();
            const budget = document.getElementById("taskBudgetInput").value.trim();
            const scheduleResult = selectedSchedule();
            if (scheduleResult.error) {
                window.alert(scheduleResult.error);
                return;
            }
            const schedule = scheduleResult.value;
            const category = document.querySelector("input[name='taskCategory']:checked").value;

            document.getElementById("postedTaskTitle").textContent = title;
            document.getElementById("postedTaskCategory").textContent = category;
            document.getElementById("postedTaskBudget").textContent = "₱" + budget;
            document.getElementById("postedTaskLocation").textContent = location;
            document.getElementById("postedTaskSchedule").textContent = schedule;

            const newTask = {
                id: "task-" + Date.now() + "-" + Math.random().toString(16).slice(2),
                title: title,
                description: description,
                location: location,
                budget: "₱" + budget,
                schedule: schedule,
                providerName: "Waiting for a provider",
                providerService: category,
                providerReviews: "New task",
                status: "pending-confirmation",
                offers: []
            };
            const postedTasks = readPostedTasks();
            postedTasks.push(newTask);
            savePostedTasks(postedTasks);
            addNotification("Task posted: " + title);
            localStorage.setItem("tapTaskDoneSelectedTaskId", newTask.id);
            sessionStorage.removeItem("tapTaskDoneShowRating");

            postTaskForm.classList.add("hidden");
            postTaskSuccess.classList.remove("hidden");
        });

        postAnotherTaskButton.addEventListener("click", function () {
            postTaskForm.reset();
            resetSchedulePicker();
            showPostTaskForm();
        });

        viewPostedTaskButton.addEventListener("click", function () {
            window.location.href = "/MyTasks";
        });
    }



    /* =====================================================
       FIND JOBS BUTTON
    ===================================================== */

    const findJobsContent = document.getElementById("findJobsContent");

    function renderFindJobs() {
        if (!findJobsContent) return;

        const tasks = readPostedTasks();
        const demoNames = ["Ofracio", "Marisol", "Dante", "Lea"];
        let demoFreelancer = localStorage.getItem("tapTaskDoneDemoFreelancer") || "Ofracio";
        if (!demoNames.includes(demoFreelancer)) demoFreelancer = "Ofracio";
        const inbox = document.createElement("section");
        inbox.className = "demo-inbox";
        const inboxTitle = document.createElement("h2"); inboxTitle.textContent = "Incoming booking requests";
        const inboxHelp = document.createElement("p"); inboxHelp.textContent = "Local demo: choose a freelancer to see and respond to their requests.";
        const selector = document.createElement("select"); selector.className = "demo-provider-select";
        demoNames.forEach(function (name) { const option = document.createElement("option"); option.value = name; option.textContent = name + " — freelancer demo"; option.selected = name === demoFreelancer; selector.append(option); });
        selector.addEventListener("change", function () { localStorage.setItem("tapTaskDoneDemoFreelancer", selector.value); renderFindJobs(); });
        inbox.append(inboxTitle, inboxHelp, selector);
        const requests = tasks.flatMap(function (task) { return (Array.isArray(task.bookingRequests) ? task.bookingRequests : []).filter(function (request) { return request.providerName === demoFreelancer && request.status === "pending"; }).map(function (request) { return { task: task, request: request }; }); });
        if (!requests.length) { const emptyInbox = document.createElement("p"); emptyInbox.className = "demo-inbox-empty"; emptyInbox.textContent = "No booking requests for " + demoFreelancer + " yet."; inbox.append(emptyInbox); }
        requests.forEach(function (item) {
            const requestCard = document.createElement("div"); requestCard.className = "booking-request-card";
            const requestInfo = document.createElement("div"); const requestTitle = document.createElement("strong"); requestTitle.textContent = item.task.title; const requestMeta = document.createElement("span"); requestMeta.textContent = (item.task.location || "Location to be confirmed") + " · " + (item.task.budget || "Budget to be confirmed"); requestInfo.append(requestTitle, requestMeta);
            const actions = document.createElement("div"); actions.className = "booking-request-actions";
            const decline = document.createElement("button"); decline.type = "button"; decline.className = "decline-offer-button"; decline.textContent = "Decline";
            decline.addEventListener("click", function () { item.request.status = "declined"; savePostedTasks(tasks); addNotification(demoFreelancer + " declined the booking request for " + item.task.title); renderFindJobs(); });
            const accept = document.createElement("button"); accept.type = "button"; accept.textContent = "Accept request";
            accept.addEventListener("click", function () { item.request.status = "accepted"; item.task.status = "assigned"; item.task.providerName = item.request.providerName; item.task.providerService = item.request.providerService; item.task.providerReviews = item.request.providerReviews; savePostedTasks(tasks); localStorage.setItem("tapTaskDoneCurrentTask", JSON.stringify(item.task)); localStorage.setItem("tapTaskDoneTrackingStatus", "on-the-way"); addNotification(demoFreelancer + " accepted your booking request for " + item.task.title); renderFindJobs(); });
            actions.append(decline, accept); requestCard.append(requestInfo, actions); inbox.append(requestCard);
        });
        findJobsContent.append(inbox);
        const openTasks = tasks.filter(function (task) { return task.status === "open" || task.status === "Confirmed"; });
        findJobsContent.textContent = "";

        if (!openTasks.length) {
            const empty = document.createElement("div");
            empty.className = "mode-placeholder";
            const icon = document.createElement("div");
            icon.className = "placeholder-icon";
            icon.textContent = "□";
            const title = document.createElement("h2");
            title.textContent = "No open tasks yet";
            const text = document.createElement("p");
            text.textContent = tasks.length
                ? "The client needs to confirm a task before freelancers can send an offer."
                : "Open tasks from clients will appear here.";
            empty.append(icon, title, text);
            findJobsContent.append(empty);
            return;
        }

        openTasks.forEach(function (task) {
        if (task.status === "Confirmed") task.status = "open";
        const card = document.createElement("article");
        card.className = "job-offer-card";
        const eyebrow = document.createElement("span");
        eyebrow.className = "job-offer-eyebrow";
        eyebrow.textContent = "Open task · Freelancer view";
        const title = document.createElement("h2");
        title.textContent = task.title || "Untitled task";
        const details = document.createElement("p");
        details.className = "job-offer-details";
        details.textContent = (task.description || "No description provided.") + " · " +
            (task.location || "Location not set") + " · " + (task.budget || "Budget not set");
        const freelancer = currentFreelancer();
        const notice = document.createElement("p");
        notice.className = "job-offer-note";
        notice.textContent = "Freelancer: " + freelancer.name + " · " + freelancer.service + " · " + freelancer.rating + " · " + freelancer.availability;
        const offerFields = document.createElement("div");
        offerFields.className = "job-offer-fields";
        const priceInput = document.createElement("input");
        priceInput.type = "text";
        priceInput.maxLength = 30;
        priceInput.placeholder = "Your offer price (optional)";
        const messageInput = document.createElement("input");
        messageInput.type = "text";
        messageInput.maxLength = 160;
        messageInput.placeholder = "Short message to client";
        offerFields.append(priceInput, messageInput);
        const offerButton = document.createElement("button");
        offerButton.type = "button";
        const hasOffer = Array.isArray(task.offers) && task.offers.some(function (offer) {
            return offer.name === freelancer.name;
        });
        offerButton.textContent = hasOffer ? "Offer sent" : freelancer.availability === "Available" ? "Send offer as " + freelancer.name : "Unavailable — " + freelancer.availability;
        offerButton.disabled = hasOffer || freelancer.availability !== "Available";
        offerButton.addEventListener("click", function () {
            const latestTasks = readPostedTasks();
            const latestTask = latestTasks.find(function (item) { return item.id === task.id; });
            if (!latestTask || latestTask.status !== "open") return;
            latestTask.offers = Array.isArray(latestTask.offers) ? latestTask.offers : [];
            if (!latestTask.offers.some(function (offer) { return offer.name === freelancer.name; })) {
                latestTask.offers.push({
                    id: "offer-" + Date.now(),
                    name: freelancer.name,
                    service: freelancer.service,
                    rating: freelancer.rating,
                    price: priceInput.value.trim() || "Budget offer",
                    message: messageInput.value.trim() || "I can help with this task. I am available today."
                });
                savePostedTasks(latestTasks);
                addNotification("Offer sent for: " + latestTask.title);
            }
            renderFindJobs();
        });
        card.append(eyebrow, title, details, notice, offerFields, offerButton);
        findJobsContent.append(card);
        });
    }

    /* =====================================================
       OPEN SPECIFIC MODE FROM ANOTHER PAGE
       This runs here because Find Jobs is now fully available.
    ===================================================== */

    const requestedMode = new URLSearchParams(window.location.search).get("mode");

    if (requestedMode === "post") {
        showPostTask();
    }
    else if (requestedMode === "jobs") {
        showFindJobs();
    }
    else {
        showFindFreelancer();
    }



    /* =====================================================
       NOTIFICATION
    ===================================================== */

    const notificationButton =
        document.getElementById(
            "notificationButton"
        );


    notificationButton.addEventListener(
        "click",
        function () {

            showMessage(
                "Notifications",
                "You currently have no new notifications."
            );

        }
    );



    /* =====================================================
       USER MENU
    ===================================================== */

    const userMoreButton =
        document.getElementById(
            "userMoreButton"
        );


    userMoreButton.addEventListener(
        "click",
        function () {

            showMessage(
                "Account",
                "Account settings will be connected later."
            );

        }
    );



    /* =====================================================
       POPUP
    ===================================================== */

    const messageOverlay =
        document.getElementById(
            "messageOverlay"
        );


    const popupTitle =
        document.getElementById(
            "popupTitle"
        );


    const popupMessage =
        document.getElementById(
            "popupMessage"
        );


    const popupClose =
        document.getElementById(
            "popupClose"
        );


    const popupOkay =
        document.getElementById(
            "popupOkay"
        );


    function showMessage(
        title,
        message
    ) {

        popupTitle.textContent =
            title;


        popupMessage.textContent =
            message;


        messageOverlay.classList.remove(
            "hidden"
        );

    }


    function closeMessage() {

        messageOverlay.classList.add(
            "hidden"
        );

    }


    popupClose.addEventListener(
        "click",
        closeMessage
    );


    popupOkay.addEventListener(
        "click",
        closeMessage
    );


    messageOverlay.addEventListener(
        "click",
        function (event) {

            if (
                event.target ===
                messageOverlay
            ) {

                closeMessage();

            }

        }
    );

});
