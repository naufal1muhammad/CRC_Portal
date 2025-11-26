$(`div.modal[id$="-modal"]`).on(`shown.bs.modal`, function () {
    $(`#${$(this).attr(`id`)} .form-input`).not(`disabled`).first().focus();
});

function showModal(modal, label = `Modal`, button = `create`) {
    let bgColor = button == `create` ? `primary` : `success`;
    let textColor = `light`;

    let icon = `fas fa-plus`;
    if (button == `save`) {
        icon = `fas fa-save`;
    } else if (button == `delete`) {
        icon = `fas fa-trash`;
    }

    $(`#${modal._element.id}Label`).parent().removeClass(function (index, className) {
        return (className.match(/(^|\s)bg-\S+/g) || []).join(` `);
    });
    $(`#${modal._element.id}Label`).parent().addClass(`bg-${bgColor}`);
    $(`#${modal._element.id}Label`).addClass(`text-${textColor}`).text(label);

    $(`#${modal._element.id}Card`).removeClass(function (index, className) {
        return (className.match(/(^|\s)text-bg-\S+/g) || []).join(` `);
    });
    $(`#${modal._element.id}Card`).addClass(`text-bg-${bgColor}`);

    if (button == `create` && $(`#${modal._element.id}-buttons-${button}`).length == 0) {
        button = `add`;
    }

    $(`#${modal._element.id} button[id^="${modal._element.id}-buttons"]`).addClass(`d-none`);
    $(`#${modal._element.id}-buttons-${button}`).removeClass(`d-none`);

    $(`#${modal._element.id}LabelIcon`).empty().append(`<i class="${icon}"></i>`).addClass(`text-${textColor}`);

    modal.show();
}

function resetFormValidation(selector, type = `create`) {
    $(`${selector} input, ${selector} textarea`).not(`[type="hidden"], [type="checkbox"], [type="radio"]`).removeClass(`is-valid is-invalid`);
    $(`${selector} input[type="checkbox"], ${selector} input[type="radio"]`).removeClass(`is-valid is-invalid`);
    $(`${selector} select`).each(function () { $(this).removeClass(`is-valid is-invalid`); });

    if (type == `create`) {
        $(`${selector} input, ${selector} textarea`).not(`[type="hidden"], [type="checkbox"], [type="radio"]`).val(``);
        $(`${selector} input[type="checkbox"], ${selector} input[type="radio"]`).removeAttr(`disabled`).prop(`checked`, false);
        $(`${selector} select`).each(function () { $(this).val($(this).find(`option:first`).val()); });
        $(`${selector} .form-input`).each(function () {
            $(this).addClass(`form-control`).removeClass(`form-control-plaintext`).removeAttr(`readonly disabled`);
        });
    } else if (type == `update`) {
        $(`${selector} .form-input`).each(function () {
            $(this).addClass(`form-control-plaintext`).removeClass(`form-control`);
            $(this).prop(`tagName`) == `SELECT` || $(this).hasClass(`datetimepicker`) ?
                $(this).attr(`disabled`, `disabled`) :
                $(this).attr(`readonly`, `readonly`);
        });
    }
}