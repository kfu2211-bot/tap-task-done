document.addEventListener("DOMContentLoaded", function () {

    console.log("Home Dashboard loaded");


    // =====================================================
    // LOAD LOGGED-IN USER
    // =====================================================

    const account =
        localStorage.getItem("tapTaskDoneAccount");

    if (!account) {

        window.location.href = "/Login";
        return;

    }


    let user;

    try {

        user = JSON.parse(account);

    }
    catch (error) {

        console.error(
            "Account data could not be loaded.",
            error
        );

        return;

    }


    // =====================================================
    // SIDEBAR USER
    // =====================================================

    const sidebarName =
        document.getElementById("sidebarName");

    const sidebarEmail =
        document.getElementById("sidebarEmail");

    const sidebarAvatar =
        document.querySelector(".user-avatar");


    // =====================================================
    // PROFILE ELEMENTS
    // =====================================================

    const profileOverlay =
        document.getElementById("profileOverlay");

    const profileButton =
        document.querySelector(".sidebar-user");

    const closeProfile =
        document.getElementById("closeProfile");

    const closeProfileBottom =
        document.getElementById("closeProfileBottom");

    const profileName =
        document.getElementById("profileName");

    const profileEmail =
        document.getElementById("profileEmail");

    const profileAvatar =
        document.getElementById("profileAvatar");

    const profileFullName =
        document.getElementById("profileFullName");

    const profileEmailInput =
        document.getElementById("profileEmailInput");

    const nicknameInput =
        document.getElementById("nicknameInput");

    const saveProfile =
        document.getElementById("saveProfile");

    const profileVerificationStatus =
        document.getElementById(
            "profileVerificationStatus"
        );

    const profileVerificationDescription =
        document.getElementById(
            "profileVerificationDescription"
        );

    const openVerification =
        document.getElementById(
            "openVerification"
        );

    const changePasswordButton =
        document.getElementById(
            "changePasswordButton"
        );


    // =====================================================
    // GET INITIALS
    // =====================================================

    function getInitials(name) {

        if (!name) {
            return "U";
        }


        const words =
            name.trim().split(/\s+/);


        if (words.length === 1) {

            return words[0]
                .substring(0, 2)
                .toUpperCase();

        }


        return (
            words[0][0] +
            words[words.length - 1][0]
        ).toUpperCase();

    }


    // =====================================================
    // CURRENT DISPLAY NAME
    // =====================================================

    function getDisplayName(accountUser) {

        return (
            accountUser.nickname ||
            accountUser.name ||
            "User"
        );

    }


    // =====================================================
    // UPDATE SIDEBAR
    // =====================================================

    function updateSidebar() {

        const displayName =
            getDisplayName(user);


        if (sidebarName) {

            sidebarName.textContent =
                displayName;

        }


        if (sidebarEmail) {

            sidebarEmail.textContent =
                user.email || "";

        }


        if (sidebarAvatar) {

            sidebarAvatar.textContent =
                getInitials(displayName);

        }

    }


    updateSidebar();


    // =====================================================
    // POST A TASK
    // =====================================================

    const postTaskButton =
        document.querySelector(".post-button");


    // Keep the Home call-to-action readable even while an older Razor build is open.
    if (postTaskButton) {
        postTaskButton.textContent = "Post a task";
        postTaskButton.setAttribute("aria-label", "Post a task");
    }


    if (postTaskButton) {

        postTaskButton.addEventListener(
            "click",
            function (event) {

                event.preventDefault();

                window.location.href =
                    "/FindPost?mode=post";

            }
        );

    }


    // =====================================================
    // FIND A FREELANCER
    // =====================================================

    const findFreelancerButton =
        document.querySelector(".find-button");


    if (findFreelancerButton) {

        findFreelancerButton.addEventListener(
            "click",
            function (event) {

                event.preventDefault();

                window.location.href =
                    "/FindPost?mode=freelancer";

            }
        );

    }


    // =====================================================
    // SEARCH BAR
    // =====================================================

    const searchInput =
        document.querySelector(
            ".search-box input"
        );


    if (searchInput) {

        searchInput.addEventListener(
            "keydown",
            function (event) {

                if (event.key === "Enter") {

                    const searchValue =
                        searchInput.value.trim();


                    if (searchValue !== "") {

                        window.location.href =
                            "/FindPost?search=" +
                            encodeURIComponent(
                                searchValue
                            );

                    }

                }

            }
        );

    }


    // =====================================================
    // BROWSE CATEGORIES
    // =====================================================

    const categoryCards =
        document.querySelectorAll(
            ".category-card"
        );


    categoryCards.forEach(function (card) {

        card.style.cursor =
            "pointer";


        card.addEventListener(
            "click",
            function () {

                const categoryNameElement =
                    card.querySelector("h4");


                if (!categoryNameElement) {
                    return;
                }


                const categoryName =
                    categoryNameElement
                        .textContent
                        .trim();


                window.location.href =
                    "/FindPost?category=" +
                    encodeURIComponent(
                        categoryName
                    );

            }
        );

    });


    // =====================================================
    // TOP SERVICE PROVIDERS
    // =====================================================

    const providerCards =
        document.querySelectorAll(
            ".provider-card"
        );


    providerCards.forEach(function (card) {

        card.style.cursor =
            "pointer";


        card.addEventListener(
            "click",
            function (event) {

                if (
                    event.target.closest(
                        ".provider-bottom button"
                    )
                ) {

                    return;

                }


                const nameElement =
                    card.querySelector(
                        ".provider-name strong"
                    );


                const serviceElement =
                    card.querySelector(
                        ".provider-name span"
                    );


                const name =
                    nameElement
                        ? nameElement
                            .textContent
                            .trim()
                        : "";


                const service =
                    serviceElement
                        ? serviceElement
                            .textContent
                            .trim()
                        : "";


                if (name !== "") {

                    window.location.href =
                        "/FindPost?provider=" +
                        encodeURIComponent(name) +
                        "&service=" +
                        encodeURIComponent(service);

                }

            }
        );

    });


    // =====================================================
    // BOOK BUTTON
    // =====================================================

    const bookButtons =
        document.querySelectorAll(
            ".provider-bottom button"
        );


    bookButtons.forEach(function (button) {

        button.addEventListener(
            "click",
            function (event) {

                event.preventDefault();
                event.stopPropagation();


                const card =
                    button.closest(
                        ".provider-card"
                    );


                if (!card) {
                    return;
                }


                const nameElement =
                    card.querySelector(
                        ".provider-name strong"
                    );


                const serviceElement =
                    card.querySelector(
                        ".provider-name span"
                    );


                const name =
                    nameElement
                        ? nameElement
                            .textContent
                            .trim()
                        : "Provider";


                const service =
                    serviceElement
                        ? serviceElement
                            .textContent
                            .trim()
                        : "Service";


                const priceElement =
                    card.querySelector(
                        ".provider-bottom span"
                    );


                const price =
                    priceElement
                        ? priceElement
                            .textContent
                            .trim()
                        : "";


                const confirmed =
                    confirm(
                        "Book " +
                        name +
                        "?\n\n" +
                        "Service: " +
                        service +
                        "\n" +
                        "Price: " +
                        price
                    );


                if (confirmed) {

                    window.location.href =
                        "/FindPost?provider=" +
                        encodeURIComponent(name) +
                        "&service=" +
                        encodeURIComponent(service);

                }

            }
        );

    });


    // =====================================================
    // TOP RATED PROVIDER + AVAILABILITY
    // Only the highest-rated provider is shown on Home.
    // =====================================================

    const providerGrid = document.querySelector(".provider-grid");

    if (providerGrid) {
        const providers = [
            { name: "Ofrac", service: "Plumber", rating: 4.8, reviews: 600, jobs: 100, price: "₱600 / task", status: "Busy", statusDetail: "Currently working on another task. Available again at 4:00 PM." },
            { name: "Marisol", service: "House Cleaner", rating: 4.9, reviews: 421, jobs: 210, price: "₱450 / task", status: "Available", statusDetail: "Free to accept a new task today." },
            { name: "Dante", service: "Electrician", rating: 4.7, reviews: 288, jobs: 132, price: "₱750 / task", status: "Busy", statusDetail: "Currently on a job. Check back later for availability." },
            { name: "Lea", service: "Errand Runner", rating: 4.9, reviews: 512, jobs: 340, price: "₱300 / task", status: "Day off", statusDetail: "Not accepting tasks today. Available again tomorrow." }
        ];

        const topProvider = providers.sort(function (first, second) {
            return second.rating - first.rating || second.reviews - first.reviews;
        })[0];

        const card = document.createElement("button");
        card.type = "button";
        card.className = "provider-card top-rated-provider";
        card.setAttribute("aria-label", "View " + topProvider.name + " availability");
        card.innerHTML = "<div class='provider-top'><div class='provider-avatar'>" + topProvider.name.slice(0, 2) + "</div><div class='provider-name'><strong>" + topProvider.name + "</strong><span>" + topProvider.service + "</span></div></div><div class='provider-stats'><span class='rating'>★ " + topProvider.rating + " <small>(" + topProvider.reviews + ")</small></span><span>" + topProvider.jobs + " jobs</span></div><div class='provider-bottom'><span>" + topProvider.price + "</span><span class='availability-preview'>Check availability</span></div>";
        providerGrid.replaceChildren(card);

        card.addEventListener("click", function () {
            showProviderAvailability(topProvider);
        });
    }

    function showProviderAvailability(provider) {
        let overlay = document.getElementById("providerAvailabilityOverlay");
        if (!overlay) {
            overlay = document.createElement("div");
            overlay.id = "providerAvailabilityOverlay";
            overlay.className = "provider-availability-overlay";
            document.body.append(overlay);
            overlay.addEventListener("click", function (event) {
                if (event.target === overlay) overlay.remove();
            });
        }

        const statusClass = provider.status.toLowerCase().replace(/\s+/g, "-");
        overlay.innerHTML = "<section class='provider-availability-panel' role='dialog' aria-modal='true'><button class='provider-availability-close' type='button' aria-label='Close'>×</button><div class='provider-availability-avatar'>" + provider.name.slice(0, 2) + "</div><p class='provider-availability-eyebrow'>Top-rated provider</p><h2>" + provider.name + "</h2><p class='provider-availability-service'>" + provider.service + " · ★ " + provider.rating + " (" + provider.reviews + " reviews)</p><div class='provider-availability-status " + statusClass + "'><span></span>" + provider.status + "</div><p class='provider-availability-detail'>" + provider.statusDetail + "</p></section>";
        overlay.querySelector(".provider-availability-close").addEventListener("click", function () { overlay.remove(); });
    }


    // =====================================================
    // VIEW ALL PROVIDERS
    // =====================================================

    const viewAllLink =
        document.querySelector(
            ".providers-heading a"
        );


    if (viewAllLink) {

        viewAllLink.addEventListener(
            "click",
            function (event) {

                event.preventDefault();

                window.location.href =
                    "/FindPost?mode=find";

            }
        );

    }


    // =====================================================
    // NOTIFICATION
    // =====================================================

    const notificationButton =
        document.querySelector(
            ".notification"
        );


    if (notificationButton) {

        notificationButton.addEventListener(
            "click",
            function () {

                alert(
                    "You have no new notifications."
                );

            }
        );

    }


    // =====================================================
    // SIDEBAR NAVIGATION
    // =====================================================

    const sidebarItems =
        document.querySelectorAll(
            ".nav-item"
        );


    sidebarItems.forEach(function (item) {

        item.addEventListener(
            "click",
            function (event) {

                const text =
                    item.textContent
                        .trim()
                        .toLowerCase();


                /*
                 * Identity Verification
                 * ay gumagamit ng sariling href.
                 */

                if (
                    text.includes(
                        "identity verification"
                    )
                ) {

                    return;

                }


                event.preventDefault();


                if (text.includes("home")) {

                    window.location.href =
                        "/Dashboard";

                }

                else if (text.includes("find")) {

                    window.location.href =
                        "/FindPost";

                }

                else if (
                    text.includes("my tasks")
                ) {

                    window.location.href =
                        "/MyTasks";

                }

                else if (
                    text.includes("messages")
                ) {

                    window.location.href =
                        "/Messages";

                }

                else if (
                    text.includes("tracking")
                ) {

                    window.location.href =
                        "/Tracking";

                }

                else if (
                    text.includes("reviews")
                ) {

                    window.location.href =
                        "/Reviews";

                }

                else if (
                    text.includes("safety") ||
                    text.includes("reports")
                ) {

                    window.location.href =
                        "/Reports";

                }

                else if (
                    text.includes("settings")
                ) {

                    window.location.href =
                        "/Settings";

                }

            }
        );

    });


    // =====================================================
    // USER MENU / LOGOUT
    // =====================================================

    const userMenu =
        document.querySelector(
            ".user-menu"
        );


    if (userMenu) {

        userMenu.style.cursor =
            "pointer";


        userMenu.addEventListener(
            "click",
            function (event) {

                event.stopPropagation();


                const logout =
                    confirm(
                        "Do you want to log out?"
                    );


                if (logout) {

                    localStorage.removeItem(
                        "tapTaskDoneAccount"
                    );


                    window.location.href =
                        "/Login";

                }

            }
        );

    }


    // =====================================================
    // LOAD PROFILE
    // =====================================================

    function loadProfile() {

        const profileAccount =
            localStorage.getItem(
                "tapTaskDoneAccount"
            );


        if (!profileAccount) {
            return;
        }


        let profileUser;


        try {

            profileUser =
                JSON.parse(profileAccount);

        }
        catch (error) {

            console.error(
                "Profile data error.",
                error
            );

            return;

        }


        const displayName =
            profileUser.nickname ||
            profileUser.name ||
            "User";


        const email =
            profileUser.email ||
            "";


        // FULL NAME

        if (profileFullName) {

            profileFullName.value =
                profileUser.name ||
                "";

        }


        // EMAIL

        if (profileEmailInput) {

            profileEmailInput.value =
                email;

        }


        // NICKNAME

        if (nicknameInput) {

            nicknameInput.value =
                profileUser.nickname ||
                "";

        }


        // PROFILE NAME

        if (profileName) {

            profileName.textContent =
                displayName;

        }


        // PROFILE EMAIL

        if (profileEmail) {

            profileEmail.textContent =
                email;

        }


        // PROFILE AVATAR

        if (profileAvatar) {

            profileAvatar.textContent =
                getInitials(displayName);

        }


        // VERIFICATION STATUS

        loadProfileVerificationStatus();

    }


    // =====================================================
    // SAVE PROFILE
    // =====================================================

    if (saveProfile) {

        saveProfile.addEventListener(
            "click",
            function (event) {

                event.preventDefault();
                event.stopPropagation();


                const fullName =
                    profileFullName
                        ? profileFullName.value.trim()
                        : "";


                const email =
                    profileEmailInput
                        ? profileEmailInput.value.trim()
                        : "";


                const nickname =
                    nicknameInput
                        ? nicknameInput.value.trim()
                        : "";


                if (fullName === "") {

                    alert(
                        "Please enter your full name."
                    );

                    return;

                }


                if (email === "") {

                    alert(
                        "Please enter your email address."
                    );

                    return;

                }


                /*
                 * Update local user object
                 */

                user.name =
                    fullName;


                user.email =
                    email;


                user.nickname =
                    nickname;


                /*
                 * Save to localStorage
                 */

                localStorage.setItem(
                    "tapTaskDoneAccount",
                    JSON.stringify(user)
                );


                /*
                 * Update sidebar
                 */

                updateSidebar();


                /*
                 * Update profile display
                 */

                const displayName =
                    nickname ||
                    fullName;


                if (profileName) {

                    profileName.textContent =
                        displayName;

                }


                if (profileEmail) {

                    profileEmail.textContent =
                        email;

                }


                if (profileAvatar) {

                    profileAvatar.textContent =
                        getInitials(displayName);

                }


                /*
                 * Success
                 */

                alert(
                    "Your profile has been updated successfully."
                );

            }
        );

    }


    // =====================================================
    // PROFILE VERIFICATION STATUS
    // =====================================================

    function loadProfileVerificationStatus() {

        const verificationStatus =
            localStorage.getItem(
                "tapTaskDoneVerificationStatus"
            );


        if (
            verificationStatus ===
            "pending"
        ) {

            if (profileVerificationStatus) {

                profileVerificationStatus.textContent =
                    "Pending Verification";

            }


            if (profileVerificationDescription) {

                profileVerificationDescription.textContent =
                    "Your documents are currently being reviewed.";

            }


            if (openVerification) {

                openVerification.textContent =
                    "View";

            }

        }

        else {

            if (profileVerificationStatus) {

                profileVerificationStatus.textContent =
                    "Not Verified";

            }


            if (profileVerificationDescription) {

                profileVerificationDescription.textContent =
                    "Your identity has not been verified yet.";

            }


            if (openVerification) {

                openVerification.textContent =
                    "Verify";

            }

        }

    }


    // =====================================================
    // OPEN VERIFICATION
    // =====================================================

    if (openVerification) {

        openVerification.addEventListener(
            "click",
            function (event) {

                event.preventDefault();
                event.stopPropagation();


                window.location.href =
                    "/IdentityVerification";

            }
        );

    }


    // =====================================================
    // CHANGE PASSWORD
    // =====================================================

    if (changePasswordButton) {

        changePasswordButton.addEventListener(
            "click",
            function () {

                alert(
                    "Change Password will be added next."
                );

            }
        );

    }


    // =====================================================
    // OPEN PROFILE
    // =====================================================

    if (profileButton && profileOverlay) {

        profileButton.addEventListener(
            "click",
            function (event) {

                /*
                 * Huwag mag-open kapag
                 * three dots ang pinindot.
                 */

                if (
                    event.target.closest(
                        ".user-menu"
                    )
                ) {

                    return;

                }


                loadProfile();


                profileOverlay.classList.add(
                    "show"
                );

            }
        );

    }


    // =====================================================
    // CLOSE PROFILE - TOP X
    // =====================================================

    if (closeProfile && profileOverlay) {

        closeProfile.addEventListener(
            "click",
            function () {

                profileOverlay.classList.remove(
                    "show"
                );

            }
        );

    }


    // =====================================================
    // CLOSE PROFILE - BOTTOM
    // =====================================================

    if (
        closeProfileBottom &&
        profileOverlay
    ) {

        closeProfileBottom.addEventListener(
            "click",
            function () {

                profileOverlay.classList.remove(
                    "show"
                );

            }
        );

    }


    // =====================================================
    // CLICK OUTSIDE PROFILE
    // =====================================================

    if (profileOverlay) {

        profileOverlay.addEventListener(
            "click",
            function (event) {

                if (
                    event.target ===
                    profileOverlay
                ) {

                    profileOverlay.classList.remove(
                        "show"
                    );

                }

            }
        );

    }

});
