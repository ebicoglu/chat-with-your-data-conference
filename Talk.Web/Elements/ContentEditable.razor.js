export async function start(elem) {
    elem.textContent = elem.getAttribute('value');

    elem.addEventListener('blur', () => {
        elem.value = elem.textContent;
        elem.dispatchEvent(new Event('change', { 'bubbles': true }));
    });

    const observer = new MutationObserver(mutations => {
        mutations.forEach(mutation => {
            if (mutation.attributeName === 'value') {
                elem.textContent = elem.getAttribute('value');
            }
        });
    });
    
    observer.observe(elem, { attributes: true });
}
