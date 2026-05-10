let _observers = new Map();
let _sentinelObserver = null;
let _sentinelPending = false;

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
