(function (_dataTableDataManagement, $, undefined) {
    /**
    * To initialize a table element as DataTable using resource object.
    * @author   Abdul Kareem
    * @param    {Object} resource                   Required, only if data and selector are not specified.
    * @param    {Array[Object]} columnDefs          Optional. This is a DataTable property.
    * @param    {null|Array[String]} actionColumn   Optional. If not empty, adds an extra column at the front. `edit` and `delete` only.
    * @param    {null|Function} editAction          Optional. Function to execute when edit icon is clicked. Row ID will be passed as argument.
    * @param    {null|Function} deleteAction        Optional. Function to execute when delete icon is clicked. Row ID will be passed as argument.
    * @param    {Integer} orderBy                   Optional. Column to use for ordering when DataTable is initialized. Defaults to 0 (first column).
    * @param    {String} order                      Optional. `asc` or `desc`. Defaults to `asc`.
    * @param    {String} idColumn                   Optional. Column to use for ID-ing rows. Defaults to using `id` column.
    * @param    {Function} initComplete             Optional. Function to execute when DataTable has finished initialization.
    * @param    {Function} drawCallback             Optional. Function to execute when DataTable is redrawn.
    * @return   {Void}
    */
    _dataTableDataManagement.initialize = function ({
        resource = null,
        columnDefs = [],
        actionColumn = [`edit`],
        editAction = null,
        deleteAction = null,
        orderBy = 0,
        order = `asc`,
        idColumn = `id`,
        initComplete = function () { },
        drawCallback = function () { }
    } = {}) {
        if ($(`#${resource.elementName}-table`).length == 0) {
            console.error(`Unable to initialize DataTable. (#${resource.elementName}-table not found.)`);
            return;
        }

        resource.fetch()
            .then(response => response.json())
            .then(resourceData => {
                initializeDataTable(resource.elementName, resourceData, columnDefs, actionColumn, editAction, deleteAction, orderBy, order, idColumn, initComplete, drawCallback);
            })
            .catch(function (error) { console.error(`Error in initialize datatable: ${error}`); });
    }

    /**
    * To initialize a table element as DataTable, using selector and user-provided data.
    * @author   Abdul Kareem
    * @param    {String} selector                   Required, only if resource is not specified.
    * @param    {Array[Object]} resourceData        Optional. Data for the table.
    * @param    {Array[Object]} columnDefs          Optional. This is a DataTable property.
    * @param    {null|Array[String]} actionColumn   Optional. If not empty, adds an extra column at the front. `edit` and `delete` only.
    * @param    {null|Function} editAction          Optional. Function to execute when edit icon is clicked. Row ID will be passed as argument.
    * @param    {null|Function} deleteAction        Optional. Function to execute when delete icon is clicked. Row ID will be passed as argument.
    * @param    {Integer} orderBy                   Optional. Column to use for ordering when DataTable is initialized. Defaults to 0 (first column).
    * @param    {String} order                      Optional. `asc` or `desc`. Defaults to `asc`.
    * @param    {String} idColumn                   Optional. Column to use for ID-ing rows. Defaults to using `id` column.
    * @param    {Function} initComplete             Optional. Function to execute when DataTable has finished initialization.
    * @param    {Function} drawCallback             Optional. Function to execute when DataTable is redrawn.
    * @return   {Void}
    */
    _dataTableDataManagement.initialize2 = function ({
        selector = null,
        resourceData = [],
        columnDefs = [],
        actionColumn = [`edit`],
        editAction = null,
        deleteAction = null,
        orderBy = 0,
        order = `asc`,
        idColumn = `id`,
        initComplete = function () { },
        drawCallback = function () { }
    } = {}) {
        if ($(`#${selector}-table`).length == 0) {
            console.error(`Unable to initialize DataTable. (#${selector}-table not found.)`);
            return;
        }

        initializeDataTable(selector, resourceData, columnDefs, actionColumn, editAction, deleteAction, orderBy, order, idColumn, initComplete, drawCallback);
    }

    function initializeDataTable(selector, resourceData, columnDefs, actionColumn, editAction, deleteAction, orderBy, order, idColumn, initComplete, drawCallback) {
        if ($(`#${selector}-table thead`).length === 0) {
            $(`#${selector}-table`).append(`<thead><tr></tr></thead>`);

            if (columnDefs.length === 0) {
                columnDefs = populateColumnDefs(selector, resourceData);
            } else {
                for (let i = 0; i < columnDefs.length; i++) {
                    let headerText = columnDefs[i].title !== undefined ? columnDefs[i].title : columnDefs[i].data;
                    $(`#${selector}-table thead tr`).append(`<th>${headerText}</th>`);
                }
            }
        }

        if (actionColumn != null) {
            $(`#${selector}-table thead tr`).prepend(`<th></th>`);

            // Adding an extra column at the beginning.
            // Need to shift each columnDefs' targets property to the right. 
            for (let i = 0; i < columnDefs.length; i++) {
                columnDefs[i].targets = columnDefs[i].targets + 1;
            }

            // Same goes to orderBy
            if (columnDefs.length > 0) {
                orderBy = orderBy + 1;
            }
        }

        // Remove existing <tfoot>.
        // Then, clone <thead> to <tfoot>.
        $(`#${selector}-table tfoot`).remove();
        $(`#${selector}-table`).append(
            $(`<tfoot/>`).append($(`#${selector}-table thead tr`).clone())
        );

        $(`#${selector}-table`).addClass(`table-borderless table-hover table-striped dt-responsive tablelist`);
        $.fn.dataTable.moment('DD-MM-YYYY HH:mm:ss'); // Extension

        if ($.fn.DataTable.isDataTable(`#${selector}-table`)) {
            $(`#${selector}-table`).DataTable().destroy();
        }

        $(`#${selector}-table`).DataTable({

            "initComplete": function (settings, json) {
                registerEventHandlers(selector, actionColumn, editAction, deleteAction);

                var api = new $.fn.dataTable.Api(settings);
                api.columns().header().each(function (column) { replaceColumnTitle(`NjirimaraCol`, `No`, column) });
                api.columns().footer().each(function (column) { replaceColumnTitle(`NjirimaraCol`, `No`, column) });

                initComplete();
            },
            "drawCallback": function (settings, json) {
                registerEventHandlers(selector, actionColumn, editAction, deleteAction);

                drawCallback();
            },
            "data": resourceData[`DataList`],
            "buttons": [
                {
                    "buttons": [
                        { "extend": `copy`, "exportOptions": exportOptions(columnDefs) },
                        { "extend": `excel`, "exportOptions": exportOptions(columnDefs) },
                        { "extend": `pdf`, "orientation": `landscape`, "exportOptions": exportOptions(columnDefs) },
                        { "extend": `print`, "exportOptions": exportOptions(columnDefs) }
                    ],
                    "className": `btn-sm`,
                    "extend": `collection`,
                    "text": `Export`
                }
            ],
            "columnDefs": finalizeColumnDefs(columnDefs, actionColumn, idColumn, selector),
            "dom": `
                <'row'<'col-3 d-flex align-items-center justify-content-start'B><'col-9 d-flex align-items-center justify-content-end pr-1'fl>>
                <'row'<'col-12'tr>>
                <'row'<'col-4 d-flex align-items-center justify-content-start pl-1'i><'col-8 d-flex align-items-center justify-content-end pr-1'p>>
            `,
            "language": {
                "search": `_INPUT_`,
                "searchPlaceholder": `Search...`,
                "sLengthMenu": `_MENU_`
            },
            "order": [[orderBy, order]],
            "rowId": function (data) {
                return selector + `-` + data[idColumn];
            }
        });
    }

    function populateColumnDefs(elementName, resourceData) {
        let columnDefs = [];

        if (resourceData[`DataList`].length != 0) {
            let resourceDataKey = Object.keys(resourceData[`DataList`][0]);
            let targetCount = 0;
            for (let i = 0; i < resourceDataKey.length; i++) {
                if (!(resourceDataKey[i] == "NjirimaraCol")) {
                    var colmaintaaa = resourceData[`ColumnMaintenance`][resourceDataKey[i]];
                    $(`#${elementName}-table thead tr`).append(`<th>${colmaintaaa[`ColumnDesc`]}</th>`);
                    $(`#${elementName}-table tfoot tr`).append(`<th>${colmaintaaa[`ColumnDesc`]}</th>`);

                    if (colmaintaaa[`DataType`] == `datetime` || colmaintaaa[`DataType`] == `date`) {
                        columnDefs.push({
                            "data": resourceDataKey[i],
                            "createdCell": function (td, cellData, rowData, row, col) {
                                $(td).addClass(`${resourceDataKey[i]}-data`);
                            },
                            "render": function (data) {
                                return moment(data).format('DD-MM-YYYY HH:mm:ss');
                            },
                            "targets": targetCount++
                        });
                    }

                    else if (colmaintaaa[`DataType`] == `bit`) {
                        columnDefs.push({
                            "data": resourceDataKey[i],
                            "createdCell": function (td, cellData, rowData, row, col) {
                                $(td).addClass(`${resourceDataKey[i]}-data`);
                            },
                            "render": function (data, type, row) {
                                return data.toString().toLowerCase() === `true`
                                    ? `<i class="fas fa-2x fa-check-circle text-success"><span hidden>Yes</span></i>`
                                    : `<i class="fas fa-2x fa-times-circle text-danger"><span hidden>No</span></i>`;
                            },
                            "targets": targetCount++
                        });
                    }

                    else {
                        columnDefs.push({
                            "data": resourceDataKey[i],
                            "createdCell": function (td, cellData, rowData, row, col) {
                                $(td).addClass(`${resourceDataKey[i]}-data`);
                            },
                            "targets": targetCount++
                        });
                    }
                }
            }
        }

        return columnDefs;
    }

    function exportOptions(columnDefs) {
        let columns = [];

        for (let i = 0; i < columnDefs.length; i++) {
            columns.push(columnDefs[i].targets);
        }

        return { "columns": columns };
    }

    function finalizeColumnDefs(columnDefs, actionColumn, idColumn, selector) {
        let extras = [];

        if (actionColumn != null) {
            extras.push({
                "data": idColumn,
                "render": function (data, type, row) {
                    let renderHtml = `<p class="m-0">`;

                    if (actionColumn.includes(`edit`)) {
                        renderHtml += `
                            <a href="#" id="${selector}-${data}" class="text-primary edit-entry">
                                <i class="fas fa-edit"></i>
                            </a>`;
                    }

                    if (actionColumn.includes(`delete`)) {
                        renderHtml += `
                            <a href="#" id="${selector}-${data}" class="text-danger delete-entry">
                                <i class="fas fa-trash"></i>
                            </a>`;
                    }

                    renderHtml += `</p>`;

                    return renderHtml;
                },
                "orderable": false,
                "targets": 0
            });
        }

        return [].concat(
            extras,
            columnDefs,
            [
                {
                    "className": `text-center align-middle`,
                    "targets": `_all`
                }
            ]
        );
    }

    function registerEventHandlers(elementName, actionColumn, editAction, deleteAction) {
        if (actionColumn != null) {
            // Edit
            if (actionColumn.includes(`edit`)) {
                if (editAction == null) {
                    $(`table#${elementName}-table a.edit-entry`).unbind();
                    $(`table#${elementName}-table a.edit-entry`).on(`click`, function (event) {
                        event.preventDefault();

                        try {
                            getRecord($(this).attr(`id`).split(elementName + `-`).pop());
                        }
                        catch (error) { _toast.error(error.message); }
                    });

                    $(document).off(`click`, `table#${elementName}-table .dtr-data a.edit-entry`);
                    $(document).on(`click`, `table#${elementName}-table .dtr-data a.edit-entry`, function (event) {
                        event.preventDefault();

                        try {
                            getRecord($(this).attr(`id`).split(elementName + `-`).pop());
                        }
                        catch (error) { _toast.error(error.message); }
                    });
                } else {
                    $(`table#${elementName}-table a.edit-entry`).unbind();
                    $(`table#${elementName}-table a.edit-entry`).on(`click`, function (event) {
                        event.preventDefault();

                        try {
                            editAction($(this).attr(`id`).split(elementName + `-`).pop());
                        }
                        catch (error) { _toast.error(error.message); }
                    });

                    $(document).off(`click`, `table#${elementName}-table .dtr-data a.edit-entry`);
                    $(document).on(`click`, `table#${elementName}-table .dtr-data a.edit-entry`, function (event) {
                        event.preventDefault();

                        try {
                            editAction($(this).attr(`id`).split(elementName + `-`).pop());
                        }
                        catch (error) { _toast.error(error.message); }
                    });
                }
            }

            // Delete
            if (actionColumn.includes(`delete`)) {
                if (deleteAction == null) {
                    $(`table#${elementName}-table a.delete-entry`).unbind();
                    $(`table#${elementName}-table a.delete-entry`).on(`click`, function (event) {
                        event.preventDefault();

                        try {
                            // Poor implementation, needs refactoring.
                            _form.putOriginal(`id`, $(this).attr(`id`).split(elementName + `-`).pop());
                            _form.putOriginal(_form.item, $(`#${_form.name}-table`).DataTable().row(`#${_form.name}-${_form.original[`id`]}`).data()[_form.item]);
                            deleteRecord();
                        }
                        catch (error) { _toast.error(error.message); }
                    });

                    $(document).off(`click`, `table#${elementName}-table .dtr-data a.delete-entry`);
                    $(document).on(`click`, `table#${elementName}-table .dtr-data a.delete-entry`, function (event) {
                        event.preventDefault();

                        try {
                            // Poor implementation, needs refactoring.
                            _form.putOriginal(`id`, $(this).attr(`id`).split(elementName + `-`).pop());
                            _form.putOriginal(_form.item, $(`#${_form.name}-table`).DataTable().row(`#${_form.name}-${_form.original[`id`]}`).data()[_form.item]);
                            deleteRecord();
                        }
                        catch (error) { _toast.error(error.message); }
                    });
                } else {
                    $(`table#${elementName}-table a.delete-entry`).unbind();
                    $(`table#${elementName}-table a.delete-entry`).on(`click`, function (event) {
                        event.preventDefault();

                        try { deleteAction($(this).attr(`id`).split(elementName + `-`).pop()); }
                        catch (error) { _toast.error(error.message); }
                    });

                    $(document).off(`click`, `table#${elementName}-table .dtr-data a.delete-entry`);
                    $(document).on(`click`, `table#${elementName}-table .dtr-data a.delete-entry`, function (event) {
                        event.preventDefault();

                        try {
                            deleteAction($(this).attr(`id`).split(elementName + `-`).pop());
                        }
                        catch (error) { _toast.error(error.message); }
                    });
                }
            }
        }
    }

    function replaceColumnTitle(oldString, newString, column) {
        if ($(column).text() === oldString) {
            $(column).text(newString);
        }
    }

}(window._dataTableDataManagement = window._dataTableDataManagement || {}, jQuery));
