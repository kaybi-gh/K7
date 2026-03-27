export function init(rootElement) {
    if (rootElement.__embla) return;

    var viewportNode = rootElement.querySelector('[data-carousel-viewport]');
    if (!viewportNode) return;

    var embla = EmblaCarousel(viewportNode, {
        dragFree: true,
        containScroll: 'trimSnaps',
        align: 'start',
        slidesToScroll: 1
    });

    rootElement.__embla = embla;

    var prevBtn = rootElement.querySelector('[data-carousel-prev]');
    var nextBtn = rootElement.querySelector('[data-carousel-next]');

    if (prevBtn) prevBtn.addEventListener('click', function () { embla.scrollPrev(); });
    if (nextBtn) nextBtn.addEventListener('click', function () { embla.scrollNext(); });

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

export function destroy(rootElement) {
    if (rootElement && rootElement.__embla) {
        rootElement.__embla.destroy();
        delete rootElement.__embla;
    }
}
