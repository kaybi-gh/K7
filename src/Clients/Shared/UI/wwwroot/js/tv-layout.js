// Couch / 10-foot layout. Detection must stay in sync with TelevisionLayout.cs.
// Must run synchronously in <head> before first paint (no defer / module).
(function () {
    var ua = navigator.userAgent || '';
    var isTv = ua.indexOf('K7TV/') !== -1
        || /\bAFT[A-Z0-9]/i.test(ua)
        || /Android TV/i.test(ua);
    var dpr = window.devicePixelRatio || 1;
    var rewrote = false;

    if (isTv) {
        document.documentElement.classList.add('platform-tv');
        // Compensate for the TV's high pixel density: rendering at
        // initial-scale = 1/dpr makes 1 CSS px == 1 physical px so
        // the UI is no longer zoomed in. We must drop maximum-scale
        // (and user-scalable=no) for some Android WebViews to honor
        // an initial-scale below 1.
        if (dpr > 1.05) {
            var scale = 1 / dpr;
            var meta = document.querySelector('meta[name="viewport"]');
            var content = 'width=device-width, initial-scale=' + scale.toFixed(4) + ', viewport-fit=cover';
            if (meta) {
                meta.setAttribute('content', content);
            } else {
                meta = document.createElement('meta');
                meta.name = 'viewport';
                meta.content = content;
                document.head.appendChild(meta);
            }
            rewrote = true;
        }
    }

    window.__k7TvDebug = {
        userAgent: ua,
        isTv: isTv,
        dpr: dpr,
        screenW: screen.width,
        screenH: screen.height,
        viewportRewritten: rewrote
    };
})();
