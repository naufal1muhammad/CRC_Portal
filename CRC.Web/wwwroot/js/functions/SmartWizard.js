// @ts-nocheck
(function (_smartWizard, $, undefined) {
    _smartWizard.reset = null;

    _smartWizard.initialize = function ({ container = ``, onFinish = function () { }, onReset = function () { } } = {}) {
        if (container !== ``) {
            if ($(container).length > 0) {
                $(container).smartWizard({
                    "theme": `dots`,
                    "autoAdjustHeight": false,
                    "toolbarSettings": {
                        "toolbarPosition": `none`
                    },
                    "keyboardSettings": { "keyNavigation": false }
                });

                _smartWizard.reset = onReset;
                registerEventHandlers(container, onFinish);
            } else {
                console.error(`Unable to initialize SmartWizard. (${container} not found.)`);
            }
        } else {
            console.error(`Unable to initialize SmartWizard. Container not defined.`);
        }
    }

    _smartWizard.dataTablePicker = function ({ container = ``, rowPrefixId = ``, rowId = ``, resource = null, columnDefs = [], drawCallback = function () { } } = {}) {
        if ($(container).length > 0) {
            if ($.fn.DataTable.isDataTable(container)) {
                $(container).DataTable().destroy();
            }

            $(container).DataTable({
                "ajax": {
                    "url": resource.url(),
                    "dataSrc": ``
                },
                "columnDefs": finalizeColumnDefs(container, columnDefs),
                "createdRow": function (row, data, dataIndex) {
                    $(row).attr(`id`, rowPrefixId + `-` + data[rowId]);
                    $(row).css(`cursor`, `pointer`);
                },
                "dom": `
                    <'row'<'col-12 d-flex align-items-center justify-content-end'fl>>
                    <'row'<'col-12'tr>>
                    <'row'<'col-12 d-flex align-items-center justify-content-end pr-1'p>>
                `,
                "language": {
                    "search": `_INPUT_`,
                    "searchPlaceholder": `Search...`,
                    "sLengthMenu": `_MENU_`
                },
                "drawCallback": function () {
                    $(this.api().table().header()).hide();
                    $(this.api().table().footer()).hide();

                    drawCallback();
                }
            });
        } else {
            console.error(`Unable to initialize SmartWizard DataTablePicker. (${container} not found.)`)
        }
    }

    function finalizeColumnDefs(container, columnDefs) {
        return [].concat(
            columnDefs,
            [
                {
                    "data": null,
                    "render": function (data, type, row) {
                        return `
                            <button type="button" class="btn btn-link btn-block ${container.split(`#`)[1]} py-0">
                                <i class="fas fa-plus-circle"></i>
                            </button>
                        `;
                    },
                    "orderable": false,
                    "targets": columnDefs.length
                },
                {
                    "className": `text-center align-middle`,
                    "targets": `_all`
                }
            ]
        );
    }

    function registerEventHandlers(container, onFinish) {
        $(container).on(`showStep`, function (e, anchorObject, stepIndex, stepDirection, stepPosition) {
            $(`.sw-next-step`).addClass(`d-none`);
            $(`.sw-prev-step`).addClass(`d-none`);
            $(`.sw-finish-step`).addClass(`d-none`);

            if (stepPosition == `first`) {
                $(`.sw-next-step`).removeClass(`d-none`);
            } else if (stepPosition == `last`) {
                $(`.sw-prev-step`).removeClass(`d-none`);
                $(`.sw-finish-step`).removeClass(`d-none`);
            } else {
                $(`.sw-prev-step`).removeClass(`d-none`);
                $(`.sw-next-step`).removeClass(`d-none`);
            }
        });

        $(document).on(`click`, `.sw-prev-step`, function () { $(container).smartWizard(`prev`); });
        $(document).on(`click`, `.sw-next-step`, function () { $(container).smartWizard(`next`); });
        $(document).on(`click`, `.sw-finish-step`, function () { onFinish(); });
    }
}(window._smartWizard = window._smartWizard || {}, jQuery));
