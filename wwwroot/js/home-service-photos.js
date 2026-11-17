(() => {
    const carousel = document.querySelector(".home-service-photos");
    if (!carousel) return;
    const slides = Array.from(carousel.querySelectorAll(".home-service-slide"));
    const label = carousel.querySelector(".home-service-category");
    const toggle = carousel.querySelector(".home-service-toggle");
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    let index = 0;
    let timer;
    let paused = motion.matches;

    function updateRotation() {
        clearInterval(timer);
        toggle.textContent = paused ? "Play" : "Pause";
        toggle.setAttribute("aria-label", paused ? "Play service photo rotation" : "Pause service photo rotation");
        if (paused || document.hidden) return;
        timer = setInterval(() => {
            slides[index].classList.remove("is-current");
            index = (index + 1) % slides.length;
            slides[index].classList.add("is-current");
            const category = slides[index].dataset.service;
            label.textContent = category.charAt(0).toUpperCase() + category.slice(1);
        }, 5000);
    }

    toggle.hidden = false;
    toggle.addEventListener("click", () => {
        paused = !paused;
        updateRotation();
    });
    motion.addEventListener("change", event => {
        paused = event.matches;
        updateRotation();
    });
    document.addEventListener("visibilitychange", updateRotation);
    updateRotation();
})();
