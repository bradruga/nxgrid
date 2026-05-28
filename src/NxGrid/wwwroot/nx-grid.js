class NxGrid {
    constructor(id, dotNetObjectReference) {
        this.id = id;
        this.dotNetObjectReference = dotNetObjectReference;
        
        // Prevent Tab from moving browser focus out of the edit cell.
        // Must be a capturing listener (fires before any element handlers).
        this._editTabHandler = (event) => {
            if (!event.target) return;
            const cls = event.target.classList;
            if (event.key === 'Tab' &&
                (cls.contains('nx-grid-edit-input') ||
                 cls.contains('nx-grid-combo-input') ||
                 cls.contains('nx-grid-datepicker-input') ||
                 cls.contains('nx-grid-edit-textarea') ||
                 cls.contains('nx-grid-edit-textarea-sl'))) {
                event.preventDefault();
            }
            // Prevent newline insertion in the single-line textarea used in multiline grids.
            // The Blazor keydown handler commits on Enter before the char is inserted, but
            // preventing default here avoids any race with the oninput event.
            if (event.key === 'Enter' && cls.contains('nx-grid-edit-textarea-sl')) {
                event.preventDefault();
            }
            // Prevent newline insertion when committing a multi-line cell with plain Enter
            // (or Ctrl+Enter). Without this, the browser inserts \n into the textarea
            // synchronously — before Blazor removes it — causing a one-frame flash where
            // the text appears shifted. Shift+Enter intentionally inserts a line break, so
            // it is allowed through.
            if (event.key === 'Enter' && !event.shiftKey && cls.contains('nx-grid-edit-textarea')) {
                event.preventDefault();
            }
        };
        document.addEventListener('keydown', this._editTabHandler, true);

        // Synchronously mirror the textarea value into the hidden height-anchor span so
        // row height expands in the same JS tick as a Shift+Enter newline insertion.
        // Without this, the browser repaints the textarea with extra height before Blazor
        // has updated the span, causing a brief flash where text appears shifted upward.
        this._editInputHandler = (event) => {
            if (!event.target) return;
            if (!event.target.classList.contains('nx-grid-edit-textarea')) return;
            const ta = event.target;
            const cell = ta.closest('.nx-grid-cell-editing-ml');
            if (!cell) return;
            const anchor = cell.querySelector('.nx-grid-cell-text-multiline');
            if (!anchor) return;
            const v = ta.value;
            anchor.textContent = v.endsWith('\n') ? v + '​' : v;
        };
        document.addEventListener('input', this._editInputHandler, true);

        // Selectively prevent default for navigation keys so the browser doesn't
        // scroll/navigate, while letting F-keys (F5=refresh, F12=devtools, etc.) through.
        this._gridKeyHandler = (event) => {
            if (/^F\d+$/.test(event.key)) return;
            const gridElement = document.getElementById(this.id);
            if (gridElement && gridElement.contains(event.target) && document.activeElement === gridElement) {
                event.preventDefault();
            }
        };
        document.addEventListener('keydown', this._gridKeyHandler, true);

        // Lost focus event for column menu and context menu
        this._menuClickHandler = (event) => {
            const gridElement = document.getElementById(this.id);
            if (!gridElement) return;

            const menuElement = gridElement.querySelector('.nx-grid-column-menu');
            if (menuElement && !menuElement.contains(event.target)) {
                // Header-row clicks are handled by OnColumnButtonClick — don't also
                // dismiss the menu here, or in WASM the menu closes before it appears.
                const headerRow = gridElement.querySelector('.nx-grid-header-row');
                if (!headerRow || !headerRow.contains(event.target)) {
                    this.dotNetObjectReference.invokeMethodAsync('OnColumnMenuLostFocus');
                }
            }

            const contextMenu = gridElement.querySelector('.nx-grid-context-menu');
            if (contextMenu && !contextMenu.contains(event.target)) {
                this.dotNetObjectReference.invokeMethodAsync('OnContextMenuLostFocus');
            }
        };
        document.addEventListener('click', this._menuClickHandler);
    }

    copyToClipboard(text) {
        if (!navigator.clipboard) {
            console.error("Clipboard API not supported");
            return;
        }

        navigator.clipboard.writeText(text).then(
            () => console.log('Text copied to clipboard successfully!'),
            (err) => console.error('Could not copy text: ', err)
        );
    }

    readFromClipboard() {
        if (!navigator.clipboard) return Promise.resolve('');
        return navigator.clipboard.readText().catch(() => '');
    }

    positionColumnMenu(columnIndex) {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return { top: 0, left: 0 };

        const menuElement = gridElement.querySelector('.nx-grid-column-menu');
        if (!menuElement) return { top: 0, left: 0 };

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        if (!headerRow) return { top: 0, left: 0 };

        const headerCells = headerRow.querySelectorAll('.nx-grid-cell');
        if (columnIndex < 0 || columnIndex >= headerCells.length) return { top: 0, left: 0 };

        const targetCell = headerCells[columnIndex];
        const cellRect = targetCell.getBoundingClientRect();

        const top = cellRect.bottom;
        let left = cellRect.left - 1;

        // Clamp to screen edges (menu is visibility:hidden so offsetWidth is still valid)
        const menuWidth = menuElement.offsetWidth;
        if (left + menuWidth > window.innerWidth) {
            left = window.innerWidth - menuWidth - 10;
        }
        if (left < 0) {
            left = 10;
        }

        return { top, left };
    }
    
    getPageRowCount(rowHeight) {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return 10;
        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        const headerHeight = headerRow ? headerRow.offsetHeight : 0;
        return Math.max(1, Math.floor((gridElement.clientHeight - headerHeight) / rowHeight));
    }

    scrollCellIntoView(rowIndex, rowHeight, colIndex) {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return;

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        const headerHeight = headerRow ? headerRow.offsetHeight : 0;

        // Vertical — use actual DOM positions when virtualization is disabled (multiline mode)
        let rowTop, rowBottom;
        if (gridElement.classList.contains('nx-grid-multiline')) {
            const rows = gridElement.querySelectorAll('.nx-grid-row');
            if (rowIndex < rows.length) {
                rowTop    = rows[rowIndex].offsetTop - headerHeight;
                rowBottom = rowTop + rows[rowIndex].offsetHeight;
            } else {
                rowTop    = rowIndex * rowHeight;
                rowBottom = rowTop + rowHeight;
            }
        } else {
            rowTop    = rowIndex * rowHeight;
            rowBottom = rowTop + rowHeight;
        }
        const scrollTop = gridElement.scrollTop;
        const clientHeight = gridElement.clientHeight;

        if (rowTop < scrollTop) {
            gridElement.scrollTop = rowTop;
        } else if (rowBottom > scrollTop + clientHeight - headerHeight) {
            gridElement.scrollTop = rowBottom - clientHeight + headerHeight;
        }

        // Horizontal
        const headerCells = headerRow ? headerRow.querySelectorAll('.nx-grid-cell') : [];
        if (colIndex >= 0 && colIndex < headerCells.length) {
            const rowStart = gridElement.querySelector('.nx-grid-row-start');
            const rowStartWidth = rowStart ? rowStart.offsetWidth : 0;

            const cell = headerCells[colIndex];
            const gridRect = gridElement.getBoundingClientRect();
            const cellRect = cell.getBoundingClientRect();

            const cellLeft  = cellRect.left  - gridRect.left + gridElement.scrollLeft;
            const cellRight = cellRect.right - gridRect.left + gridElement.scrollLeft;
            const scrollLeft  = gridElement.scrollLeft;
            const clientWidth = gridElement.clientWidth;

            if (cellLeft < scrollLeft + rowStartWidth) {
                gridElement.scrollLeft = cellLeft - rowStartWidth;
            } else if (cellRight > scrollLeft + clientWidth) {
                gridElement.scrollLeft = cellRight - clientWidth;
            }
        }
    }

    focusGrid() {
        const el = document.getElementById(this.id);
        if (el) el.focus();
    }

    getCssVar(varName) {
        const el = document.getElementById(this.id);
        if (!el) return '';
        return getComputedStyle(el).getPropertyValue(varName).trim();
    }

    getDatePickerPosition() {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return { top: 0, left: 0 };

        const wrapper = gridElement.querySelector('.nx-grid-datepicker-wrapper');
        if (!wrapper) return { top: 0, left: 0 };

        const rect = wrapper.getBoundingClientRect();
        const popupWidth = 228;
        let left = rect.left;

        if (left + popupWidth > window.innerWidth) left = window.innerWidth - popupWidth - 10;
        if (left < 0) left = 0;

        return { top: rect.bottom, left };
    }

    getComboDropdownPosition() {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return { top: 0, left: 0, width: 150 };

        const wrapper = gridElement.querySelector('.nx-grid-combo-wrapper');
        if (!wrapper) return { top: 0, left: 0, width: 150 };

        const rect = wrapper.getBoundingClientRect();
        let left = rect.left;
        const width = Math.max(rect.width, 150);

        if (left + width > window.innerWidth) left = window.innerWidth - width - 10;
        if (left < 0) left = 0;

        return { top: rect.bottom, left, width };
    }

    dispose() {
        if (this._editTabHandler) {
            document.removeEventListener('keydown', this._editTabHandler, true);
            this._editTabHandler = null;
        }
        if (this._gridKeyHandler) {
            document.removeEventListener('keydown', this._gridKeyHandler, true);
            this._gridKeyHandler = null;
        }
        if (this._editInputHandler) {
            document.removeEventListener('input', this._editInputHandler, true);
            this._editInputHandler = null;
        }
        if (this._menuClickHandler) {
            document.removeEventListener('click', this._menuClickHandler);
            this._menuClickHandler = null;
        }
    }

    async resizeColumn(columnIndex, startMouseX, minWidth, maxWidth){
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return [];

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        if (!headerRow) return [];

        const headerCells = headerRow.querySelectorAll('.nx-grid-cell');
        if (columnIndex < 0 || columnIndex >= headerCells.length) return [];

        // Snapshot every column's rendered pixel width before the drag starts
        const initialWidths = Array.from(headerCells).map(c => c.getBoundingClientRect().width);
        let currentWidth = initialWidths[columnIndex];

        // Inject a style element that freezes ALL columns and live-updates the target
        const styleEl = document.createElement('style');
        document.head.appendChild(styleEl);

        const safeId = CSS.escape(this.id);
        const colRule = (nth, w) =>
            `#${safeId} .nx-grid-header-row .nx-grid-cell:nth-child(${nth}),` +
            `#${safeId} .nx-grid-row .nx-grid-cell:nth-child(${nth}){` +
            `width:${w}px!important;min-width:${w}px!important;max-width:${w}px!important;flex-grow:0!important}`;

        const updateStyles = (resizeWidth) => {
            // +2: row-start gutter is first child, column cells start at nth-child(2)
            styleEl.textContent = initialWidths
                .map((w, i) => colRule(i + 2, i === columnIndex ? resizeWidth : w))
                .join('');
        };
        updateStyles(currentWidth);

        const effectiveMin = minWidth ?? 20;
        const effectiveMax = maxWidth ?? Infinity;
        const mouseMoveHandler = (event) => {
            const delta = event.clientX - startMouseX;
            currentWidth = Math.min(effectiveMax, Math.max(effectiveMin, initialWidths[columnIndex] + delta));
            updateStyles(currentWidth);
        };
        document.addEventListener('mousemove', mouseMoveHandler);

        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';

        await new Promise((resolve) => {
            const mouseUpHandler = () => {
                document.removeEventListener('mousemove', mouseMoveHandler);
                document.removeEventListener('mouseup', mouseUpHandler);
                resolve();
            };
            document.addEventListener('mouseup', mouseUpHandler);
        });

        // Keep styleEl alive — Blazor hasn't re-rendered yet. C# will call
        // cleanupResizeStyle() from OnAfterRenderAsync once the new widths are in the DOM.
        this._resizeStyleEl = styleEl;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';

        // Return all widths: initial for untouched columns, final for the resized one
        return initialWidths.map((w, i) => i === columnIndex ? currentWidth : w);
    }

    cleanupResizeStyle() {
        if (this._resizeStyleEl) {
            this._resizeStyleEl.remove();
            this._resizeStyleEl = null;
        }
    }

    getFillHandlePosition(maxRow, maxCol, rowHeight) {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return null;

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        const headerHeight = headerRow ? headerRow.offsetHeight : 0;
        const gridRect = gridElement.getBoundingClientRect();

        const headerCells = headerRow ? headerRow.querySelectorAll('.nx-grid-cell') : [];
        if (maxCol < 0 || maxCol >= headerCells.length) return null;

        const colRect = headerCells[maxCol].getBoundingClientRect();
        const colRight = colRect.right; // viewport coord

        let rowBottomViewport;
        if (gridElement.classList.contains('nx-grid-multiline')) {
            const rows = gridElement.querySelectorAll('.nx-grid-row');
            if (maxRow < rows.length)
                rowBottomViewport = rows[maxRow].getBoundingClientRect().bottom;
            else
                return null;
        } else {
            const rowBottom = headerHeight + (maxRow + 1) * rowHeight - gridElement.scrollTop;
            rowBottomViewport = gridRect.top + rowBottom;
        }

        // Hide if bottom-right corner is not visible within the grid viewport
        const visTop = gridRect.top + headerHeight;
        if (rowBottomViewport < visTop + 4 || rowBottomViewport > gridRect.bottom - 2) return null;
        if (colRight < gridRect.left + 4 || colRight > gridRect.right + 2) return null;

        return { top: rowBottomViewport - 4, left: colRight - 4 };
    }

    async dragFill(minRow, maxRow, minCol, maxCol, rowHeight, rowCount) {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return null;

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        const headerHeight = headerRow ? headerRow.offsetHeight : 0;
        const gridRect = gridElement.getBoundingClientRect();
        const headerCells = headerRow ? Array.from(headerRow.querySelectorAll('.nx-grid-cell')) : [];
        const colCount = headerCells.length;

        // Column bounds in scrollable content coordinates (stable: content-space left/right)
        const colBounds = headerCells.map(c => {
            const r = c.getBoundingClientRect();
            return {
                left:  r.left  - gridRect.left + gridElement.scrollLeft,
                right: r.right - gridRect.left + gridElement.scrollLeft,
            };
        });

        const isMultiLine = gridElement.classList.contains('nx-grid-multiline');

        const getRowTop = (idx) => {
            if (!isMultiLine) return headerHeight + idx * rowHeight;
            const rows = gridElement.querySelectorAll('.nx-grid-row');
            return idx < rows.length ? rows[idx].offsetTop : headerHeight + idx * rowHeight;
        };
        const getRowBottom = (idx) => {
            if (!isMultiLine) return headerHeight + (idx + 1) * rowHeight;
            const rows = gridElement.querySelectorAll('.nx-grid-row');
            if (idx < rows.length) { const r = rows[idx]; return r.offsetTop + r.offsetHeight; }
            return headerHeight + (idx + 1) * rowHeight;
        };
        const getRowAtY = (clientY) => {
            const relY = clientY - gridRect.top + gridElement.scrollTop;
            if (!isMultiLine) {
                const dataY = relY - headerHeight;
                return Math.max(0, Math.min(rowCount - 1, Math.floor(dataY / rowHeight)));
            }
            const rows = gridElement.querySelectorAll('.nx-grid-row');
            for (let i = 0; i < rows.length; i++) {
                if (relY < rows[i].offsetTop + rows[i].offsetHeight) return i;
            }
            return Math.max(0, rows.length - 1);
        };
        const getColAtX = (clientX) => {
            const relX = clientX - gridRect.left + gridElement.scrollLeft;
            for (let c = 0; c < colBounds.length; c++) {
                if (relX < colBounds[c].right) return c;
            }
            return Math.max(0, colBounds.length - 1);
        };

        // Source region in content-space coordinates
        const srcTop    = getRowTop(minRow);
        const srcBottom = getRowBottom(maxRow);
        const srcLeft   = minCol < colBounds.length ? colBounds[minCol].left  : 0;
        const srcRight  = maxCol < colBounds.length ? colBounds[maxCol].right : 0;

        // Preview overlay (absolute = scrolls with content)
        const preview = document.createElement('div');
        preview.className = 'nx-grid-fill-preview';
        preview.style.display = 'none';
        gridElement.appendChild(preview);

        let direction = null;
        let fillCount = 0;

        const updatePreview = (clientX, clientY) => {
            const relX = clientX - gridRect.left + gridElement.scrollLeft;
            const relY = clientY - gridRect.top  + gridElement.scrollTop;

            const extendDown  = Math.max(0, relY - srcBottom);
            const extendUp    = Math.max(0, srcTop  - relY);
            const extendRight = Math.max(0, relX - srcRight);
            const extendLeft  = Math.max(0, srcLeft - relX);
            const maxExt = Math.max(extendDown, extendUp, extendRight, extendLeft);

            if (maxExt < rowHeight / 4) {
                preview.style.display = 'none';
                direction = null; fillCount = 0;
                return;
            }

            let pTop, pLeft, pWidth, pHeight;

            if (extendDown >= extendUp && extendDown >= extendRight && extendDown >= extendLeft) {
                direction = 'down';
                const targetRow = Math.min(rowCount - 1, getRowAtY(clientY));
                fillCount = Math.max(1, targetRow - maxRow);
                pTop    = srcBottom;
                pLeft   = srcLeft;
                pWidth  = srcRight - srcLeft;
                pHeight = getRowBottom(maxRow + fillCount) - srcBottom;
            } else if (extendUp >= extendDown && extendUp >= extendRight && extendUp >= extendLeft) {
                direction = 'up';
                const targetRow = Math.max(0, getRowAtY(clientY));
                fillCount = Math.max(1, minRow - targetRow);
                const fillTop = getRowTop(minRow - fillCount);
                pTop    = fillTop;
                pLeft   = srcLeft;
                pWidth  = srcRight - srcLeft;
                pHeight = srcTop - fillTop;
            } else if (extendRight >= extendLeft) {
                direction = 'right';
                const targetCol = Math.min(colCount - 1, getColAtX(clientX));
                fillCount = Math.max(1, targetCol - maxCol);
                const endCol = Math.min(maxCol + fillCount, colCount - 1);
                pTop    = srcTop;
                pLeft   = srcRight;
                pWidth  = colBounds[endCol].right - srcRight;
                pHeight = srcBottom - srcTop;
            } else {
                direction = 'left';
                const targetCol = Math.max(0, getColAtX(clientX));
                fillCount = Math.max(1, minCol - targetCol);
                const startCol = Math.max(0, minCol - fillCount);
                pTop    = srcTop;
                pLeft   = colBounds[startCol].left;
                pWidth  = srcLeft - colBounds[startCol].left;
                pHeight = srcBottom - srcTop;
            }

            if (fillCount > 0 && pWidth > 0 && pHeight > 0) {
                preview.style.top    = `${pTop}px`;
                preview.style.left   = `${pLeft}px`;
                preview.style.width  = `${pWidth}px`;
                preview.style.height = `${pHeight}px`;
                preview.style.display = 'block';
            } else {
                preview.style.display = 'none';
            }
        };

        const mouseMoveHandler = (e) => updatePreview(e.clientX, e.clientY);
        document.addEventListener('mousemove', mouseMoveHandler);
        document.body.style.cursor    = 'crosshair';
        document.body.style.userSelect = 'none';

        const result = await new Promise(resolve => {
            document.addEventListener('mouseup', function upHandler() {
                document.removeEventListener('mousemove', mouseMoveHandler);
                document.removeEventListener('mouseup', upHandler);
                resolve(direction && fillCount > 0 ? { direction, fillCount } : null);
            });
        });

        preview.remove();
        document.body.style.cursor    = '';
        document.body.style.userSelect = '';

        return result;
    }

    async dragRow(startRowIndex, rowCount, rowHeight) {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return startRowIndex;

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        const headerHeight = headerRow ? headerRow.offsetHeight : 0;

        const indicator = document.createElement('div');
        indicator.className = 'nx-grid-drop-indicator';
        indicator.style.display = 'none';
        gridElement.appendChild(indicator);

        let targetIndex = startRowIndex;
        let lastClientY = null;

        const updateIndicator = (clientY) => {
            lastClientY = clientY;
            const gridRect = gridElement.getBoundingClientRect();
            const relY = clientY - gridRect.top - headerHeight + gridElement.scrollTop;
            let idx = Math.round(relY / rowHeight);
            targetIndex = Math.max(0, Math.min(rowCount, idx));
            // Absolute top inside the scrollable content — no scrollTop adjustment needed
            indicator.style.top = `${headerHeight + targetIndex * rowHeight}px`;
            indicator.style.display = 'block';
        };

        const autoScrollZone = 40;
        let scrollInterval = null;

        const clearAutoScroll = () => {
            if (scrollInterval !== null) {
                clearInterval(scrollInterval);
                scrollInterval = null;
            }
        };

        const updateAutoScroll = (clientY) => {
            clearAutoScroll();
            const gridRect = gridElement.getBoundingClientRect();
            const relY = clientY - gridRect.top;
            const maxScroll = gridElement.scrollHeight - gridElement.clientHeight;

            let speed = 0;
            if (relY < autoScrollZone && gridElement.scrollTop > 0) {
                speed = -(autoScrollZone - Math.max(0, relY)) / autoScrollZone * 10;
            } else if (relY > gridRect.height - autoScrollZone && gridElement.scrollTop < maxScroll) {
                speed = (relY - (gridRect.height - autoScrollZone)) / autoScrollZone * 10;
            }

            if (speed !== 0) {
                scrollInterval = setInterval(() => {
                    const newScroll = Math.max(0, Math.min(maxScroll, gridElement.scrollTop + speed));
                    if (newScroll === gridElement.scrollTop) { clearAutoScroll(); return; }
                    gridElement.scrollTop = newScroll;
                    if (lastClientY !== null) updateIndicator(lastClientY);
                }, 16);
            }
        };

        const mouseMoveHandler = (event) => {
            updateIndicator(event.clientY);
            updateAutoScroll(event.clientY);
        };

        document.addEventListener('mousemove', mouseMoveHandler);
        document.body.style.cursor = 'grabbing';
        document.body.style.userSelect = 'none';

        const result = await new Promise((resolve) => {
            document.addEventListener('mouseup', function mouseUpHandler() {
                document.removeEventListener('mousemove', mouseMoveHandler);
                document.removeEventListener('mouseup', mouseUpHandler);
                clearAutoScroll();
                resolve(targetIndex);
            });
        });

        indicator.remove();
        document.body.style.cursor = '';
        document.body.style.userSelect = '';

        return result;
    }
}

export { NxGrid };
export function nxGrid(id, dotNetObjectReference) {
    return new NxGrid(id, dotNetObjectReference);
}
export function isMacPlatform() {
    return /Mac|iPhone|iPad|iPod/.test(navigator.platform);
}
export function localStorageGet(key) {
    try { return localStorage.getItem(key); } catch { return null; }
}
export function localStorageSet(key, value) {
    try { localStorage.setItem(key, value); } catch { }
}
export function localStorageRemove(key) {
    try { localStorage.removeItem(key); } catch { }
}
export function triggerPrint(printAreaId) {
    const printArea = document.getElementById(printAreaId);
    if (!printArea) return;

    // Move to body so no ancestor positioning offsets the print area
    const parent = printArea.parentNode;
    const nextSibling = printArea.nextSibling;
    document.body.appendChild(printArea);

    const styleEl = document.createElement('style');
    styleEl.textContent = [
        '@media print {',
        `  body > *:not(#${CSS.escape(printAreaId)}) { display: none !important; }`,
        `  #${CSS.escape(printAreaId)} { position: static !important; visibility: visible !important; }`,
        `  #${CSS.escape(printAreaId)} * { visibility: visible !important; }`,
        '}'
    ].join('\n');
    document.head.appendChild(styleEl);

    window.addEventListener('afterprint', () => {
        styleEl.remove();
        if (nextSibling) parent.insertBefore(printArea, nextSibling);
        else parent.appendChild(printArea);
    }, { once: true });

    window.print();
}
