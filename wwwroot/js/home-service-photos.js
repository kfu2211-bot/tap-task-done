(() => {
    const slides = Array.from(document.querySelectorAll(".home-service-photos .home-service-slide, [data-service-carousel] .landing-service-slide"));
    if (slides.length < 2) return;

    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    let index = 0;
    let timer;

    function updateRotation() {
        clearInterval(timer);
        if (motion.matches || document.hidden) return;

        timer = setInterval(() => {
            slides[index].classList.remove("is-current");
            index = (index + 1) % slides.length;
            slides[index].classList.add("is-current");
        }, 3000);
    }

    motion.addEventListener("change", updateRotation);
    document.addEventListener("visibilitychange", updateRotation);
    updateRotation();
})();
