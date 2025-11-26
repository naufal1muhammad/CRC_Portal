/**
    * To toast stuff using Toastify-JS.
    * @author   Azizan Haniff
*/
const _toast = (function () {
    let absolutePath = window.location.origin.replace(/\/$/, ``) + (($(`#main-url`).attr(`href`) == `/`) ? `` : $(`#main-url`).attr(`href`));
    let success = function (text) { toast(text, `bg-gradient bg-success`, `${absolutePath}/img/toastify-images/success.png`); };
    let warning = function (text) { toast(text, `bg-gradient bg-warning`, `${absolutePath}/img/toastify-images/warning.png`); };
    let error = function (text) { toast(text, `bg-gradient bg-danger`, `${absolutePath}/img/toastify-images/danger.png`); };
    let info = function (text) { toast(text, `bg-gradient bg-info`, `${absolutePath}/img/toastify-images/info.png`); };
    let custom = function (text, className, avatar) { toast(text, className, avatar) };
    let customContent = function (text, className, avatar, url) { toast(text, className, avatar, url) };
    let content = function (text, url) { toast(text, `bg-gradient bg-primary`, `${absolutePath}/img/toastify-images/success.png`, url) };

    let toast = function (text, className, avatar, url) {
        if (url == "" || url == null) {
            let duration = text.split(` `).filter(function (n) { return n != `` }).length * 200;

            Toastify({
                "avatar": avatar,
                "text": text,
                "backgroundColor": `none`,
                "className": className,
                "duration": duration < 3000 ? 3000 : duration,
                "close": true,
                "stopOnFocus": true
            }).showToast();
        } else {
            Toastify({
                "avatar": avatar,
                "text": text,
                "backgroundColor": `none`,
                "className": className,
                "duration": -1,
                "close": true,
                "stopOnFocus": true,
                "destination": absolutePath + url
            }).showToast();
        }
    };

    return {
        "success": function (text) { return success(text); },
        "warning": function (text) { return warning(text); },
        "error": function (text) { return error(text); },
        "info": function (text) { return info(text); },
        "custom": function (text, className, avatar) { return custom(text, className, avatar); },
        "customContent": function (text, className, avatar, url) { return customContent(text, className, avatar, url); },
        "content": function (text, url) { return content(text, url) }
    };
})();
