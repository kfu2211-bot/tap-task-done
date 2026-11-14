const taskHelp = document.querySelector(".task-help");
const taskHelpLaunch = document.querySelector(".task-help-launch");
const taskHelpAnswer = document.querySelector("#task-help-answer");
const taskHelpInput = document.querySelector("#task-help-input");
const taskHelpReport = document.querySelector("#task-help-report");
taskHelpReport.href = taskHelp.dataset.reportUrl;
const taskHelpAnswers = {
    settings: "Pag naka-log in, pindutin ang pangalan o profile icon mo sa kanang itaas. Sa account dropdown, piliin ang Settings.",
    report: "Sa sidebar, buksan ang Safety & Reports. Piliin ang reason, ilagay kung sino ang nire-report at ano ang nangyari, at mag-attach ng evidence kung meron. Pindutin ang Submit report. Makikita ang status at admin updates sa Your report history.",
    review: "Kapag confirmed na complete ang task, buksan ang Rate your task sa Tracking. Pumili ng stars, maglagay ng feedback kung gusto mo, at pindutin ang Submit review.",
    messages: "Buksan ang Messages at piliin ang account na gusto mong kausapin. Piliin ang task sa chat header kung may higit sa isang task.",
    password: "Sa Log in, piliin ang Forgot password? at sundin ang instructions na ipapadala sa email.",
    tracking: "Buksan ang Tracking at piliin ang task para makita ang progress at ang susunod na available na action.",
    role: "Sa Settings, hanapin ang Work role. Piliin ang Client, Freelancer, o Client & Freelancer at i-save ang settings.",
    notifications: "Buksan ang bell o ang Notifications para makita ang updates. Gamitin ang Unread para makita ang mga bago.",
    post: "Piliin ang Client o Client & Freelancer sa Settings. Kapag approved na ang identity verification mo, pumunta sa Find & Post para mag-post ng task.",
    jobs: "Piliin ang Freelancer o Client & Freelancer sa Settings. Kapag approved na ang identity verification mo, pumunta sa Find & Post para maghanap ng trabaho.",
    verify: "Gamitin ang code na pinadala sa email para sa email verification. Para mag-post o maghanap ng trabaho, isumite din ang hinihinging documents sa Identity Verification. Doon mo makikita ang status ng review.",
    areas: "Available sa Solana Casa Real, Cabalantian, Parulog, Cabetican, at Magliman sa Bacolor, Pampanga."
};


const english = {
    settings: "After signing in, open the account menu by selecting your name or profile icon in the upper-right corner. Choose Settings.",
    report: "Open Safety & Reports from the sidebar. Choose a reason, identify who you are reporting, describe what happened, and attach evidence if available. Select Submit report. Check Your report history for updates.",
    review: "Once the task is confirmed complete, open Rate your task in Tracking. Choose a star rating, add optional feedback, and select Submit review.",
    messages: "Open Messages and select the person you want to contact. If you share several tasks, use the selector in the chat header.",
    password: "Select Forgot password? on the Log in page and follow the instructions sent to your email.",
    tracking: "Open Tracking and select a task to see its progress and available actions.",
    role: "In Settings, open Work role, choose Client, Freelancer, or Client & Freelancer, and save your changes.",
    notifications: "Open Notifications using the bell. Select Unread to see notifications you have not read.",
    post: "Choose Client or Client & Freelancer in Settings. Once your identity verification is approved, go to Find & Post to post a task.",
    jobs: "Choose Freelancer or Client & Freelancer in Settings. Once your identity verification is approved, go to Find & Post to look for jobs.",
    verify: "Use the code sent by email to verify your email address. To post tasks or find jobs, submit the required documents under Identity Verification. You can also check their review status there.",
    areas: "The service covers Solana Casa Real, Cabalantian, Parulog, Cabetican, and Magliman in Bacolor, Pampanga.",
    fallback: "I don't have an answer for that yet. Try naming the feature, such as Safety & Reports, Messages, Tracking, or Settings."
};

const filipino = {
    settings: "Pagkapasok sa iyong account, pindutin ang pangalan o larawan mo sa kanang itaas. Piliin ang Settings sa lalabas na menu.",
    report: "Buksan ang Safety & Reports sa menu. Piliin ang dahilan, tukuyin kung sino ang inirereklamo, at ilarawan ang nangyari. Maglakip ng ebidensiya kung mayroon at pindutin ang Submit report. Makikita ang mga bagong abiso sa Your report history.",
    review: "Kapag nakumpirmang tapos na ang gawain, buksan ang Rate your task sa Tracking. Pumili ng bilang ng bituin, sumulat ng puna kung nais, at pindutin ang Submit review.",
    messages: "Buksan ang Messages at piliin ang taong nais mong kausapin. Kung marami kayong gawain, piliin ang nais pag-usapan sa itaas ng usapan.",
    password: "Piliin ang Forgot password? sa pahina ng pagpasok at sundin ang mga tagubiling ipinadala sa iyong email.",
    tracking: "Buksan ang Tracking at piliin ang gawain para makita ang kalagayan nito at ang susunod na hakbang.",
    role: "Sa Settings, hanapin ang Work role. Piliin ang Client, Freelancer, o Client & Freelancer at itala ang pagbabago.",
    notifications: "Pindutin ang kampana upang buksan ang Notifications. Piliin ang Unread para makita ang mga abisong hindi mo pa nababasa.",
    post: "Piliin ang Client o Client & Freelancer sa Settings. Kapag napatunayan na ang iyong pagkakakilanlan, gamitin ang Find & Post upang maglathala ng gawain.",
    jobs: "Piliin ang Freelancer o Client & Freelancer sa Settings. Kapag napatunayan na ang iyong pagkakakilanlan, gamitin ang Find & Post upang maghanap ng trabaho.",
    verify: "Gamitin ang kodigong ipinadala sa email upang patunayan ang iyong email address. Upang maglathala ng gawain o maghanap ng trabaho, isumite ang mga hinihinging dokumento sa Identity Verification. Doon mo rin makikita ang kalagayan ng pagsusuri.",
    areas: "Sakop ng serbisyo ang Solana Casa Real, Cabalantian, Parulog, Cabetican, at Magliman sa Bacolor, Pampanga.",
    fallback: "Wala pa akong sagot diyan. Subukang banggitin ang pahinang tinutukoy mo, gaya ng Safety & Reports, Messages, Tracking, o Settings."
};

function questionLanguage(question) {
    if (question.split(/\s+/).length === 1 && /^(settings|tracking|messages|verification|report|help|offers|booking)\W*$/.test(question)) return lastQuestionLanguage;
    const hasFilipino = /\b(paano|pano|ba|dito|saan|ano|bakit|kailan|ako|ko|ninyo|maghanap|trabaho|gawain|sumbong|reklamo|pagkakakilanlan|mensahe|paraan|puwede|pwede)\b/.test(question) ||
        /\b(?:mag|i|na|pa)-[a-z]+/.test(question);
    if (!hasFilipino) return "en";
    const hasEnglish = /\b(report|post|task|job|jobs|verify|verification|password|review|feedback|message|chat|tracking|role|settings|notification|notifications|available|scam|account|payment|payments|pay)\b/.test(question);
    return hasEnglish ? "taglish" : "fil";
}

let lastQuestionLanguage = "taglish";
const specificAnswers = {
    payment: ["Tap, Task, Done does not process payments. The client and freelancer can discuss and agree on how and when to pay through Messages or over a phone call.", "Sa labas ng Tap, Task, Done ginagawa ang pagbabayad. Ang kliyente at freelancer ay maaaring mag-usap sa Messages o sa tawag sa telepono upang magkasundo kung paano at kailan magbabayad.", "Sa labas ng Tap, Task, Done ang payment. Ang client at freelancer ang mag-uusap sa Messages o sa phone call para magkasundo sa payment method at kung kailan magbabayad."],
    reportStatus: ["Open Safety & Reports → Your report history to check the status and admin updates.", "Buksan ang Safety & Reports → Your report history upang makita ang kalagayan ng ulat at mga tugon ng admin.", "Sa Safety & Reports → Your report history, makikita mo ang status at admin updates."],
    pendingPost: ["Wait for Identity Verification approval before posting. Check your status on the Identity Verification page.", "Hintaying maaprubahan ang pagpapatunay ng iyong pagkakakilanlan bago maglathala ng gawain. Tingnan ang kalagayan sa Identity Verification.", "Hintaying ma-approve ang identity verification bago mag-post. Sa Identity Verification mo makikita ang status."],
    photo: ["Open your account menu in the upper-right corner, then go to Settings → Profile photo to upload a new picture.", "Buksan ang menu ng account sa kanang itaas at pumunta sa Settings → Profile photo upang mag-upload ng bagong larawan.", "Sa dropdown ng pangalan mo, buksan ang Settings → Profile photo para mag-upload ng bagong picture."],
    edit: ["Open the task in My Tasks. Use Edit task while it is awaiting confirmation, then save your changes.", "Buksan ang gawain sa My Tasks. Piliin ang Edit task habang hinihintay pa ang kumpirmasyon at i-save ang pagbabago.", "Sa My Tasks, buksan ang task at gamitin ang Edit task habang pending confirmation pa. I-save ang changes."],
    cancel: ["Open the task in My Tasks, select Cancel task, enter a reason, and confirm the cancellation.", "Buksan ang gawain sa My Tasks, piliin ang Cancel task, ilagay ang dahilan, at kumpirmahin ang pagkansela.", "Sa My Tasks, buksan ang task, piliin ang Cancel task, ilagay ang reason, at i-confirm."],
    booking: ["In Find & Post, open a freelancer’s profile to send a private booking request. Freelancers can review incoming requests under My Jobs → Booking requests.", "Sa Find & Post, buksan ang profile ng freelancer upang magpadala ng pribadong kahilingan. Makikita ng freelancer ang mga kahilingan sa My Jobs → Booking requests.", "Sa Find & Post, buksan ang profile ng freelancer para mag-send ng booking request. Sa My Jobs → Booking requests ito makikita ng freelancer."],
    offers: ["Open your task in My Tasks to review offers and choose a freelancer.", "Buksan ang gawain sa My Tasks upang suriin ang mga alok at pumili ng freelancer.", "Buksan ang task sa My Tasks para makita ang offers at pumili ng freelancer."],
    location: ["Select a pin in the Bacolor service area. Check the suggested address or enter a landmark, then select Confirm this pin.", "Pumili ng lokasyon sa sakop na bahagi ng Bacolor. Suriin ang address o maglagay ng palatandaan bago pindutin ang Confirm this pin.", "Mag-pin sa Bacolor service area. I-check ang address o mag-type ng landmark, tapos pindutin ang Confirm this pin."],
    login: ["Check your email and password. If you need to reset your password, select Forgot password? on the Log in page.", "Suriin ang iyong email at password. Kung kailangan mong palitan ang password, piliin ang Forgot password? sa pahina ng pagpasok.", "I-check ang email at password mo. Kung kailangan ng reset, pindutin ang Forgot password? sa Log in page."]
};

const followUp = {
    en: ["Sorry, I can only answer questions about using Tap, Task, Done.", "Which step are you having trouble with?"],
    fil: ["Paumanhin, masasagot ko lamang ang mga tanong tungkol sa paggamit ng Tap, Task, Done.", "Saang hakbang ka nahihirapan?"],
    taglish: ["Sorry, para lang sa mga tanong tungkol sa paggamit ng Tap, Task, Done ang Task Help.", "Saang step ka nagka-problema?"]
};

function showTaskHelpAnswer(topic, language = "taglish", unclear = false) {
    const answers = language === "en" ? english : language === "fil" ? filipino : taskHelpAnswers;
    taskHelpAnswer.lang = language === "en" ? "en" : "fil";
    taskHelpReport.hidden = topic !== "report" && topic !== "reportStatus";
    taskHelpAnswer.hidden = false;
    if (topic) {
        taskHelpAnswer.textContent = specificAnswers[topic]?.[["en", "fil", "taglish"].indexOf(language)] ?? answers[topic];
        return;
    }
    const [redirect, clarify] = followUp[language];
    taskHelpAnswer.textContent = unclear ? clarify : redirect;
}

taskHelpLaunch.hidden = false;
taskHelpLaunch.addEventListener("click", () => taskHelp.showModal());
taskHelp.addEventListener("close", () => taskHelpLaunch.focus());
taskHelp.querySelectorAll("[data-topic]").forEach(button => {
    button.addEventListener("click", () => showTaskHelpAnswer(button.dataset.topic));
});
document.querySelector("#task-help-question").addEventListener("submit", event => {
    event.preventDefault();
    const question = taskHelpInput.value.trim().toLowerCase();
    if (!question) return;
    const topic = Object.entries({
        payment: /\b(?:payments?|pay|bayad|magbayad|pagbabayad|gcash)\b/,
        reportStatus: /(?=.*\b(?:status|pending|update|history|kalagayan)\b)(?=.*(?:report|reklamo|ulat))/,
        pendingPost: /(?=.*(?:post|maglathala))(?=.*(?:verif|pagkakakilanlan))(?=.*(?:pending|while|habang|pa ba|before|bago))/,
        photo: /profile (?:photo|picture)|larawan ng profile/,
        edit: /\b(?:edit|update|baguhin)\b.*\b(?:task|post|gawain)\b/,
        cancel: /\b(?:cancel|kanselahin)\b.*\b(?:task|post|gawain)\b/,
        booking: /\bbooking\b|mag-book/,
        offers: /\boffers?\b|\balok\b/,
        location: /\b(?:location|landmark|address|pin|lokasyon)\b/,
        report: /report|reklamo|sumbong|scam|harass|complaint/,
        password: /password|forgot|reset|hudyat|kontrasenyas/,
        review: /review|rating|feedback|puna|\brate\b/,
        messages: /message|chat|kausap|kausapin|mensahe/,
        notifications: /notification|unread|alert|abiso/,
        tracking: /tracking|progress|kumplet|complet/,
        role: /\brole\b|work preferences/,
        settings: /\bsettings?\b/,
        verify: /verif|pagkakakilanlan|patunayan|pending|\botp\b|\bid\b/,
        areas: /barangay|available|bacolor|coverage|service area/,
        post: /post|hire|client|magpagawa|maglathala|kumuha ng tulong/,
        jobs: /job|trabaho|freelanc|work|apply|hanapbuhay/
    }).find(([, pattern]) => pattern.test(question))?.[0];
    const unclear = /ayaw gumana|hindi gumagana|di gumagana|may error|may problema|not working|doesn.t work|something went wrong|help me|can.t|hindi makapag/.test(question);
    lastQuestionLanguage = questionLanguage(question);
    const postingIssue = topic === "post" && unclear;
    showTaskHelpAnswer(postingIssue ? undefined : topic, lastQuestionLanguage, unclear);
    taskHelpInput.value = "";
});
