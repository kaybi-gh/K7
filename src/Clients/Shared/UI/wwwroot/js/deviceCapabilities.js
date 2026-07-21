// Chromium / MSE playback capability probes (canPlayType + mediaCapabilities).
// Used by the web client and Windows MAUI (WebView2 + Video.js HLS).

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
        ogg_audio: 'audio/ogg',
        wav: 'audio/wav'
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
                hdrMetadataType: "smpteSt2086",
            }
        });

        return hdrCheck.supported;
    } catch (e) {
        console.warn("HDR detection failed:", e);
        return false;
    }
};
