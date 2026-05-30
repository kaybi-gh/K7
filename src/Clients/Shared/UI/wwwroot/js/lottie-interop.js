window.K7 = window.K7 || {};

K7.Lottie = {
    _instances: {},
    _cache: {},

    preload: function (path) {
        if (this._cache[path]) return;
        fetch(path)
            .then(function (r) { return r.json(); })
            .then(function (data) { K7.Lottie._cache[path] = data; })
            .catch(function () { });
    },

    play: function (container, path) {
        if (!container || !window.lottie) return;
        container.innerHTML = '';
        var cached = this._cache[path];
        var opts = {
            container: container,
            renderer: 'svg',
            loop: true,
            autoplay: true
        };
        if (cached) {
            opts.animationData = cached;
        } else {
            opts.path = path;
        }
        window.lottie.loadAnimation(opts);
    },

    create: function (id, container, path) {
        if (!container || !window.lottie) return;
        container.innerHTML = '';
        var anim = window.lottie.loadAnimation({
            container: container,
            renderer: 'svg',
            loop: true,
            autoplay: true,
            path: path
        });
        this._instances[id] = anim;
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
