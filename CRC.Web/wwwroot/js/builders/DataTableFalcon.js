/**
    * To build a table element as DataTable (BS5 + Falcon theme).
    * @param    {object} configuration
    * @param    {string} configuration.selector                 The selector to build the component. e.g. `#divId`.
    * @param    {object[]|Resource} configuration.resource      This can either be a Resource object, or an array of object (dataset).
    * @param    {object[]} [configuration.columnDefs]           DataTable property.
    * @param    {string[]} [configuration.actionColumn]         If not empty, adds an extra column at the front. `edit` and `delete` only. Defaults to [`edit`].
    * @param    {Function} [configuration.editOnClick]          Function to execute when edit icon is clicked. Row ID will be passed as argument. Defaults to null.
    * @param    {Function} [configuration.deleteOnClick]        Function to execute when delete icon is clicked. Row ID will be passed as argument. Defaults to null.
    * @param    {number} [configuration.orderBy]                Column to use for ordering when DataTable is initialized. Defaults to 0 (first column).
    * @param    {string} [configuration.order]                  `asc` or `desc`. Defaults to `asc`.
    * @param    {string} [configuration.idColumn]               Column to use for ID-ing rows. Defaults to `id`.
    * @param    {Function} [configuration.initComplete]         Function to execute when DataTable has finished initialization.
    * @param    {Function} [configuration.drawCallback]         Function to execute when DataTable is redrawn.
    * @param    {boolean} [configuration.editIcon]              Edit icon.
    * @param    {object} [configuration.fixedColumns]           Fixed Columns extension. Defaults to { left: 1, right: 1}.
    * @param    {string} [configuration.exportTitle]            Title of the exported excel.
    * @param    {boolean} [configuration.searching]             Toggle search ar
    * @param    {boolean} [configuration.paging]                Toggle paging
    * @param    {boolean} [configuration.info]                  Toggle info
    * @return   {void|Promise<Response>}
*/
const _dataTable = configuration => (function (x) {
    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "resource": (x.resource !== undefined) ? x.resource : null,
            "columnDefs": (x.columnDefs !== undefined) ? x.columnDefs : [],
            "actionColumn": (x.actionColumn !== undefined) ? x.actionColumn : [`edit`],
            "editOnClick": (x.editOnClick !== undefined) ? x.editOnClick : null,
            "deleteOnClick": (x.deleteOnClick !== undefined) ? x.deleteOnClick : null,
            "orderBy": (x.orderBy !== undefined) ? x.orderBy : 0,
            "order": (x.order !== undefined) ? x.order : `asc`,
            "idColumn": (x.idColumn !== undefined) ? x.idColumn : `id`,
            "initComplete": (x.initComplete !== undefined) ? x.initComplete : function () { },
            "drawCallback": (x.drawCallback !== undefined) ? x.drawCallback : function () { },
            "editIcon": (x.editIcon !== undefined) ? x.editIcon : `fas fa-eye`,
            "fixedColumns": (x.fixedColumns !== undefined) ? x.fixedColumns : { left: 1, right: 1 },
            "exportTitle": (x.exportTitle !== undefined) ? x.exportTitle : `Data`,
            "pageLength": (x.pageLength !== undefined) ? x.pageLength : 10,
            "searching": (x.searching !== undefined) ? x.searching : true,
            "paging": (x.paging !== undefined) ? x.paging : true,
            "info": (x.info !== undefined) ? x.info : true
        };
    };

    let buildComponent = function (x) {
        if (x.selector == null) { console.error(`DataTableBS5.buildComponent(): selector not defined.`); return; }
        if ($(x.selector).length == 0) { console.error(`DataTableBS5.buildComponent(): ${x.selector} not found.`); return; }
        if (x.resource == null) { console.error(`DataTableBS5.buildComponent(): resource not defined.`); return; }

        if (Array.isArray(x.resource)) {
            builder(x.selector, x.resource, x.columnDefs, x.actionColumn, x.editOnClick, x.deleteOnClick, x.orderBy, x.order, x.idColumn, x.initComplete, x.drawCallback, x.editIcon, x.fixedColumns, x.exportTitle, x.pageLength, x.searching, x.paging, x.info);
        } else {
            return x.resource.fetch()
                .then(response => response.json())
                .then(data => builder(x.selector, data, x.columnDefs, x.actionColumn, x.editOnClick, x.deleteOnClick, x.orderBy, x.order, x.idColumn, x.initComplete, x.drawCallback, x.editIcon, x.fixedColumns, x.exportTitle, x.pageLength, x.searching, x.paging, x.info))
                .catch(error => console.error(error));
        }
    };

    let builder = function (selector, resourceData, columnDefs, actionColumn, editOnClick, deleteOnClick, orderBy, order, idColumn, initComplete, drawCallback, editIcon, fixedColumns, exportTitle, pageLength, searching, paging, info) {
        if (!Array.isArray(resourceData)) { console.error(`DataTableBS5.builder(): resourceData expects an array of objects.`); return; }
        if (resourceData.length > 0 && __fnGetPropertyValue(resourceData[0], idColumn) === undefined) { console.error(`DataTableBS5.builder(): ${idColumn} not found in object.`); return; }

        // If DataTable is already initialized, no need to reinitialize again.
        if ($.fn.DataTable.isDataTable(selector)) {
            $(selector).DataTable().clear().rows.add(resourceData).draw();
            return;
        }

        // To exclude `#` or `.` in selector.
        let elementId = selector.substring(1);

        columnDefs = buildThead(selector, resourceData, columnDefs);

        if (actionColumn.length > 0) {
            $(`${selector} thead tr`).append(`<th></th>`);
        }

        $(selector).addClass(`table-hover table-borderless data-table text-900 fs--1`);
        $.fn.dataTable.moment('DD-MM-YYYY HH:mm:ss'); // Extension

        $(selector).DataTable({
            "initComplete": function (settings, json) {
                registerEventHandlers(selector, elementId, actionColumn, editOnClick, deleteOnClick);

                var api = new $.fn.dataTable.Api(settings);
                api.columns().header().each(function (column) { replaceColumnTitle(`NjirimaraCol`, `No`, column) });
                api.columns().footer().each(function (column) { replaceColumnTitle(`NjirimaraCol`, `No`, column) });

                $(`.${elementId}-buttons-excel`).unbind();
                $(`.${elementId}-buttons-excel`).on(`click`, function () {
                    $(selector).DataTable().button(`.buttons-excel`).trigger();
                });

                initComplete();
            },
            "drawCallback": function (settings, json) {
                registerEventHandlers(selector, elementId, actionColumn, editOnClick, deleteOnClick);

                $(this).closest('.dataTables_wrapper').find('.pagination').addClass('pagination-sm');

                drawCallback();
            },
            "data": resourceData,
            "buttons": [
                {
                    "buttons": [
                        { "extend": `copy`, "exportOptions": exportOptions(columnDefs), title: exportTitle },
                        { "extend": `excel`, "exportOptions": exportOptions(columnDefs), title: exportTitle },
                        { "extend": `pdf`, "orientation": `landscape`, "exportOptions": exportOptions(columnDefs), title: exportTitle },
                        { "extend": `print`, "exportOptions": exportOptions(columnDefs), title: exportTitle }
                    ],
                    "className": `btn-sm`,
                    "extend": `collection`,
                    "text": `Export`
                }
            ],
            "columnDefs": finalizeColumnDefs(columnDefs, actionColumn, idColumn, elementId, editIcon),
            "dom": `
                <'row g-0 align-items-center justify-content-center justify-content-sm-end'<'col-auto'f><'col-auto pe-3'l>>
                <'table-responsive scrollbar'tr>
                <'row g-0 align-items-center justify-content-center justify-content-sm-between'<'col-auto px-3'i><'col-auto px-3'p>>
            `,
            "language": {
                "search": `_INPUT_`,
                "searchPlaceholder": `Search...`,
                "sLengthMenu": `_MENU_`
            },
            "order": [[orderBy, order]],
            "rowId": function (data) {
                return elementId + `-` + data[idColumn];
            },
            fixedColumns: fixedColumns,
            autoWidth: false,
            pageLength: pageLength,
            searching: searching,
            paging: paging,
            info: info
        });
    };

    let buildThead = function (selector, resourceData, columnDefs) {
        if ($(`${selector} thead`).length === 0) {
            $(selector).append(`<thead><tr></tr></thead>`);

            if (columnDefs.length === 0) {
                // If columnDefs is empty, then add all properties available in resourceData.
                if (resourceData.length != 0) {
                    let resourceDataKey = Object.keys(resourceData[0]);

                    for (let i = 0; i < resourceDataKey.length; i++) {
                        $(`${selector} thead tr`).append(`<th>${resourceDataKey[i]}</th>`);
                        $(`${selector} tfoot tr`).append(`<th>${resourceDataKey[i]}</th>`);

                        columnDefs.push({
                            "data": resourceDataKey[i],
                            "createdCell": function (td, cellData, rowData, row, col) {
                                $(td).addClass(`${resourceDataKey[i]}-data`);
                            },
                            "targets": i
                        });
                    }
                }
            } else {
                for (let i = 0; i < columnDefs.length; i++) {
                    let headerText = columnDefs[i].title !== undefined ? columnDefs[i].title : columnDefs[i].data;
                    $(`${selector} thead tr`).append(`<th>${headerText}</th>`);
                }
            }
        }

        return columnDefs;
    };

    let exportOptions = function (columnDefs) {
        let columns = [];

        for (let i = 0; i < columnDefs.length; i++) {
            columns.push(columnDefs[i].targets);
        }

        return { "columns": columns };
    };

    let finalizeColumnDefs = function (columnDefs, actionColumn, idColumn, elementId, editIcon) {
        let extras = [];

        if (actionColumn.length > 0) {
            extras.push({
                "data": idColumn,
                "render": function (data, type, row) {
                    let renderHtml = `<p class="m-0">`;

                    if (actionColumn.includes(`edit`)) {
                        renderHtml += `
                            <a href="#" name="${elementId}-${data}" class="edit-row btn btn-sm btn-falcon-primary rounded-pill">
                                <span class="${editIcon}"></span>
                            </a>`;
                    }

                    if (actionColumn.includes(`delete`)) {
                        renderHtml += `
                            <a href="#" name="${elementId}-${data}" class="delete-row btn btn-sm btn-falcon-danger rounded-pill">
                                <span class="fas fa-trash"></span>
                            </a>`;
                    }

                    renderHtml += `</p>`;

                    return renderHtml;
                },
                "className": `text-center`,
                "orderable": false,
                "targets": columnDefs.length,
            });
        }

        return [].concat(
            columnDefs,
            extras,
            [
                {
                    "className": `align-middle`,
                    "targets": `_all`
                }
            ]
        );
    };

    let registerEventHandlers = function (selector, elementId, actionColumn, editOnClick, deleteOnClick) {
        if (actionColumn.length > 0) {
            // Edit
            if (actionColumn.includes(`edit`)) {
                if (editOnClick == null) {
                    $(`table${selector} a.edit-row`).unbind();
                    $(`table${selector} a.edit-row`).on(`click`, function (event) {
                        event.preventDefault();

                        try { getRecord($(this).attr(`name`).split(elementId + `-`).pop()); }
                        catch (error) { console.error(error); }
                    });

                    $(document).off(`click`, `table${selector} .dtr-data a.edit-row`);
                    $(document).on(`click`, `table${selector} .dtr-data a.edit-row`, function (event) {
                        event.preventDefault();

                        try { getRecord($(this).attr(`name`).split(elementId + `-`).pop()); }
                        catch (error) { console.error(error); }
                    });
                } else {
                    $(`table${selector} a.edit-row`).unbind();
                    $(`table${selector} a.edit-row`).on(`click`, function (event) {
                        event.preventDefault();

                        try { editOnClick($(this).attr(`name`).split(elementId + `-`).pop(), $(this)); }
                        catch (error) { console.error(error); }
                    });

                    $(document).off(`click`, `table${selector} .dtr-data a.edit-row`);
                    $(document).on(`click`, `table${selector} .dtr-data a.edit-row`, function (event) {
                        event.preventDefault();

                        try { editOnClick($(this).attr(`name`).split(elementId + `-`).pop(), $(this)); }
                        catch (error) { console.error(error); }
                    });
                }
            }

            // Delete
            if (actionColumn.includes(`delete`)) {
                if (deleteOnClick == null) {
                    $(`table${selector} a.delete-row`).unbind();
                    $(`table${selector} a.delete-row`).on(`click`, function (event) {
                        event.preventDefault();

                        try { deleteRecord($(this).attr(`name`).split(elementId + `-`).pop()); }
                        catch (error) { console.error(error); }
                    });

                    $(document).off(`click`, `table${selector} .dtr-data a.delete-row`);
                    $(document).on(`click`, `table${selector} .dtr-data a.delete-row`, function (event) {
                        event.preventDefault();

                        try { deleteRecord($(this).attr(`name`).split(elementId + `-`).pop()); }
                        catch (error) { console.error(error); }
                    });
                } else {
                    $(`table${selector} a.delete-row`).unbind();
                    $(`table${selector} a.delete-row`).on(`click`, function (event) {
                        event.preventDefault();

                        try { deleteOnClick($(this).attr(`name`).split(elementId + `-`).pop(), $(this)); }
                        catch (error) { console.error(error); }
                    });

                    $(document).off(`click`, `table${selector} .dtr-data a.delete-row`);
                    $(document).on(`click`, `table${selector} .dtr-data a.delete-row`, function (event) {
                        event.preventDefault();

                        try { deleteOnClick($(this).attr(`name`).split(elementId + `-`).pop(), $(this)); }
                        catch (error) { console.error(error); }
                    });
                }
            }
        }
    };

    let replaceColumnTitle = function (oldString, newString, column) {
        if ($(column).text() === oldString) {
            $(column).text(newString);
        }
    };

    return buildComponent(setConfiguration(x));
})(configuration);
