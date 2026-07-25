let audioState = {
    element: null,
    dotNetRef: null,
    crossfadeElement: null,
    crossfadeDuration: 0,
    crossfadeTimer: null,
    crossfadePending: false,
    crossfadeActive: false,
    stateDebounceTimer: null,
    // Generation counter: incremented whenever the active element changes
    generation: 0,
    // Gapless prebuffer
    gaplessNextElement: null,
    gaplessNextSource: null,
    gaplessPrebuffered: false,
    // Web Audio API nodes
    audioContext: null,
    sourceNode: null,
    fadeGainNode: null,
    crossfadeSourceNode: null,
    gainNode: null,
    loudnessGainNode: null,
    eqFilters: [],
    limiterNode: null,
    analyserNode: null,
    // Loudness settings
    loudnessEnabled: false,
    loudnessTargetLufs: -18,
    loudnessPreampDb: 0,
    limiterEnabled: true,
    trackLoudnessLufs: null,
    trackReplayGain: null,
    // EQ settings
    eqEnabled: false,
    eqBands: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    // Seek suppression
    lastSeekTime: 0,
    // blob: URLs from Windows stream bridge (revoke on dispose / replace)
    objectUrls: [],
    // User pause while a dual-stream crossfade is armed/running
    playbackPaused: false,
    crossfadeFade: null,
    _resumeWaiters: []
};

function trackObjectUrl(url) {
    if (url && url.indexOf('blob:') === 0)
        audioState.objectUrls.push(url);
}

function revokeObjectUrls(exceptUrl) {
    const kept = [];
    for (const url of audioState.objectUrls) {
        if (exceptUrl && url === exceptUrl) {
            kept.push(url);
            continue;
        }
        try { URL.revokeObjectURL(url); } catch { /* ignore */ }
    }
    audioState.objectUrls = kept;
}

async function resolvePlayableSrc(src, mimeType) {
    if (window.K7 && typeof K7.resolveAudioPlayableUrl === 'function') {
        try {
            const resolved = await K7.resolveAudioPlayableUrl(src, mimeType);
            if (resolved && resolved !== src)
                trackObjectUrl(resolved);
            return resolved || src;
        } catch (e) {
            console.error('resolveAudioPlayableUrl failed', e);
            throw e;
        }
    }
    return src;
}

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
window.K7.shareOrCopy = async function (text) {
    if (navigator.share) {
        try {
            await navigator.share({ text });
            return true;
        } catch (e) {
            if (e.name !== 'AbortError') console.warn('Share failed', e);
        }
    }
    try {
        await navigator.clipboard.writeText(text);
    } catch (e) {
        console.warn('Clipboard write failed', e);
    }
    return false;
};
window.K7.scrollIntoViewSmooth = function (el, container) {
    if (!el) return;
    // Prefer scrolling only the lyrics container so nested overflow parents
    // (fullscreen shell) do not move and create a double-scrollbar fight.
    if (container) {
        var cRect = container.getBoundingClientRect();
        var eRect = el.getBoundingClientRect();
        var offset = (eRect.top + eRect.height / 2) - (cRect.top + cRect.height / 2);
        container.scrollBy({ top: offset, behavior: 'smooth' });
        return;
    }
    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};
window.K7.focusIfContained = function (el, container) {
    if (!el || !container) return;
    var active = document.activeElement;
    if (active && container.contains(active))
        el.focus({ preventScroll: true });
};
window.K7.scrollToElement = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

window.K7.unlockAudio = function () {
    // Resume AudioContext created outside a user gesture
    if (audioState.audioContext && audioState.audioContext.state === 'suspended') {
        audioState.audioContext.resume();
    }
    // Prime the audio element with a silent play+pause so future play() calls succeed.
    // Keep the silent data URI (do not clear src) to avoid MEDIA_ELEMENT_ERROR: Empty src.
    var el = audioState.element;
    if (el && el.paused && !el.src) {
        el.src = 'data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAQB8AAIA+AAACABAAZGF0YQAAAAA=';
        el.play().then(function () { el.pause(); }).catch(function () { /* ignore autoplay block */ });
    }
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

    // Only intercept arrow/volume keys when focus is NOT on a spatial-nav focusable element
    // outside the audio player bar. This prevents stealing arrows from page navigation.
    const isArrowOrVolume = e.code === 'ArrowRight' || e.code === 'ArrowLeft' || e.code === 'ArrowUp' || e.code === 'ArrowDown';
    if (isArrowOrVolume) {
        const active = document.activeElement;
        // Allow if focus is on body (nothing focused) or inside the audio bottom bar
        const inAudioBar = active && active.closest('.audio-bottom-bar, .mini-music-player, .fullscreen-music-player');
        if (active && active !== document.body && !inAudioBar) return;
    }

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

// EQ band center frequencies (Hz)
const EQ_FREQUENCIES = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

function initAudioGraph(element) {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    audioState.audioContext = ctx;

    // Source: MediaElementSource from the <audio> element
    const source = ctx.createMediaElementSource(element);
    audioState.sourceNode = source;

    // Fade gain node (used for crossfade volume control, not visible to user)
    const fadeGain = ctx.createGain();
    fadeGain.gain.value = 1.0;
    audioState.fadeGainNode = fadeGain;

    // Loudness gain node (for ReplayGain / LUFS normalization)
    const loudnessGain = ctx.createGain();
    loudnessGain.gain.value = 1.0;
    audioState.loudnessGainNode = loudnessGain;

    // 10-band parametric EQ
    const eqFilters = EQ_FREQUENCIES.map(freq => {
        const filter = ctx.createBiquadFilter();
        filter.type = 'peaking';
        filter.frequency.value = freq;
        filter.Q.value = 1.4; // moderate bandwidth
        filter.gain.value = 0;
        return filter;
    });
    audioState.eqFilters = eqFilters;

    // Limiter (DynamicsCompressor configured as a brick-wall limiter)
    const limiter = ctx.createDynamicsCompressor();
    limiter.threshold.value = -1.0; // dBFS
    limiter.knee.value = 0;
    limiter.ratio.value = 20;
    limiter.attack.value = 0.003;
    limiter.release.value = 0.01;
    audioState.limiterNode = limiter;

    // Master volume gain (user volume control)
    const masterGain = ctx.createGain();
    masterGain.gain.value = 1.0;
    audioState.gainNode = masterGain;

    // Analyser (for future visualizer)
    const analyser = ctx.createAnalyser();
    analyser.fftSize = 256;
    audioState.analyserNode = analyser;

    // Connect signal chain: source -> fadeGain -> loudness -> EQ -> limiter -> master -> analyser -> destination
    source.connect(fadeGain);
    fadeGain.connect(loudnessGain);

    // Chain EQ filters
    let prevNode = loudnessGain;
    for (const filter of eqFilters) {
        prevNode.connect(filter);
        prevNode = filter;
    }

    prevNode.connect(limiter);
    limiter.connect(masterGain);
    masterGain.connect(analyser);
    analyser.connect(ctx.destination);

    applyEqBands();
    computeLoudnessGain();
    applyLimiter();
}

function resumeAudioContext() {
    if (audioState.audioContext && audioState.audioContext.state === 'suspended') {
        audioState.audioContext.resume();
    }
}

function computeLoudnessGain() {
    if (!audioState.loudnessEnabled || !audioState.loudnessGainNode) {
        if (audioState.loudnessGainNode) {
            audioState.loudnessGainNode.gain.value = 1.0;
        }
        return;
    }

    let gainDb = 0;
    if (audioState.trackLoudnessLufs !== null) {
        gainDb = audioState.loudnessTargetLufs - audioState.trackLoudnessLufs + audioState.loudnessPreampDb;
    } else if (audioState.trackReplayGain !== null) {
        gainDb = audioState.trackReplayGain + audioState.loudnessPreampDb;
    }

    // Convert dB to linear gain, clamped to prevent extreme values
    gainDb = Math.max(-20, Math.min(20, gainDb));
    const linearGain = Math.pow(10, gainDb / 20);
    audioState.loudnessGainNode.gain.value = linearGain;
}

function applyEqBands() {
    const filters = audioState.eqFilters;
    const bands = audioState.eqBands;
    if (!filters || filters.length === 0) return;

    for (let i = 0; i < filters.length; i++) {
        filters[i].gain.value = audioState.eqEnabled ? (bands[i] || 0) : 0;
    }
}

function applyLimiter() {
    if (!audioState.limiterNode) return;
    // When disabled, set threshold very high so it never activates
    audioState.limiterNode.threshold.value = audioState.limiterEnabled ? -1.0 : 0;
}

// Public APIs for .NET interop

window.audioSetLoudness = function (enabled, targetLufs, preampDb, limiterEnabled) {
    audioState.loudnessEnabled = enabled;
    audioState.loudnessTargetLufs = targetLufs;
    audioState.loudnessPreampDb = preampDb;
    audioState.limiterEnabled = limiterEnabled;
    computeLoudnessGain();
    applyLimiter();
};

window.audioSetTrackLoudness = function (loudnessLufs, replayGain) {
    audioState.trackLoudnessLufs = loudnessLufs;
    audioState.trackReplayGain = replayGain;
    computeLoudnessGain();
};

window.audioSetEq = function (enabled, bands) {
    audioState.eqEnabled = enabled;
    if (bands && bands.length === 10) {
        audioState.eqBands = bands;
    }
    applyEqBands();
};

window.audioFadeOut = function (durationSec) {
    return new Promise((resolve) => {
        const ctx = audioState.audioContext;
        const gain = audioState.fadeGainNode;
        if (!ctx || !gain) {
            resolve();
            return;
        }

        const seconds = Math.max(0.05, Number(durationSec) || 0);
        const now = ctx.currentTime;
        try {
            gain.gain.cancelScheduledValues(now);
            gain.gain.setValueAtTime(gain.gain.value, now);
            gain.gain.linearRampToValueAtTime(0, now + seconds);
        } catch (e) {
            console.warn('audioFadeOut failed', e);
            resolve();
            return;
        }

        setTimeout(resolve, Math.ceil(seconds * 1000));
    });
};

window.audioResetFadeGain = function () {
    const gain = audioState.fadeGainNode;
    if (!gain) return;
    try {
        const ctx = audioState.audioContext;
        const now = ctx ? ctx.currentTime : 0;
        gain.gain.cancelScheduledValues(now);
        gain.gain.setValueAtTime(1, now);
    } catch (e) {
        gain.gain.value = 1;
    }
};

window.audioGetAnalyserData = function () {
    if (!audioState.analyserNode) return null;
    const data = new Uint8Array(audioState.analyserNode.frequencyBinCount);
    audioState.analyserNode.getByteFrequencyData(data);
    return Array.from(data);
};

window.initAudioPlayer = function (dotNetRef) {
    if (audioState.element) {
        disposeAudioPlayer();
    }

    const el = new Audio();
    el.preload = 'auto';
    el.crossOrigin = 'anonymous';

    audioState.element = el;
    audioState.dotNetRef = dotNetRef;

    // Initialize Web Audio API context and signal chain
    initAudioGraph(el);

    const initGen = audioState.generation;

    el.addEventListener('timeupdate', () => {
        // Ignore if this element is no longer the active one
        if (initGen !== audioState.generation) return;

        if (!audioState.crossfadeActive) {
            dotNetRef.invokeMethodAsync('OnTimeUpdated', el.currentTime)
                .catch(e => console.error('OnTimeUpdated failed', e));
        }

        if (audioState.crossfadeActive || audioState.crossfadePending)
            return;

        // Pre-end crossfade detection
        if (audioState.crossfadeDuration > 0
            && !audioState.crossfadeTimer
            && (Date.now() - audioState.lastSeekTime) > 1500
            && isFinite(el.duration)
            && el.duration > 0) {
            const remaining = el.duration - el.currentTime;
            const armWindow = audioState.crossfadeDuration + (audioState.gaplessNextElement ? 2 : 0);
            if (remaining <= armWindow && remaining > 0) {
                audioState.crossfadePending = true;
                dotNetRef.invokeMethodAsync('OnCrossfadeNeeded')
                    .catch(e => console.error('OnCrossfadeNeeded failed', e));
            }
        }

        // Early prepare of next track (gapless + crossfade)
        maybeRequestGaplessPrebuffer(el, dotNetRef);
    });

    el.addEventListener('durationchange', () => {
        if (initGen !== audioState.generation) return;
        if (isFinite(el.duration)) {
            dotNetRef.invokeMethodAsync('OnDurationChanged', el.duration)
                .catch(e => console.error('OnDurationChanged failed', e));
        }
    });

    el.addEventListener('progress', () => {
        if (initGen !== audioState.generation) return;
        const buffered = el.buffered;
        let bufferedEnd = 0;
        if (buffered && buffered.length > 0) {
            bufferedEnd = buffered.end(buffered.length - 1);
        }
        dotNetRef.invokeMethodAsync('OnBufferedUpdated', bufferedEnd)
            .catch(e => console.error('OnBufferedUpdated failed', e));
    });

    el.addEventListener('volumechange', () => {
        if (initGen !== audioState.generation) return;
        dotNetRef.invokeMethodAsync('OnVolumeChanged', el.volume, el.muted)
            .catch(e => console.error('OnVolumeChanged failed', e));
    });

    el.addEventListener('play', () => {
        if (initGen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('playing', () => {
        if (initGen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('pause', () => {
        if (initGen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'paused');
    });

    el.addEventListener('waiting', () => {
        if (initGen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'buffering');
    });

    el.addEventListener('ended', () => {
        if (initGen !== audioState.generation) return;
        dotNetRef.invokeMethodAsync('OnTrackEnded')
            .catch(e => console.error('OnTrackEnded failed', e));
    });

    el.addEventListener('error', () => {
        if (initGen !== audioState.generation) return;
        // unlockAudio / dispose may clear or leave an empty src; ignore those.
        if (!el.currentSrc && !el.src)
            return;
        const code = el.error ? el.error.code : -1;
        const msg = el.error ? el.error.message : 'unknown';
        console.error('Audio error', code, msg);
    });
};

function cancelCrossfadeFade() {
    pauseCrossfadeFade();
    const fade = audioState.crossfadeFade;
    if (fade) {
        audioState.crossfadeFade = null;
        try { fade.resolve(false); } catch { /* ignore */ }
    }
    if (audioState.crossfadeTimer) {
        clearInterval(audioState.crossfadeTimer);
        audioState.crossfadeTimer = null;
    }
}

window.disposeAudioPlayer = function () {
    cancelCrossfadeFade();
    audioState.playbackPaused = false;
    audioState._resumeWaiters = [];
    if (audioState.crossfadeSourceNode) {
        audioState.crossfadeSourceNode.disconnect();
        audioState.crossfadeSourceNode = null;
    }
    if (audioState.crossfadeElement) {
        audioState.crossfadeElement.pause();
        audioState.crossfadeElement.src = '';
        audioState.crossfadeElement = null;
    }
    if (audioState.sourceNode) {
        audioState.sourceNode.disconnect();
        audioState.sourceNode = null;
    }
    if (audioState.element) {
        audioState.element.pause();
        audioState.element.src = '';
        audioState.element = null;
    }
    if (audioState.audioContext) {
        audioState.audioContext.close().catch(() => {});
        audioState.audioContext = null;
    }
    audioState.loudnessGainNode = null;
    audioState.fadeGainNode = null;
    audioState.gainNode = null;
    audioState.limiterNode = null;
    audioState.analyserNode = null;
    audioState.eqFilters = [];
    audioState.crossfadeActive = false;
    audioState.crossfadePending = false;
    audioState.dotNetRef = null;
    revokeObjectUrls();
};

window.audioPlay = function () {
    audioState.playbackPaused = false;
    const waiters = audioState._resumeWaiters;
    audioState._resumeWaiters = [];
    for (const resolve of waiters)
        resolve();

    resumeAudioContext();
    const el = audioState.element;
    if (el) {
        const promise = el.play();
        if (promise)
            promise.catch(e => console.warn('Audio play prevented', e));
    }

    const nextEl = audioState.crossfadeElement;
    if (nextEl && audioState.crossfadeActive) {
        nextEl.play().catch(e => console.warn('Crossfade play prevented', e));
        resumeCrossfadeFade();
    }
};

window.audioPause = function () {
    audioState.playbackPaused = true;
    audioState.element?.pause();
    audioState.crossfadeElement?.pause();
    pauseCrossfadeFade();
};

window.audioStop = function () {
    audioState.playbackPaused = true;
    cancelCrossfadeFade();
    const el = audioState.element;
    if (el) {
        el.pause();
        el.currentTime = 0;
    }
    if (audioState.crossfadeElement) {
        try {
            audioState.crossfadeElement.pause();
            audioState.crossfadeElement.src = '';
        } catch { /* ignore */ }
        audioState.crossfadeElement = null;
    }
    audioState.crossfadeActive = false;
    audioState.crossfadePending = false;
    if (audioState.fadeGainNode)
        audioState.fadeGainNode.gain.value = 1.0;
};

window.audioSeek = function (seconds) {
    const el = audioState.element;
    if (el && isFinite(seconds)) {
        audioState.lastSeekTime = Date.now();
        audioState.crossfadePending = false;
        el.currentTime = seconds;
    }
};

window.audioSetVolume = function (volume) {
    const v = Math.max(0, Math.min(1, volume));
    if (audioState.gainNode) {
        audioState.gainNode.gain.value = v;
    }
    if (audioState.element) {
        audioState.element.volume = 1.0; // always 1.0 when using Web Audio gain
    }
};

window.audioSetMuted = function (muted) {
    if (audioState.element) {
        audioState.element.muted = muted;
    }
    if (audioState.crossfadeElement) {
        audioState.crossfadeElement.muted = muted;
    }
};

window.audioChangeSource = async function (src, mimeType) {
    const el = audioState.element;
    if (!el) return;

    resumeAudioContext();
    cancelCrossfadeFade();
    audioState.crossfadePending = false;
    audioState.crossfadeActive = false;

    if (audioState.crossfadeElement) {
        try {
            audioState.crossfadeElement.pause();
            audioState.crossfadeElement.src = '';
        } catch { /* ignore */ }
        audioState.crossfadeElement = null;
    }

    // Check if we have a prebuffered gapless element for this source
    if (audioState.gaplessPrebuffered
        && audioState.gaplessNextSource
        && audioState.gaplessNextSource.src === src) {
        audioGaplessSwitch();
        return;
    }

    // Discard stale prebuffer
    if (audioState.gaplessNextElement) {
        audioState.gaplessNextElement.src = '';
        audioState.gaplessNextElement = null;
        audioState.gaplessNextSource = null;
        audioState.gaplessPrebuffered = false;
    }

    // Reset fade gain in case a previous crossfade left it at 0
    if (audioState.fadeGainNode) {
        audioState.fadeGainNode.gain.value = 1.0;
    }

    let playableSrc;
    try {
        playableSrc = await resolvePlayableSrc(src, mimeType);
    } catch (e) {
        console.error('Audio source resolve failed', e);
        if (audioState.dotNetRef) {
            audioState.dotNetRef.invokeMethodAsync('OnPlaybackFailed')
                .catch(err => console.error('OnPlaybackFailed failed', err));
        }
        return;
    }

    const previousSrc = el.currentSrc || el.src;
    el.src = playableSrc;
    el.load();
    if (previousSrc && previousSrc !== playableSrc)
        revokeObjectUrls(playableSrc);

    el.addEventListener('canplay', function () {
        el.play().catch(() => {});
    }, { once: true });
};

window.audioSetCrossfadeDuration = function (seconds) {
    audioState.crossfadeDuration = seconds;
};

window.audioGaplessPrebuffer = async function (src, mimeType) {
    // Discard any previous prebuffer
    if (audioState.gaplessNextElement) {
        audioState.gaplessNextElement.pause();
        audioState.gaplessNextElement.src = '';
        audioState.gaplessNextElement = null;
        audioState.gaplessNextSource = null;
    }
    audioState.gaplessPrebuffered = false;

    let playableSrc;
    try {
        playableSrc = await resolvePlayableSrc(src, mimeType);
    } catch (e) {
        console.error('Gapless prebuffer resolve failed', e);
        return;
    }

    const nextEl = new Audio();
    nextEl.preload = 'auto';
    nextEl.crossOrigin = 'anonymous';
    nextEl.src = playableSrc;
    nextEl.load();
    audioState.gaplessNextElement = nextEl;
    // Keep the logical src for matching SourceChanged against the original stream URL.
    audioState.gaplessNextSource = { src, mimeType, playableSrc };

    nextEl.addEventListener('canplay', () => {
        audioState.gaplessPrebuffered = true;
    }, { once: true });
    nextEl.addEventListener('canplaythrough', () => {
        audioState.gaplessPrebuffered = true;
    }, { once: true });
};

function waitUntilRemaining(el, targetRemaining, timeoutMs) {
    return new Promise((resolve) => {
        const start = performance.now();
        const tick = async () => {
            if (audioState.playbackPaused) {
                await waitUntilNotPaused();
            }
            if (!el || !isFinite(el.duration) || el.duration <= 0) {
                resolve(0);
                return;
            }
            const remaining = el.duration - el.currentTime;
            if (remaining <= targetRemaining + 0.05 || performance.now() - start >= timeoutMs) {
                resolve(Math.max(0, remaining));
                return;
            }
            setTimeout(tick, 40);
        };
        tick();
    });
}

function waitUntilNotPaused() {
    if (!audioState.playbackPaused)
        return Promise.resolve();
    return new Promise((resolve) => {
        audioState._resumeWaiters.push(resolve);
    });
}

function pauseCrossfadeFade() {
    const fade = audioState.crossfadeFade;
    if (!fade || fade.paused)
        return;

    fade.paused = true;
    if (typeof fade.segmentStartPerf === 'number') {
        fade.elapsedMs = Math.min(
            fade.durationMs,
            fade.elapsedMs + (performance.now() - fade.segmentStartPerf));
        fade.segmentStartPerf = null;
    }

    if (audioState.crossfadeTimer) {
        clearInterval(audioState.crossfadeTimer);
        audioState.crossfadeTimer = null;
    }

    const ctx = audioState.audioContext;
    const fadeGain = fade.fadeGain;
    if (ctx && fadeGain) {
        const now = ctx.currentTime;
        const ratio = fade.durationMs > 0 ? fade.elapsedMs / fade.durationMs : 1;
        fadeGain.gain.cancelScheduledValues(now);
        fadeGain.gain.setValueAtTime(Math.cos(ratio * Math.PI / 2), now);
    }

    const nextEl = fade.nextEl;
    if (nextEl) {
        const ratio = fade.durationMs > 0 ? fade.elapsedMs / fade.durationMs : 1;
        nextEl.volume = Math.max(0, Math.min(1, Math.sin(ratio * Math.PI / 2) * fade.masterVolumeFn()));
    }
}

function resumeCrossfadeFade() {
    const fade = audioState.crossfadeFade;
    if (!fade || !fade.paused)
        return;

    fade.paused = false;
    const remainingMs = Math.max(0, fade.durationMs - fade.elapsedMs);
    if (remainingMs <= 16) {
        finishCrossfadeFade(fade);
        return;
    }

    runCrossfadeFadeSegment(fade, remainingMs);
}

function finishCrossfadeFade(fade) {
    if (audioState.crossfadeTimer) {
        clearInterval(audioState.crossfadeTimer);
        audioState.crossfadeTimer = null;
    }

    const nextEl = fade.nextEl;
    const fadeGain = fade.fadeGain;
    if (nextEl)
        nextEl.volume = Math.max(0, Math.min(1, fade.masterVolumeFn()));
    if (fadeGain)
        fadeGain.gain.value = 0;

    audioState.crossfadeFade = null;
    fade.resolve(true);
}

function runCrossfadeFadeSegment(fade, remainingMs) {
    const ctx = audioState.audioContext;
    const fadeGain = fade.fadeGain;
    const startRatio = fade.durationMs > 0 ? fade.elapsedMs / fade.durationMs : 0;
    fade.segmentStartPerf = performance.now();

    if (ctx && fadeGain) {
        const now = ctx.currentTime;
        fadeGain.gain.cancelScheduledValues(now);
        fadeGain.gain.setValueAtTime(Math.cos(startRatio * Math.PI / 2), now);
        const points = Math.max(10, Math.round((remainingMs / 1000) * 40));
        for (let i = 1; i <= points; i++) {
            const localRatio = i / points;
            const globalRatio = startRatio + localRatio * (1 - startRatio);
            fadeGain.gain.setValueAtTime(
                Math.cos(globalRatio * Math.PI / 2),
                now + localRatio * (remainingMs / 1000));
        }
    }

    if (audioState.crossfadeTimer) {
        clearInterval(audioState.crossfadeTimer);
        audioState.crossfadeTimer = null;
    }

    audioState.crossfadeTimer = setInterval(() => {
        if (fade.paused)
            return;

        const segmentElapsed = performance.now() - fade.segmentStartPerf;
        const elapsedMs = Math.min(fade.durationMs, fade.elapsedMs + segmentElapsed);
        const ratio = fade.durationMs > 0 ? elapsedMs / fade.durationMs : 1;

        if (!ctx || !fadeGain) {
            if (audioState.element)
                audioState.element.volume = Math.cos(ratio * Math.PI / 2) * fade.masterVolumeFn();
        }

        fade.nextEl.volume = Math.max(0, Math.min(1, Math.sin(ratio * Math.PI / 2) * fade.masterVolumeFn()));

        if (ratio >= 1)
            finishCrossfadeFade(fade);
    }, 25);
}

function scheduleEqualPowerFade(fadeGain, nextEl, effectiveDuration, masterVolumeFn) {
    return new Promise((resolve) => {
        if (audioState.crossfadeTimer) {
            clearInterval(audioState.crossfadeTimer);
            audioState.crossfadeTimer = null;
        }

        const fade = {
            nextEl,
            fadeGain,
            masterVolumeFn,
            durationMs: Math.max(50, effectiveDuration * 1000),
            elapsedMs: 0,
            segmentStartPerf: null,
            paused: false,
            resolve
        };
        audioState.crossfadeFade = fade;

        if (audioState.playbackPaused) {
            fade.paused = true;
            const ctx = audioState.audioContext;
            if (ctx && fadeGain) {
                const now = ctx.currentTime;
                fadeGain.gain.cancelScheduledValues(now);
                fadeGain.gain.setValueAtTime(1, now);
            }
            nextEl.volume = 0;
            return;
        }

        runCrossfadeFadeSegment(fade, fade.durationMs);
    });
}

function waitForCanPlay(el, timeoutMs) {
    return new Promise((resolve) => {
        if (el.readyState >= HTMLMediaElement.HAVE_FUTURE_DATA) {
            resolve(true);
            return;
        }

        let settled = false;
        const finish = (ok) => {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            el.removeEventListener('canplay', onReady);
            el.removeEventListener('canplaythrough', onReady);
            el.removeEventListener('error', onError);
            resolve(ok);
        };
        const onReady = () => finish(true);
        const onError = () => finish(false);
        const timer = setTimeout(() => {
            finish(el.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA);
        }, timeoutMs);

        el.addEventListener('canplay', onReady);
        el.addEventListener('canplaythrough', onReady);
        el.addEventListener('error', onError);
    });
}

function waitForMediaPlaying(el, timeoutMs) {
    return new Promise((resolve) => {
        let settled = false;
        const finish = (ok) => {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            el.removeEventListener('playing', onPlaying);
            el.removeEventListener('canplay', onCanPlay);
            el.removeEventListener('error', onError);
            resolve(ok);
        };
        const onPlaying = () => finish(true);
        const onCanPlay = () => {
            if (!el.paused && el.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA)
                finish(true);
        };
        const onError = () => finish(false);
        const timer = setTimeout(() => {
            finish(!el.paused && el.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA);
        }, timeoutMs);

        el.addEventListener('playing', onPlaying);
        el.addEventListener('canplay', onCanPlay);
        el.addEventListener('error', onError);

        if (!el.paused && el.readyState >= HTMLMediaElement.HAVE_FUTURE_DATA)
            finish(true);
    });
}

function takePrebufferedElement(nextSrc) {
    const prep = audioState.gaplessNextSource;
    const el = audioState.gaplessNextElement;
    if (!el || !prep || prep.src !== nextSrc)
        return null;

    audioState.gaplessNextElement = null;
    audioState.gaplessNextSource = null;
    audioState.gaplessPrebuffered = false;
    return { el, playableSrc: prep.playableSrc };
}

function maybeRequestGaplessPrebuffer(el, dotNetRef) {
    if (!dotNetRef) return;
    if (audioState.gaplessPrebuffered || audioState.gaplessNextElement) return;
    if (audioState.crossfadePending || audioState.crossfadeActive) return;
    if (!isFinite(el.duration) || el.duration <= 0) return;

    const remaining = el.duration - el.currentTime;
    const prepareWindow = audioState.crossfadeDuration > 0
        ? audioState.crossfadeDuration + 10
        : 10;
    if (remaining > 0 && remaining <= prepareWindow) {
        dotNetRef.invokeMethodAsync('OnGaplessPrebufferNeeded')
            .catch(e => console.error('OnGaplessPrebufferNeeded failed', e));
    }
}

window.audioGaplessSwitch = function () {
    // Instantly switch to the prebuffered element (no overlap)
    const nextEl = audioState.gaplessNextElement;
    if (!nextEl || !audioState.gaplessPrebuffered) return false;

    const ctx = audioState.audioContext;
    const currentEl = audioState.element;

    // Stop current
    if (currentEl) {
        currentEl.pause();
        currentEl.src = '';
    }
    if (audioState.sourceNode) {
        audioState.sourceNode.disconnect();
    }
    if (audioState.fadeGainNode) {
        audioState.fadeGainNode.disconnect();
    }

    // Wire next into audio graph
    let nextSource = null;
    if (ctx) {
        resumeAudioContext();
        nextSource = ctx.createMediaElementSource(nextEl);
        const newFadeGain = ctx.createGain();
        newFadeGain.gain.value = 1.0;
        nextSource.connect(newFadeGain);
        newFadeGain.connect(audioState.loudnessGainNode);
        audioState.fadeGainNode = newFadeGain;
    }

    audioState.element = nextEl;
    audioState.sourceNode = nextSource;
    audioState.gaplessNextElement = null;
    audioState.gaplessNextSource = null;
    audioState.gaplessPrebuffered = false;
    audioState.crossfadePending = false;

    audioState.generation++;
    attachEventsToElement(nextEl, audioState.dotNetRef);
    nextEl.play().catch(e => console.warn('Gapless play prevented', e));
    return true;
};

window.audioStartCrossfade = async function (nextSrc, nextMimeType, fadeDuration) {
    const duration = fadeDuration !== undefined && fadeDuration > 0 ? fadeDuration : audioState.crossfadeDuration;
    if (duration <= 0 || !audioState.element) {
        // No crossfade - just change source directly
        try {
            await audioChangeSource(nextSrc, nextMimeType);
        } finally {
            if (audioState.dotNetRef) {
                audioState.dotNetRef.invokeMethodAsync('OnCrossfadeCompleted')
                    .catch(e => console.error('OnCrossfadeCompleted failed', e));
            }
        }
        return;
    }

    if (audioState.crossfadeTimer) {
        clearInterval(audioState.crossfadeTimer);
        audioState.crossfadeTimer = null;
    }

    // Prefer an element already resolved/buffered by the early prepare path.
    let nextEl = null;
    let playableSrc = null;
    const prebuffered = takePrebufferedElement(nextSrc);
    if (prebuffered) {
        nextEl = prebuffered.el;
        playableSrc = prebuffered.playableSrc;
    } else {
        try {
            playableSrc = await resolvePlayableSrc(nextSrc, nextMimeType);
        } catch (e) {
            console.error('Crossfade resolve failed', e);
            try {
                await audioChangeSource(nextSrc, nextMimeType);
            } finally {
                if (audioState.dotNetRef) {
                    audioState.dotNetRef.invokeMethodAsync('OnCrossfadeCompleted')
                        .catch(err => console.error('OnCrossfadeCompleted failed', err));
                }
            }
            return;
        }

        nextEl = new Audio();
        nextEl.preload = 'auto';
        nextEl.crossOrigin = 'anonymous';
        nextEl.src = playableSrc;
    }

    resumeAudioContext();
    const ctx = audioState.audioContext;
    const currentEl = audioState.element;
    const dotNetRef = audioState.dotNetRef;
    const masterVolume = () => (audioState.gainNode ? audioState.gainNode.gain.value : 1);

    // Suppress outgoing timeupdates while we arm the incoming track.
    audioState.crossfadeActive = true;
    audioState.crossfadeElement = nextEl;
    audioState.crossfadeSourceNode = null;

    // IMPORTANT: do NOT createMediaElementSource on the incoming element during the fade.
    // Dual MediaElementSource into one AudioContext is unreliable in Chromium/WebView2
    // (outgoing fades, incoming stays silent). Play the next track through the element
    // output (OS mixer) so both streams are actually audible, then promote into the
    // Web Audio graph when the fade completes.
    nextEl.volume = 0;
    nextEl.muted = !!(currentEl && currentEl.muted);

    const abortToHardCut = async (reason) => {
        console.warn(reason);
        cancelCrossfadeFade();
        try {
            nextEl.pause();
            nextEl.src = '';
        } catch { /* ignore */ }
        audioState.crossfadeElement = null;
        audioState.crossfadeSourceNode = null;
        try {
            await audioChangeSource(nextSrc, nextMimeType);
        } finally {
            audioState.crossfadeActive = false;
            audioState.crossfadePending = false;
            if (audioState.dotNetRef) {
                audioState.dotNetRef.invokeMethodAsync('OnCrossfadeCompleted')
                    .catch(err => console.error('OnCrossfadeCompleted failed', err));
            }
        }
    };

    // Keep reporting the OUTGOING clock during the blend (UI stays on track A).
    // Incoming element time is ignored until promote + OnCrossfadeCompleted.
    const outgoingTimeHandler = () => {
        if (dotNetRef && !currentEl.paused) {
            dotNetRef.invokeMethodAsync('OnTimeUpdated', currentEl.currentTime)
                .catch(e => console.error('OnTimeUpdated failed', e));
        }
    };
    currentEl.addEventListener('timeupdate', outgoingTimeHandler);

    const canPlay = await waitForCanPlay(nextEl, 20000);
    if (!canPlay) {
        await abortToHardCut('Crossfade next track failed to buffer; falling back to hard cut');
        return;
    }

    await waitUntilNotPaused();

    try {
        if (nextEl.currentTime > 0.05)
            nextEl.currentTime = 0;
        if (!audioState.playbackPaused)
            await nextEl.play();
    } catch (e) {
        console.warn('Crossfade next play prevented', e);
    }

    if (!audioState.playbackPaused) {
        const ready = await waitForMediaPlaying(nextEl, 5000);
        if (!ready || nextEl.paused) {
            await abortToHardCut('Crossfade next track not playing; falling back to hard cut');
            return;
        }
    }

    await waitUntilNotPaused();

    // If we armed early, wait until the real overlap window so the fade lasts
    // the full configured duration instead of a rushed 1-2s cut.
    const remainingAtReady = await waitUntilRemaining(currentEl, duration, 30000);
    await waitUntilNotPaused();

    let effectiveDuration = duration;
    if (remainingAtReady > 0)
        effectiveDuration = Math.min(duration, Math.max(0.8, remainingAtReady));

    // Align the incoming intro with the start of the audible blend.
    try {
        nextEl.volume = 0;
        if (nextEl.currentTime > 0.02)
            nextEl.currentTime = 0;
        if (!audioState.playbackPaused && nextEl.paused)
            await nextEl.play();
    } catch (e) {
        console.warn('Crossfade realign failed', e);
    }

    await waitUntilNotPaused();
    const fadeCompleted = await scheduleEqualPowerFade(
        audioState.fadeGainNode, nextEl, effectiveDuration, masterVolume);
    if (!fadeCompleted) {
        // Cancelled by pause/stop/source change; cleanup already done by caller.
        return;
    }

    currentEl.removeEventListener('timeupdate', outgoingTimeHandler);
    audioState.crossfadePending = false;
    audioState.crossfadeActive = false;

    // Increment generation BEFORE pausing old element to prevent stale pause event
    audioState.generation++;

    if (audioState.sourceNode) {
        audioState.sourceNode.disconnect();
    }
    if (audioState.fadeGainNode) {
        audioState.fadeGainNode.disconnect();
    }
    currentEl.pause();
    currentEl.src = '';

    // Promote incoming into the shared Web Audio graph (EQ / loudness / master).
    let nextSource = null;
    if (ctx) {
        try {
            nextSource = ctx.createMediaElementSource(nextEl);
            const newFadeGain = ctx.createGain();
            newFadeGain.gain.value = 1.0;
            nextSource.connect(newFadeGain);
            newFadeGain.connect(audioState.loudnessGainNode);
            audioState.fadeGainNode = newFadeGain;
            // Element volume must stay at 1 once routed through Web Audio master gain.
            nextEl.volume = 1.0;
        } catch (e) {
            console.error('Crossfade promote into Web Audio failed', e);
            nextEl.volume = masterVolume();
        }
    } else {
        nextEl.volume = masterVolume();
    }

    audioState.element = nextEl;
    audioState.sourceNode = nextSource;
    audioState.crossfadeElement = null;
    audioState.crossfadeSourceNode = null;

    attachEventsToElement(nextEl, audioState.dotNetRef);

    if (!nextEl.paused && audioState.dotNetRef) {
        notifyPlaybackState(audioState.dotNetRef, 'playing');
    }

    if (dotNetRef) {
        if (isFinite(nextEl.duration)) {
            dotNetRef.invokeMethodAsync('OnDurationChanged', nextEl.duration).catch(() => {});
        }
        dotNetRef.invokeMethodAsync('OnTimeUpdated', nextEl.currentTime || 0).catch(() => {});
        dotNetRef.invokeMethodAsync('OnCrossfadeCompleted')
            .catch(e => console.error('OnCrossfadeCompleted failed', e));
    }
};

function attachEventsToElement(el, dotNetRef) {
    if (!dotNetRef) return;

    const gen = audioState.generation;

    el.addEventListener('timeupdate', () => {
        // Ignore if this listener belongs to a stale generation
        if (gen !== audioState.generation) return;

        // Keep the outgoing clock flowing during arm/blend (UI stays on track A).
        if (!audioState.crossfadeActive) {
            dotNetRef.invokeMethodAsync('OnTimeUpdated', el.currentTime)
                .catch(e => console.error('OnTimeUpdated failed', e));
        }

        if (audioState.crossfadeActive || audioState.crossfadePending)
            return;

        // Pre-end crossfade detection
        if (audioState.crossfadeDuration > 0
            && !audioState.crossfadeTimer
            && (Date.now() - audioState.lastSeekTime) > 1500
            && isFinite(el.duration)
            && el.duration > 0) {
            const remaining = el.duration - el.currentTime;
            // Match C#: arm a bit early once prepared so the fade can last full duration.
            const armWindow = audioState.crossfadeDuration + (audioState.gaplessNextElement ? 2 : 0);
            if (remaining <= armWindow && remaining > 0) {
                audioState.crossfadePending = true;
                dotNetRef.invokeMethodAsync('OnCrossfadeNeeded')
                    .catch(e => console.error('OnCrossfadeNeeded failed', e));
            }
        }

        // Early prepare of next track (gapless + crossfade)
        maybeRequestGaplessPrebuffer(el, dotNetRef);
    });

    el.addEventListener('durationchange', () => {
        if (gen !== audioState.generation) return;
        if (isFinite(el.duration)) {
            dotNetRef.invokeMethodAsync('OnDurationChanged', el.duration)
                .catch(e => console.error('OnDurationChanged failed', e));
        }
    });

    el.addEventListener('progress', () => {
        if (gen !== audioState.generation) return;
        const buffered = el.buffered;
        let bufferedEnd = 0;
        if (buffered && buffered.length > 0) {
            bufferedEnd = buffered.end(buffered.length - 1);
        }
        dotNetRef.invokeMethodAsync('OnBufferedUpdated', bufferedEnd)
            .catch(e => console.error('OnBufferedUpdated failed', e));
    });

    el.addEventListener('volumechange', () => {
        if (gen !== audioState.generation) return;
        dotNetRef.invokeMethodAsync('OnVolumeChanged', el.volume, el.muted)
            .catch(e => console.error('OnVolumeChanged failed', e));
    });

    el.addEventListener('play', () => {
        if (gen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('playing', () => {
        if (gen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'playing');
    });

    el.addEventListener('pause', () => {
        if (gen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'paused');
    });

    el.addEventListener('waiting', () => {
        if (gen !== audioState.generation) return;
        notifyPlaybackState(dotNetRef, 'buffering');
    });

    el.addEventListener('ended', () => {
        if (gen !== audioState.generation) return;
        dotNetRef.invokeMethodAsync('OnTrackEnded')
            .catch(e => console.error('OnTrackEnded failed', e));
    });

    el.addEventListener('error', () => {
        if (gen !== audioState.generation) return;
        const code = el.error ? el.error.code : -1;
        const msg = el.error ? el.error.message : 'unknown';
        console.error('Audio error', code, msg);
    });
}

// Wake Lock (keep screen on)
let _wakeLock = null;

window.audioSetKeepScreenOn = async function (enabled) {
    if (enabled && 'wakeLock' in navigator) {
        try {
            _wakeLock = await navigator.wakeLock.request('screen');
            _wakeLock.addEventListener('release', () => { _wakeLock = null; });
        } catch (e) {
            console.warn('WakeLock request failed', e);
        }
    } else if (_wakeLock) {
        await _wakeLock.release();
        _wakeLock = null;
    }
};

// Visualizer
window.K7.Visualizer = {
    _animId: null,
    _canvas: null,
    _ctx: null,

    start: function (canvasEl) {
        this.stop();
        this._canvas = canvasEl;
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
    },

    setPeaks: function () { },
    setProgress: function () { },

    _loop: function () {
        try {
            if (!this._canvas || !this._ctx || !audioState.analyserNode) return;

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

            const data = new Uint8Array(audioState.analyserNode.frequencyBinCount);
            audioState.analyserNode.getByteFrequencyData(data);

            ctx.clearRect(0, 0, w, h);

            // Draw frequency bars
            const barCount = 64;
            const step = Math.floor(data.length / barCount);
            const barWidth = w / barCount;
            const gap = 2;

            for (let i = 0; i < barCount; i++) {
                const value = data[i * step] / 255;
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
