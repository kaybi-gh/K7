/**
 * K7 Theme initializer - runs before first Blazor render to avoid flash.
 * Restores the appearance saved on the previous visit: base theme (data-theme), the CSS
 * variables of the chosen theme, and the browser chrome color.
 */
(function () {
    var BASES = { 'default-dark': '#0d0907', 'default-light': '#e4e3d9' };

    function write(base, declarations, themeColor) {
        if (!BASES[base]) base = 'default-dark';
        document.documentElement.setAttribute('data-theme', base);

        var style = document.querySelector('style[data-k7-theme]');
        if (!style && declarations) {
            style = document.createElement('style');
            style.setAttribute('data-k7-theme', '');
            document.head.appendChild(style);
        }
        if (style) {
            style.textContent = declarations ? 'html:root{' + declarations + '}' : '';
        }

        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) {
            meta.setAttribute('content', themeColor || BASES[base]);
        }
    }

    window.K7 = window.K7 || {};
    window.K7.writeAppearance = write;

    var saved = localStorage.getItem('k7-theme');
    if (BASES[saved]) {
        write(saved, localStorage.getItem('k7-theme-css'), localStorage.getItem('k7-theme-color'));
    } else {
        write(window.__K7_DEFAULT_THEME__ === 'default-light' ? 'default-light' : 'default-dark', null, null);
    }
})();

window.K7.dismissPreload = function () {
    var el = document.getElementById('preload');
    if (!el) return;
    el.style.opacity = '0';
    el.style.pointerEvents = 'none';
    setTimeout(function () { el.remove(); }, 400);
};

window.K7.getSavedTheme = function () {
    return localStorage.getItem('k7-theme');
};

window.K7.clearSavedTheme = function () {
    localStorage.removeItem('k7-theme');
    localStorage.removeItem('k7-theme-css');
    localStorage.removeItem('k7-theme-color');
};

/* Theme picked in this browser before appearance moved to the account. Null once carried over. */
window.K7.getLegacyTheme = function () {
    if (localStorage.getItem('k7-appearance-synced')) return null;
    return localStorage.getItem('k7-theme') || '';
};

window.K7.markAppearanceSynced = function () {
    localStorage.setItem('k7-appearance-synced', '1');
};

window.K7.applyAppearance = function (base, declarations, themeColor) {
    window.K7.writeAppearance(base, declarations, themeColor);
    localStorage.setItem('k7-theme', base);
    if (declarations) {
        localStorage.setItem('k7-theme-css', declarations);
    } else {
        localStorage.removeItem('k7-theme-css');
    }
    if (themeColor) {
        localStorage.setItem('k7-theme-color', themeColor);
    } else {
        localStorage.removeItem('k7-theme-color');
    }
};

window.K7.copyText = function (text) {
    if (!navigator.clipboard || !navigator.clipboard.writeText) return Promise.resolve(false);
    return navigator.clipboard.writeText(text || '').then(function () { return true; }, function () { return false; });
};

window.K7.downloadTextFile = function (fileName, text, mimeType) {
    var url = URL.createObjectURL(new Blob([text || ''], { type: mimeType || 'text/plain' }));
    var link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
};

window.K7.getBoundingRect =function (el) {
    var r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
};

window.K7.getViewportWidth = function () {
    return window.innerWidth;
};

window.K7.clickById = function (id) {
    var el = document.getElementById(id);
    if (el) el.click();
};

window.K7.clickElement = function (el) {
    if (el) el.click();
};

window.K7.decodeLoadedImage = function (img) {
    if (!img) return Promise.resolve(false);
    if (typeof img.decode !== 'function')
        return Promise.resolve(true);
    return img.decode().then(function () { return true; }).catch(function () { return true; });
};

window.K7.markImageDecoded = function (img) {
    if (!img || img.dataset.k7Decoded === '1')
        return;
    var ready = function () {
        if (img.dataset.k7Decoded === '1')
            return;
        img.dataset.k7Decoded = '1';
        img.classList.add('k7-img-decoded');
    };
    var finish = function () {
        window.K7.decodeLoadedImage(img).then(ready);
    };
    if (img.complete && img.naturalWidth > 0) {
        finish();
        return;
    }
    img.addEventListener('load', finish, { once: true });
    img.addEventListener('error', ready, { once: true });
};

window.K7.preloadImage = function (url) {
    if (!url) return Promise.resolve();
    return new Promise(function (resolve) {
        var img = new Image();
        img.decoding = 'async';
        var done = false;
        var finish = function () {
            if (done) return;
            done = true;
            resolve();
        };
        var afterLoad = function () {
            window.K7.decodeLoadedImage(img).then(finish);
        };
        img.onload = afterLoad;
        img.onerror = finish;
        img.src = url;
        if (img.complete && img.naturalWidth > 0)
            afterLoad();
    });
};

window.K7.preloadImages = function (urls) {
    if (!urls || !urls.length) return Promise.resolve();
    return Promise.all(urls.map(window.K7.preloadImage));
};

window.K7.openExternalUrl = function (url) {
    if (!url) return false;
    var opened = window.open(url, '_blank', 'noopener,noreferrer');
    return !!opened;
};
