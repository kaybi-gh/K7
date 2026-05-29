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
    lastSeekTime: 0
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
window.K7.scrollIntoViewSmooth = function (el) {
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
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
    // Prime the audio element with a silent play+pause so future play() calls succeed
    var el = audioState.element;
    if (el && el.paused && !el.src) {
        el.src = 'data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAQB8AAIA+AAACABAAZGF0YQAAAAA=';
        el.play().then(function () { el.pause(); el.src = ''; }).catch(function () { el.src = ''; });
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
        if (audioState.crossfadeActive) return;
        dotNetRef.invokeMethodAsync('OnTimeUpdated', el.currentTime)
            .catch(e => console.error('OnTimeUpdated failed', e));

        // Pre-end crossfade detection
        if (audioState.crossfadeDuration > 0
            && !audioState.crossfadePending
            && !audioState.crossfadeTimer
            && (Date.now() - audioState.lastSeekTime) > 1500
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
    audioState.dotNetRef = null;
};

window.audioPlay = function () {
    const el = audioState.element;
    if (!el) return;
    resumeAudioContext();
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
};

window.audioChangeSource = function (src, mimeType) {
    const el = audioState.element;
    if (!el) return;

    resumeAudioContext();
    audioState.crossfadePending = false;
    audioState.crossfadeActive = false;

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

    el.src = src;
    el.load();

    el.addEventListener('canplay', function () {
        el.play().catch(() => {});
    }, { once: true });
};

window.audioSetCrossfadeDuration = function (seconds) {
    audioState.crossfadeDuration = seconds;
};

window.audioGaplessPrebuffer = function (src, mimeType) {
    // Discard any previous prebuffer
    if (audioState.gaplessNextElement) {
        audioState.gaplessNextElement.src = '';
        audioState.gaplessNextElement = null;
        audioState.gaplessNextSource = null;
    }
    audioState.gaplessPrebuffered = false;

    const nextEl = new Audio();
    nextEl.preload = 'auto';
    nextEl.crossOrigin = 'anonymous';
    nextEl.src = src;
    nextEl.load();
    audioState.gaplessNextElement = nextEl;
    audioState.gaplessNextSource = { src, mimeType };

    nextEl.addEventListener('canplaythrough', () => {
        audioState.gaplessPrebuffered = true;
    }, { once: true });
};

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

window.audioStartCrossfade = function (nextSrc, nextMimeType, fadeDuration) {
    const duration = fadeDuration !== undefined && fadeDuration > 0 ? fadeDuration : audioState.crossfadeDuration;
    if (duration <= 0 || !audioState.element) {
        // No crossfade - just change source directly
        audioChangeSource(nextSrc, nextMimeType);
        return;
    }

    resumeAudioContext();
    const ctx = audioState.audioContext;
    const currentEl = audioState.element;
    const dotNetRef = audioState.dotNetRef;

    // Mark crossfade active so old element's timeupdate is ignored
    audioState.crossfadeActive = true;

    // Create next audio element with its own Web Audio pipeline
    const nextEl = new Audio();
    nextEl.preload = 'auto';
    nextEl.crossOrigin = 'anonymous';
    nextEl.src = nextSrc;

    audioState.crossfadeElement = nextEl;

    // Create a temporary gain for the next element during crossfade
    let nextSource = null;
    let nextGain = null;
    if (ctx) {
        nextSource = ctx.createMediaElementSource(nextEl);
        nextGain = ctx.createGain();
        nextGain.gain.value = 0;
        // Connect next through the same chain: nextSource -> nextGain -> loudness -> (rest of chain)
        nextSource.connect(nextGain);
        nextGain.connect(audioState.loudnessGainNode);
        audioState.crossfadeSourceNode = nextSource;
    }

    const stepMs = 50;
    const steps = (duration * 1000) / stepMs;
    let step = 0;

    // Report new track's time updates during crossfade
    const crossfadeTimeHandler = () => {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnTimeUpdated', nextEl.currentTime)
                .catch(e => console.error('OnTimeUpdated failed', e));
        }
    };
    const crossfadeDurationHandler = () => {
        if (isFinite(nextEl.duration) && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnDurationChanged', nextEl.duration)
                .catch(e => console.error('OnDurationChanged failed', e));
        }
    };
    nextEl.addEventListener('timeupdate', crossfadeTimeHandler);
    nextEl.addEventListener('durationchange', crossfadeDurationHandler);

    // Notify .NET of new duration immediately once available
    if (isFinite(nextEl.duration) && dotNetRef) {
        dotNetRef.invokeMethodAsync('OnDurationChanged', nextEl.duration)
            .catch(() => {});
    }
    // Reset time to 0 for the new track
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnTimeUpdated', 0).catch(() => {});
    }

    // Use an equal-power crossfade curve for smoother transitions
    nextEl.play().catch(e => console.warn('Crossfade next play prevented', e));

    audioState.crossfadeTimer = setInterval(() => {
        step++;
        const ratio = step / steps;

        // Equal-power fade: cos/sin curves
        const fadeOut = Math.cos(ratio * Math.PI / 2);
        const fadeIn = Math.sin(ratio * Math.PI / 2);

        // Fade out current via Web Audio GainNode (not element.volume)
        if (audioState.fadeGainNode) {
            audioState.fadeGainNode.gain.value = fadeOut;
        }

        if (nextGain) {
            nextGain.gain.value = fadeIn;
        }

        if (step >= steps) {
            clearInterval(audioState.crossfadeTimer);
            audioState.crossfadeTimer = null;
            audioState.crossfadePending = false;
            audioState.crossfadeActive = false;

            // Increment generation BEFORE pausing old element to prevent stale pause event
            audioState.generation++;

            // Disconnect old source
            if (audioState.sourceNode) {
                audioState.sourceNode.disconnect();
            }
            if (audioState.fadeGainNode) {
                audioState.fadeGainNode.disconnect();
            }
            currentEl.pause();
            currentEl.src = '';

            // Remove temporary event listeners from next element
            nextEl.removeEventListener('timeupdate', crossfadeTimeHandler);
            nextEl.removeEventListener('durationchange', crossfadeDurationHandler);

            // Promote next element: create new fadeGain and reconnect
            if (ctx && nextSource) {
                if (nextGain) {
                    nextGain.disconnect();
                    nextSource.disconnect();
                }
                const newFadeGain = ctx.createGain();
                newFadeGain.gain.value = 1.0;
                nextSource.connect(newFadeGain);
                newFadeGain.connect(audioState.loudnessGainNode);
                audioState.fadeGainNode = newFadeGain;
            }

            // Swap state
            audioState.element = nextEl;
            audioState.sourceNode = nextSource;
            audioState.crossfadeElement = null;
            audioState.crossfadeSourceNode = null;

            // Re-attach DOM events to new element
            attachEventsToElement(nextEl, audioState.dotNetRef);

            if (!nextEl.paused && audioState.dotNetRef) {
                notifyPlaybackState(audioState.dotNetRef, 'playing');
            }
        }
    }, stepMs);
};

function attachEventsToElement(el, dotNetRef) {
    if (!dotNetRef) return;

    const gen = audioState.generation;

    el.addEventListener('timeupdate', () => {
        // Ignore if this listener belongs to a stale generation
        if (gen !== audioState.generation) return;
        if (audioState.crossfadeActive) return;
        dotNetRef.invokeMethodAsync('OnTimeUpdated', el.currentTime)
            .catch(e => console.error('OnTimeUpdated failed', e));

        // Pre-end crossfade detection
        if (audioState.crossfadeDuration > 0
            && !audioState.crossfadePending
            && !audioState.crossfadeTimer
            && (Date.now() - audioState.lastSeekTime) > 1500
            && isFinite(el.duration)
            && el.duration > 0) {
            const remaining = el.duration - el.currentTime;
            if (remaining <= audioState.crossfadeDuration && remaining > 0) {
                audioState.crossfadePending = true;
                dotNetRef.invokeMethodAsync('OnCrossfadeNeeded')
                    .catch(e => console.error('OnCrossfadeNeeded failed', e));
            }
        }

        // Gapless prebuffer: when crossfade is 0, prebuffer next track ~10s before end
        if (audioState.crossfadeDuration === 0
            && !audioState.gaplessPrebuffered
            && !audioState.gaplessNextElement
            && !audioState.crossfadePending
            && isFinite(el.duration)
            && el.duration > 0) {
            const remaining = el.duration - el.currentTime;
            if (remaining <= 10 && remaining > 0) {
                dotNetRef.invokeMethodAsync('OnGaplessPrebufferNeeded')
                    .catch(e => console.error('OnGaplessPrebufferNeeded failed', e));
            }
        }
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

    _loop: function () {
        if (!this._canvas || !this._ctx || !audioState.analyserNode) return;

        const ctx = this._ctx;
        const canvas = this._canvas;
        const w = canvas.width = canvas.clientWidth * (window.devicePixelRatio || 1);
        const h = canvas.height = canvas.clientHeight * (window.devicePixelRatio || 1);

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
    }
};
