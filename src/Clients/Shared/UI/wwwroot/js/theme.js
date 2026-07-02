/**
 * K7 Theme initializer - runs before first Blazor render to avoid flash.
 * Reads the saved theme from localStorage and applies it to <html>.
 */
(function () {
    var theme = localStorage.getItem('k7-theme')
        || window.__K7_DEFAULT_THEME__
        || 'default-dark';
    document.documentElement.setAttribute('data-theme', theme);

    var themeColor = theme === 'default-light' ? '#e4e3d9' : '#0d0907';
    var meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
        meta.setAttribute('content', themeColor);
    }

    var customCss = localStorage.getItem('k7-custom-css');
    if (customCss) {
        var style = document.createElement('style');
        style.setAttribute('data-k7-custom', '');
        style.textContent = customCss;
        document.head.appendChild(style);
    }
})();

window.K7 = window.K7 || {};

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

window.K7.applyTheme = function (dataAttribute) {
    document.documentElement.setAttribute('data-theme', dataAttribute);
    localStorage.setItem('k7-theme', dataAttribute);
    var themeColor = dataAttribute === 'default-light' ? '#e4e3d9' : '#0d0907';
    var meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
        meta.setAttribute('content', themeColor);
    }
};

window.K7.applyCustomCss = function (css) {
    var existing = document.querySelector('style[data-k7-custom]');
    if (existing) {
        existing.textContent = css || '';
    } else if (css) {
        var style = document.createElement('style');
        style.setAttribute('data-k7-custom', '');
        style.textContent = css;
        document.head.appendChild(style);
    }
    if (css) {
        localStorage.setItem('k7-custom-css', css);
    } else {
        localStorage.removeItem('k7-custom-css');
    }
};

window.K7.getBoundingRect = function (el) {
    var r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
};
