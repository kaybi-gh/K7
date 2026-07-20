export function initCodeSegments(form, hidden, segments) {
    if (!hidden || !segments || segments.length === 0) return;

    var initial = hidden.value || '';
    if (initial) {
        var parts = initial.replace(/[^0-9]/g, '');
        for (var i = 0; i < segments.length; i++) {
            segments[i].value = parts.substring(i * 4, (i + 1) * 4);
        }
        syncHidden();
    }

    if (form) {
        form.addEventListener('submit', syncHidden);
    }

    function syncHidden() {
        var vals = [];
        segments.forEach(function (s) { vals.push(s.value); });
        hidden.value = vals.join('-');
    }

    segments.forEach(function (seg, idx) {
        seg.addEventListener('input', function () {
            this.value = this.value.replace(/[^0-9]/g, '');
            syncHidden();
            if (this.value.length >= 4 && idx < segments.length - 1) {
                segments[idx + 1].focus();
            }
        });

        seg.addEventListener('keydown', function (e) {
            if (e.key === 'Backspace' && this.value.length === 0 && idx > 0) {
                segments[idx - 1].focus();
            }
        });

        seg.addEventListener('paste', function (e) {
            e.preventDefault();
            var pasted = (e.clipboardData || window.clipboardData).getData('text');
            var digits = pasted.replace(/[^0-9]/g, '');
            for (var i = 0; i < segments.length; i++) {
                segments[i].value = digits.substring(i * 4, (i + 1) * 4);
            }
            syncHidden();
            var lastFilled = Math.min(Math.floor(digits.length / 4), segments.length - 1);
            segments[lastFilled].focus();
        });
    });

    if (segments[0]) segments[0].focus();
}
