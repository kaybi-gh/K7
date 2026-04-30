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
            if (_layers[i].el === el) return;
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

    function popLayer(el) {
        if (!el) return;
        for (var i = _layers.length - 1; i >= 0; i--) {
            if (_layers[i].el === el) {
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
        if (tag === 'button' || tag === 'a' || role === 'button' || role === 'switch') {
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
            if (layer.type === 'overlay') return;
            e.preventDefault();
            e.stopImmediatePropagation();
            var onClose = layer.onClose;
            popLayer(layer.el);
            if (onClose) invokeCallback(onClose, 'OnLayerClosed');
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

            // Auto-refresh after any DOM mutation (covers all Blazor re-renders)
            var observer = new MutationObserver(scheduleRefresh);
            observer.observe(document.body, {
                childList: true,
                subtree: true,
                attributes: true,
                attributeFilter: ['disabled', 'tabindex', 'hidden']
            });
        }

        document.addEventListener('keydown', handleKeyDown, true);
        document.addEventListener('enhancedload', onPageNavigated);
    }

    // Public API

    return {
        init: init,
        pushLayer: pushLayer,
        popLayer: popLayer,
        focusFirst: focusFirst,
        focusElement: focusElement,
        refresh: refresh,
        addSection: addSection,
        removeSection: removeSection,
        registerHomeEscape: registerHomeEscape,
        isFocusInside: isFocusInside,
        isElementEditing: isElementEditing
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