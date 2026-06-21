export function init(rootElement) {
    if (!rootElement || rootElement.__vcarousel) return;

    var viewportNode = rootElement.querySelector('[data-vcarousel-viewport]');
    if (!viewportNode) return;

    var containerNode = viewportNode.firstElementChild;
    if (!containerNode) return;

    var currentIndex = 0;
    var scrollAnim = null;
    var lastFocusedPerSlide = {};

    // Bezier-like easing that matches Embla's feel
    function easeInOutQuart(t) {
        return t < 0.5 ? 8 * t * t * t * t : 1 - Math.pow(-2 * t + 2, 4) / 2;
    }

    function smoothScrollTo(targetY, duration) {
        if (scrollAnim) cancelAnimationFrame(scrollAnim);
        var startY = viewportNode.scrollTop;
        var diff = targetY - startY;
        if (Math.abs(diff) < 1) return;
        var startTime = null;
        duration = duration || 600;

        function step(timestamp) {
            if (!startTime) startTime = timestamp;
            var elapsed = timestamp - startTime;
            var progress = Math.min(elapsed / duration, 1);
            viewportNode.scrollTop = startY + diff * easeInOutQuart(progress);
            if (progress < 1) {
                scrollAnim = requestAnimationFrame(step);
            } else {
                scrollAnim = null;
            }
        }
        scrollAnim = requestAnimationFrame(step);
    }

    function computePadding() {
        var viewportHeight = viewportNode.clientHeight;
        var slides = containerNode.children;
        if (slides.length === 0) return;
        var firstHeight = slides[0].offsetHeight;
        var lastHeight = slides[slides.length - 1].offsetHeight;
        var topPad = Math.max(0, (viewportHeight - firstHeight) / 2);
        var bottomPad = Math.max(0, (viewportHeight - lastHeight) / 2);
        containerNode.style.paddingTop = topPad + 'px';
        containerNode.style.paddingBottom = bottomPad + 'px';
    }

    function updateSlides(activeIdx) {
        var slides = containerNode.children;
        for (var i = 0; i < slides.length; i++) {
            var diff = i - activeIdx;
            var distance = Math.abs(diff);
            if (distance === 0) {
                slides[i].style.maskImage = 'none';
                slides[i].style.webkitMaskImage = 'none';
                slides[i].style.opacity = '1';
            } else if (diff < 0) {
                slides[i].style.opacity = '0.5';
            } else {
                slides[i].style.opacity = '0.5';
            }
        }
        currentIndex = activeIdx;
    }

    function scrollToSlide(idx, instant) {
        var slides = containerNode.children;
        if (idx < 0 || idx >= slides.length) return;
        var slide = slides[idx];
        var slideTop = slide.offsetTop;
        var slideHeight = slide.offsetHeight;
        var viewportHeight = viewportNode.clientHeight;
        var targetY = slideTop - (viewportHeight - slideHeight) / 2;
        if (instant) {
            if (scrollAnim) cancelAnimationFrame(scrollAnim);
            scrollAnim = null;
            viewportNode.scrollTop = targetY;
        } else {
            smoothScrollTo(targetY, 550);
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
            // Remember last focused element in this slide
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
            // Let the event bubble so spatial navigation can move focus to the navbar
            return;
        }

        if (targetIdx >= slides.length) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        e.preventDefault();
        e.stopPropagation();

        // Restore last focused element in target slide, or fallback to first focusable
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
        computePadding();
        var slides = containerNode.children;
        if (slides.length === 0) return;
        var idx = Math.min(Math.max(currentIndex, 0), slides.length - 1);
        updateSlides(idx);
        scrollToSlide(idx, instant);
    }

    function layoutSlides() {
        relayoutViewport(false);
    }

    var resizeObserver = typeof ResizeObserver !== 'undefined'
        ? new ResizeObserver(function () {
            relayoutViewport(true);
        })
        : null;

    if (resizeObserver) {
        resizeObserver.observe(viewportNode);
    }

    requestAnimationFrame(function () {
        requestAnimationFrame(layoutSlides);
    });

    rootElement.__vcarousel = {
        currentIndex: function () { return currentIndex; },
        refresh: layoutSlides,
        cleanup: function () {
            rootElement.removeEventListener('focusin', onFocusIn, true);
            rootElement.removeEventListener('keydown', onKeyDown, true);
            if (scrollAnim) cancelAnimationFrame(scrollAnim);
            if (resizeObserver) resizeObserver.disconnect();
        }
    };
}

export function scrollNext(rootElement) {
    if (!rootElement || !rootElement.__vcarousel) return;
    var containerNode = rootElement.querySelector('[data-vcarousel-viewport]').firstElementChild;
    var idx = Math.min(rootElement.__vcarousel.currentIndex() + 1, containerNode.children.length - 1);
    var slides = containerNode.children;
    slides[idx].scrollIntoView({ behavior: 'smooth', block: 'center' });
    updateSlidesFromRoot(rootElement, idx);
}

export function scrollPrev(rootElement) {
    if (!rootElement || !rootElement.__vcarousel) return;
    var containerNode = rootElement.querySelector('[data-vcarousel-viewport]').firstElementChild;
    var idx = Math.max(rootElement.__vcarousel.currentIndex() - 1, 0);
    var slides = containerNode.children;
    slides[idx].scrollIntoView({ behavior: 'smooth', block: 'center' });
    updateSlidesFromRoot(rootElement, idx);
}

export function scrollTo(rootElement, index) {
    if (!rootElement || !rootElement.__vcarousel) return;
    var containerNode = rootElement.querySelector('[data-vcarousel-viewport]').firstElementChild;
    var slides = containerNode.children;
    if (index >= 0 && index < slides.length) {
        slides[index].scrollIntoView({ behavior: 'smooth', block: 'center' });
        updateSlidesFromRoot(rootElement, index);
    }
}

function updateSlidesFromRoot(rootElement, activeIdx) {
    var containerNode = rootElement.querySelector('[data-vcarousel-viewport]').firstElementChild;
    var slides = containerNode.children;
    for (var i = 0; i < slides.length; i++) {
        var diff = i - activeIdx;
        var distance = Math.abs(diff);
        if (distance === 0) {
            slides[i].style.maskImage = 'none';
            slides[i].style.webkitMaskImage = 'none';
            slides[i].style.opacity = '1';
        } else if (diff < 0) {
            var mask = distance === 1
                ? 'linear-gradient(to bottom, transparent 0%, black 60%)'
                : 'linear-gradient(to bottom, transparent 0%, black 90%)';
            slides[i].style.maskImage = mask;
            slides[i].style.webkitMaskImage = mask;
            slides[i].style.opacity = distance === 1 ? '0.6' : '0.2';
        } else {
            var mask = distance === 1
                ? 'linear-gradient(to top, transparent 0%, black 60%)'
                : 'linear-gradient(to top, transparent 0%, black 90%)';
            slides[i].style.maskImage = mask;
            slides[i].style.webkitMaskImage = mask;
            slides[i].style.opacity = distance === 1 ? '0.6' : '0.2';
        }
    }
}

export function reInit(rootElement) {
    destroy(rootElement);
    init(rootElement);
}

export function refresh(rootElement) {
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
    }
}
