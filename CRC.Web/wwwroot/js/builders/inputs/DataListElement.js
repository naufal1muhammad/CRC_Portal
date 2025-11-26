/**
 * To populate options of a DataList element with the result set received from a HTTP request.
 * @author   Azizan Haniff
 * @param    {object} configuration
 * @param    {string} configuration.selector                                        Selector for <datalist> element, e.g. `#mySelectId`.
 * @param    {object[]|Resource} configuration.resource                             This can either be a Resource object, or an array of object.
 * @param    {string} [configuration.valueColumn]                                   The column used as value of the <option> elements. Required only if response returns a list of objects.
 * @param    {string} [configuration.displayColumn]                                 The column used as display of the <option> elements. Required only if response returns a list of objects.
 * @param    {boolean} [configuration.refreshDataList]                              This is used to refresh record in data list. Default value is set to false.
 * @param    {object[]} [configuration.refreshUrl]                                  This is the resource for the refresh data list url.
 * @param    {number} [configuration.refreshLimit]                                  This is the limit to be display to avoid long or unresponsive response. Default value is set to 10000.
 * @return   {Promise<Response>}
*/
const _dataListElement = configuration => (function (x) {
    let refreshList;

    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "resource": (x.resource !== undefined) ? x.resource : null,
            "valueColumn": (x.valueColumn !== undefined) ? x.valueColumn : null,
            "displayColumn": (x.displayColumn !== undefined) ? x.displayColumn : x.valueColumn,
            "refreshDataList": (x.refreshDataList !== undefined) ? x.refreshDataList : false,
            "refreshUrl": (x.refreshDataList) ? x.refreshUrl : null,
            "refreshLimit": (x.refreshDataList) ? x.refreshLimit : 10000
        };
    };

    let buildComponent = function (x) {
        if (x.selector == null) { console.error(`DateListElement.buildComponent(): selector is required.`); return; }
        if ($(x.selector).length == 0) { console.error(`DateListElement.buildComponent(): ${x.selector} not found.`); return; }
        if (x.resource == null) { console.error(`DateListElement.buildComponent(): resource is required.`); return; }

        if (Array.isArray(x.resource)) {
            builder(x.selector, x.valueColumn, x.displayColumn, x.refreshDataList, x.refreshUrl, x.refreshLimit);
        } else {
            return x.resource.fetch()
                .then(response => response.json())
                .then(data => builder(x.selector, data, x.valueColumn, x.displayColumn, x.refreshDataList, x.refreshUrl, x.refreshLimit))
                .catch(error => console.error(error));
        }
    };

    let builder = function (selector, data, valueColumn, displayColumn, refreshDataList, refreshUrl, refreshLimit) {
        // A few checks if response is a list of objects.
        if (typeof data[0] === `object` && valueColumn === null) { console.error(`DateListElement.buildComponent(): valueColumn is required when response is a list of objects.`); return; }
        if (typeof data[0] === `object` && __fnGetPropertyValue(data[0], valueColumn) === undefined) { console.error(`DateListElement.buildComponent(): valueColumn not found in object.`); return; }

        let options = ``;

        if (valueColumn == null) {
            // If valueColumn is null, straightforward use the value of each item.
            data.forEach(function (item) { options += `<option value="${item}">`; });
        } else {
            // If displayColumn is an instance of String.
            data.forEach(function (item) { options += `<option value="${__fnGetPropertyValue(item, valueColumn)}">${__fnGetPropertyValue(item, displayColumn)}</option>`; });
        }

        $(selector).empty().append(options);

        if (refreshDataList && refreshUrl != null) {
            let elementId = selector.substring(1);

            registerEventHandler(elementId, selector, valueColumn, displayColumn, refreshDataList, refreshUrl, refreshLimit);
        }
    };

    let registerEventHandler = function (elementId, selector, valueColumn, displayColumn, refreshDataList, refreshUrl, refreshLimit) {
        $(`input[list="${elementId}"]`).on(`keyup`, function () {
            clearTimeout(refreshList);
            refreshList = setTimeout(function () {
                if ($(`input[list="${elementId}"]`).val().length != 0) {
                    $(`input[list="${elementId}"]`).css(`background`, `url(/img/mainapp-images/others/loaderIcon.gif) 165px center no-repeat rgb(255, 255, 255)`);

                    refreshUrl.query = { key: $(`input[list="${elementId}"]`).val() };
                    refreshUrl.fetch()
                        .then(response => response.json())
                        .then(data => {
                            if (data.length > refreshLimit) {
                                _toast.info(`Unable to update data list because data is more than ${refreshLimit} records`);
                            } else {
                                builder(selector, data, valueColumn, displayColumn, refreshDataList, refreshUrl, refreshLimit);
                            }
                        })
                        .catch(error => console.error(error))
                        .then(() => $(`input[list="${elementId}"]`).css(`background`, ``));
                }
            }, 3000);
        });
    };

    return buildComponent(setConfiguration(x));
})(configuration);
