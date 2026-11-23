let players = {};

// Match AndroidExoHlsTuning: VHS disables subtitle tracks on the first segment error.
// Server returns 503 while ffmpeg extracts the sidecar VTT; retry before VHS sees it.
// 500 ms, 1 s, 2 s, 4 s, then 8 s per attempt: 80 attempts cover about ten minutes of 503,
// the same bound as SubtitleVttRetryPolicy on the native clients (a large remux extraction).
const K7_VTT_503_MAX_ATTEMPTS = 80;
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

// WebKitGTK's user agent ends in "Version/x Safari/605.1.15", so Video.js reports
// browser.IS_ANY_SAFARI (and, with touch events present, IS_IPAD / IS_IOS). VHS then attaches
// MSE the Safari way: two <source> children (the MediaSource blob URL, then the manifest as
// Safari's native-HLS fallback) instead of video.src = blob. Inside the Flatpak runtime that
// left the element on the manifest in NETWORK_NO_SOURCE with no MediaError and no request
// (endless spinner). Hand VHS a browser table without the Safari / iOS flags so it takes the
// video.src path like Chromium, Firefox and WebView2 (videojs.browser is a frozen module
// namespace: the object is replaced, its keys cannot be).
function applyLinuxGtkVideoJsQuirks() {
    if (!window.K7_LINUX_GTK || !window.videojs || !videojs.browser || videojs.browser.__k7LinuxGtk)
        return;

    var patched = {};
    Object.keys(videojs.browser).forEach(function (key) {
        patched[key] = videojs.browser[key];
    });
    patched.IS_SAFARI = false;
    patched.IS_ANY_SAFARI = false;
    patched.IS_IPAD = false;
    patched.IS_IOS = false;
    patched.__k7LinuxGtk = true;
    videojs.browser = patched;
}

window.initVideoJs = function (id, videoPlayer, videoContainer, options, dotNetRef) {
    ensurePlatformStreamBridge();
    applyLinuxGtkVideoJsQuirks();
    // The Blazor element reference can be stale after a native-player session blanked the
    // surfaces (MAUI hosts): fall back to the live <video> in the container.
    if (!videoPlayer || !videoPlayer.isConnected) {
        videoPlayer = (videoContainer && videoContainer.querySelector && videoContainer.querySelector('video'))
            || document.getElementById('video-player');
        if (!videoPlayer) {
            console.warn('initVideoJs: no <video> element available for', id);
            return;
        }
    }
    // If a player already exists for this id, dispose it first to avoid duplicate streams/listeners
    if (players[id]) {
        k7InvalidateSidecar(players[id]);
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
        // VHS (PlaylistController loadedmetadata) starts the main segment loader at t=0
        // for VOD whenever preload is not 'none', BEFORE setupFirstPlay seeks to
        // #EXT-X-START. On a resume that requested audio/video segment 0 (30s server
        // wait on a segment the remux head at the landing never produces) before the
        // real landing. With 'none' VHS loads the master on play(), then seeks, then loads.
        preload: 'none',
        html5: {
            ...(options?.html5 ?? {}),
            nativeTextTracks: false,
            // WebKitGTK exposes native audioTracks / videoTracks on <video>. With them, VHS
            // refuses overrideNative ("Overriding native VHS requires emulated tracks"), which
            // the Video.js docs resolve with emulated tracks.
            ...(window.K7_LINUX_GTK ? { nativeAudioTracks: false, nativeVideoTracks: false } : {}),
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

    const onFullscreenChange = function () {
        dotNetRef.invokeMethodAsync('OnFullscreenChanged', document.fullscreenElement === videoContainer)
            .catch(error => console.error('Error invoking OnFullscreenChanged', error));
    };
    document.addEventListener('fullscreenchange', onFullscreenChange);
    // Every disposal path (disposeVideoJs, re-init, surface blanking) fires 'dispose': without
    // this, each past playback kept a document listener calling a released DotNetObjectReference.
    player.on('dispose', function () {
        document.removeEventListener('fullscreenchange', onFullscreenChange);
    });

    players[id] = player;
    k7AttachSubtitleStyleHooks(player);
    window.showVideoJs(id);
    return player;
}

window.pauseAllK7Video = function () {
    Object.keys(players || {}).forEach(function (id) {
        try { players[id].pause(); } catch (e) { }
    });
    document.querySelectorAll('video').forEach(function (video) {
        try { video.pause(); } catch (e2) { }
    });
};

window.playAllK7Video = function () {
    Object.keys(players || {}).forEach(function (id) {
        try { players[id].play(); } catch (e) { }
    });
};

window.blankK7VideoSurfaces = function () {
    Object.keys(players || {}).forEach(function (id) {
        try {
            window.disposeVideoJs(id);
        } catch (e0) { }
    });
    document.querySelectorAll('video').forEach(function (video) {
        try {
            video.pause();
            video.removeAttribute('src');
            video.removeAttribute('poster');
            while (video.firstChild)
                video.removeChild(video.firstChild);
            video.load();
        } catch (e3) { }
        video.style.display = 'none';
        video.style.visibility = 'hidden';
        video.style.opacity = '0';
        video.style.background = '#0d0907';
    });
    document.querySelectorAll('.video-container, .video-js, .vjs-error-display, .vjs-modal-dialog, .vjs-poster').forEach(function (node) {
        if (!node || !node.style)
            return;
        node.style.display = 'none';
        node.style.visibility = 'hidden';
        node.style.opacity = '0';
        node.style.background = '#0d0907';
    });
};

// Lightweight diagnostics sink shared with Blazor (k7PlayerLog). Off unless
// window.K7_PLAYER_DEBUG = true in the console.
window.k7PlayerLog = function (event, detail) {
    if (window.K7_PLAYER_DEBUG !== true)
        return;
    if (detail === undefined)
        console.log('[K7:player]', event);
    else
        console.log('[K7:player]', event, detail);
};

function k7ShowVideoNode(node) {
    if (!node || !node.style)
        return;

    node.style.display = '';
    node.style.visibility = '';
    node.style.opacity = '';
    node.style.background = '';
}

// Undo hideVideoJs on the same player (re-init / new source / play). hideVideoJs used
// to hide every .video-container on the page and nothing ever showed them again.
window.showVideoJs = function (id) {
    var player = players[id];
    try {
        if (player && player.el()) {
            k7ShowVideoNode(player.el());
            var container = player.el().closest && player.el().closest('.video-container');
            k7ShowVideoNode(container);
        }
    } catch (e) {
    }
};

// Hide only this player. Never call blankK7VideoSurfaces here: it dispose()s every
// Video.js instance, including the one a resume is about to use. Real teardown is
// disposeVideoJs once IsVisible is false.
window.hideVideoJs = function (id) {
    var player = players[id];
    var hideNode = function (node) {
        if (!node || !node.style)
            return;
        node.style.display = 'none';
        node.style.visibility = 'hidden';
        node.style.opacity = '0';
        node.style.background = '#0d0907';
    };
    try {
        if (player) {
            try { player.error(null); } catch (e) { }
            try { player.pause(); } catch (e2) { }
            hideNode(player.el());
            var container = player.el() && player.el().closest && player.el().closest('.video-container');
            hideNode(container);
        }
    } catch (e3) {
    }
};

window.disposeVideoJs = function (id) {
    const player = players[id];
    if (player) {
        // An in-flight sidecar VTT fetch must not land on a disposed player.
        k7InvalidateSidecar(player);
        window.k7PgsStop(id);
        try {
            // Clear the default "media could not be loaded" poster before dispose.
            // A late error after close otherwise stays painted on the WebView.
            try {
                player.error(null);
            } catch (clearErr) {
            }
            var el = player.el();
            if (el) {
                el.style.display = 'none';
                el.querySelectorAll('.vjs-error-display, .vjs-modal-dialog, .vjs-poster').forEach(function (node) {
                    node.style.display = 'none';
                });
            }
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
        window.showVideoJs(id);
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
        const savedRate = player.playbackRate() || 1;
        window.showVideoJs(id);
        player.src({ src: src, type: normalizedType });
        player.ready(function () {
            if (savedRate && Math.abs(savedRate - 1) > 0.01)
                player.playbackRate(savedRate);
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

    // The server emits #EXT-X-START:TIME-OFFSET=<resume> on the media playlists when the
    // URL carries startSeconds. Video.js VHS honours it itself: setupFirstPlay() calls
    // setCurrentTime(TIME-OFFSET) on the first play() and only then starts loading
    // segments. Seeking here as well produced two or three back-to-back seeks on the
    // same MSE (ours, then VHS, then a correction), and Firefox never completed the
    // last one (seeking stuck with readyState HAVE_METADATA while data was buffered).
    // Own seek only when the playlist cannot anchor the position (no startSeconds).
    const playlistAnchorsStart = /[?&]startSeconds=/.test(src);
    window.showVideoJs(id);
    const savedRate = player.playbackRate() || 1;

    let seekApplied = false;
    const applySeekAndPlay = function () {
        if (seekApplied) return;
        seekApplied = true;
        if (!playlistAnchorsStart && Math.abs(player.currentTime() - seekTime) > 0.5)
            player.currentTime(seekTime);

        if (savedRate && Math.abs(savedRate - 1) > 0.01)
            player.playbackRate(savedRate);

        var promise = player.play();
        if (promise !== undefined) {
            promise.catch(function (error) {
                console.warn('Auto-play was prevented after seek', error);
            });
        }
        if (subtitleSlug)
            window.switchSubtitleTrackWhenReady(id, subtitleSlug);
    };

    player.one('loadedmetadata', applySeekAndPlay);
    // Safety net for techs that ignore #EXT-X-START: correct once real media arrived,
    // never while a seek is still in flight (a second seek aborts the VHS fetch).
    player.one('loadeddata', function () {
        if (!seekApplied)
            applySeekAndPlay();

        if (!player.seeking() && Math.abs(player.currentTime() - seekTime) > 1.5)
            player.currentTime(seekTime);
    });
    player.src({ src: src, type: normalizedType });

    // preload 'none': VHS loads the master only on play(). Kick it once so
    // loadedmetadata (and setupFirstPlay's seek to #EXT-X-START) can happen. A
    // rejected promise (autoplay policy) leaves the player paused: the overlay Play
    // button then runs the same first-play path.
    var kick = player.play();
    if (kick !== undefined) {
        kick.catch(function () {
        });
    }
}

window.switchAudioTrack = function (id, trackName) {
    const player = players[id];
    if (!player) return false;

    const audioTracks = player.audioTracks();
    if (!audioTracks || !audioTracks.length) return false;

    let targetIndex = -1;
    if (typeof trackName === 'string') {
        for (let i = 0; i < audioTracks.length; i++) {
            const label = audioTracks[i].label || '';
            const trackId = audioTracks[i].id != null ? String(audioTracks[i].id) : '';
            if (label === trackName || trackId.indexOf(trackName) !== -1) {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0 && trackName.indexOf('audio-') === 0) {
            const parsed = parseInt(trackName.slice(6), 10);
            if (!isNaN(parsed)) {
                for (let i = 0; i < audioTracks.length; i++) {
                    const trackId = audioTracks[i].id != null ? String(audioTracks[i].id) : '';
                    if (trackId === String(parsed) || trackId.endsWith('-' + parsed)) {
                        targetIndex = i;
                        break;
                    }
                }
                if (targetIndex < 0 && parsed >= 0 && parsed < audioTracks.length)
                    targetIndex = parsed;
            }
        }
    }

    if (targetIndex < 0) return false;

    for (let i = 0; i < audioTracks.length; i++)
        audioTracks[i].enabled = i === targetIndex;
    return true;
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

    // Sidecar VTT (Web / Windows Video.js): keep the existing cue set after seek.
    // Re-injecting without remove stacked duplicate tracks and looked like an offset.
    const pending = player._k7PendingSidecar;
    if (pending && pending.vttUrl && pending.slug) {
        if (!k7SidecarPlayerIsLive(player, id))
            return;
        if (k7EnsureSidecarTrackShowing(player, pending.slug))
            return;
        k7RemoveSidecarRemoteTracks(player);
        k7FetchAndInjectSidecarVtt(
            player,
            id,
            pending.vttUrl,
            pending.slug,
            player._k7SidecarLoadToken).catch(function () {});
        return;
    }

    const slug = player._k7ActiveSubtitleSlug;
    if (!slug)
        return;

    if (isActiveSubtitleShowing(player, slug))
        return;

    window.switchSubtitleTrackWhenReady(id, slug);
}

function k7RemoveSidecarRemoteTracks(player) {
    if (!player)
        return;
    try {
        const remoteTracks = player.remoteTextTracks && player.remoteTextTracks();
        if (!remoteTracks)
            return;
        for (let i = remoteTracks.length - 1; i >= 0; i--) {
            const track = remoteTracks[i];
            if (track && track.id && String(track.id).indexOf('k7-sidecar-') === 0)
                player.removeRemoteTextTrack(track);
        }
    } catch (e) {
    }
}

function k7EnsureSidecarTrackShowing(player, slug) {
    if (!player || !slug)
        return false;

    const sidecarId = 'k7-sidecar-' + slug;
    const textTracks = player.textTracks && player.textTracks();
    if (!textTracks)
        return false;

    let match = null;
    for (let i = 0; i < textTracks.length; i++) {
        const track = textTracks[i];
        if (!track || track.id !== sidecarId)
            continue;
        if (track.cues && track.cues.length > 0) {
            match = track;
            break;
        }
    }

    if (!match)
        return false;

    for (let i = 0; i < textTracks.length; i++) {
        if (!isSelectableTextTrack(textTracks[i]))
            continue;
        textTracks[i].mode = textTracks[i] === match ? 'showing' : 'disabled';
    }

    return true;
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

function k7InvalidateSidecar(player) {
    if (!player)
        return;
    player._k7SidecarLoadToken = (player._k7SidecarLoadToken || 0) + 1;
    player._k7PendingSidecar = null;
}

function k7SidecarPlayerIsLive(player, id) {
    if (!player || players[id] !== player)
        return false;
    try {
        if (typeof player.isDisposed === 'function' && player.isDisposed())
            return false;
        if (!player.el())
            return false;
        if (typeof player.tech === 'function'
            && !player.tech({ IWillNotUseThisInPlugins: true }))
            return false;
    } catch (e) {
        return false;
    }
    return true;
}

async function k7WaitForSidecarHost(player, id, loadToken) {
    for (let i = 0; i < 50; i++) {
        if (player._k7SidecarLoadToken !== loadToken)
            return false;
        if (k7SidecarPlayerIsLive(player, id) && player.readyState() >= 1)
            return true;
        await new Promise(function (resolve) { setTimeout(resolve, 50); });
    }
    return k7SidecarPlayerIsLive(player, id) && player.readyState() >= 1;
}

function k7EnsureSidecarSourceHook(player, id) {
    if (!player || player._k7SidecarSourceHooked)
        return;

    player._k7SidecarSourceHooked = true;
    // Quality / encode swaps call player.src(). Video.js drops auto remote text tracks
    // on source change - re-apply pending sidecar after the new master is ready.
    // Do not call loadSidecarSubtitleTrack here: that bumps the load token and
    // addRemoteTextTrack during loadTech_ throws (Invalid target for one).
    player.on('loadedmetadata', function () {
        const pending = player._k7PendingSidecar;
        if (!pending || !pending.slug || !pending.vttUrl)
            return;
        if (!k7SidecarPlayerIsLive(player, id))
            return;
        if (k7EnsureSidecarTrackShowing(player, pending.slug))
            return;
        k7RemoveSidecarRemoteTracks(player);
        k7FetchAndInjectSidecarVtt(
            player,
            id,
            pending.vttUrl,
            pending.slug,
            player._k7SidecarLoadToken).catch(function () {});
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
        k7RemoveSidecarRemoteTracks(player);
    } catch (e) {
    }

    // Prefer sidecar over HLS group tracks so we do not race two sources.
    window.switchSubtitleTrack(id, null);
    player._k7ActiveSubtitleSlug = intendedSlug;

    if (!intendedSlug || !vttUrl)
        return true;

    // Return before 503 retries so Blazor InvokeVoidAsync does not gate A/V play.
    k7FetchAndInjectSidecarVtt(player, id, vttUrl, intendedSlug, loadToken).catch(function () {});
    return true;
};

// Sidecar VTT polling, kept in step with SubtitleVttRetryPolicy (native clients): the
// server answers 503 while it extracts the track.
const K7_SIDECAR_FAST_ATTEMPTS = 15;
const K7_SIDECAR_FAST_DELAY_MS = 1000;
const K7_SIDECAR_SLOW_DELAY_MS = 3000;
const K7_SIDECAR_MAX_WAIT_MS = 10 * 60 * 1000;

// Same marker as SubtitleVttFormat.PartialNote: the body only holds the cues around the
// position sent with the request, the full track is still being extracted.
const K7_SIDECAR_PARTIAL_NOTE = 'NOTE k7-partial';
const K7_SIDECAR_PARTIAL_REFRESH_MS = 5000;
const K7_SIDECAR_MAX_REFRESH_FAILURES = 5;

function k7IsPartialSidecarVtt(text) {
    return typeof text === 'string' && text.slice(0, 64).indexOf(K7_SIDECAR_PARTIAL_NOTE) !== -1;
}

// The position lets the server answer with the cues around it before the whole track is
// extracted. Cue times stay on the media timeline either way.
function k7SidecarRequestUrl(player, vttUrl) {
    let position = 0;
    try {
        position = Number(player.currentTime()) || 0;
    } catch (e) {
    }
    if (!isFinite(position) || position < 0)
        position = 0;
    return vttUrl + (vttUrl.indexOf('?') === -1 ? '?' : '&') + 'position=' + position.toFixed(1);
}

function k7DecodeSidecarVttBody(body) {
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

// Fetches the sidecar VTT, waiting through 503 (extraction in progress) for up to 10 minutes.
// Returns the WEBVTT text, or null on failure or when isStale() turns true.
async function k7FetchSidecarVttText(player, vttUrl, isStale) {
    const startedAt = Date.now();
    for (let attempt = 1; ; attempt++) {
        const requestUrl = k7SidecarRequestUrl(player, vttUrl);
        let status = 0;
        let body = null;

        if (window.K7 && K7._windowsStreamFetchRef) {
            const result = await K7._windowsStreamFetchRef.invokeMethodAsync(
                'FetchStreamAsync',
                requestUrl,
                null);
            if (result) {
                status = result.statusCode || 0;
                body = result.body;
            }
        } else {
            const response = await fetch(requestUrl, { credentials: 'include' });
            status = response.status;
            if (status >= 200 && status < 300)
                body = await response.text();
        }

        if (isStale())
            return null;

        if (status === 503) {
            if (Date.now() - startedAt >= K7_SIDECAR_MAX_WAIT_MS)
                return null;
            const delay = attempt <= K7_SIDECAR_FAST_ATTEMPTS
                ? K7_SIDECAR_FAST_DELAY_MS
                : K7_SIDECAR_SLOW_DELAY_MS;
            await new Promise(function (resolve) { setTimeout(resolve, delay); });
            if (isStale())
                return null;
            continue;
        }

        if (status < 200 || status >= 300 || body == null)
            return null;

        return k7DecodeSidecarVttBody(body);
    }
}

// Adds the cues the track does not hold yet. seen remembers what was added, so a refreshed
// partial VTT (which repeats the earlier cues) never stacks duplicates.
function k7AddSidecarCues(track, cues, seen) {
    const CueType = typeof VTTCue !== 'undefined'
        ? VTTCue
        : (typeof TextTrackCue !== 'undefined' ? TextTrackCue : null);
    if (!CueType) {
        console.warn('loadSidecarSubtitleTrack: VTTCue unavailable');
        return false;
    }

    for (let i = 0; i < cues.length; i++) {
        const key = cues[i].start + '|' + cues[i].end + '|' + cues[i].text;
        if (seen.has(key))
            continue;
        seen.add(key);
        try {
            track.addCue(new CueType(cues[i].start, cues[i].end, cues[i].text));
        } catch (cueErr) {
        }
    }

    return true;
}

// Creates the showing k7-sidecar-* track and fills it. Returns the track, or null.
async function k7InjectSidecarTrack(player, id, intendedSlug, loadToken, isStale, cues, seen) {
    if (!await k7WaitForSidecarHost(player, id, loadToken) || isStale())
        return null;

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

    if (isStale()) {
        try {
            if (handle)
                player.removeRemoteTextTrack(handle.track || handle);
        } catch (e) {
        }
        return null;
    }

    const track = handle && (handle.track || handle);
    if (!track) {
        console.warn('loadSidecarSubtitleTrack: no text track handle');
        return null;
    }

    if (!track.id)
        track.id = 'k7-sidecar-' + intendedSlug;

    if (!k7AddSidecarCues(track, cues, seen))
        return null;

    track.mode = 'showing';
    k7AttachSubtitleStyleHooks(player);
    k7RefreshSubtitleStylesForAllPlayers();
    return track;
}

// Resolves after ms, or as soon as the user seeks: a seek usually lands outside the cues
// of a partial VTT, so the next request must not wait for the timer.
function k7WaitForSidecarRefresh(player, ms) {
    return new Promise(function (resolve) {
        let done = false;
        let timer = 0;
        const finish = function () {
            if (done)
                return;
            done = true;
            clearTimeout(timer);
            try {
                player.off('seeked', finish);
            } catch (e) {
            }
            resolve();
        };
        timer = setTimeout(finish, ms);
        try {
            player.on('seeked', finish);
        } catch (e) {
        }
    });
}

async function k7FetchAndInjectSidecarVtt(player, id, vttUrl, intendedSlug, loadToken) {
    // One loader per player: a later call (re-apply after a seek or a source swap) takes
    // over from the one still polling.
    player._k7SidecarRunId = (player._k7SidecarRunId || 0) + 1;
    const runId = player._k7SidecarRunId;
    const isStale = function () {
        return player._k7SidecarLoadToken !== loadToken || player._k7SidecarRunId !== runId;
    };

    try {
        const cached = player._k7SidecarVttCache;
        let text = cached && cached.url === vttUrl && typeof cached.text === 'string'
            ? cached.text
            : await k7FetchSidecarVttText(player, vttUrl, isStale);
        if (isStale())
            return false;

        if (!text) {
            console.warn('loadSidecarSubtitleTrack: no WEBVTT body for', vttUrl);
            return false;
        }

        let partial = k7IsPartialSidecarVtt(text);
        // Only the complete track is reusable as is after a seek or a source swap.
        if (!partial)
            player._k7SidecarVttCache = { url: vttUrl, text: text };

        let cues = k7ParseWebVttCues(text);
        // A partial VTT may hold no cue yet (no dialogue around the position): the track is
        // still created so the cues of the next answers have a place to go.
        if (cues.length === 0 && !partial) {
            console.warn('loadSidecarSubtitleTrack: zero cues for', vttUrl);
            return false;
        }

        let seen = new Set();
        let track = await k7InjectSidecarTrack(player, id, intendedSlug, loadToken, isStale, cues, seen);
        if (!track)
            return false;

        // Partial answer: ask again with the current position until the server returns the
        // complete track, adding the new cues as playback moves on.
        let failures = 0;
        while (partial) {
            await k7WaitForSidecarRefresh(player, K7_SIDECAR_PARTIAL_REFRESH_MS);
            if (isStale() || !k7SidecarPlayerIsLive(player, id))
                return false;

            const next = await k7FetchSidecarVttText(player, vttUrl, isStale);
            if (isStale())
                return false;

            if (!next) {
                failures++;
                if (failures >= K7_SIDECAR_MAX_REFRESH_FAILURES)
                    return true;
                continue;
            }

            failures = 0;
            if (next === text)
                continue;

            text = next;
            cues = k7ParseWebVttCues(text);
            partial = k7IsPartialSidecarVtt(text);
            if (partial) {
                k7AddSidecarCues(track, cues, seen);
                continue;
            }

            // Complete track: rebuild the text track from it, as a load without partial
            // answers would have done.
            player._k7SidecarVttCache = { url: vttUrl, text: text };
            if (cues.length === 0)
                return true;
            k7RemoveSidecarRemoteTracks(player);
            seen = new Set();
            track = await k7InjectSidecarTrack(player, id, intendedSlug, loadToken, isStale, cues, seen);
            if (!track)
                return false;
        }

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

// ---- Client-drawn PGS (Blu-ray bitmap) subtitles ------------------------------------
// The .sup sidecar is fetched and decoded in C# (PgsSubtitleLoader / PgsParser), which
// pushes each screen here. This side keeps them ordered and paints, on a canvas over the
// video, the screen active at the player clock. Mirror of PgsTimeline.cs.
const k7PgsStates = {};
const K7_PGS_KEEP_BEHIND_SECONDS = 120;

function k7PgsEnsureCanvas(player) {
    const root = player.el();
    if (!root)
        return null;

    let canvas = root.querySelector('canvas.k7-pgs-layer');
    if (canvas)
        return canvas;

    canvas = document.createElement('canvas');
    canvas.className = 'k7-pgs-layer';
    canvas.style.position = 'absolute';
    canvas.style.left = '0';
    canvas.style.top = '0';
    canvas.style.width = '100%';
    canvas.style.height = '100%';
    canvas.style.pointerEvents = 'none';

    // Above the picture and the text cues, below spinner / controls.
    const textDisplay = root.querySelector('.vjs-text-track-display');
    if (textDisplay && textDisplay.parentNode === root)
        root.insertBefore(canvas, textDisplay.nextSibling);
    else
        root.appendChild(canvas);
    return canvas;
}

function k7PgsFrameAt(state, seconds) {
    // A screen stays up until the next one, but never past what has loaded: the clear
    // that ends it may not have arrived yet.
    if (seconds > state.loadedUntil)
        return null;

    const frames = state.frames;
    let low = 0;
    let high = frames.length - 1;
    let found = null;
    while (low <= high) {
        const middle = (low + high) >> 1;
        if (frames[middle].time <= seconds) {
            found = frames[middle];
            low = middle + 1;
        } else {
            high = middle - 1;
        }
    }

    return found && found.objects.length > 0 ? found : null;
}

function k7PgsPrune(state, seconds) {
    const threshold = seconds - K7_PGS_KEEP_BEHIND_SECONDS;
    let remove = 0;
    while (remove + 1 < state.frames.length && state.frames[remove + 1].time <= threshold)
        remove++;
    if (remove > 0)
        state.frames.splice(0, remove);
}

function k7PgsObjectBitmap(object) {
    if (!object.bitmap) {
        const bitmap = document.createElement('canvas');
        bitmap.width = object.width;
        bitmap.height = object.height;
        bitmap.getContext('2d').putImageData(
            new ImageData(object.rgba, object.width, object.height), 0, 0);
        object.bitmap = bitmap;
    }

    return object.bitmap;
}

function k7PgsPaint(state, player, frame) {
    const root = player.el();
    const canvas = state.canvas;
    if (!root || !canvas)
        return;

    const boxWidth = root.clientWidth;
    const boxHeight = root.clientHeight;
    const ratio = window.devicePixelRatio || 1;
    const pixelWidth = Math.max(1, Math.round(boxWidth * ratio));
    const pixelHeight = Math.max(1, Math.round(boxHeight * ratio));
    if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;
    }

    const context = canvas.getContext('2d');
    context.clearRect(0, 0, canvas.width, canvas.height);
    if (!frame || boxWidth <= 0 || boxHeight <= 0)
        return;

    // Picture rectangle inside the player, for the current object-fit.
    const video = root.querySelector('video');
    const videoWidth = (video && video.videoWidth) || frame.planeWidth;
    const videoHeight = (video && video.videoHeight) || frame.planeHeight;
    const fit = (video && getComputedStyle(video).objectFit) || 'contain';
    let pictureWidth = boxWidth;
    let pictureHeight = boxHeight;
    if (fit !== 'fill') {
        const scale = fit === 'cover'
            ? Math.max(boxWidth / videoWidth, boxHeight / videoHeight)
            : Math.min(boxWidth / videoWidth, boxHeight / videoHeight);
        pictureWidth = videoWidth * scale;
        pictureHeight = videoHeight * scale;
    }

    // The subtitle plane (often 16:9) matches the picture along its uncropped side, so
    // lines authored in a letterbox stay under a cinema-scope picture.
    let scaleX = pictureWidth / frame.planeWidth;
    let scaleY = pictureHeight / frame.planeHeight;
    if (fit !== 'fill')
        scaleX = scaleY = Math.max(scaleX, scaleY);
    const planeLeft = (boxWidth - frame.planeWidth * scaleX) / 2;
    const planeTop = (boxHeight - frame.planeHeight * scaleY) / 2;

    context.imageSmoothingEnabled = true;
    context.imageSmoothingQuality = 'high';
    for (let i = 0; i < frame.objects.length; i++) {
        const object = frame.objects[i];
        let width = object.width * scaleX;
        let height = object.height * scaleY;
        // Keep the object inside the player (Fill crop, or a short player).
        const shrink = Math.min(1, boxWidth / width, boxHeight / height);
        width *= shrink;
        height *= shrink;
        let left = planeLeft + object.x * scaleX;
        let top = planeTop + object.y * scaleY;
        left = Math.min(Math.max(left, 0), boxWidth - width);
        top = Math.min(Math.max(top, 0), boxHeight - height);
        context.drawImage(
            k7PgsObjectBitmap(object),
            left * ratio, top * ratio, width * ratio, height * ratio);
    }
}

function k7PgsTick(id) {
    const state = k7PgsStates[id];
    if (!state)
        return;

    state.raf = requestAnimationFrame(function () { k7PgsTick(id); });

    const player = players[id];
    if (!player || (typeof player.isDisposed === 'function' && player.isDisposed()))
        return;

    let seconds = 0;
    try {
        seconds = player.currentTime() || 0;
    } catch (e) {
        return;
    }

    const frame = k7PgsFrameAt(state, seconds);
    const root = player.el();
    const video = root && root.querySelector('video');
    const layout = root
        ? root.clientWidth + 'x' + root.clientHeight
            + ':' + (video ? video.videoWidth + 'x' + video.videoHeight + ':' + video.style.objectFit : '')
        : '';
    if (frame === state.shown && layout === state.layout)
        return;

    if (state.shown && state.shown !== frame) {
        for (let i = 0; i < state.shown.objects.length; i++)
            state.shown.objects[i].bitmap = null;
    }

    state.shown = frame;
    state.layout = layout;
    k7PgsPaint(state, player, frame);
    k7PgsPrune(state, seconds);
}

window.k7PgsStart = function (id) {
    window.k7PgsStop(id);
    const player = players[id];
    if (!player)
        return false;

    const canvas = k7PgsEnsureCanvas(player);
    if (!canvas)
        return false;

    k7PgsStates[id] = { canvas: canvas, frames: [], loadedUntil: 0, shown: null, layout: '', raf: 0 };
    k7PgsTick(id);
    return true;
};

window.k7PgsStop = function (id) {
    const state = k7PgsStates[id];
    if (!state)
        return;

    cancelAnimationFrame(state.raf);
    delete k7PgsStates[id];
    try {
        if (state.canvas && state.canvas.parentNode)
            state.canvas.parentNode.removeChild(state.canvas);
    } catch (e) {
    }
};

window.k7PgsReset = function (id) {
    const state = k7PgsStates[id];
    if (!state)
        return;

    state.frames = [];
    state.loadedUntil = 0;
};

window.k7PgsSetLoadedUntil = function (id, seconds) {
    const state = k7PgsStates[id];
    if (state && seconds > state.loadedUntil)
        state.loadedUntil = seconds;
};

// layout: x, y, width, height per object (a PGS composition holds at most two).
window.k7PgsAddFrame = function (id, time, planeWidth, planeHeight, layout, rgba0, rgba1) {
    const state = k7PgsStates[id];
    if (!state || !planeWidth || !planeHeight)
        return;

    const pixels = [rgba0, rgba1];
    const objects = [];
    for (let i = 0; i * 4 + 3 < (layout ? layout.length : 0); i++) {
        const width = layout[i * 4 + 2];
        const height = layout[i * 4 + 3];
        const rgba = pixels[i];
        if (!rgba || width <= 0 || height <= 0 || rgba.length < width * height * 4)
            continue;

        objects.push({
            x: layout[i * 4],
            y: layout[i * 4 + 1],
            width: width,
            height: height,
            // Copy: the interop buffer is not ours to keep.
            rgba: new Uint8ClampedArray(rgba.subarray(0, width * height * 4)),
            bitmap: null
        });
    }

    const frame = { time: time, planeWidth: planeWidth, planeHeight: planeHeight, objects: objects };
    let index = state.frames.length;
    while (index > 0 && state.frames[index - 1].time > time)
        index--;
    state.frames.splice(index, 0, frame);
    if (time > state.loadedUntil)
        state.loadedUntil = time;
};
