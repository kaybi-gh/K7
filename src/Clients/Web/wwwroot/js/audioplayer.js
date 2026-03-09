let audioState = {
    element: null,
    dotNetRef: null,
    crossfadeElement: null,
    crossfadeDuration: 0,
    crossfadeTimer: null
};

window.initAudioPlayer = function (dotNetRef) {
    if (audioState.element) {
        disposeAudioPlayer();
    }

    const el = new Audio();
    el.preload = 'auto';

    audioState.element = el;
    audioState.dotNetRef = dotNetRef;

    el.addEventListener('timeupdate', () => {
        dotNetRef.invokeMethodAsync('OnTimeUpdated', el.currentTime)
            .catch(e => console.error('OnTimeUpdated failed', e));
    });

    el.addEventListener('durationchange', () => {
        if (isFinite(el.duration)) {
            dotNetRef.invokeMethodAsync('OnDurationChanged', el.duration)
                .catch(e => console.error('OnDurationChanged failed', e));
        }
    });

    el.addEventListener('progress', () => {
        const buffered = el.buffered;
        let bufferedEnd = 0;
        if (buffered && buffered.length > 0) {
            bufferedEnd = buffered.end(buffered.length - 1);
        }
        dotNetRef.invokeMethodAsync('OnBufferedUpdated', bufferedEnd)
            .catch(e => console.error('OnBufferedUpdated failed', e));
    });

    el.addEventListener('volumechange', () => {
        dotNetRef.invokeMethodAsync('OnVolumeChanged', el.volume, el.muted)
            .catch(e => console.error('OnVolumeChanged failed', e));
    });

    el.addEventListener('play', () => {
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', 'playing')
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
    });

    el.addEventListener('pause', () => {
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', 'paused')
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
    });

    el.addEventListener('waiting', () => {
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', 'buffering')
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
    });

    el.addEventListener('ended', () => {
        dotNetRef.invokeMethodAsync('OnTrackEnded')
            .catch(e => console.error('OnTrackEnded failed', e));
    });

    el.addEventListener('error', () => {
        const code = el.error ? el.error.code : -1;
        const msg = el.error ? el.error.message : 'unknown';
        console.error('Audio error', code, msg);
    });
};

window.disposeAudioPlayer = function () {
    if (audioState.crossfadeTimer) {
        clearInterval(audioState.crossfadeTimer);
        audioState.crossfadeTimer = null;
    }
    if (audioState.crossfadeElement) {
        audioState.crossfadeElement.pause();
        audioState.crossfadeElement.src = '';
        audioState.crossfadeElement = null;
    }
    if (audioState.element) {
        audioState.element.pause();
        audioState.element.src = '';
        audioState.element = null;
    }
    audioState.dotNetRef = null;
};

window.audioPlay = function () {
    const el = audioState.element;
    if (!el) return;
    const promise = el.play();
    if (promise) {
        promise.catch(e => console.warn('Audio play prevented', e));
    }
};

window.audioPause = function () {
    audioState.element?.pause();
};

window.audioStop = function () {
    const el = audioState.element;
    if (!el) return;
    el.pause();
    el.currentTime = 0;
};

window.audioSeek = function (seconds) {
    const el = audioState.element;
    if (el && isFinite(seconds)) {
        el.currentTime = seconds;
    }
};

window.audioSetVolume = function (volume) {
    if (audioState.element) {
        audioState.element.volume = Math.max(0, Math.min(1, volume));
    }
};

window.audioSetMuted = function (muted) {
    if (audioState.element) {
        audioState.element.muted = muted;
    }
};

window.audioChangeSource = function (src, mimeType) {
    const el = audioState.element;
    if (!el) return;

    el.src = src;
    el.load();

    const promise = el.play();
    if (promise) {
        promise.catch(e => console.warn('Audio play prevented after source change', e));
    }
};

window.audioSetCrossfadeDuration = function (seconds) {
    audioState.crossfadeDuration = seconds;
};

window.audioStartCrossfade = function (nextSrc, nextMimeType) {
    const duration = audioState.crossfadeDuration;
    if (duration <= 0 || !audioState.element) {
        // No crossfade — just change source directly
        audioChangeSource(nextSrc, nextMimeType);
        return;
    }

    const currentEl = audioState.element;
    const nextEl = new Audio();
    nextEl.preload = 'auto';
    nextEl.volume = 0;
    nextEl.src = nextSrc;

    audioState.crossfadeElement = nextEl;

    const stepMs = 50;
    const steps = (duration * 1000) / stepMs;
    let step = 0;
    const startVolume = currentEl.volume;

    nextEl.play().catch(e => console.warn('Crossfade next play prevented', e));

    audioState.crossfadeTimer = setInterval(() => {
        step++;
        const ratio = step / steps;

        currentEl.volume = Math.max(0, startVolume * (1 - ratio));
        nextEl.volume = Math.min(startVolume, startVolume * ratio);

        if (step >= steps) {
            clearInterval(audioState.crossfadeTimer);
            audioState.crossfadeTimer = null;

            currentEl.pause();
            currentEl.src = '';

            // Swap: next becomes current
            nextEl.volume = startVolume;
            audioState.element = nextEl;
            audioState.crossfadeElement = null;

            // Re-attach events to new element
            attachEventsToElement(nextEl, audioState.dotNetRef);
        }
    }, stepMs);
};

function attachEventsToElement(el, dotNetRef) {
    if (!dotNetRef) return;

    el.addEventListener('timeupdate', () => {
        dotNetRef.invokeMethodAsync('OnTimeUpdated', el.currentTime)
            .catch(e => console.error('OnTimeUpdated failed', e));
    });

    el.addEventListener('durationchange', () => {
        if (isFinite(el.duration)) {
            dotNetRef.invokeMethodAsync('OnDurationChanged', el.duration)
                .catch(e => console.error('OnDurationChanged failed', e));
        }
    });

    el.addEventListener('progress', () => {
        const buffered = el.buffered;
        let bufferedEnd = 0;
        if (buffered && buffered.length > 0) {
            bufferedEnd = buffered.end(buffered.length - 1);
        }
        dotNetRef.invokeMethodAsync('OnBufferedUpdated', bufferedEnd)
            .catch(e => console.error('OnBufferedUpdated failed', e));
    });

    el.addEventListener('volumechange', () => {
        dotNetRef.invokeMethodAsync('OnVolumeChanged', el.volume, el.muted)
            .catch(e => console.error('OnVolumeChanged failed', e));
    });

    el.addEventListener('play', () => {
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', 'playing')
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
    });

    el.addEventListener('pause', () => {
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', 'paused')
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
    });

    el.addEventListener('waiting', () => {
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', 'buffering')
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
    });

    el.addEventListener('ended', () => {
        dotNetRef.invokeMethodAsync('OnTrackEnded')
            .catch(e => console.error('OnTrackEnded failed', e));
    });

    el.addEventListener('error', () => {
        const code = el.error ? el.error.code : -1;
        const msg = el.error ? el.error.message : 'unknown';
        console.error('Audio error', code, msg);
    });
}
