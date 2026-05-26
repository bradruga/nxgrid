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
