window.K7 = window.K7 || {};
window.K7.AmbientTheme = {
    _audio: null,
    _objectUrl: null,
    _sourceUrl: null,
    _targetVolume: 0.25,
    _fadeRaf: null,
    _generation: 0,

    playBytes: async function (bytes, volume, crossfadeSeconds, sourceUrl, dotNetRef) {
        if (!bytes || !bytes.length) return;

        var targetVolume = typeof volume === 'number' ? Math.min(1, Math.max(0, volume)) : 0.25;
        var crossfadeMs = Math.max(0, (typeof crossfadeSeconds === 'number' ? crossfadeSeconds : 1.2) * 1000);
        var generation = ++this._generation;
        var identity = sourceUrl || '';

        if (identity && this._sourceUrl === identity && this._audio && !this._audio.paused)
            return;

        var data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        var objectUrl = URL.createObjectURL(new Blob([data], { type: 'audio/mpeg' }));

        var next = new Audio(objectUrl);
        next.loop = false;
        next.preload = 'auto';
        next.volume = 0;

        var self = this;
        next.addEventListener('ended', function () {
            if (self._audio !== next)
                return;
            self.stop();
            if (dotNetRef && typeof dotNetRef.invokeMethodAsync === 'function') {
                dotNetRef.invokeMethodAsync('NotifyNaturalEnded')
                    .catch(function (e) { console.warn('AmbientTheme NotifyNaturalEnded failed', e); });
            }
        });

        try {
            var playPromise = next.play();
            if (playPromise && typeof playPromise.catch === 'function')
                await playPromise;
        } catch (e) {
            console.warn('AmbientTheme play failed', e);
            this._revoke(objectUrl);
            return;
        }

        if (generation !== this._generation) {
            try { next.pause(); } catch (e) { }
            this._revoke(objectUrl);
            return;
        }

        var previous = this._audio;
        var previousObjectUrl = this._objectUrl;
        this._audio = next;
        this._objectUrl = objectUrl;
        this._sourceUrl = identity;
        this._targetVolume = targetVolume;
        this._dotNetRef = dotNetRef || null;

        if (previous && crossfadeMs > 0) {
            await this._crossfade(previous, next, targetVolume, crossfadeMs, generation);
            this._disposeAudio(previous, previousObjectUrl);
        } else {
            if (previous)
                this._disposeAudio(previous, previousObjectUrl);
            if (crossfadeMs > 0)
                await this._fadeVolume(next, 0, targetVolume, Math.min(crossfadeMs, 800), generation);
            else
                next.volume = targetVolume;
        }
    },

    fadeOut: async function (durationSeconds) {
        var audio = this._audio;
        if (!audio) return;

        var generation = ++this._generation;
        var durationMs = Math.max(0, (typeof durationSeconds === 'number' ? durationSeconds : 1.5) * 1000);
        var from = audio.volume;

        if (durationMs <= 0) {
            this.stop();
            return;
        }

        await this._fadeVolume(audio, from, 0, durationMs, generation);
        if (generation === this._generation)
            this.stop();
    },

    stop: function () {
        this._generation++;
        this._cancelFade();

        var audio = this._audio;
        var objectUrl = this._objectUrl;
        this._audio = null;
        this._objectUrl = null;
        this._sourceUrl = null;
        this._disposeAudio(audio, objectUrl);
    },

    _crossfade: async function (fromAudio, toAudio, targetVolume, durationMs, generation) {
        var fromStart = fromAudio.volume;
        var start = performance.now();
        var self = this;

        return new Promise(function (resolve) {
            self._cancelFade();

            var step = function (now) {
                if (generation !== self._generation) {
                    resolve();
                    return;
                }

                var t = Math.min(1, (now - start) / durationMs);
                var fadeOut = Math.cos(t * 0.5 * Math.PI);
                var fadeIn = Math.sin(t * 0.5 * Math.PI);

                try { fromAudio.volume = fromStart * fadeOut; } catch (e) { }
                try { toAudio.volume = targetVolume * fadeIn; } catch (e) { }

                if (t < 1) {
                    self._fadeRaf = requestAnimationFrame(step);
                } else {
                    self._fadeRaf = null;
                    resolve();
                }
            };

            self._fadeRaf = requestAnimationFrame(step);
        });
    },

    _fadeVolume: async function (audio, from, to, durationMs, generation) {
        if (!audio) return;
        if (durationMs <= 0) {
            try { audio.volume = to; } catch (e) { }
            return;
        }

        var start = performance.now();
        var self = this;

        return new Promise(function (resolve) {
            self._cancelFade();

            var step = function (now) {
                if (generation !== self._generation || self._audio !== audio) {
                    resolve();
                    return;
                }

                var t = Math.min(1, (now - start) / durationMs);
                try { audio.volume = from + (to - from) * t; } catch (e) { }

                if (t < 1) {
                    self._fadeRaf = requestAnimationFrame(step);
                } else {
                    self._fadeRaf = null;
                    resolve();
                }
            };

            self._fadeRaf = requestAnimationFrame(step);
        });
    },

    _cancelFade: function () {
        if (this._fadeRaf !== null) {
            cancelAnimationFrame(this._fadeRaf);
            this._fadeRaf = null;
        }
    },

    _disposeAudio: function (audio, objectUrl) {
        if (audio) {
            try {
                audio.pause();
                audio.removeAttribute('src');
                audio.load();
            } catch (e) { }
        }
        this._revoke(objectUrl);
    },

    _revoke: function (objectUrl) {
        if (!objectUrl) return;
        try { URL.revokeObjectURL(objectUrl); } catch (e) { }
    }
};
