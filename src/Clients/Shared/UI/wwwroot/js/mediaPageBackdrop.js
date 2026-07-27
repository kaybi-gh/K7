var _instances = new Map();

/* Full fade reached after this many pixels of scroll (tune per feel). */
var scrollFadeDistance = 1000;

function getInstance(backdropEl) {
    var instance = _instances.get(backdropEl);
    if (!instance) {
        instance = {};
        _instances.set(backdropEl, instance);
    }

    return instance;
}

export function pickHeroImageUrl(cappedUrl, highResUrl, pixelBudget) {
    if (!cappedUrl) {
        return highResUrl || null;
    }

    if (!highResUrl || highResUrl === cappedUrl) {
        return cappedUrl;
    }

    var budget = typeof pixelBudget === 'number' && pixelBudget > 0 ? pixelBudget : 1920;
    var need = (window.innerWidth || 0) * (window.devicePixelRatio || 1);
    return need > budget ? highResUrl : cappedUrl;
}

function updateSoftStillBlur(backdropEl) {
    var instance = _instances.get(backdropEl);
    if (!instance || !instance.imageWidth || !instance.imageHeight) {
        return;
    }

    var rect = backdropEl.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) {
        return;
    }

    /* Cover only upscales past native pixels when the source is smaller than the
       backdrop in both dimensions. If either axis already exceeds the container
       (typical mobile portrait with a wide still), skip blur entirely. */
    if (instance.imageWidth >= rect.width || instance.imageHeight >= rect.height) {
        backdropEl.style.setProperty('--media-soft-still-blur', '0px');
        return;
    }

    var scale = Math.max(rect.width / instance.imageWidth, rect.height / instance.imageHeight);
    var blur = scale <= 1 ? 0 : Math.min(instance.maxBlurPx, (scale - 1) * instance.maxBlurPx);
    backdropEl.style.setProperty('--media-soft-still-blur', blur.toFixed(2) + 'px');
}

function loadSoftStillDimensions(backdropEl, imageUrl, fallbackWidth, fallbackHeight) {
    var instance = getInstance(backdropEl);

    if (instance.imageLoadToken) {
        instance.imageLoadToken.cancelled = true;
    }

    if (fallbackWidth > 0 && fallbackHeight > 0) {
        instance.imageWidth = fallbackWidth;
        instance.imageHeight = fallbackHeight;
        updateSoftStillBlur(backdropEl);
    }

    var loadToken = { cancelled: false };
    instance.imageLoadToken = loadToken;

    var img = new Image();
    img.onload = function () {
        if (loadToken.cancelled) {
            return;
        }

        if (img.naturalWidth > 0 && img.naturalHeight > 0) {
            instance.imageWidth = img.naturalWidth;
            instance.imageHeight = img.naturalHeight;
            updateSoftStillBlur(backdropEl);
        }
    };
    img.onerror = function () {
        if (loadToken.cancelled) {
            return;
        }

        updateSoftStillBlur(backdropEl);
    };
    img.src = imageUrl;
}

export function attachHeroImagePicker(backdropEl, dotNetRef, pixelBudget) {
    if (!backdropEl || !dotNetRef) {
        return false;
    }

    var instance = getInstance(backdropEl);

    if (instance.heroPickOnResize) {
        window.removeEventListener('resize', instance.heroPickOnResize);
    }

    if (instance.heroPickResizeTimer) {
        clearTimeout(instance.heroPickResizeTimer);
        instance.heroPickResizeTimer = null;
    }

    function onResize() {
        if (instance.heroPickResizeTimer) {
            clearTimeout(instance.heroPickResizeTimer);
        }

        instance.heroPickResizeTimer = setTimeout(function () {
            instance.heroPickResizeTimer = null;
            dotNetRef.invokeMethodAsync('OnHeroViewportChangedAsync');
        }, 250);
    }

    instance.heroPickOnResize = onResize;
    instance.heroPickDotNetRef = dotNetRef;
    instance.heroPickPixelBudget = pixelBudget;
    window.addEventListener('resize', onResize, { passive: true });
    return true;
}

export function preloadImage(url) {
    return new Promise(function (resolve) {
        if (!url) {
            resolve(false);
            return;
        }

        var img = new Image();
        img.onload = function () { resolve(true); };
        img.onerror = function () { resolve(false); };
        img.src = url;
    });
}

export function attachScrollFade(scrollRoot, backdropEl) {
    if (!scrollRoot || !backdropEl || typeof scrollRoot.addEventListener !== 'function') {
        return false;
    }

    var instance = getInstance(backdropEl);

    function onScroll() {
        var fade = Math.min(scrollRoot.scrollTop / scrollFadeDistance, 1);
        backdropEl.style.setProperty('--media-scroll-fade', fade.toFixed(3));
    }

    scrollRoot.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
    instance.scrollRoot = scrollRoot;
    instance.onScroll = onScroll;

    return true;
}

export function attachSoftStillBlur(backdropEl, imageUrl, fallbackWidth, fallbackHeight, maxBlurPx) {
    if (!backdropEl || !imageUrl) {
        return false;
    }

    var instance = getInstance(backdropEl);

    if (instance.onResize) {
        window.removeEventListener('resize', instance.onResize);
    }

    if (instance.resizeObserver) {
        instance.resizeObserver.disconnect();
    }

    instance.maxBlurPx = maxBlurPx;

    function onResize() {
        updateSoftStillBlur(backdropEl);
    }

    instance.onResize = onResize;
    window.addEventListener('resize', onResize, { passive: true });

    if (typeof ResizeObserver !== 'undefined') {
        instance.resizeObserver = new ResizeObserver(onResize);
        instance.resizeObserver.observe(backdropEl);
    }

    loadSoftStillDimensions(backdropEl, imageUrl, fallbackWidth, fallbackHeight);

    return true;
}

export function dispose(backdropEl) {
    var instance = _instances.get(backdropEl);
    if (!instance) {
        return;
    }

    if (instance.imageLoadToken) {
        instance.imageLoadToken.cancelled = true;
    }

    if (instance.scrollRoot && typeof instance.scrollRoot.removeEventListener === 'function') {
        instance.scrollRoot.removeEventListener('scroll', instance.onScroll);
    }

    if (instance.onResize) {
        window.removeEventListener('resize', instance.onResize);
    }

    if (instance.heroPickOnResize) {
        window.removeEventListener('resize', instance.heroPickOnResize);
    }

    if (instance.heroPickResizeTimer) {
        clearTimeout(instance.heroPickResizeTimer);
        instance.heroPickResizeTimer = null;
    }

    if (instance.resizeObserver) {
        instance.resizeObserver.disconnect();
    }

    _instances.delete(backdropEl);
}
