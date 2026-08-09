var MAX_DOT_SLOTS = 7;

export function init(rootElement) {
    if (!rootElement || rootElement.__vcarousel) return;

    var viewportNode = rootElement.querySelector('[data-vcarousel-viewport]');
    if (!viewportNode) return;

    var containerNode = viewportNode.firstElementChild;
    if (!containerNode) return;

    var paginationNode = rootElement.querySelector('[data-vcarousel-pagination]');
    var dotsNode = rootElement.querySelector('[data-vcarousel-dots]');
    var chevronUpNode = rootElement.querySelector('[data-vcarousel-chevron-up]');
    var chevronDownNode = rootElement.querySelector('[data-vcarousel-chevron-down]');
    var currentIndex = 0;
    var scrollAnim = null;
    var lastFocusedPerSlide = {};
    var resizeObserver = null;
    var mutationObserver = null;
    var relayoutTimer = null;

    function easeOutCubic(t) {
        return 1 - Math.pow(1 - t, 3);
    }

    function smoothScrollTo(targetY, duration) {
        if (scrollAnim) cancelAnimationFrame(scrollAnim);
        var startY = viewportNode.scrollTop;
        var diff = targetY - startY;
        if (Math.abs(diff) < 1) {
            scrollAnim = null;
            return;
        }
        var startTime = null;
        duration = duration || 280;

        function step(timestamp) {
            if (!startTime) startTime = timestamp;
            var elapsed = timestamp - startTime;
            var progress = Math.min(elapsed / duration, 1);
            viewportNode.scrollTop = startY + diff * easeOutCubic(progress);
            if (progress < 1) {
                scrollAnim = requestAnimationFrame(step);
            } else {
                scrollAnim = null;
                viewportNode.scrollTop = targetY;
            }
        }
        scrollAnim = requestAnimationFrame(step);
    }

    function clearSlideStyles(slide) {
        slide.style.maskImage = '';
        slide.style.webkitMaskImage = '';
        slide.style.opacity = '';
    }

    function setReady(isReady) {
        if (isReady) {
            rootElement.setAttribute('data-vcarousel-ready', '');
        } else {
            rootElement.removeAttribute('data-vcarousel-ready');
        }
    }

    function normalizeSlideHeights() {
        var slides = containerNode.children;
        if (resizeObserver) {
            resizeObserver.disconnect();
        }

        for (var i = 0; i < slides.length; i++) {
            slides[i].style.minHeight = '';
        }

        var maxH = 0;
        for (var j = 0; j < slides.length; j++) {
            maxH = Math.max(maxH, slides[j].offsetHeight);
        }

        for (var k = 0; k < slides.length; k++) {
            slides[k].style.minHeight = maxH > 0 ? maxH + 'px' : '';
        }

        viewportNode.style.height = maxH > 0 ? maxH + 'px' : '';

        requestAnimationFrame(function () {
            if (resizeObserver && rootElement.__vcarousel) {
                resizeObserver.observe(containerNode);
            }
        });

        return maxH;
    }

    function buildDotModel(count, activeIdx) {
        if (count <= 1) return [];

        if (count <= MAX_DOT_SLOTS) {
            var all = [];
            for (var i = 0; i < count; i++) {
                all.push({ type: 'dot', index: i, active: i === activeIdx });
            }
            return all;
        }

        var topOverflow = false;
        var bottomOverflow = false;
        var start;
        var end;

        var bothBarsSize = MAX_DOT_SLOTS - 2;
        start = activeIdx - Math.floor(bothBarsSize / 2);
        end = start + bothBarsSize - 1;

        if (start < 0) {
            start = 0;
            end = bothBarsSize - 1;
        }
        if (end >= count) {
            end = count - 1;
            start = count - bothBarsSize;
        }

        topOverflow = start > 0;
        bottomOverflow = end < count - 1;

        if (!topOverflow && bottomOverflow) {
            var oneBarSize = MAX_DOT_SLOTS - 1;
            start = 0;
            end = Math.min(count - 1, oneBarSize - 1);
            bottomOverflow = end < count - 1;
        } else if (topOverflow && !bottomOverflow) {
            var oneBarSizeEnd = MAX_DOT_SLOTS - 1;
            end = count - 1;
            start = Math.max(0, count - oneBarSizeEnd);
            topOverflow = start > 0;
        }

        var model = [];
        if (topOverflow) {
            model.push({ type: 'overflow' });
        }
        for (var j = start; j <= end; j++) {
            model.push({ type: 'dot', index: j, active: j === activeIdx });
        }
        if (bottomOverflow) {
            model.push({ type: 'overflow' });
        }
        return model;
    }

    function updatePagination(activeIdx) {
        if (!paginationNode || !dotsNode) return;

        var count = containerNode.children.length;
        var model = buildDotModel(count, activeIdx);

        if (model.length === 0) {
            paginationNode.hidden = true;
            dotsNode.replaceChildren();
            if (chevronUpNode) chevronUpNode.hidden = true;
            if (chevronDownNode) chevronDownNode.hidden = true;
            return;
        }

        paginationNode.hidden = false;

        if (chevronUpNode) {
            chevronUpNode.hidden = activeIdx <= 0;
        }
        if (chevronDownNode) {
            chevronDownNode.hidden = activeIdx >= count - 1;
        }

        var existing = dotsNode.children;
        var needRebuild = existing.length !== model.length;
        if (!needRebuild) {
            for (var i = 0; i < model.length; i++) {
                var isOverflow = existing[i].classList.contains('vertical-carousel__dot--overflow');
                if ((model[i].type === 'overflow') !== isOverflow) {
                    needRebuild = true;
                    break;
                }
            }
        }

        if (needRebuild) {
            var frag = document.createDocumentFragment();
            for (var k = 0; k < model.length; k++) {
                var el = document.createElement('span');
                el.className = 'vertical-carousel__dot';
                if (model[k].type === 'overflow') {
                    el.classList.add('vertical-carousel__dot--overflow');
                } else if (model[k].active) {
                    el.classList.add('vertical-carousel__dot--active');
                }
                frag.appendChild(el);
            }
            dotsNode.replaceChildren(frag);
            return;
        }

        for (var n = 0; n < model.length; n++) {
            existing[n].classList.toggle(
                'vertical-carousel__dot--active',
                model[n].type === 'dot' && model[n].active);
        }
    }

    function updateSlides(activeIdx) {
        var slides = containerNode.children;
        for (var i = 0; i < slides.length; i++) {
            clearSlideStyles(slides[i]);
        }
        currentIndex = activeIdx;
        updatePagination(activeIdx);
    }

    function scrollToSlide(idx, instant) {
        var slides = containerNode.children;
        if (idx < 0 || idx >= slides.length) return;
        var slide = slides[idx];
        var targetY = slide.offsetTop;
        if (instant) {
            if (scrollAnim) cancelAnimationFrame(scrollAnim);
            scrollAnim = null;
            viewportNode.scrollTop = targetY;
        } else {
            smoothScrollTo(targetY, 280);
        }
        updateSlides(idx);
    }

    function getSlideIndex(target) {
        if (!containerNode) return -1;
        var node = target;
        while (node && node !== containerNode && node !== rootElement) {
            if (node.parentElement === containerNode) {
                var children = containerNode.children;
                for (var i = 0; i < children.length; i++) {
                    if (children[i] === node) return i;
                }
                return -1;
            }
            node = node.parentElement;
        }
        return -1;
    }

    function onFocusIn(e) {
        var idx = getSlideIndex(e.target);
        if (idx >= 0) {
            lastFocusedPerSlide[idx] = e.target;
            if (idx !== currentIndex) {
                scrollToSlide(idx);
            }
        }
    }

    function onKeyDown(e) {
        if (e.key !== 'ArrowUp' && e.key !== 'ArrowDown') return;

        var idx = getSlideIndex(e.target);
        if (idx < 0) return;

        var targetIdx = e.key === 'ArrowUp' ? idx - 1 : idx + 1;
        var slides = containerNode.children;

        if (targetIdx < 0) {
            return;
        }

        if (targetIdx >= slides.length) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        e.preventDefault();
        e.stopPropagation();

        var target = lastFocusedPerSlide[targetIdx];
        if (!target || !target.isConnected) {
            target = slides[targetIdx].querySelector('.focusable');
        }
        if (target) {
            target.focus({ preventScroll: true });
        }
    }

    rootElement.addEventListener('focusin', onFocusIn, true);
    rootElement.addEventListener('keydown', onKeyDown, true);

    function relayoutViewport(instant) {
        if (scrollAnim && instant) {
            return;
        }

        containerNode.style.paddingTop = '';
        containerNode.style.paddingBottom = '';
        var slides = containerNode.children;
        if (slides.length === 0) {
            viewportNode.style.height = '';
            updatePagination(0);
            setReady(false);
            return;
        }

        // Size the viewport before revealing siblings to avoid a multi-row flash.
        var needsReveal = !rootElement.hasAttribute('data-vcarousel-ready');
        normalizeSlideHeights();
        if (needsReveal) {
            setReady(true);
            normalizeSlideHeights();
        }

        var idx = Math.min(Math.max(currentIndex, 0), slides.length - 1);
        scrollToSlide(idx, instant);
    }

    function layoutSlides() {
        relayoutViewport(false);
    }

    function scheduleRelayout(instant) {
        if (relayoutTimer) {
            clearTimeout(relayoutTimer);
            relayoutTimer = null;
        }

        // Child carousels remount skeleton->content without changing slide count;
        // debounce so Blazor multi-pass renders coalesce into one measure.
        relayoutTimer = setTimeout(function () {
            relayoutTimer = null;
            if (scrollAnim && instant) return;
            relayoutViewport(!!instant);
        }, 50);
    }

    function onWindowResize() {
        if (scrollAnim) return;
        relayoutViewport(true);
    }

    window.addEventListener('resize', onWindowResize);

    resizeObserver = typeof ResizeObserver !== 'undefined'
        ? new ResizeObserver(function () {
            if (scrollAnim) return;
            scheduleRelayout(true);
        })
        : null;

    if (resizeObserver) {
        resizeObserver.observe(containerNode);
    }

    mutationObserver = typeof MutationObserver !== 'undefined'
        ? new MutationObserver(function () {
            if (scrollAnim) return;
            scheduleRelayout(true);
        })
        : null;

    if (mutationObserver) {
        mutationObserver.observe(containerNode, { childList: true });
    }

    requestAnimationFrame(function () {
        requestAnimationFrame(layoutSlides);
    });

    rootElement.__vcarousel = {
        currentIndex: function () { return currentIndex; },
        scrollTo: scrollToSlide,
        refresh: layoutSlides,
        scheduleRefresh: function () { scheduleRelayout(true); },
        cleanup: function () {
            rootElement.removeEventListener('focusin', onFocusIn, true);
            rootElement.removeEventListener('keydown', onKeyDown, true);
            window.removeEventListener('resize', onWindowResize);
            if (relayoutTimer) clearTimeout(relayoutTimer);
            if (scrollAnim) cancelAnimationFrame(scrollAnim);
            if (resizeObserver) resizeObserver.disconnect();
            if (mutationObserver) mutationObserver.disconnect();
        }
    };
}

export function scrollNext(rootElement) {
    if (!rootElement || !rootElement.__vcarousel) return;
    var containerNode = rootElement.querySelector('[data-vcarousel-viewport]').firstElementChild;
    var idx = Math.min(rootElement.__vcarousel.currentIndex() + 1, containerNode.children.length - 1);
    rootElement.__vcarousel.scrollTo(idx, false);
}

export function scrollPrev(rootElement) {
    if (!rootElement || !rootElement.__vcarousel) return;
    var idx = Math.max(rootElement.__vcarousel.currentIndex() - 1, 0);
    rootElement.__vcarousel.scrollTo(idx, false);
}

export function scrollTo(rootElement, index) {
    if (!rootElement || !rootElement.__vcarousel) return;
    rootElement.__vcarousel.scrollTo(index, true);
}

export function reInit(rootElement) {
    destroy(rootElement);
    init(rootElement);
}

export function refresh(rootElement) {
    if (rootElement?.__vcarousel?.scheduleRefresh) {
        rootElement.__vcarousel.scheduleRefresh();
        return;
    }

    if (rootElement?.__vcarousel?.refresh) {
        rootElement.__vcarousel.refresh();
    }
}

export function getSlideCount(rootElement) {
    if (!rootElement) return 0;
    var viewportNode = rootElement.querySelector('[data-vcarousel-viewport]');
    if (!viewportNode?.firstElementChild) return 0;
    return viewportNode.firstElementChild.children.length;
}

export function destroy(rootElement) {
    if (rootElement && rootElement.__vcarousel) {
        rootElement.__vcarousel.cleanup();
        delete rootElement.__vcarousel;
        rootElement.removeAttribute('data-vcarousel-ready');
    }
}
