const _dateElement = configuration => (function () {
    let setConfiguration = function (x) {
        return {
            "selector": (x.selector !== undefined) ? x.selector : null,
            "targets": (x.targets !== undefined) ? x.targets : [x.selector],
            "startDate": (x.startDate !== undefined) ? x.startDate : moment(),
            "format": (x.format !== undefined) ? x.format : `yyyy-mm-dd`,
            "changeDate": (x.changeDate !== undefined) ? x.changeDate : function () { alert($(selector).datepicker(`getFormattedDate`)); }
        };
    };

    /**
    * To initialize an Input element as Datepicker (uxsolutions's bootstrap-datepicker).
    * HTML must be: <input type="text" id="<selector>">
    * @author   Azizan Haniff
    * @param    {object} x
    * @param    {string} x.selector         Selector for <input> element, e.g. `#mySelectId`.
    * @param    {string[]} [x.targets]      Input targets to assign the selected date. Defaults to using selector value. 
    * @param    {string} [x.startDate]      Date with string format. Defaults to using moment().
    * @param    {string} [x.format]         Date format. Defaults to `yyyy-mm-dd`.
    * @return   {void}
    */
    let asInput = function (x) {
        if (x.selector == null) { console.error(`DateElement.asInput(): selector is required.`); return; }
        if ($(x.selector).length == 0) { console.error(`DateElement.asInput(): ${x.selector} not found.`); return; }
        if (x.targets.length == 0) { console.error(`DateElement.asInput(): targets cannot be empty.`); return; }
        if (!$(x.selector).hasClass(`form-control`)) { $(x.selector).addClass(`form-control`); }

        initialize(x.selector, x.targets, x.format);
        assignDate(x.selector, x.targets, x.startDate);
    };

    /**
    * To initialize an Div or some container element as Datepicker (uxsolutions's bootstrap-datepicker).
    * HTML can be: <div id="<selector>"></div>
    * To get the value, use $(selector).datepicker(`getFormattedDate`); }
    * @author   Azizan Haniff
    * @param    {object} x
    * @param    {string} x.selector         Selector for <input> element, e.g. `#mySelectId`.
    * @param    {string[]} [x.targets]      Input targets to assign the selected date. Defaults to using selector value.
    * @param    {string} [x.startDate]      Date with string format. Defaults to using moment().
    * @param    {string} [x.format]         Date format. Defaults to `yyyy-mm-dd`.
    * @param    {Function} [x.changeDate]   NOT YET IMPLEMENTED. A function to execute when changeDate event is called.
    * @return   {void}
    */
    let asInline = function (x) {
        if (x.selector == null) { console.error(`DateElement.asInline(): selector is required.`); return; }
        if ($(x.selector).length == 0) { console.error(`DateElement.asInline(): ${x.selector} not found.`); return; }
        if (x.targets.length == 0) { console.error(`DateElement.asInline(): targets cannot be empty.`); return; }
        if (!$(x.selector).hasClass(`container`)) { $(x.selector).addClass(`container`); }
        if (!$(x.selector).hasClass(`d-flex`)) { $(x.selector).addClass(`d-flex`); }
        if (!$(x.selector).hasClass(`justify-content-center`)) { $(x.selector).addClass(`justify-content-center`); }

        initialize(x.selector, x.targets, x.format);
        assignDate(x.selector, x.targets, x.startDate);
    };

    let initialize = function (selector, targets, format) {
        if (!$(selector).data(`datepicker`)) {
            $(selector).unbind();

            $(selector).datepicker({
                "format": format,
                "todayBtn": `linked`,
                "todayHighlight": true,
                "zIndexOffset": 1035,
            });

            $(selector).on(`changeDate`, function () {
                for (let i = 0; i < targets.length; i++) {
                    if ($(targets[i]).length > 0) {
                        $(targets[i]).val($(selector).datepicker(`getFormattedDate`));
                    }
                }
            });
        }
    };

    let assignDate = function (selector, targets, startDate) {
        $(selector).val(moment(startDate).format(`YYYY-MM-DD`));
        $(selector).datepicker(`update`, moment(startDate).format(`YYYY-MM-DD`));

        for (let i = 0; i < targets.length; i++) {
            if ($(targets[i]).length > 0) {
                $(targets[i]).val($(selector).datepicker(`getFormattedDate`));
            }
        }
    };

    return {
        "asInput": function (x) { asInput(setConfiguration(x)); },
        "asInline": function (x) { asInline(setConfiguration(x)); }
    };
})(configuration);
