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
    const h = findHeading(text);
    if (!h) return false;
    h.scrollIntoView({ behavior: 'smooth', block: 'center' });
    highlightHeading(h);
    return true;
}

let pendingHeading = 0;

// Called before navigating: waits for `route` to be the current page and the heading to render, then keeps
// it centred until the page's grids stop shifting the layout. Held here, not in SiteSearch, because the
// layout (and its SiteSearch) can be replaced by the navigation. Any user scroll or key cancels it.
export function scrollToHeadingAfterNavigation(route, text) {
    const id = ++pendingHeading;
    const userInput = ['wheel', 'touchstart', 'mousedown', 'keydown'];
    const cancel = () => { if (id === pendingHeading) pendingHeading++; };
    userInput.forEach(t => window.addEventListener(t, cancel, true));
    const done = () => userInput.forEach(t => window.removeEventListener(t, cancel, true));

    const normalizePath = p => p.replace(/\/$/, '').toLowerCase();
    const path = normalizePath(new URL(route, document.baseURI).pathname);
    const started = performance.now();
    let heading = null, lastTop = null, steadyFrames = 0;

    const tick = () => {
        if (id !== pendingHeading || performance.now() - started > 5000) return done();
        if (!heading && normalizePath(location.pathname) === path) {
            heading = findHeading(text);
            if (heading) highlightHeading(heading);
        }
        if (heading) {
            const top = heading.getBoundingClientRect().top;
            if (lastTop === null || Math.abs(top - lastTop) > 1) {
                heading.scrollIntoView({ block: 'center' });
                steadyFrames = 0;
            } else if (++steadyFrames > 30) {
                return done();
            }
            lastTop = heading.getBoundingClientRect().top;
        }
        requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
}

function findHeading(text) {
    const normalized = text.trim().toLowerCase();
    return [...document.querySelectorAll('h1, h2, h3')].find(h => h.textContent.trim().toLowerCase() === normalized) ?? null;
}

function highlightHeading(h) {
    h.classList.remove('search-highlight-active');
    void h.offsetWidth; // force reflow to restart animation
    h.classList.add('search-highlight-active');
    setTimeout(() => h.classList.remove('search-highlight-active'), 2200);
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
