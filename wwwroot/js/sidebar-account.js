/* Keeps the signed-in account display identical on every authenticated page. */
(function () {
    async function loadSidebarAccount() {
        if (!document.getElementById("productPolish")) {
            var polish = document.createElement("link");
            polish.id = "productPolish";
            polish.rel = "stylesheet";
            polish.href = "/css/product-polish.css";
            document.head.appendChild(polish);
        }
        // A small second layer keeps every authenticated page comfortable on phones,
        // even when an older page has its own desktop-first stylesheet.
        if (!document.getElementById("mobilePolish")) {
            var mobilePolish = document.createElement("link");
            mobilePolish.id = "mobilePolish";
            mobilePolish.rel = "stylesheet";
            mobilePolish.href = "/css/mobile-polish.css";
            document.head.appendChild(mobilePolish);
        }
        var account;

        try {
            account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "null") || {};
        } catch (error) {
            account = {};
        }

        // This protects the local prototype pages from being opened after sign out.
        // Production access control must still be enforced by the server.
        if (!account || !account.email) {
            window.location.replace("/Login");
            return;
        }

        // The database, not the browser cache, is authoritative for work preferences.
        try {
            var response = await fetch('/api/work-preferences', { credentials: 'same-origin' });
            if (response.ok) {
                var preferences = await response.json();
                account.role = preferences.role;
                account.service = preferences.service;
                account.availability = preferences.availability;
                localStorage.setItem('tapTaskDoneAccount', JSON.stringify(account));
            }
        } catch (error) { console.warn('Work preferences could not be refreshed.', error); }

        var name = (account.nickname || account.name || "User").trim();
        var email = (account.email || "user@email.com").trim();
        var initials = name
            .split(/\s+/)
            .filter(Boolean)
            .slice(0, 2)
            .map(function (part) { return part.charAt(0); })
            .join("")
            .toUpperCase() || "U";

        document.querySelectorAll("#sidebarName").forEach(function (element) {
            element.textContent = name;
        });

        document.querySelectorAll("#sidebarEmail").forEach(function (element) {
            element.textContent = email;
        });

        document.querySelectorAll("#sidebarAvatar, #userAvatar, .sidebar-account .account-avatar, .sidebar-user .user-avatar").forEach(function (element) {
            element.textContent = initials;
        });

        // Keep every page on the same predictable sidebar journey. Existing links are
        // moved into this sequence; only links missing from an older page are created.
        var role = String(account.role || "client").toLowerCase();
        var isFreelancer = role === "freelancer" || role === "both";
        var isClient = role === "client" || role === "both";
        var sidebarFlow = [
            { href: "/Dashboard", icon: "⌂", label: "Home" },
            { href: "/FindPost", icon: "⌕", label: "Find & Post" },
            ...(isClient ? [{ href: "/MarketTasks?view=mine", icon: "▤", label: "My Tasks" }] : []),
            ...(isFreelancer ? [{ href: "/MyJobs", icon: "▤", label: "My Jobs" }] : []),
            { href: "/Messages", icon: "□", label: "Messages" },
            { href: "/Tracking", icon: "➤", label: "Tracking" },
            { href: "/Reviews", icon: "☆", label: "Reviews" },
            { href: "/IdentityVerification", icon: "✓", label: "Identity Verification" },
            { href: "/Reports", icon: "⚑", label: "Safety & Reports" },
            { href: "/Settings", icon: "⚙", label: "Settings" }
        ];

        // Keep the main action clear for the active role.
        sidebarFlow.forEach(function (item) {
            if (item.href === "/FindPost") item.label = isFreelancer && !isClient ? "Find Jobs" : isClient && !isFreelancer ? "Post a Task" : "Find & Post";
        });

        document.querySelectorAll(".sidebar-menu, .sidebar-nav, .sidebar-navigation, .navigation, .mytasks-navigation").forEach(function (nav) {
            if (nav.dataset.sidebarFlowNormalized === "true") return;
            nav.dataset.sidebarFlowNormalized = "true";

            var sampleLink = nav.querySelector("a");
            var sampleIcon = sampleLink && sampleLink.querySelector("span");

            sidebarFlow.forEach(function (item) {
                var link = nav.querySelector('a[href="' + item.href + '"]');

                if (!link) {
                    link = document.createElement("a");
                    link.href = item.href;
                    link.className = sampleLink ? sampleLink.className : "";
                    link.classList.remove("active");

                    if (link.classList.contains("mytasks-nav-item")) {
                        // My Tasks uses an icon span followed by a text node.
                        link.innerHTML = "<span>" + item.icon + "</span>";
                        link.appendChild(document.createTextNode(item.label));
                    } else {
                        link.innerHTML = '<span class="' + (sampleIcon ? sampleIcon.className : "menu-icon") + '">' + item.icon + "</span><span>" + item.label + "</span>";
                    }
                }

                link.classList.toggle("active", window.location.pathname.toLowerCase() === item.href.split("?")[0].toLowerCase());
                // appendChild moves an existing link instead of copying it, preventing duplicates.
                nav.appendChild(link);
            });

            // Remove role-only entries left over from a page's original static sidebar.
            nav.querySelectorAll('a[href="/MyTasks"]').forEach(function (link) { link.remove(); });
            nav.querySelectorAll('a[href="/Notifications"]').forEach(function (link) { link.remove(); });
            nav.querySelectorAll('a[href="/MyJobs"]').forEach(function (link) { if (!isFreelancer) link.remove(); });
        });

        var popupStyle = document.createElement("link");
        popupStyle.rel = "stylesheet";
        popupStyle.href = "/css/notification-popup.css";
        document.head.appendChild(popupStyle);
        var header = document.querySelector("main > header, .topbar, .top-header, .mytasks-topbar, .market-header");
        var notificationSelector = ".notification, .notification-button, #notificationButton, .mytasks-notification";
        if (header && !header.querySelector(notificationSelector)) {
            var newBell = document.createElement("button");
            newBell.type = "button";
            newBell.className = "notification app-notification-trigger";
            newBell.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-2 7-2 9h16c0-2-2-2-2-9M10 20h4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>';
            header.appendChild(newBell);
        }
        var panel = document.createElement("section");
        panel.className = "notification-popover";
        panel.id = "notificationPopover";
        panel.hidden = true;
        panel.setAttribute("aria-label", "Notifications");
        panel.innerHTML = '<div class="notification-popover-heading"><div><strong>Notifications</strong><small>Your latest updates</small></div><button type="button" class="notification-popover-close" aria-label="Close notifications">×</button></div><div class="notification-popover-list" aria-live="polite"></div><a class="notification-popover-all" href="/Notifications">View all notifications</a>';
        document.body.appendChild(panel);
        var notificationList = panel.querySelector(".notification-popover-list");
        var currentTrigger = null;
        function closeNotifications() {
            panel.hidden = true;
            if (currentTrigger) currentTrigger.setAttribute("aria-expanded", "false");
        }
        async function loadNotifications() {
            notificationList.textContent = "Loading notifications…";
            try {
                var response = await fetch("/Notifications", { credentials: "same-origin", cache: "no-store" });
                if (!response.ok || response.redirected) throw new Error("Notifications unavailable");
                var doc = new DOMParser().parseFromString(await response.text(), "text/html");
                var forms = doc.querySelectorAll(".notification-list form");
                notificationList.replaceChildren();
                if (!forms.length) {
                    var emptyMessage = document.createElement("p");
                    emptyMessage.textContent = doc.querySelector(".notification-empty") ? "No notifications yet." : "Notifications are unavailable right now.";
                    notificationList.appendChild(emptyMessage);
                    return;
                }
                Array.from(forms).slice(0, 8).forEach(function (form) {
                    form.setAttribute("action", "/Notifications?handler=Open");
                    notificationList.appendChild(document.importNode(form, true));
                });
            } catch (error) { notificationList.textContent = "Could not load notifications. Please try again."; }
        }
        panel.querySelector(".notification-popover-close").addEventListener("click", function () { closeNotifications(); if (currentTrigger) currentTrigger.focus(); });
        document.addEventListener("click", function (event) { if (!panel.hidden && !panel.contains(event.target) && !(currentTrigger && currentTrigger.contains(event.target))) closeNotifications(); });
        document.addEventListener("keydown", function (event) { if (event.key === "Escape" && !panel.hidden) { closeNotifications(); if (currentTrigger) currentTrigger.focus(); } });
        document.querySelectorAll(notificationSelector).forEach(function (button) {
            if (button.dataset.notificationsReady === "true") return;
            button.dataset.notificationsReady = "true";
            button.classList.add("app-notification-trigger");
            button.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-2 7-2 9h16c0-2-2-2-2-9M10 20h4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>';
            button.title = "View notifications";
            button.setAttribute("aria-haspopup", "true");
            button.setAttribute("aria-controls", "notificationPopover");
            button.setAttribute("aria-expanded", "false");
            if (button.tagName !== "BUTTON") { button.setAttribute("role", "button"); button.tabIndex = 0; button.addEventListener("keydown", function (event) { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); button.click(); } }); }
            button.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopImmediatePropagation();
                var wasOpen = !panel.hidden && currentTrigger === button;
                closeNotifications();
                if (!wasOpen) { currentTrigger = button; panel.hidden = false; button.setAttribute("aria-expanded", "true"); loadNotifications(); }
            }, true);
        });

        async function updateNotificationCount() {
            try {
                const response = await fetch("/api/notifications/unread-count", { credentials: "same-origin", cache: "no-store" });
                if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) return;
                const data = await response.json();
                const count = Number(data.count) || 0;
                document.querySelectorAll(".notification, .notification-button, #notificationButton, .mytasks-notification").forEach(function (button) {
                    button.title = count ? count + " unread notifications" : "View notifications";
                    button.setAttribute("aria-label", button.title);
                    button.style.boxShadow = count ? "0 0 0 2px #22c99b" : "";
                });
            } catch (error) { console.warn("Notifications could not be refreshed.", error); }
        }
        updateNotificationCount();
        setInterval(updateNotificationCount, 30000);

        // Profile cards across the signed-in sidebar lead to the same settings page.
        document.querySelectorAll("#openProfile, .sidebar .sidebar-user, .mytasks-sidebar .mytasks-user, .sidebar .sidebar-account").forEach(function (profileEntry) {
            if (profileEntry.dataset.settingsRedirect === "true") return;
            profileEntry.dataset.settingsRedirect = "true";
            profileEntry.setAttribute("role", "link");
            profileEntry.setAttribute("tabindex", "0");
            profileEntry.setAttribute("aria-label", "Open profile settings");
            profileEntry.style.cursor = "pointer";
            function openSettings() {
                if (window.location.pathname.toLowerCase().replace(/\/$/, "") !== "/settings") window.location.href = "/Settings";
            }
            profileEntry.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopImmediatePropagation();
                openSettings();
            }, true);
            profileEntry.addEventListener("keydown", function (event) {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    openSettings();
                }
            }, true);
        });

        // A single accessible menu button is added to every signed-in page on small screens.
        var sidebar = document.querySelector(".sidebar, .mytasks-sidebar");
        if (sidebar && !document.getElementById("mobileMenuToggle")) {
            var menuButton = document.createElement("button");
            menuButton.id = "mobileMenuToggle";
            menuButton.type = "button";
            menuButton.setAttribute("aria-label", "Open navigation menu");
            menuButton.setAttribute("aria-expanded", "false");
            menuButton.textContent = "☰";
            var overlay = document.createElement("div");
            overlay.id = "mobileMenuOverlay";
            overlay.setAttribute("aria-hidden", "true");

            function closeMenu() {
                document.body.classList.remove("sidebar-open");
                menuButton.setAttribute("aria-expanded", "false");
                menuButton.setAttribute("aria-label", "Open navigation menu");
                menuButton.textContent = "☰";
            }
            function toggleMenu() {
                var isOpen = document.body.classList.toggle("sidebar-open");
                menuButton.setAttribute("aria-expanded", String(isOpen));
                menuButton.setAttribute("aria-label", isOpen ? "Close navigation menu" : "Open navigation menu");
                menuButton.textContent = isOpen ? "×" : "☰";
            }
            menuButton.addEventListener("click", toggleMenu);
            overlay.addEventListener("click", closeMenu);
            // Use delegation so links added while normalizing an older page's
            // sidebar also close the drawer after a tap.
            sidebar.addEventListener("click", function (event) {
                if (event.target.closest("a")) closeMenu();
            });
            document.addEventListener("keydown", function (event) { if (event.key === "Escape") closeMenu(); });
            window.addEventListener("resize", function () { if (window.innerWidth > 700) closeMenu(); });
            document.body.append(menuButton, overlay);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", loadSidebarAccount);
    } else {
        loadSidebarAccount();
    }
    function enforceLocalAccountStatus() {
        let account, users;
        try { account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "null"); users = JSON.parse(localStorage.getItem("tapTaskDoneLocalUsers") || "[]"); } catch (error) { return; }
        const record = account && users.find(function (user) { return String(user.email).toLowerCase() === String(account.email).toLowerCase(); });
        if (!record || !["Suspended", "Restricted"].includes(record.status) || document.getElementById("accountStatusBlock")) return;
        const block = document.createElement("div"); block.id = "accountStatusBlock";
        block.style.cssText = "position:fixed;inset:0;z-index:9999;display:grid;place-items:center;padding:24px;background:rgba(3,7,6,.86);color:#fff;text-align:center;font-family:system-ui";
        block.innerHTML = '<div style="max-width:420px;padding:30px;border:1px solid #7e4a45;border-radius:16px;background:#161b19"><h2 style="margin-top:0">Account ' + record.status.toLowerCase() + '</h2><p>Your account cannot create tasks, send requests, or message users while this safety review is active.</p><a href="/Reports" style="display:inline-block;margin-top:10px;padding:10px 14px;border-radius:8px;background:#2bd5a8;color:#06201a;text-decoration:none;font-weight:800">Contact support / submit a report</a></div>';
        document.body.append(block);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", enforceLocalAccountStatus); else enforceLocalAccountStatus();
}());
