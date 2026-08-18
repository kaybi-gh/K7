window.K7 = window.K7 || {};

K7.ensurePlaybackAssets = function () {
    if (K7._playbackAssetsPromise)
        return K7._playbackAssetsPromise;

    if (typeof window.initVideoJs === 'function' && typeof window.initAudioPlayer === 'function') {
        K7._playbackAssetsPromise = Promise.resolve();
        return K7._playbackAssetsPromise;
    }

    K7._playbackAssetsPromise = K7._loadPlaybackAssets();
    return K7._playbackAssetsPromise;
};

K7._loadPlaybackAssets = function () {
    var ui = '_content/K7.Clients.Shared.UI/';
    return K7._loadCss(ui + 'dist/video.js/video-js.min.css')
        .then(function () { return K7._loadScript(ui + 'dist/video.js/video.min.js'); })
        .then(function () { return K7._loadScript(ui + 'js/videoplayer.js'); })
        .then(function () { return K7._loadScript(ui + 'js/audioplayer.js'); })
        .then(function () { return K7._loadScript(ui + 'js/peaksVisualizer.js'); })
        .then(function () { return K7._loadScript(ui + 'js/ambientTheme.js'); });
};

K7._loadCss = function (href) {
    return new Promise(function (resolve) {
        if (document.querySelector('link[href="' + href + '"]')) {
            resolve();
            return;
        }
        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        link.onload = function () { resolve(); };
        link.onerror = function () { resolve(); };
        document.head.appendChild(link);
    });
};

K7._loadScript = function (src) {
    return new Promise(function (resolve, reject) {
        if (document.querySelector('script[src="' + src + '"]')) {
            resolve();
            return;
        }
        var script = document.createElement('script');
        script.src = src;
        script.async = false;
        script.onload = function () { resolve(); };
        script.onerror = function () { reject(new Error('Failed to load ' + src)); };
        document.head.appendChild(script);
    });
};
