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
        }, 100);
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
            el.focus();
        }
    }
    function stopEditing(el) {
        el.removeAttribute('data-sn-editing');
        if (isTextInput(el)) {
            el.setAttribute('readonly', '');
        }
    }
    function isActivatable(el) { return el && el.hasAttribute('data-sn-activatable'); }

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

    // Long-press helpers for touch fallback in navigation.js
    var LONG_PRESS_MS = 600;

    function isEnterKey(key) {
        return key === 'Enter' || key === 'NumpadEnter';
    }

    function findLongPressTarget(container) {
        if (!container) return null;
        var root = container.closest('.media-card') || container.parentElement;
        if (!root) return null;
        return root.querySelector('[data-longpress-target]');
    }

    function openLongPressMenu(container) {
        var targetRoot = findLongPressTarget(container);
        if (!targetRoot) return;
        var activator = targetRoot.querySelector('.k7-menu-activator-inner')
            || targetRoot.querySelector('.k7-menu-activator')
            || targetRoot.querySelector('button');
        if (activator) activator.click();
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

        // If inside a hidden overlay, suppress button click but let keydown reach Blazor
        var overlay = active.closest('.video-controls-overlay');
        if (overlay && overlay.style.opacity === '0') {
            e.preventDefault();
            return;
        }

        // Long-press on [data-longpress]: block native Enter navigation; MediaCard handles timing.
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
        if (!isEnterKey(e.key)) return;

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
                var item = openMenu.querySelector('.k7-menu-item');
                if (item) item.focus({ preventScroll: true });
            }
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
                layer.onClose = null;
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

    function handleKeyDown(e) {
        var key = e.key;
        var layer = peekLayer();

        if (!window.__snBlurAdded) {
            window.__snBlurAdded = true;
            document.addEventListener('blur', function (ev) {
                if (ev.target && ev.target.hasAttribute && ev.target.hasAttribute('data-sn-editing')) {
                    stopEditing(ev.target);
                    if (window.SpatialNavigation) SpatialNavigation.resume();
                }
            }, true);
        }

        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(key) !== -1) {
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
            if (active && (isEditing(active) || (isTextInput(active) && !isActivatable(active)))) {
                SpatialNavigation.pause();
            } else {
                SpatialNavigation.resume();
            }
        }

        if (isEnterKey(key)) {
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
        if (e.target && e.target !== document.body && e.target.closest) {
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
            if (!_carouselNavHandled) {
                scrollCarouselToElement(el);
            }
            if (!el.closest('[data-carousel-item]')) {
                el.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
            }
        }, 10);
    }, true);

    // Focus First

    function queryFocusSelector(selector) {
        if (!selector) return null;
        var main = document.querySelector('.app-main');
        var el = main ? main.querySelector(selector) : null;
        if (!el) el = document.querySelector(selector);
        return el;
    }

    function focusTargetElement(el) {
        if (!el || !el.isConnected) return false;
        if (el.matches(FOCUSABLE)) {
            el.focus({ preventScroll: true });
            return true;
        }
        var focusable = el.querySelector(FOCUSABLE);
        if (focusable) {
            focusable.focus({ preventScroll: true });
            return true;
        }
        if (el.matches('input, textarea, select, button, a[href], [tabindex]:not([tabindex="-1"])')) {
            el.focus({ preventScroll: true });
            return true;
        }
        return false;
    }

    function getPageFocusTarget() {
        var markers = document.querySelectorAll('[data-initial-focus]');
        for (var i = 0; i < markers.length; i++) {
            var marker = markers[i];
            var selector = marker.getAttribute('data-initial-focus');
            if (selector) {
                var target = queryFocusSelector(selector);
                if (target) return target;
                continue;
            }
            return marker;
        }
        return null;
    }

    function focusFirst(selector) {
        setTimeout(function () {
            var el = selector ? queryFocusSelector(selector) : null;
            if (el && focusTargetElement(el)) return;
            focusFirstInPage();
        }, 100);
    }

    function focusFirstFocusableInPage() {
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

    function focusFirstInPage() {
        var pageTarget = getPageFocusTarget();
        if (pageTarget && focusTargetElement(pageTarget)) return;
        focusFirstFocusableInPage();
    }

    function shouldRefocusPage(el) {
        if (!el || el === document.body || el === document.documentElement) return true;
        if (/^H[1-6]$/.test(el.tagName)) return true;
        if (el.hasAttribute('tabindex') && el.getAttribute('tabindex') === '-1' && !el.matches(FOCUSABLE)) return true;
        return false;
    }

    function ensurePageFocus() {
        if (_layers.length > 0) return;
        if (!shouldRefocusPage(document.activeElement)) return;

        var pageTarget = getPageFocusTarget();
        if (pageTarget) {
            focusTargetElement(pageTarget);
            return;
        }

        if (document.documentElement.classList.contains('platform-tv')) {
            focusFirstFocusableInPage();
        }
    }

    function focusElement(el) {
        if (el) el.focus({ preventScroll: true });
    }

    function onPageNavigated() {
        setTimeout(ensurePageFocus, 150);
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
                attributeFilter: ['disabled', 'tabindex', 'hidden', 'data-initial-focus', 'data-sn-layer', 'data-sn-section']
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
        document.addEventListener('enhancedload', onPageNavigated);
        setTimeout(ensurePageFocus, 200);

        // Mouse click on activatable text inputs immediately enters edit mode
        document.addEventListener('mousedown', function (e) {
            var el = e.target;
            if (el && isTextInput(el) && isActivatable(el) && !isEditing(el)) {
                startEditing(el);
                if (window.SpatialNavigation) SpatialNavigation.pause();
            }
        }, true);

        // Touch long-press on [data-longpress] containers (mobile)
        var _touchLongPress = null;
        document.addEventListener('touchstart', function (e) {
            var target = e.target;
            if (!target || !target.closest) return;
            var container = target.closest('[data-longpress]');
            if (!container) return;
            _touchLongPress = {
                container: container,
                timer: setTimeout(function () {
                    if (!_touchLongPress) return;
                    _touchLongPress.triggered = true;
                    openLongPressMenu(container);
                }, LONG_PRESS_MS),
                triggered: false,
                startX: e.touches[0].clientX,
                startY: e.touches[0].clientY
            };
        }, { passive: true });

        document.addEventListener('touchmove', function (e) {
            if (!_touchLongPress) return;
            var dx = e.touches[0].clientX - _touchLongPress.startX;
            var dy = e.touches[0].clientY - _touchLongPress.startY;
            if (dx * dx + dy * dy > 100) {
                clearTimeout(_touchLongPress.timer);
                _touchLongPress = null;
            }
        }, { passive: true });

        document.addEventListener('touchend', function (e) {
            if (!_touchLongPress) return;
            clearTimeout(_touchLongPress.timer);
            var state = _touchLongPress;
            _touchLongPress = null;
            if (state.triggered) {
                e.preventDefault();
            }
        });

        document.addEventListener('touchcancel', function () {
            if (_touchLongPress) {
                clearTimeout(_touchLongPress.timer);
                _touchLongPress = null;
            }
        }, { passive: true });
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

K7.isImageLoaded = function (element) {
    return !!element && element.complete && element.naturalHeight > 0;
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
            K7._positionMediaCardDropdown(dropdown, anchorRect, ddRect, cbOffset, vw, vh);
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

K7._positionMediaCardDropdown = function (dropdown, anchorRect, ddRect, cbOffset, vw, vh) {
    var margin = 8;
    dropdown.style.bottom = '';
    dropdown.style.transform = 'none';
    dropdown.style.zIndex = '100014';
    dropdown.style.maxHeight = 'min(320px, calc(100vh - 16px))';
    dropdown.style.overflowY = 'auto';
    dropdown.style.minWidth = '180px';
    dropdown.style.width = 'max-content';
    dropdown.style.maxWidth = Math.min(280, vw - margin * 2) + 'px';

    // top-start + left-start: menu top-left on card top-left, overlapping the corner
    var left = anchorRect.left;
    var top = anchorRect.top;

    // top-end + right-start: flip when menu would overflow the viewport on the right
    if (left + ddRect.width > vw - margin) {
        left = anchorRect.right - ddRect.width;
    }

    if (left < margin) left = margin;
    if (left + ddRect.width > vw - margin) left = vw - ddRect.width - margin;

    if (top + ddRect.height > vh - margin) {
        top = Math.max(margin, vh - ddRect.height - margin);
    }
    if (top < margin) top = margin;

    dropdown.style.left = (left - cbOffset.left) + 'px';
    dropdown.style.top = (top - cbOffset.top) + 'px';
};

K7._suppressEnterUntilKeyUp = false;
K7._swallowNextEnterClick = false;
K7._enterSuppressCallbacks = [];
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
        var cardContainer = mediaCard.querySelector('[data-longpress]');
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
    if (!el || !el._k7MenuAnchor) return;
    if (el._k7MenuAnchor.parentNode === root) {
        root.insertBefore(el, el._k7MenuAnchor);
    }
    el.classList.remove('k7-menu-portal', 'k7-menu-dropdown--teleported');
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
        parent = parent.parentElement;
    }
    return false;
};

K7._needsMenuPortal = function (root) {
    if (!root) return false;
    if (root.closest('.fullscreen-player')) {
        return false;
    }
    if (window.innerWidth < 600) {
        return true;
    }
    return K7._hasFixedContainingBlockAncestor(root);
};

K7.attachMobileMenu = function (root, dropdown, backdrop) {
    if (!root || !dropdown) return;

    if (!K7._needsMenuPortal(root)) {
        if (dropdown.classList.contains('k7-menu-portal')) {
            K7._restoreMenuElement(dropdown, root);
            K7._restoreMenuElement(backdrop, root);
        }
        dropdown.classList.remove('k7-menu-dropdown--video-player');
        if (backdrop) backdrop.classList.remove('k7-backdrop--video-player');
        if (dropdown) dropdown.classList.remove('k7-menu-dropdown--teleported');
        return;
    }

    K7._teleportMenuElement(dropdown, root);
    if (backdrop) K7._teleportMenuElement(backdrop, root);
    if (window.innerWidth < 600) {
        dropdown.classList.add('k7-menu-dropdown--teleported');
    } else {
        dropdown.classList.remove('k7-menu-dropdown--teleported');
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
    if (!root) return;
    K7._restoreMenuElement(dropdown, root);
    K7._restoreMenuElement(backdrop, root);
    if (dropdown) {
        dropdown.classList.remove('k7-menu-dropdown--video-player', 'k7-menu-dropdown--teleported');
    }
    if (backdrop) backdrop.classList.remove('k7-backdrop--video-player');
};

K7.attachSelectPortal = function (root, dropdown, backdrop) {
    if (!root || !dropdown) return;
    K7._teleportMenuElement(dropdown, root);
    if (backdrop) {
        K7._teleportMenuElement(backdrop, root);
        backdrop.classList.add('k7-backdrop--teleported');
    }
    dropdown.classList.add('k7-select-dropdown--teleported');
};

K7.detachSelectPortal = function (root, dropdown, backdrop) {
    if (!root) return;
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

K7.scrollToElement = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
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