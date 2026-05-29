// K7 Chromecast integration via Google Cast SDK for Web
// Uses the Chrome/Edge built-in Cast sender API

let castContext = null;
let currentSession = null;
let dotNetRef = null;
let mediaStatusInterval = null;

window.K7Cast = {
    init: function (ref) {
        dotNetRef = ref;

        if (!window.chrome || !window.chrome.cast) {
            console.warn('Google Cast SDK not available');
            return false;
        }

        const sessionRequest = new chrome.cast.SessionRequest(
            chrome.cast.media.DEFAULT_MEDIA_RECEIVER_APP_ID
        );

        const apiConfig = new chrome.cast.ApiConfig(
            sessionRequest,
            sessionListener,
            receiverListener
        );

        chrome.cast.initialize(apiConfig, onInitSuccess, onInitError);
        return true;
    },

    requestSession: async function () {
        return new Promise((resolve, reject) => {
            chrome.cast.requestSession(
                (session) => {
                    currentSession = session;
                    notifyStateChanged(true);
                    resolve(true);
                },
                (error) => {
                    console.error('Cast session request failed:', error);
                    resolve(false);
                }
            );
        });
    },

    castMedia: function (url, contentType, title, subtitle, thumbnailUrl, duration, startPosition) {
        if (!currentSession) return false;

        const mediaInfo = new chrome.cast.media.MediaInfo(url, contentType);
        mediaInfo.streamType = chrome.cast.media.StreamType.BUFFERED;

        if (duration) {
            mediaInfo.duration = duration;
        }

        const metadata = new chrome.cast.media.GenericMediaMetadata();
        if (title) metadata.title = title;
        if (subtitle) metadata.subtitle = subtitle;
        if (thumbnailUrl) {
            metadata.images = [new chrome.cast.Image(thumbnailUrl)];
        }
        mediaInfo.metadata = metadata;

        const request = new chrome.cast.media.LoadRequest(mediaInfo);
        request.autoplay = true;
        request.currentTime = startPosition || 0;

        currentSession.loadMedia(request, onMediaLoaded, onMediaError);
        return true;
    },

    play: function () {
        const media = getCurrentMedia();
        if (media) media.play(null);
    },

    pause: function () {
        const media = getCurrentMedia();
        if (media) media.pause(null);
    },

    stop: function () {
        const media = getCurrentMedia();
        if (media) media.stop(null);
    },

    seek: function (seconds) {
        const media = getCurrentMedia();
        if (media) {
            const request = new chrome.cast.media.SeekRequest();
            request.currentTime = seconds;
            media.seek(request);
        }
    },

    setVolume: function (volume) {
        if (currentSession) {
            currentSession.setReceiverVolumeLevel(volume);
        }
    },

    stopCasting: function () {
        if (currentSession) {
            currentSession.stop(onSessionStop, onSessionStopError);
            currentSession = null;
            notifyStateChanged(false);
        }
    },

    isAvailable: function () {
        return !!(window.chrome && window.chrome.cast);
    },

    isCasting: function () {
        return currentSession !== null;
    },

    dispose: function () {
        dotNetRef = null;
        currentSession = null;
    }
};

function getCurrentMedia() {
    if (!currentSession) return null;
    const mediaSession = currentSession.media[0];
    return mediaSession || null;
}

function sessionListener(session) {
    currentSession = session;
    notifyStateChanged(true);
}

function receiverListener(availability) {
    if (dotNetRef) {
        const available = availability === chrome.cast.ReceiverAvailability.AVAILABLE;
        dotNetRef.invokeMethodAsync('OnReceiverAvailabilityChanged', available);
    }
}

function onInitSuccess() {
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnCastInitialized');
    }
}

function onInitError(error) {
    console.error('Cast init error:', error);
}

function onMediaLoaded(mediaSession) {
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnMediaLoaded');
    }
    startMediaStatusPolling();
}

function onMediaError(error) {
    console.error('Cast media error:', error);
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnMediaError', error.description || 'Unknown error');
    }
}

function onSessionStop() {
    stopMediaStatusPolling();
    currentSession = null;
    notifyStateChanged(false);
}

function onSessionStopError(error) {
    console.error('Cast session stop error:', error);
}

function notifyStateChanged(isCasting) {
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnCastStateChanged', isCasting);
    }
}

function startMediaStatusPolling() {
    stopMediaStatusPolling();
    mediaStatusInterval = setInterval(() => {
        const media = getCurrentMedia();
        if (!media || !dotNetRef) {
            stopMediaStatusPolling();
            return;
        }

        let state = 'idle';
        switch (media.playerState) {
            case chrome.cast.media.PlayerState.PLAYING:
                state = 'playing';
                break;
            case chrome.cast.media.PlayerState.PAUSED:
                state = 'paused';
                break;
            case chrome.cast.media.PlayerState.BUFFERING:
                state = 'buffering';
                break;
            case chrome.cast.media.PlayerState.IDLE:
                state = 'idle';
                stopMediaStatusPolling();
                break;
        }

        dotNetRef.invokeMethodAsync('OnMediaStatusUpdate', state, media.currentTime || 0, media.media?.duration || 0, currentSession?.receiver?.volume?.level || 1);
    }, 1000);
}

function stopMediaStatusPolling() {
    if (mediaStatusInterval) {
        clearInterval(mediaStatusInterval);
        mediaStatusInterval = null;
    }
}
