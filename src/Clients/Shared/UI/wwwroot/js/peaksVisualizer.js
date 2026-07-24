window.K7 = window.K7 || {};

// Peaks-driven visualizer for native MAUI playback (no Web Audio AnalyserNode).
// Bars pulse from stored waveform peaks + a time phase so the cover feels alive.
window.K7.Visualizer = window.K7.Visualizer || {
    _animId: null,
    _canvas: null,
    _ctx: null,
    _peaks: null,
    _getProgress: null,
    _progress: 0,

    start: function (canvasEl, peaks) {
        this.stop();
        this._canvas = canvasEl;
        this._peaks = Array.isArray(peaks) && peaks.length ? peaks : null;
        this._progress = 0;
        if (!canvasEl) return;
        this._ctx = canvasEl.getContext('2d');
        this._loop();
    },

    stop: function () {
        if (this._animId) {
            cancelAnimationFrame(this._animId);
            this._animId = null;
        }
        this._canvas = null;
        this._ctx = null;
        this._peaks = null;
        this._progress = 0;
    },

    setPeaks: function (peaks) {
        this._peaks = Array.isArray(peaks) && peaks.length ? peaks : null;
    },

    setProgress: function (progress) {
        this._progress = typeof progress === 'number' ? progress : 0;
    },

    _loop: function () {
        try {
            if (!this._canvas || !this._ctx) return;

            const ctx = this._ctx;
            const canvas = this._canvas;
            const clientW = canvas.clientWidth || 0;
            const clientH = canvas.clientHeight || 0;
            if (clientW <= 0 || clientH <= 0) {
                this._animId = requestAnimationFrame(() => this._loop());
                return;
            }

            const w = canvas.width = clientW * (window.devicePixelRatio || 1);
            const h = canvas.height = clientH * (window.devicePixelRatio || 1);
            ctx.clearRect(0, 0, w, h);

            const barCount = 64;
            const barWidth = w / barCount;
            const gap = 2;
            const t = performance.now() / 1000;
            const progress = this._progress || 0;
            const peaks = this._peaks;

            for (let i = 0; i < barCount; i++) {
                let value;
                if (peaks && peaks.length) {
                    const idx = Math.floor((i / barCount) * peaks.length);
                    const base = Math.max(0.05, Math.min(1, peaks[idx] || 0));
                    const pulse = 0.55 + 0.45 * Math.sin(t * 6 + i * 0.35 + progress * 20);
                    value = base * pulse;
                } else {
                    value = 0.2 + 0.35 * Math.abs(Math.sin(t * 4 + i * 0.4));
                }

                const barHeight = value * h * 0.8;
                const x = i * barWidth;
                const y = h - barHeight;
                ctx.fillStyle = 'rgba(255, 255, 255, ' + (0.4 + value * 0.6) + ')';
                ctx.fillRect(x + gap / 2, y, barWidth - gap, barHeight);
            }

            this._animId = requestAnimationFrame(() => this._loop());
        } catch (e) {
            this.stop();
        }
    }
};
