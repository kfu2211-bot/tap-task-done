(() => {
    let saved = '', timer, sending = false;
    async function sync() {
        let language = 'en'; try { language = localStorage.getItem('tts-language') === 'fil' ? 'fil' : 'en'; } catch {}
        const theme = document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';
        const key = language + '/' + theme; if (key === saved) return;
        if (sending) { queue(); return; }
        sending = true;
        try {
            const response = await fetch('/api/email-presentation?language=' + language + '&theme=' + theme, {
                method:'POST', credentials:'same-origin', headers:{'X-TTS-Preference':'1'}
            });
            if (response.ok && !response.redirected) saved = key;
        } catch { /* Browser preferences remain usable during connection errors. */ }
        finally { sending = false; }
    }
    function queue() { clearTimeout(timer); timer = setTimeout(sync, 300); }
    window.addEventListener('site-language-changed',queue);
    new MutationObserver(queue).observe(document.documentElement,{attributes:true,attributeFilter:['data-theme']});
    queue();
})();
