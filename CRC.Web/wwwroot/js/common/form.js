(function (_form, $, undefined) {
    _form.url = ``;
    _form.original = [];
    _form.name = ``;
    _form.displayName = ``;
    _form.item = ``;
    _form.list = ``;

    _form.setUrl = function (url) {
        _form.url = url.split(`#`)[0];
    };

    _form.putOriginal = function (key, value) {
        _form.original[key] = value;
    };

    _form.setName = function (name) {
        _form.name = name;
        _form.displayName = name;
    };

    _form.setDisplayName = function (displayName) {
        _form.displayName = displayName;
    };

    _form.setItem = function (item) {
        _form.item = item;
    };

    _form.reset = function (flag = true) {
        if (flag) {
            $(`form[name="form-${_form.name}"]`)
                .find(`input[type="text"],input[type="number"], select, textarea`)
                .removeClass(`is-valid is-invalid`)
                .val(``);

            $(`input[type="checkbox"]`).prop(`checked`, false);

            $(`input[type="radio"]`).prop(`checked`, function () {
                return this.getAttribute(`checked`) === `checked`;
            });

            $(`form[name="form-${_form.name}"] :input:enabled:visible:first`).focus();
        }
    };

    _form.setInputAsDate = function (name, startDate = moment()) {
        $(`input[name="${_form.name}-${name}"]`).val(moment(startDate).format(`DD MMMM YYYY`));
        $(`input[name="${_form.name}-${name}"]`).datepicker({
            "format": `dd mmmm yyyy`,
            "showOnFocus": false,
            "uiLibrary": `bootstrap4`,
            "value": moment(startDate).format(`DD MMMM YYYY`)
        });

        if ($(`input[name="${_form.name}-${name}"]`).parent().children().length === 2) {
            $(`input[name="${_form.name}-${name}"]`).parent().append(`<div class="invalid-feedback"></div>`);
        }
    };

    _form.setInputAsRadio = function (name, options = true) {
        $(`input[id="${_form.name}-${name}-${options.toString().toLowerCase()}"]`).prop(`checked`, true);
    };

    _form.toTable = function () {
        let tableContainer = $(`div[id="${_form.name}-tableContainer"]`);
        tableContainer.removeClass(`d-none`);

        let detail = $(`div[id="${_form.name}-detail"]`);
        detail.addClass(`d-none`);

        $(`#menu`).removeClass(`d-none`);
    };

    _form.toDetail = function () {
        _form.list = ``;
        $(`div[id$="-list"]`).addClass(`d-none`);

        let tableContainer = $(`div[id="${_form.name}-tableContainer"]`);
        tableContainer.addClass(`d-none`);

        let detail = $(`div[id="${_form.name}-detail"]`);
        detail.removeClass(`d-none`);

        $(`#menu`).removeClass(`d-none`);
    };

    _form.addEntry = function () {
        if (_form.list === ``) {
            _form.toDetail();

            let detail = $(`div[id="${_form.name}-detail"]`);
            detail.find(`.card-header .float-right`).addClass(`d-none`);
            detail.find(`#${_form.name}-header`).children(`:last`).text(setTextAsTitle(`Add new entry`));
            detail.find(`#${_form.name}-header`).text(setTextAsTitle(`Add new entry`));

            _form.createInfo();
            _form.modifyInfo();

            _form.reset();
            redrawButton([`add`]);

            $(`#menu`).addClass(`d-none`);
        } else {
            $(`#${_form.list}Modal`).modal(`show`);
        }
    };

    _form.editEntry = function (title, resetFlag = true, excludeDeleteBtn = false) {
        _form.toDetail();

        let detail = $(`div[id="${_form.name}-detail"]`);
        detail.find(`.card-header .float-right`).removeClass(`d-none`);
        detail.find(`#${_form.name}-header`).children(`:last`).text(setTextAsTitle(title));
        detail.find(`#${_form.name}-header`).text(setTextAsTitle(title));

        _form.reset(resetFlag);
        if (excludeDeleteBtn == false) {
            redrawButton([`edit`, `delete`]);
        } else {
            redrawButton([`edit`]);
        }

    };

    _form.readEntry = function (title) {
        _form.toDetail();

        let detail = $(`div[id="${_form.name}-detail"]`);
        detail.find(`#${_form.name}-header`).children(`:last`).text(setTextAsTitle(title));
        detail.find(`#${_form.name}-header`).text(setTextAsTitle(title));

        _form.reset();
        redrawButton();
    };

    _form.showList = function (name) {
        $(`#${_form.name}-detail`).addClass(`d-none`);

        _form.list = name;
        let list = $(`div[id="${_form.list}-list"]`);
        list.find(`#${_form.list}-title`).children(`:last`).text(setTextAsTitle(`${_form.list} associated with ${_form.displayName}`));
        list.find(`#${_form.list}-title`).text(setTextAsTitle(`${_form.list} associated with ${_form.displayName}`));
        list.removeClass(`d-none`);
    }

    _form.createInfo = function (createdAt = null, createdBy = null) {
        let element = ``;

        if (createdAt != null) {
            element += `Created on <span id="createdAt">${moment(createdAt).format(`dddd, MMMM Do YYYY, h:mm:ss a`)}</span>`;

            if (createdBy != null) {
                element += ` by <span id="createdBy">${createdBy}</span>`;
            }
        }

        $(`#create-info`).empty().append(element).addClass(`font-italic small text-muted float-left p-1`);
    }

    _form.modifyInfo = function (modifiedAt = null, modifiedBy = null) {
        let element = ``;

        if (modifiedAt != null) {
            element += `Updated on <span id="modifiedAt">${moment(modifiedAt).format(`dddd, MMMM Do YYYY, h:mm:ss a`)}</span>`;

            if (modifiedBy != null) {
                element += ` by <span id="modifiedBy">${modifiedBy}</span>`;
            }
        }

        $(`#modify-info`).empty().append(element).addClass(`font-italic small text-muted float-right p-1`);
    }

    _form.isValidInput = function (elements, min = null, max = null) {
        var isValid = true;

        for (let i = 0; i < elements.length; i++) {
            let element = elements.eq(i);

            element.removeClass(`is-invalid`);

            if (element.val() === null || element.val().trim().length === 0) {
                if (i === 0) {
                    isValid = false;
                    element.parent().find(`.invalid-feedback`).text(`Required!`);
                    element.addClass(`is-invalid`);
                    element.focus();
                } else {
                    element.remove();
                }
            } else if (elements.eq(0).val().length === 0) {
                isValid = true;
                elements.eq(0).removeClass(`is-invalid`);
                elements.eq(0).addClass(`is-valid`);
                elements.eq(0).val(element.val().trim());
                element.remove();
            } else if ((min !== null || max !== null) && isNaN(element.val())) {
                isValid = false;
                element.parent().find(`.invalid-feedback`).text(`Value must be a number!`);
                element.addClass(`is-invalid`);
                element.focus();
            } else if (min !== null && element.val() < min) {
                isValid = false;
                element.parent().find(`.invalid-feedback`).text(`Minimum value is ${min}!`);
                element.addClass(`is-invalid`);
                element.focus();
            } else if (max !== null && element.val() > max) {
                isValid = false;
                element.parent().find(`.invalid-feedback`).text(`Maximum value is ${max}!`);
                element.addClass(`is-invalid`);
                element.focus();
            } else if (element.hasClass(`date-input`) && !moment(element.val(), `DD MMMM YYYY`, true).isValid()) {
                isValid = false;
                element.parent().find(`.invalid-feedback`).text(`Invalid date!`);
                element.addClass(`is-invalid`);
                element.focus();
            } else {
                element.val(element.val().trim());
                element.addClass(`is-valid`);
            }
        }

        return isValid;
    };

    _form.displayAsStatusIcon = function (id, name, data) {
        $(`tr[id="${_form.name}-${id}"] .${name}-data`).html(
            data.toString().toLowerCase() === `true`
                ? `<i class="fas fa-2x fa-check-circle text-success"><span hidden>Yes</span></i>`
                : `<i class="fas fa-2x fa-times-circle text-danger"><span hidden>No</span></i>`
        );
    }

    _form.populateSelectOptions = function (url, element, item = `name`) {
        fetch(`${url}?handler=json`, {
            headers: {
                "Accept": `application/json`,
                "Content-Type": `application/json`,
                "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
            }
        })
            .then(response => response.json())
            .then(data => {
                let options = `<option value="" selected disabled>Choose ${element}</option>`;
                for (let i = 0; i < data.length; i++) {
                    options += `<option value="${data[i].id}">${data[i][item]}</option>`;
                }

                $(`#${_form.name}-${element}`)
                    .empty()
                    .append(options);
            })
            .catch(function (error) { _toast.error(`Error in getting ${element} options: ${error}`); });
    }

    _form.populateSelectOptionsByResource = function (resource) {
        fetch(resource.url(), {
            headers: {
                "Accept": `application/json`,
                "Content-Type": `application/json`,
                "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
            }
        })
            .then(response => response.json())
            .then(data => {
                let options = `<option value="" selected disabled>Choose ${resource.elementName}</option>`;
                for (let i = 0; i < data.length; i++) {
                    options += `<option value="${data[i].id}">${data[i][resource.displayColumn]}</option>`;
                }

                $(`#${_form.name}-${resource.elementName}`)
                    .empty()
                    .append(options);
            })
            .catch(function (error) { _toast.error(`Error in getting ${resource.elementName} options: ${error}`); });
    }

    _form.populateCheckboxOptions = function (url, element, item = `name`, additionalQuery = {}) {
        if ($(`#${_form.name}-${element}-container`).length > 0) {

            query = ``;
            for (const [key, value] of Object.entries(additionalQuery)) {
                query += `&${key}=${value}`;
            }

            fetch(`${url}?handler=json${query}`, {
                headers: {
                    "Accept": `application/json`,
                    "Content-Type": `application/json`,
                    "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
                }
            })
                .then(response => response.json())
                .then(data => {
                    let options = ``;
                    for (let i = 0; i < data.length; i++) {
                        options += `
                            <div class="custom-control custom-checkbox custom-control-inline">
                                <input type="checkbox" class="custom-control-input" id="${_form.name}-${element}-${data[i].id}" name="${_form.name}-${element}" value="${data[i].id}">
                                <label class="custom-control-label" for="${_form.name}-${element}-${data[i].id}">${data[i][item]}</label>
                            </div>
                        `;
                    }

                    $(`#${_form.name}-${element}-container`)
                        .empty()
                        .append(options);
                })
                .catch(function (error) { _toast.error(`Error in getting ${element} options: ${error}`); });
        }
    }

    _form.populateCheckboxOptionsByResource = function (resource) {
        if ($(`#${_form.name}-${resource.elementName}-container`).length > 0) {
            fetch(resource.url(), {
                headers: {
                    "Accept": `application/json`,
                    "Content-Type": `application/json`,
                    "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
                }
            })
                .then(response => response.json())
                .then(data => {
                    let options = ``;
                    for (let i = 0; i < data.length; i++) {
                        options += `
                            <div class="custom-control custom-checkbox custom-control-inline">
                                <input type="checkbox" class="custom-control-input" id="${_form.name}-${resource.elementName}-${data[i].id}" name="${_form.name}-${resource.elementName}" value="${data[i].id}">
                                <label class="custom-control-label" for="${_form.name}-${resource.elementName}-${data[i].id}">${data[i][resource.displayColumn]}</label>
                            </div>
                        `;
                    }

                    $(`#${_form.name}-${resource.elementName}-container`)
                        .empty()
                        .append(options);
                })
                .catch(function (error) { _toast.error(`Error in getting ${resource.elementName} options: ${error}`); });
        }
    }

    _form.getSelectOptionText = function (element, value) {
        return $(`select[name="${_form.name}-${element}"] option[value="${value}"]`).text();
    }

    function redrawButton(buttons = []) {
        let buttonGroup = $(`#form-${_form.name}-buttonGroup`).length != 0
            ? $(`#form-${_form.name}-buttonGroup`)
            : $(`form[name="form-${_form.name}"] div.btn-group`);
        buttonGroup.empty();

        if (buttons.includes(`add`)) {
            buttonGroup.append(`
                <button type="button" class="btn btn-primary add-entry-button">
                    <i class="fas fa-plus mr-1"></i>
                    <span>Add</span>
                </button>
            `);

            $(`.add-entry-button`).unbind();
            $(`.add-entry-button`).click(function () {
                addRecord();
            });
        }
        if (buttons.includes(`edit`)) {
            buttonGroup.append(`
                <button type="button" class="btn btn-success edit-entry-button">
                    <i class="fas fa-save mr-1"></i>
                    <span>Save</span>
                </button>
            `);

            $(`.edit-entry-button`).unbind();
            $(`.edit-entry-button`).click(function () {
                editRecord();
            });
        }
        if (buttons.includes(`delete`)) {
            buttonGroup.append(`
                <button type="button" class="btn btn-danger delete-entry-button">
                    <i class="fas fa-trash mr-1"></i>
                    <span>Delete</span>
                </button>
            `);

            $(`.delete-entry-button`).unbind();
            $(`.delete-entry-button`).click(function () {
                deleteRecord();
            });
        }
    }

    function setTextAsTitle(text, uppercaseFirstLetterOnly = false) {
        if (uppercaseFirstLetterOnly) {
            return text.charAt(0).toUpperCase() + text.slice(1).toLowerCase();
        } else {
            return text.split(` `).map((word) => {
                return word.charAt(0).toUpperCase() + word.slice(1);
            }).join(` `);
        }
    }
}(window._form = window._form || {}, jQuery));

$(function () {
    $(document).on(`click`, `.to-table`, function (event) {
        event.preventDefault();

        _confirmationModal.customAction({
            "title": `Go Back To List`,
            "message": `Do you want to go back to list?`,
            "infoText": `This will remove all changes that you've made.`,
            "label": ``,
            "okProcess": function () {
                _form.toTable();
            }
        });
    });

    $(document).on(`click`, `.to-detail`, function (event) {
        event.preventDefault();
        _form.toDetail();
    });

    $(document).on(`click`, `.add-entry`, function (event) {
        event.preventDefault();
        _form.addEntry();
    });

    $(`button[id^="to-"][id$="-list"]`).click(function (event) {
        event.preventDefault();
        _form.showList($(this).attr(`id`).split(`-`)[1]);
    });
});
