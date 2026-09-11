let players = {};

// Match AndroidExoHlsTuning: VHS disables subtitle tracks on the first segment error.
// Server returns 503 while ffmpeg extracts the sidecar VTT; retry before VHS sees it.
const K7_VTT_503_MAX_ATTEMPTS = 12;
const K7_VTT_503_MAX_BACKOFF_MS = 15_000;

function isVttSubtitleUrl(uri) {
    if (!uri || typeof uri !== 'string')
        return false;

    // Segment cues (.../segments/N.vtt) and any other sidecar VTT URL.
    if (/\.vtt(\?|#|$)/i.test(uri))
        return true;

    return /\/hls-stream\/subtitles\/\d+\/segments\//i.test(uri);
}

function vtt503RetryDelayMs(attemptOneBased) {
    const exponent = Math.min(Math.max(attemptOneBased, 1) - 1, 4);
    return Math.min(500 * (1 << exponent), K7_VTT_503_MAX_BACKOFF_MS);
}

function getXhrStatusCode(err, response) {
    if (response) {
        if (typeof response.statusCode === 'number')
            return response.statusCode;

        if (typeof response.status === 'number')
            return response.status;

        if (response.rawRequest && typeof response.rawRequest.status === 'number')
            return response.rawRequest.status;
    }

    if (err && typeof err.statusCode === 'number')
        return err.statusCode;

    return 0;
}

// Native XHR patch: VHS / @videojs/xhr often bypass module wrappers. Retry 503 on
// .vtt before onload / onreadystatechange see the failure (so VHS does not disable).
function installNativeXhrVtt503Retry() {
    if (typeof XMLHttpRequest === 'undefined')
        return false;

    if (XMLHttpRequest.prototype.__k7Vtt503Patched)
        return true;

    XMLHttpRequest.prototype.__k7Vtt503Patched = true;

    const nativeOpen = XMLHttpRequest.prototype.open;
    const nativeSend = XMLHttpRequest.prototype.send;
    const nativeSetRequestHeader = XMLHttpRequest.prototype.setRequestHeader;

    XMLHttpRequest.prototype.open = function (method, url, async, user, password) {
        this.__k7Method = method;
        this.__k7Url = url == null ? '' : String(url);
        this.__k7Async = async !== false;
        this.__k7User = user;
        this.__k7Password = password;
        this.__k7Headers = [];
        this.__k7VttHooked = false;
        return nativeOpen.apply(this, arguments);
    };

    XMLHttpRequest.prototype.setRequestHeader = function (name, value) {
        if (this.__k7Headers)
            this.__k7Headers.push([name, value]);

        return nativeSetRequestHeader.apply(this, arguments);
    };

    XMLHttpRequest.prototype.send = function (body) {
        const xhr = this;
        if (!isVttSubtitleUrl(xhr.__k7Url) || xhr.__k7VttHooked)
            return nativeSend.apply(xhr, arguments);

        xhr.__k7VttHooked = true;
        xhr.__k7VttAttempt = 0;
        xhr.__k7VttBody = body;

        const userOnLoad = xhr.onload;
        const userOnError = xhr.onerror;
        const userOnReady = xhr.onreadystatechange;
        const userOnLoadEnd = xhr.onloadend;
        const userOnTimeout = xhr.ontimeout;

        const clearUserHandlers = function () {
            xhr.onload = null;
            xhr.onerror = null;
            xhr.onreadystatechange = null;
            xhr.onloadend = null;
            xhr.ontimeout = null;
        };

        const finishWithUserHandlers = function () {
            xhr.onload = userOnLoad;
            xhr.onerror = userOnError;
            xhr.onreadystatechange = userOnReady;
            xhr.onloadend = userOnLoadEnd;
            xhr.ontimeout = userOnTimeout;

            // Prefer onreadystatechange (video.js sets it and schedules the real callback).
            if (typeof userOnReady === 'function')
                userOnReady.call(xhr);
            else if (xhr.status >= 200 && xhr.status < 300 && typeof userOnLoad === 'function')
                userOnLoad.call(xhr);
            else if (typeof userOnError === 'function')
                userOnError.call(xhr);

            if (typeof userOnLoadEnd === 'function')
                userOnLoadEnd.call(xhr);
        };

        const armHandlers = function () {
            clearUserHandlers();
            xhr.onreadystatechange = function () {
                if (xhr.readyState !== 4)
                    return;

                xhr.__k7VttAttempt += 1;
                if (xhr.status === 503 && xhr.__k7VttAttempt < K7_VTT_503_MAX_ATTEMPTS) {
                    const delay = vtt503RetryDelayMs(xhr.__k7VttAttempt);
                    setTimeout(function () {
                        try {
                            nativeOpen.call(
                                xhr,
                                xhr.__k7Method,
                                xhr.__k7Url,
                                xhr.__k7Async,
                                xhr.__k7User,
                                xhr.__k7Password);
                            const headers = xhr.__k7Headers || [];
                            for (let i = 0; i < headers.length; i++)
                                nativeSetRequestHeader.call(xhr, headers[i][0], headers[i][1]);

                            armHandlers();
                            nativeSend.call(xhr, xhr.__k7VttBody);
                        }
                        catch (e) {
                            finishWithUserHandlers();
                        }
                    }, delay);
                    return;
                }

                finishWithUserHandlers();
            };
        };

        armHandlers();
        return nativeSend.apply(xhr, arguments);
    };

    return true;
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

                const status = getXhrStatusCode(err, response);
                if (status === 503 && attempt < K7_VTT_503_MAX_ATTEMPTS) {
                    pendingTimer = setTimeout(function () {
                        pendingTimer = null;
                        tryRequest();
                    }, vtt503RetryDelayMs(attempt));
                    return;
                }

                callback(err, response);
            });

            if (activeRequest && typeof activeRequest.abort === 'function') {
                const innerAbort = activeRequest.abort.bind(activeRequest);
                request.abort = function () {
                    aborted = true;
                    if (pendingTimer !== null) {
                        clearTimeout(pendingTimer);
                        pendingTimer = null;
                    }
                    innerAbort();
                };
            }
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

    wrappedXhr.__k7Vtt503Retry = true;
    return wrappedXhr;
}

function ensureVtt503RetryXhr() {
    installNativeXhrVtt503Retry();

    if (!window.videojs || !videojs.xhr)
        return false;

    // VHS picks (!0 === Vhs.xhr.original ? videojs : Vhs).xhr.
    // Only wrap videojs.xhr. Wrapping Vhs.xhr and forcing original=false makes the
    // default Vhs xhr factory re-enter itself (no retries, request dies).
    if (!videojs.xhr.__k7Vtt503Retry)
        videojs.xhr = wrapXhrForVtt503Retry(videojs.xhr);

    const vhs = videojs.Vhs || videojs.VHS;
    if (vhs && vhs.xhr)
        vhs.xhr.original = true;

    return true;
}

window.K7 = window.K7 || {};
K7.ensureVtt503RetryXhr = ensureVtt503RetryXhr;

// Native patch installs immediately (before video.js). Module wrap when available.
installNativeXhrVtt503Retry();
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

    // Remux seek keeps the same HLS source; VHS often drops EXT-X-MEDIA text tracks.
    player.on('seeked', function () {
        window.reapplyActiveSubtitleTrack(id);
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
        // Sidecar path (C# passes null): do not disable tracks here. loadSidecar /
        // loadedmetadata re-apply owns text subs. HLS-only slug still waits for VHS.
        if (subtitleSlug)
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
        if (subtitleSlug)
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

    // Remember selection so seek / VHS track resets can re-apply.
    player._k7ActiveSubtitleSlug = slug || null;

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

function isActiveSubtitleShowing(player, slug) {
    if (!player || !slug)
        return false;

    const textTracks = player.textTracks && player.textTracks();
    if (!textTracks)
        return false;

    for (let i = 0; i < textTracks.length; i++) {
        if (!isSelectableTextTrack(textTracks[i]))
            continue;

        if (textTrackMatchesSlug(textTracks[i], slug) && textTracks[i].mode === 'showing')
            return true;
    }

    return false;
}

// VHS often disables EXT-X-MEDIA subs after seek / segment errors. Re-bind the
// last selected slug when the track is no longer showing.
window.reapplyActiveSubtitleTrack = function (id) {
    const player = players[id];
    if (!player)
        return;

    const slug = player._k7ActiveSubtitleSlug;
    if (!slug)
        return;

    if (isActiveSubtitleShowing(player, slug))
        return;

    window.switchSubtitleTrackWhenReady(id, slug);
}

// VHS registers EXT-X-MEDIA text tracks asynchronously; retry until the slug appears.
window.switchSubtitleTrackWhenReady = function (id, slug, maxAttempts) {
    const player = players[id];
    if (!player)
        return;

    player._k7ActiveSubtitleSlug = slug || null;

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

// Mirror of WebVttCueParser.cs - inject cues without a second Video.js XHR.
// blob: + addRemoteTextTrack fails when the media element uses credentials
// (ProgressEvent statusCode 0). preloadTextTracks only delays load, it does not fix that.
function k7ParseVttTimestamp(timestamp) {
    const parts = String(timestamp || '').trim().split(':');
    try {
        if (parts.length === 3)
            return parseFloat(parts[0]) * 3600 + parseFloat(parts[1]) * 60 + parseFloat(parts[2]);
        if (parts.length === 2)
            return parseFloat(parts[0]) * 60 + parseFloat(parts[1]);
    } catch (e) {
    }
    return 0;
}

function k7StripVttCueMarkup(line) {
    let out = '';
    let inTag = false;
    for (let i = 0; i < line.length; i++) {
        const c = line[i];
        if (c === '<') {
            inTag = true;
            continue;
        }
        if (c === '>') {
            inTag = false;
            continue;
        }
        if (!inTag)
            out += c;
    }
    return out
        .replace(/&nbsp;/gi, ' ')
        .replace(/&amp;/gi, '&')
        .replace(/&lt;/gi, '<')
        .replace(/&gt;/gi, '>')
        .trim();
}

function k7ParseWebVttCues(vtt) {
    if (!vtt || typeof vtt !== 'string')
        return [];

    const lines = vtt.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n');
    const cues = [];
    let i = 0;

    while (i < lines.length) {
        if (lines[i].indexOf('-->') !== -1)
            break;
        i++;
    }

    while (i < lines.length) {
        const line = lines[i].trim();
        if (line.indexOf('-->') === -1) {
            i++;
            continue;
        }

        const parts = line.split('-->');
        if (parts.length < 2) {
            i++;
            continue;
        }

        let endPart = parts[1].trim();
        const endSpace = endPart.indexOf(' ');
        if (endSpace > 0)
            endPart = endPart.slice(0, endSpace);

        const start = k7ParseVttTimestamp(parts[0]);
        const end = k7ParseVttTimestamp(endPart);
        i++;

        const textLines = [];
        while (i < lines.length && lines[i].trim().length > 0) {
            const text = k7StripVttCueMarkup(lines[i].replace(/\s+$/, ''));
            if (text.length > 0)
                textLines.push(text);
            i++;
        }

        const body = textLines.join('\n');
        if (end > start && body.length > 0)
            cues.push({ start: start, end: end, text: body });
    }

    return cues;
}

function k7NormalizeWebVttText(text) {
    if (typeof text !== 'string')
        return null;

    // Strip UTF-8 BOM if present.
    if (text.charCodeAt(0) === 0xfeff)
        text = text.slice(1);

    const trimmed = text.replace(/^\uFEFF/, '');
    if (trimmed.indexOf('#EXTM3U') === 0)
        return null;

    if (trimmed.indexOf('WEBVTT') === 0 || trimmed.toLowerCase().indexOf('webvtt') === 0)
        return trimmed;

    return null;
}

function k7EnsureSidecarSourceHook(player, id) {
    if (!player || player._k7SidecarSourceHooked)
        return;

    player._k7SidecarSourceHooked = true;
    // Quality / encode swaps call player.src(). Video.js drops auto remote text tracks
    // on source change - re-apply pending sidecar after the new master is ready.
    player.on('loadedmetadata', function () {
        const pending = player._k7PendingSidecar;
        if (!pending || !pending.slug || !pending.vttUrl)
            return;
        window.loadSidecarSubtitleTrack(id, pending.vttUrl, pending.slug);
    });
}

// Windows MAUI HLS: VHS EXT-X-MEDIA subtitle playlists often never surface cues in WebView2.
// Load the full sidecar VTT (same endpoint as native Direct), then inject cues in-memory.
// Do not set track.src (blob or https): Video.js emulated tracks re-XHR and can fail
// (503 disable, or blob + withCredentials -> ProgressEvent status 0).
window.loadSidecarSubtitleTrack = async function (id, vttUrl, slug) {
    const player = players[id];
    if (!player)
        return false;

    ensurePlatformStreamBridge();
    k7EnsureSidecarSourceHook(player, id);

    const intendedSlug = slug || null;
    if (!intendedSlug || !vttUrl) {
        player._k7PendingSidecar = null;
        player._k7SidecarVttCache = null;
    } else {
        player._k7PendingSidecar = { vttUrl: vttUrl, slug: intendedSlug };
    }

    if (player._k7SidecarLoadToken)
        player._k7SidecarLoadToken += 1;
    else
        player._k7SidecarLoadToken = 1;
    const loadToken = player._k7SidecarLoadToken;

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
    player._k7ActiveSubtitleSlug = intendedSlug;

    if (!intendedSlug || !vttUrl)
        return true;

    const fetchVttText = async function () {
        if (player._k7SidecarVttCache
            && player._k7SidecarVttCache.url === vttUrl
            && typeof player._k7SidecarVttCache.text === 'string') {
            return player._k7SidecarVttCache.text;
        }

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
                const normalized = k7NormalizeWebVttText(body);
                if (normalized)
                    return normalized;
                // Bridge may return base64 for byte[]; decode if it does not look like WEBVTT.
                try {
                    const binary = atob(body);
                    return k7NormalizeWebVttText(new TextDecoder('utf-8').decode(
                        Uint8Array.from(binary, function (c) { return c.charCodeAt(0); })));
                } catch (e) {
                    return k7NormalizeWebVttText(body);
                }
            }

            if (body instanceof ArrayBuffer)
                return k7NormalizeWebVttText(new TextDecoder('utf-8').decode(new Uint8Array(body)));

            if (body instanceof Uint8Array)
                return k7NormalizeWebVttText(new TextDecoder('utf-8').decode(body));

            if (Array.isArray(body))
                return k7NormalizeWebVttText(new TextDecoder('utf-8').decode(new Uint8Array(body)));

            return null;
        }

        return null;
    };

    try {
        const text = await fetchVttText();
        if (player._k7SidecarLoadToken !== loadToken)
            return false;

        if (!text) {
            console.warn('loadSidecarSubtitleTrack: no WEBVTT body for', vttUrl);
            return false;
        }

        player._k7SidecarVttCache = { url: vttUrl, text: text };

        const cues = k7ParseWebVttCues(text);
        if (cues.length === 0) {
            console.warn('loadSidecarSubtitleTrack: zero cues for', vttUrl);
            return false;
        }

        // No src: avoid Video.js TextTrack XHR (blob fails with credentials, https can 503 once).
        // manualCleanup true: quality/encode src swaps must not auto-drop the sidecar before
        // loadedmetadata can re-apply (we remove k7-sidecar-* ourselves).
        const handle = player.addRemoteTextTrack({
            kind: 'subtitles',
            srclang: 'und',
            label: intendedSlug,
            id: 'k7-sidecar-' + intendedSlug,
            mode: 'showing',
            default: true
        }, true);

        if (player._k7SidecarLoadToken !== loadToken) {
            try {
                if (handle)
                    player.removeRemoteTextTrack(handle.track || handle);
            } catch (e) {
            }
            return false;
        }

        const track = handle && (handle.track || handle);
        if (!track) {
            console.warn('loadSidecarSubtitleTrack: no text track handle');
            return false;
        }

        if (!track.id)
            track.id = 'k7-sidecar-' + intendedSlug;

        const CueType = typeof VTTCue !== 'undefined'
            ? VTTCue
            : (typeof TextTrackCue !== 'undefined' ? TextTrackCue : null);
        if (!CueType) {
            console.warn('loadSidecarSubtitleTrack: VTTCue unavailable');
            return false;
        }

        for (let i = 0; i < cues.length; i++) {
            try {
                track.addCue(new CueType(cues[i].start, cues[i].end, cues[i].text));
            } catch (cueErr) {
            }
        }

        track.mode = 'showing';
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
        // seeked handler also re-applies; call once here for players that skip seeked
        // when the target equals the current time within tolerance.
        setTimeout(function () {
            window.reapplyActiveSubtitleTrack(id);
        }, 0);
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
