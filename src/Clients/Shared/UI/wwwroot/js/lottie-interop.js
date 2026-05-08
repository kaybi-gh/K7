window.K7 = window.K7 || {};

K7.Lottie = {
    _instances: {},

    play: function (container, path) {
        if (!container || !window.lottie) return;
        container.innerHTML = '';
        window.lottie.loadAnimation({
            container: container,
            renderer: 'svg',
            loop: true,
            autoplay: true,
            path: path
        });
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
