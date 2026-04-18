let audioState = {
    element: null,
    dotNetRef: null,
    crossfadeElement: null,
    crossfadeDuration: 0,
    crossfadeTimer: null,
    crossfadePending: false,
    stateDebounceTimer: null
};

function notifyPlaybackState(dotNetRef, state) {
    if (audioState.stateDebounceTimer) {
        clearTimeout(audioState.stateDebounceTimer);
        audioState.stateDebounceTimer = null;
    }

    if (state === 'playing') {
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', 'playing')
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
        return;
    }

    audioState.stateDebounceTimer = setTimeout(() => {
        audioState.stateDebounceTimer = null;
        dotNetRef.invokeMethodAsync('OnPlaybackStateChanged', state)
            .catch(e => console.error('OnPlaybackStateChanged failed', e));
    }, 150);
}

window.K7 = window.K7 || {};
window.K7.scrollIntoViewSmooth = function (el) {
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};
window.K7.scrollToElement = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

// MediaSession API
window.K7.updateMediaSession = function (title, artist, album, artworkUrl) {
    if (!('mediaSession' in navigator)) return;
    const metadata = { title: title || '', artist: artist || '', album: album || '' };
    if (artworkUrl) {
        metadata.artwork = [
            { src: artworkUrl, sizes: '512x512', type: 'image/jpeg' }
        ];
    }
    navigator.mediaSession.metadata = new MediaMetadata(metadata);
};

window.K7.setupMediaSessionActions = function (dotNetRef) {
    if (!('mediaSession' in navigator) || !dotNetRef) return;
    const ms = navigator.mediaSession;
    ms.setActionHandler('play', () => dotNetRef.invokeMethodAsync('OnMediaSessionPlay'));
    ms.setActionHandler('pause', () => dotNetRef.invokeMethodAsync('OnMediaSessionPause'));
    ms.setActionHandler('previoustrack', () => dotNetRef.invokeMethodAsync('OnMediaSessionPrevious'));
    ms.setActionHandler('nexttrack', () => dotNetRef.invokeMethodAsync('OnMediaSessionNext'));
    ms.setActionHandler('seekto', (details) => {
        if (details.seekTime !== undefined)
            dotNetRef.invokeMethodAsync('OnMediaSessionSeek', details.seekTime);
    });
};

window.K7.updateMediaSessionPosition = function (position, duration, playbackRate) {
    if (!('mediaSession' in navigator)) return;
    try {
        navigator.mediaSession.setPositionState({
            duration: duration || 0,
            playbackRate: playbackRate || 1,
            position: Math.min(position || 0, duration || 0)
        });
    } catch { }
};

// Global keyboard shortcuts
window.K7._keyboardDotNetRef = null;
window.K7.initKeyboardShortcuts = function (dotNetRef) {
    window.K7._keyboardDotNetRef = dotNetRef;
    if (!window.K7._keyboardAttached) {
        window.K7._keyboardAttached = true;
        document.addEventListener('keydown', window.K7._onKeyDown);
    }
};
window.K7.disposeKeyboardShortcuts = function () {
    window.K7._keyboardDotNetRef = null;
};
window.K7._onKeyDown = function (e) {
    const ref = window.K7._keyboardDotNetRef;
    if (!ref) return;
    const tag = (e.target.tagName || '').toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select' || e.target.isContentEditable) return;
    let action = null;
    if (e.code === 'Space' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'PlayPause';
    else if (e.code === 'ArrowRight' && !e.ctrlKey && !e.shiftKey) action = 'SeekForward';
    else if (e.code === 'ArrowLeft' && !e.ctrlKey && !e.shiftKey) action = 'SeekBackward';
    else if (e.code === 'ArrowRight' && e.ctrlKey) action = 'NextTrack';
    else if (e.code === 'ArrowLeft' && e.ctrlKey) action = 'PreviousTrack';
    else if ((e.code === 'KeyM') && !e.ctrlKey && !e.metaKey) action = 'ToggleMute';
    else if (e.code === 'ArrowUp' && !e.ctrlKey) action = 'VolumeUp';
    else if (e.code === 'ArrowDown' && !e.ctrlKey) action = 'VolumeDown';
    if (action) {
        e.preventDefault();
        ref.invokeMethodAsync('OnKeyboardAction', action)
            .catch(e => console.error('OnKeyboardAction failed', e));
    }
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

        // Pre-end crossfade detection
        if (audioState.crossfadeDuration > 0
            && !audioState.crossfadePending
            && !audioState.crossfadeTimer
            && isFinite(el.duration)
            && el.duration > 0) {
            const remaining = el.duration - el.currentTime;
            if (remaining <= audioState.crossfadeDuration && remaining > 0) {
                audioState.crossfadePending = true;
                dotNetRef.invokeMethodAsync('OnCrossfadeNeeded')
                    .catch(e => console.error('OnCrossfadeNeeded failed', e));
            }
        }
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
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('playing', () => {
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('pause', () => {
        notifyPlaybackState(dotNetRef, 'paused');
    });

    el.addEventListener('waiting', () => {
        notifyPlaybackState(dotNetRef, 'buffering');
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

    audioState.crossfadePending = false;

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

window.audioStartCrossfade = function (nextSrc, nextMimeType, fadeDuration) {
    const duration = fadeDuration !== undefined && fadeDuration > 0 ? fadeDuration : audioState.crossfadeDuration;
    if (duration <= 0 || !audioState.element) {
        // No crossfade - just change source directly
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
            audioState.crossfadePending = false;

            currentEl.pause();
            currentEl.src = '';

            // Swap: next becomes current
            nextEl.volume = startVolume;
            audioState.element = nextEl;
            audioState.crossfadeElement = null;

            // Re-attach events to new element
            attachEventsToElement(nextEl, audioState.dotNetRef);

            // New element is already playing - notify .NET
            if (!nextEl.paused && audioState.dotNetRef) {
                notifyPlaybackState(audioState.dotNetRef, 'playing');
            }
        }
    }, stepMs);
};

function attachEventsToElement(el, dotNetRef) {
    if (!dotNetRef) return;

    el.addEventListener('timeupdate', () => {
        dotNetRef.invokeMethodAsync('OnTimeUpdated', el.currentTime)
            .catch(e => console.error('OnTimeUpdated failed', e));

        // Pre-end crossfade detection
        if (audioState.crossfadeDuration > 0
            && !audioState.crossfadePending
            && !audioState.crossfadeTimer
            && isFinite(el.duration)
            && el.duration > 0) {
            const remaining = el.duration - el.currentTime;
            if (remaining <= audioState.crossfadeDuration && remaining > 0) {
                audioState.crossfadePending = true;
                dotNetRef.invokeMethodAsync('OnCrossfadeNeeded')
                    .catch(e => console.error('OnCrossfadeNeeded failed', e));
            }
        }
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
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('playing', () => {
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('pause', () => {
        notifyPlaybackState(dotNetRef, 'paused');
    });

    el.addEventListener('waiting', () => {
        notifyPlaybackState(dotNetRef, 'buffering');
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
