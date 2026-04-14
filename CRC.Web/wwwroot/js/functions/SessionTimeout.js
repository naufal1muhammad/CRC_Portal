(function () {
    "use strict";

    const ACTIVITY_EVENTS = ["mousemove", "keydown", "click", "scroll", "touchstart"];
    const LOGOUT_URL = "/Account/Logout";
    const TIMEOUT_ENDPOINT = "/Account/GetSessionTimeout";

    let inactivityTimerId = null;
    let timeoutMs = 0;

    function triggerLogout() {
        window.location.href = LOGOUT_URL;
    }

    function resetTimer() {
        if (inactivityTimerId !== null) {
            clearTimeout(inactivityTimerId);
        }
        inactivityTimerId = setTimeout(triggerLogout, timeoutMs);
    }

    function start(seconds) {
        timeoutMs = seconds * 1000;
        ACTIVITY_EVENTS.forEach(function (evt) {
            window.addEventListener(evt, resetTimer, { passive: true });
        });
        resetTimer();
    }

    document.addEventListener("DOMContentLoaded", function () {
        fetch(TIMEOUT_ENDPOINT, {
            method: "GET",
            credentials: "same-origin",
            headers: { "Accept": "application/json" },
            redirect: "manual"
        })
            .then(function (response) {
                if (!response || !response.ok) {
                    return null;
                }
                return response.json();
            })
            .then(function (data) {
                if (!data || typeof data.inactivityTimeoutSeconds !== "number" || data.inactivityTimeoutSeconds <= 0) {
                    return;
                }
                start(data.inactivityTimeoutSeconds);
            })
            .catch(function () {
                // Network/parse error — fail silently; server-side cookie
                // expiration will still catch the user on their next request.
            });
    });
})();
