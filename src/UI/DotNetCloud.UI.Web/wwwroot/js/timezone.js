window.dotnetcloud = window.dotnetcloud || {};
window.dotnetcloud.getTimezone = function () {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
    } catch (e) {
        return "UTC";
    }
};
window.dotnetcloud.getLocale = function () {
    try {
        return navigator.language || (navigator.languages && navigator.languages[0]) || "en-US";
    } catch (e) {
        return "en-US";
    }
};
window.dotnetcloud.detectAndFillTimezone = function (inputId) {
    try {
        var tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
        if (tz) {
            var el = document.getElementById(inputId);
            if (el) el.value = tz;
        }
    } catch (e) { }
};
