(function () {
    "use strict";

    const ACTIVITY_EVENTS = ["mousemove", "keydown", "click", "scroll", "touchstart"];
    const LOGOUT_URL = "/Account/Logout";
    const TIMEOUT_ENDPOINT = "/Account/GetSessionTimeout";
    const REFRESH_ENDPOINT = "/Account/GetSessionTimeout";
    const WARNING_SECONDS = 30;

    let inactivityTimerId = null;
    let warningTimerId = null;
    let countdownIntervalId = null;
    let timeoutMs = 0;
    let modalEl = null;
    let countdownEl = null;

    function triggerLogout() {
        window.location.href = LOGOUT_URL;
    }

    function buildModal() {
        const wrapper = document.createElement("div");
        wrapper.setAttribute("role", "alertdialog");
        wrapper.setAttribute("aria-live", "assertive");
        wrapper.style.cssText = [
            "position:fixed",
            "top:16px",
            "left:50%",
            "transform:translateX(-50%)",
            "z-index:2147483000",
            "display:none",
            "min-width:320px",
            "max-width:560px",
            "padding:14px 18px",
            "background:#fff3cd",
            "color:#856404",
            "border:1px solid #ffeeba",
            "border-radius:8px",
            "box-shadow:0 4px 14px rgba(0,0,0,0.15)",
            "font-family:Segoe UI, Arial, sans-serif",
            "font-size:14px"
        ].join(";");

        const message = document.createElement("div");
        message.style.cssText = "margin-bottom:10px;font-weight:600;";
        const prefix = document.createTextNode("You will be logged out in ");
        const span = document.createElement("span");
        span.textContent = String(WARNING_SECONDS);
        const suffix = document.createTextNode(" seconds.");
        message.appendChild(prefix);
        message.appendChild(span);
        message.appendChild(suffix);

        const btn = document.createElement("button");
        btn.type = "button";
        btn.textContent = "STAY LOGGED IN";
        btn.style.cssText = [
            "background:#856404",
            "color:#fff",
            "border:none",
            "padding:8px 16px",
            "border-radius:4px",
            "font-weight:600",
            "cursor:pointer",
            "font-size:13px",
            "letter-spacing:0.5px"
        ].join(";");
        btn.addEventListener("click", stayLoggedIn);

        wrapper.appendChild(message);
        wrapper.appendChild(btn);
        document.body.appendChild(wrapper);

        modalEl = wrapper;
        countdownEl = span;
    }

    function showWarning() {
        if (modalEl === null) {
            buildModal();
        }
        let remaining = WARNING_SECONDS;
        countdownEl.textContent = String(remaining);
        modalEl.style.display = "block";

        if (countdownIntervalId !== null) {
            clearInterval(countdownIntervalId);
        }
        countdownIntervalId = setInterval(function () {
            remaining -= 1;
            if (remaining <= 0) {
                clearInterval(countdownIntervalId);
                countdownIntervalId = null;
                countdownEl.textContent = "0";
                return;
            }
            countdownEl.textContent = String(remaining);
        }, 1000);
    }

    function hideWarning() {
        if (countdownIntervalId !== null) {
            clearInterval(countdownIntervalId);
            countdownIntervalId = null;
        }
        if (modalEl !== null) {
            modalEl.style.display = "none";
        }
    }

    function stayLoggedIn() {
        hideWarning();
        fetch(REFRESH_ENDPOINT, {
            method: "GET",
            credentials: "same-origin",
            headers: { "Accept": "application/json" },
            redirect: "manual"
        }).catch(function () {
            // If the refresh call fails, the sliding expiration simply won't
            // be re-issued and the user will be logged out on next request.
        });
        resetTimer();
    }

    function resetTimer() {
        if (inactivityTimerId !== null) {
            clearTimeout(inactivityTimerId);
        }
        if (warningTimerId !== null) {
            clearTimeout(warningTimerId);
        }
        hideWarning();

        inactivityTimerId = setTimeout(triggerLogout, timeoutMs);

        const warningDelay = timeoutMs - WARNING_SECONDS * 1000;
        if (warningDelay > 0) {
            warningTimerId = setTimeout(showWarning, warningDelay);
        }
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
