const _instances = new Map();
const CompactOn = 40;
const CompactOff = 8;
const CompactClass = 'is-header-compact';
const CompactHeaderPx = 48;
const CompactBuffer = 32;
const SettleMs = 180;
const ShortLandscapeQuery = '(orientation: landscape) and (max-height: 600px)';
const ScrollerSelector = [
    '.album-detail-page',
    '.k7-virtual-grid',
    '.k7-virtual-list',
    '.k7-data-table-scroll',
    '.browse-view-table',
    '.browse-view-list',
    '.browse-view-simple-grid',
    '.browse-view-grid',
    '.album-tracklist',
    '.playlist-tracklist'
].join(', ');

function isShortLandscape() {
    return window.matchMedia(ShortLandscapeQuery).matches;
}

function headerEl(pageEl) {
    return pageEl.querySelector('.playlist-header, .album-header');
}

function canScroll(el) {
    if (!(el instanceof Element))
        return false;
    var overflowY = window.getComputedStyle(el).overflowY;
    if (overflowY !== 'auto' && overflowY !== 'scroll' && overflowY !== 'overlay')
        return false;
    return el.scrollHeight > el.clientHeight;
}

function findScroller(pageEl, eventTarget) {
    if (pageEl.classList.contains('page-scrollable'))
        return pageEl;

    if (eventTarget instanceof Element
        && eventTarget !== pageEl
        && pageEl.contains(eventTarget)
        && canScroll(eventTarget))
        return eventTarget;

    var albumPage = pageEl.querySelector('.album-detail-page');
    if (albumPage)
        return albumPage;

    var nodes = pageEl.querySelectorAll(ScrollerSelector);
    for (var i = 0; i < nodes.length; i++) {
        if (canScroll(nodes[i]))
            return nodes[i];
    }
    return nodes.length > 0 ? nodes[0] : null;
}

function readScrollTop(pageEl, eventTarget) {
    var scroller = findScroller(pageEl, eventTarget);
    return scroller ? scroller.scrollTop : 0;
}

function measureOverflow(scroller) {
    if (!scroller)
        return 0;
    return scroller.scrollHeight - scroller.clientHeight;
}

function estimateSaved(pageEl) {
    var header = headerEl(pageEl);
    var headerH = header ? header.getBoundingClientRect().height : 0;
    var saved = Math.max(0, headerH - CompactHeaderPx);
    var toolbar = pageEl.querySelector('.browse-view-toolbar');
    if (toolbar) {
        var th = toolbar.getBoundingClientRect().height;
        if (th > 1)
            saved += th;
    }
    return saved + CompactBuffer;
}

function isLocked(inst) {
    return inst.lock || performance.now() < inst.lockUntil;
}

function lock(inst) {
    inst.lock = true;
    inst.lockUntil = performance.now() + SettleMs;
    requestAnimationFrame(function () {
        inst.lock = false;
    });
}

function leftoverAfterCompact(pageEl, overflow) {
    return overflow - estimateSaved(pageEl);
}

function setCompact(inst, scroller, enable) {
    var oldTop = scroller ? scroller.scrollTop : 0;
    lock(inst);

    if (enable)
        inst.pageEl.classList.add(CompactClass);
    else
        inst.pageEl.classList.remove(CompactClass);

    if (!scroller)
        return;

    if (enable) {
        if (scroller.scrollTop < CompactOff)
            scroller.scrollTop = Math.max(oldTop, CompactOn);
        if (measureOverflow(scroller) <= CompactOn || scroller.scrollTop < CompactOff) {
            inst.pageEl.classList.remove(CompactClass);
            scroller.scrollTop = oldTop;
        }
        return;
    }
}

function update(inst, top, eventTarget) {
    if (isLocked(inst))
        return;

    var force = isShortLandscape();
    if (force) {
        inst.pageEl.classList.add(CompactClass);
        return;
    }

    var compact = inst.pageEl.classList.contains(CompactClass);
    var scroller = findScroller(inst.pageEl, eventTarget);
    var overflow = measureOverflow(scroller);

    if (!compact) {
        if (top <= CompactOn || leftoverAfterCompact(inst.pageEl, overflow) <= CompactOn)
            return;
        setCompact(inst, scroller, true);
        return;
    }

    if (top < CompactOff)
        setCompact(inst, scroller, false);
}

export function attach(id, pageEl) {
    dispose(id);
    if (!(pageEl instanceof Element))
        return;

    var mql = window.matchMedia(ShortLandscapeQuery);
    var inst = {
        pageEl: pageEl,
        mql: mql,
        lock: false,
        lockUntil: 0,
        raf: 0,
        pendingTarget: pageEl,
        onScroll: function (e) {
            var target = e.target;
            if (!(target instanceof Element))
                return;
            if (target !== pageEl && !pageEl.contains(target))
                return;
            inst.pendingTarget = target;
            if (inst.raf)
                return;
            inst.raf = requestAnimationFrame(function () {
                inst.raf = 0;
                if (isLocked(inst))
                    return;
                var targetEl = inst.pendingTarget;
                update(inst, readScrollTop(pageEl, targetEl), targetEl);
            });
        },
        onViewport: function () {
            if (isLocked(inst))
                return;
            update(inst, readScrollTop(pageEl, pageEl), pageEl);
        }
    };

    window.addEventListener('scroll', inst.onScroll, true);
    window.addEventListener('resize', inst.onViewport);
    if (typeof mql.addEventListener === 'function')
        mql.addEventListener('change', inst.onViewport);
    else if (typeof mql.addListener === 'function')
        mql.addListener(inst.onViewport);

    _instances.set(id, inst);
    update(inst, readScrollTop(pageEl, pageEl), pageEl);
}

export function dispose(id) {
    var inst = _instances.get(id);
    if (!inst)
        return;

    if (inst.raf)
        cancelAnimationFrame(inst.raf);

    window.removeEventListener('scroll', inst.onScroll, true);
    window.removeEventListener('resize', inst.onViewport);
    if (inst.mql) {
        if (typeof inst.mql.removeEventListener === 'function')
            inst.mql.removeEventListener('change', inst.onViewport);
        else if (typeof inst.mql.removeListener === 'function')
            inst.mql.removeListener(inst.onViewport);
    }

    inst.pageEl.classList.remove(CompactClass);
    _instances.delete(id);
}
