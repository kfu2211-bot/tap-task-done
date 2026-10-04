
document.addEventListener("DOMContentLoaded", function () {
    try {
        const account = JSON.parse(
            localStorage.getItem("tapTaskDoneAccount") || "{}"
        );
        const name = account.nickname || account.name || "User";
        const email = account.email || "user@email.com";
        const initials = name.split(/\s+/).filter(Boolean).slice(0, 2)
            .map(function (part) { return part.charAt(0); }).join("").toUpperCase() || "U";

        const sidebar = document.querySelector(".sidebar-user");
        const sidebarName = sidebar && sidebar.querySelector(".user-info strong");
        const sidebarEmail = sidebar && sidebar.querySelector(".user-info span");
        const sidebarAvatar = sidebar && sidebar.querySelector(".user-avatar");

        if (sidebarName) sidebarName.textContent = name;
        if (sidebarEmail) sidebarEmail.textContent = email;
        if (sidebarAvatar) sidebarAvatar.textContent = initials;
    } catch (error) {
        console.error("Unable to load sidebar account.", error);
    }

    // Show reviews submitted from Tracking at the top of the review history.
    try {
        const savedReviews = JSON.parse(
            localStorage.getItem("tapTaskDoneReviews") || "[]"
        );
        const filters = document.querySelector(".review-filters");
        const reviewList = filters && filters.parentElement;

        if (!reviewList || !Array.isArray(savedReviews)) {
            return;
        }

        const firstExistingReview = filters.nextSibling;

        savedReviews.slice().reverse().forEach(function (review) {
            const card = document.createElement("article");
            card.className = "review-card submitted-review";

            const top = document.createElement("div");
            top.className = "review-top";

            const person = document.createElement("div");
            person.className = "review-person";

            const avatar = document.createElement("div");
            avatar.className = "review-avatar";
            avatar.textContent = (review.provider || "Ofrac").slice(0, 2);

            const personDetails = document.createElement("div");
            const provider = document.createElement("strong");
            provider.textContent = review.provider || "Ofrac";
            const service = document.createElement("span");
            service.textContent = review.service || "Freelancer";
            personDetails.append(provider, service);
            person.append(avatar, personDetails);

            const date = document.createElement("time");
            date.textContent = review.createdAt || "Just now";
            top.append(person, date);

            const rating = document.createElement("div");
            rating.className = "review-rating";
            const stars = document.createElement("span");
            stars.className = "stars";
            stars.textContent = "★".repeat(Number(review.rating) || 0) + "☆".repeat(5 - (Number(review.rating) || 0));
            const separator = document.createElement("span");
            separator.className = "review-separator";
            separator.textContent = "·";
            const task = document.createElement("span");
            task.textContent = review.task || "Completed task";
            rating.append(stars, separator, task);

            const feedback = document.createElement("p");
            feedback.textContent = review.feedback || (review.tags || []).join(", ") || "Review submitted.";
            card.append(top, rating, feedback);
            reviewList.insertBefore(card, firstExistingReview);
        });
    } catch (error) {
        console.error("Unable to load review history.", error);
    }
});

// Render only real reviews submitted from Tracking, then make the filters live.
document.addEventListener("DOMContentLoaded", function () {
    let reviews = [];

    try {
        const saved = JSON.parse(localStorage.getItem("tapTaskDoneReviews") || "[]");
        reviews = Array.isArray(saved) ? saved : [];
    } catch (error) {
        reviews = [];
    }

    const filters = document.querySelector(".review-filters");
    const reviewList = filters && filters.parentElement;
    if (!reviewList) return;

    reviewList.querySelectorAll(".review-card").forEach(function (card) {
        card.remove();
    });

    const emptyMessage = document.createElement("p");
    emptyMessage.className = "empty-review-history";
    reviewList.append(emptyMessage);

    function createCard(review) {
        const rating = Math.max(1, Math.min(5, Number(review.rating) || 0));
        const card = document.createElement("article");
        card.className = "review-card submitted-review";
        card.dataset.rating = String(rating);
        const top = document.createElement("div");
        top.className = "review-top";
        const person = document.createElement("div");
        person.className = "review-person";
        const avatar = document.createElement("div");
        avatar.className = "review-avatar";
        avatar.textContent = (review.provider || "Ofrac").slice(0, 2);
        const details = document.createElement("div");
        const provider = document.createElement("strong");
        provider.textContent = review.provider || "Ofrac";
        const service = document.createElement("span");
        service.textContent = review.service || "Freelancer";
        details.append(provider, service);
        person.append(avatar, details);
        const date = document.createElement("time");
        date.textContent = review.createdAt || "Just now";
        top.append(person, date);

        const ratingLine = document.createElement("div");
        ratingLine.className = "review-rating";
        const stars = document.createElement("span");
        stars.className = "stars";
        stars.textContent = "★".repeat(rating) + "☆".repeat(5 - rating);
        const separator = document.createElement("span");
        separator.className = "review-separator";
        separator.textContent = "·";
        const task = document.createElement("span");
        task.textContent = review.task || "Completed task";
        ratingLine.append(stars, separator, task);

        const feedback = document.createElement("p");
        feedback.textContent = review.feedback || (review.tags || []).join(", ") || "Review submitted.";
        card.append(top, ratingLine, feedback);
        return card;
    }

    reviews.slice().reverse().forEach(function (review) {
        reviewList.insertBefore(createCard(review), emptyMessage);
    });

    const total = reviews.length;
    const average = total
        ? reviews.reduce(function (sum, review) { return sum + (Number(review.rating) || 0); }, 0) / total
        : 0;
    const ratingNumber = document.querySelector(".rating-number");
    const ratingStars = document.querySelector(".rating-stars");
    const reviewsCount = document.querySelector(".reviews-count");
    if (ratingNumber) ratingNumber.textContent = total ? average.toFixed(1) : "—";
    if (ratingStars) ratingStars.textContent = total ? "★ ★ ★ ★ ★" : "☆ ☆ ☆ ☆ ☆";
    if (reviewsCount) reviewsCount.textContent = total + (total === 1 ? " review given" : " reviews given");

    document.querySelectorAll(".rating-row").forEach(function (row, index) {
        const star = 5 - index;
        const count = reviews.filter(function (review) { return Number(review.rating) === star; }).length;
        const fill = row.querySelector(".rating-fill");
        const totalLabel = row.querySelector(".rating-total");
        if (fill) fill.style.width = total ? ((count / total) * 100) + "%" : "0%";
        if (totalLabel) totalLabel.textContent = String(count);
    });

    function applyFilter(selectedRating) {
        let visible = 0;
        reviewList.querySelectorAll(".submitted-review").forEach(function (card) {
            const show = selectedRating === null || Number(card.dataset.rating) === selectedRating;
            card.style.display = show ? "block" : "none";
            if (show) visible += 1;
        });
        emptyMessage.style.display = visible ? "none" : "block";
        emptyMessage.textContent = selectedRating === null
            ? "No submitted reviews yet. Complete a task to leave your first review."
            : "No " + selectedRating + "-star reviews yet.";
    }

    document.querySelectorAll(".filter-button").forEach(function (button) {
        button.addEventListener("click", function () {
            document.querySelectorAll(".filter-button").forEach(function (item) {
                item.classList.remove("active");
            });
            button.classList.add("active");
            const match = button.textContent.match(/\d/);
            applyFilter(match ? Number(match[0]) : null);
        });
    });

    applyFilter(null);
});
