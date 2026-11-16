document.querySelectorAll('.job-offer-toggle').forEach(toggle => {
    const form = document.getElementById(toggle.getAttribute('aria-controls'));
    if (!form) return;

    const price = form.elements.namedItem('price');
    if (!(price instanceof HTMLInputElement)) return;

    form.hidden = true;
    toggle.hidden = false;
    toggle.addEventListener('click', () => {
        form.hidden = !form.hidden;
        toggle.setAttribute('aria-expanded', String(!form.hidden));
        if (!form.hidden) price.focus({ preventScroll: true });
    });

    const budget = form.querySelector('[data-offer-price]');
    if (budget) {
        budget.hidden = false;
        budget.addEventListener('click', () => {
            price.value = budget.dataset.offerPrice;
            price.focus();
        });
    }
});
