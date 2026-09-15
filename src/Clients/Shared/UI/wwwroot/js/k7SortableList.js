const _instances = new Map();
const _ghostClass = 'k7-sortable-list__ghost';
const _gapClass = 'k7-sortable-list__gap';

function rowsOf(listEl) {
    return listEl.querySelectorAll(':scope > .k7-sortable-list__row');
}

function indexFromY(listEl, clientY, from) {
    var rows = rowsOf(listEl);
    if (rows.length === 0)
        return 0;

    for (var i = 0; i < rows.length; i++) {
        if (i === from)
            continue;

        var rect = rows[i].getBoundingClientRect();
        if (clientY < rect.top + rect.height / 2)
            return i;
    }

    return rows.length;
}

function clearChrome(listEl) {
    var gap = listEl.querySelector(':scope > .' + _gapClass);
    if (gap)
        gap.remove();

    document.querySelectorAll('.' + _ghostClass).forEach(function (el) {
        el.remove();
    });
}

function copyScope(fromEl, toEl) {
    if (!fromEl || !toEl || !fromEl.attributes)
        return;

    for (var i = 0; i < fromEl.attributes.length; i++) {
        var name = fromEl.attributes[i].name;
        if (name.charAt(0) === 'b' && name.charAt(1) === '-')
            toEl.setAttribute(name, fromEl.attributes[i].value);
    }
}

function placeGap(listEl, to, height) {
    var gap = listEl.querySelector(':scope > .' + _gapClass);
    if (!gap) {
        gap = document.createElement('div');
        gap.className = _gapClass;
        gap.setAttribute('aria-hidden', 'true');
        copyScope(listEl, gap);
    }

    gap.style.height = height + 'px';
    var rows = rowsOf(listEl);
    if (to >= rows.length)
        listEl.appendChild(gap);
    else
        listEl.insertBefore(gap, rows[to]);
}

function makeGhost(row, rect) {
    var ghost = row.cloneNode(true);
    ghost.classList.add(_ghostClass);
    ghost.classList.remove('k7-sortable-list__row--dragging');
    ghost.setAttribute('aria-hidden', 'true');
    ghost.querySelectorAll('button, a, input, select, textarea').forEach(function (el) {
        el.setAttribute('tabindex', '-1');
        el.setAttribute('disabled', '');
    });
    ghost.style.width = rect.width + 'px';
    ghost.style.height = rect.height + 'px';
    ghost.style.left = '0px';
    ghost.style.top = '0px';
    document.body.appendChild(ghost);
    return ghost;
}

function moveGhost(ghost, clientX, clientY, offsetX, offsetY) {
    ghost.style.transform = 'translate(' + (clientX - offsetX) + 'px, ' + (clientY - offsetY) + 'px)';
}

function rowIndex(listEl, handle) {
    var row = handle && handle.closest('.k7-sortable-list__row');
    if (!row || row.parentElement !== listEl)
        return -1;
    return Array.prototype.indexOf.call(rowsOf(listEl), row);
}

function toggleHandleEdit(handle) {
    var editing = handle.hasAttribute('data-sn-editing');
    if (editing) {
        handle.removeAttribute('data-sn-editing');
        handle.dispatchEvent(new CustomEvent('sn:editcommit', { bubbles: false }));
        if (window.SpatialNavigation)
            SpatialNavigation.resume();
    } else {
        handle.setAttribute('data-sn-editing', 'true');
        if (window.SpatialNavigation)
            SpatialNavigation.pause();
        handle.dispatchEvent(new CustomEvent('sn:editstart', { bubbles: false }));
    }
}

export function attach(id, listEl, dotnetRef) {
    dispose(id);
    if (!(listEl instanceof Element) || !dotnetRef)
        return;

    var from = -1;
    var lastTo = -1;
    var pointerId = null;
    var dragRow = null;
    var dragHeight = 0;
    var grabOffsetX = 0;
    var grabOffsetY = 0;
    var ghost = null;

    function onPointerDown(e) {
        if (listEl.classList.contains('k7-sortable-list--disabled'))
            return;
        if (listEl.classList.contains('k7-sortable-list--keyboard-grab'))
            return;
        if (e.button !== 0)
            return;

        var handle = e.target.closest('.k7-sortable-list__handle');
        if (!handle || !listEl.contains(handle))
            return;

        var row = handle.closest('.k7-sortable-list__row');
        if (!row || row.parentElement !== listEl)
            return;

        from = Array.prototype.indexOf.call(rowsOf(listEl), row);
        if (from < 0)
            return;

        var rect = row.getBoundingClientRect();
        lastTo = from;
        pointerId = e.pointerId;
        dragRow = row;
        dragHeight = rect.height;
        grabOffsetX = e.clientX - rect.left;
        grabOffsetY = e.clientY - rect.top;
        handle.setPointerCapture(e.pointerId);
        e.preventDefault();
        ghost = makeGhost(row, rect);
        moveGhost(ghost, e.clientX, e.clientY, grabOffsetX, grabOffsetY);
        row.classList.add('k7-sortable-list__row--dragging');
        listEl.classList.add('k7-sortable-list--dragging');
        placeGap(listEl, from, dragHeight);
    }

    function onPointerMove(e) {
        if (from < 0 || e.pointerId !== pointerId)
            return;

        e.preventDefault();
        if (ghost)
            moveGhost(ghost, e.clientX, e.clientY, grabOffsetX, grabOffsetY);

        var to = indexFromY(listEl, e.clientY, from);
        if (to === lastTo)
            return;

        lastTo = to;
        placeGap(listEl, to, dragHeight);
    }

    function endDrag(e) {
        if (from < 0 || (e && e.pointerId !== pointerId))
            return;

        var origin = from;
        var to = lastTo < 0 ? from : lastTo;
        from = -1;
        lastTo = -1;
        pointerId = null;
        clearChrome(listEl);
        listEl.classList.remove('k7-sortable-list--dragging');
        if (dragRow)
            dragRow.classList.remove('k7-sortable-list__row--dragging');
        dragRow = null;
        ghost = null;
        dotnetRef.invokeMethodAsync('OnDragDropped', origin, to);
    }

    function onKeyDown(e) {
        var handle = e.target.closest && e.target.closest('.k7-sortable-list__handle');
        if (!handle || !listEl.contains(handle) || handle.disabled)
            return;

        var grabbing = listEl.classList.contains('k7-sortable-list--keyboard-grab')
            || handle.hasAttribute('data-sn-editing');

        if (e.key === ' ' || e.key === 'Spacebar') {
            e.preventDefault();
            e.stopPropagation();
            toggleHandleEdit(handle);
            return;
        }

        if ((e.key === 'ArrowUp' || e.key === 'ArrowDown') && grabbing)
            e.preventDefault();
    }

    function onEditStart(e) {
        var handle = e.target.closest && e.target.closest('.k7-sortable-list__handle');
        if (!handle || !listEl.contains(handle))
            return;

        var index = rowIndex(listEl, handle);
        if (index < 0)
            return;

        dotnetRef.invokeMethodAsync('OnKeyboardGrabStart', index);
    }

    function onEditCommit(e) {
        var handle = e.target.closest && e.target.closest('.k7-sortable-list__handle');
        if (!handle || !listEl.contains(handle))
            return;

        dotnetRef.invokeMethodAsync('OnKeyboardGrabCommit');
    }

    function onEditCancel(e) {
        var handle = e.target.closest && e.target.closest('.k7-sortable-list__handle');
        if (!handle || !listEl.contains(handle))
            return;

        dotnetRef.invokeMethodAsync('OnKeyboardGrabCancel');
    }

    listEl.addEventListener('pointerdown', onPointerDown);
    listEl.addEventListener('pointermove', onPointerMove, { passive: false });
    listEl.addEventListener('pointerup', endDrag);
    listEl.addEventListener('pointercancel', endDrag);
    listEl.addEventListener('keydown', onKeyDown, true);
    listEl.addEventListener('sn:editstart', onEditStart, true);
    listEl.addEventListener('sn:editcommit', onEditCommit, true);
    listEl.addEventListener('sn:editcancel', onEditCancel, true);

    _instances.set(id, {
        listEl: listEl,
        onPointerDown: onPointerDown,
        onPointerMove: onPointerMove,
        endDrag: endDrag,
        onKeyDown: onKeyDown,
        onEditStart: onEditStart,
        onEditCommit: onEditCommit,
        onEditCancel: onEditCancel
    });
}

export function focusGrabbed(id) {
    var inst = _instances.get(id);
    if (!inst)
        return;

    var handle = inst.listEl.querySelector('.k7-sortable-list__handle--grabbing');
    if (!handle || document.activeElement === handle)
        return;

    handle.focus({ preventScroll: true });
}

export function dispose(id) {
    var inst = _instances.get(id);
    if (!inst)
        return;

    clearChrome(inst.listEl);
    inst.listEl.removeEventListener('pointerdown', inst.onPointerDown);
    inst.listEl.removeEventListener('pointermove', inst.onPointerMove);
    inst.listEl.removeEventListener('pointerup', inst.endDrag);
    inst.listEl.removeEventListener('pointercancel', inst.endDrag);
    inst.listEl.removeEventListener('keydown', inst.onKeyDown, true);
    inst.listEl.removeEventListener('sn:editstart', inst.onEditStart, true);
    inst.listEl.removeEventListener('sn:editcommit', inst.onEditCommit, true);
    inst.listEl.removeEventListener('sn:editcancel', inst.onEditCancel, true);
    _instances.delete(id);
}
