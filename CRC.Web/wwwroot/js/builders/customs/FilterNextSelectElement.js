/**
 * To populate options of a Select element that dependent with previous select element with the result set received from a HTTP request.
 * For valueColumn, displayColumn and allOption, traverse by using period ".". e.g. `department.name`, `user.accessmatrices.roleId`. See GetValue.js.
 * @author   Abdul Kareem
 * @param    {object} configuration
 * @param    {string} configuration.firstFilterName                                 Selector for <select> element, e.g. `mySelectId`.
 * @param    {string} configuration.secondFilterName                                Selector for <select> element, e.g. `mySelectId`.
 * @param    {string} [configuration.secondFilterLabelText]                         The label text to display at the first <option> element of the secondFilterName.
 * @param    {string} configuration.resourcePath                                    The resource Path is the api resource Path.
 * @param    {[]} configuration.queries                                             The extra queries.
 * @param    {string|string[]} [configuration.valueColumn]                          The column used as value of the <option> elements. Defaults to using secondFilterName.
 * @param    {string|string[]} [configuration.displayColumn]                        The column used as display text of the <option> elements. Defaults to using secondFilterName.
 * @return   {Promise<Response>}
*/
const _filterNextSelectElement = configuration => (function (x) {
    let setConfiguration = function (x) {
        return {
            "firstFilterName": (x.firstFilterName !== undefined) ? x.firstFilterName : null,
            "secondFilterName": (x.secondFilterName !== undefined) ? x.secondFilterName : null,
            "secondFilterLabelText": (x.secondFilterLabelText !== undefined) ? x.secondFilterLabelText : null,
            "resourcePath": (x.resourcePath !== undefined) ? x.resourcePath : null,
            "queries": (x.queries !== undefined) ? x.queries : [],
            "valueColumn": (x.valueColumn !== undefined) ? x.valueColumn : x.secondFilterName,
            "displayColumn": (x.displayColumn !== undefined) ? x.displayColumn : x.secondFilterName
        };
    };

    let buildComponent = function (x) {
        if (x.firstFilterName == null) { console.error(`FilterNextSelectElement.buildComponent(): firstFilterName is required.`); return; }
        if (x.secondFilterName == null) { console.error(`FilterNextSelectElement.buildComponent(): secondFilterName is required.`); return; }
        if (x.resourcePath == null) { console.error(`FilterNextSelectElement.buildComponent(): resourcePath is required.`); return; }

        builder(x.firstFilterName, x.secondFilterName, x.secondFilterLabelText, x.resourcePath, x.queries, x.valueColumn, x.displayColumn);
    };

    let builder = function (firstFilterName, secondFilterName, secondFilterLabelText, resourcePath, queries, valueColumn, displayColumn) {
        var labelText;
        if (secondFilterLabelText == null) { labelText = secondFilterName.charAt(0).toUpperCase() + secondFilterName.slice(1); }
        else { labelText = secondFilterLabelText }

        $(`[name="filter-${firstFilterName}"]`).change(function () {
            _selectElement({
                selector: `#filter-${secondFilterName}`
                , labelText: `${labelText}`
                , resource: new Resource(resourcePath, { query: (queries == []) ? { prevSelector: $(`[name="filter-${firstFilterName}"]`).val() } : Object.assign({ prevSelector: $(`[name="filter-${firstFilterName}"]`).val() }, queries) })
                , valueColumn: `${valueColumn}`, displayColumn: `${displayColumn}`
            })
                .then(() => {
                    $(`#filter-${secondFilterName}`).prop("disabled", false);
                })
        });
    };

    return buildComponent(setConfiguration(x));
})(configuration);
