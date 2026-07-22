/**
 * Spatial Navigation engine for keyboard / remote control (TV D-pad).
 *
 * Architecture:
 *  - js-spatial-navigation handles all geometric nearest-neighbour calculation.
 *  - Named sections (registered via addSection / removeSection) carry
 *    "enterTo: last-focused" so returning to a region remembers position.
 *  - Focus traps (popovers, dialogs) are implemented with a global
 *    navigableFilter that restricts movement to the topmost layer element.
 *    This is resilient to Blazor re-renders because it consults the live DOM
 *    reference instead of maintaining static selectors.
 *  - A MutationObserver debounces SpatialNavigation.makeFocusable() after
 *    every DOM change, so StateHasChanged() automatically syncs SN state.
 */
var SpatialNav = (function () {

    // State

    var _initialized = false;
    var _layers = [];
    var _homeEscapeCallback = null;
    var _homeEscapeTimer = null;
    var _homePattern = /^\/$/;
    var _videoPlayerBackCallback = null;
    var _videoPlayerRemoteRef = null;
    var _refreshTimer = null;
    var _sectionLastFocused = {};
    var _currentSectionId = null;
    var _tvTextEditStartedAt = 0;
    var _tvEditDismissViaBack = false;
    var TV_TEXT_EDIT_BLUR_GRACE_MS = 400;

    var FOCUSABLE = [
        '.focusable'
    ].join(', ');

    // Layer Stack

    function layerElementsMatch(a, b) {
        if (!a || !b) return false;
        if (a === b) return true;
        if (a.isSameNode && a.isSameNode(b)) return true;
        var uidA = a.getAttribute && a.getAttribute('data-sn-layer-uid');
        var uidB = b.getAttribute && b.getAttribute('data-sn-layer-uid');
        return !!(uidA && uidB && uidA === uidB);
    }

    function removeLayerAt(i) {
        var layer = _layers.splice(i, 1)[0];
        layer.onClose = null;
        if (layer.restoreFocus && layer.restoreFocus.isConnected) {
            layer.restoreFocus.focus({ preventScroll: true });
        }
    }

    function isLayerActive(layer) {
        if (!layer || !layer.el || !layer.el.isConnected) return false;
        if (layer.type === 'page') return true;
        if (layer.el.hasAttribute && layer.el.hasAttribute('data-sn-layer')) return true;
        if (layer.type === 'popover') return false;
        return isElementVisible(layer.el);
    }

    function pruneDisconnectedLayers() {
        for (var i = _layers.length - 1; i >= 0; i--) {
            if (_layers[i].el && !_layers[i].el.isConnected) {
                _layers[i].onClose = null;
                _layers.splice(i, 1);
            }
        }
    }

    function pushLayer(el, type, opts) {
        if (!el) return;
        pruneDisconnectedLayers();
        opts = opts || {};
        for (var i = 0; i < _layers.length; i++) {
            var existing = _layers[i].el;
            if (layerElementsMatch(existing, el)) {
                // Merge options into existing layer (e.g. onClose callback arriving after auto-detection)
                if (opts.onClose && !_layers[i].onClose) _layers[i].onClose = opts.onClose;
                if (opts.focusSelector && !_layers[i].focusSelector) _layers[i].focusSelector = opts.focusSelector;
                _layers[i].el = el;
                return;
            }
        }
        _layers.push({
            el: el,
            type: type || 'popover',
            onClose: opts.onClose || null,
            restoreFocus: opts.restoreFocus || document.activeElement,
            focusSelector: opts.focusSelector || null
        });
        autoFocusLayer(_layers[_layers.length - 1]);
    }

    function attachLayerCallback(el, onClose) {
        if (!el) return;
        // Try matching by UID attribute (most reliable in MAUI WebView)
        var uid = el.getAttribute && el.getAttribute('data-sn-layer-uid');
        if (uid) {
            for (var i = 0; i < _layers.length; i++) {
                if (_layers[i].el.getAttribute && _layers[i].el.getAttribute('data-sn-layer-uid') === uid) {
                    if (onClose) _layers[i].onClose = onClose;
                    return;
                }
            }
        }
        for (var i = 0; i < _layers.length; i++) {
            var existing = _layers[i].el;
            if (layerElementsMatch(existing, el)) {
                if (onClose) _layers[i].onClose = onClose;
                _layers[i].el = el;
                return;
            }
        }
        // Last resort: find most recent layer of same type without a callback
        var tag = el.getAttribute && el.getAttribute('data-sn-layer');
        if (tag) {
            for (var i = _layers.length - 1; i >= 0; i--) {
                if (_layers[i].type === tag && !_layers[i].onClose) {
                    _layers[i].onClose = onClose;
                    return;
                }
            }
        }
    }

    function popLayer(el) {
        if (!el) return;
        var uid = el.getAttribute && el.getAttribute('data-sn-layer-uid');
        for (var i = _layers.length - 1; i >= 0; i--) {
            if (layerElementsMatch(_layers[i].el, el)) {
                removeLayerAt(i);
                return;
            }
        }
        if (uid) {
            for (var i = _layers.length - 1; i >= 0; i--) {
                var existing = _layers[i].el;
                if (existing && existing.getAttribute && existing.getAttribute('data-sn-layer-uid') === uid) {
                    removeLayerAt(i);
                    return;
                }
            }
        }
    }

    function peekLayer() {
        pruneDisconnectedLayers();
        while (_layers.length > 0) {
            var top = _layers[_layers.length - 1];
            if (isLayerActive(top)) return top;
            var stale = _layers.pop();
            stale.onClose = null;
        }
        return null;
    }

    function autoFocusLayer(layer) {
        var attempts = 5;
        function tryFocus() {
            var container = layer.el;
            if (!container || !container.isConnected) return;
            if (window.K7 && window.K7._suppressEnterUntilKeyUp) return;
            // When video controls are hidden, keep focus on the overlay root.
            // Focusing a control bar button on TV/Android WebView triggers stale
            // mouseenter events that immediately re-show the overlay.
            if (container.classList && container.classList.contains('video-controls-overlay')
                && container.classList.contains('controls-hidden')) {
                container.focus({ preventScroll: true });
                return;
            }
            var target = null;
            if (layer.focusSelector) {
                target = container.querySelector(layer.focusSelector);
            }
            if (!target) {
                var items = getFocusables(container);
                target = items.length > 0 ? items[0] : null;
            }
            if (target) {
                target.focus({ preventScroll: true });
            } else if (--attempts > 0) {
                setTimeout(tryFocus, 80);
            }
        }
        setTimeout(tryFocus, 150);
    }

    // Section Management

    function addSection(id, opts) {
        if (!window.SpatialNavigation) return;
        opts = opts || {};
        var config = {
            selector: opts.selector || ('[data-sn-section="' + id + '"] ' + FOCUSABLE),
            restrict: opts.restrict || 'self-first',
            enterTo: opts.enterTo || 'last-focused',
            leaveFor: opts.leaveFor || null
        };
        try {
            SpatialNavigation.add(id, config);
        } catch (e) {
            try { SpatialNavigation.set(id, config); } catch (e2) { }
        }
        try { SpatialNavigation.makeFocusable(id); } catch (e) { }
    }

    function removeSection(id) {
        if (!window.SpatialNavigation) return;
        try { SpatialNavigation.remove(id); } catch (e) { }
    }

    // DOM Refresh

    function scheduleRefresh() {
        if (_refreshTimer) clearTimeout(_refreshTimer);
        _refreshTimer = setTimeout(function () {
            _refreshTimer = null;
            if (window.SpatialNavigation) SpatialNavigation.makeFocusable();
            lockActivatableInputs();
            ensurePageFocus();
        }, 32);
    }

    function refresh() {
        if (window.SpatialNavigation) SpatialNavigation.makeFocusable();
        lockActivatableInputs();
    }

    // Focusable Discovery

    function isVisibleInCarouselViewport(el) {
        var viewport = el.closest('[data-carousel-viewport]');
        if (!viewport) return true;
        var vpRect = viewport.getBoundingClientRect();
        var elRect = el.getBoundingClientRect();
        var cx = elRect.left + elRect.width / 2;
        return cx >= vpRect.left - 5 && cx <= vpRect.right + 5;
    }

    function isElementVisible(el) {
        var style = window.getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden') return false;
        if (el.offsetParent !== null) return true;
        var rect = el.getBoundingClientRect();
        return rect.width !== 0 || rect.height !== 0;
    }

    function getFocusables(container) {
        return Array.from(container.querySelectorAll(FOCUSABLE)).filter(function (el) {
            if (isInClosedSidebarNavGroup(el)) return false;
            if (el.closest('[data-carousel-item]')) return isVisibleInCarouselViewport(el);
            return isElementVisible(el);
        });
    }

    function isSidebarNavGroupOpen(details) {
        return !!(details && details.matches && details.matches('details.nav-group') && details.open);
    }

    function isInClosedSidebarNavGroup(el) {
        if (!el || !el.closest) return false;
        var group = el.closest('details.nav-group:not([open])');
        if (!group) return false;
        if (el.matches && el.matches('summary.nav-group-toggle')) return false;
        return group.contains(el);
    }

    function clearStaleSectionLastFocused(details) {
        var section = details && details.closest ? details.closest('[data-sn-section]') : null;
        if (!section) return;
        var sectionId = section.getAttribute('data-sn-section');
        if (!sectionId || !_sectionLastFocused[sectionId]) return;
        var last = _sectionLastFocused[sectionId];
        if (!last || !last.isConnected || !section.contains(last)) {
            delete _sectionLastFocused[sectionId];
            return;
        }
        if (!isElementVisible(last)) {
            delete _sectionLastFocused[sectionId];
            return;
        }
        if (!isSidebarNavGroupOpen(details) && details.contains(last)) {
            delete _sectionLastFocused[sectionId];
        }
    }

    function relocateFocusFromCollapsedNavGroup(details) {
        if (!details || isSidebarNavGroupOpen(details)) return;
        var active = document.activeElement;
        if (!active || !details.contains(active)) return;
        if (active.matches && active.matches('summary.nav-group-toggle')) return;
        var summary = details.querySelector('summary.nav-group-toggle');
        if (summary && isElementVisible(summary)) {
            summary.focus({ preventScroll: true });
        }
    }

    function ensureSidebarFocusVisible() {
        var active = document.activeElement;
        if (!active || !isInClosedSidebarNavGroup(active)) return false;
        var group = active.closest('details.nav-group');
        if (!group) return false;
        relocateFocusFromCollapsedNavGroup(group);
        return true;
    }

    function handleNavGroupToggle(e) {
        var details = e.target;
        if (!details || !details.matches || !details.matches('details.nav-group')) return;
        clearStaleSectionLastFocused(details);
        if (!details.open) {
            relocateFocusFromCollapsedNavGroup(details);
        }
        scheduleRefresh();
    }

    // Scroll

    function scrollCarouselToElement(el) {
        var carouselRoot = el.closest('[data-carousel]');
        if (!carouselRoot || !carouselRoot.__embla) return;
        var item = el.closest('[data-carousel-item]');
        if (!item) return;

        var container = carouselRoot.querySelector('.carousel-container');
        var allItems = container ? Array.from(container.querySelectorAll('[data-carousel-item]')) : [];
        var idx = allItems.indexOf(item);

        if (idx === 0) {
            carouselRoot.__embla.scrollTo(0);
            return;
        }

        var viewport = carouselRoot.querySelector('[data-carousel-viewport]') || carouselRoot;
        var vpRect = viewport.getBoundingClientRect();
        var itemRect = item.getBoundingClientRect();
        if (itemRect.left >= vpRect.left + 1 && itemRect.right <= vpRect.right + 5) return;

        if (itemRect.right > vpRect.right + 5) {
            carouselRoot.__embla.scrollNext();
        } else if (itemRect.left < vpRect.left + 1) {
            carouselRoot.__embla.scrollPrev();
        }
    }

    function isNearPageTop(el) {
        return el.getBoundingClientRect().top < window.innerHeight * 0.4;
    }

    // Scroll the TV detail-page root just enough to fully reveal a focused card,
    // including its footer metadata below the poster. The focused element itself
    // (e.g. .media-card-link) only spans the poster - measuring its own rect would
    // miss the title/subtitle text below it, which is why cards used to stay
    // clipped at the bottom of the viewport. Measuring the whole card/item instead
    // accounts for that extra height.
    function getFocusScrollRoot(el) {
        if (!el || !el.closest) return null;
        var tvScroll = el.closest('[data-tv-scroll]');
        if (tvScroll && window.K7 && window.K7.TvDetailScroll && window.K7.TvDetailScroll.hasInstance(tvScroll)) {
            return tvScroll;
        }
        var pageScroll = el.closest('.page-scrollable');
        if (pageScroll) return pageScroll;
        return document.querySelector('.app-main');
    }

    function scrollCardIntoTvView(root, el) {
        if (!root || !el) return;
        var card = el.closest('.media-card') || el.closest('[data-carousel-item]');
        if (!card) return;

        var margin = 24;
        var rootRect = root.getBoundingClientRect();
        var cardRect = card.getBoundingClientRect();

        var overflowBottom = cardRect.bottom - rootRect.bottom;
        var overflowTop = rootRect.top - cardRect.top;

        if (overflowBottom > -margin) {
            root.scrollBy({ top: overflowBottom + margin, behavior: 'smooth' });
        } else if (overflowTop > -margin) {
            root.scrollBy({ top: -(overflowTop + margin), behavior: 'smooth' });
        }
    }

    // Carousel Navigation

    var _carouselNavHandled = false;

    function handleCarouselNav(active, direction) {
        var carousel = active.closest('[data-carousel]');
        if (!carousel) return false;
        if (direction === 'ArrowUp' || direction === 'ArrowDown') return false;

        var currentItem = active.closest('[data-carousel-item]');
        if (!currentItem) return false;

        // Block ArrowRight from the loop-back item (action is click/Enter only)
        if (currentItem.hasAttribute('data-carousel-loop-back') && direction === 'ArrowRight') {
            return true;
        }

        var allItems = Array.from(carousel.querySelectorAll('[data-carousel-item]'));
        var currentIdx = allItems.indexOf(currentItem);
        if (currentIdx === -1) return false;

        var targetIdx = direction === 'ArrowRight' ? currentIdx + 1 : currentIdx - 1;
        if (targetIdx < 0 || targetIdx >= allItems.length) return false;

        var targetItem = allItems[targetIdx];
        var target = targetItem.matches(FOCUSABLE) ? targetItem : targetItem.querySelector(FOCUSABLE);
        if (!target) return false;

        if (carousel.__embla) {
            var viewport = carousel.querySelector('[data-carousel-viewport]') || carousel;
            var vpRect = viewport.getBoundingClientRect();
            var targetRect = targetItem.getBoundingClientRect();

            if (targetIdx === 0) {
                carousel.__embla.scrollTo(0);
            } else if (targetRect.right > vpRect.right + 5) {
                carousel.__embla.scrollNext();
            } else if (targetRect.left < vpRect.left + 1) {
                carousel.__embla.scrollPrev();
            }
        }
        _carouselNavHandled = true;
        setTimeout(function () {
            target.focus({ preventScroll: true });
            setTimeout(function () { _carouselNavHandled = false; }, 50);
        }, 10);
        return true;
    }

    // Editing Mode

    function isEditing(el) { return el && el.hasAttribute('data-sn-editing'); }
    function startEditing(el) {
        el.setAttribute('data-sn-editing', 'true');
        if (isTextInput(el)) {
            el.removeAttribute('readonly');
            el.focus({ preventScroll: true });
            // Android TV WebView (MAUI) routes OK via onTvRemoteSelect, not keydown Enter.
            // focus()/click() alone do not raise IME; native InputMethodManager is required.
            if (isTvLongPressMode()) {
                _tvTextEditStartedAt = Date.now();
                _tvEditDismissViaBack = false;
                try { el.click(); } catch (err) { /* ignore */ }
                setTimeout(function () {
                    if (window.K7 && window.K7.showSoftKeyboard) {
                        window.K7.showSoftKeyboard();
                    }
                    // Recover if IME/WebView focus handling briefly blurs the input.
                    setTimeout(function () {
                        if (isEditing(el) && document.activeElement !== el) {
                            el.focus({ preventScroll: true });
                        }
                    }, 50);
                }, 0);
            }
        }
    }
    function stopEditing(el) {
        el.removeAttribute('data-sn-editing');
        if (isTextInput(el)) {
            el.setAttribute('readonly', '');
            if (isTvLongPressMode() && window.K7 && window.K7.hideSoftKeyboard) {
                window.K7.hideSoftKeyboard();
            }
        }
    }
    function isActivatable(el) { return el && el.hasAttribute('data-sn-activatable'); }

    // Shared OK/Enter activation for data-sn-activatable controls (text fields, seekbar, sliders).
    // Used by handleEnter and handleTvRemoteSelect so both paths stay in sync on TV.
    function toggleActivatableEdit(el) {
        if (!el || !isActivatable(el)) return false;
        if (isEditing(el)) {
            stopEditing(el);
            if (window.SpatialNavigation) SpatialNavigation.resume();
            el.dispatchEvent(new CustomEvent('sn:editcommit', { bubbles: false }));
        } else {
            startEditing(el);
            if (window.SpatialNavigation) SpatialNavigation.pause();
            el.dispatchEvent(new CustomEvent('sn:editstart', { bubbles: false }));
        }
        return true;
    }

    function isTextInput(el) {
        var tag = (el.tagName || '').toLowerCase();
        if (tag === 'textarea') return true;
        if (tag !== 'input') return false;
        var type = (el.getAttribute('type') || 'text').toLowerCase();
        return ['text', 'password', 'search', 'email', 'number', 'tel', 'url'].indexOf(type) !== -1;
    }

    // Ensure activatable text inputs are readonly when not being edited
    function lockActivatableInputs() {
        var els = document.querySelectorAll('[data-sn-activatable]');
        for (var i = 0; i < els.length; i++) {
            var el = els[i];
            if (isTextInput(el) && !isEditing(el)) {
                el.setAttribute('readonly', '');
            }
        }
    }

    // Long-press helpers for spatial navigation on media cards
    function isEnterKey(key, code, keyCode) {
        key = key || '';
        code = code || '';
        if (keyCode === 13 || keyCode === 23 || keyCode === 66) return true;
        if (key === 'Enter' || key === 'NumpadEnter' || key === 'Select' || key === 'DpadCenter') return true;
        if (code === 'Enter' || code === 'NumpadEnter' || code === 'Select' || code === 'DpadCenter') return true;
        return false;
    }

    function getVideoControlsOverlay(el) {
        return el && el.closest ? el.closest('.video-controls-overlay') : null;
    }

    function isVideoControlsHidden(overlay) {
        return !!(overlay && overlay.classList.contains('controls-hidden'));
    }

    function swallowNextEnterClick() {
        window.K7 = window.K7 || {};
        window.K7._swallowNextEnterClick = true;
        setTimeout(function () {
            if (window.K7) window.K7._swallowNextEnterClick = false;
        }, 50);
    }

    function isTvLongPressMode() {
        return document.documentElement.classList.contains('platform-tv')
            || window.__k7TvNativeRemote === true;
    }

    function makeFakeKeyEvent(keyCode, target) {
        return {
            key: '',
            code: '',
            keyCode: keyCode,
            which: keyCode,
            repeat: false,
            target: target || document.activeElement,
            preventDefault: function () { },
            stopImmediatePropagation: function () { }
        };
    }

    function isMediaCardMenuOpen(card) {
        if (!card) return false;
        if (card.classList.contains('media-card--menu-open')) return true;
        if (card.querySelector('.k7-menu-dropdown--open')) return true;
        return false;
    }

    function handleTvRemoteSelect(phase, keyCode, heldMs) {
        window.__k7TvNativeRemote = true;

        var active = document.activeElement;
        var openMenuEl = active && active.closest ? active.closest('.k7-menu-dropdown--open') : null;

        if (openMenuEl) {
            if (phase === 'up' && heldMs < 600) {
                if (active && active !== document.body) {
                    // Menu-local activatable fields (e.g. actor K7SearchSelect) must use the
                    // same edit + soft-keyboard path as page-level K7TextField on TV.
                    if (toggleActivatableEdit(active)) {
                        return;
                    }
                    if (active.classList.contains('k7-menu-close')
                        || active.classList.contains('k7-menu-item')
                        || active.tagName === 'BUTTON') {
                        active.click();
                    }
                }
            }
            if (phase === 'long-up' || phase === 'up') {
                cancelMediaCardLongPress();
                _mediaCardPressStart = null;
            }
            return;
        }

        var videoOverlay = getVideoControlsOverlay(active);

        if (videoOverlay && isVideoControlsHidden(videoOverlay)) {
            if (phase === 'up' && heldMs < 600) {
                handleHiddenVideoPlayerSelect(makeFakeKeyEvent(keyCode, active));
            }
            return;
        }

        var fakeEvent = makeFakeKeyEvent(keyCode, active);

        if (phase === 'down') {
            var downCtx = resolveMediaCardLongPress(fakeEvent);
            if (!downCtx) return;
            cancelMediaCardLongPress();
            _mediaCardPressStart = {
                card: downCtx.card,
                link: downCtx.link,
                startTime: Date.now()
            };
            _mediaCardLongPress = {
                card: downCtx.card,
                link: downCtx.link,
                triggered: false
            };
            return;
        }

        if (phase === 'long') {
            var longCtx = resolveMediaCardLongPress(fakeEvent);
            if (!longCtx) return;

            cancelMediaCardLongPress();
            if (!isMediaCardMenuOpen(longCtx.card)) {
                openMediaCardMenu(longCtx.card);
            }
            swallowNextEnterClick();
            window.K7 = window.K7 || {};
            window.K7._suppressEnterUntilKeyUp = true;
            _mediaCardLongPress = { card: longCtx.card, link: longCtx.link, triggered: true };
            return;
        }

        if (phase === 'long-up') {
            cancelMediaCardLongPress();
            _mediaCardPressStart = null;
            swallowNextEnterClick();
            window.K7 = window.K7 || {};
            window.K7._suppressEnterUntilKeyUp = false;
            return;
        }

        if (phase === 'up') {
            var upCtx = resolveMediaCardLongPress(fakeEvent);
            var pressStart = _mediaCardPressStart;
            var state = _mediaCardLongPress;
            var card = (upCtx && upCtx.card) || (state && state.card) || (pressStart && pressStart.card);
            var link = (state && state.link) || (pressStart && pressStart.link) || (upCtx && upCtx.link);

            cancelMediaCardLongPress();
            _mediaCardPressStart = null;

            if (state && state.triggered) {
                swallowNextEnterClick();
                return;
            }

            if (card && link) {
                navigateMediaCardLink(link);
                return;
            }

            if (active && active !== document.body && heldMs < 600) {
                // MAUI Android TV consumes Select keys before keydown reaches handleEnter.
                // Activatable controls (search fields, seekbar, sliders) must enter edit mode here.
                if (toggleActivatableEdit(active)) {
                    return;
                }
                var tag = (active.tagName || '').toLowerCase();
                if (tag === 'button' || tag === 'a' || active.classList.contains('focusable')) {
                    active.click();
                }
            }
        }
    }

    function getLongPressContainer(el) {
        if (!el || !el.closest) return null;
        var container = el.closest('[data-longpress]');
        if (!container) return null;
        var value = container.getAttribute('data-longpress');
        if (value === 'false') return null;
        return container;
    }

    function resolveMediaCardLongPress(e) {
        var activeEl = document.activeElement;
        var container = getLongPressContainer(activeEl);
        if (!container && e && e.target) {
            container = getLongPressContainer(e.target);
            if (container) activeEl = e.target;
        }
        if (!container) return null;
        var card = container.closest('.media-card');
        if (!card) return null;
        return {
            container: container,
            card: card,
            activeEl: activeEl,
            link: card.querySelector('a.media-card-link[href]')
        };
    }

    var _mediaCardLongPress = null;
    var _mediaCardPressStart = null;

    function cancelMediaCardLongPress() {
        if (_mediaCardLongPress && _mediaCardLongPress.timer) {
            clearTimeout(_mediaCardLongPress.timer);
        }
        _mediaCardLongPress = null;
    }

    function openMediaCardMenu(card) {
        if (isMediaCardMenuOpen(card)) {
            return true;
        }

        var container = card.querySelector('[data-longpress]');
        if (container && container._k7MediaCardDotNet) {
            invokeCallback(container._k7MediaCardDotNet, 'OpenContextMenuFromLongPressAsync');
            return true;
        }
        var activator = card.querySelector('[data-longpress-target] .k7-menu-activator-inner');
        if (activator) activator.click();
        return !!activator;
    }

    function navigateMediaCardLink(link) {
        if (!link) return;
        window.K7 = window.K7 || {};
        window.K7._allowMediaCardLinkClick = link;
        link.click();
    }

    function handleMediaCardLongPressKeyDown(e) {
        if (!isTvLongPressMode()) return false;
        if (window.__k7TvNativeRemote) return false;
        var ctx = resolveMediaCardLongPress(e);
        if (!ctx) return false;

        e.preventDefault();
        e.stopImmediatePropagation();

        if (!e.repeat) {
            _mediaCardPressStart = {
                card: ctx.card,
                link: ctx.link,
                startTime: Date.now()
            };
        } else if (_mediaCardPressStart && _mediaCardPressStart.card === ctx.card) {
            var heldMs = Date.now() - _mediaCardPressStart.startTime;
            if (heldMs >= 600 && (!_mediaCardLongPress || !_mediaCardLongPress.triggered)) {
                cancelMediaCardLongPress();
                openMediaCardMenu(ctx.card);
                swallowNextEnterClick();
                window.K7 = window.K7 || {};
                window.K7._suppressEnterUntilKeyUp = true;
                _mediaCardLongPress = { card: ctx.card, link: ctx.link, triggered: true };
                return true;
            }
        }

        cancelMediaCardLongPress();
        _mediaCardLongPress = {
            card: ctx.card,
            link: ctx.link,
            triggered: false,
            timer: setTimeout(function () {
                if (!_mediaCardLongPress) return;
                if (isMediaCardMenuOpen(ctx.card)) {
                    _mediaCardLongPress.triggered = true;
                    return;
                }
                _mediaCardLongPress.triggered = true;
                openMediaCardMenu(ctx.card);
                swallowNextEnterClick();
                window.K7 = window.K7 || {};
                window.K7._suppressEnterUntilKeyUp = true;
            }, 600)
        };
        return true;
    }

    function handleMediaCardLongPressKeyUp(e) {
        if (!isTvLongPressMode()) return false;
        if (window.__k7TvNativeRemote) return false;
        if (!isEnterKey(e.key, e.code, e.keyCode)) return false;

        var ctx = resolveMediaCardLongPress(e);
        var pressStart = _mediaCardPressStart;
        var state = _mediaCardLongPress;
        var card = (ctx && ctx.card) || (state && state.card) || (pressStart && pressStart.card);

        if (!card) {
            cancelMediaCardLongPress();
            _mediaCardPressStart = null;
            return false;
        }

        if (ctx && ctx.activeEl && !card.contains(ctx.activeEl)) {
            cancelMediaCardLongPress();
            _mediaCardPressStart = null;
            return false;
        }

        e.preventDefault();
        e.stopImmediatePropagation();

        var triggered = state && state.triggered;
        var link = (state && state.link) || (pressStart && pressStart.link) || (ctx && ctx.link);
        var heldMs = pressStart ? (Date.now() - pressStart.startTime) : 0;

        cancelMediaCardLongPress();
        _mediaCardPressStart = null;

        if (triggered || heldMs >= 600) {
            if (!triggered && !isMediaCardMenuOpen(card)) openMediaCardMenu(card);
            swallowNextEnterClick();
            window.K7 = window.K7 || {};
            window.K7._suppressEnterUntilKeyUp = true;
            return true;
        }

        navigateMediaCardLink(link);
        return true;
    }

    function handleHiddenVideoPlayerArrow(key, code, e) {
        if (!_videoPlayerRemoteRef) return false;

        e.preventDefault();
        e.stopImmediatePropagation();
        if (window.SpatialNavigation) SpatialNavigation.pause();

        var keyCode = e.keyCode || 0;
        if (key === 'ArrowLeft' || code === 'ArrowLeft' || keyCode === 37 || keyCode === 21) {
            invokeCallback(_videoPlayerRemoteRef, 'OnRemoteSeekLeft');
        } else if (key === 'ArrowRight' || code === 'ArrowRight' || keyCode === 39 || keyCode === 22) {
            invokeCallback(_videoPlayerRemoteRef, 'OnRemoteSeekRight');
        } else if (key === 'ArrowUp' || code === 'ArrowUp' || keyCode === 38 || keyCode === 19) {
            invokeCallback(_videoPlayerRemoteRef, 'OnRemoteVolumeUp');
        } else if (key === 'ArrowDown' || code === 'ArrowDown' || keyCode === 40 || keyCode === 20) {
            invokeCallback(_videoPlayerRemoteRef, 'OnRemoteVolumeDown');
        } else {
            return false;
        }
        return true;
    }

    function handleHiddenVideoPlayerSelect(e) {
        e.preventDefault();
        e.stopImmediatePropagation();
        swallowNextEnterClick();
        if (_videoPlayerRemoteRef) invokeCallback(_videoPlayerRemoteRef, 'OnRemoteSelect');
    }

    // Enter Handling

    function handleEnter(e) {
        var active = document.activeElement;
        if (!active || active === document.body) return;

        if (window.K7 && window.K7._suppressEnterUntilKeyUp) {
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        // Text inputs: let Enter pass through so form/Blazor handlers can fire
        if (active && isTextInput(active) && !isActivatable(active)) return;

        // Textareas need Enter for line breaks (only when in edit mode)
        if (active.tagName && active.tagName.toLowerCase() === 'textarea' && isEditing(active)) return;

        // If inside a hidden overlay, route OK/Enter to the player instead of focused controls.
        var videoOverlay = getVideoControlsOverlay(active);
        if (videoOverlay && isVideoControlsHidden(videoOverlay)) {
            handleHiddenVideoPlayerSelect(e);
            return;
        }

        // Long-press on [data-longpress]: block native Enter navigation on the <a> itself.
        // On TV, handleMediaCardLongPressKeyDown already owns timing and returns before this
        // runs. On desktop/mobile, MediaCard's own OnKeyDown/OnKeyUp own the short vs long
        // press timing, so the keydown must keep bubbling to Blazor - do not stop propagation
        // here or the component's @onkeydown handler never fires and Enter stops navigating.
        var longPressContainer = active.closest('[data-longpress]');
        if (longPressContainer) {
            e.preventDefault();
            var card = longPressContainer.closest('.media-card');
            var openMenu = card && card.querySelector('.k7-menu-dropdown--open');
            if (openMenu) {
                var menuItem = openMenu.querySelector('.k7-menu-item');
                if (menuItem) menuItem.focus({ preventScroll: true });
            }
            return;
        }

        if (toggleActivatableEdit(active)) {
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        var tag = (active.tagName || '').toLowerCase();
        var role = active.getAttribute('role') || '';
        if (tag === 'button' || tag === 'a') {
            if (document.documentElement.classList.contains('platform-tv') && getLongPressContainer(active)) {
                e.preventDefault();
                return;
            }
            // Native button/a elements receive click from Enter/DpadCenter natively.
            // Don't synthesize - DpadCenter fires both keydown AND click on Android TV,
            // causing double-fire if we also call .click() here.
            return;
        }
        if (role === 'button' || role === 'switch') {
            active.click();
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }
        // Fallback: any focusable element (e.g. table rows) gets click on Enter
        if (active.classList.contains('focusable')) {
            active.click();
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }

    function handleKeyUp(e) {
        var key = e.key;
        var code = e.code || '';

        if (key === 'ArrowLeft' || key === 'ArrowRight' || code === 'ArrowLeft' || code === 'ArrowRight'
            || e.keyCode === 37 || e.keyCode === 39 || e.keyCode === 21 || e.keyCode === 22) {
            var overlay = getVideoControlsOverlay(document.activeElement);
            if (overlay && isVideoControlsHidden(overlay)) {
                e.preventDefault();
                if (_videoPlayerRemoteRef) invokeCallback(_videoPlayerRemoteRef, 'OnRemoteSeekCommit');
                return;
            }
        }

        if (!isEnterKey(key, code, e.keyCode)) return;

        if (handleMediaCardLongPressKeyUp(e)) return;

        if (window.K7 && window.K7._suppressEnterUntilKeyUp) {
            window.K7._suppressEnterUntilKeyUp = false;
            e.preventDefault();
            e.stopImmediatePropagation();
            window.K7._swallowNextEnterClick = true;
            setTimeout(function () {
                if (window.K7) window.K7._swallowNextEnterClick = false;
            }, 50);
            var callbacks = window.K7._enterSuppressCallbacks
                ? window.K7._enterSuppressCallbacks.splice(0)
                : [];
            setTimeout(function () {
                for (var i = 0; i < callbacks.length; i++) {
                    try { callbacks[i](); } catch (err) { /* ignore */ }
                }
            }, 0);
            var openMenu = document.querySelector('.k7-menu-dropdown--open');
            if (openMenu) {
                var menuItem = openMenu.querySelector('.k7-menu-item');
                if (menuItem) menuItem.focus({ preventScroll: true });
            }
            return;
        }
    }

    // Escape / Back Handling

    function findMediaCardForMenu(openMenu) {
        if (!openMenu) return null;
        var card = openMenu.closest('.media-card');
        if (card) return card;
        if (openMenu._k7MenuAnchor && openMenu._k7MenuAnchor.closest) {
            return openMenu._k7MenuAnchor.closest('.media-card');
        }
        return null;
    }

    function closeOpenK7MenuDropdowns() {
        var menus = document.querySelectorAll('.k7-menu-dropdown.k7-menu-dropdown--open');
        if (!menus.length) return false;

        var closedAny = false;
        var seenCards = [];

        for (var m = menus.length - 1; m >= 0; m--) {
            var openMenu = menus[m];
            var card = findMediaCardForMenu(openMenu);
            if (card && seenCards.indexOf(card) === -1) {
                seenCards.push(card);
                var container = card.querySelector('[data-longpress]');
                if (container && container._k7MediaCardDotNet) {
                    invokeCallback(container._k7MediaCardDotNet, 'CloseContextMenuFromBackAsync');
                    closedAny = true;
                }
            }
            popLayer(openMenu);
        }

        if (closedAny) return true;

        var openMenu = menus[menus.length - 1];
        var menuUid = openMenu.getAttribute && openMenu.getAttribute('data-sn-layer-uid');

        for (var i = _layers.length - 1; i >= 0; i--) {
            var layer = _layers[i];
            if (!layer.onClose) continue;
            var layerUid = layer.el && layer.el.getAttribute && layer.el.getAttribute('data-sn-layer-uid');
            if (layerElementsMatch(layer.el, openMenu) || (menuUid && layerUid && menuUid === layerUid)) {
                var staleCallback = layer.onClose;
                layer.onClose = null;
                popLayer(layer.el);
                invokeCallback(staleCallback, 'OnLayerClosed');
                return true;
            }
        }

        var closeBtn = openMenu.querySelector('.k7-menu-close');
        if (closeBtn) {
            closeBtn.click();
            return true;
        }

        var backdrops = document.body.querySelectorAll('.k7-backdrop');
        for (var b = backdrops.length - 1; b >= 0; b--) {
            backdrops[b].click();
        }

        return backdrops.length > 0;
    }

    function handleEscape(e) {
        var active = document.activeElement;

        if (active && isEditing(active)) {
            if (isTextInput(active) && isTvLongPressMode()) {
                _tvEditDismissViaBack = true;
            }
            stopEditing(active);
            if (window.SpatialNavigation) SpatialNavigation.resume();
            active.dispatchEvent(new CustomEvent('sn:editcancel', { bubbles: false }));
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        if (isOpenSearchSelectInput(active)) return;

        var playbackDetail = document.querySelector('.playback-settings-menu--open.playback-settings-menu--detail');
        if (playbackDetail) {
            e.preventDefault();
            e.stopImmediatePropagation();
            var activeNav = playbackDetail.querySelector('.playback-settings-nav-item--active');
            if (activeNav) activeNav.click();
            return;
        }

        var playbackOpen = document.querySelector('.playback-settings-menu--open:not(.playback-settings-menu--detail)');
        if (playbackOpen) {
            e.preventDefault();
            e.stopImmediatePropagation();
            var closeBtn = playbackOpen.querySelector('.playback-settings-close');
            if (closeBtn) closeBtn.click();
            return;
        }

        if (closeOpenK7MenuDropdowns()) {
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        var layer = peekLayer();
        if (layer && layer.type !== 'page') {
            e.preventDefault();
            e.stopImmediatePropagation();

            var isSubmenuLayer = layer.el && (
                layer.el.getAttribute('data-sn-submenu') === 'true' ||
                (layer.el.closest && !!layer.el.closest('.k7-menu--submenu'))
            );

            if (layer.onClose) {
                var isOverlay = layer.type === 'overlay';
                var staleCallback = layer.onClose;
                if (!isOverlay) layer.onClose = null;
                if (!isOverlay) popLayer(layer.el);
                invokeCallback(staleCallback, 'OnLayerClosed', function (ok) {
                    if (!ok) closeLayerDom(layer);
                });
                return;
            }

            if (isSubmenuLayer) {
                popLayer(layer.el);
                var submenuRoot = layer.el.closest('.k7-menu--submenu');
                if (submenuRoot) {
                    var activator = submenuRoot.querySelector('.k7-menu-activator');
                    if (activator) activator.click();
                }
                return;
            }

            if (layer.type === 'overlay' && isVideoPlayerActive() && handleVideoPlayerBack()) {
                return;
            }

            popLayer(layer.el);
            var closeTarget = null;
            var closeSelector = layer.el.getAttribute('data-sn-layer-close');
            if (closeSelector === 'self') {
                closeTarget = layer.el;
            } else if (closeSelector) {
                closeTarget = document.querySelector(closeSelector);
            }
            if (!closeTarget) {
                var parent = layer.el.parentElement;
                while (parent && parent !== document.body) {
                    closeTarget = parent.querySelector(':scope > .k7-backdrop');
                    if (closeTarget) break;
                    parent = parent.parentElement;
                }
            }
            if (closeTarget) {
                closeTarget.click();
            }
            return;
        }

        e.preventDefault();
        e.stopImmediatePropagation();
        handleBackNav();
    }

    function handleBackKey(e) {
        var key = e.key;
        if (key !== 'Backspace' && key !== 'GoBack' && key !== 'XF86Back') return;
        handleEscape(e);
    }

    function handleBackNav() {
        if (isVideoPlayerActive()) {
            if (handleVideoPlayerBack()) return;
            return;
        }

        var path = window.location.pathname;
        if (_homePattern.test(path)) {
            if (_homeEscapeTimer) {
                clearTimeout(_homeEscapeTimer);
                _homeEscapeTimer = null;
                if (_homeEscapeCallback) invokeCallback(_homeEscapeCallback, 'OnHomeEscapeSecond');
                return;
            }
            if (_homeEscapeCallback) invokeCallback(_homeEscapeCallback, 'OnHomeEscapeFirst');
            _homeEscapeTimer = setTimeout(function () { _homeEscapeTimer = null; }, 3000);
            return;
        }

        // Block back navigation on auth pages (no navbar flicker)
        if (/^\/(sign-in|linkdevice|select-profile|select-user)(\/|$)/.test(path)) {
            return;
        }

        var previousUrl = window.location.href;
        window.history.back();
        var checkCount = 0;
        var checker = setInterval(function () {
            checkCount++;
            if (window.location.href !== previousUrl || checkCount > 10) {
                clearInterval(checker);
                setTimeout(function () {
                    if (shouldRefocusPage(document.activeElement)) {
                        ensurePageFocus();
                    }
                }, 100);
            }
        }, 50);
    }

    function closeLayerDom(layer) {
        if (!layer || !layer.el) return;
        popLayer(layer.el);
        var closeTarget = null;
        var closeSelector = layer.el.getAttribute('data-sn-layer-close');
        if (closeSelector === 'self') {
            closeTarget = layer.el;
        } else if (closeSelector) {
            closeTarget = document.querySelector(closeSelector);
        }
        if (!closeTarget) {
            var parent = layer.el.parentElement;
            while (parent && parent !== document.body) {
                closeTarget = parent.querySelector(':scope > .k7-backdrop');
                if (closeTarget) break;
                parent = parent.parentElement;
            }
        }
        if (closeTarget) closeTarget.click();
    }

    function invokeCallback(callback, methodName, onComplete) {
        if (!callback) {
            if (onComplete) onComplete(false);
            return;
        }
        if (callback.invokeMethodAsync) {
            try {
                var promise = callback.invokeMethodAsync(methodName || 'Invoke');
                if (promise && promise.then) {
                    promise.then(
                        function () { if (onComplete) onComplete(true); },
                        function () { if (onComplete) onComplete(false); }
                    );
                } else if (onComplete) {
                    onComplete(true);
                }
            } catch (ex) {
                if (onComplete) onComplete(false);
            }
            return;
        }
        if (typeof callback === 'function') callback();
        if (onComplete) onComplete(true);
    }

    // Key Handler

    function isOpenSearchSelectInput(el) {
        return !!(el && el.closest && el.closest('.k7-search-select--open'));
    }

    function isPrintableCharacterKey(e) {
        return e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey;
    }

    function handleKeyDown(e) {
        var key = e.key;
        var layer = peekLayer();

        if (!window.__snBlurAdded) {
            window.__snBlurAdded = true;
            document.addEventListener('blur', function (ev) {
                if (!ev.target || !ev.target.hasAttribute || !ev.target.hasAttribute('data-sn-editing')) return;
                var editingEl = ev.target;
                // Seekbar/slider divs and desktop blur-to-commit keep the old behavior.
                if (!isTextInput(editingEl) || !isTvLongPressMode()) {
                    stopEditing(editingEl);
                    if (window.SpatialNavigation) SpatialNavigation.resume();
                    return;
                }
                if (_tvEditDismissViaBack) {
                    _tvEditDismissViaBack = false;
                    return;
                }
                // TV text inputs: blur right after edit start is usually a spurious WebView/IME
                // side effect while the keyboard opens. Later blur (IME Back) should exit edit
                // mode in one step instead of leaving data-sn-editing set.
                setTimeout(function () {
                    if (!editingEl.isConnected || !editingEl.hasAttribute('data-sn-editing')) return;
                    if (document.activeElement === editingEl) return;
                    if (Date.now() - _tvTextEditStartedAt < TV_TEXT_EDIT_BLUR_GRACE_MS) {
                        editingEl.focus({ preventScroll: true });
                        if (document.activeElement !== editingEl && window.K7 && window.K7.showSoftKeyboard) {
                            window.K7.showSoftKeyboard();
                        }
                        return;
                    }
                    stopEditing(editingEl);
                    if (window.SpatialNavigation) SpatialNavigation.resume();
                    editingEl.dispatchEvent(new CustomEvent('sn:editcancel', { bubbles: false }));
                }, 50);
            }, true);
        }

        var activeEl = document.activeElement;
        var searchRoot = activeEl && activeEl.closest ? activeEl.closest('.k7-search-select') : null;
        if (searchRoot) {
            if (isPrintableCharacterKey(e)) {
                var searchInput = searchRoot.querySelector('input, textarea');
                if (searchInput && isEditing(searchInput)) {
                    if (window.SpatialNavigation) SpatialNavigation.pause();
                    return;
                }
            }
            if (searchRoot.classList.contains('k7-search-select--open')) {
                if (key === 'ArrowDown' || key === 'ArrowUp' || key === 'Escape') {
                    if (window.SpatialNavigation) SpatialNavigation.pause();
                    return;
                }
                if (isEnterKey(key, e.code, e.keyCode)) {
                    var enterInput = searchRoot.querySelector('input, textarea');
                    if (enterInput && isEditing(enterInput)) {
                        if (window.SpatialNavigation) SpatialNavigation.pause();
                        return;
                    }
                }
            }
        }

        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(key) !== -1
            || ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(e.code || '') !== -1
            || [19, 20, 21, 22, 37, 38, 39, 40].indexOf(e.keyCode || 0) !== -1) {
            ensureSidebarFocusVisible();
            var el = document.activeElement;
            // Next-episode overlay: keep focus and navigation inside the overlay
            var nepOverlay = document.querySelector('.nep-overlay');
            if (nepOverlay && nepOverlay.isConnected) {
                var nepLayer = peekLayer();
                if (!nepLayer || nepLayer.el !== nepOverlay) {
                    pushLayer(nepOverlay, 'overlay', { focusSelector: '.k7-btn' });
                }
                if (!nepOverlay.contains(el)) {
                    var nepItems = getFocusables(nepOverlay);
                    if (nepItems.length > 0) {
                        nepItems[0].focus({ preventScroll: true });
                        e.preventDefault();
                        return;
                    }
                }
            }
            // Text inputs/textareas: let arrows through when focused (editing or non-activatable)
            if (el && isTextInput(el) && (isEditing(el) || !isActivatable(el))) {
                if (window.SpatialNavigation) SpatialNavigation.pause();
                return;
            }
            // When overlay is hidden, route arrows to the player for seek/volume HUD.
            var videoOverlay = getVideoControlsOverlay(el);
            if (videoOverlay && isVideoControlsHidden(videoOverlay)) {
                if (handleHiddenVideoPlayerArrow(key, e.code || '', e)) return;
            }
            if (el && el.closest('[data-carousel]') && handleCarouselNav(el, key)) {
                e.preventDefault();
                e.stopPropagation();
                return;
            }
            if ((key === 'ArrowDown' || key === 'ArrowUp') && window.K7 && window.K7.TvDetailScroll) {
                window.K7.TvDetailScroll.handleVerticalNav(key, el);
            }
            // When an activatable element is in editing mode, let the event through
            if (el && isActivatable(el) && isEditing(el)) {
                if (window.SpatialNavigation) SpatialNavigation.pause();
                // Don't preventDefault on native range inputs - browser handles arrow keys
                if (el.tagName !== 'INPUT' || el.type !== 'range') {
                    e.preventDefault();
                }
                return;
            }
        }

        var active = document.activeElement;
        if (window.SpatialNavigation) {
            if (active && (isEditing(active) || (isTextInput(active) && !isActivatable(active)))) {
                SpatialNavigation.pause();
            } else {
                SpatialNavigation.resume();
            }
        }

        if (isEnterKey(key, e.code, e.keyCode)) {
            if (handleMediaCardLongPressKeyDown(e)) return;
            var videoOverlay = getVideoControlsOverlay(activeEl);
            if (videoOverlay && isVideoControlsHidden(videoOverlay)) {
                handleHiddenVideoPlayerSelect(e);
                return;
            }
            if (window.K7 && window.K7._suppressEnterUntilKeyUp) {
                e.preventDefault();
                e.stopImmediatePropagation();
                return;
            }
            if (window.K7 && window.K7._swallowNextEnterClick) {
                window.K7._swallowNextEnterClick = false;
            }
            handleEnter(e);
            return;
        }
        if (key === 'Escape' || key === 'GoBack' || key === 'BrowserBack') { handleEscape(e); return; }
        if (key === 'Backspace' || key === 'XF86Back') { handleBackKey(e); return; }
        if (key === ' ' && active && active.closest('.video-controls-overlay')) { e.preventDefault(); }
    }

    // Focus Guard - prevent focus from escaping the active layer

    var _guardingFocus = false;

    document.addEventListener('toggle', handleNavGroupToggle, true);

    document.addEventListener('focus', function (e) {
        if (_guardingFocus) return;

        // Layer guard
        var layer = peekLayer();
        if (layer && layer.el) {
            if (!e.target || e.target === document.body) return;
            if (!layer.el.contains(e.target)) {
                var items = getFocusables(layer.el);
                if (items.length > 0) {
                    _guardingFocus = true;
                    items[0].focus({ preventScroll: true });
                    _guardingFocus = false;
                }
                return;
            }
        }

        // Section enter-to-last-focused
        if (e.target && e.target !== document.body && e.target.closest) {
            var section = e.target.closest('[data-sn-section]');
            if (section) {
                var id = section.getAttribute('data-sn-section');
                var enterTo = section.getAttribute('data-sn-enter');
                if (id !== _currentSectionId && enterTo === 'last-focused' && _sectionLastFocused[id]) {
                    var last = _sectionLastFocused[id];
                    if (last.isConnected && last !== e.target && section.contains(last) && isElementVisible(last)) {
                        _guardingFocus = true;
                        _currentSectionId = id;
                        last.focus({ preventScroll: true });
                        _guardingFocus = false;
                        return;
                    }
                }
                _currentSectionId = id;
                _sectionLastFocused[id] = e.target;
            } else {
                _currentSectionId = null;
            }
        }
    }, true);

    // Focus Scroll Listener

    document.addEventListener('focus', function (e) {
        var el = e.target;
        if (!el || !el.closest) return;

        setTimeout(function () {
            if (!el.matches || !el.matches(FOCUSABLE)) return;
            if (isNearPageTop(el)) {
                var nearTopScrollRoot = getFocusScrollRoot(el);
                if (nearTopScrollRoot && nearTopScrollRoot.scrollTop > 0) {
                    nearTopScrollRoot.scrollTo({ top: 0, behavior: 'smooth' });
                    return;
                }
            }
            if (!_carouselNavHandled) {
                scrollCarouselToElement(el);
            }
            var tvScrollRoot = el.closest('[data-tv-scroll]');
            var hasTvScroll = !!(tvScrollRoot && window.K7 && window.K7.TvDetailScroll && window.K7.TvDetailScroll.hasInstance(tvScrollRoot));
            if (tvScrollRoot && !el.closest('[data-tv-scroll-zone="below"]')) {
                if (!_carouselNavHandled) {
                    scrollCarouselToElement(el);
                }
                if (hasTvScroll) {
                    window.K7.TvDetailScroll.clampMainView(el);
                    return;
                }
            }
            if (el.closest('[data-carousel-item]')) {
                // Carousel items are horizontally positioned by embla (handled above);
                // only the vertical page position may still need adjusting so the card's
                // footer metadata below the poster is not clipped.
                var cardScrollRoot = hasTvScroll ? tvScrollRoot : getFocusScrollRoot(el);
                if (cardScrollRoot) {
                    scrollCardIntoTvView(cardScrollRoot, el);
                }
                return;
            }
            var focusScrollRoot = getFocusScrollRoot(el);
            if (focusScrollRoot && focusScrollRoot.classList.contains('page-scrollable')) {
                scrollCardIntoTvView(focusScrollRoot, el);
                return;
            }
            el.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
        }, 10);
    }, true);

    // Focus First

    function queryFocusSelector(selector) {
        if (!selector) return null;
        var roots = [
            document.querySelector('.app-main .page-viewport'),
            document.querySelector('.empty-layout'),
            document.querySelector('.app-main')
        ];
        for (var i = 0; i < roots.length; i++) {
            if (roots[i]) {
                var scoped = roots[i].querySelector(selector);
                if (scoped) return scoped;
            }
        }
        return document.querySelector(selector);
    }

    function isStandaloneAuthPage() {
        return /^\/(welcome|sign-in|linkdevice|select-profile|select-user)(\/|$)/.test(window.location.pathname);
    }

    function applyDomFocus(el) {
        if (!el) return;
        if (window.SpatialNavigation && SpatialNavigation.focus) {
            try {
                if (SpatialNavigation.focus(el, true)) return;
            } catch (ex) { }
        }
        try {
            el.focus({ preventScroll: true, focusVisible: true });
        } catch (ex) {
            el.focus({ preventScroll: true });
        }
    }

    function focusTargetElement(el) {
        if (!el || !el.isConnected) return false;
        if (el.closest('[data-carousel-item]')) {
            scrollCarouselToElement(el);
        }
        if (el.matches(FOCUSABLE)) {
            applyDomFocus(el);
            return true;
        }
        var focusable = el.querySelector(FOCUSABLE);
        if (focusable) {
            applyDomFocus(focusable);
            return true;
        }
        if (el.matches('input, textarea, select, button, a[href], [tabindex]:not([tabindex="-1"])')) {
            applyDomFocus(el);
            return true;
        }
        return false;
    }

    function getPageFocusRoot() {
        return document.querySelector('[data-page-focus]')
            || document.querySelector('.app-main .page-viewport')
            || document.querySelector('.empty-layout')
            || document.querySelector('.app-main');
    }

    function getFocusablesInPageContent() {
        var root = getPageFocusRoot();
        return root ? getFocusables(root) : [];
    }

    // Blazor bool true attributes render as "True"; treat those as marker-self, not CSS selectors.
    function isInitialFocusSelector(value) {
        if (value == null) return false;
        var v = String(value).trim();
        if (!v) return false;
        if (/^(true|false)$/i.test(v)) return false;
        return true;
    }

    function getPageFocusTarget() {
        var markers = document.querySelectorAll('[data-initial-focus]');
        for (var i = 0; i < markers.length; i++) {
            var marker = markers[i];
            var selector = marker.getAttribute('data-initial-focus');
            if (isInitialFocusSelector(selector)) {
                var target = marker.querySelector(selector) || queryFocusSelector(selector);
                if (target) return target;
                continue;
            }
            return marker;
        }
        return null;
    }

    function focusFirst(selector) {
        var delays = selector
            ? (isStandaloneAuthPage() ? [100, 300, 600, 1200, 2000] : [100, 300, 600])
            : [100];
        var resolved = false;

        function attempt(index) {
            if (resolved) return;

            var el = selector ? queryFocusSelector(selector) : null;
            if (el && focusTargetElement(el)) {
                resolved = true;
                return;
            }

            var pageTarget = getPageFocusTarget();
            if (pageTarget && focusTargetElement(pageTarget)) {
                resolved = true;
                return;
            }

            if (index < delays.length - 1) {
                setTimeout(function () { attempt(index + 1); }, delays[index + 1] - delays[index]);
            } else if (!resolved) {
                focusFirstInPage();
            }
        }

        setTimeout(function () { attempt(0); }, delays[0]);
    }

    function focusFirstFocusableInPage() {
        var layer = peekLayer();
        if (layer) {
            var items = getFocusables(layer.el);
            if (items.length > 0) { items[0].focus({ preventScroll: true }); return; }
        }

        var items = getFocusablesInPageContent();
        if (items.length > 0) {
            items[0].focus({ preventScroll: true });
            return;
        }

        var all = getFocusables(document.body).filter(function (el) {
            return !el.closest('.app-nav');
        });
        if (all.length > 0) all[0].focus({ preventScroll: true });
    }

    function focusFirstInPage() {
        var pageTarget = getPageFocusTarget();
        if (pageTarget && focusTargetElement(pageTarget)) return;
        focusFirstFocusableInPage();
    }

    function shouldRefocusPage(el) {
        if (!el || el === document.body || el === document.documentElement) return true;
        if (/^H[1-6]$/.test(el.tagName)) return true;
        if (el.hasAttribute('tabindex') && el.getAttribute('tabindex') === '-1' && !el.matches(FOCUSABLE)) return true;
        if (el.closest && el.closest('.app-nav') && getPageFocusTarget()) return true;
        if (isStandaloneAuthPage()) {
            var authTarget = getPageFocusTarget();
            if (authTarget && el !== authTarget && !authTarget.contains(el)) return true;
        }
        return false;
    }

    function ensurePageFocus() {
        if (_layers.length > 0) return;
        if (document.querySelector('[data-sn-editing]')) return;
        if (!shouldRefocusPage(document.activeElement)) return;

        var pageTarget = getPageFocusTarget();
        if (pageTarget) {
            if (isStandaloneAuthPage()) {
                focusFirst('[data-initial-focus]');
            } else {
                focusTargetElement(pageTarget);
            }
            return;
        }

        if (document.documentElement.classList.contains('platform-tv')) {
            focusFirstFocusableInPage();
        }
    }

    function focusElement(el) {
        applyDomFocus(el);
    }

    function onPageNavigated() {
        setTimeout(ensurePageFocus, 150);
    }

    // Home Escape

    function registerHomeEscape(dotNetRef, homePattern) {
        _homeEscapeCallback = dotNetRef;
        if (homePattern) _homePattern = new RegExp(homePattern);
    }

    function isVideoPlayerActive() {
        var container = document.querySelector('.video-container');
        return !!(container && isElementVisible(container));
    }

    function registerVideoPlayerBack(dotNetRef) {
        _videoPlayerBackCallback = dotNetRef;
    }

    function unregisterVideoPlayerBack() {
        _videoPlayerBackCallback = null;
    }

    function registerVideoPlayerRemote(dotNetRef) {
        _videoPlayerRemoteRef = dotNetRef;
    }

    function unregisterVideoPlayerRemote() {
        _videoPlayerRemoteRef = null;
    }

    function handleVideoPlayerBack() {
        if (!_videoPlayerBackCallback) return false;
        invokeCallback(_videoPlayerBackCallback, 'OnLayerClosed');
        return true;
    }

    function cancelEditingIn(rootSelector) {
        var root = rootSelector ? document.querySelector(rootSelector) : document;
        if (!root) return;
        var editing = root.querySelector('[data-sn-editing]');
        if (!editing) return;
        stopEditing(editing);
        if (window.SpatialNavigation) SpatialNavigation.resume();
        editing.dispatchEvent(new CustomEvent('sn:editcancel', { bubbles: false }));
    }

    // Utility

    function isFocusInside(el) {
        return !!(el && el.contains(document.activeElement));
    }

    function isElementEditing(el) {
        return !!(el && el.hasAttribute('data-sn-editing'));
    }

    function hasEditingIn(rootSelector) {
        var root = rootSelector ? document.querySelector(rootSelector) : document;
        if (!root) return false;
        return !!root.querySelector('[data-sn-editing]');
    }

    // Init

    function init() {
        if (_initialized) return;
        _initialized = true;

        if (window.SpatialNavigation) {
            SpatialNavigation.init();

            // Global filter: when a layer is open, restrict navigation to it.
            SpatialNavigation.set({
                navigableFilter: function (el) {
                    if (window.getComputedStyle(el).visibility === 'hidden') return false;
                    if (isInClosedSidebarNavGroup(el)) return false;
                    var layer = peekLayer();
                    if (layer && layer.el) {
                        return layer.el.contains(el);
                    }
                    return true;
                }
            });

            // Default section covering the full page.
            SpatialNavigation.add('default', {
                selector: FOCUSABLE,
                restrict: 'self-first',
                enterTo: 'last-focused'
            });

            SpatialNavigation.makeFocusable();

            // Auto-detection: watch for data-sn-layer and data-sn-section attributes.
            // More reliable than C# JS interop which can fail in MAUI.
            var _trackedLayerIds = {};
            var _layerUidCounter = 0;
            var _trackedSections = new Set();

            function syncSections() {
                var sectionEls = document.querySelectorAll('[data-sn-section]');
                var currentSet = new Set();

                sectionEls.forEach(function (el) {
                    var id = el.getAttribute('data-sn-section');
                    if (!id) return;
                    currentSet.add(id);
                    if (!_trackedSections.has(id)) {
                        var enterTo = el.getAttribute('data-sn-enter') || 'last-focused';
                        var restrict = el.getAttribute('data-sn-restrict') || 'self-first';
                        addSection(id, { enterTo: enterTo, restrict: restrict });
                    }
                });

                _trackedSections.forEach(function (id) {
                    if (!currentSet.has(id)) {
                        removeSection(id);
                    }
                });

                _trackedSections = currentSet;
            }

            function syncLayers() {
                var layerEls = document.querySelectorAll('[data-sn-layer]');
                var currentIds = {};

                layerEls.forEach(function (el) {
                    // Assign a stable UID if not present
                    var uid = el.getAttribute('data-sn-layer-uid');
                    if (!uid) {
                        uid = 'snl-' + (++_layerUidCounter);
                        el.setAttribute('data-sn-layer-uid', uid);
                    }
                    currentIds[uid] = el;

                    if (!_trackedLayerIds[uid]) {
                        // New layer appeared
                        var type = el.getAttribute('data-sn-layer') || 'popover';
                        pushLayer(el, type, {});
                    } else {
                        // Update element reference (may change between calls in MAUI WebView)
                        for (var i = 0; i < _layers.length; i++) {
                            if (_layers[i].el === _trackedLayerIds[uid] || (_layers[i].el.getAttribute && _layers[i].el.getAttribute('data-sn-layer-uid') === uid)) {
                                _layers[i].el = el;
                                break;
                            }
                        }
                    }
                });

                // Check for removed layers
                for (var uid in _trackedLayerIds) {
                    if (!currentIds[uid]) {
                        popLayer(_trackedLayerIds[uid]);
                    }
                }

                _trackedLayerIds = currentIds;
            }

            // Auto-refresh after any DOM mutation (covers all Blazor re-renders)
            syncSections();
            var observer = new MutationObserver(function () {
                scheduleRefresh();
                syncSections();
                syncLayers();
            });
            observer.observe(document.body, {
                childList: true,
                subtree: true,
                attributes: true,
                attributeFilter: ['disabled', 'tabindex', 'hidden', 'open', 'data-initial-focus', 'data-sn-layer', 'data-sn-section']
            });
        }

        document.addEventListener('keydown', handleKeyDown, true);
        document.addEventListener('keyup', handleKeyUp, true);
        document.addEventListener('click', function (e) {
            if (!window.K7 || !window.K7._swallowNextEnterClick) return;
            window.K7._swallowNextEnterClick = false;
            e.preventDefault();
            e.stopImmediatePropagation();
        }, true);
        document.addEventListener('click', function (e) {
            if (!isTvLongPressMode()) return;
            var target = e.target;
            if (!target || !target.closest) return;
            var link = target.closest('a.media-card-link[href]');
            if (!link || !link.closest('[data-longpress]')) return;
            if (window.K7 && window.K7._allowMediaCardLinkClick === link) {
                window.K7._allowMediaCardLinkClick = null;
                return;
            }
            e.preventDefault();
            e.stopImmediatePropagation();
        }, true);
        document.addEventListener('enhancedload', onPageNavigated);
        setTimeout(ensurePageFocus, 200);

        // Mouse click on activatable text inputs immediately enters edit mode
        document.addEventListener('mousedown', function (e) {
            var el = e.target;
            if (el && isTextInput(el) && isActivatable(el) && !isEditing(el)) {
                toggleActivatableEdit(el);
            }
        }, true);

        document.addEventListener('contextmenu', function (e) {
            var target = e.target;
            if (!target || !target.closest) return;
            if (target.closest('[data-longpress]')) {
                e.preventDefault();
            }
        }, true);

        window.K7 = window.K7 || {};
        window.K7.onTvRemoteSelect = handleTvRemoteSelect;
    }

    // Public API

    function handleBack() {
        var fakeEvent = { key: 'GoBack', preventDefault: function () {}, stopImmediatePropagation: function () {} };
        handleEscape(fakeEvent);
    }

    return {
        init: init,
        pushLayer: pushLayer,
        popLayer: popLayer,
        attachLayerCallback: attachLayerCallback,
        focusFirst: focusFirst,
        focusElement: focusElement,
        refresh: refresh,
        addSection: addSection,
        removeSection: removeSection,
        registerHomeEscape: registerHomeEscape,
        registerVideoPlayerBack: registerVideoPlayerBack,
        unregisterVideoPlayerBack: unregisterVideoPlayerBack,
        registerVideoPlayerRemote: registerVideoPlayerRemote,
        unregisterVideoPlayerRemote: unregisterVideoPlayerRemote,
        cancelEditingIn: cancelEditingIn,
        isFocusInside: isFocusInside,
        isElementEditing: isElementEditing,
        hasEditingIn: hasEditingIn,
        handleBack: handleBack
    };

})();

// RatingStars JS helper
window.K7 = window.K7 || {};

K7._backgroundLockCount = 0;
K7._dialogLockActive = false;

K7.setNativePlayerActive = function (active, windowsWebVideo) {
    document.documentElement.classList.toggle('native-player-active', !!active);
    document.body.classList.toggle('native-player-active', !!active);
    var useWindowsWebVideo = !!active && !!windowsWebVideo;
    document.documentElement.classList.toggle('windows-web-video', useWindowsWebVideo);
    document.body.classList.toggle('windows-web-video', useWindowsWebVideo);
    if (!active) {
        requestAnimationFrame(function () {
            var app = document.getElementById('app');
            if (app) app.style.removeProperty('visibility');
            var chrome = document.querySelectorAll('.app-nav, .app-nav-bar, .app-nav-popover, .k7-menu-dropdown');
            for (var i = 0; i < chrome.length; i++) {
                chrome[i].style.removeProperty('visibility');
                chrome[i].style.removeProperty('opacity');
            }
            if (window.SpatialNav && window.SpatialNav.refresh) window.SpatialNav.refresh();
        });
    }
};

K7._updateBackgroundLock = function () {
    var locked = K7._backgroundLockCount > 0 || K7._dialogLockActive;
    document.body.classList.toggle('k7-overlay-locked', locked);
};

K7.acquireBackgroundInteractionLock = function () {
    K7._backgroundLockCount++;
    K7._updateBackgroundLock();
};

K7.releaseBackgroundInteractionLock = function () {
    K7._backgroundLockCount = Math.max(0, K7._backgroundLockCount - 1);
    K7._updateBackgroundLock();
};

K7.acquireMobileBackgroundInteractionLock = function () {
    if (window.innerWidth >= 600) return false;
    K7.acquireBackgroundInteractionLock();
    return true;
};

K7.releaseMobileBackgroundInteractionLock = function () {
    if (window.innerWidth >= 600) return;
    K7.releaseBackgroundInteractionLock();
};

K7.setDialogOpen = function (open) {
    var shouldLock = !!open;
    if (shouldLock === !!K7._dialogLockActive) return;
    K7._dialogLockActive = shouldLock;
    K7._updateBackgroundLock();
};

K7.isImageLoaded = function (element) {
    return !!element && element.complete && element.naturalHeight > 0;
};

K7.scrollSearchSelectOptionIntoView = function (dropdown, index) {
    if (!dropdown || index < 0) return;
    var options = dropdown.querySelectorAll('.k7-search-select-option');
    var option = options[index];
    if (option) option.scrollIntoView({ block: 'nearest' });
};

K7.scrollSearchSelectIntoMenuView = function (root) {
    if (!root || window.innerWidth >= 600) return;
    var menu = root.closest('.k7-menu-dropdown');
    if (!menu) return;
    root.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
};

K7.bindSearchSelectMenuDismiss = function (root) {
    if (!root || window.innerWidth >= 600) return;
    var menu = root.closest('.k7-menu-dropdown');
    if (!menu) return;

    if (menu.__k7SearchSelectRoot === root && menu.__k7SearchDismissHandler) return;

    if (menu.__k7SearchDismissHandler) {
        menu.removeEventListener('pointerdown', menu.__k7SearchDismissHandler, true);
    }

    menu.__k7SearchSelectRoot = root;
    menu.__k7SearchDismissHandler = function (e) {
        if (!root.classList.contains('k7-search-select--editing')
            && !root.classList.contains('k7-search-select--open')) {
            return;
        }
        if (root.contains(e.target)) return;
        var input = root.querySelector('input, textarea');
        if (input) input.blur();
    };
    menu.addEventListener('pointerdown', menu.__k7SearchDismissHandler, true);
};

K7.unbindSearchSelectMenuDismiss = function (root) {
    if (!root) return;
    var menu = root.closest('.k7-menu-dropdown');
    if (!menu || menu.__k7SearchSelectRoot !== root) return;
    if (menu.__k7SearchDismissHandler) {
        menu.removeEventListener('pointerdown', menu.__k7SearchDismissHandler, true);
    }
    menu.__k7SearchDismissHandler = null;
    menu.__k7SearchSelectRoot = null;
};

K7.isFocusWithin = function (root) {
    return !!(root && document.activeElement && root.contains(document.activeElement));
};

K7.isSpatialEditingIn = function (root) {
    if (!root || !root.querySelector) return false;
    var input = root.querySelector('input, textarea');
    return !!(input && input.hasAttribute('data-sn-editing'));
};

K7.initSoftKeyboardBridge = function (dotNetRef) {
    K7._softKeyboardDotNetRef = dotNetRef;
};

K7.showSoftKeyboard = function () {
    if (K7._softKeyboardDotNetRef) {
        K7._softKeyboardDotNetRef.invokeMethodAsync('Show').catch(function () { });
    }
};

K7.hideSoftKeyboard = function () {
    if (K7._softKeyboardDotNetRef) {
        K7._softKeyboardDotNetRef.invokeMethodAsync('Hide').catch(function () { });
    }
};

// Gives the input real DOM focus without entering edit mode, so the existing
// OK/Enter activation flow (SpatialNav.handleEnter -> startEditing) picks it up
// the same way it does for a control the user spatial-navigated to manually.
// Do NOT start editing here: this runs from Blazor's OnAfterRenderAsync, outside
// a direct user gesture, so most WebViews (Android TV included) will not raise
// the on-screen keyboard even if we removed readonly and focused synchronously.
K7.focusSearchSelectInput = function (root) {
    var input = root && root.querySelector ? root.querySelector('input, textarea') : null;
    if (input) input.focus({ preventScroll: true });
};

K7.bindSearchSelectEditing = function (root, dotNetRef) {
    if (!root || !dotNetRef) return;
    var input = root.querySelector('input, textarea');
    if (!input || input.__k7SearchSelectBound) return;
    input.__k7SearchSelectBound = true;
    input.addEventListener('sn:editstart', function () {
        dotNetRef.invokeMethodAsync('OnSpatialEditStarted');
    });
    input.addEventListener('sn:editcancel', function () {
        dotNetRef.invokeMethodAsync('OnSpatialEditEnded');
    });
    input.addEventListener('sn:editcommit', function () {
        dotNetRef.invokeMethodAsync('OnSpatialEditEnded');
    });
};

K7.setSafeArea = function (top, bottom, left, right) {
    var s = document.documentElement.style;
    s.setProperty('--k7-safe-top', top + 'px');
    s.setProperty('--k7-safe-bottom', bottom + 'px');
    s.setProperty('--k7-safe-left', left + 'px');
    s.setProperty('--k7-safe-right', right + 'px');
};

K7.positionDropdown = function (root, dropdown) {
    if (!root || !dropdown) return;

    // On mobile, CSS bottom sheet handles positioning
    if (window.innerWidth < 600) return;

    var isSubmenu = !!root.closest('.k7-menu-dropdown');
    var anchor = K7._resolveMenuAnchor(root);
    var anchorRect = anchor.getBoundingClientRect();
    if (anchorRect.width === 0 && anchorRect.height === 0) {
        var mediaCard = root.closest('.media-card');
        if (mediaCard) {
            anchor = mediaCard.querySelector('.media-card-container') || mediaCard;
            anchorRect = anchor.getBoundingClientRect();
        }
    }
    var gap = 4;
    var inVideoPlayer = root.closest('.video-controls-overlay');
    if (inVideoPlayer && isSubmenu) gap = 8;

    // Use fixed positioning to escape stacking contexts
    dropdown.style.position = 'fixed';
    dropdown.style.top = '';
    dropdown.style.bottom = '';
    dropdown.style.left = '';
    dropdown.style.right = '';
    dropdown.style.maxHeight = '';
    dropdown.style.width = '';
    dropdown.style.minWidth = '';
    dropdown.style.overflowY = '';

    // Measure dropdown size
    dropdown.style.visibility = 'hidden';
    dropdown.style.opacity = '0';
    dropdown.style.display = 'block';
    var ddRect = dropdown.getBoundingClientRect();
    dropdown.style.display = '';
    dropdown.style.visibility = '';
    dropdown.style.opacity = '';

    var isPortaled = dropdown.classList.contains('k7-menu-portal');
    var cbOffset = isPortaled ? { left: 0, top: 0 } : K7._getFixedContainingBlockOffset(dropdown);

    var vw = window.innerWidth;
    var vh = window.innerHeight;

    if (isSubmenu) {
        var parentDropdown = root.parentElement && root.parentElement.closest
            ? root.parentElement.closest('.k7-menu-dropdown')
            : root.closest('.k7-menu-dropdown');
        if (!parentDropdown) return;
        var parentRect = parentDropdown.getBoundingClientRect();

        var leftOfParent = parentRect.left - ddRect.width - gap;
        if (inVideoPlayer || leftOfParent >= 8) {
            dropdown.style.left = (Math.max(8, leftOfParent) - cbOffset.left) + 'px';
        } else {
            var rightOfParent = parentRect.right + gap;
            if (rightOfParent + ddRect.width <= vw - 8) {
                dropdown.style.left = (rightOfParent - cbOffset.left) + 'px';
            } else {
                dropdown.style.left = (8 - cbOffset.left) + 'px';
            }
        }

        var top = anchorRect.top;
        if (top + ddRect.height > vh - 8) {
            top = vh - ddRect.height - 8;
        }
        if (top < 8) top = 8;
        dropdown.style.top = (top - cbOffset.top) + 'px';
        dropdown.style.maxHeight = 'min(320px, calc(100vh - 80px))';
        dropdown.style.overflowY = 'auto';
        dropdown.style.minWidth = Math.max(parentRect.width, 180) + 'px';
        dropdown.style.transform = 'none';
        dropdown.style.zIndex = '100014';
    } else {
        root.classList.remove('k7-menu--upward');
        var mediaCard = root.closest('.media-card');
        if (mediaCard) {
            dropdown.classList.add('k7-menu-dropdown--card-corner');
            var cardEl = mediaCard.querySelector('.media-card-container') || anchor;
            var cardRect = cardEl.getBoundingClientRect();
            K7._positionMediaCardDropdown(dropdown, mediaCard, cardRect, ddRect, cbOffset, vw, vh);
            return;
        }

        dropdown.classList.remove('k7-menu-dropdown--card-corner');

        // Root menu: open below/above the activator
        var spaceBelow = vh - anchorRect.bottom - gap;
        var spaceAbove = anchorRect.top - gap;
        var placeAbove = spaceBelow < ddRect.height && spaceAbove > spaceBelow;

        if (placeAbove) {
            root.classList.add('k7-menu--upward');
            dropdown.style.bottom = (vh - anchorRect.top + gap - cbOffset.top) + 'px';
        } else {
            dropdown.style.top = (anchorRect.bottom + gap - cbOffset.top) + 'px';
        }

        // Horizontal: align right edge to anchor right, shift if overflows
        var left = anchorRect.right - ddRect.width;
        if (left < 8) {
            left = 8;
        }
        if (left + ddRect.width > vw - 8) {
            left = vw - ddRect.width - 8;
        }
        dropdown.style.left = (left - cbOffset.left) + 'px';
    }
};

K7._positionMediaCardDropdown = function (dropdown, mediaCard, cardRect, ddRect, cbOffset, vw, vh) {
    var margin = 8;
    var gap = 4;
    var isTv = document.documentElement.classList.contains('platform-tv')
        || window.__k7TvNativeRemote === true;

    dropdown.style.transform = 'none';
    dropdown.style.zIndex = '100014';
    dropdown.style.width = 'max-content';
    dropdown.style.minWidth = '180px';

    if (isTv) {
        dropdown.style.maxWidth = Math.min(480, vw - margin * 2) + 'px';
        dropdown.style.maxHeight = 'none';
        dropdown.style.overflowY = 'visible';

        dropdown.style.visibility = 'hidden';
        dropdown.style.display = 'block';
        var naturalRect = dropdown.getBoundingClientRect();
        dropdown.style.visibility = '';
        dropdown.style.display = '';

        var menuHeight = naturalRect.height;
        var menuWidth = naturalRect.width;

        var top = cardRect.top + (cardRect.height - menuHeight) / 2;
        if (top < margin) top = margin;
        if (top + menuHeight > vh - margin) {
            top = margin;
            if (menuHeight > vh - margin * 2) {
                dropdown.style.maxHeight = (vh - margin * 2) + 'px';
                dropdown.style.overflowY = 'auto';
            }
        }

        dropdown.style.top = (top - cbOffset.top) + 'px';
        dropdown.style.bottom = '';

        var left = cardRect.left + (cardRect.width - menuWidth) / 2;
        if (left < margin) left = margin;
        if (left + menuWidth > vw - margin) left = Math.max(margin, vw - margin - menuWidth);

        dropdown.style.left = (left - cbOffset.left) + 'px';
        return;
    }

    dropdown.style.overflowY = 'auto';
    dropdown.style.maxWidth = Math.min(280, vw - margin * 2) + 'px';
    dropdown.style.maxHeight = 'min(320px, calc(100vh - ' + (margin * 2) + 'px))';

    var activator = mediaCard.querySelector('.media-card-menu .k7-menu-activator-inner');
    var trigger = activator ? activator.getBoundingClientRect() : null;
    var triggerTop = trigger && trigger.height > 0 ? trigger.top : cardRect.bottom - 48;

    // Anchor bottom edge just above the three-dots trigger (immune to height measure drift)
    var menuBottom = triggerTop - gap;
    dropdown.style.top = '';
    dropdown.style.bottom = (vh - menuBottom - cbOffset.top) + 'px';

    // Left-aligned to card when there is room on the right; otherwise right-aligned to card
    var left = cardRect.left;
    if (left + ddRect.width > vw - margin) {
        left = cardRect.right - ddRect.width;
    }
    if (left < margin) {
        left = margin;
    }

    dropdown.style.left = (left - cbOffset.left) + 'px';
};

K7._suppressEnterUntilKeyUp = false;
K7._swallowNextEnterClick = false;
K7._enterSuppressCallbacks = [];

K7.registerMediaCardLongPress = function (el, dotNetRef) {
    if (!el) return;
    el._k7MediaCardDotNet = dotNetRef;
};

K7.unregisterMediaCardLongPress = function (el) {
    if (!el) return;
    el._k7MediaCardDotNet = null;
};

K7.suppressEnterUntilKeyUp = function (callback) {
    K7._suppressEnterUntilKeyUp = true;
    K7._swallowNextEnterClick = true;
    if (typeof callback === 'function') {
        K7._enterSuppressCallbacks.push(callback);
    }
};

K7.positionDropdownDeferred = function (root, dropdown) {
    K7.positionDropdown(root, dropdown);
    requestAnimationFrame(function () {
        requestAnimationFrame(function () {
            K7.positionDropdown(root, dropdown);
        });
    });
};

K7._resolveMenuAnchor = function (root) {
    if (!root) return root;
    var mediaCard = root.closest('.media-card');
    if (mediaCard) {
        var cardContainer = mediaCard.querySelector('.media-card-container')
            || mediaCard.querySelector('[data-longpress]');
        if (cardContainer) return cardContainer;
    }
    var activatorEl = root.querySelector('.k7-menu-activator');
    return (activatorEl && activatorEl.firstElementChild) || root;
};

K7._getFixedContainingBlockOffset = function (el) {
    var parent = el.parentElement;
    while (parent && parent !== document.documentElement) {
        var style = getComputedStyle(parent);
        if (style.transform !== 'none' || style.filter !== 'none' ||
            style.backdropFilter !== 'none' || style.willChange === 'transform') {
            var rect = parent.getBoundingClientRect();
            return { left: rect.left, top: rect.top };
        }
        parent = parent.parentElement;
    }
    return { left: 0, top: 0 };
};

K7.resetDropdown = function (root) {
    if (!root) return;
    root.classList.remove('k7-menu--open', 'k7-menu--upward');
    // Keep inline position styles intact during the CSS close transition (0.15s)
    // to prevent the dropdown from snapping to its default position before fading out.
    // They will be overwritten by positionDropdown on next open.
};

K7._teleportMenuElement = function (el, root) {
    if (!el) return;
    if (!el._k7MenuAnchor) {
        el._k7MenuAnchor = document.createComment('k7-menu-portal');
        root.appendChild(el._k7MenuAnchor);
    }
    if (el.parentElement !== document.body) {
        document.body.appendChild(el);
    }
    el.classList.add('k7-menu-portal');
};

K7._restoreMenuElement = function (el, root) {
    if (!el || !el.classList) return;
    if (el._k7MenuAnchor && root && root.isConnected && el._k7MenuAnchor.parentNode === root) {
        root.insertBefore(el, el._k7MenuAnchor);
    } else if (el._k7MenuAnchor && el._k7MenuAnchor.parentNode) {
        el._k7MenuAnchor.remove();
        el._k7MenuAnchor = null;
    } else if (el.parentElement === document.body) {
        // Blazor loses track of reparented nodes; drop body orphans.
        el.remove();
        return;
    }
    el.classList.remove('k7-menu-portal', 'k7-menu-dropdown--teleported');
};

K7._pruneOrphanedMenuBackdrops = function () {
    if (document.querySelector('.k7-menu-dropdown--open')) return;
    var orphans = document.body.querySelectorAll('.k7-menu-portal.k7-backdrop');
    for (var i = 0; i < orphans.length; i++) {
        orphans[i].remove();
    }
    var menus = document.querySelectorAll('.k7-menu');
    for (var m = 0; m < menus.length; m++) {
        K7._releaseMobileOverlayLock(menus[m]);
    }
};

K7._releaseMobileOverlayLock = function (owner) {
    if (owner && owner._k7MobileLockAcquired) {
        K7.releaseBackgroundInteractionLock();
        owner._k7MobileLockAcquired = false;
    }
};

K7._acquireMobileOverlayLock = function (owner) {
    if (window.innerWidth >= 600 || !owner || owner._k7MobileLockAcquired) return;
    K7.acquireBackgroundInteractionLock();
    owner._k7MobileLockAcquired = true;
};

K7.releaseMobileOverlayLock = function (owner) {
    K7._releaseMobileOverlayLock(owner);
};

K7._hasFixedContainingBlockAncestor = function (el) {
    var parent = el.parentElement;
    while (parent && parent !== document.body) {
        var style = getComputedStyle(parent);
        if (style.transform !== 'none' || style.filter !== 'none' ||
            style.backdropFilter !== 'none' || style.willChange === 'transform') {
            return true;
        }
        if (style.overflow === 'hidden' || style.overflowX === 'hidden' || style.overflowY === 'hidden') {
            if (parent.classList.contains('carousel-viewport') || parent.closest('.carousel-viewport')) {
                return true;
            }
        }
        // Page-level z-index stacking contexts trap fixed dropdowns under later siblings
        // (e.g. movie hero vs cast carousel). Skip dialog/modal layers (z >= 500).
        if (style.position !== 'static' && style.zIndex !== 'auto') {
            var z = parseInt(style.zIndex, 10);
            if (!isNaN(z) && z > 0 && z < 500) {
                return true;
            }
        }
        parent = parent.parentElement;
    }
    return false;
};

K7._needsMenuPortal = function (root) {
    if (!root) return false;
    if (root.closest('.fullscreen-player')) {
        return false;
    }
    if (root.closest('.k7-dialog-backdrop')) {
        return false;
    }
    if (window.innerWidth < 600) {
        return true;
    }
    return K7._hasFixedContainingBlockAncestor(root);
};

K7.attachMobileMenu = function (root, dropdown, backdrop) {
    if (!root || !dropdown) return;

    var inMediaCard = !!root.closest('.media-card');
    if (!inMediaCard && !K7._needsMenuPortal(root)) {
        if (dropdown.classList.contains('k7-menu-portal')) {
            K7._restoreMenuElement(dropdown, root);
            K7._restoreMenuElement(backdrop, root);
        }
        K7._releaseMobileOverlayLock(root);
        dropdown.classList.remove('k7-menu-dropdown--video-player');
        if (backdrop) backdrop.classList.remove('k7-backdrop--video-player');
        if (dropdown) dropdown.classList.remove('k7-menu-dropdown--teleported');
        return;
    }

    K7._teleportMenuElement(dropdown, root);
    if (backdrop) K7._teleportMenuElement(backdrop, root);
    if (window.innerWidth < 600) {
        dropdown.classList.add('k7-menu-dropdown--teleported');
        K7._acquireMobileOverlayLock(root);
    } else {
        dropdown.classList.remove('k7-menu-dropdown--teleported');
        K7._releaseMobileOverlayLock(root);
    }
    if (root.closest('.video-controls-overlay')) {
        dropdown.classList.add('k7-menu-dropdown--video-player');
        if (backdrop) backdrop.classList.add('k7-backdrop--video-player');
    }
};

K7.positionPlaybackSettingsDetail = function (stack, detail) {
    if (!stack || !detail || window.innerWidth < 600) return;

    detail.style.top = '';
    detail.style.maxHeight = '';
    detail.style.overflowY = '';

    var pad = 8;
    var vh = window.innerHeight;
    var stackRect = stack.getBoundingClientRect();
    var detailRect = detail.getBoundingClientRect();

    var top = detailRect.top;
    var height = detailRect.height;
    var maxAvailable = vh - pad * 2;

    if (height > maxAvailable) {
        detail.style.maxHeight = maxAvailable + 'px';
        detail.style.overflowY = 'auto';
        height = maxAvailable;
    }

    var bottom = top + height;
    if (bottom > vh - pad) {
        top = Math.max(pad, vh - pad - height);
    }

    if (top < pad) {
        top = pad;
        detail.style.maxHeight = maxAvailable + 'px';
        detail.style.overflowY = 'auto';
    }

    detail.style.top = Math.round(top - stackRect.top) + 'px';
};

K7.detachMobileMenu = function (root, dropdown, backdrop) {
    if (root) K7._releaseMobileOverlayLock(root);
    if (dropdown && dropdown.classList) {
        K7._restoreMenuElement(dropdown, root);
        dropdown.classList.remove('k7-menu-dropdown--video-player', 'k7-menu-dropdown--teleported', 'k7-menu-dropdown--open');
    }
    if (backdrop && backdrop.classList) {
        K7._restoreMenuElement(backdrop, root);
        if (backdrop.isConnected && backdrop.parentElement === document.body) {
            backdrop.remove();
        }
        backdrop.classList.remove('k7-backdrop--video-player');
    }
    K7._pruneOrphanedMenuBackdrops();
};

K7.attachSelectPortal = function (root, dropdown, backdrop) {
    if (!root || !dropdown) return;
    K7._teleportMenuElement(dropdown, root);
    if (backdrop) {
        K7._teleportMenuElement(backdrop, root);
        backdrop.classList.add('k7-backdrop--teleported');
    }
    dropdown.classList.add('k7-select-dropdown--teleported');
    K7._acquireMobileOverlayLock(root);
};

K7.detachSelectPortal = function (root, dropdown, backdrop) {
    if (!root) return;
    K7._releaseMobileOverlayLock(root);
    K7._restoreMenuElement(dropdown, root);
    if (backdrop) {
        K7._restoreMenuElement(backdrop, root);
        backdrop.classList.remove('k7-backdrop--teleported');
    }
    if (dropdown) dropdown.classList.remove('k7-select-dropdown--teleported');
};

K7.positionSelectDropdown = function (button, dropdown) {
    if (!button || !dropdown) return;

    // On mobile, CSS bottom sheet handles positioning after teleport.
    if (window.innerWidth < 600) return;

    var rect = button.getBoundingClientRect();
    var gap = 4;
    var cbOffset = K7._getFixedContainingBlockOffset(dropdown);

    dropdown.style.position = 'fixed';
    dropdown.style.top = '';
    dropdown.style.bottom = '';
    dropdown.style.left = '';
    dropdown.style.right = '';
    dropdown.style.maxHeight = 'min(280px, calc(100vh - 80px))';
    dropdown.style.overflowY = 'auto';

    dropdown.style.visibility = 'hidden';
    dropdown.style.opacity = '0';
    dropdown.style.display = 'block';
    dropdown.style.width = 'max-content';
    dropdown.style.minWidth = rect.width + 'px';
    dropdown.style.maxWidth = (window.innerWidth - 16) + 'px';
    var ddRect = dropdown.getBoundingClientRect();
    dropdown.style.display = '';
    dropdown.style.visibility = '';
    dropdown.style.opacity = '';

    var vh = window.innerHeight;
    var vw = window.innerWidth;

    var spaceBelow = vh - rect.bottom - gap;
    var spaceAbove = rect.top - gap;
    var placeAbove = spaceBelow < ddRect.height && spaceAbove > spaceBelow;

    if (placeAbove) {
        dropdown.style.bottom = (vh - rect.top + gap - cbOffset.top) + 'px';
        dropdown.style.top = '';
    } else {
        dropdown.style.top = (rect.bottom + gap - cbOffset.top) + 'px';
        dropdown.style.bottom = '';
    }

    var width = Math.min(Math.max(rect.width, ddRect.width), vw - 16);
    var left = rect.left;
    if (left + width > vw - 8) {
        left = vw - width - 8;
    }
    if (left < 8) left = 8;
    dropdown.style.left = (left - cbOffset.left) + 'px';
    dropdown.style.width = width + 'px';
    dropdown.style.minWidth = rect.width + 'px';
};

K7.TvDetailScroll = (function () {
    var _instances = new WeakMap();

    function getZone(root, name) {
        return root.querySelector('[data-tv-scroll-zone="' + name + '"]');
    }

    function createHandlers(inst) {
        function scrollToMain(instant) {
            inst.showingBelow = false;
            inst.root.scrollTo({ top: 0, behavior: instant ? 'instant' : 'smooth' });
        }

        function scrollToBelow() {
            var main = getZone(inst.root, 'main');
            var below = getZone(inst.root, 'below');
            if (!main || !below) return;
            inst.showingBelow = true;
            inst.root.scrollTo({ top: main.offsetHeight, behavior: 'smooth' });
        }

        function clampMainView() {
            if (!inst.showingBelow && inst.root.scrollTop !== 0) {
                inst.root.scrollTop = 0;
            }
        }

        function isInZone(el, zoneName) {
            if (!el || !el.closest) return false;
            var zone = getZone(inst.root, zoneName);
            return !!(zone && zone.contains(el));
        }

        function isInZoneCarousel(el, zoneName) {
            return isInZone(el, zoneName) && !!el.closest('[data-carousel]');
        }

        function onFocusIn(e) {
            if (!inst.root.contains(e.target)) return;
            if (isInZone(e.target, 'actions')) {
                scrollToMain(false);
            } else if (isInZone(e.target, 'below')) {
                if (!inst.showingBelow) {
                    scrollToBelow();
                }
            } else if ((isInZoneCarousel(e.target, 'episodes') || isInZoneCarousel(e.target, 'seasons')) && !inst.showingBelow) {
                clampMainView();
            }
        }

        inst.scrollToMain = scrollToMain;
        inst.scrollToBelow = scrollToBelow;
        inst.clampMainView = clampMainView;
        inst.onFocusIn = onFocusIn;
        inst.handleVerticalNav = function (key, el) {
            if (!el || !inst.root.contains(el)) return;

            if (!getZone(inst.root, 'below')) return;

            if (key === 'ArrowDown') {
                if (isInZoneCarousel(el, 'episodes') || isInZoneCarousel(el, 'seasons')) {
                    scrollToBelow();
                } else if (isInZone(el, 'actions') && !getZone(inst.root, 'seasons') && !getZone(inst.root, 'episodes')) {
                    scrollToBelow();
                }
            }
        };
    }

    return {
        init: function (root) {
            if (!root) return;
            K7.TvDetailScroll.dispose(root);
            var inst = { root: root, showingBelow: false, onFocusIn: null };
            createHandlers(inst);
            root.scrollTop = 0;
            root.addEventListener('focusin', inst.onFocusIn, true);
            _instances.set(root, inst);
        },
        dispose: function (root) {
            var inst = root ? _instances.get(root) : null;
            if (!inst) return;
            if (inst.onFocusIn) {
                inst.root.removeEventListener('focusin', inst.onFocusIn, true);
            }
            _instances.delete(root);
        },
        sync: function (root) {
            var inst = root ? _instances.get(root) : null;
            if (inst && !inst.showingBelow) {
                inst.root.scrollTop = 0;
            }
        },
        hasInstance: function (root) {
            return !!(root && _instances.has(root));
        },
        clampMainView: function (el) {
            var root = el && el.closest('[data-tv-scroll]');
            var inst = root && _instances.get(root);
            if (inst) inst.clampMainView();
        },
        handleVerticalNav: function (key, el) {
            var root = el && el.closest('[data-tv-scroll]');
            var inst = root && _instances.get(root);
            if (inst) inst.handleVerticalNav(key, el);
        }
    };
})();

K7.SeasonTv = K7.TvDetailScroll;

K7.scrollToElement = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

K7.scrollToTop = function (element) {
    if (element) element.scrollTop = 0;
};

K7.focusById = function (id) {
    var el = document.getElementById(id);
    if (!el) return;
    var target = el.querySelector('.focusable') || el;
    target.focus({ preventScroll: false });
};

K7.RatingStars = {
    _instances: new WeakMap(),
    init: function (el, dotNetRef) {
        if (!el || typeof el.addEventListener !== 'function') return;
        var handlers = {
            start: function () { dotNetRef.invokeMethodAsync('OnEditStart'); },
            commit: function () { dotNetRef.invokeMethodAsync('OnEditCommit'); },
            cancel: function () { dotNetRef.invokeMethodAsync('OnEditCancel'); }
        };
        el.addEventListener('sn:editstart', handlers.start);
        el.addEventListener('sn:editcommit', handlers.commit);
        el.addEventListener('sn:editcancel', handlers.cancel);
        K7.RatingStars._instances.set(el, handlers);
    },
    dispose: function (el) {
        var h = K7.RatingStars._instances.get(el);
        if (h) {
            el.removeEventListener('sn:editstart', h.start);
            el.removeEventListener('sn:editcommit', h.commit);
            el.removeEventListener('sn:editcancel', h.cancel);
            K7.RatingStars._instances.delete(el);
        }
    }
};

K7.SeekBar = {
    _instances: new WeakMap(),
    init: function (el, dotNetRef) {
        if (!el || typeof el.addEventListener !== 'function') return;
        var handlers = {
            start: function () { dotNetRef.invokeMethodAsync('OnEditStart'); },
            commit: function () { dotNetRef.invokeMethodAsync('OnEditCommit'); },
            cancel: function () { dotNetRef.invokeMethodAsync('OnEditCancel'); }
        };
        el.addEventListener('sn:editstart', handlers.start);
        el.addEventListener('sn:editcommit', handlers.commit);
        el.addEventListener('sn:editcancel', handlers.cancel);
        K7.SeekBar._instances.set(el, handlers);
    },
    dispose: function (el) {
        var h = K7.SeekBar._instances.get(el);
        if (h) {
            el.removeEventListener('sn:editstart', h.start);
            el.removeEventListener('sn:editcommit', h.commit);
            el.removeEventListener('sn:editcancel', h.cancel);
            K7.SeekBar._instances.delete(el);
        }
    }
};

document.addEventListener('DOMContentLoaded', function () { SpatialNav.init(); });
if (document.readyState !== 'loading') { SpatialNav.init(); }