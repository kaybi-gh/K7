window.K7 = window.K7 || {};

K7.Lottie = {
    play: function (container, path) {
        if (!container || !window.lottie) return;
        container.innerHTML = '';
        window.lottie.loadAnimation({
            container: container,
            renderer: 'svg',
            loop: true,
            autoplay: true,
            path: path
        });
    }
};
