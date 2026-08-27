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
    var _selectionModeCallback = null;
    var _videoPlayerBackCallback = null;
    var _videoPlayerRemoteRef = null;
    var _refreshTimer = null;
    var _sectionLastFocused = {};
    var _currentSectionId = null;
    var _pageFocusSettled = false;
    var _userChoseAppNav = false;
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
                var initial = container.querySelector('[data-initial-focus]');
                if (initial) {
                    var selector = initial.getAttribute('data-initial-focus');
                    target = isInitialFocusSelector(selector)
                        ? (container.querySelector(selector) || initial)
                        : initial;
                }
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
            syncAllDetailsContentInert();
            if (window.SpatialNavigation) SpatialNavigation.makeFocusable();
            lockActivatableInputs();
            ensurePageFocus();
            focusActivatableInOpenMenus();
        }, 32);
    }

    function refresh() {
        syncAllDetailsContentInert();
        if (window.SpatialNavigation) SpatialNavigation.makeFocusable();
        lockActivatableInputs();
        focusActivatableInOpenMenus();
    }

    // When a menu opens (or its submenu content swaps), focus the first activatable
    // text field once so TV OK hits toggleActivatableEdit. Do not enter edit mode.
    function focusActivatableInOpenMenus() {
        var closedMarked = document.querySelectorAll('.k7-menu-dropdown:not(.k7-menu-dropdown--open) [data-sn-menu-focused]');
        for (var c = 0; c < closedMarked.length; c++) {
            closedMarked[c].removeAttribute('data-sn-menu-focused');
        }

        var menus = document.querySelectorAll('.k7-menu-dropdown--open');
        for (var m = 0; m < menus.length; m++) {
            var menu = menus[m];
            var active = document.activeElement;
            if (active && menu.contains(active) && isTextInput(active) && isActivatable(active)) {
                continue;
            }
            var candidates = menu.querySelectorAll('input[data-sn-activatable], textarea[data-sn-activatable]');
            for (var i = 0; i < candidates.length; i++) {
                var input = candidates[i];
                if (!isTextInput(input) || !isElementVisible(input)) continue;
                if (input.getAttribute('data-sn-menu-focused') === '1') continue;
                input.setAttribute('data-sn-menu-focused', '1');
                input.focus({ preventScroll: true });
                break;
            }
        }
    }

    // Focusable Discovery

    function isInsideInactiveFeedHub(el) {
        if (!el || !el.closest) return false;
        if (el.closest('[inert]')) return true;
        var page = el.closest('.feed-hub-page');
        return !!(page && !page.classList.contains('feed-hub-page--active'));
    }

    function isVisibleInCarouselViewport(el) {
        var viewport = el.closest('[data-carousel-viewport]');
        if (!viewport) return true;
        var vpRect = viewport.getBoundingClientRect();
        var elRect = el.getBoundingClientRect();
        var cx = elRect.left + elRect.width / 2;
        return cx >= vpRect.left - 5 && cx <= vpRect.right + 5;
    }

    function isElementVisible(el) {
        if (isInsideInactiveFeedHub(el)) return false;
        // Loop-back slide stays in the DOM with height:0 until .visible; never treat as focusable.
        if (el.closest && el.closest('[data-carousel-loop-back]:not(.visible)')) return false;
        var style = window.getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden') return false;
        if (el.offsetParent !== null) return true;
        var rect = el.getBoundingClientRect();
        return rect.width !== 0 || rect.height !== 0;
    }

    function getFocusables(container) {
        return Array.from(container.querySelectorAll(FOCUSABLE)).filter(function (el) {
            if (isInClosedDetailsContent(el)) return false;
            if (isInsideInactiveFeedHub(el)) return false;
            if (el.closest('[data-carousel-item]')) {
                return isElementVisible(el) && isVisibleInCarouselViewport(el);
            }
            return isElementVisible(el);
        });
    }

    // Hidden content inside a closed <details> must not receive spatial-nav / tab focus.
    // Applies to sidebar nav groups and K7ExpansionPanel (and any other details).
    function isInClosedDetailsContent(el) {
        if (!el || !el.closest) return false;
        var details = el.closest('details:not([open])');
        if (!details) return false;
        var summary = details.querySelector(':scope > summary');
        if (summary && (el === summary || summary.contains(el))) return false;
        return true;
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
        if (!details.open && details.contains(last)) {
            delete _sectionLastFocused[sectionId];
        }
    }

    function relocateFocusFromCollapsedDetails(details) {
        if (!details || details.open) return;
        var active = document.activeElement;
        if (!active || !details.contains(active)) return;
        var summary = details.querySelector(':scope > summary');
        if (summary && (active === summary || summary.contains(active))) return;
        if (summary && isElementVisible(summary)) {
            summary.focus({ preventScroll: true });
        }
    }

    function syncDetailsContentInert(details) {
        if (!details || !details.children) return;
        var kids = details.children;
        for (var i = 0; i < kids.length; i++) {
            var child = kids[i];
            if (child.tagName === 'SUMMARY') continue;
            if (details.open) child.removeAttribute('inert');
            else child.setAttribute('inert', '');
        }
    }

    function syncAllDetailsContentInert() {
        var list = document.querySelectorAll('details.k7-expansion, details.nav-group');
        for (var i = 0; i < list.length; i++) syncDetailsContentInert(list[i]);
    }

    function ensureSidebarFocusVisible() {
        var active = document.activeElement;
        if (!active || !isInClosedDetailsContent(active)) return false;
        var details = active.closest('details');
        if (!details) return false;
        relocateFocusFromCollapsedDetails(details);
        return true;
    }

    function handleNavGroupToggle(e) {
        var details = e.target;
        if (!details || !details.matches || !details.matches('details')) return;
        syncDetailsContentInert(details);
        if (details.matches('details.nav-group')) {
            clearStaleSectionLastFocused(details);
        }
        if (!details.open) {
            relocateFocusFromCollapsedDetails(details);
        }
        scheduleRefresh();
    }

    // Scroll

    var _pendingCarouselScroll = new WeakMap();

    function scrollCarouselToElement(el) {
        if (!el || !el.closest) return;
        var carouselRoot = el.closest('[data-carousel]');
        if (!carouselRoot) return;
        var item = el.closest('[data-carousel-item]');
        if (!item) return;

        if (!carouselRoot.__embla) {
            scheduleScrollCarouselWhenReady(el);
            return;
        }

        var container = carouselRoot.querySelector('.carousel-container');
        var allItems = container ? Array.from(container.querySelectorAll('[data-carousel-item]')) : [];
        var idx = allItems.indexOf(item);
        if (idx < 0) return;

        // One scrollNext/Prev is not enough when focus lands far off-screen (e.g. loop-back).
        // Jump so initial TV focus (Keep Watching -> mid-season episode) is visible immediately.
        try {
            carouselRoot.__embla.scrollTo(idx, true);
        } catch (e) {
            if (idx === 0) carouselRoot.__embla.scrollTo(0, true);
        }
    }

    function scheduleScrollCarouselWhenReady(el) {
        if (_pendingCarouselScroll.has(el)) return;
        var attempts = 0;
        _pendingCarouselScroll.set(el, true);

        function retry() {
            _pendingCarouselScroll.delete(el);
            if (!el.isConnected) return;

            var item = el.closest('[data-carousel-item]');
            var active = document.activeElement;
            var stillFocused = !active
                || active === el
                || el.contains(active)
                || (item && item.contains(active));
            if (!stillFocused) return;

            var carouselRoot = el.closest('[data-carousel]');
            if (carouselRoot && carouselRoot.__embla) {
                scrollCarouselToElement(el);
                return;
            }
            if (++attempts < 40) {
                _pendingCarouselScroll.set(el, true);
                requestAnimationFrame(retry);
            }
        }

        requestAnimationFrame(retry);
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

    // Dialog / panel overflow: when the last (or first) focusable inside a
    // scrollable region is focused but more content remains, arrow keys should
    // scroll before Spatial Navigation leaves to a sibling outside (e.g. Close).
    function findOverflowScrollParent(el) {
        var node = el;
        while (node && node !== document.body && node.nodeType === 1) {
            var style = window.getComputedStyle(node);
            var oy = style.overflowY;
            if ((oy === 'auto' || oy === 'scroll' || oy === 'overlay')
                && node.scrollHeight > node.clientHeight + 1) {
                return node;
            }
            node = node.parentElement;
        }
        return null;
    }

    function shouldIgnoreOverflowScroll(container) {
        if (!container || !container.classList) return true;
        if (container.classList.contains('page-scrollable')) return true;
        if (container.classList.contains('app-main')) return true;
        if (container.hasAttribute('data-tv-scroll')) return true;
        if (container.closest && container.closest('[data-carousel]')) return true;
        if (container.closest && container.closest('.k7-menu-dropdown')) return true;
        return false;
    }

    function isEdgeFocusableInOverflow(el, container, direction) {
        var items = getFocusables(container);
        if (items.length === 0) return el === container;
        var idx = items.indexOf(el);
        if (idx < 0) return false;
        if (direction === 'down') return idx === items.length - 1;
        return idx === 0;
    }

    function tryScrollOverflowOnArrow(el, arrowKey) {
        if (!el || !el.closest) return false;
        if (arrowKey !== 'ArrowDown' && arrowKey !== 'ArrowUp') return false;

        var container = null;
        var selfStyle = window.getComputedStyle(el);
        var selfOy = selfStyle.overflowY;
        if ((selfOy === 'auto' || selfOy === 'scroll' || selfOy === 'overlay')
            && el.scrollHeight > el.clientHeight + 1) {
            container = el;
        } else {
            container = findOverflowScrollParent(el);
        }
        if (!container || shouldIgnoreOverflowScroll(container)) return false;

        var maxScroll = container.scrollHeight - container.clientHeight;
        if (maxScroll <= 1) return false;

        var step = Math.max(56, Math.round(container.clientHeight * 0.45));
        var atTop = container.scrollTop <= 1;
        var atBottom = container.scrollTop >= maxScroll - 1;

        if (arrowKey === 'ArrowDown') {
            if (atBottom) return false;
            if (container !== el && !isEdgeFocusableInOverflow(el, container, 'down')) return false;
            container.scrollBy({ top: step, behavior: 'smooth' });
            return true;
        }

        if (atTop) return false;
        if (container !== el && !isEdgeFocusableInOverflow(el, container, 'up')) return false;
        container.scrollBy({ top: -step, behavior: 'smooth' });
        return true;
    }

    function scrollCardIntoTvView(root, el) {
        if (!root || !el) return;
        var card = el.closest('.media-card') || el.closest('[data-carousel-item]');
        if (!card) return;

        // On vertical page feeds, keep the whole carousel row (title + cards) in view
        // so scrolling up does not clip the section header.
        var measureEl = card;
        if (root.classList && root.classList.contains('page-scrollable')) {
            measureEl = el.closest('.carousel-wrapper') || card;
        }

        var margin = 24;
        var rootRect = root.getBoundingClientRect();
        var cardRect = measureEl.getBoundingClientRect();

        var overflowBottom = cardRect.bottom - rootRect.bottom;
        var overflowTop = rootRect.top - cardRect.top;

        // While focused in the below zone, never scroll back into the hero.
        var minScroll = 0;
        if (el.closest('[data-tv-scroll-zone="below"]')) {
            var mainZone = root.querySelector('[data-tv-scroll-zone="main"]');
            if (mainZone) minScroll = mainZone.offsetHeight;
        }

        if (overflowBottom > -margin) {
            root.scrollBy({ top: overflowBottom + margin, behavior: 'smooth' });
        } else if (overflowTop > -margin) {
            var delta = -(overflowTop + margin);
            var nextTop = root.scrollTop + delta;
            if (nextTop < minScroll) delta = minScroll - root.scrollTop;
            if (delta !== 0) {
                root.scrollBy({ top: delta, behavior: 'smooth' });
            }
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

        var allItems = Array.from(carousel.querySelectorAll('[data-carousel-item]')).filter(function (item) {
            if (item.hasAttribute('data-carousel-loop-back') && !item.classList.contains('visible'))
                return false;
            return true;
        });
        var currentIdx = allItems.indexOf(currentItem);
        if (currentIdx === -1) return false;

        var targetIdx = direction === 'ArrowRight' ? currentIdx + 1 : currentIdx - 1;
        // At horizontal edges, stay on the current tile (do not let spatial nav escape
        // to hero controls like the back button).
        if (targetIdx < 0 || targetIdx >= allItems.length) return true;

        var targetItem = allItems[targetIdx];
        var target = targetItem.matches(FOCUSABLE) ? targetItem : targetItem.querySelector(FOCUSABLE);
        if (!target) return false;

        if (carousel.__embla) {
            try {
                carousel.__embla.scrollTo(targetIdx);
            } catch (e) {
                if (targetIdx === 0) carousel.__embla.scrollTo(0);
                else if (targetIdx > currentIdx) carousel.__embla.scrollNext();
                else carousel.__embla.scrollPrev();
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
    function requestSoftKeyboard(el) {
        if (!window.K7 || !window.K7.showSoftKeyboard) return;
        if (el && !isEditing(el)) return;
        window.K7.showSoftKeyboard();
    }

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
                    requestSoftKeyboard(el);
                    // Recover if IME/WebView focus handling briefly blurs the input.
                    setTimeout(function () {
                        if (isEditing(el) && document.activeElement !== el) {
                            el.focus({ preventScroll: true });
                        }
                    }, 50);
                    // Retry once: JS->.NET interop is async and TV IME often misses the first show.
                    setTimeout(function () {
                        requestSoftKeyboard(el);
                    }, 100);
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

    function isOpenSearchSelectRoot(el) {
        return !!(el && el.closest && el.closest('.k7-search-select--open'));
    }

    function resumeSpatialNavUnlessSearchSelectOpen(el) {
        if (isOpenSearchSelectRoot(el)) {
            if (window.SpatialNavigation) SpatialNavigation.pause();
            return;
        }
        if (window.SpatialNavigation) SpatialNavigation.resume();
    }

    // After IME dismiss: OK on the input should pick the highlighted hint, not re-enter edit.
    function tryActivateOpenSearchSelectHint(el) {
        if (!el || !isTextInput(el) || !isOpenSearchSelectRoot(el) || isEditing(el)) return false;
        var root = el.closest('.k7-search-select--open');
        var activeOpt = root && root.querySelector('.k7-search-select-option--active');
        if (!activeOpt) return false;
        activeOpt.click();
        return true;
    }

    // Shared OK/Enter activation for data-sn-activatable controls (text fields, seekbar, sliders).
    // Used by handleEnter and handleTvRemoteSelect so both paths stay in sync on TV.
    function toggleActivatableEdit(el) {
        if (!el || !isActivatable(el)) return false;
        if (tryActivateOpenSearchSelectHint(el)) return true;
        if (isEditing(el)) {
            stopEditing(el);
            el.dispatchEvent(new CustomEvent('sn:editcommit', { bubbles: false }));
            resumeSpatialNavUnlessSearchSelectOpen(el);
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

    // Long-press helpers for spatial navigation on media cards.
    // Android KEYCODE_ENTER is 66; desktop KeyB is also 66. Prefer key/code when present
    // so typing "b" in activatable inputs is not treated as OK/Enter (which exits edit mode).
    function isEnterKey(key, code, keyCode) {
        key = key || '';
        code = code || '';
        if (key === 'b' || key === 'B' || code === 'KeyB') return false;
        if (keyCode === 13 || keyCode === 23 || keyCode === 66) return true;
        if (key === 'Enter' || key === 'NumpadEnter' || key === 'Select' || key === 'DpadCenter') return true;
        if (code === 'Enter' || code === 'NumpadEnter' || code === 'Select' || code === 'DpadCenter') return true;
        return false;
    }

    function getVideoControlsOverlay(el) {
        if (el && el.closest) {
            var fromEl = el.closest('.video-controls-overlay');
            if (fromEl) return fromEl;
        }
        return document.querySelector('.video-controls-overlay');
    }

    // When overlay chrome is forced visible by scrub JS but Blazor already hid it,
    // treat as hidden so OK/arrows reopen scrub instead of no-oping.
    function isVideoControlsHidden(overlay) {
        if (!overlay) return false;
        // Stuck seekbar-scrubbing after a failed commit still means chrome is "busy",
        // but if controls-hidden is also set, prefer the hidden path so OK can reopen.
        if (overlay.classList.contains('seekbar-scrubbing')
            && !overlay.classList.contains('controls-hidden'))
            return false;
        return overlay.classList.contains('controls-hidden');
    }

    function getVideoSeekBarScrubbing() {
        var overlay = document.querySelector('.video-controls-overlay');
        if (!overlay) return null;
        return overlay.querySelector('.seekbar-container[data-sn-editing], .seekbar-container.scrubbing');
    }

    function setVideoOverlayScrubbingClass(active) {
        var overlay = document.querySelector('.video-controls-overlay');
        if (!overlay) return;
        if (active) {
            overlay.classList.add('seekbar-scrubbing');
            overlay.classList.remove('controls-hidden');
            overlay.classList.add('controls-visible');
        } else {
            overlay.classList.remove('seekbar-scrubbing');
        }
    }

    // Commit/cancel even when focus drifted off the seekbar (common on Android TV WebView).
    function commitVideoSeekBarScrubIfAny() {
        var seekbar = getVideoSeekBarScrubbing();
        if (!seekbar) return false;
        try { seekbar.focus({ preventScroll: true }); } catch (ex) { }
        var scrubTime = (window.K7 && K7.SeekBar) ? K7.SeekBar.getScrubTime(seekbar) : 0;
        stopEditing(seekbar);
        if (window.K7 && K7.SeekBar) K7.SeekBar.clearLocalScrub(seekbar);
        setVideoOverlayScrubbingClass(false);
        if (window.K7 && K7.tvDpadHoldStop) K7.tvDpadHoldStop(false);

        // Call the JavascriptInterface directly. K7.tvNativeSeek may be missing if the
        // bridge inject raced; never claim success without a real seek.
        var nativeOk = false;
        try {
            if (window.K7TvVideo && typeof K7TvVideo.seek === 'function') {
                K7TvVideo.seek(scrubTime);
                nativeOk = true;
            } else if (window.K7 && typeof K7.tvNativeSeek === 'function') {
                K7.tvNativeSeek(scrubTime);
                nativeOk = true;
            }
        } catch (exSeek) {
        }

        if (nativeOk) {
            var inst = window.K7 && K7.SeekBar && K7.SeekBar._instances.get(seekbar);
            try {
                if (inst && inst.dotNetRef) {
                    if (inst.dotNetRef.invokeMethod) inst.dotNetRef.invokeMethod('OnEditCancelSoft');
                    else if (inst.dotNetRef.invokeMethodAsync) inst.dotNetRef.invokeMethodAsync('OnEditCancelSoft');
                }
            } catch (exSoft) { }
            if (window.K7 && K7.hideVideoControlsOverlay) K7.hideVideoControlsOverlay();
            invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteOverlayHidden');
            return true;
        }

        // Fallback: SeekBar sn:editcommit -> DotNet OnEditCommitAt -> afterScrubCommit.
        seekbar.dispatchEvent(new CustomEvent('sn:editcommit', { bubbles: false }));
        return true;
    }

    // Returns "" | "soft" | "hard". soft = exit edit, keep overlay. hard = cancel scrub (hide chrome).
    function cancelVideoSeekBarScrubIfAny() {
        var overlay = document.querySelector('.video-controls-overlay');
        var seekbar = getVideoSeekBarScrubbing();
        if (!seekbar && overlay)
            seekbar = overlay.querySelector('.seekbar-container[data-sn-editing]');
        if (!seekbar) return '';

        // stepLocal sets _scrub; OK-only edit must not initLocalScrub (see SeekBar.init).
        var hadLocalScrub = !!(window.K7 && K7.SeekBar && K7.SeekBar._scrub && K7.SeekBar._scrub.el === seekbar);

        stopEditing(seekbar);
        if (window.K7 && K7.SeekBar) K7.SeekBar.clearLocalScrub(seekbar);
        setVideoOverlayScrubbingClass(false);
        if (window.K7 && K7.tvDpadHoldStop) K7.tvDpadHoldStop();

        if (hadLocalScrub) {
            // Hide in pure JS - do not wait for DotNet OnEditCancel / afterScrubCommit.
            if (window.K7 && K7.hideVideoControlsOverlay) K7.hideVideoControlsOverlay();
            var instHard = window.K7 && K7.SeekBar && K7.SeekBar._instances.get(seekbar);
            try {
                if (instHard && instHard.dotNetRef) {
                    if (instHard.dotNetRef.invokeMethod) instHard.dotNetRef.invokeMethod('OnEditCancelSoft');
                    else if (instHard.dotNetRef.invokeMethodAsync) instHard.dotNetRef.invokeMethodAsync('OnEditCancelSoft');
                }
            } catch (exH) { }
            invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteOverlayHidden');
        } else {
            var inst = window.K7 && K7.SeekBar && K7.SeekBar._instances.get(seekbar);
            try {
                if (inst && inst.dotNetRef) {
                    if (inst.dotNetRef.invokeMethod) inst.dotNetRef.invokeMethod('OnEditCancelSoft');
                    else if (inst.dotNetRef.invokeMethodAsync) inst.dotNetRef.invokeMethodAsync('OnEditCancelSoft');
                }
            } catch (ex) { }
            try { seekbar.focus({ preventScroll: true }); } catch (ex2) { }
            if (window.SpatialNavigation) SpatialNavigation.resume();
            invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteSeekEditCancelled');
        }

        return hadLocalScrub ? 'hard' : 'soft';
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
        if (card.classList.contains('media-card--menu-open')
            || card.classList.contains('k7-category-card--menu-open')) return true;
        if (card.querySelector('.k7-menu-dropdown--open')) return true;
        return false;
    }

    // Snapshot on Select down so key-up still activates the control the user focused,
    // even if Android window focus churn moved document.activeElement mid-press.
    var _tvSelectSnapshotEl = null;

    function resolveSelectActivationTarget(el) {
        if (!el || el === document.body || el === document.documentElement)
            return null;
        if (el.closest) {
            var hit = el.closest('button, a.focusable, a.media-card-link, .focusable, [data-sn-activatable]');
            if (hit) return hit;
        }
        return el;
    }

    function snapshotTvSelectTarget() {
        var active = document.activeElement;
        _tvSelectSnapshotEl = resolveSelectActivationTarget(active) || active;
    }

    function takeTvSelectSnapshot() {
        var el = _tvSelectSnapshotEl;
        _tvSelectSnapshotEl = null;
        if (el && el.isConnected)
            return el;
        return resolveSelectActivationTarget(document.activeElement) || document.activeElement;
    }


    function handleTvRemoteSelect(phase, keyCode, heldMs) {
        window.__k7TvNativeRemote = true;

        if (phase === 'down')
            snapshotTvSelectTarget();

        var active = (phase === 'up' || phase === 'long-up')
            ? (_tvSelectSnapshotEl && _tvSelectSnapshotEl.isConnected
                ? _tvSelectSnapshotEl
                : document.activeElement)
            : document.activeElement;


        var openMenuEl = active && active.closest ? active.closest('.k7-menu-dropdown--open') : null;

        if (openMenuEl) {
            if (phase === 'up' && heldMs < 600) {
                var menuTarget = takeTvSelectSnapshot();
                if (menuTarget && menuTarget !== document.body) {
                    if (toggleActivatableEdit(menuTarget)) {
                        return;
                    }
                    if (menuTarget.classList.contains('k7-menu-close')
                        || menuTarget.classList.contains('k7-menu-item')
                        || menuTarget.tagName === 'BUTTON') {
                        menuTarget.click();
                    }
                }
            }
            if (phase === 'long-up' || phase === 'up') {
                cancelMediaCardLongPress();
                _mediaCardPressStart = null;
                _tvSelectSnapshotEl = null;
            }
            return;
        }

        var videoOverlay = getVideoControlsOverlay(active);

        if (videoOverlay && isVideoControlsHidden(videoOverlay)) {
            if (phase === 'up' && heldMs < 600) {
                _tvSelectSnapshotEl = null;
                handleHiddenVideoPlayerSelect(makeFakeKeyEvent(keyCode, active));
            }
            return;
        }

        if (phase === 'up' && heldMs < 600 && commitVideoSeekBarScrubIfAny()) {
            _tvSelectSnapshotEl = null;
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
            _tvSelectSnapshotEl = null;
            return;
        }

        if (phase === 'long-up') {
            cancelMediaCardLongPress();
            _mediaCardPressStart = null;
            _tvSelectSnapshotEl = null;
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
                _tvSelectSnapshotEl = null;
                swallowNextEnterClick();
                return;
            }

            if (card && link) {
                _tvSelectSnapshotEl = null;
                navigateMediaCardLink(link);
                return;
            }

            var target = takeTvSelectSnapshot();
            // Stale Select-up after process sleep / unpaired down (adb heldMs=404696018).
            if (heldMs > 10000) {
                return;
            }
            if (target && target !== document.body && heldMs < 600) {
                // MAUI Android TV consumes Select keys before keydown reaches handleEnter.
                // Activatable controls (search fields, seekbar, sliders) must enter edit mode here.
                if (toggleActivatableEdit(target)) {
                    return;
                }
                var tag = (target.tagName || '').toLowerCase();
                if (tag === 'button' || tag === 'a' || target.classList.contains('focusable')) {
                    target.click();
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
        var card = container.closest('.media-card') || container.closest('.k7-category-card');
        if (!card) return null;
        return {
            container: container,
            card: card,
            activeEl: activeEl,
            link: card.querySelector('a.media-card-link[href]')
                || card.querySelector('button.k7-category-card__hit')
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
        var activator = card.querySelector('[data-longpress-target] .media-card-menu-trigger')
            || card.querySelector('[data-longpress-target] .k7-menu-activator-inner');
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

    function invokeCallbackSync(callback, methodName, arg) {
        if (!callback) return false;
        try {
            if (callback.invokeMethod) {
                if (typeof arg === 'undefined')
                    callback.invokeMethod(methodName || 'Invoke');
                else
                    callback.invokeMethod(methodName || 'Invoke', arg);
                return true;
            }
        } catch (ex) { }
        // Fallback when sync interop is unavailable (some hosts only expose async).
        if (typeof arg === 'undefined')
            invokeCallback(callback, methodName);
        else if (callback.invokeMethodAsync) {
            try { callback.invokeMethodAsync(methodName || 'Invoke', arg); } catch (ex2) { }
        }
        return false;
    }

    function nativeTvHoldOwnsHiddenArrows() {
        // Android TV Activity already called tvDpadHoldStart via EvaluateJavascript.
        // Swallow the duplicate keydown so we do not reset the 400ms long-press timer.
        return !!(window.K7TvVideo || window.__k7TvNativeRemote);
    }

    function handleHiddenVideoPlayerArrow(key, code, e) {
        if (!_videoPlayerRemoteRef) return false;

        e.preventDefault();
        e.stopImmediatePropagation();

        var keyCode = e.keyCode || 0;
        // L/R while chrome is hidden: short-skip vs long-scrub via tvDpadHold*.
        // Web keyboard never gets the native EvaluateJavascript call - start the hold here.
        if (key === 'ArrowLeft' || code === 'ArrowLeft' || keyCode === 37 || keyCode === 21) {
            if (nativeTvHoldOwnsHiddenArrows())
                return true;
            if (window.K7 && K7.tvDpadHoldStart)
                K7.tvDpadHoldStart('ArrowLeft', keyCode);
            else
                invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteSkipDirection', -1);
            return true;
        }
        if (key === 'ArrowRight' || code === 'ArrowRight' || keyCode === 39 || keyCode === 22) {
            if (nativeTvHoldOwnsHiddenArrows())
                return true;
            if (window.K7 && K7.tvDpadHoldStart)
                K7.tvDpadHoldStart('ArrowRight', keyCode);
            else
                invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteSkipDirection', 1);
            return true;
        }
        if (window.SpatialNavigation) SpatialNavigation.pause();
        if (key === 'ArrowUp' || code === 'ArrowUp' || keyCode === 38 || keyCode === 19) {
            invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteVolumeUp');
        } else if (key === 'ArrowDown' || code === 'ArrowDown' || keyCode === 40 || keyCode === 20) {
            invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteVolumeDown');
        } else {
            return false;
        }
        return true;
    }

    // Soft-notify Blazor that chrome should be considered visible - async only, never per-step.
    var _videoOverlayShownNotifyAt = 0;
    function notifyVideoRemoteOverlayShown() {
        var now = Date.now();
        if (now - _videoOverlayShownNotifyAt < 500) return;
        _videoOverlayShownNotifyAt = now;
        invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteOverlayShown');
    }

    function handleHiddenVideoPlayerSelect(e) {
        e.preventDefault();
        e.stopImmediatePropagation();
        swallowNextEnterClick();
        if (window.K7 && K7.tvDpadHoldStop) K7.tvDpadHoldStop(false);

        // Show chrome in the DOM immediately even if the Blazor circuit is wedged.
        var overlay = document.querySelector('.video-controls-overlay');
        if (overlay) {
            overlay.classList.remove('controls-hidden', 'seekbar-scrubbing');
            overlay.classList.add('controls-visible');
            var seekbar = overlay.querySelector('.seekbar-container');
            if (seekbar) {
                // Never leave the seekbar in edit/scrub mode when merely opening chrome -
                // that traps DPAD on the bar.
                seekbar.removeAttribute('data-sn-editing');
                if (window.K7 && K7.SeekBar) K7.SeekBar.clearLocalScrub(seekbar);
            }
            if (window.SpatialNavigation) {
                try { SpatialNavigation.resume(); } catch (exSn) { }
                try { SpatialNavigation.makeFocusable(); } catch (exMf) { }
            }
            try {
                var playBtn = overlay.querySelector('.play-pause-btn');
                if (playBtn) playBtn.focus({ preventScroll: true });
                else if (seekbar) seekbar.focus({ preventScroll: true });
                else overlay.focus({ preventScroll: true });
            } catch (ex) { }
        }
        if (window.SpatialNavigation) SpatialNavigation.resume();
        // Sync Blazor _showOverlay via sync invokeMethod when possible (async stalls leave
        // progress re-renders re-applying controls-hidden).
        if (_videoPlayerRemoteRef) {
            try {
                if (_videoPlayerRemoteRef.invokeMethod)
                    _videoPlayerRemoteRef.invokeMethod('OnRemoteSelect');
                else
                    invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteSelect');
            } catch (exSelect) {
                invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteSelect');
            }
        }
    }

    function invokeCallbackAsync(callback, methodName, arg) {
        if (!callback) return;
        try {
            if (typeof arg === 'undefined') {
                if (callback.invokeMethodAsync) callback.invokeMethodAsync(methodName || 'Invoke');
            } else if (callback.invokeMethodAsync) {
                callback.invokeMethodAsync(methodName || 'Invoke', arg);
            }
        } catch (ex) { }
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

        // Seekbar scrub commit even when focus left the seekbar (Android TV blur).
        if (commitVideoSeekBarScrubIfAny()) {
            e.preventDefault();
            e.stopImmediatePropagation();
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
            // Web keyboard starts the hold from handleHiddenVideoPlayerArrow; always stop it
            // on keyup even if long-press already revealed chrome (interval would keep scrubbing).
            if (window.K7 && K7.tvDpadHoldStop && window.K7._tvDpadHold)
                K7.tvDpadHoldStop(true);
            var overlay = getVideoControlsOverlay(document.activeElement);
            if (overlay && isVideoControlsHidden(overlay)) {
                // Phone/Tablet keyboard accumulate-seek commits on keyup. TV/Desktop already
                // opened seekbar edit on keydown (overlay no longer hidden).
                e.preventDefault();
                if (_videoPlayerRemoteRef) invokeCallbackSync(_videoPlayerRemoteRef, 'OnRemoteSeekCommit');
                if (window.SpatialNavigation) SpatialNavigation.resume();
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
        if (window.K7 && K7._menuPositionAnchor && K7._menuPositionAnchor.closest) {
            var fromAnchor = K7._menuPositionAnchor.closest('.media-card');
            if (fromAnchor) return fromAnchor;
        }
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

        // Seekbar scrub may keep data-sn-editing while focus has drifted to the overlay root.
        if (cancelVideoSeekBarScrubIfAny()) {
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        if (active && isEditing(active)) {
            if (isTextInput(active) && isTvLongPressMode()) {
                _tvEditDismissViaBack = true;
            }
            stopEditing(active);
            active.dispatchEvent(new CustomEvent('sn:editcancel', { bubbles: false }));
            // Keep focus on the input so open search-select hints stay keyboard-navigable.
            if (isTextInput(active) && isOpenSearchSelectRoot(active)) {
                active.focus({ preventScroll: true });
            }
            resumeSpatialNavUnlessSearchSelectOpen(active);
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        if (isOpenSearchSelectInput(active)) return;

        if (tryClosePlaybackSettingsLevel()) {
            e.preventDefault();
            e.stopImmediatePropagation();
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

        if (_selectionModeCallback) {
            e.preventDefault();
            e.stopImmediatePropagation();
            invokeCallback(_selectionModeCallback, 'OnSelectionEscape');
            return;
        }

        e.preventDefault();
        e.stopImmediatePropagation();
        handleBackNav();
    }

    function handleBackKey(e) {
        var key = e.key;
        if (key === 'Backspace') {
            var active = document.activeElement;
            if (active && isTextInput(active) && (isEditing(active) || !isActivatable(active)))
                return;

            var layer = peekLayer();
            // On dialogs, only Escape (and platform back keys) close - keyboard Backspace
            // is reserved for editing (PIN pad, etc.).
            if (layer && layer.type === 'dialog')
                return;
        } else if (key !== 'GoBack' && key !== 'XF86Back') {
            return;
        }
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
                // Seekbar/slider: Android TV WebView often blurs mid-edit; keep editing and refocus.
                // Explicit OK (commit) or Back (cancel) still exit via toggleActivatableEdit / Escape.
                var role = editingEl.getAttribute('role') || '';
                if (editingEl.classList.contains('seekbar-container') || role === 'slider') {
                    setTimeout(function () {
                        if (!editingEl.isConnected || !editingEl.hasAttribute('data-sn-editing')) return;
                        if (document.activeElement !== editingEl)
                            editingEl.focus({ preventScroll: true });
                    }, 0);
                    return;
                }
                // Other non-text activatables and desktop blur-to-commit keep the old behavior.
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
                        requestSoftKeyboard(editingEl);
                        return;
                    }
                    stopEditing(editingEl);
                    editingEl.dispatchEvent(new CustomEvent('sn:editcancel', { bubbles: false }));
                    if (isOpenSearchSelectRoot(editingEl)) {
                        editingEl.focus({ preventScroll: true });
                    }
                    resumeSpatialNavUnlessSearchSelectOpen(editingEl);
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
                if (key === 'ArrowDown' || key === 'ArrowUp') {
                    if (window.SpatialNavigation) SpatialNavigation.pause();
                    return;
                }
                if (key === 'Escape') {
                    var escapeInput = searchRoot.querySelector('input, textarea');
                    // While editing, fall through to handleEscape so Back dismisses IME first
                    // and keeps hints. After edit end, Blazor closes the dropdown.
                    if (escapeInput && !isEditing(escapeInput)) {
                        if (window.SpatialNavigation) SpatialNavigation.pause();
                        return;
                    }
                }
                if (isEnterKey(key, e.code, e.keyCode)) {
                    // Let Blazor select a highlighted hint whether still editing or after IME dismiss.
                    if (window.SpatialNavigation) SpatialNavigation.pause();
                    return;
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
            // When overlay is hidden, route arrows to the player (seekbar edit / volume / phone HUD).
            var videoOverlay = getVideoControlsOverlay(el);
            if (videoOverlay && isVideoControlsHidden(videoOverlay)) {
                if (handleHiddenVideoPlayerArrow(key, e.code || '', e)) return;
            }
            // Normalize TV remote keyCodes (Android/WebView often omit e.key).
            var arrowKey = key;
            if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(arrowKey) === -1) {
                if (e.code === 'ArrowLeft' || e.keyCode === 37 || e.keyCode === 21) arrowKey = 'ArrowLeft';
                else if (e.code === 'ArrowRight' || e.keyCode === 39 || e.keyCode === 22) arrowKey = 'ArrowRight';
                else if (e.code === 'ArrowUp' || e.keyCode === 38 || e.keyCode === 19) arrowKey = 'ArrowUp';
                else if (e.code === 'ArrowDown' || e.keyCode === 40 || e.keyCode === 20) arrowKey = 'ArrowDown';
            }
            // Overlay just opened for scrub but focus may still be on the overlay root:
            // keep routing L/R into seekbar edit instead of SpatialNav.
            if (videoOverlay && !isVideoControlsHidden(videoOverlay)
                && (arrowKey === 'ArrowLeft' || arrowKey === 'ArrowRight')
                && (el === videoOverlay || (el && el.classList && el.classList.contains('video-controls-overlay')))) {
                e.preventDefault();
                e.stopImmediatePropagation();
                if (window.K7 && window.K7.beginSeekBarScrub)
                    window.K7.beginSeekBarScrub(arrowKey === 'ArrowLeft' ? -1 : 1);
                return;
            }
            if (el && el.closest('[data-carousel]') && handleCarouselNav(el, arrowKey)) {
                e.preventDefault();
                e.stopPropagation();
                return;
            }
            // Virtual browse grids: keep Up/Down inside the grid (placeholders / next row).
            // Right still reaches the jump-index via SpatialNavigation.
            if ((arrowKey === 'ArrowDown' || arrowKey === 'ArrowUp')
                && window.K7 && typeof window.K7.handleVirtualBrowseArrow === 'function'
                && window.K7.handleVirtualBrowseArrow(arrowKey, el)) {
                e.preventDefault();
                e.stopImmediatePropagation();
                return;
            }
            if ((arrowKey === 'ArrowDown' || arrowKey === 'ArrowUp') && window.K7 && window.K7.TvDetailScroll) {
                if (window.K7.TvDetailScroll.handleVerticalNav(arrowKey, el)) {
                    e.preventDefault();
                    e.stopPropagation();
                    return;
                }
            }
            // Help dialogs / overflow panels: scroll remaining content before leaving
            // the region (e.g. last expansion header -> dialog Close).
            if ((arrowKey === 'ArrowDown' || arrowKey === 'ArrowUp')
                && tryScrollOverflowOnArrow(el, arrowKey)) {
                e.preventDefault();
                e.stopImmediatePropagation();
                return;
            }
            // When an activatable element is in editing mode, let the event through
            if (el && isActivatable(el) && isEditing(el)) {
                if (window.SpatialNavigation) SpatialNavigation.pause();
                // Seekbar: drive scrub via JS->.NET so TV key-repeat does not wait on Blazor @onkeydown.
                if (el.classList.contains('seekbar-container')
                    && (arrowKey === 'ArrowLeft' || arrowKey === 'ArrowRight')) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    if (window.K7 && window.K7.scrubSeekBar)
                        window.K7.scrubSeekBar(arrowKey === 'ArrowLeft' ? -1 : 1);
                    return;
                }
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
        if ((e.ctrlKey || e.metaKey) && (key === 'a' || key === 'A') && _selectionModeCallback) {
            var layerForSelectAll = peekLayer();
            if (layerForSelectAll && layerForSelectAll.type !== 'page')
                return;
            var activeForSelectAll = document.activeElement;
            if (!(activeForSelectAll && isTextInput(activeForSelectAll))) {
                e.preventDefault();
                e.stopImmediatePropagation();
                invokeCallback(_selectionModeCallback, 'OnSelectionSelectAll');
                return;
            }
        }
        if (key === ' ' && active && active.closest('.video-controls-overlay')) { e.preventDefault(); }
    }

    // Focus Guard - prevent focus from escaping the active layer

    var _guardingFocus = false;

    document.addEventListener('toggle', handleNavGroupToggle, true);

    // Track intentional AppNav focus so delayed focusFirst / MutationObserver
    // retries do not yank the user back to the page carousel.
    document.addEventListener('focusin', function (e) {
        if (!e.target || !isAppNavFocusable(e.target)) return;
        var from = e.relatedTarget;
        if (from && from.closest && !from.closest('.app-nav') && !isInsideInactiveFeedHub(from)) {
            _userChoseAppNav = true;
            markPageFocusSettled();
        }
    }, true);

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
            // Mouse / touch focus must not auto-scroll the page (carousel drag, clicks).
            if (window.K7 && window.K7.isKeyboardNavMode && !window.K7.isKeyboardNavMode()) return;
            // Hero pages: return to hero only via TvDetailScroll actions zone, never via
            // "near top" heuristics (that wrongly snaps when moving between below carousels).
            var tvScrollRootEarly = el.closest('[data-tv-scroll]');
            if (isNearPageTop(el) && !tvScrollRootEarly) {
                var nearTopScrollRoot = getFocusScrollRoot(el);
                // Home / Explore feeds: never yank to scrollTop 0 - that overshoots and
                // leaves the focused carousel truncated below the viewport. Incremental
                // scrollCardIntoTvView below keeps the row in view instead.
                if (nearTopScrollRoot
                    && !nearTopScrollRoot.classList.contains('page-scrollable')
                    && nearTopScrollRoot.scrollTop > 0) {
                    nearTopScrollRoot.scrollTo({ top: 0, behavior: 'smooth' });
                    return;
                }
            }
            if (!_carouselNavHandled) {
                scrollCarouselToElement(el);
            }
            var tvScrollRoot = tvScrollRootEarly;
            var hasTvScroll = !!(tvScrollRoot && window.K7 && window.K7.TvDetailScroll && window.K7.TvDetailScroll.hasInstance(tvScrollRoot));
            if (tvScrollRoot && !el.closest('[data-tv-scroll-zone="below"]')) {
                if (!_carouselNavHandled) {
                    scrollCarouselToElement(el);
                }
                // Only clamp the hero view for the actions row; other hero focusables
                // (synopsis, etc.) must not yank scroll while browsing below content.
                if (hasTvScroll && el.closest('[data-tv-scroll-zone="actions"]')) {
                    window.K7.TvDetailScroll.clampMainView(el);
                    return;
                }
                if (hasTvScroll) return;
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
        var layer = peekLayer();
        if (layer && layer.el) {
            var inLayer = layer.el.querySelectorAll(selector);
            for (var l = 0; l < inLayer.length; l++) {
                if (!isInsideInactiveFeedHub(inLayer[l]))
                    return inLayer[l];
            }
        }
        var roots = [
            document.querySelector('.app-main .page-viewport'),
            document.querySelector('.empty-layout'),
            document.querySelector('.app-main')
        ];
        for (var i = 0; i < roots.length; i++) {
            if (!roots[i]) continue;
            var matches = roots[i].querySelectorAll(selector);
            for (var j = 0; j < matches.length; j++) {
                if (!isInsideInactiveFeedHub(matches[j]))
                    return matches[j];
            }
        }
        var fallback = document.querySelectorAll(selector);
        for (var k = 0; k < fallback.length; k++) {
            if (!isInsideInactiveFeedHub(fallback[k]))
                return fallback[k];
        }
        return null;
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
        var prev = document.activeElement;
        if (prev && prev !== el && prev !== document.body && typeof prev.blur === 'function') {
            try { prev.blur(); } catch (ex) { }
        }
        try {
            el.focus({ preventScroll: true, focusVisible: true });
        } catch (ex) {
            el.focus({ preventScroll: true });
        }
    }

    function focusTargetElement(el) {
        if (!el || !el.isConnected) return false;
        if (isInsideInactiveFeedHub(el)) return false;
        if (el.closest('[data-carousel-item]')) {
            scrollCarouselToElement(el);
        }
        if (el.matches(FOCUSABLE) && isElementVisible(el)) {
            applyDomFocus(el);
            return true;
        }
        var focusable = Array.from(el.querySelectorAll(FOCUSABLE)).find(function (candidate) {
            return !isInsideInactiveFeedHub(candidate) && isElementVisible(candidate);
        });
        if (focusable) {
            applyDomFocus(focusable);
            return true;
        }
        if (el.matches('input, textarea, select, button, a[href], [tabindex]:not([tabindex="-1"])')
            && isElementVisible(el)) {
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
            if (isInsideInactiveFeedHub(marker)) continue;
            var selector = marker.getAttribute('data-initial-focus');
            if (isInitialFocusSelector(selector)) {
                var scoped = null;
                var scopedMatches = marker.querySelectorAll(selector);
                for (var s = 0; s < scopedMatches.length; s++) {
                    if (!isInsideInactiveFeedHub(scopedMatches[s])) {
                        scoped = scopedMatches[s];
                        break;
                    }
                }
                var target = scoped || queryFocusSelector(selector);
                if (target && !isInsideInactiveFeedHub(target)) return target;
                continue;
            }
            return marker;
        }
        return null;
    }

    function markPageFocusSettled() {
        _pageFocusSettled = true;
    }

    function resetPageFocusSettled() {
        _pageFocusSettled = false;
        _userChoseAppNav = false;
    }

    function isAppNavFocusable(el) {
        return !!(el && el.closest && el.closest('.app-nav') && el.matches && el.matches(FOCUSABLE));
    }

    function focusFirst(selector) {
        var delays = selector
            ? (isStandaloneAuthPage() ? [100, 300, 600, 1200, 2000] : [100, 300, 600])
            : [100];
        var resolved = false;

        function attempt(index) {
            if (resolved) return;

            // After initial page focus landed, do not yank focus back from the navbar
            // on delayed retries (user may have already moved up intentionally).
            // Also stop retrying once the user has moved from page content to AppNav.
            if (isAppNavFocusable(document.activeElement)
                && (_pageFocusSettled || _userChoseAppNav || index > 0)) {
                resolved = true;
                markPageFocusSettled();
                return;
            }

            var el = selector ? queryFocusSelector(selector) : null;
            if (el && focusTargetElement(el)) {
                resolved = true;
                markPageFocusSettled();
                return;
            }

            // A specific selector (dialog keypad, overlay control) must not fall back to
            // the page's [data-initial-focus] - on select-profile that is the profile card.
            var layer = peekLayer();
            if (selector && layer && layer.type !== 'page') {
                if (index < delays.length - 1) {
                    setTimeout(function () { attempt(index + 1); }, delays[index + 1] - delays[index]);
                }
                return;
            }

            var pageTarget = getPageFocusTarget();
            if (pageTarget && focusTargetElement(pageTarget)) {
                resolved = true;
                markPageFocusSettled();
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
            if (items.length > 0) {
                items[0].focus({ preventScroll: true });
                markPageFocusSettled();
                return;
            }
        }

        var items = getFocusablesInPageContent();
        if (items.length > 0) {
            items[0].focus({ preventScroll: true });
            markPageFocusSettled();
            return;
        }

        var all = getFocusables(document.body).filter(function (el) {
            return !el.closest('.app-nav');
        });
        if (all.length > 0) {
            all[0].focus({ preventScroll: true });
            markPageFocusSettled();
        }
    }

    function focusFirstInPage() {
        var pageTarget = getPageFocusTarget();
        if (pageTarget && focusTargetElement(pageTarget)) {
            markPageFocusSettled();
            return;
        }
        focusFirstFocusableInPage();
    }

    function shouldRefocusPage(el) {
        if (!el || el === document.body || el === document.documentElement) return true;
        // Focus left on a hidden Movie/detail route while FeedHub Home is showing.
        if (el.closest && el.closest('.page-route--hub-delegated')) return true;
        // Focus stuck on a parked (inert / inactive) hub page.
        if (isInsideInactiveFeedHub(el)) return true;
        if (/^H[1-6]$/.test(el.tagName)) return true;
        if (el.hasAttribute('tabindex') && el.getAttribute('tabindex') === '-1' && !el.matches(FOCUSABLE)) return true;
        // Pull focus off the navbar only until the page's initial focus has landed.
        // After that (or once the user intentionally moved to AppNav), do not yank back.
        if (el.closest && el.closest('.app-nav') && getPageFocusTarget()) {
            if (_pageFocusSettled || _userChoseAppNav) return false;
            return true;
        }
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
            } else if (focusTargetElement(pageTarget)) {
                markPageFocusSettled();
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

    // Enter edit mode on an activatable control (removes readonly, pauses SN).
    // Used for programmatic autofocus where FocusAsync alone leaves the field locked.
    function startEditingElement(el) {
        if (!el) return;
        if (isActivatable(el)) {
            if (isEditing(el)) {
                if (isTextInput(el)) el.focus({ preventScroll: true });
                return;
            }
            startEditing(el);
            if (window.SpatialNavigation) SpatialNavigation.pause();
            el.dispatchEvent(new CustomEvent('sn:editstart', { bubbles: false }));
            return;
        }
        applyDomFocus(el);
    }

    function onPageNavigated() {
        _selectionModeCallback = null;
        resetPageFocusSettled();
        setTimeout(ensurePageFocus, 150);
    }

    // Home Escape

    function registerHomeEscape(dotNetRef, homePattern) {
        _homeEscapeCallback = dotNetRef;
        if (homePattern) _homePattern = new RegExp(homePattern);
    }

    function registerSelectionMode(dotNetRef) {
        _selectionModeCallback = dotNetRef;
    }

    function unregisterSelectionMode() {
        _selectionModeCallback = null;
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

    // Close playback settings one level: detail -> root menu, root menu -> closed.
    // Returns true when a level was closed (caller must not hide the video overlay).
    function tryClosePlaybackSettingsLevel() {
        var menu = document.querySelector('.playback-settings-menu--open');
        if (!menu) return false;

        if (menu.classList.contains('playback-settings-menu--detail')) {
            var backBtn = menu.querySelector('.playback-settings-panel--detail .playback-settings-back');
            if (backBtn) {
                var backStyle = window.getComputedStyle(backBtn);
                if (backStyle.display !== 'none' && backStyle.visibility !== 'hidden') {
                    backBtn.click();
                    return true;
                }
            }
            var activeNav = menu.querySelector('.playback-settings-nav-item--active');
            if (activeNav) {
                activeNav.click();
                return true;
            }
            return true;
        }

        var closeBtn = menu.querySelector('.playback-settings-close');
        if (closeBtn) closeBtn.click();
        return true;
    }

    function cancelEditingIn(rootSelector) {
        var root = rootSelector ? document.querySelector(rootSelector) : document;
        if (!root) return;
        var editing = root.querySelector('[data-sn-editing]');
        if (!editing) return;
        stopEditing(editing);
        editing.dispatchEvent(new CustomEvent('sn:editcancel', { bubbles: false }));
        if (isTextInput(editing) && isOpenSearchSelectRoot(editing)) {
            editing.focus({ preventScroll: true });
        }
        resumeSpatialNavUnlessSearchSelectOpen(editing);
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
        if (root.querySelector('[data-sn-editing]')) return true;
        // JS-local seekbar scrub may run briefly before data-sn-editing is set.
        if (root.querySelector('.seekbar-container.scrubbing')) return true;
        if (window.K7 && K7.SeekBar && K7.SeekBar._scrub && K7.SeekBar._scrub.el
            && root.contains(K7.SeekBar._scrub.el))
            return true;
        return false;
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
                    if (isInsideInactiveFeedHub(el)) return false;
                    if (window.getComputedStyle(el).visibility === 'hidden') return false;
                    if (isInClosedDetailsContent(el)) return false;
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

            // Auto-refresh after meaningful DOM mutations (covers Blazor re-renders).
            // Ignore image loading churn (spinner nodes under .k7-img-wrap) so TV remote
            // input is not starved while a grid of MediaCards finishes loading.
            syncSections();
            var observer = new MutationObserver(function (mutations) {
                if (!hasMeaningfulDomMutation(mutations))
                    return;
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

        function isImageLoadingNode(node) {
            if (!node || node.nodeType !== 1) return false;
            var el = node;
            if (el.classList && el.classList.contains('k7-img-loading')) return true;
            if (el.closest && el.closest('.k7-img-loading')) return true;
            return false;
        }

        function isImageLoadingMutation(mutation) {
            if (mutation.type !== 'childList') return false;
            var i;
            for (i = 0; i < mutation.addedNodes.length; i++) {
                if (!isImageLoadingNode(mutation.addedNodes[i])) return false;
            }
            for (i = 0; i < mutation.removedNodes.length; i++) {
                if (!isImageLoadingNode(mutation.removedNodes[i])) return false;
            }
            return mutation.addedNodes.length > 0 || mutation.removedNodes.length > 0;
        }

        function hasMeaningfulDomMutation(mutations) {
            for (var i = 0; i < mutations.length; i++) {
                if (!isImageLoadingMutation(mutations[i]))
                    return true;
            }
            return false;
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
        window.K7.cancelVideoSeekOrEdit = cancelVideoSeekBarScrubIfAny;
        // Native Activity Back while video is up - never wait on Blazor JSRuntime.
        window.K7.handleVideoTvBack = function () {
            if (window.K7.tvDpadHoldStop) K7.tvDpadHoldStop();
            var cancel = cancelVideoSeekBarScrubIfAny();
            if (cancel === 'soft') {
                return 'soft';
            }
            if (cancel === 'hard') {
                return 'hard';
            }

            // Close playback settings menu / submenu one level at a time (do not hide overlay).
            if (tryClosePlaybackSettingsLevel()) {
                return 'menu';
            }

            var overlay = document.querySelector('.video-controls-overlay');
            if (overlay && !overlay.classList.contains('controls-hidden')) {
                if (window.K7.hideVideoControlsOverlay) K7.hideVideoControlsOverlay();
                invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteOverlayHidden');
                return 'hide';
            }

            // Close player via native JavascriptInterface (K7.tvNativeClosePlayer may be missing
            // if SpatialNav init raced the bridge inject).
            var closed = false;
            try {
                if (window.K7TvVideo && typeof K7TvVideo.closePlayer === 'function') {
                    K7TvVideo.closePlayer();
                    closed = true;
                } else if (window.K7 && typeof K7.tvNativeClosePlayer === 'function') {
                    K7.tvNativeClosePlayer();
                    closed = true;
                }
            } catch (exNative) { }
            if (closed) {
                return 'close';
            }
            if (_videoPlayerBackCallback) {
                try {
                    if (_videoPlayerBackCallback.invokeMethod)
                        _videoPlayerBackCallback.invokeMethod('OnLayerClosed');
                    else if (_videoPlayerBackCallback.invokeMethodAsync)
                        _videoPlayerBackCallback.invokeMethodAsync('OnLayerClosed');
                } catch (exClose) { }
            }
            return 'close';
        };
        // Native Activity forwards DPAD here when ExoPlayer stole WebView focus after seek.
        window.K7.dispatchTvArrowKey = function (arrowKey, action, keyCode, repeat) {
            var fake = {
                key: arrowKey || '',
                code: arrowKey || '',
                keyCode: keyCode || 0,
                which: keyCode || 0,
                repeat: !!repeat,
                target: document.activeElement,
                preventDefault: function () { },
                stopImmediatePropagation: function () { },
                stopPropagation: function () { }
            };
            if (action === 'keyup') {
                handleKeyUp(fake);
            } else {
                handleKeyDown(fake);
            }
        };

        // Short press L/R (chrome hidden): configured skip prefs. Long press: overlay scrub.
        // While seekbar editing: hold-scrub. While chrome visible: SpatialNav.move + fallback.
        window.K7.navigateVideoOverlay = function (arrowKey) {
            var overlay = document.querySelector('.video-controls-overlay');
            if (!overlay || overlay.classList.contains('controls-hidden')) return false;

            var snDir = arrowKey === 'ArrowLeft' ? 'left'
                : arrowKey === 'ArrowRight' ? 'right'
                : arrowKey === 'ArrowUp' ? 'up'
                : arrowKey === 'ArrowDown' ? 'down'
                : '';
            if (!snDir) return false;

            var before = document.activeElement;
            if (window.SpatialNavigation) {
                try { SpatialNavigation.resume(); } catch (exResume) { }
                try { SpatialNavigation.makeFocusable(); } catch (exMf) { }
                try {
                    SpatialNavigation.move(snDir);
                } catch (exMove) { }
            }

            var after = document.activeElement;
            if (after && after !== before && overlay.contains(after)) {
                return true;
            }

            // SpatialNav.move often no-ops with injected keys / pause / 0-size siblings.
            // Fall back to geometric focus among visible .focusable controls in the overlay.
            var items = Array.prototype.slice.call(overlay.querySelectorAll('.focusable')).filter(function (el) {
                return el.offsetWidth > 0 && el.offsetHeight > 0 && !el.hasAttribute('disabled');
            });
            if (!items.length) return false;

            var cur = (before && overlay.contains(before)) ? before : items[0];
            if (items.indexOf(cur) < 0) cur = items[0];
            var curRect = cur.getBoundingClientRect();
            var curCx = curRect.left + curRect.width / 2;
            var curCy = curRect.top + curRect.height / 2;
            var best = null;
            var bestScore = Infinity;
            for (var i = 0; i < items.length; i++) {
                var el = items[i];
                if (el === cur) continue;
                var r = el.getBoundingClientRect();
                var cx = r.left + r.width / 2;
                var cy = r.top + r.height / 2;
                var dx = cx - curCx;
                var dy = cy - curCy;
                var primary = 0;
                var secondary = 0;
                if (snDir === 'left') {
                    if (dx >= -2) continue;
                    primary = -dx;
                    secondary = Math.abs(dy);
                } else if (snDir === 'right') {
                    if (dx <= 2) continue;
                    primary = dx;
                    secondary = Math.abs(dy);
                } else if (snDir === 'up') {
                    if (dy >= -2) continue;
                    primary = -dy;
                    secondary = Math.abs(dx);
                } else {
                    if (dy <= 2) continue;
                    primary = dy;
                    secondary = Math.abs(dx);
                }
                var score = primary * 1000 + secondary;
                if (score < bestScore) {
                    bestScore = score;
                    best = el;
                }
            }

            if (!best) {
                return false;
            }

            try { best.focus({ preventScroll: true }); } catch (exFocus) { }
            if (window.SpatialNavigation && SpatialNavigation.focus) {
                try { SpatialNavigation.focus(best, true); } catch (exSnFocus) { }
            }
            return true;
        };

        window.K7.tvDpadHoldStop = function (isKeyUp) {
            var hold = window.K7._tvDpadHold;
            window.K7._tvDpadHold = null;
            if (!hold) return;

            if (hold.longTimer) clearTimeout(hold.longTimer);
            if (hold.timer) clearInterval(hold.timer);

            // Short press released before long-press threshold.
            if (isKeyUp && hold.mode === 'pending' && hold.dir) {
                var delta = window.K7.getTvSkipDelta
                    ? window.K7.getTvSkipDelta(hold.dir)
                    : (hold.dir < 0 ? -10 : 10);
                var nativeOk = false;
                try {
                    if (window.K7TvVideo && typeof K7TvVideo.skip === 'function') {
                        K7TvVideo.skip(hold.dir);
                        nativeOk = true;
                    } else if (window.K7TvVideo && typeof K7TvVideo.seekBy === 'function') {
                        K7TvVideo.seekBy(delta);
                        nativeOk = true;
                    }
                } catch (exBy) { }
                // Native already seeked - only ask Blazor for HUD. Otherwise full skip via DotNet.
                if (nativeOk)
                    invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteSkipHud', delta);
                else
                    invokeCallbackAsync(_videoPlayerRemoteRef, 'OnRemoteSkipDirection', hold.dir);
            }
            // mode === 'scrub': interval stopped; leave scrub session for Select commit.
        };
        window.K7.tvDpadHoldStart = function (arrowKey, keyCode) {
            var arrow = arrowKey || '';
            var overlay = document.querySelector('.video-controls-overlay');
            var seekbar = overlay && overlay.querySelector('.seekbar-container');
            var editing = !!(seekbar && seekbar.hasAttribute('data-sn-editing'));
            var hidden = !!(overlay && overlay.classList.contains('controls-hidden'));
            var isHorizontal = arrow === 'ArrowLeft' || arrow === 'ArrowRight';
            var dir = arrow === 'ArrowLeft' ? -1 : 1;

            // Chrome visible: navigate controls (all directions). Fake keydown never reaches
            // SpatialNavigation's own listener - move() + geometric fallback.
            if (overlay && !hidden && !(editing || (seekbar && seekbar.classList.contains('scrubbing')))) {
                window.K7.tvDpadHoldStop(false);
                if (!window.K7.navigateVideoOverlay(arrow))
                    window.K7.dispatchTvArrowKey(arrow, 'keydown', keyCode || 0, false);
                return;
            }

            if (!isHorizontal) {
                window.K7.tvDpadHoldStop(false);
                window.K7.dispatchTvArrowKey(arrow, 'keydown', keyCode || 0, false);
                return;
            }

            // Already scrubbing (seekbar edit): accelerate with hold interval.
            if (editing || (seekbar && seekbar.classList.contains('scrubbing'))) {
                if (window.K7._tvDpadHold && window.K7._tvDpadHold.dir === dir
                    && window.K7._tvDpadHold.mode === 'scrub')
                    return;
                window.K7.tvDpadHoldStop(false);
                if (window.K7.beginSeekBarScrub) window.K7.beginSeekBarScrub(dir);
                window.K7._tvDpadHold = {
                    dir: dir,
                    mode: 'scrub',
                    timer: setInterval(function () {
                        if (window.K7.beginSeekBarScrub) window.K7.beginSeekBarScrub(dir);
                    }, 90)
                };
                return;
            }

            // Chrome hidden: arm short-skip vs long-scrub.
            // Ignore key-repeat while pending/scrubbing so OS auto-repeat cannot reset the
            // 400ms long-press timer (web keyboard) or stack a second hold.
            if (window.K7._tvDpadHold && window.K7._tvDpadHold.dir === dir
                && (window.K7._tvDpadHold.mode === 'pending' || window.K7._tvDpadHold.mode === 'scrub'))
                return;
            window.K7.tvDpadHoldStop(false);
            window.K7._tvDpadHold = {
                dir: dir,
                mode: 'pending',
                longTimer: setTimeout(function () {
                    var h = window.K7._tvDpadHold;
                    if (!h || h.dir !== dir || h.mode !== 'pending') return;
                    h.mode = 'scrub';
                    h.longTimer = null;
                    if (window.K7.beginSeekBarScrub) window.K7.beginSeekBarScrub(dir);
                    h.timer = setInterval(function () {
                        if (window.K7.beginSeekBarScrub) window.K7.beginSeekBarScrub(dir);
                    }, 90);
                }, 400)
            };
        };

        watchBlazorErrorUi();
    }

    // When Blazor shows #blazor-error-ui (circuit/render failure outside ErrorBoundary),
    // promote it as the top spatial-nav layer and focus Reload so TV remotes can recover.
    function watchBlazorErrorUi() {
        var el = document.getElementById('blazor-error-ui');
        if (!el) return;

        var reload = el.querySelector('a.reload, .reload');
        var dismiss = el.querySelector('.dismiss');
        if (reload) {
            reload.classList.add('focusable');
            if (!reload.hasAttribute('tabindex')) reload.setAttribute('tabindex', '0');
        }
        if (dismiss) {
            dismiss.classList.add('focusable');
            if (!dismiss.hasAttribute('tabindex')) dismiss.setAttribute('tabindex', '0');
        }

        var wasVisible = isBlazorErrorUiVisible(el);

        function sync() {
            var visible = isBlazorErrorUiVisible(el);
            if (visible === wasVisible) return;
            wasVisible = visible;

            if (visible) {
                pushLayer(el, 'error', {});
                if (window.SpatialNavigation) {
                    try { SpatialNavigation.makeFocusable(); } catch (e) { }
                }
                if (reload) {
                    setTimeout(function () {
                        try { reload.focus({ preventScroll: true }); } catch (e2) { }
                    }, 50);
                }
            } else {
                popLayer(el);
            }
        }

        var observer = new MutationObserver(sync);
        observer.observe(el, { attributes: true, attributeFilter: ['style', 'class'] });
    }

    function isBlazorErrorUiVisible(el) {
        if (!el) return false;
        // Blazor toggles the inline style to display:block on unhandled errors.
        if (el.style && el.style.display === 'block') return true;
        if (el.style && el.style.display === 'none') return false;
        return isElementVisible(el);
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
        startEditing: startEditingElement,
        refresh: refresh,
        addSection: addSection,
        removeSection: removeSection,
        registerHomeEscape: registerHomeEscape,
        registerSelectionMode: registerSelectionMode,
        unregisterSelectionMode: unregisterSelectionMode,
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

// Hero snap / focus-scroll is for keyboard and TV remotes only.
// Mouse and touch must not move the page when focusing or dragging carousels.
K7._inputModality = 'keyboard';
K7.isKeyboardNavMode = function () {
    return document.documentElement.classList.contains('platform-tv')
        || K7._inputModality === 'keyboard';
};
(function trackInputModality() {
    function setModality(mode) {
        if (K7._inputModality === mode
            && document.documentElement.getAttribute('data-input-modality') === mode) {
            return;
        }
        K7._inputModality = mode;
        document.documentElement.setAttribute('data-input-modality', mode);
    }

    setModality('keyboard');

    document.addEventListener('pointerdown', function () {
        setModality('pointer');
    }, true);
    // Mouse hover without a click must still exit keyboard mode, otherwise
    // hover overlays stay suppressed after arrow-key navigation.
    document.addEventListener('pointermove', function (e) {
        if (e.pointerType === 'mouse' || e.pointerType === 'pen') {
            setModality('pointer');
        }
    }, true);
    document.addEventListener('keydown', function (e) {
        var key = e.key;
        if (key === 'Tab'
            || key === 'ArrowUp' || key === 'ArrowDown'
            || key === 'ArrowLeft' || key === 'ArrowRight'
            || key === 'Home' || key === 'End'
            || key === 'PageUp' || key === 'PageDown') {
            setModality('keyboard');
        }
    }, true);
})();

K7._backgroundLockCount = 0;
K7._dialogLockActive = false;

K7.setNativePlayerActive = function (active, windowsWebVideo) {
    document.documentElement.classList.toggle('native-player-active', !!active);
    document.body.classList.toggle('native-player-active', !!active);
    var useWindowsWebVideo = !!active && !!windowsWebVideo;
    document.documentElement.classList.toggle('windows-web-video', useWindowsWebVideo);
    document.body.classList.toggle('windows-web-video', useWindowsWebVideo);
    if (!active) {
        document.documentElement.classList.remove('native-player-playing');
        document.body.classList.remove('native-player-playing');
        requestAnimationFrame(function () {
            var app = document.getElementById('app');
            if (app) app.style.removeProperty('visibility');
            var chrome = document.querySelectorAll('.app-nav, .app-nav-bar, .app-nav-popover, .k7-menu-dropdown');
            for (var i = 0; i < chrome.length; i++) {
                chrome[i].style.removeProperty('visibility');
                chrome[i].style.removeProperty('opacity');
            }
            // Native video hides the WebView; Embla keeps stale 0-width snaps until reInit.
            // Delay past Android WebView IsVisible restore + layout.
            var settleAfterNativeVideo = function () {
                // Restore carousel snaps before focus so SpatialNav cannot land on loop-back.
                if (window.K7 && window.K7.reInitAndRestoreCarousels) {
                    window.K7.reInitAndRestoreCarousels();
                }
                if (window.K7 && window.K7.restoreFocusAfterNativeVideo) {
                    window.K7.restoreFocusAfterNativeVideo();
                }
                if (window.SpatialNav && window.SpatialNav.refresh) window.SpatialNav.refresh();
                if (window.K7 && window.K7.restoreFocusAfterNativeVideo) {
                    window.K7.restoreFocusAfterNativeVideo();
                }
            };
            setTimeout(function () {
                requestAnimationFrame(settleAfterNativeVideo);
            }, 50);
            setTimeout(function () {
                requestAnimationFrame(settleAfterNativeVideo);
            }, 220);
        });
    }
};

/** ReInit Embla after the WebView is shown again, keeping the last real snap. */
K7.reInitAndRestoreCarousels = function () {
    var nodes = document.querySelectorAll('[data-carousel]');
    for (var i = 0; i < nodes.length; i++) {
        var restore = nodes[i].__k7RestoreCarousel;
        if (typeof restore === 'function') {
            try { restore(); } catch (e) { /* ignore */ }
            continue;
        }
        if (nodes[i].__embla) {
            try { nodes[i].__embla.reInit(); } catch (e2) { /* ignore */ }
        }
    }
};

function resolveHeroFocusable(target) {
    if (!target) return null;
    if (target.classList && target.classList.contains('focusable')) return target;
    if (target.querySelector) {
        var inner = target.querySelector('.focusable, button, a, [tabindex]:not([tabindex="-1"])');
        if (inner) return inner;
    }
    return target;
}

function scrollCarouselToFocusedItem(el) {
    if (!el || !el.closest) return;
    var carousel = el.closest('[data-carousel]');
    if (!carousel || !carousel.__embla) return;
    var item = el.closest('[data-carousel-item]');
    if (!item) return;
    var slides = [];
    try { slides = carousel.__embla.slideNodes(); } catch (e) { return; }
    for (var i = 0; i < slides.length; i++) {
        if (slides[i] === item) {
            try { carousel.__embla.scrollTo(i, true); } catch (e2) { }
            return;
        }
    }
}

/** After Android/iOS native chrome closes, put TV focus back on Play or the hero carousel card. */
K7.restoreFocusAfterNativeVideo = function () {
    var root = document.querySelector('[data-tv-scroll]');
    if (root && window.K7 && window.K7.TvDetailScroll && window.K7.TvDetailScroll.hasInstance(root)) {
        try { window.K7.TvDetailScroll.scrollToMain(root, true); } catch (e) { /* ignore */ }
    }

    var lastHero = null;
    if (root && window.K7 && window.K7.TvDetailScroll && window.K7.TvDetailScroll.getLastHeroFocus) {
        try { lastHero = window.K7.TvDetailScroll.getLastHeroFocus(root); } catch (e2) { lastHero = null; }
    }

    var target =
        document.querySelector('[data-tv-scroll-zone="actions"] [data-initial-focus]')
        || document.querySelector('.movie-actions-play[data-initial-focus], .serie-actions-play[data-initial-focus], .episode-actions-play[data-initial-focus]')
        || document.querySelector('[data-tv-scroll-zone="actions"][data-initial-focus]')
        || document.querySelector('[data-tv-scroll-zone="episodes"] [data-initial-focus]')
        || document.querySelector('[data-tv-scroll-zone="seasons"] [data-initial-focus]')
        || lastHero
        || document.querySelector('[data-initial-focus]');
    if (!target) return;

    target = resolveHeroFocusable(target);
    scrollCarouselToFocusedItem(target);

    if (window.SpatialNav && window.SpatialNav.focusElement) {
        window.SpatialNav.focusElement(target);
    } else {
        target.focus({ preventScroll: true });
    }
};

/** Android/iOS native MediaElement: unlock CSS/WebView see-through once frames can show. */
K7.setNativePlayerPlaying = function (playing) {
    document.documentElement.classList.toggle('native-player-playing', !!playing);
    document.body.classList.toggle('native-player-playing', !!playing);
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

K7.attachSearchSelectPortal = function (root, dropdown) {
    if (!root || !dropdown) return;
    K7._teleportMenuElement(dropdown, root);
    dropdown.classList.add('k7-search-select-dropdown--teleported');
};

K7.detachSearchSelectPortal = function (root, dropdown) {
    if (!dropdown) return;
    K7._restoreMenuElement(dropdown, root);
    dropdown.classList.remove('k7-search-select-dropdown--teleported');
};

K7.positionSearchSelectDropdown = function (root, dropdown) {
    if (!root || !dropdown) return;

    // Escape dialog/rule-row stacking so hints paint above later controls.
    K7.attachSearchSelectPortal(root, dropdown);

    var rect = root.getBoundingClientRect();
    var gap = 4;
    var maxHeight = Math.min(240, Math.floor(window.innerHeight * 0.4));
    var spaceBelow = window.innerHeight - rect.bottom - gap;
    var spaceAbove = rect.top - gap;
    var openUp = spaceBelow < 120 && spaceAbove > spaceBelow;
    var z = String((parseInt(getComputedStyle(document.documentElement).getPropertyValue('--z-dialog'), 10) || 1300) + 10);

    dropdown.style.position = 'fixed';
    dropdown.style.left = Math.max(8, Math.min(rect.left, window.innerWidth - rect.width - 8)) + 'px';
    dropdown.style.width = Math.max(rect.width, 160) + 'px';
    dropdown.style.right = 'auto';
    dropdown.style.maxHeight = maxHeight + 'px';
    dropdown.style.zIndex = z;

    if (openUp) {
        dropdown.style.top = 'auto';
        dropdown.style.bottom = (window.innerHeight - rect.top + gap) + 'px';
    } else {
        dropdown.style.bottom = 'auto';
        dropdown.style.top = (rect.bottom + gap) + 'px';
    }
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

K7.resumeSpatialNavIfIdle = function () {
    if (document.querySelector('[data-sn-editing]')) return;
    if (document.querySelector('.k7-search-select--open')) return;
    if (window.SpatialNavigation) SpatialNavigation.resume();
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
    var positionAnchor = K7._menuPositionAnchor;
    var anchor = K7._resolveMenuAnchor(root);
    if (positionAnchor) {
        anchor = positionAnchor;
    }
    var anchorRect = anchor.getBoundingClientRect();
    if (anchorRect.width === 0 && anchorRect.height === 0) {
        var mediaCardFallback = resolveMediaCardFromMenuRoot(root, positionAnchor);
        if (mediaCardFallback) {
            anchor = mediaCardFallback.querySelector('.media-card-container') || mediaCardFallback;
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
        var mediaCard = resolveMediaCardFromMenuRoot(root, positionAnchor);
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

function resolveMediaCardFromMenuRoot(root, positionAnchor) {
    if (positionAnchor && positionAnchor.closest) {
        var fromAnchor = positionAnchor.closest('.media-card');
        if (fromAnchor) return fromAnchor;
        if (positionAnchor.classList && positionAnchor.classList.contains('media-card'))
            return positionAnchor;
    }
    return root && root.closest ? root.closest('.media-card') : null;
}

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

    var activator = mediaCard.querySelector('.media-card-menu .media-card-menu-trigger')
        || mediaCard.querySelector('.media-card-menu .k7-menu-activator-inner');
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

// Physical keyboard / TV numpad capture for PinDialog.
K7.pinDialogKeyCapture = {
    _ref: null,
    _onKey: null,
    _focusTimer: null,

    attach: function (dotNetRef) {
        this.detach();
        this._ref = dotNetRef;
        var self = this;
        this._onKey = function (e) {
            if (!self._ref) return;
            if (!document.querySelector('.pin-dialog')) return;

            var digit = K7.pinDialogKeyCapture._digitFromEvent(e);
            if (digit !== null) {
                e.preventDefault();
                e.stopPropagation();
                self._ref.invokeMethodAsync('OnCapturedKey', digit);
                return;
            }

            var key = e.key;
            if (key !== 'Backspace')
                return;

            e.preventDefault();
            e.stopPropagation();
            self._ref.invokeMethodAsync('OnCapturedKey', key);
        };
        // Capture so TV number keys are seen even when focus is still on the profile card.
        document.addEventListener('keydown', this._onKey, true);
        this._focusKeypad();
    },

    _digitFromEvent: function (e) {
        var key = e.key || '';
        if (key === 'Backspace' || key === 'Tab' || key === 'Enter' || key === 'Escape'
            || key === 'ArrowUp' || key === 'ArrowDown' || key === 'ArrowLeft' || key === 'ArrowRight')
            return null;
        if (key.length === 1 && key >= '0' && key <= '9')
            return key;
        if (/^Numpad[0-9]$/.test(key))
            return key.charAt(6);

        var code = e.code || '';
        if (/^Digit[0-9]$/.test(code))
            return code.charAt(5);
        if (/^Numpad[0-9]$/.test(code))
            return code.charAt(6);

        var kc = e.keyCode || 0;
        if (kc >= 48 && kc <= 57)
            return String(kc - 48);
        if (kc >= 96 && kc <= 105)
            return String(kc - 96);
        // Android KEYCODE_0..9 (7-16) and KEYCODE_NUMPAD_0..9 (144-153) when e.key is omitted.
        if (key === '' || key === 'Unidentified') {
            if (kc >= 7 && kc <= 16)
                return String(kc - 7);
            if (kc >= 144 && kc <= 153)
                return String(kc - 144);
        }
        return null;
    },

    _focusKeypad: function () {
        var self = this;
        function waitForEnterRelease() {
            if (!self._ref) return;
            if (window.K7 && (K7._suppressEnterUntilKeyUp || K7._swallowNextEnterClick)) {
                self._focusTimer = setTimeout(waitForEnterRelease, 40);
                return;
            }
            self._focusKeypadNow(0);
        }
        waitForEnterRelease();
    },

    _focusKeypadNow: function (attempt) {
        var self = this;
        var delays = [0, 50, 120, 250, 450, 800];
        if (!self._ref) return;
        var key = document.querySelector('.pin-dialog__keypad .focusable, .pin-dialog__keypad .k7-btn');
        if (key) {
            try { key.focus({ preventScroll: true }); } catch (ex) { }
            if (document.activeElement === key)
                return;
        }
        var next = attempt + 1;
        if (next < delays.length) {
            self._focusTimer = setTimeout(function () { self._focusKeypadNow(next); }, delays[next] - delays[attempt]);
        }
    },

    detach: function () {
        if (this._focusTimer) {
            clearTimeout(this._focusTimer);
            this._focusTimer = null;
        }
        if (this._onKey) {
            document.removeEventListener('keydown', this._onKey, true);
            this._onKey = null;
        }
        this._ref = null;
    }
};

K7.registerMediaCardLongPress = function (el, dotNetRef) {
    if (!el) return;
    el._k7MediaCardDotNet = dotNetRef;
};

K7.unregisterMediaCardLongPress = function (el) {
    if (!el) return;
    el._k7MediaCardDotNet = null;
};

K7._menuPositionAnchor = null;

K7.setMenuPositionAnchor = function (el) {
    K7._menuPositionAnchor = el || null;
};

K7.clearMenuPositionAnchor = function () {
    K7._menuPositionAnchor = null;
};

K7.suppressEnterUntilKeyUp = function (callback) {
    K7._suppressEnterUntilKeyUp = true;
    K7._swallowNextEnterClick = true;
    if (typeof callback === 'function') {
        K7._enterSuppressCallbacks.push(callback);
    }
};

// Update the URL hash without involving Blazor navigation (so a mouse click
// on a focused episode link is not cancelled by NavigateTo).
K7.replaceUrlHash = function (hash) {
    if (!hash) return;
    var normalized = hash.charAt(0) === '#' ? hash : '#' + hash;
    if (window.location.hash === normalized) return;
    history.replaceState(null, '', window.location.pathname + window.location.search + normalized);
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
    var episodeCard = root.closest('.episode-card');
    if (episodeCard) return episodeCard;
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

    var positionAnchor = K7._menuPositionAnchor;
    var inMediaCard = !!root.closest('.media-card')
        || !!(positionAnchor && positionAnchor.closest && positionAnchor.closest('.media-card'));
    var forcePortal = !!positionAnchor;
    if (!inMediaCard && !forcePortal && !K7._needsMenuPortal(root)) {
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
            if (window.K7 && window.K7.isKeyboardNavMode && !window.K7.isKeyboardNavMode()) return;
            // Return to hero only when focusing the hero controls row.
            if (isInZone(e.target, 'actions')) {
                scrollToMain(false);
            } else if (isInZone(e.target, 'below')) {
                if (!inst.showingBelow) {
                    scrollToBelow();
                } else {
                    // Keep the below snap if a prior scroll nudge revealed the hero.
                    var main = getZone(inst.root, 'main');
                    if (main && inst.root.scrollTop < main.offsetHeight - 8) {
                        inst.root.scrollTop = main.offsetHeight;
                    }
                }
            } else if (isInZoneCarousel(e.target, 'episodes') || isInZoneCarousel(e.target, 'seasons')) {
                // Keep the last hero tile so ArrowUp from casting restores the same episode.
                inst.lastHeroFocus = e.target;
                if (inst.showingBelow) {
                    // Focus returned to episodes/seasons while the view still shows casting.
                    scrollToMain(false);
                } else {
                    clampMainView();
                }
            }
        }

        inst.scrollToMain = scrollToMain;
        inst.scrollToBelow = scrollToBelow;
        inst.clampMainView = clampMainView;
        inst.onFocusIn = onFocusIn;
        inst.handleVerticalNav = function (key, el) {
            if (!el || !inst.root.contains(el)) return false;

            if (!getZone(inst.root, 'below')) return false;

            function focusFirstInBelow() {
                var below = getZone(inst.root, 'below');
                var target = below && (
                    below.querySelector('[data-carousel-item]:not([data-carousel-loop-back]) .focusable')
                    || below.querySelector('.focusable')
                );
                if (!target) return false;
                target.focus({ preventScroll: true });
                return document.activeElement === target || target.contains(document.activeElement);
            }

            if (key === 'ArrowDown') {
                if (isInZoneCarousel(el, 'episodes') || isInZoneCarousel(el, 'seasons')) {
                    inst.lastHeroFocus = el;
                    scrollToBelow();
                    // Defer focus so this keydown finishes while focus is still on the
                    // episodes carousel (sync focus+preventDefault raced with Embla nav).
                    setTimeout(function () {
                        if (!inst.showingBelow) return;
                        focusFirstInBelow();
                    }, 0);
                    return true;
                }
                if (isInZone(el, 'actions') && !getZone(inst.root, 'seasons') && !getZone(inst.root, 'episodes')) {
                    scrollToBelow();
                    setTimeout(function () {
                        if (!inst.showingBelow) return;
                        focusFirstInBelow();
                    }, 0);
                    return true;
                }
                return false;
            }

            if (key === 'ArrowUp' && isInZone(el, 'below')) {
                // If nothing in the below zone sits above the focused card, leave toward
                // the hero carousel (seasons/episodes), then actions, then back. Otherwise
                // spatial nav would pick the top navbar because hero controls are off-screen.
                var below = getZone(inst.root, 'below');
                var currentRect = el.getBoundingClientRect();
                var candidates = below ? below.querySelectorAll('.focusable') : [];
                var hasAboveInBelow = false;
                for (var i = 0; i < candidates.length; i++) {
                    var cand = candidates[i];
                    if (cand === el || el.contains(cand) || cand.contains(el)) continue;
                    if (cand.offsetWidth <= 0 && cand.offsetHeight <= 0) continue;
                    var r = cand.getBoundingClientRect();
                    if (r.bottom < currentRect.top + 8) {
                        hasAboveInBelow = true;
                        break;
                    }
                }
                if (hasAboveInBelow) return false;

                scrollToMain(false);
                var target = null;
                if (inst.lastHeroFocus && inst.lastHeroFocus.isConnected
                    && inst.root.contains(inst.lastHeroFocus)) {
                    target = inst.lastHeroFocus;
                }
                // TV detail pages keep seasons/episodes on the hero between casting and
                // actions/synopsis; prefer those so ArrowUp from casting does not skip them.
                if (!target) {
                    var heroCarousel = getZone(inst.root, 'seasons') || getZone(inst.root, 'episodes');
                    if (heroCarousel) {
                        target = heroCarousel.querySelector('.focusable');
                    }
                }
                if (!target) {
                    var actions = getZone(inst.root, 'actions');
                    if (actions) {
                        target = actions.matches('.focusable') ? actions : actions.querySelector('.focusable');
                    }
                }
                if (!target) {
                    var main = getZone(inst.root, 'main');
                    target = main && main.querySelector('.k7-back-btn.focusable, .k7-back-btn .focusable, .focusable');
                }
                if (target) {
                    target.focus({ preventScroll: true });
                    return true;
                }
            }

            return false;
        };
    }

    return {
        init: function (root) {
            if (!root) return;
            K7.TvDetailScroll.dispose(root);
            var inst = { root: root, showingBelow: false, lastHeroFocus: null, onFocusIn: null };
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
            if (!inst || inst.showingBelow) return;
            // Do not yank scroll back to the hero while the user is on mouse/touch.
            if (window.K7 && window.K7.isKeyboardNavMode && !window.K7.isKeyboardNavMode()) return;
            inst.root.scrollTop = 0;
        },
        hasInstance: function (root) {
            return !!(root && _instances.has(root));
        },
        getLastHeroFocus: function (root) {
            var inst = root ? _instances.get(root) : null;
            var el = inst && inst.lastHeroFocus;
            return (el && el.isConnected && inst.root.contains(el)) ? el : null;
        },
        scrollToMain: function (root, instant) {
            var inst = root ? _instances.get(root) : null;
            if (inst) inst.scrollToMain(!!instant);
        },
        clampMainView: function (el) {
            if (window.K7 && window.K7.isKeyboardNavMode && !window.K7.isKeyboardNavMode()) return;
            var root = el && el.closest('[data-tv-scroll]');
            var inst = root && _instances.get(root);
            if (inst) inst.clampMainView();
        },
        handleVerticalNav: function (key, el) {
            var root = el && el.closest('[data-tv-scroll]');
            var inst = root && _instances.get(root);
            return !!(inst && inst.handleVerticalNav(key, el));
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

K7.focusById = function (id, preventScroll) {
    var el = document.getElementById(id);
    if (!el) return false;
    if (el.closest && (el.closest('[inert]') || el.closest('.feed-hub-page:not(.feed-hub-page--active)')))
        return false;
    var target = el.querySelector('.focusable') || el;
    target.focus({ preventScroll: !!preventScroll });
    return true;
};

K7.isAppNavFocused = function () {
    var el = document.activeElement;
    return !!(el && el.closest && el.closest('.app-nav'));
};

K7.RatingStars = {
    _instances: new WeakMap(),
    init: function (el, dotNetRef) {
        if (!el || typeof el.addEventListener !== 'function') return;
        var handlers = {
            start: function () { dotNetRef.invokeMethodAsync('OnEditStart'); },
            commit: function () { dotNetRef.invokeMethodAsync('OnEditCommit'); },
            cancel: function () { dotNetRef.invokeMethodAsync('OnEditCancel'); },
            pointerDown: function (e) {
                if (e.button != null && e.button !== 0) return;
                try { el.setPointerCapture(e.pointerId); } catch (ex) { }
            }
        };
        el.addEventListener('sn:editstart', handlers.start);
        el.addEventListener('sn:editcommit', handlers.commit);
        el.addEventListener('sn:editcancel', handlers.cancel);
        el.addEventListener('pointerdown', handlers.pointerDown);
        K7.RatingStars._instances.set(el, handlers);
    },
    dispose: function (el) {
        var h = K7.RatingStars._instances.get(el);
        if (h) {
            el.removeEventListener('sn:editstart', h.start);
            el.removeEventListener('sn:editcommit', h.commit);
            el.removeEventListener('sn:editcancel', h.cancel);
            el.removeEventListener('pointerdown', h.pointerDown);
            K7.RatingStars._instances.delete(el);
        }
    }
};

K7.SeekBar = {
    _instances: new WeakMap(),
    _scrub: null,
    _afterScrubCommitBusy: false,
    directChild: function (el, className) {
        if (!el || !el.children) return null;
        for (var i = 0; i < el.children.length; i++) {
            var child = el.children[i];
            if (child && child.classList && child.classList.contains(className))
                return child;
        }
        return null;
    },
    removeDirectChildren: function (el, className) {
        if (!el || !el.children) return;
        for (var i = el.children.length - 1; i >= 0; i--) {
            var child = el.children[i];
            if (child && child.classList && child.classList.contains(className))
                child.remove();
        }
    },
    init: function (el, dotNetRef) {
        if (!el || typeof el.addEventListener !== 'function') return;
        var handlers = {
            start: function () {
                try {
                    if (dotNetRef.invokeMethod) dotNetRef.invokeMethod('OnEditStart');
                    else dotNetRef.invokeMethodAsync('OnEditStart');
                } catch (ex) { }
                // Do not initLocalScrub until the first L/R step. OK-only edit must leave
                // _scrub null so Escape can soft-cancel without afterScrubCommit / hide.
            },
            commit: function () {
                var scrubTime = K7.SeekBar.getScrubTime(el);
                K7.SeekBar.clearLocalScrub(el);
                try {
                    if (dotNetRef.invokeMethodAsync)
                        dotNetRef.invokeMethodAsync('OnEditCommitAt', scrubTime);
                    else
                        dotNetRef.invokeMethodAsync('OnEditCommit');
                } catch (ex) { }
            },
            cancel: function () {
                K7.SeekBar.clearLocalScrub(el);
                dotNetRef.invokeMethodAsync('OnEditCancel');
            }
        };
        el.addEventListener('sn:editstart', handlers.start);
        el.addEventListener('sn:editcommit', handlers.commit);
        el.addEventListener('sn:editcancel', handlers.cancel);
        K7.SeekBar._instances.set(el, { handlers: handlers, dotNetRef: dotNetRef });
    },
    dispose: function (el) {
        var inst = K7.SeekBar._instances.get(el);
        if (inst && inst.handlers) {
            el.removeEventListener('sn:editstart', inst.handlers.start);
            el.removeEventListener('sn:editcommit', inst.handlers.commit);
            el.removeEventListener('sn:editcancel', inst.handlers.cancel);
            K7.SeekBar._instances.delete(el);
        }
        K7.SeekBar.clearLocalScrub(el);
    },
    initLocalScrub: function (el) {
        if (!el) return;
        var duration = parseFloat(el.getAttribute('aria-valuemax')) || 0;
        var current = parseFloat(el.getAttribute('aria-valuenow')) || 0;
        if (!isFinite(duration) || duration < 0) duration = 0;
        if (!isFinite(current) || current < 0) current = 0;
        current = Math.min(current, duration);
        K7.SeekBar._scrub = {
            el: el,
            time: current,
            duration: duration,
            repeatCount: 0,
            decayTimer: null
        };
        el.classList.add('scrubbing');
        el.setAttribute('data-scrub-time', String(current));
        var overlay = el.closest('.video-controls-overlay');
        if (overlay) {
            overlay.classList.add('seekbar-scrubbing');
            overlay.classList.remove('controls-hidden');
            overlay.classList.add('controls-visible');
        }
        // Drop any Blazor live-position preview nodes so only the scrub pair remains.
        K7.SeekBar.removeDirectChildren(el, 'thumb');
        K7.SeekBar.removeDirectChildren(el, 'thumbnail');
        K7.SeekBar.ensurePreview(el);
        K7.SeekBar.applyPreview(el, current, duration);
    },
    clearLocalScrub: function (el) {
        if (K7.SeekBar._scrub && (!el || K7.SeekBar._scrub.el === el)) {
            if (K7.SeekBar._scrub.decayTimer) clearTimeout(K7.SeekBar._scrub.decayTimer);
            K7.SeekBar._scrub = null;
        }
        if (el) {
            el.removeAttribute('data-scrub-time');
            el.classList.remove('scrubbing');
            K7.SeekBar.removeDirectChildren(el, 'thumb');
            K7.SeekBar.removeDirectChildren(el, 'thumbnail');
            var overlay = el.closest('.video-controls-overlay');
            if (overlay) overlay.classList.remove('seekbar-scrubbing');
        }
    },
    afterScrubCommit: function () {
        if (K7.SeekBar._afterScrubCommitBusy) return;
        K7.SeekBar._afterScrubCommitBusy = true;
        try {
            var overlay = document.querySelector('.video-controls-overlay');
            var seekbar = overlay && overlay.querySelector('.seekbar-container');
            if (seekbar) K7.SeekBar.clearLocalScrub(seekbar);
            if (window.K7 && K7.hideVideoControlsOverlay)
                K7.hideVideoControlsOverlay();
            else if (overlay) {
                overlay.classList.remove('seekbar-scrubbing', 'controls-visible');
                overlay.classList.add('controls-hidden');
                try { overlay.focus({ preventScroll: true }); } catch (ex) { }
            }
            if (window.SpatialNavigation) SpatialNavigation.resume();
        } finally {
            setTimeout(function () { K7.SeekBar._afterScrubCommitBusy = false; }, 100);
        }
    },
    getScrubTime: function (el) {
        if (K7.SeekBar._scrub && K7.SeekBar._scrub.el === el)
            return K7.SeekBar._scrub.time;
        var attr = el && el.getAttribute('data-scrub-time');
        var parsed = attr ? parseFloat(attr) : NaN;
        if (isFinite(parsed)) return parsed;
        return parseFloat(el && el.getAttribute('aria-valuenow')) || 0;
    },
    getStep: function (repeatCount) {
        if (repeatCount <= 4) return 2;
        if (repeatCount <= 10) return 5;
        if (repeatCount <= 18) return 10;
        if (repeatCount <= 28) return 20;
        if (repeatCount <= 40) return 30;
        return 60;
    },
    formatTime: function (seconds) {
        var s = Math.max(0, Math.floor(seconds || 0));
        var h = Math.floor(s / 3600);
        var m = Math.floor((s % 3600) / 60);
        var sec = s % 60;
        var pad = function (n) { return n < 10 ? '0' + n : String(n); };
        return h > 0 ? h + ':' + pad(m) + ':' + pad(sec) : m + ':' + pad(sec);
    },
    ensurePreview: function (el) {
        // Avoid :scope - older Android TV WebViews mishandle it and create duplicate nodes.
        var thumb = K7.SeekBar.directChild(el, 'thumb');
        if (thumb && !thumb.hasAttribute('data-scrub-preview')) {
            thumb.remove();
            thumb = null;
        }
        if (!thumb) {
            thumb = document.createElement('div');
            thumb.className = 'thumb';
            thumb.setAttribute('data-scrub-preview', 'true');
            el.appendChild(thumb);
        }
        thumb.style.position = 'absolute';
        thumb.style.top = '50%';
        thumb.style.width = '16px';
        thumb.style.height = '16px';
        thumb.style.backgroundColor = '#fff';
        thumb.style.borderRadius = '50%';
        thumb.style.transform = 'translate(-50%, -50%)';
        thumb.style.zIndex = '4';
        thumb.style.pointerEvents = 'none';
        thumb.style.display = '';
        thumb.style.visibility = '';

        var thumbnail = K7.SeekBar.directChild(el, 'thumbnail');
        if (thumbnail && !thumbnail.hasAttribute('data-scrub-preview')) {
            thumbnail.remove();
            thumbnail = null;
        }
        if (!thumbnail) {
            thumbnail = document.createElement('div');
            thumbnail.className = 'thumbnail';
            thumbnail.setAttribute('data-scrub-preview', 'true');
            thumbnail.innerHTML = '<div class="thumbnail-image"></div><div class="thumbnail-time"></div>';
            el.appendChild(thumbnail);
        } else if (!thumbnail.querySelector('.thumbnail-image') && el.getAttribute('data-thumbnails-uri')) {
            var img = document.createElement('div');
            img.className = 'thumbnail-image';
            thumbnail.insertBefore(img, thumbnail.firstChild);
        }
        thumbnail.style.position = 'absolute';
        thumbnail.style.bottom = '30px';
        thumbnail.style.transform = 'translateX(-50%)';
        thumbnail.style.display = 'flex';
        thumbnail.style.flexDirection = 'column';
        thumbnail.style.alignItems = 'center';
        thumbnail.style.pointerEvents = 'none';
        thumbnail.style.zIndex = '100006';
        thumbnail.style.visibility = '';

        var track = el.querySelector('.seekbar-track');
        if (track && !track.querySelector('.hover')) {
            var hover = document.createElement('div');
            hover.className = 'hover';
            track.appendChild(hover);
        }
    },
    applyPreview: function (el, time, duration) {
        var pct = duration > 0 ? Math.max(0, Math.min(100, (time / duration) * 100)) : 0;
        var pctStr = pct.toFixed(4) + '%';
        var thumb = K7.SeekBar.directChild(el, 'thumb');
        var thumbnail = K7.SeekBar.directChild(el, 'thumbnail');
        // Prefer the scrub-marked pair if a live-position Blazor node reappeared.
        if (thumb && !thumb.hasAttribute('data-scrub-preview')) {
            var scrubThumb = el.querySelector(':scope > .thumb[data-scrub-preview], .thumb[data-scrub-preview]');
            if (scrubThumb) thumb = scrubThumb;
        }
        if (thumbnail && !thumbnail.hasAttribute('data-scrub-preview')) {
            var scrubThumbNail = el.querySelector('.thumbnail[data-scrub-preview]');
            if (scrubThumbNail) thumbnail = scrubThumbNail;
        }
        if (thumb) thumb.style.left = pctStr;
        if (thumbnail) {
            thumbnail.style.left = pctStr;
            var timeEl = thumbnail.querySelector('.thumbnail-time');
            if (timeEl) {
                timeEl.textContent = K7.SeekBar.formatTime(time);
                timeEl.style.marginTop = '4px';
                timeEl.style.backgroundColor = 'rgba(0,0,0,0.7)';
                timeEl.style.color = '#fff';
                timeEl.style.padding = '2px 6px';
                timeEl.style.borderRadius = '4px';
                timeEl.style.whiteSpace = 'nowrap';
                timeEl.style.fontSize = '12px';
            }
            var img = thumbnail.querySelector('.thumbnail-image');
            var uri = el.getAttribute('data-thumbnails-uri');
            if (img && uri) {
                var interval = parseInt(el.getAttribute('data-thumb-interval') || '30', 10);
                var perRow = parseInt(el.getAttribute('data-thumbs-per-row') || '10', 10);
                var tw = parseInt(el.getAttribute('data-thumb-width') || '320', 10);
                var th = parseInt(el.getAttribute('data-thumb-height') || '180', 10);
                var index = Math.floor(time / interval);
                var col = index % perRow;
                var row = Math.floor(index / perRow);
                img.style.display = 'block';
                img.style.boxSizing = 'border-box';
                img.style.backgroundImage = 'url("' + uri + '")';
                img.style.backgroundPosition = '-' + (col * tw) + 'px -' + (row * th) + 'px';
                img.style.backgroundSize = (perRow * tw) + 'px auto';
                img.style.backgroundRepeat = 'no-repeat';
                img.style.width = tw + 'px';
                img.style.height = th + 'px';
                img.style.overflow = 'hidden';
                img.style.borderRadius = '4px';
                img.style.border = '1px solid #fff';
                img.style.flexShrink = '0';
            }
        }
        var hover = el.querySelector('.seekbar-track .hover');
        if (hover) hover.style.width = pctStr;
    },
    stepLocal: function (el, direction) {
        if (!el) return;
        if (!K7.SeekBar._scrub || K7.SeekBar._scrub.el !== el)
            K7.SeekBar.initLocalScrub(el);

        var s = K7.SeekBar._scrub;
        if (!s) return;

        if (s.decayTimer) clearTimeout(s.decayTimer);
        s.decayTimer = setTimeout(function () {
            if (K7.SeekBar._scrub === s) s.repeatCount = 0;
        }, 400);

        s.repeatCount += 1;
        var step = K7.SeekBar.getStep(s.repeatCount);
        if (direction < 0) s.time = Math.max(0, s.time - step);
        else s.time = Math.min(s.duration, s.time + step);

        el.setAttribute('data-scrub-time', String(s.time));
        // Hide any Blazor live-position pair that re-rendered after our last cleanup.
        K7.SeekBar.hideLivePositionPreviews(el);
        K7.SeekBar.ensurePreview(el);
        K7.SeekBar.applyPreview(el, s.time, s.duration);
    },
    hideLivePositionPreviews: function (el) {
        if (!el || !el.children) return;
        for (var i = 0; i < el.children.length; i++) {
            var child = el.children[i];
            if (!child || !child.classList) continue;
            if ((child.classList.contains('thumb') || child.classList.contains('thumbnail'))
                && !child.hasAttribute('data-scrub-preview')) {
                child.style.display = 'none';
                child.style.visibility = 'hidden';
            }
        }
    }
};

K7.hideVideoControlsOverlay = function () {
    if (window.K7 && K7.tvDpadHoldStop) K7.tvDpadHoldStop(false);
    var overlay = document.querySelector('.video-controls-overlay');
    if (!overlay) return;
    overlay.classList.remove('seekbar-scrubbing', 'controls-visible');
    overlay.classList.add('controls-hidden');
    var seekbar = overlay.querySelector('.seekbar-container');
    if (seekbar && window.K7 && K7.SeekBar)
        K7.SeekBar.clearLocalScrub(seekbar);
    try { overlay.focus({ preventScroll: true }); } catch (ex) { }
    if (window.SpatialNavigation) SpatialNavigation.resume();
};

// Synced from VideoPlayerControlsOverlay (SkipBackSeconds / SkipForwardSeconds prefs).
K7.setTvSkipSeconds = function (backSeconds, forwardSeconds) {
    window.K7 = window.K7 || {};
    K7.tvSkipBackSeconds = Math.max(1, parseInt(backSeconds, 10) || 10);
    K7.tvSkipForwardSeconds = Math.max(1, parseInt(forwardSeconds, 10) || 10);
};

K7.getTvSkipDelta = function (dir) {
    var back = (window.K7 && K7.tvSkipBackSeconds) || 10;
    var fwd = (window.K7 && K7.tvSkipForwardSeconds) || 10;
    return dir < 0 ? -back : fwd;
};

K7.scrubSeekBar = function (direction) {
    // Prefer beginSeekBarScrub (OnEditStart once + stepLocal).
    if (window.K7 && K7.beginSeekBarScrub) {
        K7.beginSeekBarScrub(direction);
        return;
    }
    var seekbar = document.querySelector('.video-controls-overlay .seekbar-container');
    if (!seekbar) return;
    K7.SeekBar.stepLocal(seekbar, direction);
};

K7.beginSeekBarScrub = function (direction) {
    var overlay = document.querySelector('.video-controls-overlay');
    var seekbar = overlay && overlay.querySelector('.seekbar-container');
    if (!seekbar) {
        return;
    }

    var starting = !seekbar.hasAttribute('data-sn-editing');

    // Force controls visible immediately (Blazor StateHasChanged is async).
    if (overlay) {
        overlay.classList.add('seekbar-scrubbing');
        overlay.classList.remove('controls-hidden');
        overlay.classList.add('controls-visible');
    }

    try { seekbar.focus({ preventScroll: true }); } catch (ex) { }

    var inst = K7.SeekBar._instances.get(seekbar);

    if (starting) {
        if (window.SpatialNav && window.SpatialNav.startEditing)
            window.SpatialNav.startEditing(seekbar);
        else {
            seekbar.setAttribute('data-sn-editing', 'true');
            if (window.SpatialNavigation) SpatialNavigation.pause();
            seekbar.dispatchEvent(new CustomEvent('sn:editstart', { bubbles: false }));
        }
        // OnEditStart once per scrub session - never on every key repeat (kills Blazor batches).
        try {
            if (inst && inst.dotNetRef) {
                if (inst.dotNetRef.invokeMethodAsync) inst.dotNetRef.invokeMethodAsync('OnEditStart');
                else if (inst.dotNetRef.invokeMethod) inst.dotNetRef.invokeMethod('OnEditStart');
            }
        } catch (ex) { }
    } else if (!K7.SeekBar._scrub || K7.SeekBar._scrub.el !== seekbar) {
        K7.SeekBar.initLocalScrub(seekbar);
    }

    K7.SeekBar.stepLocal(seekbar, direction);
};

document.addEventListener('DOMContentLoaded', function () { SpatialNav.init(); });
if (document.readyState !== 'loading') { SpatialNav.init(); }
