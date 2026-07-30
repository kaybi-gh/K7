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
    _observers.set(element, observer);

    const style = getComputedStyle(element);
    return Math.floor(element.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight));
}

export function dispose(element) {
    const observer = _observers.get(element);
    if (observer) {
        observer.disconnect();
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
 * - own ArrowUp/Down so spatial nav cannot escape to jump-index on vertical moves
 * - land on placeholder cells while Virtualize loads; block further Down until real
 * - recover focus when placeholders are replaced with content
 * - ArrowRight still reaches jump-index via spatial nav
 */
export function initVirtualKeyNav(scrollRoot, itemHeight, options = {}) {
    if (!(scrollRoot instanceof Element) || _gridKeyHandlers.has(scrollRoot)) return;

    const getColumns = typeof options.getColumns === 'function'
        ? options.getColumns
        : () => 1;
    const focusableSelector = options.focusableSelector || '.focusable';

    let _lastFocusedIndex = -1;
    let _colCount = 0;
    let _recovering = false;
    let _waitingForRow = false;
    let _desiredIndex = -1;

    function getCards() {
        return Array.from(scrollRoot.querySelectorAll(focusableSelector));
    }

    function getCardIndex(el) {
        return getCards().indexOf(el);
    }

    function isPlaceholder(el) {
        return !!(el && el.matches && el.matches(VIRTUAL_PLACEHOLDER_FOCUS_SELECTOR));
    }

    function focusCardAt(index, cards) {
        const list = cards || getCards();
        if (index < 0 || index >= list.length) return false;
        const target = list[index];
        if (!target) return false;
        _recovering = true;
        target.focus({ preventScroll: true });
        _recovering = false;
        _lastFocusedIndex = index;
        return true;
    }

    function scrollRowIntoView(row, direction) {
        if (!row) return;
        const rowRect = row.getBoundingClientRect();
        const rootRect = scrollRoot.getBoundingClientRect();

        if (direction === 'down') {
            const bottomEdge = rowRect.bottom - rootRect.top + scrollRoot.scrollTop;
            const targetScroll = bottomEdge + itemHeight - scrollRoot.clientHeight;
            if (targetScroll > scrollRoot.scrollTop) {
                scrollRoot.scrollTop = targetScroll;
            }
        } else {
            const topEdge = rowRect.top - rootRect.top + scrollRoot.scrollTop;
            const targetScroll = topEdge - itemHeight;
            if (targetScroll < scrollRoot.scrollTop) {
                scrollRoot.scrollTop = Math.max(0, targetScroll);
            }
        }
    }

    function nudgeScrollForLoad() {
        const maxScroll = Math.max(0, scrollRoot.scrollHeight - scrollRoot.clientHeight);
        const next = Math.min(maxScroll, scrollRoot.scrollTop + Math.max(itemHeight * 0.5, 24));
        if (next > scrollRoot.scrollTop) {
            scrollRoot.scrollTop = next;
        }
    }

    function tryFulfillPendingFocus() {
        if (!_waitingForRow || _desiredIndex < 0) return;

        const cards = getCards();
        if (_desiredIndex >= cards.length) {
            nudgeScrollForLoad();
            return;
        }

        const target = cards[_desiredIndex];
        focusCardAt(_desiredIndex, cards);

        if (isPlaceholder(target)) {
            scrollRowIntoView(target.closest(VIRTUAL_ROW_SELECTOR), 'down');
            return;
        }

        _waitingForRow = false;
        _desiredIndex = -1;
        scrollRowIntoView(target.closest(VIRTUAL_ROW_SELECTOR), 'down');
    }

    function recoverFocus() {
        if (_lastFocusedIndex < 0) return;
        _recovering = true;
        const cards = getCards();
        if (cards.length === 0) { _recovering = false; return; }
        const target = cards[Math.min(_lastFocusedIndex, cards.length - 1)];
        if (target) {
            target.focus({ preventScroll: true });
        }
        _recovering = false;
    }

    function handleVerticalArrow(arrowKey, focused) {
        if (!focused || !scrollRoot.contains(focused)) return false;
        if (!focused.matches(focusableSelector)) return false;

        const isDown = arrowKey === 'ArrowDown';
        const isUp = arrowKey === 'ArrowUp';
        if (!isDown && !isUp) return false;

        const row = focused.closest(VIRTUAL_ROW_SELECTOR);
        if (!row) return false;

        const cols = _colCount || getColumns(_colCount) || 1;
        _colCount = cols;
        const cards = getCards();
        const currentIndex = getCardIndex(focused);
        if (currentIndex < 0) return false;

        if (isDown && isPlaceholder(focused)) {
            _waitingForRow = true;
            _desiredIndex = currentIndex;
            nudgeScrollForLoad();
            return true;
        }

        // First row: let SpatialNavigation leave the grid (toolbar / app navbar).
        if (isUp && currentIndex < cols) {
            _waitingForRow = false;
            _desiredIndex = -1;
            return false;
        }

        const targetIndex = isDown
            ? currentIndex + cols
            : currentIndex - cols;

        if (isUp) {
            _waitingForRow = false;
            _desiredIndex = -1;
        }

        if (targetIndex >= cards.length) {
            _waitingForRow = true;
            _desiredIndex = targetIndex;
            _lastFocusedIndex = currentIndex;
            nudgeScrollForLoad();
            return true;
        }

        const target = cards[targetIndex];
        _lastFocusedIndex = targetIndex;
        focusCardAt(targetIndex, cards);
        scrollRowIntoView(target.closest(VIRTUAL_ROW_SELECTOR), isDown ? 'down' : 'up');

        if (isDown && isPlaceholder(target)) {
            _waitingForRow = true;
            _desiredIndex = targetIndex;
        } else if (!isPlaceholder(target)) {
            _waitingForRow = false;
            _desiredIndex = -1;
        }

        return true;
    }

    const onFocusIn = (e) => {
        if (_recovering) return;
        if (e.target && e.target.matches && e.target.matches(focusableSelector)) {
            _lastFocusedIndex = getCardIndex(e.target);
            _colCount = getColumns(_colCount) || 1;
            if (!isPlaceholder(e.target) && _waitingForRow && _lastFocusedIndex === _desiredIndex) {
                _waitingForRow = false;
                _desiredIndex = -1;
            }
        }
    };

    const onFocusOut = () => {
        if (_recovering) return;
        setTimeout(() => {
            const active = document.activeElement;
            if (!active || active === document.body) {
                if (_waitingForRow) {
                    tryFulfillPendingFocus();
                    return;
                }
                recoverFocus();
            }
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
            if (_waitingForRow) {
                tryFulfillPendingFocus();
            }
        })
        : null;

    if (mutationObserver) {
        mutationObserver.observe(scrollRoot, { childList: true, subtree: true });
    }

    scrollRoot.addEventListener('keydown', onKeyDown, true);
    scrollRoot.addEventListener('focusin', onFocusIn);
    scrollRoot.addEventListener('focusout', onFocusOut);
    _gridKeyHandlers.set(scrollRoot, {
        onKeyDown,
        onFocusIn,
        onFocusOut,
        mutationObserver,
        handleVerticalArrow
    });
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
    if (!focusedEl || !focusedEl.closest) return false;
    if (arrowKey !== 'ArrowDown' && arrowKey !== 'ArrowUp') return false;
    if (focusedEl.closest('.k7-jump-index')) return false;

    const root = focusedEl.closest('.k7-virtual-grid, .k7-virtual-list, .k7-data-table-scroll, .browse-view-table');
    if (!root) return false;

    const handlers = _gridKeyHandlers.get(root);
    if (!handlers || typeof handlers.handleVerticalArrow !== 'function') return false;
    return handlers.handleVerticalArrow(arrowKey, focusedEl);
}

if (typeof window !== 'undefined') {
    window.K7 = window.K7 || {};
    window.K7.handleVirtualBrowseArrow = handleVirtualBrowseArrow;
}
