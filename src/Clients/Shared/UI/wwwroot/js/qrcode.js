window.k7QrCode = {
    generate: function (elementId, text, size) {
        var el = document.getElementById(elementId);
        if (!el || !text) return;
        var s = size || 200;
        el.innerHTML = '';
        new QRCode(el, {
            text: text,
            width: s,
            height: s,
            colorDark: '#000000',
            colorLight: '#ffffff',
            correctLevel: QRCode.CorrectLevel.M
        });
    }
};
