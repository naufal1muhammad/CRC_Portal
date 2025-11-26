/**
 * To build a list checkbox.
 * @author   Azizan Haniff
*/
const _listCheckbox = function () {
    let builtComponent = {};

    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "title": (x.title !== undefined) ? x.title : null,
            "row": (x.row !== undefined) ? x.row : null,
            "rowIdColumn": (x.rowIdColumn !== undefined) ? x.rowIdColumn : `id`,
            "rowDisplayColumn": (x.rowDisplayColumn !== undefined) ? x.rowDisplayColumn : `id`,
            "initData": (x.initData !== undefined) ? x.initData : [],
            "required": (x.required !== undefined) ? x.required : false
        };
    };

    /**
     * Get all checked options.
     * @param   {string} selector  The selector to retrieve checked options.
     * @return  {object[]}
    */
    let getCheckedOptions = function (selector) {
        if (selector == null) { console.error(`ListCheckbox.getCheckedOptions(): selector not defined.`); return; }
        if ($(selector).length == 0) { console.error(`ListCheckbox.getCheckedOptions(): ${selector} not found.`); return; }
        if (builtComponent[selector] === undefined) { console.error(`ListCheckbox.getCheckedOptions(): ${selector} is not a List Checkbox component.`); return false; }

        // To exclude `#` or `.` in selector.
        let elementName = selector.substring(1);

        let data = [];
        $(`[name="${elementName}"]:checked`).each(function (index, element) {
            data.push($(element).val());
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
     * @param    {string[]} [x.initData]            Checked all the data in this array. Defaults to empty array.
     * @param    {boolean} [x.required]             If true, require at least one checkbox checked. Defaults to false.
     * @return   {void|Promise<Response>}
    */
    let buildComponent = function (x) {
        builtComponent[x.selector] = { "status": false, "configuration": x };

        if (x.selector == null) { console.error(`ListCheckbox.buildComponent(): selector not defined.`); return; }
        if ($(x.selector).length == 0) { console.error(`ListCheckbox.buildComponent(): ${x.selector} not found.`); return; }
        if (x.row == null) { console.error(`ListCheckbox.buildComponent(): row not defined.`); return; }

        if (Array.isArray(x.row) && Array.isArray(x.col)) {
            builder(x.selector, x.title, x.row, x.rowIdColumn, x.rowDisplayColumn, x.initData, x.required);
        } else {
            let resources = [];
            if (!Array.isArray(x.row)) { resources.push(x.row.fetch()); }

            return Promise.all(resources)
                .then(responses => {
                    return Promise.all(responses.map(function (response) {
                        return response.json();
                    }));
                })
                .then(data => {
                    let rowData = Array.isArray(x.row) ? x.row : data[0];
                    builder(x.selector, x.title, rowData, x.rowIdColumn, x.rowDisplayColumn, x.initData, x.required);
                })
                .catch(error => console.error(error));
        }
    };

    let builder = function (selector, title, rowData, rowIdColumn, rowDisplayColumn, initData, required) {
        if (!Array.isArray(rowData)) { console.error(`ListCheckbox.builder(): rowData expects an array of objects.`); return; }
        if (rowData.length == 0) { console.error(`ListCheckbox.builder(): Row has no data.`); return; }
        if (__fnGetPropertyValue(rowData[0], rowIdColumn) === undefined) { console.error(`ListCheckbox.builder(): ${rowIdColumn} not found in row object.`); return; }
        if (__fnGetPropertyValue(rowData[0], rowDisplayColumn) === undefined) { console.error(`ListCheckbox.builder(): ${rowDisplayColumn} not found in row object.`); return; }

        // To exclude `#` or `.` in selector.
        let elementId = selector.substring(1);

        let component = ``;
        component += buildHeader(title, required);
        component += buildBody(selector, elementId, rowData, rowIdColumn, rowDisplayColumn, initData, required);

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

    let buildBody = function (selector, elementId, rowData, rowIdColumn, rowDisplayColumn, initData, required) {
        let component = ``;

        component += `<div class="card-body p-0">`;
        component += buildTable(selector, elementId, rowData, rowIdColumn, rowDisplayColumn, initData, required);
        component += `</div>`;

        return component;
    };

    let buildTable = function (selector, elementId, rowData, rowIdColumn, rowDisplayColumn, initData, required) {
        let component = ``;

        component += `<div class="table-responsive">`;
        component += `<table class="table table-borderless table-striped m-0">`;
        component += buildTbody(selector, elementId, rowData, rowIdColumn, rowDisplayColumn, initData, required);
        component += `</table>`;
        component += `</div>`;

        return component;
    };

    let buildTbody = function (selector, elementId, rowData, rowIdColumn, rowDisplayColumn, initData, required) {
        let tbody = ``;

        if (rowData.length > 0) {
            required = required ? `required` : ``;

            tbody += `<tbody>`;
            rowData.forEach(function (row) {
                tbody += `<tr>`;
                tbody += `<th class="align-middle"><label for="${elementId}-${row[rowIdColumn]}" class="m-0" style="cursor:pointer;">${row[rowDisplayColumn]}</label></th>`;
                tbody += `<td class="align-middle text-center">`;
                tbody += `<div class="custom-control custom-checkbox">`;
                tbody += `<input type="checkbox" id="${elementId}-${row[rowIdColumn]}" name="${elementId}" class="custom-control-input" value="${row[rowIdColumn]}" ${required} ${initData.some(e => e == row[rowIdColumn]) ? `checked` : ``}>`;
                tbody += `<label for="${elementId}-${row[rowIdColumn]}" class="custom-control-label text-hide">Tick</label>`;
                tbody += `<div class="invalid-feedback"></div>`;
                tbody += `</div>`;
                tbody += `</td>`;
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
