/**
 * To create a component for selecting multiple choices.
 * @author   Azizan Haniff
*/
const _listSelection = configuration => (function () {
    let history = {};

    let setConfiguration = function (x) {
        return {
            "id": (x.id !== undefined) ? x.id : null,
            "resource": (x.resource !== undefined) ? x.resource : null,
            "idColumn": (x.idColumn !== undefined) ? x.idColumn : `id`,
            "displayColumns": (x.displayColumns !== undefined) ? x.displayColumns : [`id`],
            "title": (x.title !== undefined) ? x.title : `Select Records`,
            "okProcess": (x.okProcess !== undefined) ? x.okProcess : function (selected) { }
        };
    };

    /**
     * Build the component.
     * @param    {object} x
     * @param    {string} x.id                      ID to store selection history.
     * @param    {object[]|Resource} x.resource     This can either be a Resource object, or an array of object.
     * @param    {string} [x.idColumn]              The column to use as ID. Defaults to `id` if not specified.
     * @param    {string[]} [x.displayColumns]      The column(s) to display. Defaults to `id` if not specified.
     * @param    {string} [x.title]                 Title for the modal. Defaults to `Select Records` if not specified.
     * @param    {Function} [x.okProcess]           Function to execute when OK is clicked. Will pass all selected and removed options.
     * @return   {void|Promise<Response>}
    */
    let buildComponent = function (x) {
        if (x.id == null) { console.error(`ListSelection.buildComponent(): id not defined.`); return; }
        if (x.resource == null) { console.error(`ListSelection.buildComponent(): resource not defined.`); return; }

        if (Array.isArray(x.resource)) {
            builder(x.id, x.resource, x.idColumn, x.displayColumns, x.title, x.okProcess);
        } else {
            return x.resource.fetch()
                .then(response => response.json())
                .then(data => builder(x.id, data, x.idColumn, x.displayColumns, x.title, x.okProcess))
                .catch(error => console.error(error));
        }
    }

    let builder = function (id, data, idColumn, displayColumns, title, okProcess) {
        if (!Array.isArray(data)) { console.error(`ListSelection.builder(): data expects an array of objects.`); return; }
        if (data.length > 0) {
            if (__fnGetPropertyValue(data[0], idColumn) === undefined) { console.error(`ListSelection.builder(): ${idColumn} not found in object.`); return; }
            displayColumns.forEach(function (displayColumn) { if (__fnGetPropertyValue(data[0], displayColumn) === undefined) { console.error(`ListSelection.builder(): ${displayColumn} not found in object.`); return; } });
        }

        if (data.length > 0) {
            let component = buildModal(data, idColumn, displayColumns, title);

            $(`body`).append(component);
            $(`#listSelectionModal`).modal(`show`);

            reassignHistory(id, idColumn);

            bindOkButtonEventHandler(id, idColumn, okProcess);
            bindTbodyRowEventHandler();
            bindModalEventHandler();
            bindAllCheckboxEventHandler();
        } else {
            _toast.info(`No available choice to select.`);
        }
    };

    let buildModal = function (data, idColumn, displayColumns, title) {
        let component = ``;

        component += `<div class="modal fade" id="listSelectionModal" data-backdrop="static" data-keyboard="false" tabindex="-1" role="dialog" aria-labelledby="listSelectionModalLabel" aria-modal="true">`;
        component += `<div class="modal-dialog modal-xl" role="document">`;
        component += `<div class="modal-content">`;
        component += buildModalHeader(title);
        component += buildModalBody(data, idColumn, displayColumns);
        component += buildModalFooter();
        component += `</div>`;
        component += `</div>`;
        component += `</div>`;

        return component;
    };

    let buildModalHeader = function (title) {
        let component = ``;

        component += `<div class="modal-header bg-primary">`;
        component += `<h5 id="listSelectionModalLabel" class="modal-title">`;
        component += `<i class="fas fa-list mr-3"></i>`;
        component += `<span>${title}</span>`;
        component += `</h5>`;
        component += `<button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">×</span></button>`;
        component += `</div>`;

        return component;
    };

    let buildModalBody = function (data, idColumn, displayColumns) {
        let component = ``;

        component += `<div class="modal-body">`;
        component += buildTable(data, idColumn, displayColumns);
        component += `</div>`;

        return component;
    };

    let buildTable = function (data, idColumn, displayColumns) {
        let component = ``;

        if (data.length > 0) {
            component += `<div class="table-responsive">`;
            component += `<table class="table table-borderless table-hover table-striped m-0">`;
            component += buildTableThead(idColumn, displayColumns);
            component += buildTableTbody(data, idColumn, displayColumns);
            component += `</table>`;
            component += `</div>`;
        }

        return component;
    };

    let buildTableThead = function (idColumn, displayColumns) {
        let component = ``;

        if (displayColumns.length > 0) {
            component += `<thead class="thead-light">`;
            component += `<tr>`;
            component += `<th class="align-middle text-center">`;
            component += `<div class="custom-control custom-checkbox">`;
            component += `<input type="checkbox" id="listSelection-all" class="custom-control-input">`;
            component += `<label for="listSelection-all" class="custom-control-label text-hide">${idColumn}</label>`;
            component += `</div>`;
            component += `</th>`;
            displayColumns.forEach(function (displayColumn) { component += `<th class="align-middle text-center text-uppercase">${displayColumn}</th>`; });
            component += `</tr>`;
            component += `</thead>`;
        }

        return component;
    };

    let buildTableTbody = function (data, idColumn, displayColumns) {
        let component = ``;

        if (data.length > 0) {
            component += `<tbody>`;
            data.forEach(function (row) {
                component += `<tr style="cursor: pointer;">`;
                component += `<td class="align-middle text-center">`;
                component += `<div class="custom-control custom-checkbox">`;
                component += `<input type="checkbox" id="listSelection-${row[idColumn]}" name="listSelection" class="custom-control-input" value="${row[idColumn]}">`;
                component += `<label for="listSelection-${row[idColumn]}" class="custom-control-label text-hide">Tick</label>`;
                component += `</div>`;
                component += `</td>`;
                displayColumns.forEach(function (displayColumn) { component += `<td class="align-middle text-center">${row[displayColumn]}</td>`; });
                component += `</tr>`;
            });
            component += `</tbody>`;
        }

        return component;
    };

    let buildModalFooter = function () {
        let component = ``;

        component += `<div class="modal-footer">`;
        component += `<button type="button" class="btn btn-secondary" data-dismiss="modal">Cancel</button>`;
        component += `<button type="button" id="listSelectionModal-okButton" class="btn btn-primary">OK</button>`;
        component += `</div>`;

        return component;
    };

    let bindOkButtonEventHandler = function (id, idColumn, okProcess) {
        $(`#listSelectionModal-okButton`).unbind();
        $(`#listSelectionModal-okButton`).click(function (event) {
            event.preventDefault();

            let titles = [];
            $(`#listSelectionModal thead tr th`).each(function (index, element) {
                index == 0
                    ? titles.push($(element).find(`label`).text())
                    : titles.push($(element).text());
            });

            let selected = [];
            let selectedIds = [];
            $(`[name="listSelection"]:checked`).each(function (index, element) {
                let detail = {};
                detail[titles[0]] = $(element).val();
                $(element).parents(`:eq(2)`).children().each(function (index, element) { if (index != 0) { detail[titles[index]] = $(element).text(); } });
                selected.push(detail);
                selectedIds.push($(element).val());
            });

            let current = updateHistory(id, idColumn, selected, selectedIds);

            okProcess(current.selected, current.removed);

            $(`#listSelectionModal`).modal(`hide`);
        });
    };

    let bindTbodyRowEventHandler = function () {
        $(`#listSelectionModal table tbody tr`).unbind();
        $(`#listSelectionModal table tbody tr`).click(function (event) {
            event.preventDefault();

            let checkbox = $(this).find(`input[type="checkbox"]`);
            checkbox.prop(`checked`, !checkbox.prop(`checked`));

            $(`[name="listSelection"]:checked`).length === $(`[name="listSelection"]`).length
                ? $(`#listSelection-all`).prop(`checked`, true)
                : $(`#listSelection-all`).prop(`checked`, false);
        });
    };

    let bindModalEventHandler = function () {
        $(`#listSelectionModal`).on(`hidden.bs.modal`, function () {
            $(`#listSelectionModal-okButton`).unbind();
            $(`#listSelectionModal table tbody tr`).unbind();
            $(`#listSelectionModal`).unbind();
            $(`#listSelectionModal`).remove();
        });
    };

    let bindAllCheckboxEventHandler = function () {
        $(`#listSelection-all`).unbind();
        $(`#listSelection-all`).change(function (event) {
            event.preventDefault();

            $(this).prop(`checked`)
                ? $(`[name="listSelection"]`).prop(`checked`, true)
                : $(`[name="listSelection"]`).prop(`checked`, false);
        });
    };

    let reassignHistory = function (id, idColumn) {
        if (history[id] !== undefined) {
            history[id].forEach(function (element) {
                $(`#listSelection-${element[idColumn]}`).prop(`checked`, true);
            });
        }
    };

    let updateHistory = function (id, idColumn, selected, selectedIds) {
        let removed = [];

        if (history[id] !== undefined) {
            history[id].forEach(function (element) {
                if (!selectedIds.includes(element[idColumn])) {
                    removed.push(element);
                }
            });
        }

        history[id] = selected;

        return { "selected": selected, "removed": removed };
    };

    /**
     * Reset history data.
     * @param    {string} x
     * @return   {void}
    */
    let resetHistory = function (x) {
        delete history[x];
    };

    return {
        "buildComponent": function (x) { return buildComponent(setConfiguration(x)); },
        "resetHistory": function (x) { return resetHistory(x); }
    };
})(configuration);
