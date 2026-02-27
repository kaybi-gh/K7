let players = {};

window.initVideoJs = function (id, videoPlayer, videoContainer, options, dotNetRef) {
    // If a player already exists for this id, dispose it first to avoid duplicate streams/listeners
    if (players[id]) {
        try {
            players[id].dispose();
        } catch (e) {
            console.warn('Error disposing existing player before re-init', e);
        }
        delete players[id];
    }

    const player = videojs(videoPlayer, options);
    player.volume(options.volume);

    const otherEvents = [
        'beforepluginsetup', // Signals that a plugin is about to be set up on a player.
        'error',
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


    // Fires when the browser has loaded meta data for the audio/video.ed.
    player.on('loadedmetadata', function () {
        dotNetRef.invokeMethodAsync('OnDurationChanged', player.duration())
            .catch((error) => console.error('Error invoking C# method', error));
    });

    // Fired when the current playback position has changed * During playback this is fired every 15-250 milliseconds, depending on the playback technology in use.
    player.on('timeupdate', function () {
        dotNetRef.invokeMethodAsync('OnTimeUpdated', player.currentTime())
            .catch((error) => console.error('Error invoking C# method', error));
    });

    // Fired while the user agent is downloading media data.
    player.on('progress', function () {
        const buffered = player.buffered();
        let bufferedEnd = 0;

        if (buffered && buffered.length > 0) {
            bufferedEnd = buffered.end(buffered.length - 1);
        }

        dotNetRef.invokeMethodAsync('OnBufferedUpdated', bufferedEnd)
            .catch((error) => console.error('Error invoking C# method', error));
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
    players[id]?.play();
}

window.pause = function (id) {
    players[id]?.pause();
}

window.stop = function (id) {
    players[id]?.pause();
    players[id]?.currentTime(0);
}

window.changeSource = function (id, src, type) {
    const player = players[id];
    if (player) {
        player.src({ src: src, type: type });
    }
}

window.switchAudioTrack = function (id, trackIndex) {
    const player = players[id];
    if (!player) return false;

    const audioTracks = player.audioTracks();
    if (!audioTracks || trackIndex < 0 || trackIndex >= audioTracks.length) return false;

    for (let i = 0; i < audioTracks.length; i++) {
        audioTracks[i].enabled = (i === trackIndex);
    }
    return true;
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
    players[id]?.currentTime(seconds);
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
    players[id]?.rate(rate);
}

window.getCurrentTime = function (id) {
    return players[id]?.currentTime() ?? 0;
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
