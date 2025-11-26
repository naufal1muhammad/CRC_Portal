/**
 * To create a card component that contains a list group component.
 * @author   Azizan Haniff
*/
const _listGroup = configuration => (function () {
    let configurations = {};

    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "resource": (x.resource !== undefined) ? x.resource : null,
            "title": (x.title !== undefined) ? x.title : null,
            "idColumn": (x.idColumn !== undefined) ? x.idColumn : `id`,
            "mainColumn": (x.mainColumn !== undefined) ? x.mainColumn : null,
            "subColumns": (x.subColumns !== undefined) ? x.subColumns : [],
            "anchors": (x.anchors !== undefined) ? x.anchors : [],
            "inputTag": (x.inputTag !== undefined) ? x.inputTag : false
        };
    };

    /**
     * Build the component.
     * @param    {object} x
     * @param    {string} x.selector                    The selector to build the component. e.g. `#divId`.
     * @param    {object[]|Resource} x.resource         This can either be a Resource object, or an array of object.
     * @param    {null|string} [x.title]                Title for the card.
     * @param    {string} x.idColumn                    The column to use as Id. Defaults to `id` if not specified.
     * @param    {string} x.mainColumn                  The column to display in a bolded style.
     * @param    {null|string[]} [x.subColumns]         The column(s) to display after the mainColumn.
     * @param    {object} [x.anchors]                   Anchors to add in either `header` or `listItem`.
     * @param    {boolean} [x.inputTag]                 To add input tag inside list group item or not. Defaults to false.
     * @return   {void|Promise<Response>}
    */
    let buildComponent = function (x) {
        if (x.selector == null) { console.error(`ListGroup.buildComponent(): selector not defined.`); return; }
        if ($(x.selector).length == 0) { console.error(`ListGroup.buildComponent(): ${x.selector} not found.`); return; }
        if (x.resource == null) { console.error(`ListGroup.buildComponent(): resource not defined.`); return; }
        if (x.mainColumn == null) { console.error(`ListGroup.buildComponent(): mainColumn not defined.`); return; }

        if (Array.isArray(x.resource)) {
            builder(x.selector, x.resource, x.title, x.idColumn, x.mainColumn, x.subColumns, x.anchors, x.inputTag);
        } else {
            return x.resource.fetch()
                .then(response => response.json())
                .then(data => builder(x.selector, data, x.title, x.idColumn, x.mainColumn, x.subColumns, x.anchors, x.inputTag))
                .catch(error => console.error(error));
        }
    }

    /**
     * To append an item or a list of items to a list group.
     * @param    {object} x
     * @param    {string} x.selector                The selector to append items. e.g. `#divId`.
     * @param    {object[]|Resource} x.resource     This can either be a Resource object, or an array of object.
     * @return   {void|Promise<Response>}
    */
    let append = function (x) {
        if (x.selector == null) { console.error(`ListGroup.append(): selector not defined.`); return; }
        if (configurations[x.selector] === undefined) { console.error(`ListGroup.append(): No existing ListGroup component ${x.selector} built.`); return; }
        if (x.resource == null) { console.error(`ListGroup.append(): resource not defined.`); return; }

        let configuration = configurations[x.selector];

        if (Array.isArray(x.resource)) {
            appender(x.selector, x.resource, configuration.idColumn, configuration.mainColumn, configuration.subColumns, configuration.anchors, configuration.inputTag);
        } else {
            return x.resource.fetch()
                .then(response => response.json())
                .then(data => appender(x.selector, data, configuration.idColumn, configuration.mainColumn, configuration.subColumns, configuration.anchors, configuration.inputTag))
                .catch(error => console.error(error));
        }
    }

    /**
     * To remove an item or a list of items from a list group.
     * @param    {object} x
     * @param    {string} x.selector                The selector to remove items. e.g. `#divId`.
     * @param    {object[]|Resource} x.resource     This can either be a Resource object, or an array of object.
     * @return   {void|Promise<Response>}
    */
    let remove = function (x) {
        if (x.selector == null) { console.error(`ListGroup.remove(): selector not defined.`); return; }
        if (configurations[x.selector] === undefined) { console.error(`ListGroup.remove(): No existing ListGroup component ${x.selector} built.`); return; }
        if (x.resource == null) { console.error(`ListGroup.remove(): resource not defined.`); return; }

        let configuration = configurations[x.selector];

        if (Array.isArray(x.resource)) {
            remover(x.selector, x.resource, configuration.idColumn);
        } else {
            return x.resource.fetch()
                .then(response => response.json())
                .then(data => remover(x.selector, data, configuration.idColumn))
                .catch(error => console.error(error));
        }
    };

    let builder = function (selector, data, title, idColumn, mainColumn, subColumns, anchors, inputTag) {
        if (!Array.isArray(data)) { console.error(`ListGroup.builder(): data expects an array of objects.`); return; }
        if (data.length > 0) {
            if (__fnGetPropertyValue(data[0], idColumn) === undefined) { console.error(`ListGroup.builder(): ${idColumn} not found in object.`); return; }
            if (__fnGetPropertyValue(data[0], mainColumn) === undefined) { console.error(`ListGroup.builder(): ${mainColumn} not found in object.`); return; }
            subColumns.forEach(function (subColumn) { if (__fnGetPropertyValue(data[0], subColumn) === undefined) { console.error(`ListGroup.builder(): ${subColumn} not found in object.`); return; } });
        }

        // To exclude `#` or `.` in selector.
        let elementId = selector.substring(1);

        let component = ``;
        component += buildHeader(elementId, title, anchors.header);
        component += buildListGroup(elementId, data, idColumn, mainColumn, subColumns, anchors.listItem, inputTag);

        // Append the component.
        $(selector)
            .addClass(`card card-light h-100 mb-0`)
            .empty()
            .append(component);

        bindHeaderEventHandler(elementId, anchors.header);
        bindListItemEventHandler(elementId, anchors.listItem);

        // On successful build, store the configuration.
        configurations[selector] = {
            "idColumn": idColumn,
            "mainColumn": mainColumn,
            "subColumns": subColumns,
            "anchors": anchors,
            "inputTag": inputTag
        };
    }

    function appender(selector, data, idColumn, mainColumn, subColumns, anchors, inputTag) {
        if (!Array.isArray(data)) { console.error(`ListGroup.appender(): data expects an array of objects.`); return; }
        if (data.length > 0) {
            if (__fnGetPropertyValue(data[0], idColumn) === undefined) { console.error(`ListGroup.appender(): ${idColumn} not found in object.`); return; }
            if (__fnGetPropertyValue(data[0], mainColumn) === undefined) { console.error(`ListGroup.appender(): ${mainColumn} not found in object.`); return; }
            subColumns.forEach(function (subColumn) { if (__fnGetPropertyValue(data[0], subColumn) === undefined) { console.error(`ListGroup.appender(): ${subColumn} not found in object.`); return; } });
        }

        // To exclude `#` or `.` in selector.
        let elementId = selector.substring(1);

        // Before append, filter.
        let filteredData = [];
        data.forEach(function (data) {
            if ($(`${selector}-${data[idColumn]}`).length == 0) { filteredData.push(data); }
        });

        let component = buildListGroupItems(elementId, filteredData, idColumn, mainColumn, subColumns, anchors.listItem, inputTag);

        $(selector)
            .find(`ul.list-group`)
            .append(component);

        bindListItemEventHandler(elementId, anchors.listItem);
    }

    function remover(selector, data, idColumn) {
        if (!Array.isArray(data)) { console.error(`ListGroup.remover(): data expects an array of objects.`); return; }
        if (data.length > 0) {
            if (__fnGetPropertyValue(data[0], idColumn) === undefined) { console.error(`ListGroup.remover(): ${idColumn} not found in object.`); return; }
        }

        data.forEach(function (data) {
            $(`${selector}-${__fnGetPropertyValue(data, idColumn)}`).remove();
        });
    }

    function buildHeader(elementId, title, anchors) {
        let component = ``;

        if (title != null || (anchors !== undefined && anchors.length > 0)) {
            component += `<div class="card-header">`;
            component += buildHeaderTitle(title);
            component += buildHeaderAnchors(elementId, anchors);
            component += `</div>`;
        }

        return component;
    }

    function buildHeaderTitle(title) {
        return (title != null) ? `<span class="card-title">${title}</span>` : ``;
    }

    function buildHeaderAnchors(elementId, anchors) {
        let component = ``;

        if (anchors !== undefined && anchors.length > 0) {
            component += `<span class="float-right">`;
            for (let i = 0; i < anchors.length; i++) {
                component += `<a href="${anchors[i].href}" id="${elementId}-headerAnchor${i + 1}" class="${anchors[i].color} px-2"><i class="${anchors[i].icon}"></i></a>`;
            }
            component += `</span>`;
        }

        return component;
    }

    function buildListGroup(elementId, data, idColumn, mainColumn, subColumns, anchors, inputTag) {
        let component = ``;

        component += `<ul class="list-group list-group-flush">`;
        if (data.length > 0) {
            component += buildListGroupItems(elementId, data, idColumn, mainColumn, subColumns, anchors, inputTag);
        }
        component += `</ul>`;

        return component;
    }

    function buildListGroupItems(elementId, data, idColumn, mainColumn, subColumns, anchors, inputTag) {
        let component = ``;

        data.forEach(function (item) {
            component += `<li class="list-group-item" id="${elementId}-${__fnGetPropertyValue(item, idColumn)}">`;
            component += buildListGroupText(elementId, item, idColumn, mainColumn, subColumns);
            component += buildListGroupAnchors(elementId, anchors);
            component += buildInputTag(elementId, item, idColumn, inputTag);
            component += `</li>`;
        });

        return component;
    }

    function buildListGroupText(elementId, item, idColumn, mainColumn, subColumns) {
        let component = ``;

        component += `<span>`;
        component += `<b id="${elementId}-${__fnGetPropertyValue(item, idColumn)}-mainColumn">${__fnGetPropertyValue(item, mainColumn)}</b>`;
        if (subColumns.length > 0) {
            component += `<span id="${elementId}-${__fnGetPropertyValue(item, idColumn)}-subColumns" class="ml-2">`;
            subColumns.forEach(function (subColumn) { component += __fnGetPropertyValue(item, subColumn) + ` `; });
            component += `</span>`;
        }
        component += `</span>`;

        return component;
    }

    function buildListGroupAnchors(elementId, anchors) {
        let component = ``;

        if (anchors !== undefined && anchors.length > 0) {
            component += `<span class="float-right">`;
            for (let i = 0; i < anchors.length; i++) {
                component += `<a href="${anchors[i].href}" class="${elementId}-listItemAnchor${i + 1} ${anchors[i].color} px-2"><i class="${anchors[i].icon}"></i></a>`;
            }
            component += `</span>`;
        }

        return component;
    }

    function buildInputTag(elementId, item, idColumn, inputTag) {
        return (inputTag) ? `<input type="hidden" name="${elementId}" value="${item[idColumn]}">` : ``;
    }

    function bindHeaderEventHandler(elementId, anchor) {
        if (anchor !== undefined) {
            for (let i = 0; i < anchor.length; i++) {
                $(`#${elementId}-headerAnchor${i + 1}`).unbind();
                $(`#${elementId}-headerAnchor${i + 1}`).click(function (event) {
                    event.preventDefault();
                    anchor[i].onClick($(this));
                });
            }
        }
    }

    function bindListItemEventHandler(elementId, anchor) {
        if (anchor !== undefined) {
            for (let i = 0; i < anchor.length; i++) {
                $(`.${elementId}-listItemAnchor${i + 1}`).unbind();
                $(`.${elementId}-listItemAnchor${i + 1}`).click(function (event) {
                    event.preventDefault();
                    let id = $(this).parents(`:eq(1)`).attr(`id`).split(`${elementId}-`)[1];
                    anchor[i].onClick(id, $(this));
                });
            }
        }
    }

    return {
        "buildComponent": function (x) { return buildComponent(setConfiguration(x)); },
        "append": function (x) { return append(setConfiguration(x)); },
        "remove": function (x) { return remove(setConfiguration(x)); }
    };
})(configuration);
