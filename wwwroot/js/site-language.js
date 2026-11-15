(() => {
    const labels = {
        "How it works": "Paano ito gumagana", "Community reviews": "Mga review ng komunidad",
        "Service areas": "Mga lugar na sakop", "Log in": "Mag-log in", "Create account": "Gumawa ng account",
        "Home": "Pangunahin", "Dashboard": "Dashboard", "Find & Post": "Maghanap at mag-post",
        "Post a Task": "Mag-post ng task", "Post a task": "Mag-post ng task", "Find Jobs": "Maghanap ng trabaho",
        "Find a freelancer": "Maghanap ng freelancer", "My Tasks": "Mga task ko", "My Jobs": "Mga trabaho ko",
        "Messages": "Mga mensahe", "Tracking": "Subaybayan ang task", "Reviews": "Mga review",
        "Identity Verification": "Pagpapatunay ng pagkakakilanlan", "Safety & Reports": "Kaligtasan at mga ulat",
        "Settings": "Mga setting", "Notifications": "Mga abiso", "Profile": "Profile",
        "Verification requests": "Mga kahilingan sa pagpapatunay", "Safety Reports": "Mga ulat sa kaligtasan",
        "Deletion requests": "Mga kahilingan sa pagbura", "Users": "Mga user", "Overview": "Pangkalahatang-tingin",
        "Activity Log": "Kasaysayan ng aktibidad",
        "Local help,": "Lokal na tulong,", "made simple.": "madaling hanapin.",
        "Find local freelancers in Bacolor, Pampanga.": "Maghanap ng lokal na freelancer sa Bacolor, Pampanga.",
        "What kind of help do you need?": "Anong uri ng tulong ang kailangan mo?",
        "Cleaning": "Paglilinis", "Repairs": "Pagkukumpuni", "Gardening": "Paghahardin", "Errands": "Mga utos",
        "Choose your role": "Piliin ang iyong role", "Verify your identity": "Patunayan ang iyong pagkakakilanlan",
        "Post a task or find jobs": "Mag-post ng task o maghanap ng trabaho", "Connect": "Makipag-usap",
        "Get it done": "Tapusin ang task", "Reviews from the community": "Mga review mula sa komunidad",
        "In Settings, choose Client, Freelancer, or Client & Freelancer.": "Sa Settings, piliin ang Client, Freelancer, o Client & Freelancer.",
        "Get your identity verification approved before posting a task or finding jobs.": "Magpa-verify muna bago mag-post ng task o maghanap ng trabaho.",
        "Clients post tasks. Freelancers look for jobs. Choose both roles to do both.": "Nagpo-post ang client at naghahanap ng trabaho ang freelancer. Piliin ang parehong role para magawa pareho.",
        "Discuss the task details with the client or freelancer.": "Pag-usapan ang detalye ng task kasama ang client o freelancer.",
        "Complete the task together.": "Tapusin nang magkasama ang task."
    };
    Object.assign(labels, {
        "WELCOME BACK": "MALIGAYANG PAGBABALIK", "Log in to your account": "Mag-log in sa iyong account",
        "Pick up where you left off and manage your tasks.": "Ipagpatuloy at pamahalaan ang iyong mga task.",
        "Password": "Password", "Show": "Ipakita", "Hide": "Itago", "Logging in…": "Nagla-log in…",
        "Don't have an account?": "Wala ka pang account?", "Create an account": "Gumawa ng account",
        "Forgot password?": "Nakalimutan ang password?", "Find and manage": "Hanapin at pamahalaan",
        "your one-time tasks.": "ang iyong mga task.", "Simple. Fast. Trusted.": "Simple. Mabilis. Maaasahan.",
        "Browse trusted service providers or post a task and let the right pro come to you.": "Maghanap ng maaasahang freelancer o mag-post ng task para makahanap ng tutulong.",
        "Browse categories": "Tingnan ang mga kategorya",
        "Explore services and find available freelancers. Scroll over the cards or swipe to see more.": "Tingnan ang mga serbisyo at available na freelancer. Mag-swipe para makita ang iba pa.",
        "Moving": "Paglilipat", "Painting": "Pagpipintura", "Tutoring": "Pagtuturo",
        "Pet Care": "Pag-aalaga ng alagang hayop", "Others": "Iba pa",
        "Homes, offices & deep cleans": "Paglilinis ng bahay at opisina",
        "Plumbing, electrical & fixes": "Tubero, elektrisyan at pagkukumpuni",
        "Deliveries, groceries & queues": "Paghahatid, pamimili at pagpila",
        "Packing, hauling & assembly": "Pag-iimpake, paghakot at pagbuo",
        "Garden care, plants & landscaping": "Pag-aalaga ng halaman at hardin",
        "Walls, rooms & touch-ups": "Pagpipintura ng pader at kuwarto",
        "Lessons, homework & study support": "Tulong sa aralin at takdang-aralin",
        "Pet sitting, walks & care": "Pagbabantay at pagpapasyal ng alagang hayop",
        "Unique skills & other services": "Iba pang kasanayan at serbisyo",
        "Available tasks": "Mga bukas na task", "Recently posted work open for offers.": "Mga bagong task na tumatanggap ng alok.",
        "View all": "Tingnan lahat", "Top service providers": "Mga nangungunang freelancer",
        "Highest rated from completed tasks": "Pinakamataas ang rating sa mga natapos na task",
        "Request work": "Humiling ng serbisyo", "Available": "Available", "Unavailable": "Hindi available",
        "View profile": "Tingnan ang profile", "Dark mode": "Madilim na tema", "Log out": "Mag-log out",
        "MY ACCOUNT": "ACCOUNT KO", "Identity Verification": "Pag-verify ng ID"
    });
    const tracked = new WeakMap();
    let language = "en";
    try { language = localStorage.getItem("tts-language") === "fil" ? "fil" : "en"; } catch {}
    const landing = document.querySelector(".landing-header nav");
    const menuPicker = document.createElement("details");
    menuPicker.className = "site-language-menu";
    const summary = document.createElement("summary");
    summary.setAttribute("aria-label", "Language / Wika");
    menuPicker.append(summary);
    const options = document.createElement("div");
    options.className = "site-language-options";
    for (const [code, name] of [["en", "English"], ["fil", "Filipino"]]) {
        const option = document.createElement("button");
        option.type = "button";
        option.textContent = name;
        option.dataset.language = code;
        option.addEventListener("click", () => {
            setLanguage(code);
            menuPicker.open = false;
            summary.focus();
        });
        options.append(option);
    }
    menuPicker.append(options);
    landing?.querySelector('a[href="#service-areas"]')?.after(menuPicker);

    const switchButton = document.createElement("button");
    switchButton.type = "button";
    switchButton.className = "header-theme-toggle site-language-switch";
    switchButton.setAttribute("role", "switch");
    switchButton.setAttribute("aria-label", "Filipino");
    switchButton.innerHTML = '<span>Filipino</span><span class="theme-switch-track" aria-hidden="true"></span>';
    switchButton.addEventListener("click", () => setLanguage(language === "fil" ? "en" : "fil"));

    const selectors = ".landing-header nav a, .hero-copy h1, .hero-copy p, .hero-actions a, .landing-section h2, .landing-steps strong, .landing-steps span, .service-grid a, .sidebar nav a, .mytasks-sidebar nav a, .admin-sidebar nav a, .topbar h1, .market-header h1, .admin-topbar h1, #headerAccountMenu a, #headerAccountMenu button, #headerAccountButton span, .hero-text h1, .hero-text h2, .hero-text p, .hero-label, .hero-buttons button, .hero-buttons a, .category-card h4, .category-card p, .category-card strong, .section-heading h3, .section-heading p, .dashboard-task-heading, .provider-bottom a, .provider-availability, #loginForm label, #loginForm button, .login-subtitle, .create-account-text, .login-box h1, .login-eyebrow, .section h3, .section h3 + p";
    function translate() {
        const darkMode = document.querySelector("#headerAccountMenu .header-theme-toggle:not(.site-language-switch)");
        if (darkMode && darkMode.nextElementSibling !== switchButton) darkMode.after(switchButton);
        const currentLabel = language === "fil" ? "Filipino" : "English";
        if (summary.textContent !== currentLabel) summary.textContent = currentLabel;
        switchButton.setAttribute("aria-checked", String(language === "fil"));
        options.querySelectorAll("button").forEach(option => option.setAttribute("aria-pressed", String(option.dataset.language === language)));
        document.documentElement.lang = language;
        document.querySelectorAll(selectors).forEach(element => {
            const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
            for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                const previous = tracked.get(node);
                const original = previous?.rendered === node.data ? previous.original : node.data;
                const key = original.trim().replace(/\s+/g, " ");
                const translation = Object.hasOwn(labels, key) ? labels[key] : key.replace(/^(\d+) available$/, "$1 na available");
                if (translation === key) continue;
                if (translation) {

                    const next = language === "fil" ? original.replace(original.trim(), translation) : original;
                    tracked.set(node, { original, rendered: next });
                    if (node.data !== next) node.data = next;
                }
            }
        });
        const help = document.querySelector("[data-help-language]");
        if (help && help.value !== language) {
            help.value = language;
            help.dispatchEvent(new Event("change"));
        }
    }
    function setLanguage(value) {
        language = value;
        try { localStorage.setItem("tts-language", language); } catch {}
        translate();
    }
    translate();
    new MutationObserver(translate).observe(document.body, { childList: true, subtree: true, characterData: true });
})();
