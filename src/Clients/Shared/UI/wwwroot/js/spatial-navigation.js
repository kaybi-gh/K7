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

    var FOCUSABLE = 'a[href]:not([tabindex="-1"]), button:not([disabled]):not([tabindex="-1"]):not([data-carousel-prev]):not([data-carousel-next]), [tabindex="0"]';

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
            var mainContent = document.querySelector('.mud-main-content');
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
            _sidebarCallback.invokeMethodAsync('SetSidebarOpen', open);
        }
    }

    function handleArrowKey(e) {
        var key = e.key;
        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(key) === -1) {
            return;
        }

        var tag = (document.activeElement && document.activeElement.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea' || tag === 'select') return;

        // Don't navigate behind open dialogs
        if (document.querySelector('.mud-overlay-dialog')) return;

        var active = document.activeElement;
        if (!active || active === document.body) {
            // Nothing focused — focus first content element (not sidebar)
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
            // No rows — jump to first non-sidebar focusable
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
        // Target only nav links, not hamburger/search/avatar buttons
        var navLinks = Array.from(col.querySelectorAll('.mud-nav-link'));
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
            // Active is outside any row — find nearest row in direction
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
            // At edge — find non-row focusables above/below
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
        // Only Backspace and remote Back buttons navigate back
        // Escape is left to MudBlazor for closing dialogs/popups
        if (key !== 'Backspace' && key !== 'GoBack' && key !== 'XF86Back') return;

        // Don't intercept in form fields
        var tag = (document.activeElement && document.activeElement.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea') return;

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
        document.addEventListener('keydown', handleArrowKey);
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
