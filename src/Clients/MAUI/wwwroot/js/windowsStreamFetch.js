// Windows MAUI only: Video.js VHS xhr bridge via C# HttpClient.
// Loaded from MAUI wwwroot; no-ops until K7.initWindowsStreamFetchBridge is called.
(function () {
    'use strict';

    window.K7 = window.K7 || {};

    function isK7StreamResource(url) {
        if (!url)
            return false;

        return url.indexOf('/hls-stream/') !== -1
            || url.indexOf('/direct-stream') !== -1
            || url.indexOf('/remote-stream-sessions/') !== -1;
    }

    function getVhsModule() {
        if (!window.videojs)
            return null;

        return videojs.Vhs || videojs.VHS || null;
    }

    function wantsTextXhrBody(responseType, contentType, uri) {
        if (responseType === 'arraybuffer' || responseType === 'blob')
            return false;

        if (contentType) {
            const lower = contentType.toLowerCase();
            if (lower.indexOf('mpegurl') !== -1 || lower.indexOf('text/') === 0)
                return true;
        }

        if (uri && uri.indexOf('.m3u8') !== -1)
            return true;

        return !responseType || responseType === 'text' || responseType === 'document' || responseType === 'json';
    }

    function bridgeRawBodyToBytes(rawBody) {
        if (!rawBody)
            return new Uint8Array(0);

        if (rawBody instanceof Uint8Array)
            return rawBody;

        if (rawBody instanceof ArrayBuffer)
            return new Uint8Array(rawBody);

        if (Array.isArray(rawBody))
            return new Uint8Array(rawBody);

        if (typeof rawBody !== 'string')
            return new Uint8Array(0);

        if (rawBody.indexOf('#EXTM3U') === 0)
            return null;

        try {
            const binary = atob(rawBody);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++)
                bytes[i] = binary.charCodeAt(i);

            return bytes;
        } catch {
            return null;
        }
    }

    function resolveBridgeXhrBody(rawBody, responseType, contentType, uri) {
        if (typeof rawBody === 'string' && rawBody.indexOf('#EXTM3U') === 0)
            return rawBody;

        const bytes = bridgeRawBodyToBytes(rawBody);
        if (bytes === null)
            return rawBody;

        if (bytes.length === 0)
            return typeof rawBody === 'string' ? rawBody : '';

        if (wantsTextXhrBody(responseType, contentType, uri))
            return new TextDecoder('utf-8').decode(bytes);

        return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
    }

    function buildBridgeXhrResponse(options, uri, statusCode, contentType, body, request) {
        return {
            body: body,
            statusCode: statusCode,
            method: options.method || 'GET',
            headers: {
                'content-type': contentType || 'application/octet-stream'
            },
            uri: uri,
            url: uri,
            rawRequest: request
        };
    }

    function createBridgeXhrRequest(options) {
        const uri = options.uri || options.url;
        const responseType = options.responseType || '';
        const listeners = {};

        const request = {
            uri: uri,
            url: uri,
            method: options.method || 'GET',
            responseType: responseType,
            requestType: options.requestType || '',
            readyState: 0,
            status: 0,
            response: null,
            responseText: '',
            aborted: false,
            requestTime: Date.now(),
            addEventListener: function (type, listener) {
                if (!listeners[type])
                    listeners[type] = [];

                listeners[type].push(listener);
            },
            removeEventListener: function (type, listener) {
                if (!listeners[type])
                    return;

                listeners[type] = listeners[type].filter(function (fn) {
                    return fn !== listener;
                });
            },
            dispatchEvent: function (type, event) {
                const evt = event || {};
                if (!evt.target)
                    evt.target = request;

                (listeners[type] || []).forEach(function (fn) {
                    fn(evt);
                });

                const propertyHandler = request['on' + type];
                if (typeof propertyHandler === 'function')
                    propertyHandler.call(request, evt);
            },
            abort: function () {
                if (request.aborted)
                    return;

                request.aborted = true;
                request.readyState = 4;
                request.dispatchEvent('abort', { target: request });
                request.dispatchEvent('loadend', { target: request });
            }
        };

        if (typeof options.beforeSend === 'function')
            options.beforeSend(request);

        return request;
    }

    function applyBridgeXhrBodyToRequest(request, body, responseType) {
        request.status = request.status || 200;

        if (responseType === 'arraybuffer') {
            request.response = body;
            request.responseText = '';
            request.body = body;
            return;
        }

        const textBody = typeof body === 'string'
            ? body
            : (body ? new TextDecoder('utf-8').decode(new Uint8Array(body)) : '');

        request.responseText = textBody;
        request.response = textBody;
        request.body = textBody;
    }

    function getBridgeXhrBodyByteLength(body) {
        if (typeof body === 'string')
            return body.length;

        if (body && body.byteLength)
            return body.byteLength;

        return 0;
    }

    function completeBridgeXhrRequest(request, body, responseType) {
        applyBridgeXhrBodyToRequest(request, body, responseType);
        request.readyState = 4;
        request.responseTime = Date.now();
        request.roundTripTime = request.responseTime - request.requestTime;

        const byteLength = getBridgeXhrBodyByteLength(body);
        request.bytesReceived = byteLength;

        if (request.roundTripTime > 0 && byteLength > 0)
            request.bandwidth = Math.floor(byteLength / request.roundTripTime * 8 * 1000);

        if (byteLength > 0) {
            request.dispatchEvent('progress', {
                target: request,
                loaded: byteLength,
                total: byteLength,
                lengthComputable: true
            });
        }

        request.dispatchEvent('load', { target: request });
        request.dispatchEvent('loadend', { target: request });
    }

    function fetchStreamViaBridge(dotNetRef, options, callback) {
        const uri = options.uri || options.url;
        const responseType = options.responseType || '';
        const rangeHeader = options.headers
            ? (options.headers.Range || options.headers.range || null)
            : null;
        const request = createBridgeXhrRequest(options);

        dotNetRef.invokeMethodAsync('FetchStreamAsync', uri, rangeHeader)
            .then(function (result) {
                if (request.aborted)
                    return;

                if (!result) {
                    callback(new Error('Stream fetch returned null for ' + uri), request);
                    return;
                }

                const contentType = result.contentType || '';
                const body = resolveBridgeXhrBody(result.body, responseType, contentType, uri);

                request.status = result.statusCode;
                completeBridgeXhrRequest(request, body, responseType);
                callback(null, buildBridgeXhrResponse(
                    options,
                    uri,
                    result.statusCode,
                    contentType,
                    body,
                    request));
            })
            .catch(function (err) {
                if (request.aborted)
                    return;

                request.readyState = 4;
                request.dispatchEvent('error', { target: request });
                request.dispatchEvent('loadend', { target: request });
                callback(err, request);
            });

        return request;
    }

    function wrapXhrForBridge(originalXhr, dotNetRef) {
        const wrappedXhr = function (options, callback) {
            const uri = options.uri || options.url;
            if (!isK7StreamResource(uri))
                return originalXhr(options, callback);

            return fetchStreamViaBridge(dotNetRef, options, callback);
        };

        Object.keys(originalXhr).forEach(function (key) {
            if (key === 'original')
                return;

            const value = originalXhr[key];
            wrappedXhr[key] = typeof value === 'function' ? value.bind(originalXhr) : value;
        });

        // VHS instance xhr routes to videojs.xhr when Vhs.xhr.original is true.
        wrappedXhr.original = false;
        wrappedXhr.__k7WindowsStreamBridge = true;
        return wrappedXhr;
    }

    function installWindowsStreamXhr(dotNetRef) {
        if (!dotNetRef)
            return false;

        if (!window.videojs)
            return false;

        const vhsModule = getVhsModule();
        if (!vhsModule || !vhsModule.xhr)
            return false;

        if (window.__k7WindowsStreamXhrInstalled)
            return true;

        if (!vhsModule.xhr.__k7WindowsStreamBridge) {
            vhsModule.xhr = wrapXhrForBridge(vhsModule.xhr, dotNetRef);
        }

        // Default VHS routing uses videojs.xhr when Vhs.xhr.original is true.
        if (videojs.xhr && !videojs.xhr.__k7WindowsStreamBridge) {
            videojs.xhr = wrapXhrForBridge(videojs.xhr, dotNetRef);
        }

        window.__k7WindowsStreamXhrInstalled = true;
        return true;
    }

    function ensureWindowsStreamBridge() {
        if (K7._windowsStreamFetchRef)
            installWindowsStreamXhr(K7._windowsStreamFetchRef);
    }

    function scheduleWindowsStreamBridgeInstall(dotNetRef) {
        if (installWindowsStreamXhr(dotNetRef))
            return;

        if (K7._windowsStreamFetchRetry)
            return;

        var attempts = 0;
        K7._windowsStreamFetchRetry = setInterval(function () {
            attempts += 1;
            if (installWindowsStreamXhr(dotNetRef) || attempts >= 100) {
                clearInterval(K7._windowsStreamFetchRetry);
                K7._windowsStreamFetchRetry = null;
            }
        }, 100);
    }

    K7.initWindowsStreamFetchBridge = function (dotNetRef) {
        K7._windowsStreamFetchRef = dotNetRef;
        scheduleWindowsStreamBridgeInstall(dotNetRef);
    };

    K7.ensureWindowsStreamBridge = ensureWindowsStreamBridge;

    K7.onVideoJsPlayerCreated = function () {
        ensureWindowsStreamBridge();
    };
})();
