(function (_crud, $, undefined) {
    _crud.get = function ({ resource = null, okProcess = function () { }, spinnerSelector = null } = {}) {
        let originalSpinnerContent = ``;
        if (spinnerSelector !== null) {
            originalSpinnerContent = $(spinnerSelector).html();
            $(spinnerSelector).html(`<i class="fas fa-spinner fa-spin"></i>`);
        }

        resource.fetch()
            .then(response => {
                if (response.ok) {
                    response.json().then(data => {
                        okProcess(data);
                    });
                } else {
                    _toast.warning(`Something went wrong.`);
                }
            })
            .catch(function (error) { _toast.error(error); })
            .then(() => { if (spinnerSelector !== null) { $(spinnerSelector).html(originalSpinnerContent); } });
    };
}(window._crud = window._crud || {}, jQuery));
