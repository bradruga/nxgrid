export function isMacOs() {
    return navigator.platform.startsWith('Mac') || /Mac/.test(navigator.userAgent);
}

export function registerSearchShortcut() {
    if (window.__searchShortcutRegistered) return;
    window.__searchShortcutRegistered = true;
    document.addEventListener('keydown', (e) => {
        if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
            e.preventDefault();
            document.getElementById('site-search-input')?.focus();
        }
    });
}

export function scrollToHeading(text) {
    const normalized = text.trim().toLowerCase();
    const headings = document.querySelectorAll('h1, h2, h3');
    for (const h of headings) {
        if (h.textContent.trim().toLowerCase() === normalized) {
            h.scrollIntoView({ behavior: 'smooth', block: 'center' });
            h.classList.remove('search-highlight-active');
            void h.offsetWidth; // force reflow to restart animation
            h.classList.add('search-highlight-active');
            setTimeout(() => h.classList.remove('search-highlight-active'), 2200);
            return true;
        }
    }
    return false;
}

export function startChartDrag(dotNetRef, clientX, clientY, startX, startY) {
    const onMove = (e) => {
        dotNetRef.invokeMethodAsync('OnDragMove', startX + e.clientX - clientX, startY + e.clientY - clientY);
    };
    const onUp = () => {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup', onUp);
        dotNetRef.invokeMethodAsync('OnDragEnd');
    };
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
}

export function startChartResize(dotNetRef, clientX, clientY, startW, startH) {
    const onMove = (e) => {
        dotNetRef.invokeMethodAsync('OnResizeMove',
            Math.max(150, startW + e.clientX - clientX),
            Math.max(100, startH + e.clientY - clientY));
    };
    const onUp = () => {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup', onUp);
        dotNetRef.invokeMethodAsync('OnResizeEnd');
    };
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
}
