let _observers = new Map();
let _sentinelObservers = new Map();

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
    if (!(element instanceof Element) || _sentinelObservers.has(element)) return;

    let pending = false;
    const observer = new IntersectionObserver(entries => {
        for (const entry of entries) {
            if (entry.isIntersecting && !pending) {
                pending = true;
                dotnetRef.invokeMethodAsync("OnSentinelVisible").finally(() => {
                    pending = false;
                });
            }
        }
    }, { rootMargin: "200px" });

    observer.observe(element);
    _sentinelObservers.set(element, observer);
}

export function disposeSentinel(element) {
    const observer = _sentinelObservers.get(element);
    if (observer) {
        observer.disconnect();
        _sentinelObservers.delete(element);
    }
}
