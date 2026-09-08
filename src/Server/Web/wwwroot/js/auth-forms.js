(function () {
    var revealTimeouts = {};

    document.addEventListener("click", function (e) {
        // Never intercept the login/register submitter. Disabling it or calling
        // preventDefault here aborts the POST/302 in Chromium.
        if (e.target.closest("button[type=submit], input[type=submit]"))
            return;

        var btn = e.target.closest("[data-k7-toggle-password]");
        if (!btn || btn.type === "submit")
            return;
        var input = resolveToggleInput(btn);
        if (!input)
            return;
        var reveal = input.type === "password";
        input.type = reveal ? "text" : "password";
        var icon = btn.querySelector("i");
        if (icon) {
            icon.classList.toggle("ph-eye", !reveal);
            icon.classList.toggle("ph-eye-slash", reveal);
        }
        var key = input.id || input.name || "pw";
        clearTimeout(revealTimeouts[key]);
        if (reveal) {
            revealTimeouts[key] = setTimeout(function () {
                input.type = "password";
                if (icon) {
                    icon.classList.add("ph-eye");
                    icon.classList.remove("ph-eye-slash");
                }
            }, 5000);
        }
    });

    document.addEventListener("input", function (e) {
        var target = e.target;
        if (!target)
            return;
        var passwordRoot = target.closest("[data-k7-auth-password]");
        if (passwordRoot)
            updatePasswordField(passwordRoot);
        var emailRoot = target.closest("[data-k7-auth-email]");
        if (emailRoot)
            updateEmailField(emailRoot);
    });

    document.querySelectorAll("[data-k7-auth-password]").forEach(updatePasswordField);
    document.querySelectorAll("[data-k7-auth-email]").forEach(updateEmailField);

    function resolveToggleInput(btn) {
        var id = btn.getAttribute("data-k7-toggle-for");
        if (id)
            return document.getElementById(id);
        var row = btn.closest(".auth-input-row");
        return row ? row.querySelector("input") : null;
    }

    function updatePasswordField(root) {
        var passwordInput = root.querySelector("[data-k7-password]");
        var confirmInput = root.querySelector("[data-k7-password-confirm]");
        var password = passwordInput ? passwordInput.value : "";
        root.querySelectorAll("[data-password-rule]").forEach(function (item) {
            setRuleMet(item, isRuleMet(item.getAttribute("data-password-rule"), password, item));
        });
        if (!confirmInput)
            return;
        var confirm = confirmInput.value;
        var mismatch = root.querySelector("[data-k7-password-mismatch]");
        var match = root.querySelector("[data-k7-password-match]");
        var showMismatch = confirm.length > 0 && confirm !== password;
        var showMatch = confirm.length > 0 && confirm === password;
        if (mismatch)
            mismatch.hidden = !showMismatch;
        if (match)
            match.hidden = !showMatch;
        confirmInput.setAttribute("aria-invalid", showMismatch ? "true" : "false");
        var describedBy = showMismatch && mismatch ? mismatch.id
            : showMatch && match ? match.id
            : "";
        if (describedBy)
            confirmInput.setAttribute("aria-describedby", describedBy);
        else
            confirmInput.removeAttribute("aria-describedby");
    }

    function setRuleMet(item, met) {
        item.classList.toggle("password-rules__item--met", met);
        var icon = item.querySelector("i");
        if (!icon)
            return;
        icon.classList.toggle("ph-check-circle", met);
        icon.classList.toggle("ph-circle", !met);
    }

    function isRuleMet(rule, password, item) {
        var list = item.closest("ul");
        var minLength = parseInt((list && list.getAttribute("data-required-length")) || "0", 10);
        var uniqueChars = parseInt((list && list.getAttribute("data-required-unique-chars")) || "0", 10);
        switch (rule) {
            case "MinLength":
                return password.length >= minLength;
            case "Digit":
                return /\p{Nd}/u.test(password);
            case "Lowercase":
                return /\p{Ll}/u.test(password);
            case "Uppercase":
                return /\p{Lu}/u.test(password);
            case "NonAlphanumeric":
                return /[^\p{L}\p{Nd}]/u.test(password);
            case "UniqueChars":
                return new Set(password).size >= uniqueChars;
            default:
                return false;
        }
    }

    function updateEmailField(root) {
        var input = root.querySelector("input");
        var invalid = root.querySelector("[data-k7-auth-email-invalid]");
        if (!input || !invalid)
            return;
        var value = input.value.trim();
        var showInvalid = value.length > 0 && !isValidEmail(value);
        invalid.hidden = !showInvalid;
        input.setAttribute("aria-invalid", showInvalid ? "true" : "false");
        if (showInvalid)
            input.setAttribute("aria-describedby", invalid.id);
        else
            input.removeAttribute("aria-describedby");
    }

    function isValidEmail(value) {
        var parts = value.split("@");
        if (parts.length !== 2)
            return false;
        var local = parts[0];
        var host = parts[1];
        return local.length > 0
            && host.indexOf(".") > 0
            && host.indexOf(" ") < 0
            && value.indexOf(" ") < 0;
    }
})();
