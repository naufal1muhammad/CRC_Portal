/**
 * To populate options of a Select element with the result set received from a HTTP request.
 * For valueColumn, displayColumn and allOption, traverse by using period ".". e.g. `department.name`, `user.accessmatrices.roleId`. See GetValue.js.
 * @author   Azizan Haniff
 * @param    {object} configuration
 * @param    {string} configuration.selector                                        Selector for <select> element, e.g. `#mySelectId`.
 * @param    {string} [configuration.labelText]                                     The label text to display at the first <option> element.
 * @param    {null|{display: string, value: string}} [configuration.allOption]      Defaults to null.
 * @param    {null|{display: string, value: string}} [configuration.trueOption]     Defaults to Yes - 1 if null or undefined.
 * @param    {null|{display: string, value: string}} [configuration.falseOption]    Defaults to False - 0 if null or undefined.
 * @return   {Promise<Response>}
*/
const _selectBooleanElement = configuration => (function (x) {
    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "labelText": (x.labelText !== undefined) ? x.labelText : null,
            "allOption": (x.allOption !== undefined) ? x.allOption : null,
            "trueOption": (x.trueOption !== undefined) ? x.trueOption : null,
            "falseOption": (x.falseOption !== undefined) ? x.falseOption : null
        };
    };

    let buildComponent = function (x) {
        if (x.selector == null) { console.error(`SelectBooleanElement.buildComponent(): selector is required.`); return; }
        if ($(x.selector).length == 0) { console.error(`SelectBooleanElement.buildComponent(): ${x.selector} not found.`); return; }

        builder(x.selector, x.labelText, x.allOption, x.trueOption, x.falseOption);
    };

    let builder = function (selector, labelText, allOption, trueOption, falseOption) {
        // If allOption are correctly assigned.
        if (allOption !== null && (allOption[`display`] == undefined || allOption[`value`] == undefined)) { console.error(`SelectBooleanElement.buildComponent(): allOption must have "display" and "value" properties.`); return; }
        if (trueOption !== null && (trueOption[`display`] == undefined || trueOption[`value`] == undefined)) { console.error(`SelectBooleanElement.buildComponent(): trueOption must have "display" and "value" properties.`); return; }
        if (falseOption !== null && (falseOption[`display`] == undefined || falseOption[`value`] == undefined)) { console.error(`SelectBooleanElement.buildComponent(): falseOption must have "display" and "value" properties.`); return; }

        // If there's a label text specified, add it as the first option.
        let options = labelText == null ? `` : `<option value="" selected disabled>${labelText}</option>`;

        // If allOption is not null, add an _ALL option.
        options += allOption != null ? `<option value="${allOption[`value`]}">${allOption[`display`]}</option>` : ``;

        // If trueOption is not null, add an True option.
        options += trueOption != null ? `<option value="${trueOption[`value`]}">${trueOption[`display`]}</option>` : `<option value="1">Yes</option>`;

        // If falseOption is not null, add an False option.
        options += falseOption != null ? `<option value="${falseOption[`value`]}">${falseOption[`display`]}</option>` : `<option value="0">No</option>`;

        $(selector).empty().append(options);
    };

    return buildComponent(setConfiguration(x));
})(configuration);
