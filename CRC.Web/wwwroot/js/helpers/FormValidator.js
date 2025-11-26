/**
 * To validate all visible inputs within a form element. 
 * Returns true if all inputs are valid. Otherwise, false.
 * Sample: _formValidator(`#form-application`);
 * @author   Azizan Haniff
 * @param    {string} selector The selector of a form element. e.g. `#form-application`.
 * @return   {boolean}
*/
const _formValidator = selector => (function (selector = null) {
    let validate = function (selector = null) {
        if (selector == null) { console.error(`FormValidator.validate(): selector not defined.`); return; }
        if ($(selector).length == 0) { console.error(`FormValidator.validate(): ${selector} not found.`); return; }
        if (!$(selector).is(`form`)) { console.error(`FormValidator.validate(): ${selector} is not a form element.`); return; }

        let hasFocused = false;
        let validated = [];
        let elements = $(selector).find(`:input`).not(`[type="hidden"], button`).toArray();
        elements.forEach(function (element) {
            if (!validated.includes(element.id) && !element.hasAttribute(`disabled`) && !element.hasAttribute(`readonly`) && !$(element).hasClass(`js-choice`)) {
                validated.push(element.id);

                let check = checkTagName(element);
                if (!hasFocused && !check) {
                    $(element).focus();
                    hasFocused = true;
                }
            }
        });

        // For choices.js
        elements = $(selector).find(`select.form-input.js-choice`).toArray();
        elements.forEach(function (element) {
            if (!validated.includes(element.id)) {
                validated.push(element.id);

                let check = checkChoices(element);
                if (!hasFocused && !check) {
                    $(element).parent().children().last().focus();
                    hasFocused = true;
                }
            }
        });

        return !hasFocused;
    };

    let checkTagName = function (element) {
        switch (element.tagName) {
            case `INPUT`:
                return checkInput(element);
                break;
            case `TEXTAREA`:
                return checkTextarea(element);
                break;
            case `SELECT`:
                return checkSelect(element);
                break;
            default:
                return true;
                break;
        }
    }

    let checkInput = function (element) {
        switch (element.type) {
            case `text`:
                return checkText(element);
                break;
            case `number`:
                return checkNumber(element);
                break;
            case `checkbox`:
                return checkCheckbox(element);
                break;
            case `radio`:
                return checkRadio(element);
                break;
            default:
                return true;
                break;
        }
    }

    let checkText = function (element) {
        $(element).removeClass(`is-invalid is-valid`);

        if (element.hasAttribute(`required`) && ($(element).val() === null || $(element).val().trim().length === 0)) {
            $(element).parent().find(`.invalid-feedback`).text(`Required!`);
            $(element).addClass(`is-invalid`);
            return false;
        } else if (element.hasAttribute(`minlength`) && $(element).val().trim().length < $(element).attr(`minlength`)) {
            $(element).parent().find(`.invalid-feedback`).text(`Minimum length is ${$(element).attr(`minlength`)}!`);
            $(element).addClass(`is-invalid`);
            return false;
        }

        $(element).addClass(`is-valid`);
        return true;
    }

    let checkNumber = function (element) {
        $(element).removeClass(`is-invalid is-valid`);

        if ($(element).val().length === 0) {
            $(element).parent().find(`.invalid-feedback`).text(`Not a number!`);
            $(element).addClass(`is-invalid`);
            return false;
        } else if (element.hasAttribute(`required`) && ($(element).val().length === 0)) {
            $(element).parent().find(`.invalid-feedback`).text(`Required!`);
            $(element).addClass(`is-invalid`);
            return false;
        } else if (element.hasAttribute(`min`) && parseFloat($(element).val()) < $(element).attr(`min`)) {
            $(element).parent().find(`.invalid-feedback`).text(`Minimum value is ${$(element).attr(`min`)}!`);
            $(element).addClass(`is-invalid`);
            return false;
        } else if (element.hasAttribute(`max`) && parseFloat($(element).val()) > $(element).attr(`max`)) {
            $(element).parent().find(`.invalid-feedback`).text(`Maximum value is ${$(element).attr(`max`)}!`);
            $(element).addClass(`is-invalid`);
            return false;
        }

        $(element).addClass(`is-valid`);
        return true;
    }

    let checkCheckbox = function (element) {
        $(`[name="${element.name}"]`).removeClass(`is-invalid is-valid`);

        if ($(`[name="${element.name}"]`).attr(`required`) !== undefined && $(`[name="${element.name}"]:checked`).length === 0) {
            $(`[name="${element.name}"]`).addClass(`is-invalid`);
            return false;
        }

        $(`[name="${element.name}"]`).addClass(`is-valid`);
        return true;
    }

    let checkRadio = function (element) {
        $(`[name="${element.name}"]`).removeClass(`is-invalid is-valid`);

        if ($(`[name="${element.name}"]`).attr(`required`) !== undefined && $(`[name="${element.name}"]:checked`).length === 0) {
            $(`[name="${element.name}"]`).addClass(`is-invalid`);
            return false;
        }

        $(`[name="${element.name}"]`).addClass(`is-valid`);
        return true;
    }

    let checkTextarea = function (element) {
        $(element).removeClass(`is-invalid is-valid`);

        if (element.hasAttribute(`required`) && ($(element).val() === null || $(element).val().trim().length === 0)) {
            $(element).parent().find(`.invalid-feedback`).text(`Required!`);
            $(element).addClass(`is-invalid`);
            return false;
        } else if (element.hasAttribute(`minlength`) && $(element).val().trim().length < $(element).attr(`minlength`)) {
            $(element).parent().find(`.invalid-feedback`).text(`Minimum length is ${$(element).attr(`minlength`)}!`);
            $(element).addClass(`is-invalid`);
            return false;
        }

        $(element).addClass(`is-valid`);
        return true;
    }

    let checkSelect = function (element) {
        $(element).removeClass(`is-invalid is-valid`);

        if (element.hasAttribute(`required`)) {
            if (element.hasAttribute(`multiple`) && $(element).val().length === 0) {
                $(element).parent().find(`.invalid-feedback`).text(`Required!`);
                $(element).addClass(`is-invalid`);
                return false;
            } else if ($(element).val() === null) {
                $(element).parent().find(`.invalid-feedback`).text(`Required!`);
                $(element).addClass(`is-invalid`);
                return false;
            }
        }

        $(element).addClass(`is-valid`);
        return true;
    }

    let checkChoices = function (element) {
        $(element).parents(`:eq(1)`).removeClass(`is-invalid is-valid`);

        if (element.hasAttribute(`required`) && $(element).children().length === 0) {
            $(element).parents(`:eq(2)`).find(`.invalid-feedback`).text(`Required!`);
            $(element).parents(`:eq(1)`).addClass(`is-invalid`);
            return false;
        }

        $(element).parents(`:eq(1)`).addClass(`is-valid`);
        return true;
    }

    let isValidInput = function (elements, min = null, max = null) {
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
    }

    return validate(selector);
})(selector);
