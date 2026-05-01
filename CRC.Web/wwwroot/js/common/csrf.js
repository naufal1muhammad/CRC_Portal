(function () {
    function readMeta(name) {
        var el = document.querySelector('meta[name="' + name + '"]');
        return el ? el.getAttribute('content') : null;
    }

    var token = readMeta('csrf-token');
    var headerName = readMeta('csrf-header') || 'X-CSRF-TOKEN';

    function isUnsafeMethod(method) {
        if (!method) return false;
        method = String(method).toUpperCase();
        return method === 'POST' || method === 'PUT' || method === 'PATCH' || method === 'DELETE';
    }

    function isSameOrigin(url) {
        try {
            var u = new URL(url, window.location.href);
            return u.origin === window.location.origin;
        } catch (_) {
            return true;
        }
    }

    window._csrf = {
        token: function () { return token; },
        headerName: function () { return headerName; },
        applyTo: function (headers) {
            headers = headers || {};
            if (token) headers[headerName] = token;
            return headers;
        }
    };

    if (typeof window.fetch === 'function') {
        var origFetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            init = init || {};
            var url = typeof input === 'string'
                ? input
                : (input && input.url) || '';
            var method = init.method
                || (typeof input !== 'string' && input ? input.method : '')
                || 'GET';

            if (token && isUnsafeMethod(method) && isSameOrigin(url)) {
                if (init.headers instanceof Headers) {
                    init.headers.set(headerName, token);
                } else {
                    init.headers = Object.assign({}, init.headers || {});
                    init.headers[headerName] = token;
                }
            }
            return origFetch(input, init);
        };
    }

    if (typeof window.XMLHttpRequest === 'function') {
        var origOpen = XMLHttpRequest.prototype.open;
        var origSend = XMLHttpRequest.prototype.send;
        XMLHttpRequest.prototype.open = function (method, url) {
            this.__csrfMethod = method;
            this.__csrfUrl = url;
            return origOpen.apply(this, arguments);
        };
        XMLHttpRequest.prototype.send = function () {
            if (token && isUnsafeMethod(this.__csrfMethod) && isSameOrigin(this.__csrfUrl)) {
                try { this.setRequestHeader(headerName, token); } catch (_) { }
            }
            return origSend.apply(this, arguments);
        };
    }

    function attachJQuery() {
        if (window.jQuery && window.jQuery.ajaxPrefilter) {
            window.jQuery.ajaxPrefilter(function (options, originalOptions, jqXHR) {
                if (token && isUnsafeMethod(options.type) && isSameOrigin(options.url || '')) {
                    jqXHR.setRequestHeader(headerName, token);
                }
            });
            return true;
        }
        return false;
    }
    if (!attachJQuery()) {
        document.addEventListener('DOMContentLoaded', attachJQuery);
    }
})();
