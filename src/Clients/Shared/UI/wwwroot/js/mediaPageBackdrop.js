var _instances = new Map();

/* Full fade reached after this many pixels of scroll (tune per feel). */
var scrollFadeDistance = 1000;

export function attachScrollFade(scrollRoot, backdropEl) {
    if (!scrollRoot || !backdropEl || typeof scrollRoot.addEventListener !== 'function') {
        return false;
    }

    function onScroll() {
        var fade = Math.min(scrollRoot.scrollTop / scrollFadeDistance, 1);
        backdropEl.style.setProperty('--media-scroll-fade', fade.toFixed(3));
    }

    scrollRoot.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
    _instances.set(backdropEl, { scrollRoot: scrollRoot, onScroll: onScroll });

    return true;
}

export function dispose(backdropEl) {
    var instance = _instances.get(backdropEl);
    if (!instance) {
        return;
    }

    if (instance.scrollRoot && typeof instance.scrollRoot.removeEventListener === 'function') {
        instance.scrollRoot.removeEventListener('scroll', instance.onScroll);
    }

    _instances.delete(backdropEl);
}
