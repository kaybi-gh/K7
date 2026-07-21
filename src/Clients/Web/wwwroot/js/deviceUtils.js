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
