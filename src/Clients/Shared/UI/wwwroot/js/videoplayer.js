let players = {};

// Match AndroidExoHlsTuning: VHS disables subtitle tracks on the first segment error.
// Server returns 503 while ffmpeg extracts the sidecar VTT; retry before VHS sees it.
const K7_VTT_503_MAX_ATTEMPTS = 12;
const K7_VTT_503_MAX_BACKOFF_MS = 15_000;

function isVttSubtitleUrl(uri) {
    if (!uri || typeof uri !== 'string')
        return false;

    return /\.vtt(\?|#|$)/i.test(uri);
}

function getXhrStatusCode(response) {
    if (!response)
        return 0;

    if (typeof response.statusCode === 'number')
        return response.statusCode;

    if (typeof response.status === 'number')
        return response.status;

    return 0;
}

function wrapXhrForVtt503Retry(originalXhr) {
    const wrappedXhr = function (options, callback) {
        const uri = options && (options.uri || options.url);
        if (!isVttSubtitleUrl(uri) || typeof callback !== 'function')
            return originalXhr(options, callback);

        let attempt = 0;
        let aborted = false;
        let pendingTimer = null;
        let activeRequest = null;

        const request = {
            abort: function () {
                aborted = true;
                if (pendingTimer !== null) {
                    clearTimeout(pendingTimer);
                    pendingTimer = null;
                }
                if (activeRequest && typeof activeRequest.abort === 'function')
                    activeRequest.abort();
            }
        };

        const tryRequest = function () {
            if (aborted)
                return;

            attempt += 1;
            activeRequest = originalXhr(options, function (err, response) {
                activeRequest = null;
                if (aborted)
                    return;

                const status = getXhrStatusCode(response);
                if (status === 503 && attempt < K7_VTT_503_MAX_ATTEMPTS) {
                    const exponent = Math.min(attempt - 1, 4);
                    const delay = Math.min(500 * (1 << exponent), K7_VTT_503_MAX_BACKOFF_MS);
                    pendingTimer = setTimeout(function () {
                        pendingTimer = null;
                        tryRequest();
                    }, delay);
                    return;
                }

                callback(err, response);
            });
        };

        tryRequest();
        return request;
    };

    Object.keys(originalXhr).forEach(function (key) {
        if (key === 'original')
            return;

        const value = originalXhr[key];
        wrappedXhr[key] = typeof value === 'function' ? value.bind(originalXhr) : value;
    });

    // Keep VHS on this xhr (same as the Windows stream bridge).
    wrappedXhr.original = false;
    wrappedXhr.__k7Vtt503Retry = true;
    return wrappedXhr;
}

function ensureVtt503RetryXhr() {
    if (!window.videojs)
        return false;

    const wrapHolder = function (holder) {
        if (!holder || !holder.xhr || holder.xhr.__k7Vtt503Retry)
            return;

        holder.xhr = wrapXhrForVtt503Retry(holder.xhr);
    };

    wrapHolder(videojs);
    wrapHolder(videojs.Vhs || videojs.VHS);
    return true;
}

window.K7 = window.K7 || {};
K7.ensureVtt503RetryXhr = ensureVtt503RetryXhr;

// Install as soon as video.js is present (DesignSystem loads scripts sequentially).
ensureVtt503RetryXhr();

// Optional Windows MAUI stream bridge hooks (defined by MAUI wwwroot/js/windowsStreamFetch.js).
// VTT 503 retry must stay outermost so bridge 503 responses are retried.
function ensurePlatformStreamBridge() {
    if (window.K7 && typeof K7.ensureWindowsStreamBridge === 'function')
        K7.ensureWindowsStreamBridge();
    ensureVtt503RetryXhr();
}

function notifyPlatformVideoJsPlayerCreated(player, id) {
    if (window.K7 && typeof K7.onVideoJsPlayerCreated === 'function')
        K7.onVideoJsPlayerCreated(player, id);
    ensureVtt503RetryXhr();
}

function normalizeHlsMimeType(type) {
    if (!type || type.indexOf('mpegurl') !== -1)
        return 'application/x-mpegURL';

    return type;
}

function getPlayerBufferedEnd(player) {
    try {
        const buffered = player?.buffered?.();
        if (buffered && buffered.length > 0)
            return buffered.end(buffered.length - 1);
    } catch (e) {
    }

    return 0;
}

function ensurePlaybackStarted(player, id) {
    if (!player || typeof player.paused !== 'function')
        return;

    if (!player.paused())
        return;

    const bufferedEnd = getPlayerBufferedEnd(player);
    if (!(bufferedEnd > 0) && player.readyState() < 2)
        return;

    var promise = player.play();
    if (promise !== undefined) {
        promise.catch(function () {
        });
    }
}

window.initVideoJs = function (id, videoPlayer, videoContainer, options, dotNetRef) {
    ensurePlatformStreamBridge();
    // If a player already exists for this id, dispose it first to avoid duplicate streams/listeners
    if (players[id]) {
        try {
            players[id].dispose();
        } catch (e) {
            console.warn('Error disposing existing player before re-init', e);
        }
        delete players[id];
    }

    const playerOptions = {
        ...options,
        // Keep K7 CSS (absolute px from SubtitleStyleHelper) in control - native ::cue
        // sizes as a fraction of the video height and ignores most of our stylesheet.
        textTrackSettings: false,
        html5: {
            ...(options?.html5 ?? {}),
            nativeTextTracks: false,
            vhs: {
                ...(options?.html5?.vhs ?? {}),
                overrideNative: true
            }
        }
    };

    const player = videojs(videoPlayer, playerOptions);
    player.volume(options.volume);
    // Make the player wrapper fill the .video-container so object-fit works on the <video>
    player.fill(true);

    const otherEvents = [
        'beforepluginsetup', // Signals that a plugin is about to be set up on a player.
        'languagechange', // Fires when the player language change
        'playerresize', // Called when the player size has changed // Can be done in Blazor 
        'pluginsetup', // Signals that a plugin has just been set up on a player.


        'resize', // Fires when the video's intrinsic dimensions change
        'ratechange', // Fires when the playing speed of the audio/video is changed
        'texttrackchange', // Fires when the text track has been changed
        'textdata', // Fires when we get a textdata event from tech

    ];

    const genericPlayerEvents = [
        'stalled', // Fires when the browser is trying to get media data, but data is not available.
        'abort', // Fires when the loading of an audio/video is aborted.
        'dispose', // Called when the player is being disposed of.
        'emptied', // Fires when the current playlist is empty.
        'loadstart', // Fired when the user agent begins looking for media data
        'loadeddata', // Fires when the browser has loaded the current frame of the audio/video.

        'ready', // Triggered when a Component is ready.

        'play', // Triggered whenever a play event happens. Indicates that playback has started or resumed
        'pause', // Fired whenever the media has been paused
        'ended', // Fired when the end of the media resource is reached (currentTime == duration)

        'waiting', // A readyState change on the DOM element has caused playback to stop.
        'seeking', // Fired whenever the player is jumping to a new time
        'playing', // The media is no longer blocked from playback, and has started playing.

        'seeked', // Fired when the player has finished jumping to a new time
        'enterpictureinpicture', // This event fires when the player enters picture in picture mode
        'leavepictureinpicture', // This event fires when the player leaves picture in picture mode
        'canplay', // The media has a readyState of HAVE_FUTURE_DATA or greater.
        'canplaythrough', // The media has a readyState of HAVE_ENOUGH_DATA or greater. This means that the entire media file can be played without buffering.
    ];

    genericPlayerEvents.forEach(event => {
        player.on(event, () => {
            dotNetRef.invokeMethodAsync('OnGenericPlayerEvent', event)
                .catch((err) => console.error(`${methodName} not implemented in C#`, err));
        });
    });

    player.on('error', function () {
        dotNetRef.invokeMethodAsync('OnPlayerError', player.error()?.code ?? 0, player.error()?.message ?? '')
            .catch((error) => console.error('Error invoking OnPlayerError', error));
    });

    notifyPlatformVideoJsPlayerCreated(player, id);

    // Fires when the browser has loaded meta data for the audio/video.ed.
    player.on('loadedmetadata', function () {
        const duration = player.duration();
        dotNetRef.invokeMethodAsync('OnDurationChanged', duration)
            .catch((error) => console.error('Error invoking C# method', error));
    });

    // Fired when the current playback position has changed * During playback this is fired every 15-250 milliseconds, depending on the playback technology in use.
    player.on('timeupdate', function () {
        dotNetRef.invokeMethodAsync('OnTimeUpdated', player.currentTime())
            .catch((error) => console.error('Error invoking C# method', error));
    });

    // Fired while the user agent is downloading media data.
    player.on('progress', function () {
        const bufferedEnd = getPlayerBufferedEnd(player);
        dotNetRef.invokeMethodAsync('OnBufferedUpdated', bufferedEnd)
            .catch((error) => console.error('Error invoking C# method', error));
    });

    // Autoplay may be blocked until media is buffered; retry once media can play.
    player.on('canplay', function () {
        ensurePlaybackStarted(player, id);
    });

    player.on('loadeddata', function () {
        ensurePlaybackStarted(player, id);
    });

    // // Fires when the volume has been changed
    player.on('volumechange', function () {
        dotNetRef.invokeMethodAsync('OnVolumeChanged', player.volume(), player.muted())
            .catch(error => console.error('Error invoking OnVolumeChanged', error));
    });

    player.on('ratechange', () => {
        dotNetRef.invokeMethodAsync('OnPlaybackRateChanged', player.playbackRate())
            .catch(error => console.error('Error invoking OnVolumeChanged', error));
    });

    document.addEventListener('fullscreenchange', function () {
        dotNetRef.invokeMethodAsync('OnFullscreenChanged', document.fullscreenElement === videoContainer)
            .catch(error => console.error('Error invoking OnVolumeChanged', error));
    });

    players[id] = player;
    k7AttachSubtitleStyleHooks(player);
    return player;
}

window.disposeVideoJs = function (id) {
    const player = players[id];
    if (player) {
        try {
            player.dispose();
        } catch (e) {
            console.warn('Error disposing Video.js player', e);
        }
        delete players[id];
    }
}

window.play = function (id) {
    const player = players[id];
    if (player) {
        player.ready(function () {
            var promise = player.play();
            if (promise !== undefined) {
                promise.catch(function (error) {
                    console.warn('Auto-play was prevented', error);
                });
            }
        });
    }
}

window.pause = function (id) {
    players[id]?.pause();
}

window.stop = function (id) {
    players[id]?.pause();
}

window.changeSource = function (id, src, type, subtitleSlug) {
    ensurePlatformStreamBridge();
    const player = players[id];
    if (player) {
        const normalizedType = normalizeHlsMimeType(type);
        player.src({ src: src, type: normalizedType });
        player.ready(function () {
            var promise = player.play();
            if (promise !== undefined) {
                promise.catch(function (error) {
                    console.warn('Auto-play was prevented after changing source', error);
                });
            }
        });
        // VHS adds EXT-X-MEDIA subtitle tracks after master parse - often after loadedmetadata.
        window.switchSubtitleTrackWhenReady(id, subtitleSlug);
    }
}

window.changeSourceAndSeek = function (id, src, type, seekTime, subtitleSlug) {
    ensurePlatformStreamBridge();
    const player = players[id];
    if (!player) return;

    const normalizedType = normalizeHlsMimeType(type);

    let seekApplied = false;
    const applySeekAndPlay = function () {
        if (seekApplied) return;
        seekApplied = true;
        player.currentTime(seekTime);
        var promise = player.play();
        if (promise !== undefined) {
            promise.catch(function (error) {
                console.warn('Auto-play was prevented after seek', error);
            });
        }
        window.switchSubtitleTrackWhenReady(id, subtitleSlug);
    };

    // Seek as soon as duration/playlist metadata is known - before VHS buffers segment 0.
    // #EXT-X-START on the playlist also anchors the initial position when supported.
    player.one('loadedmetadata', applySeekAndPlay);
    player.one('loadeddata', function () {
        if (Math.abs(player.currentTime() - seekTime) > 1) {
            player.currentTime(seekTime);
        }
        if (!seekApplied) {
            applySeekAndPlay();
        }
    });
    player.pause();
    player.src({ src: src, type: normalizedType });
}

window.switchAudioTrack = function (id, trackName) {
    const player = players[id];
    if (!player) return false;

    const audioTracks = player.audioTracks();
    if (!audioTracks) return false;

    let found = false;
    for (let i = 0; i < audioTracks.length; i++) {
        if (audioTracks[i].label === trackName) {
            audioTracks[i].enabled = true;
            found = true;
        } else {
            audioTracks[i].enabled = false;
        }
    }
    return found;
}

function isSelectableTextTrack(track) {
    if (!track)
        return false;

    const kind = track.kind;
    return kind === 'subtitles' || kind === 'captions';
}

function textTrackMatchesSlug(track, slug) {
    if (!track || !slug)
        return false;

    if (track.label === slug)
        return true;

    if (track.id && String(track.id).indexOf(slug) !== -1)
        return true;

    // Some VHS builds surface NAME in id / language only.
    if (track.language && track.language === slug)
        return true;

    return false;
}

window.switchSubtitleTrack = function (id, slug) {
    const player = players[id];
    if (!player) return false;

    const textTracks = player.textTracks();
    if (!textTracks) return false;

    // null/undefined/empty slug disables all subtitle tracks
    if (!slug) {
        for (let i = 0; i < textTracks.length; i++) {
            if (isSelectableTextTrack(textTracks[i])) {
                textTracks[i].mode = 'disabled';
            }
        }
        return true;
    }

    let found = false;
    for (let i = 0; i < textTracks.length; i++) {
        if (!isSelectableTextTrack(textTracks[i]))
            continue;

        if (textTrackMatchesSlug(textTracks[i], slug)) {
            // hidden then showing forces VHS to fetch the subtitle playlist if needed.
            textTracks[i].mode = 'hidden';
            textTracks[i].mode = 'showing';
            found = true;
        } else {
            textTracks[i].mode = 'disabled';
        }
    }
    return found;
}

// VHS registers EXT-X-MEDIA text tracks asynchronously; retry until the slug appears.
window.switchSubtitleTrackWhenReady = function (id, slug, maxAttempts) {
    const player = players[id];
    if (!player)
        return;

    if (player._k7SubtitleReadyToken)
        player._k7SubtitleReadyToken += 1;
    else
        player._k7SubtitleReadyToken = 1;

    const token = player._k7SubtitleReadyToken;
    const attempts = typeof maxAttempts === 'number' ? maxAttempts : 40;

    if (!slug) {
        window.switchSubtitleTrack(id, null);
        return;
    }

    const tracks = player.textTracks && player.textTracks();
    let onAddTrack = null;
    const cleanup = function () {
        if (onAddTrack && tracks && typeof tracks.removeEventListener === 'function')
            tracks.removeEventListener('addtrack', onAddTrack);
        onAddTrack = null;
    };

    const trySwitch = function (attempt) {
        if (player._k7SubtitleReadyToken !== token)
            return;

        if (window.switchSubtitleTrack(id, slug)) {
            cleanup();
            return;
        }

        if (attempt >= attempts) {
            cleanup();
            return;
        }

        setTimeout(function () {
            trySwitch(attempt + 1);
        }, 250);
    };

    if (tracks && typeof tracks.addEventListener === 'function') {
        onAddTrack = function () {
            if (player._k7SubtitleReadyToken !== token)
                return;

            if (window.switchSubtitleTrack(id, slug))
                cleanup();
        };
        tracks.addEventListener('addtrack', onAddTrack);
    }

    trySwitch(0);
}

// Windows MAUI HLS: VHS EXT-X-MEDIA subtitle playlists often never surface cues in WebView2.
// Load the full sidecar VTT (same endpoint as native Direct) via the auth bridge instead.
window.loadSidecarSubtitleTrack = async function (id, vttUrl, slug) {
    const player = players[id];
    if (!player)
        return false;

    ensurePlatformStreamBridge();

    if (player._k7SidecarObjectUrl) {
        try { URL.revokeObjectURL(player._k7SidecarObjectUrl); } catch (e) { }
        player._k7SidecarObjectUrl = null;
    }

    try {
        const remoteTracks = player.remoteTextTracks && player.remoteTextTracks();
        if (remoteTracks) {
            for (let i = remoteTracks.length - 1; i >= 0; i--) {
                const track = remoteTracks[i];
                if (track && track.id && String(track.id).indexOf('k7-sidecar-') === 0)
                    player.removeRemoteTextTrack(track);
            }
        }
    } catch (e) {
    }

    // Prefer sidecar over HLS group tracks so we do not race two sources.
    window.switchSubtitleTrack(id, null);

    if (!slug || !vttUrl)
        return true;

    const fetchVttText = async function () {
        const maxAttempts = 12;
        for (let attempt = 1; attempt <= maxAttempts; attempt++) {
            let status = 0;
            let body = null;

            if (window.K7 && K7._windowsStreamFetchRef) {
                const result = await K7._windowsStreamFetchRef.invokeMethodAsync(
                    'FetchStreamAsync',
                    vttUrl,
                    null);
                if (!result) {
                    status = 0;
                } else {
                    status = result.statusCode || 0;
                    body = result.body;
                }
            } else {
                const response = await fetch(vttUrl, { credentials: 'include' });
                status = response.status;
                if (status >= 200 && status < 300)
                    body = await response.text();
            }

            if (status === 503 && attempt < maxAttempts) {
                const exponent = Math.min(attempt - 1, 4);
                const delay = Math.min(500 * (1 << exponent), 15_000);
                await new Promise(function (resolve) { setTimeout(resolve, delay); });
                continue;
            }

            if (status < 200 || status >= 300 || body == null)
                return null;

            if (typeof body === 'string') {
                if (body.indexOf('#EXTM3U') === 0)
                    return null;
                // Bridge may return base64 for byte[]; decode if it does not look like WEBVTT.
                if (body.indexOf('WEBVTT') === 0 || body.indexOf('webvtt') === 0)
                    return body;
                try {
                    const binary = atob(body);
                    return new TextDecoder('utf-8').decode(
                        Uint8Array.from(binary, function (c) { return c.charCodeAt(0); }));
                } catch (e) {
                    return body;
                }
            }

            if (body instanceof ArrayBuffer)
                return new TextDecoder('utf-8').decode(new Uint8Array(body));

            if (body instanceof Uint8Array)
                return new TextDecoder('utf-8').decode(body);

            if (Array.isArray(body))
                return new TextDecoder('utf-8').decode(new Uint8Array(body));

            return null;
        }

        return null;
    };

    try {
        const text = await fetchVttText();
        if (!text || text.indexOf('WEBVTT') !== 0 && text.toLowerCase().indexOf('webvtt') !== 0) {
            console.warn('loadSidecarSubtitleTrack: no WEBVTT body for', vttUrl);
            return false;
        }

        const blob = new Blob([text], { type: 'text/vtt' });
        const objectUrl = URL.createObjectURL(blob);
        player._k7SidecarObjectUrl = objectUrl;

        const handle = player.addRemoteTextTrack({
            kind: 'subtitles',
            src: objectUrl,
            srclang: 'und',
            label: slug,
            id: 'k7-sidecar-' + slug,
            mode: 'showing',
            default: true
        }, false);

        const track = handle && (handle.track || handle);
        if (track) {
            track.mode = 'showing';
            if (!track.id)
                track.id = 'k7-sidecar-' + slug;
        }

        k7AttachSubtitleStyleHooks(player);
        k7RefreshSubtitleStylesForAllPlayers();
        return true;
    } catch (err) {
        console.warn('loadSidecarSubtitleTrack failed', err);
        return false;
    }
}

function k7SafeCssValue(value, fallback) {
    if (typeof value !== 'string' || !value)
        return fallback;
    // Allow only CSS-safe characters for injected values.
    if (/[;{}\\]/.test(value))
        return fallback;
    return value;
}

function k7ClearSubtitleCueInlineStyles(root) {
    if (!root || !root.querySelectorAll)
        return;

    root.querySelectorAll('.vjs-text-track-cue').forEach(function (cue) {
        cue.style.removeProperty('background-color');
        cue.style.removeProperty('background');
        cue.style.removeProperty('font-size');
        cue.style.removeProperty('font-family');
        cue.style.removeProperty('color');
        cue.style.removeProperty('text-shadow');
        cue.style.removeProperty('transform');
        cue.style.removeProperty('left');
        cue.style.removeProperty('right');
        cue.style.removeProperty('width');

        cue.querySelectorAll('*').forEach(function (node) {
            node.style.removeProperty('color');
            node.style.removeProperty('background-color');
            node.style.removeProperty('background');
            node.style.removeProperty('font-family');
            node.style.removeProperty('font-size');
            node.style.removeProperty('text-shadow');
            node.style.removeProperty('font-variant');
        });
    });
}

function k7ApplySubtitleStyleSheet(style) {
    var id = 'k7-subtitle-style';
    var el = document.getElementById(id);
    if (!el) {
        el = document.createElement('style');
        el.id = id;
        document.head.appendChild(el);
    }

    if (!style) {
        el.textContent = '';
        return;
    }

    var fontFamily = k7SafeCssValue(style.fontFamily, 'inherit');
    var fontSize = k7SafeCssValue(style.fontSize, '18px');
    var color = k7SafeCssValue(style.color, '#FFFFFF');
    var backgroundColor = k7SafeCssValue(style.backgroundColor, 'rgba(0, 0, 0, 0.5)');
    var textShadow = k7SafeCssValue(style.textShadow, 'none');

    // Video.js textTrackSettings writes inline styles on active cues; use !important
    // on the cue box and direct children, then strip inline overrides after each refresh.
    el.textContent =
        // Neutralize Video.js default .vjs-text-track { font-size: 1.4em } which scales
        // with the player base font and made HLS cues look larger than the XAML sidecar.
        '.video-js .vjs-text-track {' +
        'font-size:inherit !important;' +
        '}' +
        '.video-js .vjs-text-track-display .vjs-text-track-cue {' +
        'background:transparent !important;' +
        'background-color:transparent !important;' +
        'text-align:center !important;' +
        'width:100% !important;' +
        'left:0 !important;' +
        'right:0 !important;' +
        'transform:none !important;' +
        'top:auto !important;' +
        'bottom:8% !important;' +
        'font-family:' + fontFamily + ' !important;' +
        'font-size:' + fontSize + ' !important;' +
        'color:' + color + ' !important;' +
        'text-shadow:' + textShadow + ' !important;' +
        '}' +
        '.video-js .vjs-text-track-display .vjs-text-track-cue > * {' +
        'display:inline-block !important;' +
        'width:fit-content !important;' +
        'max-width:90% !important;' +
        'padding:4px 8px !important;' +
        'border-radius:4px !important;' +
        'line-height:1.25 !important;' +
        'white-space:pre-wrap !important;' +
        'text-align:center !important;' +
        'font-family:' + fontFamily + ' !important;' +
        'font-size:' + fontSize + ' !important;' +
        'color:' + color + ' !important;' +
        'background-color:' + backgroundColor + ' !important;' +
        'text-shadow:' + textShadow + ' !important;' +
        '}' +
        '.video-js ::cue {' +
        'font-family:' + fontFamily + ';' +
        'font-size:' + fontSize + ';' +
        'color:' + color + ';' +
        'background-color:transparent;' +
        '}';
}

function k7RefreshSubtitleStylesForAllPlayers() {
    Object.keys(players).forEach(function (playerId) {
        var player = players[playerId];
        if (!player || typeof player.el !== 'function')
            return;

        k7ClearSubtitleCueInlineStyles(player.el());
    });
}

function k7AttachSubtitleStyleHooks(player) {
    if (!player || player.k7SubtitleStyleHooksAttached)
        return;

    player.k7SubtitleStyleHooksAttached = true;

    var tracks = player.textTracks();
    if (!tracks)
        return;

    var onCueChange = function () {
        k7RefreshSubtitleStylesForAllPlayers();
    };

    tracks.addEventListener('cuechange', onCueChange);
    player.on('texttrackchange', onCueChange);
}

window.applySubtitleStyle = function (style) {
    k7ApplySubtitleStyleSheet(style);
    k7RefreshSubtitleStylesForAllPlayers();
}

window.getAudioTracks = function (id) {
    const player = players[id];
    if (!player) return [];

    const audioTracks = player.audioTracks();
    if (!audioTracks) return [];

    const result = [];
    for (let i = 0; i < audioTracks.length; i++) {
        result.push({
            label: audioTracks[i].label,
            language: audioTracks[i].language,
            enabled: audioTracks[i].enabled,
            index: i
        });
    }
    return result;
}

window.seek = function (id, seconds) {
    const player = players[id];
    if (!player) return;

    const doSeek = function () {
        player.currentTime(seconds);
    };

    if (player.readyState() >= 1) {
        doSeek();
    } else {
        player.one('loadedmetadata', doSeek);
    }
}

window.mute = function (id) {
    players[id]?.muted(true);
}

window.unmute = function (id) {
    players[id]?.muted(false);
}

window.changeVolume = function (id, volume) {
    players[id]?.volume(volume);
}

window.changePlaybackRate = function (id, rate) {
    players[id]?.playbackRate(rate);
}

window.getCurrentTime = function (id) {
    return players[id]?.currentTime() ?? 0;
}

window.getBufferedTime = function (id) {
    return getPlayerBufferedEnd(players[id]);
}

window.getDuration = function (id) {
    return players[id]?.duration() ?? 0;
}

window.enterFullscreen = function (videoContainer) {
    videoContainer?.requestFullscreen();
}

window.exitFullscreen = function () {
    document?.exitFullscreen();
}

window.setAspectRatioMode = function (id, mode) {
    const player = players[id];
    if (!player) return;
    const videoEl = player.el()?.querySelector('video');
    if (!videoEl) return;
    const fit = mode === 'Fill' ? 'cover' : mode === 'Stretch' ? 'fill' : 'contain';
    videoEl.style.setProperty('object-fit', fit, 'important');
}

window.hideBodyScroll = (hide) => {
    if (hide) {
        document.body.classList.add('no-scroll');
    } else {
        document.body.classList.remove('no-scroll');
    }
};

window.canPlayMediaSource = async function (source) {
    const {
        type,
        audioType,
        subtitleType,
        width,
        height,
        bitrate,
        framerate
    } = source;

    const tech = videojs.getTech('Html5');
    const results = {
        video: '',
        audio: '',
        subtitle: '',
        mediaCapabilities: '',
        nativeVideo: '',
        nativeAudio: '',
    };

    // 1. Video.js
    results.video = tech?.canPlayType?.(type) || '';
    results.audio = audioType ? tech?.canPlayType?.(audioType) || '' : '';

    // 2. MediaCapabilities API
    if ('mediaCapabilities' in navigator && navigator.mediaCapabilities.decodingInfo) {
        try {
            const mediaConfig = {
                type: 'file',
                video: {
                    contentType: type,
                    width,
                    height,
                    bitrate,
                    framerate
                },
                audio: audioType ? { contentType: audioType, channels: 2, bitrate: 128000, samplerate: 48000 } : undefined
            };
            const result = await navigator.mediaCapabilities.decodingInfo(mediaConfig);
            if (result.supported && result.smooth && result.powerEfficient) {
                results.mediaCapabilities = 'probably';
            } else if (result.supported) {
                results.mediaCapabilities = 'maybe';
            }
        } catch (e) {
            console.warn('mediaCapabilities decodingInfo error:', e);
        }
    }

    // 3. Native canPlayType fallback
    try {
        const videoEl = document.createElement('video');
        results.nativeVideo = videoEl.canPlayType(type);
        if (audioType) results.nativeAudio = videoEl.canPlayType(audioType);
    } catch (e) {
        console.warn('native canPlayType error:', e);
    }

    // 4. Subtitles support
    if (subtitleType === 'text/vtt') {
        results.subtitle = 'probably'; // video.js has native support
    } else {
        results.subtitle = 'maybe';
    }

    // 5. Global evaluation
    const isVideoSupported = ['probably', 'maybe'].includes(results.video || results.nativeVideo);
    const isAudioSupported = !audioType || ['probably', 'maybe'].includes(results.audio || results.nativeAudio);
    const isSubtitleSupported = ['probably', 'maybe'].includes(results.subtitle);

    const isMediaCapable = ['probably', 'maybe'].includes(results.mediaCapabilities);
    const isSupported = isVideoSupported && isAudioSupported && isSubtitleSupported;

    return {
        isSupported,
        isMediaCapable,
        details: results
    };
};
