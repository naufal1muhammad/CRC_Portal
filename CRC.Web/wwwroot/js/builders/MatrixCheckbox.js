/**
 * To build a matrix using 2 Resource objects or 2 datasets.
 * If there is only 1 dataset, only rows are built.
 * @author   Azizan Haniff
*/
const _matrixCheckbox = function () {
    let builtComponent = {};

    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "title": (x.title !== undefined) ? x.title : null,
            "row": (x.row !== undefined) ? x.row : null,
            "rowIdColumn": (x.rowIdColumn !== undefined) ? x.rowIdColumn : `id`,
            "rowDisplayColumn": (x.rowDisplayColumn !== undefined) ? x.rowDisplayColumn : `id`,
            "col": (x.col !== undefined) ? x.col : null,
            "colIdColumn": (x.colIdColumn !== undefined) ? x.colIdColumn : `id`,
            "colDisplayColumn": (x.colDisplayColumn !== undefined) ? x.colDisplayColumn : `id`,
            "required": (x.required !== undefined) ? x.required : false
        };
    };

    /**
     * Get all checked options.
     * @param   {string} selector  The selector to retrieve checked options.
     * @return  {object[]}
    */
    let getCheckedOptions = function (selector) {
        if (selector == null) { console.error(`MatrixV2.getCheckedOptions(): selector not defined.`); return; }
        if ($(selector).length == 0) { console.error(`MatrixV2.getCheckedOptions(): ${selector} not found.`); return; }
        if (builtComponent[selector] === undefined) { console.error(`MatrixV2.getCheckedOptions(): ${selector} is not a Matrix Checkbox component.`); return; }

        // To exclude `#` or `.` in selector.
        let elementName = selector.substring(1);

        let data = [];
        $(`[name="${elementName}"]:checked`).each(function (index, element) {
            data.push({
                "row": builtComponent[selector][`switched`] ? null : $(element).val().split(`,`)[0],
                "col": $(element).val().split(`,`)[1]
            })
        });

        return data;
    };

    /**
     * To build the component.
     * @param    {object} x
     * @param    {string} x.selector                The selector to build the component. e.g. `#divId`.
     * @param    {string} [x.title]                 Title for the card. Defaults to null.
     * @param    {object[]|Resource} x.row          This can either be a Resource object, or an array of object (dataset).
     * @param    {string} [x.rowIdColumn]           Default column is `id`.
     * @param    {string} [x.rowDisplayColumn]      Default column is `id`.
     * @param    {object[]|Resource} x.col          This can either be a Resource object, or an array of object (dataset).
     * @param    {string} [x.colIdColumn]           Default column is `id`.
     * @param    {string} [x.colDisplayColumn]      Default column is `id`.
     * @param    {boolean} [x.required]             If true, require at least one checkbox checked. Defaults to false.
     * @return   {void|Promise<Response>}
    */
    let buildComponent = function (x) {
        builtComponent[x.selector] = { "status": false, "switched": false, "configuration": x };

        if (x.selector == null) { console.error(`MatrixV2.buildComponent(): selector not defined.`); return; }
        if ($(x.selector).length == 0) { console.error(`MatrixV2.buildComponent(): ${x.selector} not found.`); return; }
        if (x.row == null) { console.error(`MatrixV2.buildComponent(): row not defined.`); return; }
        if (x.col == null) { console.error(`MatrixV2.buildComponent(): col not defined.`); return; }

        if (Array.isArray(x.row) && Array.isArray(x.col)) {
            builder(x.selector, x.title, x.row, x.rowIdColumn, x.rowDisplayColumn, x.col, x.colIdColumn, x.colDisplayColumn, x.required);
        } else {
            let resources = [];
            if (!Array.isArray(x.row)) { resources.push(x.row.fetch()); }
            if (!Array.isArray(x.col)) { resources.push(x.col.fetch()); }

            return Promise.all(resources)
                .then(responses => {
                    return Promise.all(responses.map(function (response) {
                        return response.json();
                    }));
                })
                .then(data => {
                    let rowData = Array.isArray(x.row) ? x.row : data[0];
                    let colData = Array.isArray(x.col) ? x.col : data[1];
                    builder(x.selector, x.title, rowData, x.rowIdColumn, x.rowDisplayColumn, colData, x.colIdColumn, x.colDisplayColumn, x.required);
                })
                .catch(error => console.error(error));
        }
    };

    let builder = function (selector, title, rowData, rowIdColumn, rowDisplayColumn, colData, colIdColumn, colDisplayColumn, required) {
        if (!Array.isArray(rowData)) { console.error(`MatrixV2.builder(): rowData expects an array of objects.`); return; }
        if (!Array.isArray(colData)) { console.error(`MatrixV2.builder(): colData expects an array of objects.`); return; }
        if (rowData.length == 0 && colData.length == 0) { console.error(`MatrixV2.builder(): Both row and col have no data.`); return; }
        if (rowData.length == 0) {
            builtComponent[selector][`switched`] = true;

            rowData = colData;
            rowIdColumn = colIdColumn;
            rowDisplayColumn = colDisplayColumn;
        }
        if (__fnGetPropertyValue(rowData[0], rowIdColumn) === undefined) { console.error(`MatrixV2.builder(): ${rowIdColumn} not found in row object.`); return; }
        if (__fnGetPropertyValue(rowData[0], rowDisplayColumn) === undefined) { console.error(`MatrixV2.builder(): ${rowDisplayColumn} not found in row object.`); return; }
        if (colData.length > 0 && !builtComponent[selector][`switched`]) {
            if (__fnGetPropertyValue(colData[0], colIdColumn) === undefined) { console.error(`MatrixV2.builder(): ${colIdColumn} not found in col object.`); return; }
            if (__fnGetPropertyValue(colData[0], colDisplayColumn) === undefined) { console.error(`MatrixV2.builder(): ${colDisplayColumn} not found in col object.`); return; }
        }

        // To exclude `#` or `.` in selector.
        let elementId = selector.substring(1);

        let component = ``;
        component += buildHeader(title, required);
        component += buildBody(selector, elementId, rowData, rowIdColumn, rowDisplayColumn, colData, colIdColumn, colDisplayColumn, required);


        // Append the component.
        $(selector)
            .addClass(`card card-light h-100 mb-0`)
            .empty()
            .append(component);

        // If required, add change event handler.
        if (required) { bindRequiredEventHandler(elementId); }

        builtComponent[selector][`status`] = true;
    };

    let buildHeader = function (title, required) {
        let component = ``;

        if (title != null) {
            component += `<div class="card-header">`;
            component += `<span class="card-title">${title}</span>`;
            component += required ? `<small class="float-right text-italic text-muted pt-1">(Must at least have one selected)</small>` : ``;
            component += `</div>`;
        }

        return component;
    };

    let buildBody = function (selector, elementId, rowData, rowIdColumn, rowDisplayColumn, colData, colIdColumn, colDisplayColumn, required) {
        let component = ``;

        component += `<div class="card-body p-0">`;
        component += buildTable(selector, elementId, rowData, rowIdColumn, rowDisplayColumn, colData, colIdColumn, colDisplayColumn, required);
        component += `</div>`;

        return component;
    };

    let buildTable = function (selector, elementId, rowData, rowIdColumn, rowDisplayColumn, colData, colIdColumn, colDisplayColumn, required) {
        let component = ``;

        component += `<div class="table-responsive">`;
        component += `<table class="table table-borderless table-striped m-0">`;
        component += buildThead(selector, elementId, colData, colIdColumn, colDisplayColumn);
        component += buildTbody(selector, elementId, rowData, rowIdColumn, rowDisplayColumn, colData, colIdColumn, required);
        component += `</table>`;
        component += `</div>`;

        return component;
    };

    let buildThead = function (selector, elementId, colData, colIdColumn, colDisplayColumn) {
        let thead = ``;

        if (colData.length > 0 && !builtComponent[selector][`switched`]) {
            thead += `<thead class="thead-light"><tr>`;
            thead += `<th></th>`;
            colData.forEach(function (col) { thead += `<th id="${elementId}-${col[colIdColumn]}" class="align-middle text-center">${col[colDisplayColumn]}</th>`; });
            thead += `</tr></thead>`;
        }

        return thead;
    };

    let buildTbody = function (selector, elementId, rowData, rowIdColumn, rowDisplayColumn, colData, colIdColumn, required) {
        let tbody = ``;

        if (rowData.length > 0) {
            let currentCol = 0;
            required = required ? `required` : ``;

            tbody += `<tbody>`;
            rowData.forEach(function (row) {
                tbody += `<tr>`;
                tbody += `<th id="${elementId}-${row[rowIdColumn]}" class="align-middle">${row[rowDisplayColumn]}</th>`;
                if (builtComponent[selector][`switched`]) {
                    let col = colData[currentCol++];
                    tbody += `<td class="align-middle text-center">`;
                    tbody += `<div class="custom-control custom-checkbox">`;
                    tbody += `<input type="checkbox" id="${elementId}-null-${col[colIdColumn]}" name="${elementId}" class="custom-control-input" value="null,${col[colIdColumn]}" ${required}>`;
                    tbody += `<label for="${elementId}-null-${col[colIdColumn]}" class="custom-control-label text-hide">Tick</label>`;
                    tbody += `<div class="invalid-feedback"></div>`;
                    tbody += `</div>`;
                    tbody += `</td>`;
                } else {
                    colData.forEach(function (col) {
                        tbody += `<td class="align-middle text-center">`;
                        tbody += `<div class="custom-control custom-checkbox">`;
                        tbody += `<input type="checkbox" id="${elementId}-${row[rowIdColumn]}-${col[colIdColumn]}" name="${elementId}" class="custom-control-input" value="${row[rowIdColumn]},${col[colIdColumn]}" ${required}>`;
                        tbody += `<label for="${elementId}-${row[rowIdColumn]}-${col[colIdColumn]}" class="custom-control-label text-hide">Tick</label>`;
                        tbody += `<div class="invalid-feedback"></div>`;
                        tbody += `</div>`;
                        tbody += `</td>`;
                    });
                }
                tbody += `</tr>`;
            });
            tbody += `</tbody>`;
        }

        return tbody;
    };

    let bindRequiredEventHandler = function (elementId) {
        $(`[name="${elementId}"]`).unbind();
        $(`[name="${elementId}"]`).change(function (event) {
            event.preventDefault();

            $(this).prop(`checked`)
                ? $(`[name="${elementId}"]`).removeAttr(`required`)
                : $(`[name="${elementId}"]`).attr(`required`, `required`);
        });
    };

    return {
        "buildComponent": function (configuration) { return buildComponent(setConfiguration(configuration)); },
        "getCheckedOptions": function (selector) { return getCheckedOptions(selector); }
    };
}();
