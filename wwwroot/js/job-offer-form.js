document.querySelectorAll('.job-offer-editor [data-offer-price]').forEach(button => {
    const price = button.form.elements.namedItem('price');
    if (!(price instanceof HTMLInputElement)) return;

    button.hidden = false;
    button.addEventListener('click', () => {
        price.value = button.dataset.offerPrice;
        price.focus();
    });
});
