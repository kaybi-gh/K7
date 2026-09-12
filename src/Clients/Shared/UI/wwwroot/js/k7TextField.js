export function getSelection(el) {
    if (!el)
        return { start: 0, end: 0, value: "" };

    var value = el.value ?? "";
    var start = el.selectionStart;
    var end = el.selectionEnd;
    if (typeof start !== "number")
        start = value.length;
    if (typeof end !== "number")
        end = start;

    return { start: start, end: end, value: value };
}

export function insertText(el, text, start, end) {
    if (!el)
        return { value: text ?? "", caret: (text ?? "").length };

    var value = el.value ?? "";
    var s = Math.max(0, Math.min(start ?? value.length, value.length));
    var e = Math.max(s, Math.min(end ?? s, value.length));
    var chunk = text ?? "";
    var next = value.slice(0, s) + chunk + value.slice(e);
    var caret = s + chunk.length;

    el.value = next;
    el.focus();
    if (typeof el.setSelectionRange === "function")
        el.setSelectionRange(caret, caret);

    return { value: next, caret: caret };
}

export function setSelection(el, start, end) {
    if (!el)
        return;

    el.focus();
    if (typeof el.setSelectionRange === "function")
        el.setSelectionRange(start, end);
}
