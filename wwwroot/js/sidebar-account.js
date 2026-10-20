/* Keeps the signed-in account display identical on every authenticated page. */
(function () {
    function loadSidebarAccount() {
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
            ...(isClient ? [{ href: "/MyTasks", icon: "▤", label: "My Tasks" }] : []),
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

                link.classList.toggle("active", window.location.pathname.toLowerCase() === item.href.toLowerCase());
                // appendChild moves an existing link instead of copying it, preventing duplicates.
                nav.appendChild(link);
            });

            // Remove role-only entries left over from a page's original static sidebar.
            nav.querySelectorAll('a[href="/MyTasks"]').forEach(function (link) { if (!isClient) link.remove(); });
            nav.querySelectorAll('a[href="/MyJobs"]').forEach(function (link) { if (!isFreelancer) link.remove(); });
        });

        document.querySelectorAll(".notification, .notification-button, #notificationButton, .mytasks-notification").forEach(function (button) {
            if (button.dataset.notificationsReady === "true") return;
            button.dataset.notificationsReady = "true";
            button.title = "View notifications";
            button.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopImmediatePropagation();
                document.querySelectorAll(".local-notification-popover").forEach(function (panel) { panel.remove(); });
                let notices = [];
                try { notices = JSON.parse(localStorage.getItem("tapTaskDoneNotifications") || "[]"); } catch (error) { notices = []; }
                const panel = document.createElement("div");
                panel.className = "local-notification-popover";
                panel.style.cssText = "position:fixed;right:22px;top:58px;z-index:5000;width:min(330px,calc(100vw - 32px));padding:14px;border:1px solid #373737;border-radius:10px;background:#171717;color:#eee;box-shadow:0 20px 50px rgba(0,0,0,.55);font:12px Arial,Helvetica,sans-serif;";
                const title = document.createElement("strong"); title.textContent = "Notifications"; title.style.display = "block"; title.style.marginBottom = "10px"; panel.append(title);
                if (!notices.length) { const empty = document.createElement("p"); empty.textContent = "You have no notifications yet."; empty.style.cssText = "margin:0;color:#999;"; panel.append(empty); }
                else { notices.slice(-5).reverse().forEach(function (notice) {
                    const item = document.createElement(notice.href ? "button" : "p");
                    item.textContent = notice.message;
                    item.style.cssText = "display:block;width:100%;margin:0;padding:9px 0;border:0;border-top:1px solid #2b2b2b;background:transparent;color:#d8d8d8;line-height:1.4;text-align:left;font:inherit;" + (notice.href ? "cursor:pointer;" : "");
                    if (notice.href) item.addEventListener("click", function () { window.location.href = notice.href; });
                    panel.append(item);
                }); }
                document.body.append(panel);
                setTimeout(function () { document.addEventListener("click", function close(event) { if (!panel.contains(event.target) && event.target !== button) { panel.remove(); document.removeEventListener("click", close); } }); }, 0);
            }, true);
        });

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
