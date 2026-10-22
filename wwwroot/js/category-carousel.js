document.addEventListener("DOMContentLoaded", () => {
    const list = document.getElementById("categoryList");
    if (!list) return;
    const carousel = list.closest(".category-carousel");
    if (!carousel) return;

    const updateEdges = () => {
        carousel.classList.toggle("has-previous", list.scrollLeft > 1);
        carousel.classList.toggle("has-next", list.scrollLeft + list.clientWidth < list.scrollWidth - 1);
    };

    list.addEventListener("wheel", (event) => {
        if (event.ctrlKey || Math.abs(event.deltaY) <= Math.abs(event.deltaX)) return;
        const maxScroll = list.scrollWidth - list.clientWidth;
        const canMove = event.deltaY > 0 ? list.scrollLeft < maxScroll - 1 : list.scrollLeft > 1;
        if (!canMove) return;

        event.preventDefault();
        const multiplier = event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? list.clientWidth : 1;
        list.scrollLeft += event.deltaY * multiplier;
    }, { passive: false });

    list.addEventListener("keydown", (event) => {
        if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
        event.preventDefault();
        const direction = event.key === "ArrowRight" ? 1 : -1;
        list.scrollBy({ left: direction * Math.max(200, list.clientWidth * 0.72), behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" });
    });
    list.addEventListener("scroll", updateEdges, { passive: true });
    window.addEventListener("resize", updateEdges);
    updateEdges();
});
