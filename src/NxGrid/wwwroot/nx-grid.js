class NxGrid {
    constructor(id, dotNetObjectReference) {
        this.id = id;
        this.dotNetObjectReference = dotNetObjectReference;
        
        // Prevent Tab from moving browser focus out of the edit cell.
        // Must be a capturing listener (fires before any element handlers).
        this._editTabHandler = (event) => {
            if (event.key === 'Tab' &&
                event.target &&
                (event.target.classList.contains('nx-grid-edit-input') ||
                 event.target.classList.contains('nx-grid-combo-input'))) {
                event.preventDefault();
            }
        };
        document.addEventListener('keydown', this._editTabHandler, true);

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
        document.addEventListener('click', (event) => {
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
        });
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

        // Vertical
        const rowTop = rowIndex * rowHeight;
        const rowBottom = rowTop + rowHeight;
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
    }

    async resizeColumn(columnIndex){
        const gridElement = document.getElementById(this.id);
        if (!gridElement) {
            console.error("Grid element not found:", this.id);
            return;
        }

        const headerRow = gridElement.querySelector('.nx-grid-header-row');
        if (!headerRow) {
            console.error("Header row not found");
            return;
        }

        const headerCells = headerRow.querySelectorAll('.nx-grid-cell');
        if (columnIndex < 0 || columnIndex >= headerCells.length) {
            console.error("Invalid column index");
            return;
        }

        const targetCell = headerCells[columnIndex];
        const headerRowRect = headerRow.getBoundingClientRect();
        const cellRect = targetCell.getBoundingClientRect();

        // Add a div to the table that overlays perfectly on grip
        let overlay = document.createElement('div');
        overlay.style.position = 'absolute';
        overlay.style.left = (cellRect.right - headerRowRect.left - 6) + 'px';
        overlay.style.width = '6px';
        overlay.style.height = cellRect.height + 'px';
        overlay.style.zIndex = '1000';
        overlay.style.cursor = 'col-resize';
        overlay.style.borderRight = '3px solid var(--bs-primary)';
        headerRow.appendChild(overlay);

        let overlayRect = overlay.getBoundingClientRect();
        let originalLeft = overlayRect.left;
        let originalWidth = cellRect.width;

        // Make overlay move with the mouse
        let mouseMoveHandler = (event) => {
            overlay.style.left = (event.clientX - headerRowRect.left - overlayRect.width / 2) + 'px';

            // Notify Blazor of resize
            //objRef.invokeMethodAsync('OnColumnResizing', index, newWidth);
        };
        document.addEventListener('mousemove', mouseMoveHandler);

        // Wait for mouse up
        await new Promise((resolve) => {
            let mouseUpHandler = () => {
                document.removeEventListener('mousemove', mouseMoveHandler);
                document.removeEventListener('mouseup', mouseUpHandler);
                resolve();
            };
            document.addEventListener('mouseup', mouseUpHandler);
        });

        // Calculate new width
        let finalOverlayRect = overlay.getBoundingClientRect();
        let newWidth = originalWidth + (finalOverlayRect.left - originalLeft);

        // Clean up
        headerRow.removeChild(overlay);

        // Return new width
        return newWidth;
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
