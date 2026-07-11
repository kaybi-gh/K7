export function init(rootElement) {
    if (!rootElement || rootElement.__embla) return;

    var viewportNode = rootElement.querySelector('[data-carousel-viewport]');
    if (!viewportNode) return;

    var container = viewportNode.querySelector('.carousel-container') || viewportNode.firstElementChild;
    var padStart = container ? parseInt(getComputedStyle(container).paddingInlineStart) || 0 : 0;

    var embla = globalThis.EmblaCarousel(viewportNode, {
        containScroll: 'trimSnaps',
        skipSnaps: true,
        align: function () { return padStart; },
        slidesToScroll: 1
    });

    rootElement.__embla = embla;

    var prevBtn = rootElement.querySelector('[data-carousel-prev]');
    var nextBtn = rootElement.querySelector('[data-carousel-next]');
    var loopBackBtn = rootElement.querySelector('[data-carousel-loop-back]');

    if (prevBtn) prevBtn.addEventListener('click', function () { embla.scrollPrev(); });
    if (nextBtn) nextBtn.addEventListener('click', function () { embla.scrollNext(); });

    if (loopBackBtn) {
        var loopBackAction = loopBackBtn.querySelector('.carousel-loop-back__image') || loopBackBtn;

        function focusFirstCarouselItem() {
            var firstItem = rootElement.querySelector('[data-carousel-item]:not([data-carousel-loop-back])');
            if (!firstItem) return;
            var target = firstItem.querySelector('.focusable') || firstItem;
            target.focus({ preventScroll: true });
        }

        function doLoopBack(fromKeyboard) {
            embla.scrollTo(0);

            if (fromKeyboard && window.K7 && window.K7.suppressEnterUntilKeyUp) {
                window.K7.suppressEnterUntilKeyUp(focusFirstCarouselItem);
                return;
            }

            setTimeout(focusFirstCarouselItem, fromKeyboard ? 0 : 50);
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
            totalWidth += padStart + (parseInt(getComputedStyle(container).paddingInlineEnd) || 0);
            var needsLoopBack = totalWidth > viewportNode.offsetWidth;
            loopBackBtn.classList.toggle('visible', needsLoopBack);
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
    if (rootElement && rootElement.__embla) {
        rootElement.__embla.reInit();
    }
}

export function destroy(rootElement) {
    if (rootElement && rootElement.__embla) {
        rootElement.__embla.destroy();
        delete rootElement.__embla;
    }
}
