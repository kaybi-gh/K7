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
        _observers.delete(element);
    }
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
 * - keep focus on placeholder nodes while Virtualize loads
 * - scroll the viewport ahead of ArrowUp/Down
 * - recover focus only when Virtualize recycled the node (focus fell to body)
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

    function getCards() {
        return Array.from(scrollRoot.querySelectorAll(focusableSelector));
    }

    function getCardIndex(el) {
        return getCards().indexOf(el);
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

    const onFocusIn = (e) => {
        if (_recovering) return;
        if (e.target && e.target.matches && e.target.matches(focusableSelector)) {
            _lastFocusedIndex = getCardIndex(e.target);
            _colCount = getColumns(_colCount) || 1;
        }
    };

    const onFocusOut = () => {
        if (_recovering) return;
        // Only recover when Virtualize recycled the focused node (focus fell to body).
        // Do not pull focus back when the user intentionally leaves to chrome / jump index.
        setTimeout(() => {
            const active = document.activeElement;
            if (!active || active === document.body) {
                recoverFocus();
            }
        }, 0);
    };

    const onKeyDown = (e) => {
        const focused = document.activeElement;
        if (!focused || !scrollRoot.contains(focused)) return;

        // Placeholders are Enter-inert (scrubbing only).
        if ((e.key === 'Enter' || e.key === ' ')
            && focused.matches(VIRTUAL_PLACEHOLDER_FOCUS_SELECTOR)) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        // Ignore arrow scrubbing while focus is on header/chrome controls inside the scroll root.
        if (!focused.matches(focusableSelector)) return;

        const isDown = e.key === 'ArrowDown';
        const isUp = e.key === 'ArrowUp';
        if (!isDown && !isUp) return;

        const row = focused.closest(VIRTUAL_ROW_SELECTOR);
        if (!row) return;

        const cols = _colCount || getColumns(_colCount) || 1;
        if (isDown) {
            _lastFocusedIndex = getCardIndex(focused) + cols;
        } else {
            _lastFocusedIndex = Math.max(0, getCardIndex(focused) - cols);
        }

        const rowRect = row.getBoundingClientRect();
        const rootRect = scrollRoot.getBoundingClientRect();

        if (isDown) {
            const bottomEdge = rowRect.bottom - rootRect.top + scrollRoot.scrollTop;
            const targetScroll = bottomEdge + itemHeight - scrollRoot.clientHeight;
            if (targetScroll > scrollRoot.scrollTop) {
                scrollRoot.scrollTop = targetScroll;
            }
        } else if (isUp) {
            const topEdge = rowRect.top - rootRect.top + scrollRoot.scrollTop;
            const targetScroll = topEdge - itemHeight;
            if (targetScroll < scrollRoot.scrollTop) {
                scrollRoot.scrollTop = Math.max(0, targetScroll);
            }
        }
    };

    scrollRoot.addEventListener('keydown', onKeyDown);
    scrollRoot.addEventListener('focusin', onFocusIn);
    scrollRoot.addEventListener('focusout', onFocusOut);
    _gridKeyHandlers.set(scrollRoot, { onKeyDown, onFocusIn, onFocusOut });
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
        scrollRoot.removeEventListener('keydown', handlers.onKeyDown);
        scrollRoot.removeEventListener('focusin', handlers.onFocusIn);
        scrollRoot.removeEventListener('focusout', handlers.onFocusOut);
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
