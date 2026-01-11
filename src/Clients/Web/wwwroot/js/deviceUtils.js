window.getRawUserAgent = () => navigator.userAgent;

window.getParsedUserAgent = () => {
    try {
        const parsedUserAgent = bowser.parse(window.navigator.userAgent)

        return {
            BrowserName: parsedUserAgent.browser.name || null,
            BrowserVersion: parsedUserAgent.browser.version || null,
            OsName: parsedUserAgent.os.name|| null,
            OsVersion: parsedUserAgent.os.version || null,
            OsVersionName: parsedUserAgent.os.versionName || null,
            PlatformType: parsedUserAgent.platform.type || null,
            EngineName: parsedUserAgent.engine.name || null,
            EngineVersion: parsedUserAgent.engine.version || null
        };
    } catch (e) {
        console.warn('getBrowserInfo failed', e);
        return {
            BrowserName: null,
            BrowserVersion: null,
            OsName: null,
            OsVersion: null,
            OsVersionName: null,
            PlatformType: null,
            EngineName: null,
            EngineVersion: null
        };
    }
};

window.getDisplayHeight = () => {
    return window.screen.height;
};

window.getDisplayWidth = () => {
    return window.screen.width;
};

window.getSupportedAudioCodecsAsync = async function () {
    const audioElement = document.createElement('audio');

    const codecsToTest = {
        mp3: 'audio/mpeg',
        aac: 'audio/mp4; codecs="mp4a.40.2"',
        aacHE: 'audio/mp4; codecs="mp4a.40.5"',
        opus: 'audio/ogg; codecs="opus"',
        vorbis: 'audio/ogg; codecs="vorbis"',
        flac: 'audio/x-flac',
        alac: 'audio/mp4; codecs="alac"',
        m4a: 'audio/mp4',
        oggAudio: 'audio/ogg',
        wav: 'audio/wav'
    };

    return Object.entries(codecsToTest)
        .filter(([_, mimeType]) => audioElement.canPlayType(mimeType) !== '')
        .map(([codec]) => codec);
};

window.getSupportedContainersAsync = async function () {
    const videoElement = document.createElement('video');

    const containersToTest = {
        mp4: 'video/mp4',
        webm: 'video/webm',
        ogg: 'video/ogg',
        ts: 'video/mp2t',
        mkv: 'video/x-matroska',
        avi: 'video/x-msvideo'
    };

    return Object.entries(containersToTest)
        .filter(([_, mimeType]) => videoElement.canPlayType(mimeType) !== '')
        .map(([container]) => container);
};

window.getSupportedVideoCodecsAsync = async function () {
    const videoElement = document.createElement('video');

    const codecsToTest = {
        h264: 'video/mp4; codecs="avc1.42E01E"',
        vp8: 'video/webm; codecs="vp8"',
        vp9: 'video/webm; codecs="vp9"',
        av1: 'video/mp4; codecs="av01.0.05M.08"',
        hevc: 'video/mp4; codecs="hev1.1.6.L93.B0"',
        theora: 'video/ogg; codecs="theora"'
    };

    return Object.entries(codecsToTest)
        .filter(([_, mimeType]) => videoElement.canPlayType(mimeType) !== '')
        .map(([codec]) => codec);
};

window.getHdrSupport = async function () {
    if (!("mediaCapabilities" in navigator)) {
        return false;
    }

    try {
        const hdrCheck = await navigator.mediaCapabilities.decodingInfo({
            type: "file",
            video: {
                contentType: "video/webm; codecs=vp9.2",
                width: 1920,
                height: 1080,
                bitrate: 8000000,
                framerate: 60,
                hdrMetadataType: "smpteSt2084",
            }
        });

        return hdrCheck.supported;
    } catch (e) {
        console.error("HDR detection failed:", e);
        return false;
    }
};
