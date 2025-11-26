/**
 * To populate options of a Select element with the result set received from a HTTP request.
 * For valueColumn, displayColumn and allOption, traverse by using period ".". e.g. `department.name`, `user.accessmatrices.roleId`. See GetValue.js.
 * @author   Azizan Haniff
 * @param    {object} configuration
 * @param    {string} configuration.selector                                        Selector for <select> element, e.g. `#mySelectId`.
 * @param    {string} [configuration.labelText]                                     The label text to display at the first <option> element.
 * @param    {object[]|Resource} configuration.resource                             This can either be a Resource object, or an array of object.
 * @param    {string} [configuration.valueColumn]                                   The column used as value of the <option> elements. Required only if response returns a list of objects.
 * @param    {string|string[]} [configuration.displayColumn]                        The column used as display text of the <option> elements. Defaults to using valueColumn.
 * @param    {null|{display: string, value: string}} [configuration.allOption]      Defaults to null.
 * @param    {null|{display: string, value: string}} [configuration.noOption]       Defaults to null.
 * @return   {Promise<Response>}
*/
const _selectElement = configuration => (function (x) {
    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "labelText": (x.labelText !== undefined) ? x.labelText : null,
            "resource": (x.resource !== undefined) ? x.resource : null,
            "valueColumn": (x.valueColumn !== undefined) ? x.valueColumn : null,
            "displayColumn": (x.displayColumn !== undefined) ? x.displayColumn : x.valueColumn,
            "allOption": (x.allOption !== undefined) ? x.allOption : null,
            "noOption": (x.noOption !== undefined) ? x.noOption : null
        };
    };

    let buildComponent = function (x) {
        if (x.selector == null) { console.error(`SelectElement.buildComponent(): selector is required.`); return; }
        if ($(x.selector).length == 0) { console.error(`SelectElement.buildComponent(): ${x.selector} not found.`); return; }
        if (x.resource == null) { console.error(`SelectElement.buildComponent(): resource is required.`); return; }

        if (Array.isArray(x.resource)) {
            builder(x.selector, x.resource, x.labelText, x.valueColumn, x.displayColumn, x.allOption, x.noOption);
        } else {
            return x.resource.fetch()
                .then(response => response.json())
                .then(data => builder(x.selector, data, x.labelText, x.valueColumn, x.displayColumn, x.allOption, x.noOption))
                .catch(error => console.error(error));
        }
    };

    let builder = function (selector, data, labelText, valueColumn, displayColumn, allOption, noOption) {
        // A few checks if response is a list of objects.
        if (typeof data[0] === `object` && valueColumn === null) { console.error(`SelectElement.buildComponent(): valueColumn is required when response is a list of objects.`); return; }
        if (typeof data[0] === `object` && __fnGetPropertyValue(data[0], valueColumn) === undefined) { console.error(`SelectElement.buildComponent(): valueColumn not found in object.`); return; }

        // If allOption are correctly assigned.
        if (allOption !== null && (allOption[`display`] == undefined || allOption[`value`] == undefined)) { console.error(`SelectElement.buildComponent(): allOption must have "display" and "value" properties.`); return; }

        // If noOption are correctly assigned.
        if (noOption !== null && (noOption[`display`] == undefined || noOption[`value`] == undefined)) { console.error(`SelectElement.buildComponent(): noOption must have "display" and "value" properties.`); return; }

        // If there's a label text specified, add it as the first option.
        let options = labelText == null ? `` : `<option value="" selected disabled>${labelText}</option>`;

        // If allOption is not null, add an _ALL option.
        options += allOption != null ? `<option value="${allOption[`value`]}">${allOption[`display`]}</option>` : ``;

        hasNullOption = false;

        if (valueColumn == null) {
            // If valueColumn is null, straightforward use the value of each item.
            data.forEach(function (item) {
                if (item != null) {
                    options += `<option value="${item}">${item}</option>`;
                } else {
                    hasNullOption = true;
                }
            });
        } else if (typeof displayColumn === `string`) {
            // If displayColumn is an instance of String.
            data.forEach(function (item) {
                if (__fnGetPropertyValue(item, valueColumn) !== null) {
                    options += `<option value="${__fnGetPropertyValue(item, valueColumn)}">${__fnGetPropertyValue(item, displayColumn)}</option>`;
                } else {
                    hasNullOption = true;
                }
            });
        } else {
            // If displayColumn is an instance of Array.
            data.forEach(function (item) {
                // The first on displayColumn.
                let optionText = __fnGetPropertyValue(item, displayColumn[0]);

                // Second and onwards.
                for (let i = 1; i < displayColumn.length; i++) {
                    optionText += ` (${__fnGetPropertyValue(item, displayColumn[i])})`;
                }

                // Display will be in "<first> (<second>) (<n>)" format.
                options += `<option value="${__fnGetPropertyValue(item, valueColumn)}">${optionText}</option>`;
            });
        }

        if (noOption != null) {
            // If noOption is not null, add an empty option.
            options += `<option value="${noOption[`value`]}">${noOption[`display`]}</option>`;
        }
        else if (hasNullOption) {
            options += `<option value="">No ${displayColumn}</option>`;
        }

        $(selector).empty().append(options);
    };

    return buildComponent(setConfiguration(x));
})(configuration);
