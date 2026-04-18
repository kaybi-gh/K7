/**
 * Spatial Navigation for keyboard/remote control (TV D-pad).
 *
 * Arrow keys move focus:
 *  - Left/Right: within the same row, or cross from sidebar to content
 *  - Up/Down: within sidebar column, or between content rows
 *
 * Rows: [data-nav-row] or [data-carousel]
 * Columns: [data-nav-column] (sidebar)
 */
var SpatialNavigation = (function () {

    var _initialized = false;

    var FOCUSABLE = 'a[href]:not([tabindex="-1"]), button:not([disabled]):not([tabindex="-1"]):not([data-carousel-prev]):not([data-carousel-next]), input:not([type="hidden"]):not([disabled]):not([tabindex="-1"]), textarea:not([disabled]):not([tabindex="-1"]), select:not([disabled]):not([tabindex="-1"]), [tabindex="0"]';

    // Returns the focusable buttons that are direct items of a .k7-menu-dropdown,
    // excluding any buttons that live inside a nested child .k7-menu-dropdown.
    function getDirectDropdownItems(dropdown) {
        var all = Array.from(dropdown.querySelectorAll('button:not([disabled])'));
        return all.filter(function (el) {
            var p = el.parentElement;
            while (p && p !== dropdown) {
                if (p.classList.contains('k7-menu-dropdown')) return false;
                p = p.parentElement;
            }
            return true;
        });
    }

    function getRows() {
        return Array.from(document.querySelectorAll('[data-nav-row], [data-carousel], [data-nav-grid]'));
    }

    function getColumn() {
        return document.querySelector('[data-nav-column]');
    }

    function isInColumn(el) {
        return !!el.closest('[data-nav-column]');
    }

    function getContentFocusables(container, includeCarouselItems) {
        var all = Array.from(container.querySelectorAll(FOCUSABLE));
        return all.filter(function (el) {
            if (el.closest('[data-carousel-prev], [data-carousel-next]')) return false;
            if (includeCarouselItems && el.closest('[data-carousel-item]')) return true;
            return el.offsetParent !== null;
        });
    }

    function getRowFocusables(row) {
        var isCarousel = row && row.hasAttribute('data-carousel');
        return getContentFocusables(row, isCarousel);
    }

    function getColumnFocusables() {
        var col = getColumn();
        if (!col) return [];
        return getContentFocusables(col, false);
    }

    function getAllFocusables() {
        return getContentFocusables(document, false);
    }

    function getRect(el) {
        return el.getBoundingClientRect();
    }

    function findContainingRow(el) {
        return el.closest('[data-nav-row], [data-carousel], [data-nav-grid]');
    }

    function resolveFocusable(el) {
        var current = el;
        while (current && current !== document.body) {
            if (current.matches && current.matches(FOCUSABLE)) return current;
            current = current.parentElement;
        }
        return el;
    }

    function scrollCarouselToElement(el) {
        var carouselRoot = el.closest('[data-carousel]');
        if (!carouselRoot || !carouselRoot.__embla) return;
        var item = el.closest('[data-carousel-item]');
        if (!item) return;
        var items = Array.from(carouselRoot.querySelectorAll('[data-carousel-item]'));
        var index = items.indexOf(item);
        if (index >= 0) {
            carouselRoot.__embla.scrollTo(index);
        }
    }

    function focusAndScroll(el) {
        el.focus({ preventScroll: true });
        scrollCarouselToElement(el);

        var container = findContainingRow(el);
        var allContainers = Array.from(document.querySelectorAll('[data-nav-row], [data-carousel], [data-nav-grid]'));
        
        if (allContainers.length > 0 && container === allContainers[0]) {
            // Remonter tout en haut de la page
            var mainContent = document.querySelector('.app-main');
            if (mainContent) {
                mainContent.scrollTo({ top: 0, behavior: 'smooth' });
            }
            window.scrollTo({ top: 0, behavior: 'smooth' });
            
            // S'assurer que ça scrolle horizontalement si besoin
            var rect = el.getBoundingClientRect();
            if (rect.left < 0 || rect.right > window.innerWidth) {
                el.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
            }
        } else {
            el.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
        }
    }

    // Find the closest item vertically in a list of candidates
    function findClosestVertically(candidates, refRect) {
        var refCenterY = refRect.top + refRect.height / 2;
        var best = null;
        var bestDist = Infinity;
        for (var i = 0; i < candidates.length; i++) {
            var r = getRect(candidates[i]);
            if (r.width === 0 && r.height === 0) continue;
            var centerY = r.top + r.height / 2;
            var dist = Math.abs(centerY - refCenterY);
            if (dist < bestDist) {
                bestDist = dist;
                best = candidates[i];
            }
        }
        return best;
    }

    // Find the closest item horizontally in a list of candidates
    function findClosestHorizontally(candidates, refRect) {
        var refCenterX = refRect.left + refRect.width / 2;
        var best = null;
        var bestDist = Infinity;
        for (var i = 0; i < candidates.length; i++) {
            var r = getRect(candidates[i]);
            if (r.width === 0 && r.height === 0) continue;
            var centerX = r.left + r.width / 2;
            var dist = Math.abs(centerX - refCenterX);
            if (dist < bestDist) {
                bestDist = dist;
                best = candidates[i];
            }
        }
        return best;
    }

    function findClosest2D(candidates, refRect, directionDown) {
        var refCenterX = refRect.left + refRect.width / 2;
        var refCenterY = refRect.top + refRect.height / 2;
        var best = null;
        var bestDist = Infinity;
        for (var i = 0; i < candidates.length; i++) {
            var r = getRect(candidates[i]);
            if (r.width === 0 && r.height === 0) continue;
            var centerX = r.left + r.width / 2;
            var centerY = r.top + r.height / 2;
            
            // Only consider items strictly below if moving down, or strictly above if moving up
            if (directionDown === true && centerY < refCenterY + 5) continue;
            if (directionDown === false && centerY > refCenterY - 5) continue;

            // X distance is penalized heavily to jump strictly vertical where possible
            var dx = Math.abs(centerX - refCenterX);
            var dy = Math.abs(centerY - refCenterY);
            
            var dist = (dx * 10) + dy;
            if (dist < bestDist) {
                bestDist = dist;
                best = candidates[i];
            }
        }
        return best || candidates[0]; // fallback
    }

    var _sidebarCallback = null;

    function setSidebarCallback(dotnetRef) {
        _sidebarCallback = dotnetRef;
    }

    function notifySidebar(open) {
        if (_sidebarCallback) {
            try {
                _sidebarCallback.invokeMethodAsync('SetSidebarOpen', open);
            } catch (e) {
                _sidebarCallback = null;
            }
        }
    }

    function handleDialogNavigation(e, dialog, key) {
        var active = document.activeElement;
        
        // Ensure we only navigate within the dialog
        var focusables = Array.from(dialog.querySelectorAll(FOCUSABLE)).filter(function(el) { return el.offsetParent !== null; });
        if (focusables.length === 0) return;

        if (!active || !dialog.contains(active)) {
            // Focus the first item if focus was lost or not yet in dialog
            focusAndScroll(focusables[0]);
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        e.preventDefault();
        e.stopImmediatePropagation();

        var refRect = getRect(active);
        var refCenterX = refRect.left + refRect.width / 2;
        var refCenterY = refRect.top + refRect.height / 2;
        
        var best = null;
        var bestDist = Infinity;
        
        for (var i = 0; i < focusables.length; i++) {
            if (focusables[i] === active) continue;
            var r = getRect(focusables[i]);
            if (r.width === 0 && r.height === 0) continue;
            
            var cx = r.left + r.width / 2;
            var cy = r.top + r.height / 2;
            
            if (key === 'ArrowDown' && cy <= refCenterY + 2) continue;
            if (key === 'ArrowUp' && cy >= refCenterY - 2) continue;
            if (key === 'ArrowRight' && cx <= refCenterX + 2) continue;
            if (key === 'ArrowLeft' && cx >= refCenterX - 2) continue;
            
            var dx = Math.abs(cx - refCenterX);
            var dy = Math.abs(cy - refCenterY);
            
            // Prioritize correct axis
            var dist;
            if (key === 'ArrowUp' || key === 'ArrowDown') {
                dist = dx * 10 + dy;
            } else {
                dist = dy * 10 + dx;
            }
            
            if (dist < bestDist) {
                bestDist = dist;
                best = focusables[i];
            }
        }
        
        if (best) {
            focusAndScroll(best);
        }
    }

    function handleArrowKey(e) {
        var key = e.key;
        var active = document.activeElement;
        var tag = (active && active.tagName || '').toLowerCase();

        // Pass on Blur to remove editing state
        if (!window.__snBlurAdded) {
            window.__snBlurAdded = true;
            document.addEventListener('blur', function(ev) {
                if (ev.target && ev.target.hasAttribute && ev.target.hasAttribute('data-sn-editing')) {
                    ev.target.removeAttribute('data-sn-editing');
                }
            }, true);
        }

        // Handle Edit mode toggling for inputs
        if (key === 'Enter' || key === 'Escape') {
            var type = active ? active.getAttribute('type') || 'text' : '';
            var isTextLike = (tag === 'textarea') || (tag === 'input' && ['text', 'password', 'search', 'email', 'number', 'tel', 'url'].indexOf(type.toLowerCase()) !== -1);
            
            if (isTextLike && !active.hasAttribute('readonly') && !active.disabled) {
                if (key === 'Enter') {
                    if (active.hasAttribute('data-sn-editing')) {
                        active.removeAttribute('data-sn-editing');
                        e.preventDefault();
                        e.stopImmediatePropagation();
                    } else {
                        active.setAttribute('data-sn-editing', 'true');
                        e.preventDefault();
                        e.stopImmediatePropagation();
                    }
                    return;
                } else if (key === 'Escape') {
                    if (active.hasAttribute('data-sn-editing')) {
                        active.removeAttribute('data-sn-editing');
                        e.preventDefault();
                        e.stopImmediatePropagation();
                        return;
                    }
                }
            }
        }

        // Video overlay: Escape/Back closes the deepest open K7 menu level
        var isEscapeKey = key === 'Escape' || key === 'GoBack' || key === 'BrowserBack';
        if (isEscapeKey && document.querySelector('.video-controls-overlay')) {
            // If a dialog is open, let Blazor handle it
            if (document.querySelector('.k7-dialog-backdrop')) return;
            if (window.K7._skipNextEscape) {
                window.K7._skipNextEscape = false;
                return;
            }
            var openMenus = Array.from(document.querySelectorAll('.k7-menu--open'));
            if (openMenus.length > 0) {
                e.preventDefault();
                e.stopImmediatePropagation();
                if (openMenus.length > 1) {
                    // A sub-menu is open: close only the deepest level and return focus to its trigger
                    var deepestMenu = openMenus[openMenus.length - 1];
                    var subTrigger = deepestMenu.querySelector(':scope > button');
                    if (subTrigger) {
                        subTrigger.click(); // toggles it closed
                        setTimeout(function() { subTrigger.focus(); }, 50);
                    }
                } else {
                    // Only the root menu is open: close all via Blazor
                    if (window.K7._videoOverlayRef) {
                        window.K7._videoOverlayRef.invokeMethodAsync('CloseMenu');
                    }
                }
                return;
            }
            // No menu open: prevent default, let Blazor handler manage
            e.preventDefault();
            return;
        }

        // Video overlay: Enter activates focused K7 menu item
        if (key === 'Enter' && document.querySelector('.video-controls-overlay')) {
            var openMenus = Array.from(document.querySelectorAll('.k7-menu--open'));
            for (var p = 0; p < openMenus.length; p++) {
                var dropdown = openMenus[p].querySelector('.k7-menu-dropdown');
                if (dropdown && dropdown.contains(active)) {
                    var prevOpenCount = document.querySelectorAll('.k7-menu--open').length;
                    active.click();
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    // If this click opened a sub-menu, focus its first item
                    setTimeout(function() {
                        var newOpenMenus = Array.from(document.querySelectorAll('.k7-menu--open'));
                        if (newOpenMenus.length > prevOpenCount) {
                            var deepest = newOpenMenus[newOpenMenus.length - 1];
                            var subDd = deepest.querySelector(':scope > .k7-menu-dropdown');
                            if (subDd) {
                                var first = getDirectDropdownItems(subDd)[0];
                                if (first) first.focus();
                            }
                        }
                    }, 150);
                    return;
                }
            }
            // No popover open: explicitly click the focused button/interactive element.
            // MAUI Android TV WebView doesn't synthesize click from Enter after
            // programmatic focus (focusDirection), so we must do it manually.
            // But when focus is on the overlay container itself, prevent click synthesis
            // to avoid OnOverlayTap immediately undoing what the Blazor handler did.
            if (active && active.classList.contains('video-controls-overlay')) {
                e.preventDefault();
                return;
            }
            if (active && active.closest('.video-controls-overlay') && !active.closest('.seekbar-container')) {
                var tag = (active.tagName || '').toLowerCase();
                if (tag === 'button' || tag === 'a' || active.getAttribute('role') === 'button'
                    || active.classList.contains('k7-menu-item')) {
                    var prevOpenCount = document.querySelectorAll('.k7-menu--open').length;
                    active.click();
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    // After clicking a button that may open a menu, wait for Blazor to re-render
                    // then focus the first item of the newly opened (deepest) dropdown.
                    setTimeout(function() {
                        var newOpenMenus = Array.from(document.querySelectorAll('.k7-menu--open'));
                        if (newOpenMenus.length > prevOpenCount) {
                            var deepest = newOpenMenus[newOpenMenus.length - 1];
                            var dropdown = deepest.querySelector(':scope > .k7-menu-dropdown');
                            if (dropdown) {
                                var first = getDirectDropdownItems(dropdown)[0];
                                if (first) first.focus();
                            }
                        }
                    }, 150);
                    return;
                }
            }
        }

        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(key) === -1) {
            if (key === ' ' && document.querySelector('.video-controls-overlay')) {
                e.preventDefault();
            }
            return;
        }

        // If a K7 menu is open, navigate within it
        if (document.querySelector('.k7-menu--open')) {
            if (document.querySelector('.video-controls-overlay')) {
                var openMenus = Array.from(document.querySelectorAll('.k7-menu--open'));

                // Find the deepest open dropdown that directly contains the active element.
                var activeDropdown = null;
                var activeMenuRoot = null;
                for (var pi = openMenus.length - 1; pi >= 0; pi--) {
                    var dd = openMenus[pi].querySelector(':scope > .k7-menu-dropdown');
                    if (dd && dd.contains(active)) {
                        activeDropdown = dd;
                        activeMenuRoot = openMenus[pi];
                        break;
                    }
                }

                if (activeDropdown) {
                    var items = getDirectDropdownItems(activeDropdown);
                    var idx = items.indexOf(active);

                    // ArrowUp / ArrowDown: move within the current dropdown level
                    var target = null;
                    if (key === 'ArrowDown') {
                        target = idx < items.length - 1 ? items[idx + 1] : items[0];
                    } else if (key === 'ArrowUp') {
                        target = idx > 0 ? items[idx - 1] : items[items.length - 1];
                    }
                    if (target) {
                        target.focus();
                        e.preventDefault();
                        e.stopImmediatePropagation();
                    }
                } else if (key === 'ArrowDown' || key === 'ArrowUp') {
                    // Menu is open but focus is not inside any dropdown (e.g. still on trigger).
                    var deepest = openMenus[openMenus.length - 1];
                    var deepestDropdown = deepest.querySelector(':scope > .k7-menu-dropdown');
                    if (deepestDropdown) {
                        var first = getDirectDropdownItems(deepestDropdown)[0];
                        if (first) {
                            first.focus();
                            e.preventDefault();
                            e.stopImmediatePropagation();
                        }
                    }
                }
            }
            return;
        }

        // If the video player overlay is active, prevent arrow default (scrolling)
        // but let Blazor handle the key logic
        if (document.querySelector('.video-controls-overlay')) {
            e.preventDefault();
            return;
        }

        // Check if we are currently editing an input. If so, let native behavior execute.
        if (tag === 'input' || tag === 'textarea') {
            var type = active ? active.getAttribute('type') || 'text' : '';
            var isTextLike = (tag === 'textarea') || (tag === 'input' && ['text', 'password', 'search', 'email', 'number', 'tel', 'url'].indexOf(type.toLowerCase()) !== -1);
            
            // Only strictly text inputs can have this Edit Mode concept
            if (isTextLike && active.hasAttribute('data-sn-editing')) {
                return; // Let native cursor movement happen
            }
            
            // We just let the rest of spatial-navigation run if not in edit mode
            e.preventDefault(); 
        }

        // If a dialog is open, constrain navigation inside it
        var openDialogs = document.querySelectorAll('.k7-dialog-backdrop');
        if (openDialogs.length > 0) {
            var activeDialog = openDialogs[openDialogs.length - 1].querySelector('.k7-dialog-paper');
            handleDialogNavigation(e, activeDialog, key);
            return;
        }

        // Don't navigate behind other non-dialog overlays (if any)
        if (document.querySelector('.k7-dialog-backdrop') && openDialogs.length === 0) return;

        var active = document.activeElement;
        if (!active || active === document.body) {
            // Nothing focused - focus first content element (not sidebar)
            var contentItems = getAllFocusables().filter(function (el) { return !isInColumn(el); });
            if (contentItems.length > 0) {
                focusAndScroll(contentItems[0]);
            }
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        active = resolveFocusable(active);
        var inSidebar = isInColumn(active);
        var row = !inSidebar ? findContainingRow(active) : null;

        e.preventDefault();
        e.stopImmediatePropagation();

        if (inSidebar) {
            handleSidebar(active, key);
        } else if (key === 'ArrowLeft' || key === 'ArrowRight') {
            handleHorizontal(active, row, key === 'ArrowRight');
        } else {
            handleVertical(active, row, key === 'ArrowDown');
        }
    }

    function handleSidebar(active, key) {
        var items = getColumnFocusables();
        var idx = items.indexOf(active);
        if (idx === -1) {
            for (var i = 0; i < items.length; i++) {
                if (items[i].contains(active) || active.contains(items[i])) {
                    idx = i;
                    break;
                }
            }
        }

        if (key === 'ArrowUp') {
            if (idx > 0) items[idx - 1].focus({ preventScroll: false });
        } else if (key === 'ArrowDown') {
            if (idx < items.length - 1) items[idx + 1].focus({ preventScroll: false });
        } else if (key === 'ArrowRight') {
            // Jump from sidebar to the nearest content row
            notifySidebar(false);
            jumpToContent(active);
        }
        // ArrowLeft in sidebar: do nothing (already at left edge)
    }

    function jumpToContent(sidebarEl) {
        var rows = getRows();
        if (rows.length === 0) {
            // No rows - jump to first non-sidebar focusable
            var all = getAllFocusables().filter(function (el) { return !isInColumn(el); });
            if (all.length > 0) focusAndScroll(all[0]);
            return;
        }

        // Find the row closest to the sidebar element's vertical position
        var refRect = getRect(sidebarEl);
        var bestRow = null;
        var bestDist = Infinity;
        for (var i = 0; i < rows.length; i++) {
            var r = getRect(rows[i]);
            if (r.width === 0 && r.height === 0) continue;
            var centerY = r.top + r.height / 2;
            var dist = Math.abs(centerY - (refRect.top + refRect.height / 2));
            if (dist < bestDist) {
                bestDist = dist;
                bestRow = rows[i];
            }
        }

        if (bestRow) {
            var items = getRowFocusables(bestRow);
            if (items.length > 0) {
                focusAndScroll(items[0]);
                return;
            }
        }

        // Fallback
        var nonSidebar = getAllFocusables().filter(function (el) { return !isInColumn(el); });
        if (nonSidebar.length > 0) focusAndScroll(nonSidebar[0]);
    }

    function jumpToSidebar(contentEl) {
        var col = getColumn();
        if (!col) return false;
        // Target only nav links in the sidebar
        var navLinks = Array.from(col.querySelectorAll('.nav-link'));
        if (navLinks.length === 0) return false;
        var refRect = getRect(contentEl);
        var best = findClosestVertically(navLinks, refRect);
        if (best) {
            notifySidebar(true);
            best.focus({ preventScroll: false });
            return true;
        }
        return false;
    }

    function handleHorizontal(active, row, forward) {
        var isCarousel = row && row.hasAttribute('data-carousel');
        var isGrid = row && row.hasAttribute('data-nav-grid');

        if (isCarousel) {
            var currentItem = active.closest('[data-carousel-item]');
            if (!currentItem) return;

            var allItems = Array.from(row.querySelectorAll('[data-carousel-item]'));
            var currentIdx = allItems.indexOf(currentItem);
            if (currentIdx === -1) return;

            var targetIdx = forward ? currentIdx + 1 : currentIdx - 1;

            if (targetIdx < 0) {
                jumpToSidebar(active);
                return;
            }
            if (targetIdx >= allItems.length) {
                return;
            }

            var targetItem = allItems[targetIdx];
            var target = targetItem.querySelector('a[href], button:not([disabled]), [tabindex]');
            if (!target) return;

            if (row.__embla) {
                row.__embla.scrollTo(targetIdx);
            }

            setTimeout(function() {
                target.focus({ preventScroll: true });
            }, 10);
            return;
        }

        // Non-carousel row or no row: array-based navigation
        var contentFocusables = row
            ? getRowFocusables(row)
            : getAllFocusables().filter(function (el) { return !isInColumn(el); });
            
        // If it's a grid, we want horizontal movement to stay within the same visual line if possible
        if (isGrid) {
            var activeRect = getRect(active);
            var best = null;
            var bestDist = Infinity;
            for (var i = 0; i < contentFocusables.length; i++) {
                var cand = contentFocusables[i];
                if (cand === active) continue;
                var r = getRect(cand);
                // Must be roughly on the same vertical line
                if (Math.abs(r.top - activeRect.top) < 20) {
                    var dist = forward ? r.left - activeRect.right : activeRect.left - r.right;
                    if (dist > -20 && dist < bestDist) {
                        bestDist = dist;
                        best = cand;
                    }
                }
            }
            if (best) {
                focusAndScroll(best);
            } else if (!forward) {
                // If moving left and no item found on same line, jump to sidebar
                jumpToSidebar(active);
            }
            return;
        }

        var idx = findIndex(contentFocusables, active);

        if (!forward && idx <= 0) {
            // Left at leftmost → jump to sidebar
            jumpToSidebar(active);
            return;
        }

        if (idx === -1) return;

        var next = forward ? idx + 1 : idx - 1;
        if (next >= 0 && next < contentFocusables.length) {
            focusAndScroll(contentFocusables[next]);
        }
    }

    function findIndex(items, el) {
        var idx = items.indexOf(el);
        if (idx === -1) {
            for (var i = 0; i < items.length; i++) {
                if (items[i].contains(el) || el.contains(items[i])) {
                    return i;
                }
            }
        }
        return idx;
    }

    function handleVertical(active, currentRow, down) {
        var rows = getRows();
        if (rows.length === 0) {
            var all = getAllFocusables().filter(function (el) { return !isInColumn(el); });
            var idx = all.indexOf(active);
            var next = down ? idx + 1 : idx - 1;
            if (next >= 0 && next < all.length) {
                focusAndScroll(all[next]);
            }
            return;
        }

        var currentRowIndex = -1;
        if (currentRow) {
            currentRowIndex = rows.indexOf(currentRow);
        }

        if (currentRowIndex === -1) {
            // Active is outside any row - find nearest row in direction
            var activeRect = getRect(active);
            var bestRow = null;
            var bestDist = Infinity;
            for (var i = 0; i < rows.length; i++) {
                var rowRect = getRect(rows[i]);
                var dist = down
                    ? rowRect.top - activeRect.bottom
                    : activeRect.top - rowRect.bottom;
                if (dist > -20 && dist < bestDist) {
                    bestDist = dist;
                    bestRow = i;
                }
            }
            if (bestRow !== null) {
                focusClosestInRow(rows[bestRow], active, down);
            }
            return;
        }

        var isGrid = rows[currentRowIndex].hasAttribute('data-nav-grid');
        
        if (isGrid) {
            // Inside a grid, vertical movement should find the nearest item above/below visually
            var items = getRowFocusables(rows[currentRowIndex]);
            var activeRect = getRect(active);
            var activeCenterX = activeRect.left + activeRect.width / 2;
            var activeCenterY = activeRect.top + activeRect.height / 2;
            
            var best = null;
            var bestDist = Infinity;
            
            for (var i = 0; i < items.length; i++) {
                var cand = items[i];
                if (cand === active) continue;
                
                var r = getRect(cand);
                var centerX = r.left + r.width / 2;
                var centerY = r.top + r.height / 2;
                
                // Must be physically above/below based on direction
                if (down && centerY <= activeCenterY + 5) continue;
                if (!down && centerY >= activeCenterY - 5) continue;
                
                var dx = Math.abs(centerX - activeCenterX);
                var dy = Math.abs(centerY - activeCenterY);
                
                // Weight horizontal difference strongly to prefer same column
                var dist = (dx * 10) + dy;
                
                if (dist < bestDist) {
                    bestDist = dist;
                    best = cand;
                }
            }
            if (best) {
                focusAndScroll(best);
                return;
            }
            // If no item found above/below in grid, fall through to default behavior (jump to next row)
        }

        var targetRowIndex = down ? currentRowIndex + 1 : currentRowIndex - 1;
        if (targetRowIndex < 0 || targetRowIndex >= rows.length) {
            // At edge - find non-row focusables above/below
            var outsideFocusables = getAllFocusables().filter(function (el) {
                return !findContainingRow(el) && !isInColumn(el);
            });
            if (outsideFocusables.length > 0) {
                var activeRect = getRect(active);
                var best = null;
                var bestDist = Infinity;
                for (var i = 0; i < outsideFocusables.length; i++) {
                    var r = getRect(outsideFocusables[i]);
                    var dist = down
                        ? r.top - activeRect.bottom
                        : activeRect.top - r.bottom;
                    if (dist > -20 && dist < bestDist) {
                        bestDist = dist;
                        best = outsideFocusables[i];
                    }
                }
                if (best) focusAndScroll(best);
            }
            return;
        }

        focusClosestInRow(rows[targetRowIndex], active, down);
    }

    function focusClosestInRow(row, referenceEl, directionDown) {
        var items = getRowFocusables(row);
        if (items.length === 0) return;

        var refRect = getRect(referenceEl);
        var best;
        if (row.hasAttribute('data-nav-grid')) {
            best = findClosest2D(items, refRect, directionDown);
        } else {
            best = findClosestHorizontally(items, refRect);
        }
        if (!best) best = items[0];
        focusAndScroll(best);
    }

    function handleBackNavigation(e) {
        var key = e.key;
        // Backspace and remote Back buttons navigate back
        // Escape is left to Blazor for closing dialogs/popups
        if (key !== 'Backspace' && key !== 'GoBack' && key !== 'XF86Back') return;

        // Don't intercept in form fields
        var tag = (document.activeElement && document.activeElement.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea') return;

        // Video overlay with menu open: close the open K7 menu
        if (document.querySelector('.video-controls-overlay') && window.K7._videoOverlayRef) {
            var openMenus = document.querySelectorAll('.k7-menu--open');
            if (openMenus.length > 0) {
                e.preventDefault();
                e.stopImmediatePropagation();
                window.K7._videoOverlayRef.invokeMethodAsync('CloseMenu');
                return;
            }
        }

        e.preventDefault();
        e.stopImmediatePropagation();

        // Restore focus on the new page once history.back() finishes
        var previousUrl = window.location.href;
        window.history.back();

        // Very basic focus recovery mechanism. Wait until URL changes and DOM updates.
        var checkCount = 0;
        var checker = setInterval(function () {
            checkCount++;
            if (window.location.href !== previousUrl || checkCount > 10) {
                clearInterval(checker);
                setTimeout(function () {
                    // If no element currently has focus, or we lost focus completely
                    if (!document.activeElement || document.activeElement === document.body) {
                        focusFirst('[data-nav-row] ' + FOCUSABLE + ', [data-carousel] ' + FOCUSABLE + ', [data-nav-grid] ' + FOCUSABLE);
                    }
                }, 100);
            }
        }, 50);
    }

    function init() {
        if (_initialized) return;
        _initialized = true;
        document.addEventListener('keydown', handleArrowKey, true);
        document.addEventListener('keydown', handleBackNavigation);
    }

    function focusFirst(selector) {
        if (!selector) return;
        // Small delay to let Blazor finish rendering
        setTimeout(function () {
            var el = document.querySelector(selector);
            if (el) {
                var focusable = el.matches(FOCUSABLE) ? el : el.querySelector(FOCUSABLE);
                if (focusable) {
                    if (!isInColumn(focusable)) notifySidebar(false);
                    focusAndScroll(focusable);
                }
            }
        }, 100);
    }

    return { init: init, focusFirst: focusFirst, setSidebarCallback: setSidebarCallback };
})();

document.addEventListener('DOMContentLoaded', function () {
    SpatialNavigation.init();
});

if (document.readyState !== 'loading') {
    SpatialNavigation.init();
}

// Focus navigation within a container (used by video player overlay)
window.K7 = window.K7 || {};
window.K7.focusDirection = function (container, direction) {
    if (!container) return;
    var FOCUSABLE = 'button:not([disabled]):not([tabindex="-1"]), [tabindex="0"], a[href]:not([tabindex="-1"])';
    var items = Array.from(container.querySelectorAll(FOCUSABLE)).filter(function (el) {
        return el.offsetParent !== null && el !== container;
    });
    if (items.length === 0) return;

    var active = document.activeElement;
    var idx = items.indexOf(active);

    if (direction === 'first') {
        // Prefer bottom controls bar (play/pause) over close button
        var bottomBar = container.querySelector('.controls-bar.bottom');
        var target = bottomBar ? bottomBar.querySelector('button:not([disabled])') : items[0];
        (target || items[0]).focus();
    } else if (direction === 'next') {
        var next = idx < items.length - 1 ? idx + 1 : 0;
        items[next].focus();
    } else if (direction === 'prev') {
        var prev = idx > 0 ? idx - 1 : items.length - 1;
        items[prev].focus();
    }
};

window.K7.isFocusInOverlayChild = function (container) {
    if (!container || !document.activeElement) return false;
    return container.contains(document.activeElement) && document.activeElement !== container;
};

window.K7.registerVideoOverlay = function (dotNetRef) {
    window.K7._videoOverlayRef = dotNetRef;
    window.K7._skipNextEscape = false;

    // Auto-focus popover items when a K7 menu opens during video playback
    if (window.K7._popoverObserver) window.K7._popoverObserver.disconnect();

    window.K7._popoverObserver = new MutationObserver(function (mutations) {
        for (var i = 0; i < mutations.length; i++) {
            var target = mutations[i].target;
            if (!target.classList) continue;
            if (target.classList.contains('k7-menu') && target.classList.contains('k7-menu--open')) {
                var dropdown = target.querySelector('.k7-menu-dropdown');
                if (!dropdown || target.contains(document.activeElement)) continue;
                (function (menu) {
                    var attempts = 5;
                    function tryFocus() {
                        var items = Array.from(menu.querySelectorAll('.k7-menu-item')).filter(function (el) {
                            return el.offsetParent !== null;
                        });
                        if (items.length > 0) {
                            items[0].focus();
                        } else if (--attempts > 0) {
                            setTimeout(tryFocus, 50);
                        }
                    }
                    setTimeout(tryFocus, 50);
                })(dropdown);
            }
        }
    });

    window.K7._popoverObserver.observe(document.body, {
        subtree: true,
        attributes: true,
        attributeFilter: ['class']
    });
};

window.K7.unregisterVideoOverlay = function () {
    window.K7._videoOverlayRef = null;
    if (window.K7._popoverObserver) {
        window.K7._popoverObserver.disconnect();
        window.K7._popoverObserver = null;
    }
};
