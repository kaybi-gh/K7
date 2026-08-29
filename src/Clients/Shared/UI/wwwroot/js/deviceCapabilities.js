// Chromium / MSE playback capability probes.
// Used by the web client and Windows MAUI (WebView2 + Video.js HLS).
// Video codecs must match MediaSource.isTypeSupported on fMP4 CODECS strings
// (what Video.js VHS checks). <video>.canPlayType is progressive-file decode
// (hev1 Direct Play). Reporting HEVC from canPlayType makes the server remux
// hvc1 HLS, then VHS drops the only variant (MEDIA_ERR_DECODE).

function mseTypeSupported(mimeType) {
    try {
        if (window.ManagedMediaSource && typeof ManagedMediaSource.isTypeSupported === 'function'
            && ManagedMediaSource.isTypeSupported(mimeType)) {
            return true;
        }

        if (window.MediaSource && typeof MediaSource.isTypeSupported === 'function'
            && MediaSource.isTypeSupported(mimeType)) {
            return true;
        }
    } catch (e) {
        // Some engines throw on unknown codec strings.
    }

    return false;
}

function anyMseTypeSupported(mimeTypes) {
    return mimeTypes.some(mseTypeSupported);
}

window.getSupportedAudioCodecsAsync = async function () {
    const audioElement = document.createElement('audio');

    const codecsToTest = {
        mp3: 'audio/mpeg',
        aac: 'audio/mp4; codecs="mp4a.40.2"',
        aacHE: 'audio/mp4; codecs="mp4a.40.5"',
        opus: 'audio/ogg; codecs="opus"',
        vorbis: 'audio/ogg; codecs="vorbis"',
        flac: 'audio/flac',
        alac: 'audio/mp4; codecs="alac"',
        // Server AudioMediaFormat for wav uses Codec = "pcm".
        pcm: 'audio/wav',
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

    const videoContainersToTest = {
        mp4: 'video/mp4',
        webm: 'video/webm',
        ogg: 'video/ogg',
        ts: 'video/mp2t',
        mkv: 'video/x-matroska',
        avi: 'video/x-msvideo'
    };

    const audioElement = document.createElement('audio');

    const audioContainersToTest = {
        mp3: 'audio/mpeg',
        flac: 'audio/flac',
        aac: 'audio/aac',
        // Must match server MediaFormats container ids (ogg, not ogg_audio).
        ogg: 'audio/ogg',
        wav: 'audio/wav',
        m4a: 'audio/mp4',
        mp4: 'audio/mp4'
    };

    const supported = Object.entries(videoContainersToTest)
        .filter(([_, mimeType]) => videoElement.canPlayType(mimeType) !== '')
        .map(([container]) => container);

    Object.entries(audioContainersToTest)
        .filter(([_, mimeType]) => audioElement.canPlayType(mimeType) !== '')
        .forEach(([container]) => supported.push(container));

    return supported;
};

window.getSupportedVideoCodecsAsync = async function () {
    // HLS fMP4 CODECS values Video.js passes to MediaSource.isTypeSupported.
    const codecsToTest = {
        h264: [
            'video/mp4; codecs="avc1.42E01E"',
            'video/mp4; codecs="avc1.640028"'
        ],
        vp8: [
            'video/webm; codecs="vp8"'
        ],
        vp9: [
            'video/mp4; codecs="vp09.00.10.08"',
            'video/webm; codecs="vp9"'
        ],
        av1: [
            'video/mp4; codecs="av01.0.05M.08"'
        ],
        hevc: [
            'video/mp4; codecs="hvc1.1.4.L120.B0"',
            'video/mp4; codecs="hvc1.1.6.L93.B0"',
            'video/mp4; codecs="hvc1.2.4.L150.B0"',
            'video/mp4; codecs="hvc1.1.4.L120.B0,mp4a.40.2"'
        ],
        theora: [
            'video/ogg; codecs="theora"'
        ]
    };

    return Object.entries(codecsToTest)
        .filter(([_, mimeTypes]) => anyMseTypeSupported(mimeTypes))
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
                hdrMetadataType: "smpteSt2086",
            }
        });

        return hdrCheck.supported;
    } catch (e) {
        console.warn("HDR detection failed:", e);
        return false;
    }
};
