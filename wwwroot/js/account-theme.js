try {
    if (localStorage.getItem("tts-theme") === "dark") {
        document.documentElement.dataset.theme = "dark";
    }
} catch {
    // Keep the default theme when browser storage is unavailable.
}