// A banner only moves on its own while the page is visible and the system does not ask for
// reduced motion.
export function canAutoAdvance() {
    return !document.hidden && !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
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
