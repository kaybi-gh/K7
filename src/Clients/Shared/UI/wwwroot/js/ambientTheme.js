window.K7 = window.K7 || {};
window.K7.AmbientTheme = {
    _audio: null,
    play: function (url, volume) {
        this.stop();
        if (!url) return;
        var audio = new Audio(url);
        audio.loop = false;
        audio.volume = typeof volume === 'number' ? Math.min(1, Math.max(0, volume)) : 0.25;
        this._audio = audio;
        var self = this;
        audio.addEventListener('ended', function () {
            if (self._audio === audio)
                self.stop();
        });
        var p = audio.play();
        if (p && typeof p.catch === 'function') {
            p.catch(function (e) { console.warn('AmbientTheme play failed', e); });
        }
    },
    stop: function () {
        if (!this._audio) return;
        try {
            this._audio.pause();
            this._audio.removeAttribute('src');
            this._audio.load();
        } catch (e) { }
        this._audio = null;
    }
};
