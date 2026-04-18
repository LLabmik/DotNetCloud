window.dotnetcloud = window.dotnetcloud || {};
window.dotnetcloud.getTimezone = function () {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
    } catch (e) {
        return "UTC";
    }
};
