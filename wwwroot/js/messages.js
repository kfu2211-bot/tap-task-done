document.addEventListener("DOMContentLoaded", function () {
    const messageInput = document.getElementById("messageInput");
    const sendButton = document.getElementById("sendMessage");
    const chatMessages = document.getElementById("chatMessages");
    const searchInput = document.getElementById("messageSearch");
    const conversationPanel = document.querySelector(".conversation-panel");
    const chatName = document.getElementById("chatName");
    let activeTask = null;
    let messages = [];

    try { activeTask = JSON.parse(localStorage.getItem("tapTaskDoneCurrentTask") || "null"); } catch (error) { activeTask = null; }
    try { messages = JSON.parse(localStorage.getItem("tapTaskDoneMessages") || "[]"); } catch (error) { messages = []; }
    if (!Array.isArray(messages)) messages = [];

    function saveMessages() { localStorage.setItem("tapTaskDoneMessages", JSON.stringify(messages)); }
    function timeNow() { return new Date().toLocaleTimeString([], { hour: "numeric", minute: "2-digit" }); }

    function renderMessages() {
        chatMessages.textContent = "";
        if (!activeTask || activeTask.status !== "assigned") {
            chatMessages.innerHTML = "<div class='messages-empty-state'><h2>No active provider chat</h2><p>Chat becomes available after you accept a freelancer offer.</p></div>";
            return;
        }
        const taskMessages = messages.filter(function (item) { return item.taskId === activeTask.id; });
        const chatPanel = document.querySelector(".chat-panel");
        if (chatPanel) chatPanel.classList.toggle("has-no-messages", !taskMessages.length);
        if (!taskMessages.length) {
            const welcome = document.createElement("section");
            welcome.className = "chat-welcome";
            welcome.innerHTML = "<div class='chat-welcome-icon'>☏</div><h2>Start a conversation with " + (activeTask.providerName || "your provider") + "</h2><p>Ask about arrival time, task details, or any changes.</p><div class='quick-message-list'><button type='button'>What time will you arrive?</button><button type='button'>Can you confirm the details?</button><button type='button'>I have a question</button></div>";
            welcome.querySelectorAll("button").forEach(function (button) {
                button.addEventListener("click", function () {
                    messageInput.value = button.textContent;
                    messageInput.focus();
                });
            });
            chatMessages.append(welcome);
        }
        else if (chatPanel) chatPanel.classList.remove("has-no-messages");
        taskMessages.forEach(function (item) {
            const row = document.createElement("div"); row.className = "message " + item.direction;
            const bubble = document.createElement("div"); bubble.className = "message-bubble"; bubble.append(document.createTextNode(item.text));
            const time = document.createElement("small"); time.textContent = item.time; bubble.append(time); row.append(bubble); chatMessages.append(row);
        });
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function renderConversation() {
        conversationPanel.querySelectorAll(".conversation").forEach(function (item) { item.remove(); });
        if (!activeTask || activeTask.status !== "assigned") {
            if (chatName) chatName.textContent = "Messages";
            messageInput.disabled = true; sendButton.disabled = true; messageInput.placeholder = "Accept an offer to start a chat";
            renderMessages(); return;
        }
        const conversation = document.createElement("div"); conversation.className = "conversation active";
        const initials = (activeTask.providerName || "Provider").slice(0, 2).toUpperCase();
        conversation.innerHTML = "<div class='conversation-avatar'></div><div class='conversation-details'><div class='conversation-top'><strong></strong><span>Active</span></div><p></p></div>";
        conversation.querySelector(".conversation-avatar").textContent = initials;
        conversation.querySelector("strong").textContent = activeTask.providerName || "Provider";
        conversation.querySelector("p").textContent = activeTask.title || "Active task";
        conversationPanel.append(conversation);
        if (chatName) chatName.textContent = activeTask.providerName || "Provider";
        const headerService = document.querySelector(".chat-header span"); if (headerService) headerService.textContent = (activeTask.providerService || "Provider") + " · " + (activeTask.title || "Active task");
        messageInput.disabled = false; sendButton.disabled = false; messageInput.placeholder = "Write a message…";
        renderMessages();
    }

    function sendMessage() {
        const text = messageInput.value.trim();
        if (!text || !activeTask || activeTask.status !== "assigned") return;
        messages.push({ id: "message-" + Date.now(), taskId: activeTask.id, direction: "sent", text: text, time: timeNow() });
        saveMessages(); messageInput.value = ""; renderMessages();
    }

    sendButton.addEventListener("click", sendMessage);
    messageInput.addEventListener("keydown", function (event) { if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); sendMessage(); } });
    searchInput.addEventListener("input", function () { const conversation = document.querySelector(".conversation"); if (conversation) conversation.style.display = conversation.textContent.toLowerCase().includes(this.value.toLowerCase().trim()) ? "flex" : "none"; });
    renderConversation();
});
