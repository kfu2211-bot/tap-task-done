const taskHelp = document.querySelector(".task-help");
const taskHelpLaunch = document.querySelector(".task-help-launch");
const taskHelpAnswer = document.querySelector("#task-help-answer");
const taskHelpInput = document.querySelector("#task-help-input");
const taskHelpReport = document.querySelector("#task-help-report");
taskHelpReport.href = taskHelp.dataset.reportUrl;
const taskHelpAnswers = {
    settings: "Pag naka-log in, pindutin ang pangalan o profile icon mo sa kanang itaas. Sa account dropdown, piliin ang Settings.",
    report: "Sa sidebar, buksan ang Safety & Reports. Piliin ang reason, ilagay kung sino ang nire-report at ano ang nangyari, at mag-attach ng evidence kung meron. Pindutin ang Submit report. Makikita ang status at admin updates sa Your report history.",
    review: "Sa Tracking, piliin ang completed task at buksan ang Leave a review → Review your freelancer. Piliin ang rating at pindutin ang Submit review.",
    messages: "Buksan ang Messages at piliin ang account na gusto mong kausapin. Piliin ang task sa chat header kung may higit sa isang task.",
    password: "Sa Log in, piliin ang Forgot password? at sundin ang instructions na ipapadala sa email.",
    tracking: "Buksan ang Tracking at piliin ang task para makita ang progress at ang susunod na available na action.",
    role: "Buksan ang account menu sa kanang itaas, piliin ang Settings, at hanapin ang Work role. Piliin ang Client, Freelancer, o Client & Freelancer, tapos pindutin ang Save changes. Kung may agreement na lalabas, basahin ito at piliin ang Agree and activate.",
    notifications: "Buksan ang bell o ang Notifications para makita ang updates. Gamitin ang Unread para makita ang mga bago.",
    post: "Sa Settings, piliin ang Client o Client & Freelancer at hintaying maaprubahan ang Identity Verification. Buksan ang Post a Task, punan ang form, at pindutin ang Review task. Sa My Tasks, suriin ito at piliin ang Confirm and open task para makita ito ng freelancers.",
    jobs: "Piliin ang Freelancer o Client & Freelancer sa Settings. Kapag approved na ang identity verification mo, buksan ang Find Jobs tab sa Find & Post para makita ang mga bukas na task.",
    verify: "Gamitin ang code na pinadala sa email para sa email verification. Para mag-post o maghanap ng trabaho, isumite din ang hinihinging documents sa Identity Verification. Doon mo makikita ang status ng review.",
    areas: "Available sa Solana Casa Real, Cabalantian, Parulog, Cabetican, at Magliman sa Bacolor, Pampanga."
};


const english = {
    settings: "After signing in, open the account menu by selecting your name or profile icon in the upper-right corner. Choose Settings.",
    report: "Open Safety & Reports from the sidebar. Choose a reason, identify who you are reporting, describe what happened, and attach evidence if available. Select Submit report. Check Your report history for updates.",
    review: "In Tracking, select the completed task and open Leave a review → Review your freelancer. Choose a rating and select Submit review.",
    messages: "Open Messages and select the person you want to contact. If you share several tasks, use the selector in the chat header.",
    password: "Select Forgot password? on the Log in page and follow the instructions sent to your email.",
    tracking: "Open Tracking and select a task to see its progress and available actions.",
    role: "Open the account menu in the upper-right corner, select Settings, and find Work role. Choose Client, Freelancer, or Client & Freelancer, then select Save changes. If an agreement appears, review it and select Agree and activate.",
    notifications: "Open Notifications using the bell. Select Unread to see notifications you have not read.",
    post: "In Settings, choose Client or Client & Freelancer and wait for Identity Verification approval. Open Post a Task, fill out the form, and select Review task. In My Tasks, check the details and select Confirm and open task to make it visible to freelancers.",
    jobs: "Choose Freelancer or Client & Freelancer in Settings. Once your identity verification is approved, open the Find Jobs tab in Find & Post to browse open tasks.",
    verify: "Use the code sent by email to verify your email address. To post tasks or find jobs, submit the required documents under Identity Verification. You can also check their review status there.",
    areas: "The service covers Solana Casa Real, Cabalantian, Parulog, Cabetican, and Magliman in Bacolor, Pampanga.",
    fallback: "I don't have an answer for that yet. Try naming the feature, such as Safety & Reports, Messages, Tracking, or Settings."
};

const filipino = {
    settings: "Pagkapasok sa iyong account, pindutin ang pangalan o larawan mo sa kanang itaas. Piliin ang Settings sa lalabas na menu.",
    report: "Buksan ang Safety & Reports sa menu. Piliin ang dahilan, tukuyin kung sino ang inirereklamo, at ilarawan ang nangyari. Maglakip ng ebidensiya kung mayroon at pindutin ang Submit report. Makikita ang mga bagong abiso sa Your report history.",
    review: "Sa Tracking, piliin ang natapos na gawain at buksan ang Leave a review → Review your freelancer. Pumili ng rating at pindutin ang Submit review.",
    messages: "Buksan ang Messages at piliin ang taong nais mong kausapin. Kung marami kayong gawain, piliin ang nais pag-usapan sa itaas ng usapan.",
    password: "Piliin ang Forgot password? sa pahina ng pagpasok at sundin ang mga tagubiling ipinadala sa iyong email.",
    tracking: "Buksan ang Tracking at piliin ang gawain para makita ang kalagayan nito at ang susunod na hakbang.",
    role: "Buksan ang account menu sa kanang itaas, piliin ang Settings, at hanapin ang Work role. Piliin ang Client, Freelancer, o Client & Freelancer, tapos pindutin ang Save changes. Kung may agreement na lalabas, basahin ito at piliin ang Agree and activate.",
    notifications: "Pindutin ang kampana upang buksan ang Notifications. Piliin ang Unread para makita ang mga abisong hindi mo pa nababasa.",
    post: "Sa Settings, piliin ang Client o Client & Freelancer at hintaying maaprubahan ang Identity Verification. Buksan ang Post a Task, punan ang form, at pindutin ang Review task. Sa My Tasks, suriin ito at piliin ang Confirm and open task para makita ito ng freelancers.",
    jobs: "Piliin ang Freelancer o Client & Freelancer sa Settings. Kapag napatunayan na ang iyong pagkakakilanlan, buksan ang Find Jobs tab sa Find & Post para makita ang mga bukas na gawain.",
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

let lastQuestionLanguage = "en";
const specificAnswers = {
    communityReview: ["In Tracking, open a completed task and select Write a community review. Choose a rating, add feedback, and select Publish community review to show it on the landing page.", "Sa Tracking, buksan ang natapos na gawain at piliin ang Write a community review. Pumili ng rating, isulat ang feedback, at pindutin ang Publish community review para lumabas ito sa landing page.", "Sa Tracking, buksan ang completed task at piliin ang Write a community review. Ilagay ang rating at feedback, tapos pindutin ang Publish community review para lumabas sa landing page."],
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

const appGuides = {
    emailCorrection: ["On Verify your email, expand Change email. Enter the corrected address and your registration password. Select Send code to corrected email and verify the latest six-digit code.", "Sa Verify your email, buksan ang Change email. Ilagay ang tamang address at registration password. Pindutin ang Send code to corrected email at i-verify ang pinakabagong code.", "/Login"],
    deviceSecurity: ["If sign-in asks for a device code, check your email and enter the latest code. To manage sessions after signing in, open Settings → Security & devices. When sign-in is rate limited, wait up to five minutes before trying again.", "Kung humingi ng device code, tingnan ang email at ilagay ang pinakabagong code. Para i-manage ang sessions, pumunta sa Settings → Security & devices. Kung may login cooldown, maghintay nang hanggang limang minuto bago ulitin.", "/Security"],
    workPreferences: ["Open Settings → Work role. For Freelancer or Client & Freelancer, select your services and areas, enter rates if needed, and set availability. Select Save changes and accept any required agreements.", "Sa Settings → Work role, piliin ang Freelancer o Client & Freelancer. Piliin ang services at areas, ilagay ang rates kung kailangan, at i-set ang availability. Pindutin ang Save changes at tanggapin ang required agreements.", "/Settings#work-preferences"],
    identityStatus: ["Open Identity Verification to check your status. If it says Pending, wait for review. If resubmission is requested, read the admin note and submit the requested ID and a new selfie.", "Buksan ang Identity Verification para makita ang status. Kung Pending, hintayin ang review. Kung kailangang mag-resubmit, basahin ang admin note at isumite ang hinihinging ID at bagong selfie.", "/IdentityVerification"],
    serviceInquiry: ["Open the freelancer’s profile in Find a Freelancer, expand Ask about a service, and send your question. Your private chat opens in Messages. Use Request booking there when you’re ready.", "Sa Find a Freelancer, buksan ang profile at piliin ang Ask about a service. I-send ang tanong para magbukas ang private chat sa Messages. Kapag ready na, piliin ang Request booking doon.", "/FindPost"],
    bookingDecision: ["In My Jobs → Booking requests, open the request. Discuss any missing details, then accept or decline it. If accepted, open Tracking and agree on the task terms.", "Sa My Jobs → Booking requests, buksan ang request. Linawin ang detalye, tapos tanggapin o i-decline. Kung accepted, buksan ang Tracking at kumpirmahin ang task terms.", "/MyJobs?view=requests"],
    archivedChat: ["In Messages, open Archived to read conversations from completed or cancelled tasks. Archived task chats are read-only.", "Sa Messages, buksan ang Archived para balikan ang usapan ng completed o cancelled tasks. Read-only ang archived task chats.", "/Messages?archived=true"],
    phoneSharing: ["Open the task conversation in Messages. When available, select Share my number to show it to the other participant. Use Stop sharing to remove their access.", "Sa task conversation sa Messages, piliin ang Share my number kung available. Makikita ito ng kausap mo. Gamitin ang Stop sharing para alisin ang access.", "/Messages"],
    pinIssue: ["Select the service area and move the pin to the exact work location. Confirm this pin becomes available when the map lookup matches the selected area. If lookup is inconclusive, try another point at the same work location. Changing the address text does not verify the pin.", "Piliin ang service area at ilagay ang pin sa eksaktong work location. Available lang ang Confirm this pin kapag tugma ang map lookup sa area. Kung kulang ang resulta, subukan ang ibang point sa mismong lugar. Hindi sapat ang pag-edit ng address text.", "/FindPost?mode=post"],
    arrivalInstructions: ["Add optional Arrival instructions under Work address when posting or requesting a booking. The client and assigned freelancer can read the note under Task location in Tracking.", "Ilagay ang optional na Arrival instructions sa ilalim ng Work address kapag nagpo-post o nagre-request ng booking. Makikita ito ng client at assigned freelancer sa Task location sa Tracking.", "/Tracking"],
    taskTerms: ["Open the assigned task in Tracking and review scope, price, schedule and destination. Both participants must confirm the same version before travel. If you propose changes, review and confirm the new version.", "Sa Tracking, buksan ang assigned task at suriin ang scope, presyo, schedule, at destination. Parehong i-confirm ang iisang version bago mag-travel. Kapag may pagbabago, i-review at kumpirmahin ang bagong version.", "/Tracking"],
    arrival: ["Keep Tracking open during travel and enable location sharing. I’ve arrived appears when a GPS fix is within 100 m, at most 45 seconds old, and accurate to 50 m or better. If GPS is weak, select Retry location or Ask client to confirm arrival.", "Panatilihing bukas ang Tracking habang nagta-travel at i-enable ang location sharing. Lalabas ang I’ve arrived kapag nasa loob ng 100 m ang GPS fix, hanggang 45 segundo ang edad, at 50 m o mas accurate. Kung mahina ang GPS, piliin ang Retry location o Ask client to confirm arrival.", "/Tracking"],
    completion: ["The freelancer adds a completion note and selects Mark work finished. The client reviews it in Tracking and selects Confirm completion. After confirmation, eligible reviews become available.", "Maglalagay ang freelancer ng completion note at pipindot ng Mark work finished. Susuriin ito ng client sa Tracking at pipindutin ang Confirm completion. Pagkatapos, magiging available ang eligible reviews.", "/Tracking"],
    warning: ["Check the official notice in Notifications or email. A second offense has a recovery target of 3 completed tasks; third and fourth offenses require 6 and 10 after suspension. Only distinct tasks confirmed Completed by the client during your recovery period count. Task Help cannot view or remove your case.", "Tingnan ang official notice sa Notifications o email. Ang second offense ay may target na 3 completed tasks; 6 at 10 naman pagkatapos ng suspension para sa third at fourth offense. Magkakaibang task na kinumpirma ng client bilang Completed sa recovery period lang ang bibilangin. Walang access ang Task Help sa case mo.", "/Notifications"],
    accountDeletion: ["In Settings → Delete account, request a code and confirm it by email. Final deletion requires a 72-hour wait, admin approval and no unresolved commitments. You can cancel the request before final deletion.", "Sa Settings → Delete account, humingi ng email code at i-confirm ito. Kailangan ang 72-hour wait, admin approval, at walang unresolved commitments bago ang final deletion. Puwedeng i-cancel ang request bago ito ma-finalize.", "/AccountDeletion"]
};

const sensitiveHelpInput = /\b\d{6}\b|\b(?:password|passcode|otp|recovery code|verification code|api key|id number|token)\s*(?:[:=]|(?:is|ko ay|ay)\s+)\s*\S|\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b|\b(?:09\d{9}|639\d{9})\b/i;
const helpPages = new Set(["landing", "login", "verifyemail", "verifylogin", "dashboard", "findpost", "markettasks", "myjobs", "messages", "tracking", "reviews", "identityverification", "reports", "settings", "security", "notifications", "accountdeletion"]);
const pageName = location.pathname.split("/").filter(Boolean).pop()?.toLowerCase() || "landing";
const helpPage = helpPages.has(pageName) ? pageName : null;

const guidePatterns = [
    ["warning", /\b(?:warning|suspension|offense|recovery target|my (?:acc|account).*reported|(?:acc|account) ko.*(?:report|suspend))\b/i],
    ["emailCorrection", /(?:wrong|correct|change|mali|palit).*email|email.*(?:typo|mali)/i],
    ["deviceSecurity", /new.device|device code|security.*devices|(?:login|sign.in).*429|(?:sign out|logout).*device/i],
    ["identityStatus", /resubmit|pending.*(?:identity|verification)|verification.*status/i],
    ["workPreferences", /service areas? sa settings|weekly availability|work preferences|agree and activate/i],
    ["serviceInquiry", /ask about a service|(?:message|chat|tanong).*bago.*(?:book|request)|(?:chat|message).*before.*book/i],
    ["bookingDecision", /booking requests|(?:accept|decline|tanggap).*booking/i],
    ["archivedChat", /archiv.*(?:chat|message)|(?:chat|message).*archiv|read.only|(?:di|hindi|can.t).*send.*(?:completed|cancelled)/i],
    ["phoneSharing", /share my number|stop sharing.*number|(?:share|show).*phone number/i],
    ["arrivalInstructions", /arrival instructions|arrival note/i],
    ["arrival", /i.ve arrived|retry location|confirm arrival|start travel|\bgps\b/i],
    ["pinIssue", /confirm this pin|(?:pin|location).*(?:outside|verify|labas|ayaw)/i],
    ["taskTerms", /task terms|work agreement|confirm work details/i],
    ["completion", /confirm completion|mark work finished|completion note/i],
    ["accountDeletion", /delete.*account|account.*deletion|(?:bura|tanggal).*account/i]
];

const followUp = {
    en: ["Sorry, I can only help with questions about Tap, Task, Done. I’m the app’s virtual assistant. Ask me about posting tasks, finding work, or using your account.", "What are you trying to do in Tap, Task, Done? Tell me which page or step you’re on."],
    fil: ["Pasensya na, virtual assistant ako ng Tap, Task, Done. Masasagot ko lang ang mga tanong tungkol sa paggamit ng app, gaya ng pag-post ng task o paghahanap ng trabaho.", "Ano ang sinusubukan mong gawin sa Tap, Task, Done? Sabihin mo kung saang page o hakbang ka na."],
    taglish: ["Pasensya na, virtual assistant ako ng Tap, Task, Done. Para sa mga tanong tungkol sa app lang ako, gaya ng pag-post ng task o paghahanap ng work.", "Anong page o step sa Tap, Task, Done ang kailangan mo ng help?"]
};

const helpHistory = taskHelp.querySelector(".task-help-history");
const historyKey = "tts-task-help-history";
const historyOwner = taskHelp.dataset.historyOwner;
let savedMessages = [];
const helpPrompt = taskHelp.querySelector("[data-help-prompt]");
const helpTopics = taskHelp.querySelector(".task-help-topics");
let conversationStarted = false;

function updateHelpView() {
    const chatView = conversationStarted || taskHelpInput.value.trim().length > 0;
    helpPrompt.hidden = chatView;
    helpTopics.hidden = chatView;
    helpHistory.hidden = !chatView;
}

taskHelpInput.addEventListener("input", updateHelpView);

function addHelpMessage(message, speaker, language, save = true) {
    const bubble = document.createElement("p");
    bubble.className = "task-help-bubble " + speaker;
    bubble.lang = language === "en" ? "en" : "fil";
    const label = document.createElement("strong");
    label.textContent = speaker === "assistant" ? "Task Help" : language === "en" ? "You" : "Ikaw";
    bubble.append(label, document.createTextNode(message));
    conversationStarted = true;
    updateHelpView();
    helpHistory.append(bubble);
    helpHistory.scrollTop = helpHistory.scrollHeight;
    if (save) {
        savedMessages.push({ message, speaker, language });
        savedMessages = savedMessages.slice(-40);
        try {
            sessionStorage.setItem(historyKey, JSON.stringify({ owner: historyOwner, messages: savedMessages }));
        } catch { /* Chat remains available on this page. */ }
    }
}

try {
    const history = JSON.parse(sessionStorage.getItem(historyKey));
    if (history?.owner === historyOwner && Array.isArray(history.messages)) {
        savedMessages = history.messages.slice(-40);
        savedMessages.forEach(({ message, speaker, language }) => addHelpMessage(message, speaker, language, false));
    } else {
        sessionStorage.removeItem(historyKey);
    }
} catch { /* Start with an empty chat when storage is unavailable. */ }

let previousHelpTopic;

function showTaskHelpAnswer(topic, language = "taglish", unclear = false) {
    const answers = language === "en" ? english : language === "fil" ? filipino : taskHelpAnswers;
    taskHelpAnswer.lang = language === "en" ? "en" : "fil";
    taskHelpReport.hidden = topic !== "report" && topic !== "reportStatus";
    taskHelpAnswer.hidden = true;
    taskHelpReport.href = taskHelp.dataset.reportUrl;
    if (topic) previousHelpTopic = topic;
    if (Object.hasOwn(appGuides, topic)) {
        const [en, fil, path] = appGuides[topic];
        const publicGuide = topic === "emailCorrection" || topic === "deviceSecurity";
        const signedIn = taskHelp.dataset.signedIn === "true";
        const prefix = !signedIn && !publicGuide ? (language === "en" ? "Log in first. " : "Mag-log in muna. ") : "";
        addHelpMessage(prefix + (language === "en" ? en : fil), "assistant", language);
        taskHelpReport.href = signedIn || publicGuide ? (topic === "deviceSecurity" && !signedIn ? "/Login" : path) : "/Login";
        taskHelpReport.textContent = signedIn || publicGuide
            ? (language === "en" ? "Open relevant page" : "Buksan ang kaugnay na page")
            : (language === "en" ? "Log in" : "Mag-log in");
        taskHelpReport.hidden = false;
        return;
    }
    if (!taskHelpReport.hidden) {
        const signedIn = taskHelp.dataset.signedIn === "true";
        taskHelpReport.textContent = signedIn
            ? (language === "en" ? "Open Safety & Reports" : "Buksan ang Safety & Reports")
            : (language === "en" ? "Log in to report" : "Mag-log in para mag-report");
        const nextStep = topic === "reportStatus"
            ? (language === "en" ? "Open Your report history to see updates." : "Buksan ang Your report history para makita ang status.")
            : (language === "en" ? "Fill out the report form and choose Submit report." : "Punan ang report form at pindutin ang Submit report.");
        const direction = signedIn
            ? (language === "en" ? "Use the button below to open Safety & Reports." : "Pindutin ang button sa ibaba para buksan ang Safety & Reports.")
            : (language === "en" ? "Use the button below to log in. After signing in, open Safety & Reports from your dashboard menu." : "Mag-log in gamit ang button sa ibaba. Pagkapasok, buksan ang Safety & Reports sa menu ng dashboard.");
        addHelpMessage(direction + " " + nextStep, "assistant", language);
        return;
    }
    if (topic) {
        const answer = specificAnswers[topic]?.[["en", "fil", "taglish"].indexOf(language)] ?? answers[topic];
        const publicTopic = ["areas", "login", "password", "verify", "payment"].includes(topic);
        const signIn = !publicTopic && taskHelp.dataset.signedIn !== "true"
            ? (language === "en" ? "Log in first to access these options. " : "Mag-log in muna para makita ang mga option na ito. ")
            : "";
        addHelpMessage(signIn + answer, "assistant", language);
        return;
    }
    const [redirect, clarify] = followUp[language];
    addHelpMessage(unclear ? clarify : redirect, "assistant", language);
}

const helpPlacement = document.querySelector(".thread-task-actions");
if (helpPlacement) {
    taskHelpLaunch.classList.add("task-help-messages");
    taskHelpLaunch.querySelector("[aria-hidden]").textContent = "?";
    helpPlacement.append(taskHelpLaunch);
}
taskHelpLaunch.hidden = false;
taskHelpLaunch.addEventListener("click", () => taskHelp.showModal());
taskHelp.addEventListener("close", () => taskHelpLaunch.focus());
const languagePicker = taskHelp.querySelector("[data-help-language]");
const helpCopy = {
    en: ["What would you like to do?", "Type your question", "Send", "You can ask questions in English, Filipino, or Taglish.", "Post a task", "Find jobs", "Verify my identity", "Service areas", "Report an issue"],
    fil: ["Ano ang gusto mong gawin?", "I-type ang tanong mo", "Ipadala", "Magtanong sa English, Filipino, o Taglish.", "Mag-post ng task", "Maghanap ng trabaho", "Magpa-verify", "Saan available?", "Mag-report"]
};
languagePicker.addEventListener("change", () => {
    const language = languagePicker.value;
    const [prompt, inputLabel, send, hint, ...topics] = helpCopy[language];
    lastQuestionLanguage = language;
    taskHelp.lang = language;
    taskHelp.querySelector("[data-help-prompt]").textContent = prompt;
    taskHelp.querySelector("[data-help-greeting]").textContent = language === "fil"
        ? "Kumusta! Matutulungan kitang mag-post ng task, maghanap ng trabaho, o gamitin ang Tap, Task, Done."
        : "Hi! I can help you post a task, find work, or use Tap, Task, Done.";
    taskHelp.querySelector('label[for="task-help-input"]').textContent = inputLabel;
    taskHelp.querySelector(".task-help-input-row button").textContent = send;
    taskHelp.querySelector("#task-help-language").textContent = hint;
    taskHelp.querySelectorAll("[data-topic]").forEach((button, index) => {
        button.textContent = topics[index];
    });
    taskHelpAnswer.hidden = true;
    taskHelpReport.hidden = true;
});
taskHelp.querySelectorAll("[data-topic]").forEach(button => {
    button.addEventListener("click", () => {
        if (taskHelp.querySelector(".task-help-input-row button").disabled) return;
        addHelpMessage(button.textContent, "visitor", languagePicker.value);
        showTaskHelpAnswer(button.dataset.topic, languagePicker.value);
    });
});
document.querySelector("#task-help-question").addEventListener("submit", async event => {
    event.preventDefault();
    const question = taskHelpInput.value.trim();
    const button = event.currentTarget.querySelector("button");
    if (!question || button.disabled) return;

    const language = questionLanguage(question.toLowerCase());
    lastQuestionLanguage = language;
    button.disabled = true;
    if (sensitiveHelpInput.test(question)) {
        taskHelpInput.value = "";
        addHelpMessage(language === "en"
            ? "Please remove personal details or codes and ask again. Enter passwords and verification codes only in the app’s account forms."
            : "Alisin muna ang personal na detalye o code at magtanong ulit. Sa account forms lang ilagay ang password at verification code.", "assistant", language);
        button.disabled = false;
        taskHelpInput.focus();
        return;
    }
    addHelpMessage(question, "visitor", language);
    taskHelpInput.value = "";
    taskHelpInput.focus();
    taskHelpReport.hidden = true;
    const matchedGuide = guidePatterns.find(([, pattern]) => pattern.test(question));
    if (matchedGuide) {
        showTaskHelpAnswer(matchedGuide[0], language);
        button.disabled = false;
        return;
    }
    const reportedAccount = /\b(?:report(?:ed)?|ni-report|nireport|reported|warning|suspend(?:ed)?|restricted)\b/i.test(question) &&
        /\b(?:my\s+(?:acc|account)|(?:acc|account)\s+ko|ako)\b/i.test(question);
    if (reportedAccount) {
        addHelpMessage(language === "en"
            ? "If someone reported your account, Task Help can’t see the case or remove the report. Check the report or moderation notice you received in Notifications or by email. If your notice shows a recovery target of 3 completed tasks, complete 3 separate tasks after the warning and make sure the client confirms each as Completed. The warning clears automatically once you reach the target. If you received a restriction notice, follow its instructions. If you believe there’s a mistake and can still sign in, use Safety & Reports to explain what happened and include any relevant evidence."
            : "Kung may nag-report sa account mo, walang access ang Task Help sa case at hindi nito matatanggal ang report. Tingnan ang report o moderation notice na natanggap mo sa Notifications o email. Kung nakalagay na 3 completed tasks ang recovery target mo, kailangan ng 3 magkakaibang task na kinumpirma ng client bilang Completed pagkatapos ma-issue ang warning. Awtomatikong mawawala ang warning kapag naabot ang target. Kung may restriction notice, sundin ang nakasaad doon. Kung sa tingin mo ay may pagkakamali at nakaka-sign in ka pa, gamitin ang Safety & Reports para ipaliwanag ang nangyari at magbigay ng ebidensiya.", "assistant", language);
        button.disabled = false;
        return;
    }
    const asksWhere = /^(?:saan(?:\s+(?:po|yun|iyon|doon|dito))?(?:\s+(?:yan|iyan))?|where(?:\s+is\s+it)?)\s*[?!.]*$/i.test(question);
    const asksAboutRole = /\b(?:role|roles)\b/i.test(question) && /\b(?:saan|paano|pano|makikita|palitan|change|switch|where|select|choose|work)\b/i.test(question);
    if (asksAboutRole || (asksWhere && previousHelpTopic === "role")) {
        showTaskHelpAnswer("role", language);
        button.disabled = false;
        return;
    }
    if (asksWhere && previousHelpTopic) {
        showTaskHelpAnswer(previousHelpTopic, language);
        button.disabled = false;
        return;
    }
    if (/\b(?:single|may jowa|relationship status|panget|pangit|gwapo|pogi|maganda|ugly|handsome|pretty)\b/i.test(question) &&
        /\b(?:developer|creator|founder|gumawa|owner)\b/i.test(question)) {
        showTaskHelpAnswer(undefined, language);
        button.disabled = false;
        return;
    }
    taskHelpAnswer.hidden = true;
    button.style.setProperty("--submit-spinner-color", getComputedStyle(button).color);
    button.dataset.pendingLabel = "true";
    button.setAttribute("aria-busy", "true");
    try {
        const response = await fetch("/api/task-help", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ text: question, page: helpPage, previousTopic: previousHelpTopic })
        });
        if (!response.ok) throw new Error("Task Help unavailable");
        const { topic } = await response.json();
        if (topic === "sensitive") {
            addHelpMessage(language === "en" ? "Remove personal details or codes and try again." : "Alisin ang personal na detalye o code at subukan ulit.", "assistant", language);
            return;
        }
        const recognized = Object.hasOwn(appGuides, topic) || Object.hasOwn(english, topic) || Object.hasOwn(specificAnswers, topic);
        showTaskHelpAnswer(recognized ? topic : undefined, language, topic === "unclear");
    } catch {
        addHelpMessage(language === "en"
            ? "I couldn’t look up your question right now. Please try again in a moment."
            : "Hindi ko masagot ang tanong mo sa ngayon. Pakisubukan ulit mamaya.", "assistant", language);
    } finally {
        delete button.dataset.pendingLabel;
        button.style.removeProperty("--submit-spinner-color");
        button.removeAttribute("aria-busy");
        taskHelpAnswer.hidden = true;
        button.disabled = false;
    }
});
