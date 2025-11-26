(function (_confirmationModal, $, undefined) {
    _confirmationModal.customAction = function ({ title = `Confirmation Modal`, message = `What to do?`, infoText = ``, buttonText = `OK`, resource = null, okProcess = function () { }, cancelProcess = function () { }, errorResponses = function () { } } = {}) {
        show(title, message, infoText, buttonText, resource, okProcess, cancelProcess, `info`, `fas fa-exclamation-circle`, errorResponses);
    };

    _confirmationModal.add = function ({ label = `record`, infoText = ``, resource = null, okProcess = function () { }, cancelProcess = function () { }, errorResponses = function () { } } = {}) {
        show(`Add ${label}`, `Are you sure you want to add ${label}?`, infoText, `Add ${label}`, resource, okProcess, cancelProcess, `primary`, `fas fa-plus`, errorResponses);
    };

    _confirmationModal.update = function ({ label = `record`, infoText = ``, resource = null, okProcess = function () { }, cancelProcess = function () { }, errorResponses = function () { } } = {}) {
        show(`Update ${label}`, `Are you sure you want to update ${label}?`, infoText, `Update ${label}`, resource, okProcess, cancelProcess, `success`, `fas fa-save`, errorResponses);
    };

    _confirmationModal.remove = function ({ label = `record`, infoText = ``, resource = null, okProcess = function () { }, cancelProcess = function () { }, errorResponses = function () { } } = {}) {
        show(`Remove ${label}`, `Are you sure you want to remove ${label}?`, infoText, `Remove ${label}`, resource, okProcess, cancelProcess, `danger`, `fas fa-trash`, errorResponses);
    };

    function show(title, message, infoText, buttonText, resource, okProcess, cancelProcess, colorClass, modalTitleIcon, errorResponses) {
        addConfirmationModalToBody(title, message, infoText, buttonText, colorClass, modalTitleIcon);
        registerOnCloseEventHandler();

        $(`#confirmationModal-confirm-button`).click(function () {
            $(`#confirmationModal-confirm-button`).html(`<i class="fas fa-spinner fa-spin"></i>`);

            if (resource !== null) {
                resource.fetch()
                    .then(response => {
                        if (response.ok) {
                            if (response.status === 204) {
                                $(`#confirmationModal`).modal(`hide`);

                                okProcess();
                            } else {
                                response.json().then(data => {
                                    $(`#confirmationModal`).modal(`hide`);

                                    okProcess(data);
                                });
                            }
                        } else {
                            errorResponses(response);
                        }
                    })
                    .catch(function (error) { console.log(error); _toast.error(error); })
                    .then(() => { $(`#confirmationModal-confirm-button`).html(title); });
            } else {
                $(`#confirmationModal`).modal(`hide`);

                okProcess();
            }
        });

        $(`#confirmationModal-cancel-button`).click(function () {
            cancelProcess();
        });
    }

    function addConfirmationModalToBody(title, message, infoText, buttonText, colorClass, modalTitleIcon) {
        let confirmationModal = `
            <div class="modal fade" id="confirmationModal" data-bs-backdrop="static" data-bs-keyboard="false" tabindex="-1" role="dialog" aria-hidden="true">
                <div class="modal-dialog modal-dialog-centered" role="document">
                    <div class="modal-content border-0">
                        <div class="modal-body p-0">
                            <div class="rounded-top-lg py-3 ps-4 pe-6 bg-${colorClass}-subtle">
                                <h4 id="confirmationModalLabel" class="m-1">
                                    <span class="${modalTitleIcon}"></span>
                                    <span class="mx-2">${title}</span>
                                </h4>
                            </div>
                            <div class="m-3">
                                ${((infoText.length != 0)
                                    ? `<p id="confirmationModal-message">${message}</p><p class="small font-italic font-weight-bold m-0">${infoText}</p>`
                                    : `<p id="confirmationModal-message" class="p-0">${message}</p>`)}
                            </div>
                        </div>
                        <div class="modal-footer">
                            <div class="row justify-content-between align-items-center">
                                <div class="col-auto">
                                    <button type="button" id="confirmationModal-cancel-button" class="btn btn-falcon-warning btn-sm" data-bs-dismiss="modal">
                                        <span class="fas fa-times" data-fa-transform="shrink-3"></span>
                                        <span class="d-none d-sm-inline-block d-xl-none d-xxl-inline-block ms-1">Cancel</span>
                                    </button>
                                    <button type="button" id="confirmationModal-confirm-button" class="btn btn-falcon-${colorClass} btn-sm">
                                        <span class="fas fa-check" data-fa-transform="shrink-3"></span>
                                        <span class="d-none d-sm-inline-block d-xl-none d-xxl-inline-block ms-1">${buttonText}</span>
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
        $(`body`).append(confirmationModal);
        $(`#confirmationModal`).modal(`show`);
    }

    function registerOnCloseEventHandler() {
        $(`#confirmationModal`).on(`hidden.bs.modal`, function () {
            $(`#confirmationModal-confirm-button`).unbind();
            $(`#confirmationModal`).unbind();
            $(`#confirmationModal`).remove();
        });
    }
}(window._confirmationModal = window._confirmationModal || {}, jQuery));
