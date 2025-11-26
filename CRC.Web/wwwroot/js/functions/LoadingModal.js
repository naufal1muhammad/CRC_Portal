(function (_loadingModal, $, undefined) {
    let loadingModal = `
        ﻿﻿<div class="modal fade" id="loading-modal" data-bs-keyboard="false" data-bs-backdrop="static" tabindex="-1" aria-labelledby="loading-modalLabel" aria-hidden="true">
            <div class="modal-dialog modal-dialog-centered modal-sm" role="document">
                <div class="modal-content border-0">
                    <div class="modal-body p-0 text-center">
                        <div class="bg-light rounded-3 py-3">
                            <div class="spinner-border" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                            <p class="fs--2 mb-0">Thank you for your <span class="fw-semi-bold">patience</span></p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;

    _loadingModal.show = function () {
        show();
    };

    _loadingModal.hide = function () {
        hide();
    };

    function show() {
        $(`body`).append(loadingModal);

        $(`#loading-modal`).modal(`show`);
    }

    function hide() {
        setTimeout(() => {
            $(`#loading-modal`).modal(`hide`);
        }, 1000);
    }
}(window._loadingModal = window._loadingModal || {}, jQuery));
