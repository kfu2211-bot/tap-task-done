/* Keeps the signed-in account display identical on every authenticated page. */
(function () {
    function loadSidebarAccount() {
        var account;

        try {
            account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "null") || {};
        } catch (error) {
            account = {};
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
        var sidebarFlow = [
            { href: "/Dashboard", icon: "⌂", label: "Home" },
            { href: "/FindPost", icon: "⌕", label: "Find & Post" },
            { href: "/MyTasks", icon: "▤", label: "My Tasks" },
            { href: "/Messages", icon: "□", label: "Messages" },
            { href: "/Tracking", icon: "➤", label: "Tracking" },
            { href: "/Reviews", icon: "☆", label: "Reviews" },
            { href: "/IdentityVerification", icon: "✓", label: "Identity Verification" },
            { href: "/Reports", icon: "⚑", label: "Safety & Reports" },
            { href: "/Settings", icon: "⚙", label: "Settings" }
        ];

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
                else { notices.slice(-5).reverse().forEach(function (notice) { const item = document.createElement("p"); item.textContent = notice.message; item.style.cssText = "margin:0;padding:9px 0;border-top:1px solid #2b2b2b;color:#d8d8d8;line-height:1.4;"; panel.append(item); }); }
                document.body.append(panel);
                setTimeout(function () { document.addEventListener("click", function close(event) { if (!panel.contains(event.target) && event.target !== button) { panel.remove(); document.removeEventListener("click", close); } }); }, 0);
            }, true);
        });

        // Settings replaces the older dashboard-only profile popup.
        document.querySelectorAll("#openProfile, .dashboard .sidebar-user").forEach(function (profileEntry) {
            if (profileEntry.dataset.settingsRedirect === "true") return;
            profileEntry.dataset.settingsRedirect = "true";
            profileEntry.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopImmediatePropagation();
                window.location.href = "/Settings";
            }, true);
            profileEntry.addEventListener("keydown", function (event) {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    window.location.href = "/Settings";
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
            sidebar.querySelectorAll("a").forEach(function (link) { link.addEventListener("click", closeMenu); });
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
}());
