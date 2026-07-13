import { scrollTo as scrollVerticalCarouselTo } from './vertical-carousel.js';

export function scrollToCard(mediaId) {
    var cardRoot = document.getElementById('home-card-' + mediaId);
    if (!cardRoot) return false;

    var carouselItem = cardRoot.closest('[data-carousel-item]');
    var carousel = cardRoot.closest('[data-carousel]');
    if (carousel && carousel.__embla && carouselItem && carouselItem.parentElement) {
        var items = carouselItem.parentElement.querySelectorAll('[data-carousel-item]:not([data-carousel-loop-back])');
        for (var i = 0; i < items.length; i++) {
            if (items[i] === carouselItem) {
                carousel.__embla.scrollTo(i);
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
        var rowEl = carousel ? (carousel.closest('.carousel-wrapper') || carousel) : null;
        if (rowEl) {
            rowEl.scrollIntoView({ block: 'nearest', behavior: 'instant' });
        }
    }

    return true;
}
