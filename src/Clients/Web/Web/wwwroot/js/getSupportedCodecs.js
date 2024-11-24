//window.getSupportedCodecs = async function () {
//    const videoElement = document.createElement('video');
//    const codecs = {
//        h264: videoElement.canPlayType('video/mp4; codecs="avc1.42E01E"') !== '',
//        vp8: videoElement.canPlayType('video/webm; codecs="vp8"') !== '',
//        vp9: videoElement.canPlayType('video/webm; codecs="vp9"') !== '',
//        av1: videoElement.canPlayType('video/mp4; codecs="av01.0.05M.08"') !== ''
//    };

//    // Filtrer les codecs supportés et retourner seulement leurs noms
//    return Object.keys(codecs).filter(codec => codecs[codec]);
//};

window.getSupportedCodecs = async function () {
    const videoElement = document.createElement('video');
    const audioElement = document.createElement('audio');

    // Définir les codecs et conteneurs à tester
    const codecsToTest = {
        // Codecs vidéo
        h264: 'video/mp4; codecs="avc1.42E01E"',
        vp8: 'video/webm; codecs="vp8"',
        vp9: 'video/webm; codecs="vp9"',
        av1: 'video/mp4; codecs="av01.0.05M.08"',
        hevc: 'video/mp4; codecs="hev1.1.6.L93.B0"',
        theora: 'video/ogg; codecs="theora"',

        // Conteneurs vidéo
        mp4: 'video/mp4',
        webm: 'video/webm',
        ogg: 'video/ogg',
        mkv: 'video/x-matroska',

        // Codecs audio
        mp3: 'audio/mpeg',
        aac: 'audio/mp4; codecs="mp4a.40.2"',
        opus: 'audio/ogg; codecs="opus"',
        vorbis: 'audio/ogg; codecs="vorbis"',
        flac: 'audio/x-flac',
        alac: 'audio/mp4; codecs="alac"',

        // Conteneurs audio
        m4a: 'audio/mp4',
        oggAudio: 'audio/ogg',
        wav: 'audio/wav'
    };

    // Tester chaque codec avec le bon élément (audio ou vidéo)
    const supportedCodecs = Object.entries(codecsToTest).filter(([codec, mimeType]) => {
        if (mimeType.startsWith('video/')) {
            return videoElement.canPlayType(mimeType) !== '';
        }
        if (mimeType.startsWith('audio/')) {
            return audioElement.canPlayType(mimeType) !== '';
        }
        return false;
    }).map(([codec]) => codec);

    return supportedCodecs;
};