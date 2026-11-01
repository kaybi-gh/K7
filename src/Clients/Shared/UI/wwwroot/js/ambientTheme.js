window.K7 = window.K7 || {};
window.K7.AmbientTheme = {
    _audio: null,
    _objectUrl: null,
    _sourceUrl: null,
    _targetVolume: 0.25,
    _fadeRaf: null,
    _generation: 0,
    _pausedByHide: false,
    _visibilityBound: false,

    playBytes: async function (bytes, volume, crossfadeSeconds, sourceUrl, dotNetRef) {
        if (!bytes || !bytes.length) return false;

        return await this._play(function () {
            var data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
            var objectUrl = URL.createObjectURL(new Blob([data], { type: 'audio/mpeg' }));
            return { src: objectUrl, objectUrl: objectUrl };
        }, volume, crossfadeSeconds, sourceUrl, dotNetRef);
    },

    // Streams from a URL the audio element can load with the current authentication (same-origin
    // cookie on the web, native auth proxy on Android): no audio bytes cross the .NET bridge.
    // Resolves false when the source cannot be played so the caller can fall back to bytes.
    playUrl: async function (playableUrl, volume, crossfadeSeconds, sourceUrl, dotNetRef) {
        if (!playableUrl) return false;

        if (!this._isCrossOrigin(playableUrl)) {
            return await this._play(function () {
                return { src: playableUrl, objectUrl: null };
            }, volume, crossfadeSeconds, sourceUrl, dotNetRef);
        }

        // Android auth proxy (other origin): the WebView media stack rejects intercepted
        // responses (MEDIA_ERR_SRC_NOT_SUPPORTED on the TV box, 2026-11-01) while fetch() reads
        // them. The bytes go WebView network layer to blob, never through .NET interop.
        var blob;
        try {
            var response = await fetch(playableUrl);
            if (!response.ok) return false;
            blob = await response.blob();
        } catch (e) {
            if (window.K7 && K7.PerfTrace && K7.PerfTrace.report)
                K7.PerfTrace.report('theme fetch failed ' + (e && e.name));
            return false;
        }

        if (!blob || !blob.size) return false;

        return await this._play(function () {
            var typed = blob.type ? blob : new Blob([blob], { type: 'audio/mpeg' });
            var objectUrl = URL.createObjectURL(typed);
            return { src: objectUrl, objectUrl: objectUrl };
        }, volume, crossfadeSeconds, sourceUrl, dotNetRef);
    },

    _isCrossOrigin: function (url) {
        try {
            return new URL(url, window.location.href).origin !== window.location.origin;
        } catch (e) {
            return false;
        }
    },

    _play: async function (createSource, volume, crossfadeSeconds, sourceUrl, dotNetRef) {
        this._bindVisibility();

        var targetVolume = typeof volume === 'number' ? Math.min(1, Math.max(0, volume)) : 0.25;
        var crossfadeMs = Math.max(0, (typeof crossfadeSeconds === 'number' ? crossfadeSeconds : 1.2) * 1000);
        var generation = ++this._generation;
        var identity = sourceUrl || '';

        // Keep a visibility-paused instance. Do not restart from 0.
        if (identity && this._sourceUrl === identity && this._audio
            && (!this._audio.paused || this._pausedByHide))
            return true;

        var source = createSource();
        var objectUrl = source.objectUrl;

        var next = new Audio(source.src);
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
            if (playPromise && typeof playPromise.catch === 'function') {
                // A URL source that never starts must not hold the byte fallback for seconds.
                await (objectUrl ? playPromise : Promise.race([playPromise, new Promise(function (_, reject) {
                    setTimeout(function () {
                        var timeout = new Error('theme url did not start');
                        timeout.name = 'TimeoutError';
                        reject(timeout);
                    }, 2500);
                })]));
            }
        } catch (e) {
            console.warn('AmbientTheme play failed', e);
            if (window.K7 && K7.PerfTrace && K7.PerfTrace.report) {
                K7.PerfTrace.report('theme play failed ' + (e && e.name) + ' mediaError='
                    + (next.error ? next.error.code : 'none') + ' src=' + (objectUrl ? 'blob' : 'url'));
            }
            try { next.removeAttribute('src'); next.load(); } catch (err) { }
            this._revoke(objectUrl);
            return false;
        }

        if (generation !== this._generation) {
            try { next.pause(); } catch (e) { }
            this._revoke(objectUrl);
            return true;
        }

        var previous = this._audio;
        var previousObjectUrl = this._objectUrl;
        this._audio = next;
        this._objectUrl = objectUrl;
        this._sourceUrl = identity;
        this._targetVolume = targetVolume;
        this._dotNetRef = dotNetRef || null;
        this._pausedByHide = false;

        if (typeof document !== 'undefined' && document.hidden) {
            this._pauseForHide();
            next.volume = targetVolume;
            return true;
        }

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

        return true;
    },

    fadeOut: async function (durationSeconds) {
        var audio = this._audio;
        if (!audio) return;

        this._pausedByHide = false;

        // Hidden documents freeze rAF. A fade would leave audio running.
        if (typeof document !== 'undefined' && document.hidden) {
            this.stop();
            return;
        }

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
        this._pausedByHide = false;

        var audio = this._audio;
        var objectUrl = this._objectUrl;
        this._audio = null;
        this._objectUrl = null;
        this._sourceUrl = null;
        this._disposeAudio(audio, objectUrl);
    },

    _bindVisibility: function () {
        if (this._visibilityBound)
            return;

        this._visibilityBound = true;
        var self = this;
        document.addEventListener('visibilitychange', function () {
            if (document.hidden)
                self._pauseForHide();
            else
                self._resumeAfterHide();
        });
        window.addEventListener('pagehide', function () { self._pauseForHide(); });
        window.addEventListener('pageshow', function () { self._resumeAfterHide(); });
    },

    _pauseForHide: function () {
        var audio = this._audio;
        if (!audio || audio.paused)
            return;

        this._pausedByHide = true;
        try { audio.pause(); } catch (e) { }
    },

    _resumeAfterHide: function () {
        if (!this._pausedByHide)
            return;

        this._pausedByHide = false;
        if (this._fadeRaf !== null)
            return;

        var audio = this._audio;
        if (!audio)
            return;

        var playPromise = audio.play();
        if (playPromise && typeof playPromise.catch === 'function')
            playPromise.catch(function () { });
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
