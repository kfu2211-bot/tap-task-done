const resetDigits = Array.from(document.querySelectorAll('.reset-code-boxes input'));
resetDigits.forEach((input, index) => {
    input.addEventListener('input', () => {
        input.value = input.value.replace(/[^0-9]/g, '').slice(-1);
        if (input.value && resetDigits[index + 1]) resetDigits[index + 1].focus();
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'Backspace' && !input.value && resetDigits[index - 1]) resetDigits[index - 1].focus();
    });
    input.addEventListener('paste', event => {
        const digits = (event.clipboardData?.getData('text') || '').replace(/[^0-9]/g, '').slice(0, 6);
        if (!digits) return;
        event.preventDefault();
        digits.split('').forEach((digit, offset) => { if (resetDigits[index + offset]) resetDigits[index + offset].value = digit; });
        resetDigits[Math.min(index + digits.length, resetDigits.length - 1)].focus();
    });
});
