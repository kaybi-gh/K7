const _instances = new Map();

export function init(rootEl, dotnetRef) {
    if (!(rootEl instanceof Element) || _instances.has(rootEl)) return;

    let dragging = false;

    function getLabelAtPoint(clientY) {
        const items = rootEl.querySelectorAll('.k7-jump-index-item');
        for (const item of items) {
            const rect = item.getBoundingClientRect();
            if (clientY >= rect.top && clientY <= rect.bottom) {
                return item.textContent.trim();
            }
        }
        return null;
    }

    function onPointerDown(e) {
        if (e.button !== 0 && e.pointerType === 'mouse') return;
        dragging = true;
        rootEl.setPointerCapture(e.pointerId);
        const label = getLabelAtPoint(e.clientY);
        if (label) {
            dotnetRef.invokeMethodAsync('OnDragLabel', label);
        }
        e.preventDefault();
    }

    function onPointerMove(e) {
        if (!dragging) return;
        const label = getLabelAtPoint(e.clientY);
        if (label) {
            dotnetRef.invokeMethodAsync('OnDragLabel', label);
        }
        e.preventDefault();
    }

    function onPointerUp(e) {
        if (!dragging) return;
        dragging = false;
        dotnetRef.invokeMethodAsync('OnDragEnd');
    }

    rootEl.addEventListener('pointerdown', onPointerDown);
    rootEl.addEventListener('pointermove', onPointerMove);
    rootEl.addEventListener('pointerup', onPointerUp);
    rootEl.addEventListener('pointercancel', onPointerUp);

    _instances.set(rootEl, { onPointerDown, onPointerMove, onPointerUp });
}

export function dispose(rootEl) {
    const instance = _instances.get(rootEl);
    if (!instance) return;

    rootEl.removeEventListener('pointerdown', instance.onPointerDown);
    rootEl.removeEventListener('pointermove', instance.onPointerMove);
    rootEl.removeEventListener('pointerup', instance.onPointerUp);
    rootEl.removeEventListener('pointercancel', instance.onPointerUp);
    _instances.delete(rootEl);
}
