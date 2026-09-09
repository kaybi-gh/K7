// Couch / 10-foot layout. Detection and viewport target must stay in sync
// with TelevisionLayout.cs. Run synchronously in <head> (no defer / module).
(function () {
    var targetCssWidth = 1920;
    var tenFootMin = 1280;
    var tenFootMax = 2100;
    var ua = navigator.userAgent || '';
    var isTv = ua.indexOf('K7TV/') !== -1
        || /\bAFT[A-Z0-9]/i.test(ua)
        || /Android TV/i.test(ua);
    var dpr = window.devicePixelRatio || 1;
    var cssWidth = window.screen.width || 0;
    var scale = 1;
    var rewrote = false;

    if (isTv) {
        document.documentElement.classList.add('platform-tv');
        // Do not use initial-scale = 1/dpr. That makes 1 CSS px = 1 physical px.
        // On 4K Fire TV (dpr 2, CSS width already 1920) it blows the layout out
        // to 3840 CSS px and the TV carousel looks postage-stamp sized.
        // 1080p boxes that report ~960 CSS px still zoom out to 1920.
        // 4K that reports 3840 CSS px zooms in to 1920.
        if (cssWidth > 0 && (cssWidth < tenFootMin || cssWidth > tenFootMax)) {
            scale = cssWidth / targetCssWidth;
            if (Math.abs(scale - 1) > 0.04) {
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
    }

    window.__k7TvDebug = {
        userAgent: ua,
        isTv: isTv,
        dpr: dpr,
        cssWidth: cssWidth,
        scale: scale,
        screenW: screen.width,
        screenH: screen.height,
        viewportRewritten: rewrote
    };
})();
