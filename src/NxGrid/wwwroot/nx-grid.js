// Parse a CSS rgb/rgba string → [r, g, b], or null on failure or when fully transparent (alpha 0).
function parseRgbStr(str) {
    const m = str && str.match(/rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?/);
    if (!m) return null;
    const alpha = m[4] !== undefined ? parseFloat(m[4]) : 1;
    if (alpha === 0) return null;
    return [+m[1], +m[2], +m[3]];
}

// Returns the alpha channel of a CSS rgba() string, or 1.0 for rgb() / unparseable.
function getCssAlpha(str) {
    const m = str && str.match(/rgba\s*\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*([\d.]+)/);
    return m ? parseFloat(m[1]) : 1;
}

// Parse a CSS hex color (#rgb or #rrggbb) → [r, g, b], or null on failure.
function parseRgbHex(hex) {
    let s = hex && hex.trim().replace(/^#/, '');
    if (!s) return null;
    if (s.length === 3) s = s[0]+s[0]+s[1]+s[1]+s[2]+s[2];
    if (s.length < 6) return null;
    const r = parseInt(s.slice(0,2),16), g = parseInt(s.slice(2,4),16), b = parseInt(s.slice(4,6),16);
    return isNaN(r) || isNaN(g) || isNaN(b) ? null : [r, g, b];
}

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

        // Close the column menu when the page (outside the grid) scrolls.
        // The menu is position:fixed so its pixel coords are viewport-relative, but
        // the header it points to scrolls with the page, making the menu appear detached.
        this._pageScrollHandler = () => {
            const gridElement = document.getElementById(this.id);
            if (!gridElement) return;
            if (gridElement.querySelector('.nx-grid-column-menu')) {
                this.dotNetObjectReference.invokeMethodAsync('OnColumnMenuLostFocus');
            }
            this._repositionFillHandle();
        };
        window.addEventListener('scroll', this._pageScrollHandler, { passive: true, capture: true });

        this._fillHandleAnchor = null;
        this._scrollHandler = () => this._repositionFillHandle();

    }

    setFillHandleAnchor(maxRow, maxCol, rowHeight) {
        this._fillHandleAnchor = { maxRow, maxCol, rowHeight };
        const gridElement = document.getElementById(this.id);
        if (gridElement && !this._scrollHandlerAttached) {
            gridElement.addEventListener('scroll', this._scrollHandler, { passive: true });
            this._scrollHandlerAttached = true;
        }
        if (!this._layoutObserver) {
            this._layoutObserver = new ResizeObserver(() => this._repositionFillHandle());
            this._layoutObserver.observe(document.body);
        }
    }

    clearFillHandleAnchor() {
        this._fillHandleAnchor = null;
        if (this._layoutObserver) {
            this._layoutObserver.disconnect();
            this._layoutObserver = null;
        }
    }

    _repositionFillHandle() {
        if (!this._fillHandleAnchor) return;
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return;
        const handle = gridElement.querySelector('.nx-grid-fill-handle');
        if (!handle) return;
        const { maxRow, maxCol, rowHeight } = this._fillHandleAnchor;
        const pos = this.getFillHandlePosition(maxRow, maxCol, rowHeight);
        if (pos) {
            handle.style.top = pos.top + 'px';
            handle.style.left = pos.left + 'px';
            handle.style.visibility = '';
        } else {
            handle.style.visibility = 'hidden';
        }
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
        if (!gridElement) return { top: 0, left: 0, isMobile: false };

        const menuElement = gridElement.querySelector('.nx-grid-column-menu');
        if (!menuElement) return { top: 0, left: 0, isMobile: false };

        // On narrow screens show as a centered dialog instead of a dropdown
        if (window.innerWidth <= 768) {
            const menuHeight = menuElement.offsetHeight;
            const menuWidth = menuElement.offsetWidth;
            return {
                top: Math.max(10, (window.innerHeight - menuHeight) / 2),
                left: Math.max(10, (window.innerWidth - menuWidth) / 2),
                isMobile: true
            };
        }

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        if (!headerRow) return { top: 0, left: 0, isMobile: false };

        const headerCells = headerRow.querySelectorAll('.nx-grid-cell');
        if (columnIndex < 0 || columnIndex >= headerCells.length) return { top: 0, left: 0, isMobile: false };

        const targetCell = headerCells[columnIndex];
        const cellRect = targetCell.getBoundingClientRect();

        let top = cellRect.bottom;
        let left = cellRect.left - 1;

        // Clamp horizontally (menu is visibility:hidden so offsetWidth is still valid)
        const menuWidth = menuElement.offsetWidth;
        if (left + menuWidth > window.innerWidth) {
            left = window.innerWidth - menuWidth - 10;
        }
        if (left < 0) {
            left = 10;
        }

        // Clamp vertically: flip above the header cell when the menu would overflow the bottom
        const menuHeight = menuElement.offsetHeight;
        if (top + menuHeight > window.innerHeight) {
            top = cellRect.top - menuHeight;
            if (top < 0) top = Math.max(10, window.innerHeight - menuHeight - 10);
        }

        return { top, left, isMobile: false };
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

    focusEditInput() {
        const gridEl = document.getElementById(this.id);
        if (!gridEl) return;
        const input = gridEl.querySelector('.nx-grid-edit-input, .nx-grid-combo-input, .nx-grid-edit-textarea, .nx-grid-edit-textarea-sl');
        if (input) input.focus();
    }

    setEditInputCursor(cursorPos) {
        const gridEl = document.getElementById(this.id);
        if (!gridEl) return;
        const input = gridEl.querySelector('.nx-grid-edit-input, .nx-grid-combo-input, .nx-grid-edit-textarea, .nx-grid-edit-textarea-sl');
        if (!input) return;
        try { input.setSelectionRange(cursorPos, cursorPos); } catch (_) {}
    }

    enableEditPickMode() {
        if (this._editPickHandler) return;
        const gridEl = document.getElementById(this.id);
        if (!gridEl) return;
        this._editPickHandler = (e) => {
            const input = gridEl.querySelector('.nx-grid-edit-input, .nx-grid-combo-input, .nx-grid-edit-textarea, .nx-grid-edit-textarea-sl');
            if (input && document.activeElement === input) e.preventDefault();
        };
        gridEl.addEventListener('mousedown', this._editPickHandler, true);
    }

    disableEditPickMode() {
        if (!this._editPickHandler) return;
        const gridEl = document.getElementById(this.id);
        if (gridEl) gridEl.removeEventListener('mousedown', this._editPickHandler, true);
        this._editPickHandler = null;
    }

    getCssVar(varName) {
        const el = document.getElementById(this.id);
        if (!el) return '';
        return getComputedStyle(el).getPropertyValue(varName).trim();
    }

    getCssVars(names) {
        const el = document.getElementById(this.id);
        if (!el) return {};
        const style = getComputedStyle(el);
        const result = {};
        for (const name of names) {
            const val = style.getPropertyValue(name).trim();
            if (val) result[name] = val;
        }
        return result;
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

    dragSelect(anchorRow, anchorCol, isRowMode, maxCol) {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return Promise.resolve({ endRow: anchorRow, endCol: anchorCol });

        const selClass = 'nx-grid-cell-selected';
        const anchorClass = 'nx-grid-cell-anchor';
        let endRow = anchorRow;
        let endCol = anchorCol;

        const borderColor = 'var(--nx-grid-selection-border)';

        const applyClasses = (er, ec) => {
            const minR = Math.min(anchorRow, er);
            const maxR = Math.max(anchorRow, er);
            const minC = isRowMode ? 0 : Math.min(anchorCol, ec);
            const maxC = isRowMode ? maxCol : Math.max(anchorCol, ec);

            // Resolve selection color once for blending (handles CSS variable overrides).
            // parseRgbStr fallback handles rgba() values; null means transparent/unparseable → skip blend.
            const selHex = getComputedStyle(gridElement).getPropertyValue('--nx-grid-selection-bg').trim();
            const selRgb = parseRgbHex(selHex) || parseRgbStr(selHex);
            const selAlpha = getCssAlpha(selHex);

            for (const rowEl of gridElement.querySelectorAll('.nx-grid-row[data-row]')) {
                const ri = +rowEl.dataset.row;
                const inRowRange = ri >= minR && ri <= maxR;
                for (const cell of rowEl.querySelectorAll('.nx-grid-cell[data-col]')) {
                    const ci = +cell.dataset.col;
                    const inRange = inRowRange && ci >= minC && ci <= maxC;
                    const isAnch = !isRowMode && ri === anchorRow && ci === anchorCol;
                    const wasSelected = cell.classList.contains(selClass);
                    const nowSelected = inRange && !isAnch;
                    const isFrozen = /position\s*:\s*sticky/i.test(cell.getAttribute('style') || '');

                    cell.classList.toggle(selClass, nowSelected);
                    cell.classList.toggle(anchorClass, isAnch);

                    // Blend background for cells that have an inline background-color.
                    // The inline style's background-color has higher specificity than the
                    // selection CSS class, so the class alone would have no visible effect.
                    // We read getComputedStyle AFTER toggling the class: the inline bg-color
                    // still wins over the class, so the computed value is the cell's custom
                    // color — this also resolves CSS var() references to their actual rgb value.
                    if (/background-color\s*:/i.test(cell.getAttribute('style') || '')) {
                        if (nowSelected && !wasSelected) {
                            const cellBgStr = getComputedStyle(cell).backgroundColor;
                            const cellRgb = parseRgbStr(cellBgStr);
                            const cellAlpha = getCssAlpha(cellBgStr);
                            if (selRgb) {
                                cell.dataset.selSavedStyle = cell.getAttribute('style');
                                if (isFrozen) {
                                    // Frozen (sticky) cells need an opaque base so scrolled content
                                    // can't bleed through. Keep background-color:inherit and apply
                                    // selection + any existing tint as stacked background-image layers.
                                    const curStyle = cell.getAttribute('style') || '';
                                    const existingImg = curStyle.match(/background-image\s*:\s*([^;]+)/i);
                                    const selGradient = `linear-gradient(rgba(${selRgb[0]},${selRgb[1]},${selRgb[2]},${selAlpha}),rgba(${selRgb[0]},${selRgb[1]},${selRgb[2]},${selAlpha}))`;
                                    const bgImage = existingImg ? `${selGradient},${existingImg[1].trim()}` : selGradient;
                                    const cleaned = curStyle
                                        .replace(/background-color\s*:[^;]+;?/gi, '')
                                        .replace(/background-image\s*:[^;]+;?/gi, '')
                                        .trim();
                                    cell.setAttribute('style', (cleaned ? cleaned + ';' : '') + `background-color:inherit;background-image:${bgImage};`);
                                } else if (!cellRgb) {
                                    // Fully transparent — remove inline bg-color so the
                                    // selection CSS class can show through.
                                    cell.style.removeProperty('background-color');
                                } else if (cellAlpha < 1) {
                                    // Semi-transparent — add a selection overlay while preserving
                                    // the original partially-transparent background.
                                    cell.style.setProperty('background-image',
                                        `linear-gradient(rgba(${selRgb[0]},${selRgb[1]},${selRgb[2]},0.5),rgba(${selRgb[0]},${selRgb[1]},${selRgb[2]},0.5))`);
                                } else {
                                    // Fully opaque — blend background with selection color.
                                    const r = (cellRgb[0] + selRgb[0]) >> 1;
                                    const g = (cellRgb[1] + selRgb[1]) >> 1;
                                    const b = (cellRgb[2] + selRgb[2]) >> 1;
                                    cell.style.setProperty('background-color', `rgb(${r},${g},${b})`);
                                }
                            }
                        } else if (!nowSelected && wasSelected && cell.dataset.selSavedStyle != null) {
                            cell.setAttribute('style', cell.dataset.selSavedStyle);
                            delete cell.dataset.selSavedStyle;
                        }
                    }

                    if (inRange) {
                        const parts = [];
                        if (ri === minR) parts.push(`inset 0 2px 0 0 ${borderColor}`);
                        if (ri === maxR) parts.push(`inset 0 -2px 0 0 ${borderColor}`);
                        if (ci === minC) parts.push(`inset 2px 0 0 0 ${borderColor}`);
                        if (ci === maxC) parts.push(`inset -2px 0 0 0 ${borderColor}`);
                        cell.style.boxShadow = parts.join(',');
                    } else {
                        cell.style.boxShadow = '';
                    }
                }
            }

            // Keep the fill handle tracking the drag endpoint
            if (this._fillHandleAnchor) {
                const { rowHeight } = this._fillHandleAnchor;
                this._fillHandleAnchor = { maxRow: maxR, maxCol: maxC, rowHeight };
                this._repositionFillHandle();
            }
        };

        applyClasses(anchorRow, anchorCol);

        let lastClientX = null;
        let lastClientY = null;
        let scrollInterval = null;

        const clearAutoScroll = () => {
            if (scrollInterval !== null) {
                clearInterval(scrollInterval);
                scrollInterval = null;
            }
        };

        const tryUpdateSelection = (clientX, clientY) => {
            const target = document.elementFromPoint(clientX, clientY);
            if (!target) return;
            const cellEl = target.closest('.nx-grid-cell[data-col]');
            if (!cellEl || !gridElement.contains(cellEl)) return;
            const rowEl = cellEl.closest('.nx-grid-row[data-row]');
            if (!rowEl) return;
            const ri = +rowEl.dataset.row;
            const ci = +cellEl.dataset.col;
            if (isNaN(ri) || isNaN(ci) || (ri === endRow && ci === endCol)) return;
            endRow = ri;
            endCol = ci;
            applyClasses(endRow, endCol);
        };

        // Reapply selection after Blazor re-renders rows (e.g. virtualization during auto-scroll).
        // When the cursor is outside the grid, endRow/endCol are snapped to the visible edge so
        // the selection advances with each batch of newly rendered rows. childList-only so the
        // attribute/style mutations made by applyClasses itself don't re-trigger the observer.
        const rowObserver = new MutationObserver(() => {
            if (lastClientX !== null && lastClientY !== null) {
                const gr = gridElement.getBoundingClientRect();
                const headerRow = gridElement.querySelector('.nx-grid-header-row');
                const headerHeight = headerRow ? headerRow.offsetHeight : 0;
                const rows = gridElement.querySelectorAll('.nx-grid-row[data-row]');
                if (rows.length > 0) {
                    if (lastClientY < gr.top + headerHeight)
                        endRow = +rows[0].dataset.row;
                    else if (lastClientY >= gr.bottom)
                        endRow = +rows[rows.length - 1].dataset.row;
                    else {
                        // Cursor is inside the grid vertically — find the row now at the cursor
                        // (needed when auto-scrolling with the cursor near but inside the edge)
                        const target = document.elementFromPoint(lastClientX, lastClientY);
                        const cellEl = target && target.closest('.nx-grid-cell[data-col]');
                        if (cellEl && gridElement.contains(cellEl)) {
                            const rowEl = cellEl.closest('.nx-grid-row[data-row]');
                            if (rowEl) endRow = +rowEl.dataset.row;
                        }
                    }
                    if (!isRowMode) {
                        if (lastClientX < gr.left) endCol = 0;
                        else if (lastClientX >= gr.right) endCol = maxCol;
                    }
                }
            }
            applyClasses(endRow, endCol);
        });
        rowObserver.observe(gridElement, { childList: true, subtree: true });

        const updateAutoScroll = (clientX, clientY) => {
            clearAutoScroll();
            const gridRect = gridElement.getBoundingClientRect();
            const autoScrollZone = 40;
            const relX = clientX - gridRect.left;
            const relY = clientY - gridRect.top;
            const maxScrollTop  = gridElement.scrollHeight - gridElement.clientHeight;
            const maxScrollLeft = gridElement.scrollWidth  - gridElement.clientWidth;

            let speedY = 0;
            let speedX = 0;
            if (relY < autoScrollZone && gridElement.scrollTop > 0)
                speedY = -(autoScrollZone - Math.max(0, relY)) / autoScrollZone * 10;
            else if (relY > gridRect.height - autoScrollZone && gridElement.scrollTop < maxScrollTop)
                speedY = (relY - (gridRect.height - autoScrollZone)) / autoScrollZone * 10;

            if (relX < autoScrollZone && gridElement.scrollLeft > 0)
                speedX = -(autoScrollZone - Math.max(0, relX)) / autoScrollZone * 10;
            else if (relX > gridRect.width - autoScrollZone && gridElement.scrollLeft < maxScrollLeft)
                speedX = (relX - (gridRect.width - autoScrollZone)) / autoScrollZone * 10;

            if (speedX === 0 && speedY === 0) return;

            scrollInterval = setInterval(() => {
                let didScroll = false;
                if (speedY !== 0) {
                    const newTop = Math.max(0, Math.min(maxScrollTop, gridElement.scrollTop + speedY));
                    if (newTop !== gridElement.scrollTop) { gridElement.scrollTop = newTop; didScroll = true; }
                }
                if (speedX !== 0) {
                    const newLeft = Math.max(0, Math.min(maxScrollLeft, gridElement.scrollLeft + speedX));
                    if (newLeft !== gridElement.scrollLeft) { gridElement.scrollLeft = newLeft; didScroll = true; }
                }
                if (!didScroll) clearAutoScroll();
            }, 16);
        };

        const mouseMoveHandler = (e) => {
            lastClientX = e.clientX;
            lastClientY = e.clientY;
            tryUpdateSelection(e.clientX, e.clientY);
            updateAutoScroll(e.clientX, e.clientY);
        };

        document.body.style.userSelect = 'none';
        document.addEventListener('mousemove', mouseMoveHandler);

        return new Promise(resolve => {
            document.addEventListener('mouseup', () => {
                document.removeEventListener('mousemove', mouseMoveHandler);
                clearAutoScroll();
                rowObserver.disconnect();
                document.body.style.userSelect = '';
                resolve({ endRow, endCol });
            }, { once: true });
        });
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
        if (this._pageScrollHandler) {
            window.removeEventListener('scroll', this._pageScrollHandler, { capture: true });
            this._pageScrollHandler = null;
        }
        if (this._scrollHandlerAttached) {
            const gridElement = document.getElementById(this.id);
            if (gridElement) gridElement.removeEventListener('scroll', this._scrollHandler);
            this._scrollHandlerAttached = false;
        }
        if (this._layoutObserver) {
            this._layoutObserver.disconnect();
            this._layoutObserver = null;
        }
    }

    async resizeColumn(columnIndex, startMouseX, minWidth, maxWidth, gutterHidden){
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
            // When gutter is visible it is first child, so columns start at nth-child(2).
            // When gutter is hidden there is no gutter element, so columns start at nth-child(1).
            const nthOffset = gutterHidden ? 1 : 2;
            styleEl.textContent = initialWidths
                .map((w, i) => colRule(i + nthOffset, i === columnIndex ? resizeWidth : w))
                .join('');
        };
        updateStyles(currentWidth);

        const effectiveMin = minWidth ?? 20;
        const effectiveMax = maxWidth ?? Infinity;
        const mouseMoveHandler = (event) => {
            const delta = event.clientX - startMouseX;
            currentWidth = Math.min(effectiveMax, Math.max(effectiveMin, initialWidths[columnIndex] + delta));
            updateStyles(currentWidth);
            this._repositionFillHandle();
        };
        document.addEventListener('mousemove', mouseMoveHandler);

        const grip = headerCells[columnIndex].querySelector('.nx-grid-resize-grip');
        if (grip) grip.classList.add('nx-grid-resize-grip-active');
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

        if (grip) grip.classList.remove('nx-grid-resize-grip-active');
        document.body.style.cursor = '';
        document.body.style.userSelect = '';

        // If the mouse barely moved (click without drag, or double-click's first pass),
        // clean up immediately and return empty — signal no state change to C#.
        if (Math.abs(currentWidth - initialWidths[columnIndex]) < 2) {
            styleEl.remove();
            return [];
        }

        // Keep styleEl alive — Blazor hasn't re-rendered yet. C# will call
        // cleanupResizeStyle() from OnAfterRenderAsync once the new widths are in the DOM.
        this._resizeStyleEl = styleEl;

        // Return all widths: initial for untouched columns, final for the resized one
        return initialWidths.map((w, i) => i === columnIndex ? currentWidth : w);
    }

    cleanupResizeStyle() {
        if (this._resizeStyleEl) {
            this._resizeStyleEl.remove();
            this._resizeStyleEl = null;
        }
    }

    async measureCharWidths() {
        await document.fonts.ready;
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return null;

        const computed = window.getComputedStyle(gridElement);
        const fontSize = computed.fontSize;
        const fontFamily = computed.fontFamily;
        const fontWeight = computed.fontWeight;

        // Explicitly load the exact font strings we'll use in the canvas so the
        // browser warms up the correct typeface (e.g. a web font like Roboto) before
        // measuring. document.fonts.ready resolves when fonts are in the FontFaceSet
        // but does not guarantee the canvas engine has loaded a specific weight/size.
        try {
            await Promise.all([
                document.fonts.load(`${fontWeight} ${fontSize} ${fontFamily}`),
                document.fonts.load(`bold ${fontSize} ${fontFamily}`),
            ]);
        } catch (_) { /* ignore — fall back to whatever the canvas can resolve */ }

        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        const extendedChars = 'àáâãäåæçèéêëìíîïðñòóôõöùúûüýþÿÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖÙÚÛÜÝÞŸ€£¥©®°±×÷…–—';

        const measure = (font) => {
            ctx.font = font;
            const w = {};
            for (let i = 32; i <= 126; i++) w[String.fromCharCode(i)] = ctx.measureText(String.fromCharCode(i)).width;
            for (const ch of extendedChars) w[ch] = ctx.measureText(ch).width;
            return w;
        };

        return {
            normal: measure(`${fontWeight} ${fontSize} ${fontFamily}`),
            bold:   measure(`bold ${fontSize} ${fontFamily}`),
        };
    }

    getColumnWidths() {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return [];
        const headerCells = gridElement.querySelectorAll('.nx-grid-header-row .nx-grid-cell');
        return Array.from(headerCells).map(c => c.getBoundingClientRect().width);
    }

    getHeaderMinWidths() {
        const gridElement = document.getElementById(this.id);
        if (!gridElement) return [];

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        if (!headerRow) return [];

        // Clone the header row, strip all inline width constraints, and measure the
        // natural cell widths. The clone is never visible — no layout thrash or flicker.
        const clone = headerRow.cloneNode(true);
        clone.style.cssText = 'position:absolute;visibility:hidden;pointer-events:none;width:max-content;';

        for (const cell of clone.querySelectorAll('.nx-grid-cell')) {
            cell.style.width = '';
            cell.style.minWidth = '';
            cell.style.maxWidth = '';
            cell.style.flexGrow = '';
            cell.style.flexShrink = '0';
        }

        // The title wrap has flex:1;min-width:0 which collapses to ~0 in an unconstrained
        // flex container, causing the measured cell width to reflect only the menu button.
        // Override to let each wrap size to its natural text content width.
        // Also clear overflow:hidden on both the wrap and the title span — overflow:hidden on a
        // flex item prevents browsers from correctly computing its max-content contribution to
        // the parent's width:max-content, causing the cell to still measure too narrow.
        for (const wrap of clone.querySelectorAll('.nx-grid-column-title-wrap')) {
            wrap.style.flex = 'none';
            wrap.style.width = 'max-content';
            wrap.style.overflow = 'visible';
        }
        for (const title of clone.querySelectorAll('.nx-grid-column-title')) {
            title.style.overflow = 'visible';
            title.style.textOverflow = 'clip';
        }

        document.body.appendChild(clone);
        const widths = Array.from(clone.querySelectorAll('.nx-grid-cell'))
            .map(cell => cell.getBoundingClientRect().width);
        clone.remove();

        return widths;
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
export function getInputSelection(el) {
    if (!el) return { start: 0, end: 0 };
    return { start: el.selectionStart ?? 0, end: el.selectionEnd ?? 0 };
}
export function setInputCursor(el, pos) {
    if (!el) return;
    el.setSelectionRange(pos, pos);
}
export function setInputValueAndFocus(el, value, cursorPos) {
    if (!el) return;
    el.value = value;
    el.focus();
    el.setSelectionRange(cursorPos, cursorPos);
}
export function setupFormulaRefMode(wrapperEl, inputEl) {
    if (!wrapperEl || !inputEl) return;
    wrapperEl._refModeHandler = (e) => {
        if (document.activeElement === inputEl) {
            e.preventDefault();  // keep focus on formula bar so blur doesn't fire
        }
    };
    wrapperEl.addEventListener('mousedown', wrapperEl._refModeHandler);
}
export function teardownFormulaRefMode(wrapperEl) {
    if (wrapperEl?._refModeHandler) {
        wrapperEl.removeEventListener('mousedown', wrapperEl._refModeHandler);
        delete wrapperEl._refModeHandler;
    }
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
