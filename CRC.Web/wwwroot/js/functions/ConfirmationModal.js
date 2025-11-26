(function (_confirmationModal, $, undefined) {
    _confirmationModal.customAction = function ({ title = `Confirmation Modal`, message = `What to do?`, infoText = ``, buttonText = `OK`, resource = null, okProcess = function () { }, cancelProcess = function () { } } = {}) {
        show(title, message, infoText, buttonText, resource, okProcess, cancelProcess, `info`, `fas fa-exclamation-circle`);
    };

    _confirmationModal.add = function ({ label = `record`, infoText = ``, resource = null, okProcess = function () { }, cancelProcess = function () { } } = {}) {
        show(`Add ${label}`, `Are you sure you want to add ${label}?`, infoText, `Add`, resource, okProcess, cancelProcess, `primary`, `fas fa-exclamation-circle`);
    };

    _confirmationModal.update = function ({ label = `record`, infoText = ``, resource = null, okProcess = function () { }, cancelProcess = function () { } } = {}) {
        show(`Update ${label}`, `Are you sure you want to update ${label}?`, infoText, `Update`, resource, okProcess, cancelProcess, `success`, `fas fa-exclamation-circle`);
    };

    _confirmationModal.remove = function ({ label = `record`, infoText = ``, resource = null, okProcess = function () { }, cancelProcess = function () { } } = {}) {
        show(`Remove ${label}`, `Are you sure you want to remove ${label}?`, infoText, `Remove`, resource, okProcess, cancelProcess, `danger`, `fas fa-exclamation-triangle`);
    };

    function show(title, message, infoText, buttonText, resource, okProcess, cancelProcess, colorClass, modalTitleIcon) {
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
                        } else if (response.status === 403) {
                            _toast.warning(`Process is not allowed.`);
                        } else if (response.status === 500) {
                            if (buttonText == `Remove`) {
                                _toast.warning(`Process is not allowed. The item to be deleted is in use as REFERENCE.`);
                            }
                            else if (buttonText == `Add`) {
                                _toast.warning(`Process is not allowed. Duplicate item is not allowed.`);
                            }
                            else {
                                _toast.warning(`Process is not allowed.`);
                            }
                        } else {
                            _toast.warning(`Process failed.`);
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
            <div class="modal fade" id="confirmationModal" data-backdrop="static" data-keyboard="false" tabindex="-1" role="dialog" aria-labelledby="confirmationModalLabel" aria-modal="true">
                <div class="modal-dialog modal-dialog-centered" role="document">
                    <div class="modal-content">
                        <div class="modal-header bg-${colorClass}">
                            <h5 id="confirmationModalLabel" class="modal-title">
                                <i class="${modalTitleIcon} mr-1"></i>
                                <span>${title}</span>
                            </h5>

                            <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                <span aria-hidden="true">×</span>
                            </button>
                        </div>

                        <div class="modal-body">
                            <p id="confirmationModal-message">${message}</p>

                            ${((infoText.length != 0) ? `<p class="small font-italic font-weight-bold m-0">${infoText}</p>` : ``)}
                        </div>

                        <div class="modal-footer">
                            <button type="button" id="confirmationModal-cancel-button" class="btn btn-secondary" data-dismiss="modal">Cancel</button>
                            <button type="button" id="confirmationModal-confirm-button" class="btn btn-${colorClass}">
                                ${buttonText}
                            </button>
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
