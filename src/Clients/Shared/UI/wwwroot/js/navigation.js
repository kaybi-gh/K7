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
    var _refreshTimer = null;

    var FOCUSABLE = [
        '.focusable'
    ].join(', ');

    // Layer Stack

    function pruneDisconnectedLayers() {
        for (var i = _layers.length - 1; i >= 0; i--) {
            if (_layers[i].el && !_layers[i].el.isConnected) {
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
            if (existing === el || (existing.isSameNode && existing.isSameNode(el))) {
                // Merge options into existing layer (e.g. onClose callback arriving after auto-detection)
                if (opts.onClose && !_layers[i].onClose) _layers[i].onClose = opts.onClose;
                if (opts.focusSelector && !_layers[i].focusSelector) _layers[i].focusSelector = opts.focusSelector;
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
        // Fallback: match by reference or isSameNode
        for (var i = 0; i < _layers.length; i++) {
            var existing = _layers[i].el;
            if (existing === el || (existing.isSameNode && existing.isSameNode(el))) {
                if (onClose) _layers[i].onClose = onClose;
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
        for (var i = _layers.length - 1; i >= 0; i--) {
            var existing = _layers[i].el;
            if (existing === el || (existing.isSameNode && existing.isSameNode(el))) {
                var layer = _layers.splice(i, 1)[0];
                if (layer.restoreFocus && layer.restoreFocus.isConnected) {
                    layer.restoreFocus.focus({ preventScroll: true });
                }
                return;
            }
        }
    }

    function peekLayer() {
        pruneDisconnectedLayers();
        return _layers.length > 0 ? _layers[_layers.length - 1] : null;
    }

    function autoFocusLayer(layer) {
        var attempts = 5;
        function tryFocus() {
            var container = layer.el;
            var target = null;
            if (layer.focusSelector) {
                target = container.querySelector(layer.focusSelector);
            }
            if (!target) {
                var items = Array.from(container.querySelectorAll(FOCUSABLE));
                items = items.filter(function (el) {
                    return el.offsetParent !== null && window.getComputedStyle(el).visibility !== 'hidden';
                });
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
        }, 100);
    }

    function refresh() {
        if (window.SpatialNavigation) SpatialNavigation.makeFocusable();
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
            if (el.closest('[data-carousel-item]')) return isVisibleInCarouselViewport(el);
            return isElementVisible(el);
        });
    }

    // Scroll

    function scrollCarouselToElement(el) {
        var carouselRoot = el.closest('[data-carousel]');
        if (!carouselRoot || !carouselRoot.__embla) return;
        var item = el.closest('[data-carousel-item]');
        if (!item) return;
        var items = Array.from(carouselRoot.querySelectorAll('[data-carousel-item]'));
        var idx = items.indexOf(item);
        if (idx >= 0) carouselRoot.__embla.scrollTo(idx);
    }

    function isNearPageTop(el) {
        return el.getBoundingClientRect().top < window.innerHeight * 0.4;
    }

    // Carousel Navigation

    function handleCarouselNav(active, direction) {
        var carousel = active.closest('[data-carousel]');
        if (!carousel) return false;
        if (direction === 'ArrowUp' || direction === 'ArrowDown') return false;

        var currentItem = active.closest('[data-carousel-item]');
        if (!currentItem) return false;

        var allItems = Array.from(carousel.querySelectorAll('[data-carousel-item]'));
        var currentIdx = allItems.indexOf(currentItem);
        if (currentIdx === -1) return false;

        var targetIdx = direction === 'ArrowRight' ? currentIdx + 1 : currentIdx - 1;
        if (targetIdx < 0 || targetIdx >= allItems.length) return false;

        var targetItem = allItems[targetIdx];
        var target = targetItem.matches(FOCUSABLE) ? targetItem : targetItem.querySelector(FOCUSABLE);
        if (!target) return false;

        if (carousel.__embla) carousel.__embla.scrollTo(targetIdx);
        setTimeout(function () { target.focus({ preventScroll: true }); }, 10);
        return true;
    }

    // Editing Mode

    function isEditing(el) { return el && el.hasAttribute('data-sn-editing'); }
    function startEditing(el) {
        el.setAttribute('data-sn-editing', 'true');
    }
    function stopEditing(el) {
        el.removeAttribute('data-sn-editing');
    }
    function isActivatable(el) { return el && el.hasAttribute('data-sn-activatable'); }

    function isTextInput(el) {
        var tag = (el.tagName || '').toLowerCase();
        if (tag === 'textarea') return true;
        if (tag !== 'input') return false;
        var type = (el.getAttribute('type') || 'text').toLowerCase();
        return ['text', 'password', 'search', 'email', 'number', 'tel', 'url'].indexOf(type) !== -1;
    }

    // Enter Handling

    function handleEnter(e) {
        var active = document.activeElement;
        if (!active || active === document.body) return;

        // If inside a hidden overlay, suppress button click but let keydown reach Blazor
        var overlay = active.closest('.video-controls-overlay');
        if (overlay && overlay.style.opacity === '0') {
            e.preventDefault();
            return;
        }

        if (isActivatable(active)) {
            e.preventDefault();
            e.stopImmediatePropagation();
            if (isEditing(active)) {
                stopEditing(active);
                if (window.SpatialNavigation) SpatialNavigation.resume();
                active.dispatchEvent(new CustomEvent('sn:editcommit', { bubbles: false }));
            } else {
                startEditing(active);
                if (window.SpatialNavigation) SpatialNavigation.pause();
                active.dispatchEvent(new CustomEvent('sn:editstart', { bubbles: false }));
            }
            return;
        }

        var tag = (active.tagName || '').toLowerCase();
        var role = active.getAttribute('role') || '';
        if (tag === 'button' || tag === 'a') {
            // Native button/a elements receive click from Enter/DpadCenter natively.
            // Don't synthesize - DpadCenter fires both keydown AND click on Android TV,
            // causing double-fire if we also call .click() here.
            return;
        }
        if (role === 'button' || role === 'switch') {
            active.click();
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }

    // Escape / Back Handling

    function handleEscape(e) {
        var active = document.activeElement;
        if (active && isEditing(active)) {
            stopEditing(active);
            if (window.SpatialNavigation) SpatialNavigation.resume();
            active.dispatchEvent(new CustomEvent('sn:editcancel', { bubbles: false }));
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        var layer = peekLayer();
        if (layer && layer.type !== 'page') {
            if (layer.type === 'overlay') {
                if (layer.onClose) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    invokeCallback(layer.onClose, 'OnLayerClosed');
                }
                return;
            }
            e.preventDefault();
            e.stopImmediatePropagation();
            var onClose = layer.onClose;
            popLayer(layer.el);
            if (onClose) {
                invokeCallback(onClose, 'OnLayerClosed');
            } else {
                // Auto-detected layer: trigger close via backdrop click or custom event
                var closeSelector = layer.el.getAttribute('data-sn-layer-close');
                var closeTarget = null;
                if (closeSelector === 'self') {
                    closeTarget = layer.el;
                } else if (closeSelector) {
                    closeTarget = document.querySelector(closeSelector);
                }
                if (!closeTarget) {
                    // Walk up to find nearest .k7-backdrop (K7Menu, K7Select)
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
        var active = document.activeElement;
        if (active && (isEditing(active) || isTextInput(active))) return;
        handleEscape(e);
    }

    function handleBackNav() {
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
        if (/^\/(sign-in|linkdevice|select-user)(\/|$)/.test(path)) {
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
                    if (!document.activeElement || document.activeElement === document.body) {
                        focusFirstInPage();
                    }
                }, 100);
            }
        }, 50);
    }

    function invokeCallback(callback, methodName) {
        if (!callback) return;
        if (callback.invokeMethodAsync) {
            try { callback.invokeMethodAsync(methodName || 'Invoke'); } catch (ex) { }
            return;
        }
        if (typeof callback === 'function') callback();
    }

    // Key Handler

    function handleKeyDown(e) {
        var key = e.key;
        var layer = peekLayer();

        if (!window.__snBlurAdded) {
            window.__snBlurAdded = true;
            document.addEventListener('blur', function (ev) {
                if (ev.target && ev.target.hasAttribute && ev.target.hasAttribute('data-sn-editing')) {
                    ev.target.removeAttribute('data-sn-editing');
                }
            }, true);
        }

        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(key) !== -1) {
            var el = document.activeElement;
            // When overlay is hidden, block spatial navigation so arrows go to Blazor for seek/volume
            var hiddenOverlay = el && el.closest('.video-controls-overlay');
            if (hiddenOverlay && hiddenOverlay.style.opacity === '0') {
                if (window.SpatialNavigation) SpatialNavigation.pause();
                e.preventDefault();
                return;
            }
            if (el && el.closest('[data-carousel]') && handleCarouselNav(el, key)) {
                e.preventDefault();
                e.stopPropagation();
                return;
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
            if (active && isEditing(active)) {
                SpatialNavigation.pause();
            } else {
                SpatialNavigation.resume();
            }
        }

        if (key === 'Enter') { handleEnter(e); return; }
        if (key === 'Escape' || key === 'GoBack' || key === 'BrowserBack') { handleEscape(e); return; }
        if (key === 'Backspace' || key === 'XF86Back') { handleBackKey(e); return; }
        if (key === ' ' && active && active.closest('.video-controls-overlay')) { e.preventDefault(); }
    }

    // Focus Guard - prevent focus from escaping the active layer

    var _guardingFocus = false;

    // Section last-focused tracking: remember and restore last focused element per section
    var _sectionLastFocused = {};
    var _currentSectionId = null;

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
        if (e.target && e.target !== document.body) {
            var section = e.target.closest('[data-sn-section]');
            if (section) {
                var id = section.getAttribute('data-sn-section');
                var enterTo = section.getAttribute('data-sn-enter');
                if (id !== _currentSectionId && enterTo === 'last-focused' && _sectionLastFocused[id]) {
                    var last = _sectionLastFocused[id];
                    if (last.isConnected && last !== e.target && section.contains(last)) {
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
                var mainContent = document.querySelector('.app-main');
                if (mainContent && mainContent.scrollTop > 0) {
                    mainContent.scrollTo({ top: 0, behavior: 'smooth' });
                    return;
                }
            }
            scrollCarouselToElement(el);
            if (!el.closest('[data-carousel-item]')) {
                el.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
            }
        }, 10);
    }, true);

    // Focus First

    function focusFirst(selector) {
        setTimeout(function () {
            var el = null;
            if (selector) {
                var main = document.querySelector('.app-main');
                if (main) el = main.querySelector(selector);
                if (!el) el = document.querySelector(selector);
            }
            if (el) {
                var focusable = el.matches(FOCUSABLE) ? el : el.querySelector(FOCUSABLE);
                if (focusable) { focusable.focus({ preventScroll: true }); return; }
            }
            focusFirstInPage();
        }, 100);
    }

    function focusFirstInPage() {
        var layer = peekLayer();
        if (layer) {
            var items = getFocusables(layer.el);
            if (items.length > 0) { items[0].focus({ preventScroll: true }); return; }
        }
        var main = document.querySelector('.app-main');
        if (main) {
            var items = getFocusables(main);
            if (items.length > 0) { items[0].focus({ preventScroll: true }); return; }
        }
        var all = getFocusables(document.body);
        if (all.length > 0) all[0].focus({ preventScroll: true });
    }

    function focusElement(el) {
        if (el) el.focus({ preventScroll: true });
    }

    function onPageNavigated() {
        if (_layers.length > 0) return;
        setTimeout(function () {
            if (!document.activeElement || document.activeElement === document.body) {
                focusFirstInPage();
            }
        }, 150);
    }

    // Home Escape

    function registerHomeEscape(dotNetRef, homePattern) {
        _homeEscapeCallback = dotNetRef;
        if (homePattern) _homePattern = new RegExp(homePattern);
    }

    // Utility

    function isFocusInside(el) {
        return !!(el && el.contains(document.activeElement));
    }

    function isElementEditing(el) {
        return !!(el && el.hasAttribute('data-sn-editing'));
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
                attributeFilter: ['disabled', 'tabindex', 'hidden', 'data-sn-layer', 'data-sn-section']
            });
        }

        document.addEventListener('keydown', handleKeyDown, true);
        document.addEventListener('enhancedload', onPageNavigated);
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
        isFocusInside: isFocusInside,
        isElementEditing: isElementEditing,
        handleBack: handleBack
    };

})();

// RatingStars JS helper
window.K7 = window.K7 || {};

K7.scrollToElement = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
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