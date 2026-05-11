let _observers = new Map();
let _sentinelObserver = null;
let _sentinelPending = false;
let _gridKeyHandlers = new Map();

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
    if (!(element instanceof Element) || _observers.has(element)) return;

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const width = Math.floor(entry.contentRect.width);
            dotnetRef.invokeMethodAsync("OnContainerWidthChanged", width);
        }
    });

    observer.observe(element);
    _observers.set(element, observer);
}

export function dispose(element) {
    const observer = _observers.get(element);
    if (observer) {
        observer.disconnect();
        _observers.delete(element);
    }
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
                dotnetRef.invokeMethodAsync("OnSentinelVisible").finally(() => {
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

export function initGridKeyNav(gridElement, rowHeight) {
    if (!(gridElement instanceof Element) || _gridKeyHandlers.has(gridElement)) return;

    let _lastFocusedIndex = -1;
    let _colCount = 0;
    let _recovering = false;

    function getCardIndex(el) {
        const cards = Array.from(gridElement.querySelectorAll('.focusable'));
        return cards.indexOf(el);
    }

    function getColCount() {
        const row = gridElement.querySelector('.k7-virtual-grid-row');
        if (!row) return _colCount || 1;
        const cards = row.querySelectorAll(':scope > *');
        return cards.length || _colCount || 1;
    }

    function recoverFocus() {
        if (_lastFocusedIndex < 0) return;
        _recovering = true;
        const cards = Array.from(gridElement.querySelectorAll('.focusable'));
        if (cards.length === 0) { _recovering = false; return; }
        const target = cards[Math.min(_lastFocusedIndex, cards.length - 1)];
        if (target) {
            target.focus({ preventScroll: true });
        }
        _recovering = false;
    }

    const onFocusIn = (e) => {
        if (_recovering) return;
        if (e.target && e.target.matches && e.target.matches('.focusable')) {
            _lastFocusedIndex = getCardIndex(e.target);
            _colCount = getColCount();
        }
    };

    const onFocusOut = (e) => {
        if (_recovering) return;
        // When focus leaves the grid entirely (element recycled by Virtualize)
        setTimeout(() => {
            if (!document.activeElement || document.activeElement === document.body) {
                recoverFocus();
            }
        }, 0);
    };

    const onKeyDown = (e) => {
        const focused = document.activeElement;
        if (!focused || !gridElement.contains(focused)) return;

        const isDown = e.key === 'ArrowDown';
        const isUp = e.key === 'ArrowUp';
        if (!isDown && !isUp) return;

        const row = focused.closest('.k7-virtual-grid-row');
        if (!row) return;

        // Pre-compute the target index for recovery after scroll
        if (isDown) {
            _lastFocusedIndex = getCardIndex(focused) + (_colCount || getColCount());
        } else {
            _lastFocusedIndex = Math.max(0, getCardIndex(focused) - (_colCount || getColCount()));
        }

        const rowRect = row.getBoundingClientRect();
        const gridRect = gridElement.getBoundingClientRect();

        if (isDown) {
            const bottomEdge = rowRect.bottom - gridRect.top + gridElement.scrollTop;
            const targetScroll = bottomEdge + rowHeight - gridElement.clientHeight;
            if (targetScroll > gridElement.scrollTop) {
                gridElement.scrollTop = targetScroll;
            }
        } else if (isUp) {
            const topEdge = rowRect.top - gridRect.top + gridElement.scrollTop;
            const targetScroll = topEdge - rowHeight;
            if (targetScroll < gridElement.scrollTop) {
                gridElement.scrollTop = Math.max(0, targetScroll);
            }
        }
    };

    gridElement.addEventListener('keydown', onKeyDown);
    gridElement.addEventListener('focusin', onFocusIn);
    gridElement.addEventListener('focusout', onFocusOut);
    _gridKeyHandlers.set(gridElement, { onKeyDown, onFocusIn, onFocusOut });
}

export function disposeGridKeyNav(gridElement) {
    const handlers = _gridKeyHandlers.get(gridElement);
    if (handlers) {
        gridElement.removeEventListener('keydown', handlers.onKeyDown);
        gridElement.removeEventListener('focusin', handlers.onFocusIn);
        gridElement.removeEventListener('focusout', handlers.onFocusOut);
        _gridKeyHandlers.delete(gridElement);
    }
}
