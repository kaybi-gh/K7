const _instances = new Map();

function parsePx(value) {
    var n = parseFloat(value);
    return Number.isFinite(n) ? n : 0;
}

function notify(inst, visible) {
    if (inst.lastCount === visible)
        return;

    inst.lastCount = visible;
    inst.dotnetRef.invokeMethodAsync('SetVisibleCount', visible);
}

function measureBar(inst) {
    var barEl = inst.barEl;
    if (!(barEl instanceof Element))
        return;

    var measureRow = barEl.querySelector('[data-custom-nav-measure]');
    if (!measureRow)
        return;

    var items = measureRow.querySelectorAll('[data-custom-nav-item]');
    var more = measureRow.querySelector('[data-custom-nav-more]');
    var count = items.length;
    if (count === 0) {
        notify(inst, 0);
        return;
    }

    var styles = getComputedStyle(measureRow);
    var gap = parsePx(styles.columnGap || styles.gap);
    var padL = parsePx(styles.paddingLeft);
    var padR = parsePx(styles.paddingRight);
    if (measureRow.clientWidth <= 0)
        return;

    var available = Math.max(0, measureRow.clientWidth - padL - padR);

    var widths = [];
    var total = 0;
    var anyWidth = false;
    for (var i = 0; i < count; i++) {
        var w = items[i].offsetWidth;
        if (w > 0)
            anyWidth = true;
        widths.push(w);
        total += w + (i > 0 ? gap : 0);
    }

    if (!anyWidth)
        return;

    if (total <= available) {
        notify(inst, count);
        return;
    }

    var moreWidth = more ? more.offsetWidth : 40;
    var slack = 8;
    var budget = available - moreWidth - gap - slack;
    var used = 0;
    var visible = 0;
    for (var j = 0; j < count; j++) {
        var next = used + widths[j] + (j > 0 ? gap : 0);
        if (next > budget)
            break;
        used = next;
        visible++;
    }

    if (visible < 1)
        visible = 1;
    if (visible >= count)
        visible = count - 1;

    notify(inst, visible);
}

function scheduleMeasure(inst) {
    if (inst.raf)
        return;

    if (document.fonts && document.fonts.status !== 'loaded') {
        if (!inst.fontHook) {
            inst.fontHook = true;
            document.fonts.ready.then(function () {
                inst.fontHook = false;
                if (_instances.get(inst.id) === inst)
                    scheduleMeasure(inst);
            });
        }
        return;
    }

    inst.raf = requestAnimationFrame(function () {
        inst.raf = 0;
        measureBar(inst);
    });
}

export function attach(id, barEl, dotnetRef) {
    dispose(id);
    if (!(barEl instanceof Element) || !dotnetRef)
        return;

    var inst = {
        id: id,
        barEl: barEl,
        dotnetRef: dotnetRef,
        lastCount: -1,
        raf: 0,
        fontHook: false,
        ro: null
    };

    inst.ro = new ResizeObserver(function () {
        scheduleMeasure(inst);
    });
    inst.ro.observe(barEl);
    _instances.set(id, inst);
    scheduleMeasure(inst);
}

export function measure(id) {
    var inst = _instances.get(id);
    if (inst)
        scheduleMeasure(inst);
}

export function dispose(id) {
    var inst = _instances.get(id);
    if (!inst)
        return;

    if (inst.raf)
        cancelAnimationFrame(inst.raf);
    if (inst.ro)
        inst.ro.disconnect();
    _instances.delete(id);
}
