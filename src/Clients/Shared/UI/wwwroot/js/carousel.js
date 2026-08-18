export function init(rootElement) {
    if (!rootElement || rootElement.__embla) return;

    var viewportNode = rootElement.querySelector('[data-carousel-viewport]');
    if (!viewportNode) return;

    var container = viewportNode.querySelector('.carousel-container') || viewportNode.firstElementChild;
    function getPadStart() {
        return container ? parseInt(getComputedStyle(container).paddingInlineStart, 10) || 0 : 0;
    }

    var embla = globalThis.EmblaCarousel(viewportNode, {
        containScroll: 'trimSnaps',
        skipSnaps: true,
        align: function () { return getPadStart(); },
        slidesToScroll: 1
    });

    rootElement.__embla = embla;

    // Embla 8 auto click-guard is unreliable with Blazor <a href> handlers.
    // Block the click in capture when the pointer session scrolled the carousel.
    var pointerActive = false;
    var suppressClick = false;
    embla.on('pointerDown', function () {
        pointerActive = true;
        suppressClick = false;
    });
    embla.on('scroll', function () {
        if (pointerActive)
            suppressClick = true;
    });
    embla.on('pointerUp', function () {
        pointerActive = false;
    });
    viewportNode.addEventListener('click', function (e) {
        if (!suppressClick)
            return;

        suppressClick = false;
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();
    }, true);

    var prevBtn = rootElement.querySelector('[data-carousel-prev]');
    var nextBtn = rootElement.querySelector('[data-carousel-next]');
    var loopBackBtn = rootElement.querySelector('[data-carousel-loop-back]');

    if (prevBtn) prevBtn.addEventListener('click', function () { embla.scrollPrev(); });
    if (nextBtn) nextBtn.addEventListener('click', function () { embla.scrollNext(); });

    if (loopBackBtn) {
        var loopBackAction = loopBackBtn.querySelector('.carousel-loop-back__image') || loopBackBtn;

        function focusFirstCarouselItem() {
            // Ensure snap 0 is applied before focusing so the first card is on-screen.
            try { embla.scrollTo(0); } catch (e) { /* ignore */ }
            var firstItem = rootElement.querySelector('[data-carousel-item]:not([data-carousel-loop-back])');
            if (!firstItem) return;
            var target = firstItem.querySelector('.focusable') || firstItem;
            if (window.SpatialNav && window.SpatialNav.focusElement) {
                window.SpatialNav.focusElement(target);
            } else {
                target.focus({ preventScroll: true });
            }
        }

        function doLoopBack(fromKeyboard) {
            embla.scrollTo(0);

            // TV remotes often omit keyup; always focus first item with a fallback.
            // Defer past Embla settle so focus does not land on an off-screen slide.
            var delay = fromKeyboard ? 40 : 50;
            if (fromKeyboard && window.K7 && window.K7.suppressEnterUntilKeyUp) {
                var focused = false;
                var focusOnce = function () {
                    if (focused) return;
                    focused = true;
                    focusFirstCarouselItem();
                };
                window.K7.suppressEnterUntilKeyUp(focusOnce);
                setTimeout(focusOnce, 80);
                return;
            }

            setTimeout(focusFirstCarouselItem, delay);
        }

        loopBackAction.addEventListener('click', function (e) {
            doLoopBack(e.detail === 0);
        });
    }

    function updateArrows() {
        var canPrev = embla.canScrollPrev();
        var canNext = embla.canScrollNext();
        var hasOverflow = canPrev || canNext;

        if (prevBtn) {
            prevBtn.style.display = hasOverflow ? '' : 'none';
            prevBtn.disabled = !canPrev;
        }
        if (nextBtn) {
            nextBtn.style.display = hasOverflow ? '' : 'none';
            nextBtn.disabled = !canNext;
        }
        if (loopBackBtn) {
            var realSlides = container ? container.querySelectorAll('[data-carousel-item]:not([data-carousel-loop-back])') : [];
            var totalWidth = 0;
            var gap = container ? parseInt(getComputedStyle(container).gap) || 0 : 0;
            for (var i = 0; i < realSlides.length; i++) {
                totalWidth += realSlides[i].offsetWidth;
                if (i > 0) totalWidth += gap;
            }
            totalWidth += getPadStart() + (parseInt(getComputedStyle(container).paddingInlineEnd, 10) || 0);
            var needsLoopBack = totalWidth > viewportNode.offsetWidth;
            loopBackBtn.classList.toggle('visible', needsLoopBack);
            // Keep out of browser tab order until the control is actually shown (TV focus restore).
            var loopAction = loopBackBtn.querySelector('.carousel-loop-back__image');
            if (loopAction) loopAction.tabIndex = needsLoopBack ? 0 : -1;
        }
    }

    embla.on('init', updateArrows);
    embla.on('reInit', updateArrows);
    embla.on('select', updateArrows);
    embla.on('scroll', updateArrows);
}

export function scrollToIndex(rootElement, index) {
    if (rootElement && rootElement.__embla) {
        rootElement.__embla.scrollTo(index);
    }
}

export function reInit(rootElement) {
    if (!rootElement || !rootElement.__embla) return;

    var embla = rootElement.__embla;
    var slides = embla.slideNodes();
    var selectedIndex = embla.selectedScrollSnap();
    var atStart = selectedIndex <= 0;

    // User is browsing mid-carousel: keep the same media card in view after inserts.
    // User is at the start: stay on snap 0 so newly prepended cards (FIFO) become visible.
    var anchorId = null;
    if (!atStart) {
        for (var i = selectedIndex; i >= 0; i--) {
            var slide = slides[i];
            if (slide && !slide.hasAttribute('data-carousel-loop-back') && slide.id) {
                anchorId = slide.id;
                break;
            }
        }
    }

    embla.reInit();

    if (atStart) {
        embla.scrollTo(0, true);
        return;
    }

    if (!anchorId) return;

    var nextSlides = embla.slideNodes();
    for (var j = 0; j < nextSlides.length; j++) {
        if (nextSlides[j].id === anchorId) {
            embla.scrollTo(j, true);
            return;
        }
    }
}

export function destroy(rootElement) {
    if (rootElement && rootElement.__embla) {
        rootElement.__embla.destroy();
        delete rootElement.__embla;
    }
}
