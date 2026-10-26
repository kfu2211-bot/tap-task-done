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

    const serviceSearch = document.getElementById("serviceSearch");
    const locationInput = document.getElementById("locationInput");
    const budgetInput = document.getElementById("budgetInput");
    const availabilityFilter = document.getElementById("availabilityFilter");
    const searchButton = document.getElementById("searchButton");
    const noResults = document.getElementById("noResults");
    const freelancerList = document.getElementById("freelancerList");
    const freelancerCards = document.querySelectorAll(".freelancer-card");
    const searchParams = new URLSearchParams(window.location.search);
    const categoryFromHome = searchParams.get("category") || "";
    const otherCategory = categoryFromHome.trim().toLowerCase() === "others";
    const categoryTerms = { repairs: ["repair", "plumb", "electric"], cleaning: ["clean"], errands: ["errand", "delivery"], moving: ["moving", "mover", "hauling"], gardening: ["garden", "landscap"], painting: ["paint"], tutoring: ["tutor", "teacher", "teaching"], "pet care": ["pet care", "pet sit", "dog walk"] };
    function matchesService(value, search) {
        const text = String(value || "").toLowerCase();
        const term = String(search || "").toLowerCase().trim();
        if (!term) return true;
        if (text.includes(term) || (term.length > 3 && term.endsWith("s") && text.includes(term.slice(0, -1)))) return true;
        return !!categoryTerms[term] && categoryTerms[term].some(function (word) { return text.includes(word); });
    }
    freelancerCards.forEach(function (card) {
        const label = document.createElement("small");
        label.className = "match-details";
        label.textContent = "Service area: " + (card.dataset.location || "Not set") +
            " · " + (card.dataset.availability || "Availability not set") +
            " · Database profile";
        card.querySelector(".freelancer-info").append(label);
        const reasons = document.createElement("small");
        reasons.className = "search-match-reasons";
        card.querySelector(".freelancer-info").append(reasons);
    });

    function performSearch(keepProfile = false) {
        const openProfile = document.getElementById("providerDetailPanel");
        if (openProfile && keepProfile !== true) openProfile.classList.add("hidden");
        const query = serviceSearch.value.trim().toLowerCase();
        const location = locationInput.value.trim().toLowerCase();
        const budgetText = budgetInput.value.trim().replace(/[₱,\s]/g, "");
        const maximum = budgetText === "" ? null : Number(budgetText);
        const availability = availabilityFilter.value.toLowerCase();
        if (maximum !== null && (!Number.isFinite(maximum) || maximum <= 0)) {
            noResults.textContent = "Enter a valid maximum budget, for example 1500.";
            noResults.classList.add("show");
            return;
        }
        let count = 0;
        freelancerCards.forEach(function (card) {
            const name = card.querySelector(".freelancer-info strong").textContent.toLowerCase();
            const service = String(card.dataset.service || "").toLowerCase();
            const area = String(card.dataset.location || "").toLowerCase();
            const rate = Number(card.dataset.rate);
            const status = String(card.dataset.availability || "").toLowerCase();
            const categorySearch = categoryFromHome && query === categoryFromHome.trim().toLowerCase();
            const matchesOther = !otherCategory || !Object.values(categoryTerms).some(words => words.some(word => service.includes(word)));
            const matches = matchesOther && (!query || matchesService(service, query) || (!categorySearch && name.includes(query))) &&
                (!location || area.includes(location)) &&
                (maximum === null || (rate > 0 && rate <= maximum)) &&
                (!availability || (availability === "busy" ? status.startsWith("busy") : status === availability));
            card.style.display = matches ? "flex" : "none";
            const reasons = [];
            if (query && (matchesService(service, query) || name.includes(query))) reasons.push("service or name");
            if (location && area.includes(location)) reasons.push("location");
            if (maximum !== null && rate > 0 && rate <= maximum) reasons.push("budget");
            if (availability && (availability === "busy" ? status.startsWith("busy") : status === availability)) reasons.push("availability");
            const match = card.querySelector(".search-match-reasons");
            if (match) match.textContent = matches && reasons.length ? "Matched: " + reasons.join(", ") : "";
            if (matches) count++;
        });
        noResults.textContent = noResults.dataset.unavailable === "true" ? "The freelancer directory is temporarily unavailable. Please try again later." : "No freelancers match these filters. Try a different service, location, budget, or availability.";
        noResults.classList.toggle("show", count === 0);
    }
    searchButton.addEventListener("click", performSearch);
    [serviceSearch, locationInput, budgetInput].forEach(function (input) {
        input.addEventListener("keydown", function (event) { if (event.key === "Enter") { event.preventDefault(); performSearch(); } });
    });
    availabilityFilter.addEventListener("change", performSearch);
    const fromHome = searchParams.get("search") || (otherCategory ? "" : categoryFromHome);
    if (fromHome) serviceSearch.value = fromHome;
    if (categoryFromHome) availabilityFilter.value = "Available";
    if (otherCategory) {
        const notice = document.getElementById("otherCategoryNotice");
        if (notice) notice.hidden = false;
        serviceSearch.placeholder = "e.g. balloon artist";
    }
    performSearch();



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

    const providerDetailPanel = document.getElementById("providerDetailPanel");
    const sendBookingRequestButton = document.getElementById("sendBookingRequestButton");
    let selectedProvider = null;

    freelancerList.addEventListener("click", function (event) {
                    const button = event.target.closest(".arrow-button");
                    if (!button || !freelancerList.contains(button)) return;
                    const name = String(button.dataset.name || "").replace(/[\u00ad\u200b]/g, "").trim();
                    const card = button.closest(".freelancer-card");
                    const service = card.querySelector(".freelancer-info span").textContent.trim();
                    const rating = card.querySelector(".freelancer-rating strong").textContent.trim();
                    const price = card.querySelector(".freelancer-price").textContent.trim();
                    selectedProvider = { id: Number(card.dataset.id), name: name, email: card.dataset.email || "", service: service, rating: rating, price: price, availability: card.dataset.availability || "", location: card.dataset.location || "" };
                    document.getElementById("selectedProviderName").textContent = name;
                    document.getElementById("selectedProviderMeta").textContent = service + " · " + (selectedProvider.location || "Location not set") + " · Database profile";
                    document.getElementById("selectedProviderRating").textContent = rating;
                    document.getElementById("selectedProviderPrice").textContent = card.dataset.rate ? price + " per task" : price;
                    document.getElementById("selectedProviderAvailability").textContent = selectedProvider.availability || "Availability not set";
                    const bookingFreelancerId = document.getElementById("bookingFreelancerId");
                    if (bookingFreelancerId) bookingFreelancerId.value = card.dataset.id || "";
                    const bookingBudget = document.getElementById("bookingBudget");
                    if (bookingBudget) bookingBudget.value = card.dataset.rate || "";
                    if (sendBookingRequestButton) sendBookingRequestButton.disabled = selectedProvider.availability !== "Available" || !card.dataset.id;
                    if (sendBookingRequestButton) sendBookingRequestButton.textContent = "Send booking request to " + name;
                    const bookingPrivacyNotice = document.getElementById("bookingPrivacyNotice");
                    if (bookingPrivacyNotice) bookingPrivacyNotice.textContent = "Private booking: only " + name + " will receive this request. It will not appear in Find Jobs.";
                    card.insertAdjacentElement("afterend", providerDetailPanel);
                    providerDetailPanel.classList.remove("hidden");
                    providerDetailPanel.scrollIntoView({ behavior: "smooth", block: "nearest" });
    });

    async function refreshProviderAvailability() {
        if (document.hidden) return;
        try {
            const response = await fetch('/api/providers/availability', { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) return;
            const statuses = new Map((await response.json()).map(p => [Number(p.id), p.availability]));
            freelancerCards.forEach(card => {
                const next = statuses.get(Number(card.dataset.id));
                if (!next) return;
                card.dataset.availability = next;
                const badge = card.querySelector('.provider-availability'); if (badge) badge.textContent = next;
                const label = card.querySelector('.match-details');
                if (label) label.textContent = 'Service area: ' + card.dataset.location + ' · ' + next + ' · Database profile';
            });
            if (selectedProvider && statuses.has(selectedProvider.id)) {
                selectedProvider.availability = statuses.get(selectedProvider.id);
                document.getElementById('selectedProviderAvailability').textContent = selectedProvider.availability;
                if (sendBookingRequestButton) sendBookingRequestButton.disabled = selectedProvider.availability !== 'Available';
            }
            performSearch(true);
        } catch { /* Keep last-known labels when offline; booking is validated on the server. */ }
    }
    setInterval(refreshProviderAvailability, 15000);
    window.addEventListener('focus', refreshProviderAvailability);
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

    const preferredDateInput = document.getElementById("preferredDateInput");
    const preferredTimeInput = document.getElementById("preferredTimeInput");

    function localDateValue(date) {
        const offsetDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
        return offsetDate.toISOString().slice(0, 10);
    }

    function resetSchedulePicker() {
        if (preferredDateInput) preferredDateInput.value = "";
        if (preferredTimeInput) preferredTimeInput.value = "";
    }

    function selectedSchedule() {
        const dateValue = preferredDateInput ? preferredDateInput.value : "";
        const timeValue = preferredTimeInput ? preferredTimeInput.value : "";
        if (!dateValue && !timeValue) return { value: "To be scheduled" };
        if (!dateValue) return { error: "Please choose a preferred date, or clear the time." };

        const date = new Date(dateValue + "T" + (timeValue || "00:00"));
        const dateText = date.toLocaleDateString("en-PH", { month: "short", day: "numeric", year: "numeric" });
        if (!timeValue) return { value: dateText + " · Time to be confirmed" };
        return { value: dateText + ", " + date.toLocaleTimeString("en-PH", { hour: "numeric", minute: "2-digit" }) };
    }

    const databasePostTaskForm = document.getElementById("databasePostTaskForm");
    if (databasePostTaskForm && startPostTaskButton && cancelPostTaskButton) {
        startPostTaskButton.addEventListener("click", function () {
            postTaskStart.classList.add("hidden");
            databasePostTaskForm.classList.remove("hidden");
        });
        cancelPostTaskButton.addEventListener("click", function () {
            showFindFreelancer();
        });
        databasePostTaskForm.addEventListener("submit", function (event) {
            const schedule = selectedSchedule();
            if (schedule.error) {
                event.preventDefault();
                alert(schedule.error);
                return;
            }
            document.getElementById("databaseSchedule").value = schedule.value;
        });
    }

    document.getElementById("scheduleTodayButton")?.addEventListener("click", function () {
        preferredDateInput.value = localDateValue(new Date());
    });
    document.getElementById("scheduleTomorrowButton")?.addEventListener("click", function () {
        const tomorrow = new Date(); tomorrow.setDate(tomorrow.getDate() + 1);
        preferredDateInput.value = localDateValue(tomorrow);
    });
    document.getElementById("scheduleChooseDateButton")?.addEventListener("click", function () {
        preferredDateInput?.showPicker?.();
        preferredDateInput?.focus();
    });
    document.getElementById("scheduleClearButton")?.addEventListener("click", resetSchedulePicker);

    function currentFreelancer() {
        try {
            const account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}");
            return {
                name: account.nickname || account.name || "Freelancer",
                email: account.email || "",
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
            const key = task.id || taskSignature(task);
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

    function addNotification(message, href) {
        let notifications = [];
        try { notifications = JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch (error) { notifications = []; }
        notifications.push({ id: "notice-" + Date.now(), message: message, href: href || "/MyTasks", read: false, createdAt: new Date().toLocaleTimeString([], { hour: "numeric", minute: "2-digit" }) });
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
            let clientAccount = {};
            try { clientAccount = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}"); } catch (error) { }

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
                clientName: clientAccount.nickname || clientAccount.name || "Local client",
                clientEmail: clientAccount.email || "",
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

    // Home's provider cards keep the selected freelancer through to the real
    // server-backed booking form, rather than opening a generic search page.
    const requestedProviderId = searchParams.get("providerId");
    if (requestedProviderId && /^[1-9]\d*$/.test(requestedProviderId)) {
        showFindFreelancer();
        const providerCard = Array.from(freelancerList.querySelectorAll(".freelancer-card[data-id]"))
            .find(function (card) { return card.dataset.id === requestedProviderId; });
        if (providerCard) {
            providerCard.querySelector(".arrow-button").click();
        } else {
            showMessage("Provider unavailable", "This provider is no longer listed. Please choose another freelancer.");
        }
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
