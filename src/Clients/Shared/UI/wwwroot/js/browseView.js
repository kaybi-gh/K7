let _observers = new Map();
let _sentinelObserver = null;
let _sentinelPending = false;
let _gridKeyHandlers = new Map();
let _viewportObservers = new Map();

function invokeDotNet(dotnetRef, methodName, ...args) {
    if (!dotnetRef) {
        return Promise.resolve();
    }

    return dotnetRef.invokeMethodAsync(methodName, ...args).catch(error => {
        const message = error?.message ?? String(error);
        if (message.includes("DotNetObjectReference") || message.includes("tracked object with id")) {
            return;
        }

        throw error;
    });
}

export function isMobileViewport() {
    return window.innerWidth < 600;
}

export function observeViewport(dotnetRef) {
    if (_viewportObservers.has(dotnetRef)) {
        return isMobileViewport();
    }

    const handler = () => {
        invokeDotNet(dotnetRef, "OnViewportChanged", isMobileViewport());
    };

    window.addEventListener("resize", handler);
    _viewportObservers.set(dotnetRef, handler);

    return isMobileViewport();
}

export function disposeViewport(dotnetRef) {
    if (!dotnetRef) {
        return;
    }

    const handler = _viewportObservers.get(dotnetRef);
    if (handler) {
        window.removeEventListener("resize", handler);
        _viewportObservers.delete(dotnetRef);
    }
}

export function getSettings(key) {
    try {
        const raw = localStorage.getItem("browseView." + key);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

export function saveSettings(key, settings) {
    try {
        localStorage.setItem("browseView." + key, JSON.stringify(settings));
    } catch {
    }
}

export function observeContainerWidth(element, dotnetRef) {
    if (!(element instanceof Element) || _observers.has(element)) return 0;

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const width = Math.floor(entry.contentRect.width);
            invokeDotNet(dotnetRef, "OnContainerWidthChanged", width);
        }
    });

    observer.observe(element);

    let nearEndPending = false;
    const onScroll = () => {
        const threshold = Math.max(element.clientHeight * 0.6, 480);
        if (element.scrollHeight - element.scrollTop - element.clientHeight > threshold)
            return;
        if (nearEndPending)
            return;
        nearEndPending = true;
        invokeDotNet(dotnetRef, "OnVirtualScrollNearEnd").finally(() => {
            nearEndPending = false;
        });
    };
    element.addEventListener('scroll', onScroll, { passive: true });
    _observers.set(element, { resize: observer, onScroll });

    const style = getComputedStyle(element);
    return Math.floor(element.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight));
}

export function dispose(element) {
    const entry = _observers.get(element);
    if (!entry)
        return;

    if (typeof entry.disconnect === 'function') {
        entry.disconnect();
    } else {
        if (entry.resize)
            entry.resize.disconnect();
        if (entry.onScroll)
            element.removeEventListener('scroll', entry.onScroll);
    }
    _observers.delete(element);
}

export function scrollTo(element, scrollTop) {
    if (!(element instanceof Element)) return;
    element.scrollTo({ top: scrollTop, behavior: 'instant' });
}

export function observeSentinel(element, dotnetRef) {
    if (!(element instanceof Element)) return;

    // Disconnect previous sentinel observer if any
    if (_sentinelObserver) {
        _sentinelObserver.disconnect();
        _sentinelObserver = null;
    }

    _sentinelPending = false;
    _sentinelObserver = new IntersectionObserver(entries => {
        for (const entry of entries) {
            if (entry.isIntersecting && !_sentinelPending) {
                _sentinelPending = true;
                invokeDotNet(dotnetRef, "OnSentinelVisible").finally(() => {
                    _sentinelPending = false;
                });
            }
        }
    }, { rootMargin: "200px" });

    _sentinelObserver.observe(element);
}

export function disposeSentinel() {
    if (_sentinelObserver) {
        _sentinelObserver.disconnect();
        _sentinelObserver = null;
    }
}

const VIRTUAL_ROW_SELECTOR = [
    '.k7-virtual-grid-row',
    '.k7-virtual-list-item',
    '.k7-virtual-list-placeholder',
    'tr.k7-data-table-row',
    'tr.k7-data-table-placeholder',
    'tr.browse-view-table-row'
].join(', ');

const VIRTUAL_PLACEHOLDER_FOCUS_SELECTOR = [
    '.k7-virtual-grid-placeholder-focus',
    '.k7-virtual-list-placeholder-focus',
    'tr.k7-data-table-placeholder'
].join(', ');

function getGridColumnCount(scrollRoot, fallback) {
    const row = scrollRoot.querySelector('.k7-virtual-grid-row');
    if (!row) return fallback || 1;
    const focusables = row.querySelectorAll('.focusable');
    if (focusables.length > 0) return focusables.length;
    return row.querySelectorAll(':scope > *').length || fallback || 1;
}

/**
 * Keyboard scrubbing for virtualized grids/lists/tables:
 * - own ArrowUp/Down so spatial nav cannot escape to the navbar / jump-index
 * - D-pad Up/Down walks the logical catalog (row index), not only DOM neighbors
 * - Virtualize mounts a window around that index; unloaded slots are empty tiles
 * - never steal focus to the last loaded row when a later window mounts
 * - leave the grid on Up only when scrollTop is already at the top
 * - recover focus when Virtualize replaces a focused node
 * - ArrowRight still reaches jump-index via spatial nav
 */
export function initVirtualKeyNav(scrollRoot, itemHeight, options = {}) {
    if (!(scrollRoot instanceof Element) || _gridKeyHandlers.has(scrollRoot)) return;

    const getColumns = typeof options.getColumns === 'function'
        ? options.getColumns
        : () => 1;
    const focusableSelector = options.focusableSelector || '.focusable';

    let itemHeightPx = itemHeight;
    let _colCount = 0;
    let _lastCol = 0;
    let _logicalRow = 0;
    let _totalRows = 0;
    let _recovering = false;
    let _waitingForRow = false;
    let _pendingDir = 'down';
    let _leaveGridOnPurpose = false;
    let _lastFocusTop = 0;
    let _pendingSlotTop = 0;
    let _userScrollTop = 0;

    function getRows() {
        return Array.from(scrollRoot.querySelectorAll(VIRTUAL_ROW_SELECTOR));
    }

    function getRowFocusables(row) {
        if (!row) return [];
        return Array.from(row.querySelectorAll(focusableSelector));
    }

    function isPlaceholder(el) {
        return !!(el && el.matches && el.matches(VIRTUAL_PLACEHOLDER_FOCUS_SELECTOR));
    }

    function isGridAtTop() {
        return scrollRoot.scrollTop <= 2;
    }

    function isRowVisible(row) {
        if (!row) return false;
        const rootRect = scrollRoot.getBoundingClientRect();
        const rect = row.getBoundingClientRect();
        return rect.bottom > rootRect.top + 8 && rect.top < rootRect.bottom - 8;
    }

    function clampSlotTop(top) {
        const rootRect = scrollRoot.getBoundingClientRect();
        const minTop = rootRect.top + 8;
        const maxTop = rootRect.bottom - Math.min(itemHeightPx, rootRect.height) - 8;
        if (maxTop <= minTop)
            return (rootRect.top + rootRect.bottom - itemHeightPx) / 2;
        return Math.max(minTop, Math.min(top, maxTop));
    }

    function capturePendingSlot(direction, fromRow) {
        const rootRect = scrollRoot.getBoundingClientRect();
        if (fromRow && isRowVisible(fromRow)) {
            const top = fromRow.getBoundingClientRect().top;
            _pendingSlotTop = direction === 'down'
                ? top + itemHeightPx
                : top - itemHeightPx;
            return;
        }

        if (direction === 'down')
            _pendingSlotTop = rootRect.bottom - itemHeightPx - 8;
        else
            _pendingSlotTop = rootRect.top + 8;
        _pendingSlotTop = clampSlotTop(_pendingSlotTop);
    }

    function focusElement(el) {
        if (!el) return false;
        _recovering = true;
        el.focus({ preventScroll: true });
        _recovering = false;
        if (document.activeElement !== el)
            return false;
        _lastFocusTop = el.getBoundingClientRect().top;
        return true;
    }

    function rowNearestFocus(rows) {
        if (rows.length === 0) return null;
        let best = rows[0];
        let bestDist = Infinity;
        for (let i = 0; i < rows.length; i++) {
            const dist = Math.abs(rows[i].getBoundingClientRect().top - _lastFocusTop);
            if (dist < bestDist) {
                bestDist = dist;
                best = rows[i];
            }
        }
        return best;
    }

    function rememberUserScroll() {
        _userScrollTop = scrollRoot.scrollTop;
    }

    function restoreUserScroll() {
        if (Math.abs(scrollRoot.scrollTop - _userScrollTop) > 2)
            scrollRoot.scrollTop = _userScrollTop;
    }

    function focusColInRow(row, col) {
        const cells = getRowFocusables(row);
        if (cells.length === 0) return false;
        const idx = Math.max(0, Math.min(col, cells.length - 1));
        _lastCol = idx;
        return focusElement(cells[idx]);
    }

    function scrollRowIntoView(row, direction) {
        if (!row) return;
        const rowRect = row.getBoundingClientRect();
        const rootRect = scrollRoot.getBoundingClientRect();
        const inView = rowRect.top >= rootRect.top - 2 && rowRect.bottom <= rootRect.bottom + 2;
        if (inView)
            return;

        if (direction === 'down') {
            const bottomEdge = rowRect.bottom - rootRect.top + scrollRoot.scrollTop;
            const targetScroll = bottomEdge - scrollRoot.clientHeight + 8;
            if (targetScroll > scrollRoot.scrollTop) {
                scrollRoot.scrollTop = targetScroll;
                rememberUserScroll();
            }
        } else {
            const topEdge = rowRect.top - rootRect.top + scrollRoot.scrollTop;
            const targetScroll = topEdge - 8;
            if (targetScroll < scrollRoot.scrollTop) {
                scrollRoot.scrollTop = Math.max(0, targetScroll);
                rememberUserScroll();
            }
        }
    }

    function nudgeScroll(direction) {
        const maxScroll = Math.max(0, scrollRoot.scrollHeight - scrollRoot.clientHeight);
        const delta = Math.max(itemHeightPx, 24);
        if (direction === 'down') {
            scrollRoot.scrollTop = Math.min(maxScroll, scrollRoot.scrollTop + delta);
        } else {
            scrollRoot.scrollTop = Math.max(0, scrollRoot.scrollTop - delta);
        }
        rememberUserScroll();
    }

    function requestAdjacentRow(direction, col, fromRow) {
        _waitingForRow = true;
        _pendingDir = direction;
        _lastCol = col;
        capturePendingSlot(direction, fromRow);
        if (tryFulfillPendingFocus())
            return;

        if (direction === 'down')
            nudgeScroll('down');
        else
            nudgeScroll('up');
        requestAnimationFrame(() => tryFulfillPendingFocus());
    }

    function isEligiblePendingRow(row, activeRow) {
        if (!isRowVisible(row))
            return false;
        if (!activeRow)
            return true;
        const rowTop = row.getBoundingClientRect().top;
        const activeTop = activeRow.getBoundingClientRect().top;
        if (_pendingDir === 'down')
            return rowTop > activeTop + 8;
        return rowTop < activeTop - 8;
    }

    function hasLogicalRows() {
        return !!scrollRoot.querySelector('[data-grid-row]');
    }

    function readLogicalRow(row) {
        if (!row) return _logicalRow;
        const raw = row.getAttribute('data-grid-row');
        const n = raw == null ? NaN : parseInt(raw, 10);
        return Number.isFinite(n) ? n : _logicalRow;
    }

    function findRowByLogical(index) {
        return scrollRoot.querySelector('[data-grid-row="' + index + '"]');
    }

    function scrollToLogicalRow(index, direction) {
        const rowTop = Math.max(0, index * itemHeightPx);
        const viewH = scrollRoot.clientHeight;
        const maxScroll = Math.max(0, scrollRoot.scrollHeight - viewH);
        let scrollTop = scrollRoot.scrollTop;
        const mounted = !!findRowByLogical(index);
        const goingDown = direction === 'down';

        if (!mounted) {
            // Bottom spacer can already occupy this Y while the row is not mounted.
            // Virtualize only remounts after scrollTop changes.
            const delta = Math.max(itemHeightPx, 24);
            if (goingDown) {
                const nextTop = Math.min(maxScroll, Math.max(scrollTop + delta, rowTop + itemHeightPx - viewH + 8));
                scrollTop = nextTop > scrollTop ? nextTop : Math.min(maxScroll, scrollTop + delta);
            } else if (rowTop < scrollTop) {
                scrollTop = rowTop;
            } else {
                scrollTop = Math.max(0, scrollTop - delta);
            }
        } else if (rowTop < scrollTop + 8) {
            scrollTop = rowTop;
        } else if (rowTop + itemHeightPx > scrollTop + viewH - 8) {
            scrollTop = rowTop + itemHeightPx - viewH + 8;
        }

        scrollRoot.scrollTop = Math.min(maxScroll, Math.max(0, scrollTop));
        rememberUserScroll();
    }

    function focusLogicalRow(index, col) {
        const row = findRowByLogical(index);
        if (!row) return false;
        return focusColInRow(row, col);
    }

    function isGridAtBottom() {
        const maxScroll = Math.max(0, scrollRoot.scrollHeight - scrollRoot.clientHeight);
        return scrollRoot.scrollTop >= maxScroll - 4;
    }

    function moveLogicalRow(direction, col) {
        const delta = direction === 'down' ? 1 : -1;
        const next = _logicalRow + delta;
        if (next < 0) {
            _waitingForRow = false;
            if (isGridAtTop()) {
                _leaveGridOnPurpose = true;
                return false;
            }
            return true;
        }

        if (_totalRows > 0 && next >= _totalRows) {
            if (!isGridAtBottom()) {
                _waitingForRow = true;
                _pendingDir = direction;
                nudgeScroll(direction);
                requestAnimationFrame(() => tryFulfillPendingFocus());
            }
            return true;
        }

        _logicalRow = next;
        _lastCol = col;
        _waitingForRow = true;
        _pendingDir = direction;
        scrollToLogicalRow(next, direction);
        if (focusLogicalRow(next, col)) {
            const row = findRowByLogical(next);
            scrollRowIntoView(row, direction);
            _waitingForRow = false;
            return true;
        }

        requestAnimationFrame(() => {
            if (focusLogicalRow(_logicalRow, _lastCol))
                _waitingForRow = false;
        });
        return true;
    }

    function tryFulfillPendingFocus() {
        if (!_waitingForRow) return false;

        if (hasLogicalRows()) {
            if (focusLogicalRow(_logicalRow, _lastCol)) {
                _waitingForRow = false;
                return true;
            }
            return false;
        }

        const rows = getRows();
        if (rows.length === 0)
            return false;

        const targetTop = _pendingSlotTop;
        const active = document.activeElement;
        const activeRow = active && scrollRoot.contains(active)
            ? active.closest(VIRTUAL_ROW_SELECTOR)
            : null;

        let best = null;
        let bestDist = Infinity;
        for (let i = 0; i < rows.length; i++) {
            const row = rows[i];
            if (!isEligiblePendingRow(row, activeRow))
                continue;
            const dist = Math.abs(row.getBoundingClientRect().top - targetTop);
            if (dist < bestDist) {
                bestDist = dist;
                best = row;
            }
        }

        if (!best || bestDist > itemHeightPx * 1.25)
            return false;

        if (focusColInRow(best, _lastCol)) {
            _waitingForRow = false;
            return true;
        }
        return false;
    }

    function recoverFocus() {
        if (_waitingForRow) {
            tryFulfillPendingFocus();
            return;
        }

        if (hasLogicalRows() && focusLogicalRow(_logicalRow, _lastCol))
            return;

        const rows = getRows();
        const row = rowNearestFocus(rows);
        if (!row) return;
        const dist = Math.abs(row.getBoundingClientRect().top - _lastFocusTop);
        if (dist <= itemHeightPx * 1.25) {
            focusColInRow(row, _lastCol);
            return;
        }

        _waitingForRow = true;
        capturePendingSlot(_pendingDir, null);
        tryFulfillPendingFocus();
    }

    function handleVerticalArrow(arrowKey, focused) {
        const isDown = arrowKey === 'ArrowDown';
        const isUp = arrowKey === 'ArrowUp';
        if (!isDown && !isUp) return false;

        const focusedInGrid = !!(focused && scrollRoot.contains(focused)
            && focused.matches && focused.matches(focusableSelector));
        if (!focusedInGrid) {
            if (hasLogicalRows())
                return moveLogicalRow(isDown ? 'down' : 'up', _lastCol);
            if (!_waitingForRow)
                return false;
            requestAdjacentRow(isDown ? 'down' : 'up', _lastCol, null);
            return true;
        }

        const row = focused.closest(VIRTUAL_ROW_SELECTOR);
        if (!row) {
            requestAdjacentRow(isDown ? 'down' : 'up', _lastCol, null);
            return true;
        }

        _colCount = getColumns(_colCount) || 1;
        const cells = getRowFocusables(row);
        let col = cells.indexOf(focused);
        if (col < 0) col = _lastCol;
        _lastCol = col;

        if (hasLogicalRows()) {
            _logicalRow = readLogicalRow(row);
            return moveLogicalRow(isDown ? 'down' : 'up', col);
        }

        const rows = getRows();
        const rowIndex = rows.indexOf(row);
        const direction = isDown ? 'down' : 'up';

        if (isUp && rowIndex <= 0 && isGridAtTop()) {
            _waitingForRow = false;
            _leaveGridOnPurpose = true;
            return false;
        }

        if (!isRowVisible(row)) {
            requestAdjacentRow(direction, col, row);
            return true;
        }

        const adjacent = isDown ? rows[rowIndex + 1] : rows[rowIndex - 1];
        if (!adjacent) {
            requestAdjacentRow(direction, col, row);
            return true;
        }

        focusColInRow(adjacent, col);
        scrollRowIntoView(adjacent, direction);

        if (!scrollRoot.contains(document.activeElement)) {
            requestAdjacentRow(direction, col, row);
            return true;
        }

        _waitingForRow = false;
        return true;
    }

    const onFocusIn = (e) => {
        if (_recovering) return;
        if (e.target && e.target.matches && e.target.matches(focusableSelector)
            && scrollRoot.contains(e.target)) {
            _colCount = getColumns(_colCount) || 1;
            const row = e.target.closest(VIRTUAL_ROW_SELECTOR);
            const cells = getRowFocusables(row);
            const col = cells.indexOf(e.target);
            if (col >= 0)
                _lastCol = col;
            if (_waitingForRow && hasLogicalRows()) {
                const incoming = readLogicalRow(row);
                if (incoming !== _logicalRow)
                    return;
            }
            if (row)
                _logicalRow = readLogicalRow(row);
            _lastFocusTop = e.target.getBoundingClientRect().top;
            _waitingForRow = false;
        }
    };

    const onFocusOut = () => {
        if (_recovering) return;
        setTimeout(() => {
            if (_recovering) return;
            const active = document.activeElement;
            if (active && active.isConnected && scrollRoot.contains(active))
                return;

            if (_leaveGridOnPurpose) {
                _leaveGridOnPurpose = false;
                return;
            }

            const lostToBody = !active || !active.isConnected
                || active === document.body || active === document.documentElement;
            const lostToNav = !!(active && active.closest && active.closest('.app-nav'));

            if (_waitingForRow)
                tryFulfillPendingFocus();

            if (scrollRoot.contains(document.activeElement))
                return;

            if (lostToBody || (lostToNav && (!isGridAtTop() || _waitingForRow)))
                recoverFocus();
        }, 0);
    };

    const onKeyDown = (e) => {
        const focused = document.activeElement;
        if (!focused || !scrollRoot.contains(focused)) return;

        if ((e.key === 'Enter' || e.key === ' ')
            && focused.matches(VIRTUAL_PLACEHOLDER_FOCUS_SELECTOR)) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
        if (handleVerticalArrow(e.key, focused)) {
            e.preventDefault();
            e.stopPropagation();
        }
    };

    const mutationObserver = typeof MutationObserver !== 'undefined'
        ? new MutationObserver(() => {
            if (_leaveGridOnPurpose) return;
            if (_waitingForRow) {
                tryFulfillPendingFocus();
                return;
            }

            const active = document.activeElement;
            if (!active || !active.isConnected || active === document.body)
                recoverFocus();
        })
        : null;

    if (mutationObserver) {
        mutationObserver.observe(scrollRoot, { childList: true, subtree: true });
    }

    scrollRoot.addEventListener('keydown', onKeyDown, true);
    scrollRoot.addEventListener('focusin', onFocusIn);
    scrollRoot.addEventListener('focusout', onFocusOut);
    const onScroll = () => {
        if (_waitingForRow)
            tryFulfillPendingFocus();
    };
    scrollRoot.addEventListener('scroll', onScroll, { passive: true });
    _gridKeyHandlers.set(scrollRoot, {
        onKeyDown,
        onFocusIn,
        onFocusOut,
        onScroll,
        mutationObserver,
        handleVerticalArrow,
        isWaiting: () => _waitingForRow,
        setItemHeight: (height) => {
            if (typeof height === 'number' && height > 0)
                itemHeightPx = height;
        },
        setExtent: (height, totalRows) => {
            if (typeof height === 'number' && height > 0)
                itemHeightPx = height;
            if (typeof totalRows === 'number' && totalRows >= 0)
                _totalRows = totalRows;
        }
    });
}

export function setVirtualKeyNavItemHeight(scrollRoot, rowHeight) {
    const handlers = _gridKeyHandlers.get(scrollRoot);
    if (handlers && typeof handlers.setItemHeight === 'function')
        handlers.setItemHeight(rowHeight);
}

export function setVirtualKeyNavExtent(scrollRoot, rowHeight, totalRows) {
    const handlers = _gridKeyHandlers.get(scrollRoot);
    if (handlers && typeof handlers.setExtent === 'function')
        handlers.setExtent(rowHeight, totalRows);
}

export function initGridKeyNav(gridElement, rowHeight) {
    initVirtualKeyNav(gridElement, rowHeight, {
        getColumns: (fallback) => getGridColumnCount(gridElement, fallback)
    });
}

export function initListKeyNav(listElement, itemHeight) {
    initVirtualKeyNav(listElement, itemHeight);
}

export function initTableKeyNav(scrollElement, rowHeight) {
    initVirtualKeyNav(scrollElement, rowHeight, {
        focusableSelector: 'tbody .focusable'
    });
}

export function disposeVirtualKeyNav(scrollRoot) {
    const handlers = _gridKeyHandlers.get(scrollRoot);
    if (handlers) {
        scrollRoot.removeEventListener('keydown', handlers.onKeyDown, true);
        scrollRoot.removeEventListener('focusin', handlers.onFocusIn);
        scrollRoot.removeEventListener('focusout', handlers.onFocusOut);
        if (handlers.onScroll)
            scrollRoot.removeEventListener('scroll', handlers.onScroll);
        if (handlers.mutationObserver) {
            handlers.mutationObserver.disconnect();
        }
        _gridKeyHandlers.delete(scrollRoot);
    }
}

export function disposeGridKeyNav(gridElement) {
    disposeVirtualKeyNav(gridElement);
}

export function disposeListKeyNav(listElement) {
    disposeVirtualKeyNav(listElement);
}

export function disposeTableKeyNav(scrollElement) {
    disposeVirtualKeyNav(scrollElement);
}

/** Called from navigation.js (document capture) before SpatialNavigation can steal Down to jump-index. */
export function handleVirtualBrowseArrow(arrowKey, focusedEl) {
    if (arrowKey !== 'ArrowDown' && arrowKey !== 'ArrowUp') return false;
    if (focusedEl && focusedEl.closest && focusedEl.closest('.k7-jump-index')) return false;

    let root = focusedEl && focusedEl.closest
        ? focusedEl.closest('.k7-virtual-grid, .k7-virtual-list, .k7-data-table-scroll, .browse-view-table')
        : null;

    if (!root) {
        for (const [el, handlers] of _gridKeyHandlers) {
            if (handlers && typeof handlers.isWaiting === 'function' && handlers.isWaiting()) {
                root = el;
                break;
            }
        }
    }

    if (!root && (!focusedEl || focusedEl === document.body || focusedEl === document.documentElement)) {
        for (const [el] of _gridKeyHandlers) {
            if (el.querySelector('[data-grid-row]')) {
                root = el;
                break;
            }
        }
    }

    if (!root) return false;

    const handlers = _gridKeyHandlers.get(root);
    if (!handlers || typeof handlers.handleVerticalArrow !== 'function') return false;
    try {
        return handlers.handleVerticalArrow(arrowKey, focusedEl);
    } catch {
        return false;
    }
}

if (typeof window !== 'undefined') {
    window.K7 = window.K7 || {};
    window.K7.handleVirtualBrowseArrow = handleVirtualBrowseArrow;
}
