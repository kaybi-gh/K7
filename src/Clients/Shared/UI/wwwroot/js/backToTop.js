var _instances = new Map();

function findScrollContainer(root) {
    if (!root) return null;
    for (var child of root.children) {
        if (child === root) continue;
        var style = getComputedStyle(child);
        if (style.overflowY === 'auto' || style.overflowY === 'scroll') return child;
        var nested = findScrollContainer(child);
        if (nested) return nested;
    }
    return null;
}

export function init(buttonEl, dotnetRef, threshold) {
    if (!buttonEl) return;
    var scrollParent = findScrollContainer(buttonEl.parentElement);
    if (!scrollParent) return;

    function onScroll() {
        var visible = scrollParent.scrollTop > threshold;
        dotnetRef.invokeMethodAsync('OnVisibilityChanged', visible);
    }

    scrollParent.addEventListener('scroll', onScroll, { passive: true });
    _instances.set(buttonEl, { scrollParent: scrollParent, onScroll: onScroll });
    onScroll();
}

export function scrollToTop(buttonEl) {
    var instance = _instances.get(buttonEl);
    if (instance && instance.scrollParent) {
        instance.scrollParent.scrollTo({ top: 0, behavior: 'smooth' });
    }
}

export function dispose(buttonEl) {
    var instance = _instances.get(buttonEl);
    if (instance) {
        instance.scrollParent.removeEventListener('scroll', instance.onScroll);
        _instances.delete(buttonEl);
    }
}
