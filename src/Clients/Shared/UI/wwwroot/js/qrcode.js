window.k7QrCode = {
    generate: function (elementId, text) {
        var el = document.getElementById(elementId);
        if (!el || !text) return;
        el.innerHTML = '';
        new QRCode(el, {
            text: text,
            width: 200,
            height: 200,
            colorDark: '#000000',
            colorLight: '#ffffff',
            correctLevel: QRCode.CorrectLevel.M
        });
    }
};
