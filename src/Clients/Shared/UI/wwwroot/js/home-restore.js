import { scrollTo as scrollVerticalCarouselTo } from './vertical-carousel.js';

function scrollRowIntoView(cardRoot, carousel) {
    var rowEl = carousel ? (carousel.closest('.carousel-wrapper') || carousel) : cardRoot;
    var scrollParent = cardRoot.closest('.page-scrollable');
    if (scrollParent && rowEl) {
        var parentRect = scrollParent.getBoundingClientRect();
        var rowRect = rowEl.getBoundingClientRect();
        var nextTop = scrollParent.scrollTop + (rowRect.top - parentRect.top);
        // Keep a little breathing room under sticky headers / app bars.
        scrollParent.scrollTop = Math.max(0, nextTop - 12);
        return;
    }

    if (rowEl)
        rowEl.scrollIntoView({ block: 'nearest', behavior: 'instant' });
}

function tryScrollToCard(mediaId, allowWithoutEmbla) {
    var cardRoot = document.getElementById('home-card-' + mediaId);
    if (!cardRoot) return false;

    var carouselItem = cardRoot.closest('[data-carousel-item]');
    var carousel = cardRoot.closest('[data-carousel]');

    // Wait until Embla has initialized so horizontal position can be restored.
    if (carousel && !carousel.__embla && !allowWithoutEmbla)
        return false;

    if (carousel && carousel.__embla && carouselItem && carouselItem.parentElement) {
        var items = carouselItem.parentElement.querySelectorAll('[data-carousel-item]:not([data-carousel-loop-back])');
        for (var i = 0; i < items.length; i++) {
            if (items[i] === carouselItem) {
                carousel.__embla.scrollTo(i, true);
                break;
            }
        }
    }

    var vCarousel = cardRoot.closest('.vertical-carousel');
    if (vCarousel && vCarousel.__vcarousel) {
        var container = vCarousel.querySelector('.vertical-carousel__container');
        if (container) {
            var slide = cardRoot.closest('.vertical-carousel__container > *');
            if (slide) {
                for (var j = 0; j < container.children.length; j++) {
                    if (container.children[j] === slide || container.children[j].contains(cardRoot)) {
                        scrollVerticalCarouselTo(vCarousel, j);
                        break;
                    }
                }
            }
        }
    } else {
        scrollRowIntoView(cardRoot, carousel);
    }

    return true;
}

function waitFrame() {
    return new Promise(function (resolve) {
        requestAnimationFrame(function () { resolve(); });
    });
}

/**
 * Scroll the home feed so the previously focused card is visible.
 * Retries briefly while carousels finish initializing (Embla).
 */
export async function scrollToCard(mediaId, maxAttempts) {
    var attempts = typeof maxAttempts === 'number' && maxAttempts > 0 ? maxAttempts : 40;

    for (var attempt = 0; attempt < attempts; attempt++) {
        if (tryScrollToCard(mediaId, attempt === attempts - 1))
            return true;

        await waitFrame();
    }

    return false;
}
