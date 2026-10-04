/* =========================================================
   TAP, TASK, DONE - TRACKING
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {

    // Use the same signed-in account label as every other sidebar.
    try {
        const account = JSON.parse(
            localStorage.getItem("tapTaskDoneAccount") || "{}"
        );
        const name = account.nickname || account.name || "User";
        const email = account.email || "user@email.com";
        const initials = name.split(/\s+/).filter(Boolean).slice(0, 2)
            .map(function (part) { return part.charAt(0); }).join("").toUpperCase() || "U";

        const sidebarName = document.getElementById("sidebarName");
        const sidebarEmail = document.getElementById("sidebarEmail");
        const sidebarAvatar = document.querySelector(".sidebar-account .account-avatar");

        if (sidebarName) sidebarName.textContent = name;
        if (sidebarEmail) sidebarEmail.textContent = email;
        if (sidebarAvatar) sidebarAvatar.textContent = initials;
    } catch (error) {
        console.error("Unable to load sidebar account.", error);
    }

    // =====================================================
    // ELEMENTS
    // =====================================================

    const completeButton =
        document.getElementById("completeButton");

    const completionMessage =
        document.getElementById("completionMessage");

    const reviewNotice =
        document.getElementById("reviewNotice");

    const reviewButton =
        document.getElementById("reviewButton");

    const demoFinishTaskButton =
        document.getElementById("demoFinishTaskButton");

    const reviewForm =
        document.getElementById("reviewForm");

    const submitReviewButton =
        document.getElementById("submitReviewButton");

    const statusText =
        document.getElementById("statusText");

    const onTheWayStep =
        document.getElementById("onTheWayStep");

    const inProgressStep =
        document.getElementById("inProgressStep");

    const completedStep =
        document.getElementById("completedStep");

    const completeTaskModal = document.getElementById("completeTaskModal");
    const confirmCompletionButton = document.getElementById("confirmCompletionButton");
    const notYetButton = document.getElementById("notYetButton");
    const completeModalTaskTitle = document.getElementById("completeModalTaskTitle");
    const completeModalProvider = document.getElementById("completeModalProvider");
    const closeCompletionModal = document.getElementById("closeCompletionModal");
    const reportProviderButton = document.getElementById("reportProviderButton");

    let currentTask = null;

    try {
        currentTask = JSON.parse(
            localStorage.getItem("tapTaskDoneCurrentTask") || "null"
        );
    } catch (error) {
        currentTask = null;
    }

    if (!currentTask) {
        const trackingContent = document.querySelector(".tracking-content");

        if (trackingContent) {
            trackingContent.innerHTML = "";
            const emptyState = document.createElement("div");
            emptyState.className = "tracking-empty-state";
            const icon = document.createElement("div");
            icon.className = "tracking-empty-icon";
            icon.textContent = "○";
            const heading = document.createElement("h2");
            heading.textContent = "No active task";
            const message = document.createElement("p");
            message.textContent = "Post a new task to start tracking its progress.";
            const button = document.createElement("button");
            button.type = "button";
            button.textContent = "Post a task";
            button.addEventListener("click", function () {
                window.location.href = "/FindPost?mode=post";
            });
            emptyState.append(icon, heading, message, button);
            trackingContent.append(emptyState);
        }

        return;
    }

    const taskTitle = document.getElementById("taskTitle");
    const taskTab = document.querySelector(".task-tab");
    const taskOverviewTitle = document.getElementById("taskOverviewTitle");
    const taskOverviewMeta = document.getElementById("taskOverviewMeta");
    const trackingBudget = document.getElementById("trackingBudget");
    const trackingSchedule = document.getElementById("trackingSchedule");
    const trackingStatusSummary = document.getElementById("trackingStatusSummary");
    if (taskTitle) taskTitle.textContent = currentTask.title || "New task";
    if (taskTab) taskTab.textContent = currentTask.title || "New task";
    if (taskOverviewTitle) taskOverviewTitle.textContent = currentTask.title || "New task";
    if (taskOverviewMeta) {
        taskOverviewMeta.textContent = (currentTask.providerService || currentTask.category || "Service") + " · " + (currentTask.location || "Location to be confirmed");
    }
    if (trackingBudget) trackingBudget.textContent = currentTask.budget || "Budget to be confirmed";
    if (trackingSchedule) trackingSchedule.textContent = currentTask.schedule || "To be scheduled";

    const activeProviderName = currentTask.providerName || "Provider";
    const activeProviderService = currentTask.providerService || "Service provider";
    const providerNameElement = document.querySelector(".provider-info strong");
    const providerServiceElement = document.querySelector(".provider-info span");
    const providerAvatarElement = document.querySelector(".provider-card .provider-avatar");
    if (providerNameElement) providerNameElement.textContent = activeProviderName;
    if (providerServiceElement) providerServiceElement.textContent = activeProviderService;
    if (providerAvatarElement) providerAvatarElement.textContent = activeProviderName.slice(0, 2).toUpperCase();
    const providerRating = document.getElementById("providerRating");
    const messageProviderButton = document.getElementById("messageProviderButton");
    if (providerRating) providerRating.textContent = "★ " + (currentTask.providerReviews || "New freelancer");

    const reviewAvatar = document.querySelector(".review-avatar");
    const reviewProviderInfo = document.querySelector(".review-provider p");
    if (reviewAvatar) reviewAvatar.textContent = activeProviderName.slice(0, 2).toUpperCase();
    if (reviewProviderInfo) reviewProviderInfo.textContent = "★ " + activeProviderName + " · " + (currentTask.providerReviews || "New freelancer");


    // =====================================================
    // INITIAL STATE
    // =====================================================

    let taskStatus =
        localStorage.getItem("tapTaskDoneTrackingStatus");

    // The rating modal is shown only immediately after the client confirms completion,
    // never merely because the Tracking page was opened.
    let shouldShowRating =
        sessionStorage.getItem("tapTaskDoneShowRating") === "true";

    function showRatingForm() {
        if (!reviewNotice) return;
        reviewNotice.classList.add("is-open");
        reviewNotice.style.setProperty("display", "flex", "important");
    }

    if (!taskStatus) {
        taskStatus = "on-the-way";

        localStorage.setItem(
            "tapTaskDoneTrackingStatus",
            taskStatus
        );
    }


    // =====================================================
    // UPDATE TRACKING DISPLAY
    // =====================================================

    function updateTracking() {

        function setStatusLabel(label) {
            statusText.textContent = label;
            if (trackingStatusSummary) trackingStatusSummary.textContent = label;
        }

        // Reset states
        onTheWayStep.classList.remove("completed", "active");
        inProgressStep.classList.remove("completed", "active");
        completedStep.classList.remove("completed", "active");

        if (demoFinishTaskButton) {
            demoFinishTaskButton.style.display =
                (taskStatus === "completed" || taskStatus === "client-confirmed")
                    ? "none"
                    : "block";
            demoFinishTaskButton.textContent =
                taskStatus === "on-the-way" ? "Start work" : "Mark work finished";
        }


        // -------------------------------------------------
        // ON THE WAY
        // -------------------------------------------------

        if (taskStatus === "on-the-way") {

            onTheWayStep.classList.add("active");

            setStatusLabel("On the way");

            completeButton.disabled = true;

            completionMessage.textContent =
                "Waiting for the freelancer to finish the task.";

            reviewNotice.style.display = "none";
        }


        // -------------------------------------------------
        // IN PROGRESS
        // -------------------------------------------------

        else if (taskStatus === "in-progress") {

            onTheWayStep.classList.add("completed");

            inProgressStep.classList.add("active");

            setStatusLabel("In progress");

            completeButton.disabled = true;

            completionMessage.textContent =
                "The freelancer is currently working on your task.";

            reviewNotice.style.display = "none";
        }


        // -------------------------------------------------
        // COMPLETED
        // -------------------------------------------------

        else if (taskStatus === "completed") {

            onTheWayStep.classList.add("completed");

            inProgressStep.classList.add("completed");

            completedStep.classList.add("active");

            setStatusLabel("Completed");

            completeButton.disabled = false;

            completionMessage.textContent =
                "The freelancer has finished the task. You can now confirm completion.";

            reviewNotice.style.display = "none";
        }


        // -------------------------------------------------
        // CLIENT CONFIRMED
        // -------------------------------------------------

        else if (taskStatus === "client-confirmed") {

            onTheWayStep.classList.add("completed");

            inProgressStep.classList.add("completed");

            completedStep.classList.add("completed");

            setStatusLabel("Task completed");

            completeButton.disabled = true;

            completeButton.textContent =
                "Task completed ✓";

            completionMessage.textContent =
                "You confirmed that the task has been completed.";

            // A confirmed task must stay ready for rating until the review is submitted.
            // This also recovers a rating modal that was interrupted by a refresh.
            const hasPendingRating =
                shouldShowRating ||
                localStorage.getItem("tapTaskDonePendingRating") === "true" ||
                taskStatus === "client-confirmed";

            if (hasPendingRating) showRatingForm();
            else reviewNotice.style.display = "none";
            shouldShowRating = false;
            sessionStorage.removeItem("tapTaskDoneShowRating");
        }
    }


    // =====================================================
    // COMPLETE BUTTON
    // =====================================================

    completeButton.addEventListener("click", function () {

        // Safety check
        if (taskStatus !== "completed") {
            return;
        }


        if (completeModalTaskTitle) {
            completeModalTaskTitle.textContent = currentTask.title || "Your task";
        }
        if (completeModalProvider) {
            completeModalProvider.textContent = currentTask.providerName || "Provider";
        }

        if (completeTaskModal) {
            completeTaskModal.classList.remove("hidden");
        }

    });

    function confirmTaskCompletion() {
        if (taskStatus !== "completed") return;

        if (completeTaskModal) completeTaskModal.classList.add("hidden");


        // Save confirmed state
        taskStatus = "client-confirmed";

        // Show the rating modal immediately in this same page session.
        shouldShowRating = true;
        sessionStorage.setItem("tapTaskDoneShowRating", "true");
        localStorage.setItem("tapTaskDonePendingRating", "true");

        localStorage.setItem(
            "tapTaskDoneTrackingStatus",
            taskStatus
        );


        // Update screen
        updateTracking();

        // Keep this explicit so the form opens in the same click, not only on refresh.
        showRatingForm();
    }

    if (confirmCompletionButton) {
        confirmCompletionButton.addEventListener("click", confirmTaskCompletion);
    }

    if (notYetButton) {
        notYetButton.addEventListener("click", function () {
            if (completeTaskModal) completeTaskModal.classList.add("hidden");
            window.location.href = "/Messages";
        });
    }

    if (closeCompletionModal) {
        closeCompletionModal.addEventListener("click", function () {
            if (completeTaskModal) completeTaskModal.classList.add("hidden");
        });
    }

    if (completeTaskModal) {
        completeTaskModal.addEventListener("click", function (event) {
            if (event.target === completeTaskModal) completeTaskModal.classList.add("hidden");
        });
    }

    if (reportProviderButton) {
        reportProviderButton.addEventListener("click", function () {
            const params = new URLSearchParams({ provider: activeProviderName, task: currentTask.title || "" });
            window.location.href = "/Reports?" + params.toString();
        });
    }

    if (messageProviderButton) {
        // The normal href works without JavaScript; this adds the active task id when available.
        if (currentTask.id) messageProviderButton.href = "/Messages?task=" + encodeURIComponent(currentTask.id);
    }


    // Provider progress action: start work, then mark it finished for client confirmation.
    if (demoFinishTaskButton) {
        demoFinishTaskButton.addEventListener("click", function () {
            taskStatus = taskStatus === "on-the-way" ? "in-progress" : "completed";

            localStorage.setItem(
                "tapTaskDoneTrackingStatus",
                taskStatus
            );

            updateTracking();
        });
    }


    // =====================================================
    // RATING FORM
    // =====================================================

    if (reviewForm && submitReviewButton) {
        let selectedRating = 0;

        const starButtons =
            reviewForm.querySelectorAll(".star-picker button");

        const reviewTags =
            reviewForm.querySelectorAll(".review-tags button");

        starButtons.forEach(function (starButton) {
            starButton.addEventListener("click", function () {
                selectedRating = Number(starButton.dataset.rating);

                starButtons.forEach(function (item) {
                    const selected = Number(item.dataset.rating) <= selectedRating;
                    item.classList.toggle("selected", selected);
                    item.textContent = selected ? "★" : "☆";
                });

                submitReviewButton.disabled = false;
            });
        });

        reviewTags.forEach(function (tag) {
            tag.addEventListener("click", function () {
                tag.classList.toggle("selected");
            });
        });

        reviewForm.addEventListener("submit", function (event) {
            event.preventDefault();

            if (!selectedRating) {
                return;
            }

            const selectedTags = Array.from(reviewTags)
                .filter(function (tag) { return tag.classList.contains("selected"); })
                .map(function (tag) { return tag.textContent.trim(); });

            const review = {
                provider: currentTask.providerName || "Ofrac",
                service: currentTask.providerService || "Service provider",
                task: currentTask.title || "Task",
                rating: selectedRating,
                tags: selectedTags,
                feedback: document.getElementById("reviewFeedback").value.trim(),
                createdAt: new Date().toLocaleDateString("en-US", {
                    month: "short",
                    day: "numeric",
                    year: "numeric"
                })
            };

            let reviewHistory = [];

            try {
                reviewHistory = JSON.parse(
                    localStorage.getItem("tapTaskDoneReviews") || "[]"
                );
            } catch (error) {
                reviewHistory = [];
            }

            reviewHistory.push(review);
            localStorage.setItem("tapTaskDoneReviews", JSON.stringify(reviewHistory));
            localStorage.setItem("tapTaskDoneReview", JSON.stringify(review));

            reviewNotice.classList.remove("is-open");
            reviewNotice.style.display = "none";
            try {
                const tasks = JSON.parse(localStorage.getItem("tapTaskDoneTasks") || "[]");
                if (Array.isArray(tasks) && currentTask.id) {
                    localStorage.setItem("tapTaskDoneTasks", JSON.stringify(tasks.filter(function (task) {
                        return task.id !== currentTask.id;
                    })));
                }
            } catch (error) { }
            localStorage.removeItem("tapTaskDoneCurrentTask");
            localStorage.removeItem("tapTaskDoneTrackingStatus");
            localStorage.removeItem("tapTaskDonePendingRating");
            sessionStorage.removeItem("tapTaskDoneShowRating");
            window.location.href = "/Reviews";
        });
    }


    // =====================================================
    // TASK TABS
    // =====================================================

    const taskTabs =
        document.querySelectorAll(".task-tab");

    taskTabs.forEach(function (tab) {

        tab.addEventListener("click", function () {

            taskTabs.forEach(function (item) {
                item.classList.remove("selected");
            });

            tab.classList.add("selected");

        });

    });


    // =====================================================
    // DEMO STATUS CONTROL
    // =====================================================
    // Temporary controls for testing the prototype.
    //
    // Browser console:
    //
    // localStorage.setItem(
    //     "tapTaskDoneTrackingStatus",
    //     "in-progress"
    // );
    //
    // OR
    //
    // localStorage.setItem(
    //     "tapTaskDoneTrackingStatus",
    //     "completed"
    // );
    //
    // Then refresh the page.
    // =====================================================


    // =====================================================
    // LOAD
    // =====================================================

    updateTracking();

});
