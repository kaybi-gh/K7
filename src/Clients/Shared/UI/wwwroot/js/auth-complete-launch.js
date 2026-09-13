/**
 * Opens k7:// from /auth/complete without waiting for a click.
 * Inline scripts inside the Blazor page do not run. This file is in document head.
 */
(function () {
    var params = new URLSearchParams(window.location.search);
    var uri = params.get('launch');
    if (!uri)
        return;

    var normalized = uri.toLowerCase();
    if (normalized.indexOf('k7://callback/login') !== 0
        && normalized.indexOf('k7://callback/logout') !== 0)
        return;

    var frame = document.createElement('iframe');
    frame.setAttribute('hidden', 'hidden');
    frame.src = uri;
    document.documentElement.appendChild(frame);
    window.location.replace(uri);
})();
