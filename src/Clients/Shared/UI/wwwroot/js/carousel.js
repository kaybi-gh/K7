function invokeDotNet(dotnetRef, methodName, ...args) {
    if (!dotnetRef)
        return;

    dotnetRef.invokeMethodAsync(methodName, ...args).catch(function (error) {
        var message = error?.message ?? String(error);
        if (message.includes('DotNetObjectReference') || message.includes('tracked object with id'))
            return;
    });
}

function reportVisibleSlides(rootElement, embla, dotNetRef) {
    if (!dotNetRef || !embla)
        return;

    var slides = embla.slideNodes();
    var inView = [];
    try {
        inView = embla.slidesInView();
    } catch (e) {
        inView = [];
    }

    var first = -1;
    var last = -1;
    for (var i = 0; i < inView.length; i++) {
        var idx = inView[i];
        var slide = slides[idx];
        if (!slide || slide.hasAttribute('data-carousel-loop-back'))
            continue;
        if (first < 0)
            first = idx;
        last = idx;
    }

    if (first < 0) {
        try {
            first = last = embla.selectedScrollSnap();
        } catch (e2) {
            return;
        }
    }

    invokeDotNet(dotNetRef, 'OnVisibleSlides', first, last);
}

export function init(rootElement, dotNetRef) {
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

    function scrollToInitialFocus() {
        var initial = rootElement.querySelector('[data-initial-focus]');
        if (!initial) return;
        var item = initial.hasAttribute('data-carousel-item')
            ? initial
            : initial.closest('[data-carousel-item]');
        if (!item) return;
        var slides = embla.slideNodes();
        for (var i = 0; i < slides.length; i++) {
            if (slides[i] === item || slides[i].contains(initial)) {
                try { embla.scrollTo(i, true); } catch (e) { /* ignore */ }
                return;
            }
        }
    }

    embla.on('init', function () {
        updateArrows();
        scrollToInitialFocus();
        reportVisibleSlides(rootElement, embla, dotNetRef);
    });
    embla.on('reInit', function () {
        updateArrows();
        reportVisibleSlides(rootElement, embla, dotNetRef);
    });
    embla.on('select', function () {
        updateArrows();
        reportVisibleSlides(rootElement, embla, dotNetRef);
    });
    embla.on('scroll', updateArrows);
    if (typeof embla.on === 'function') {
        try { embla.on('slidesInView', function () { reportVisibleSlides(rootElement, embla, dotNetRef); }); } catch (e) { /* older Embla */ }
    }
    scrollToInitialFocus();
    reportVisibleSlides(rootElement, embla, dotNetRef);

    // Native video hides the WebView (0-width snaps). Remember the last real
    // selected slide so close can reInit without jumping to the last card.
    var restoringSnap = false;
    function saveSnap() {
        if (restoringSnap)
            return;
        var vp = rootElement.querySelector('[data-carousel-viewport]');
        if (!vp || vp.offsetWidth < 8)
            return;
        if (document.documentElement.classList.contains('native-player-active'))
            return;

        var idx = 0;
        try { idx = embla.selectedScrollSnap(); } catch (e) { return; }
        var slides = embla.slideNodes();
        var slide = slides[idx];
        if (slide && slide.hasAttribute('data-carousel-loop-back')) {
            if (rootElement.__k7Snap)
                return;
            idx = Math.max(0, idx - 1);
            slide = slides[idx];
        }
        rootElement.__k7Snap = {
            index: idx,
            id: slide && slide.id ? slide.id : null
        };
    }

    function restoreSnap() {
        var snap = rootElement.__k7Snap;
        restoringSnap = true;
        try {
            embla.reInit();

            var target = snap && typeof snap.index === 'number' ? snap.index : 0;
            if (snap && snap.id) {
                var nextSlides = embla.slideNodes();
                for (var i = 0; i < nextSlides.length; i++) {
                    if (nextSlides[i].id === snap.id) {
                        target = i;
                        break;
                    }
                }
            }

            embla.scrollTo(target, true);
        } catch (e) {
        } finally {
            restoringSnap = false;
        }
    }

    rootElement.__k7RestoreCarousel = restoreSnap;
    embla.on('select', saveSnap);
    embla.on('settle', saveSnap);
    saveSnap();
}

export function scrollToIndex(rootElement, index) {
    if (rootElement && rootElement.__embla) {
        rootElement.__embla.scrollTo(index, true);
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
        delete rootElement.__k7RestoreCarousel;
        delete rootElement.__k7Snap;
    }
}
