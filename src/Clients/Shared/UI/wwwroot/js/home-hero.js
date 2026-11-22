// A banner only moves on its own while it is actually on screen and the system does not ask
// for reduced motion. Home stays mounted under the player and under other pages (FeedHub), so
// a hidden banner would keep requesting its next backdrop.
export function canAutoAdvance(banner) {
    if (document.hidden || window.matchMedia('(prefers-reduced-motion: reduce)').matches)
        return false;

    if (document.documentElement.classList.contains('native-player-active'))
        return false;

    // Web and remote playback cover the page. The node exists only while that UI is up.
    if (document.querySelector('.video-container, .remote-control-host'))
        return false;

    return isPainted(banner);
}

function isPainted(banner) {
    if (!banner || typeof banner.getBoundingClientRect !== 'function')
        return false;

    var style = window.getComputedStyle(banner);
    return style.display !== 'none' && style.visibility !== 'hidden';
}

// Arrow-key navigation picks what is straight above or below. The button of a banner sits in
// its bottom corner, so from most cards of the next row "straight up" was the app bar and the
// banner was skipped. The banner itself is the target (it spans the page) and passes the focus on.
export function focusPrimary(banner) {
    if (!banner || document.activeElement !== banner)
        return;

    var target = banner.querySelector('.home-hero__actions .focusable, .home-hero__actions a, .home-hero__actions button');
    if (target)
        target.focus();
}
