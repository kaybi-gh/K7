// Provide scrollIntoViewSmooth used by SyncedLyricsDisplay
window.K7 = window.K7 || {};
window.K7.scrollIntoViewSmooth = function (el) {
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

window.K7Demo = (function () {
    var _audio = null;
    var _pendingSrc = null;

    function getAudio() {
        if (!_audio) {
            _audio = new Audio();
            _audio.volume = 1;
            _audio.addEventListener('error', function () {
                var e = _audio.error;
                console.error('[K7Demo] Audio error code=' + (e ? e.code : '?') + ' src=' + _audio.src);
            });
        }
        return _audio;
    }

    function tryPlay(a) {
        var p = a.play();
        if (p && p.catch) {
            p.catch(function (err) {
                console.warn('[K7Demo] play() blocked:', err.message || err);
            });
        }
    }

    return {
        play: function (src) {
            var a = getAudio();
            if (_pendingSrc !== src) {
                _pendingSrc = src;
                a.pause();
                a.src = src;
                a.addEventListener('canplay', function onReady() {
                    a.removeEventListener('canplay', onReady);
                    tryPlay(a);
                });
                a.load();
            } else {
                tryPlay(a);
            }
        },
        pause: function () {
            if (_audio && !_audio.paused) _audio.pause();
        },
        stop: function () {
            if (_audio) {
                _audio.pause();
                _audio.currentTime = 0;
                _pendingSrc = null;
            }
        },
        seek: function (time) {
            if (_audio && isFinite(time)) _audio.currentTime = Math.max(0, time);
        },
        setVolume: function (v) {
            getAudio().volume = Math.max(0, Math.min(1, v));
        }
    };
})();
