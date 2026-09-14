window.K7 = window.K7 || {};

K7.Lottie = {
    _instances: {},
    _cache: {},

    _ensurePlayer: function () {
        if (window.lottie) return Promise.resolve();
        if (this._playerPromise) return this._playerPromise;
        this._playerPromise = new Promise(function (resolve) {
            var script = document.createElement('script');
            script.src = '_content/K7.Clients.Shared.UI/dist/lottie/build/player/lottie_light.min.js';
            script.onload = function () { resolve(); };
            script.onerror = function () { resolve(); };
            document.head.appendChild(script);
        });
        return this._playerPromise;
    },

    preload: function (path) {
        if (this._cache[path]) return;
        fetch(path)
            .then(function (r) { return r.json(); })
            .then(function (data) { K7.Lottie._cache[path] = data; })
            .catch(function () { });
    },

    // Match branding/logo-on-light.svg (ink #262420, accent #c49a48).
    // Used for #preload splash and the reconnection overlay on light theme.
    _recolorSplashOnLight: function (data) {
        var clone = JSON.parse(JSON.stringify(data));
        var ink = [0.149, 0.141, 0.125, 1];
        var accent = [0.769, 0.604, 0.282, 1];
        (function walk(node) {
            if (!node || typeof node !== 'object') return;
            if (node.ty === 'fl' && node.c && Array.isArray(node.c.k) && node.c.k.length >= 3) {
                node.c.k = node.c.k[0] > 0.9 ? ink.slice() : accent.slice();
            }
            if (Array.isArray(node)) {
                for (var i = 0; i < node.length; i++) walk(node[i]);
                return;
            }
            Object.keys(node).forEach(function (key) { walk(node[key]); });
        })(clone);
        return clone;
    },

    play: function (container, path) {
        var self = this;
        return this._ensurePlayer().then(function () {
            if (!container || !window.lottie) return;
            container.innerHTML = '';
            var onLight = document.documentElement.getAttribute('data-theme') === 'default-light';
            var cached = self._cache[path];
            var opts = {
                container: container,
                renderer: 'svg',
                loop: true,
                autoplay: true
            };
            if (!onLight) {
                if (cached) {
                    opts.animationData = cached;
                } else {
                    opts.path = path;
                }
                window.lottie.loadAnimation(opts);
                return;
            }
            var source = cached
                ? Promise.resolve(cached)
                : fetch(path).then(function (r) { return r.json(); }).then(function (data) {
                    self._cache[path] = data;
                    return data;
                });
            return source.then(function (data) {
                opts.animationData = self._recolorSplashOnLight(data);
                window.lottie.loadAnimation(opts);
            });
        });
    },

    create: function (id, container, path) {
        var self = this;
        return this._ensurePlayer().then(function () {
            if (!container || !window.lottie) return;
            container.innerHTML = '';
            var anim = window.lottie.loadAnimation({
                container: container,
                renderer: 'svg',
                loop: true,
                autoplay: true,
                path: path
            });
            self._instances[id] = anim;
        });
    },

    replay: function (id) {
        var a = this._instances[id];
        if (a) a.goToAndPlay(0, true);
    },

    pause: function (id) {
        var a = this._instances[id];
        if (a) a.pause();
    },

    resume: function (id) {
        var a = this._instances[id];
        if (a) a.play();
    },

    setSpeed: function (id, speed) {
        var a = this._instances[id];
        if (a) a.setSpeed(speed);
    },

    setLoop: function (id, loop) {
        var a = this._instances[id];
        if (a) a.loop = loop;
    },

    destroy: function (id) {
        var a = this._instances[id];
        if (a) { a.destroy(); delete this._instances[id]; }
    }
};
