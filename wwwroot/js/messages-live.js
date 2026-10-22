(() => {
    const section = document.querySelector('.messages-current');
    if (!section) return;
    let updating = false;

    async function updateConversation() {
        if (document.hidden || updating) return;
        updating = true;
        try {
            const response = await fetch(window.location.href, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) return;
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            if (page.querySelector('.messages-current [role="alert"]')) return;

            const list = section.querySelector('.messages-conversations');
            const updatedList = page.querySelector('.messages-conversations');
            if (list && updatedList && list.innerHTML !== updatedList.innerHTML) {
                list.replaceChildren(...Array.from(updatedList.childNodes, node => document.importNode(node, true)));
            }

            const messages = section.querySelector('.thread-messages');
            const updatedMessages = page.querySelector('.thread-messages');
            const task = section.querySelector('.thread-form input[name="taskId"]');
            const updatedTask = page.querySelector('.thread-form input[name="taskId"]');
            if (!messages || !updatedMessages || !task || !updatedTask || task.value !== updatedTask.value) return;
            if (messages.innerHTML !== updatedMessages.innerHTML) {
                const stayAtBottom = messages.scrollHeight - messages.scrollTop - messages.clientHeight < 80;
                messages.replaceChildren(...Array.from(updatedMessages.childNodes, node => document.importNode(node, true)));
                if (stayAtBottom) messages.scrollTop = messages.scrollHeight;
            }
        } catch (error) {
            // Keep the current conversation and draft if the connection is temporarily unavailable.
        } finally {
            updating = false;
        }
    }

    const messages = section.querySelector('.thread-messages');
    if (messages) messages.scrollTop = messages.scrollHeight;
    setInterval(updateConversation, 5000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) updateConversation(); });
})();
