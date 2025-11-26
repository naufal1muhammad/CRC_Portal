$(function () {
    const buttonArray = [
        `-table-header-buttons-`,
        `-detail-header-buttons-`,
        `-table-buttons-`
    ];

    for (let i = 0; i < buttonArray.length; i++) {
        $(`button[class*="${buttonArray[i]}"]`).click(function () {
            let classes = $(this).attr(`class`).split(` `);
            $.each(classes, function (index, value) {
                if (value.includes(`${buttonArray[i]}`)) {
                    data(value);
                }
            });
        });
    }

    $(`button[class*="back-to-table-section"]`).click(function () {
        const element = $(this).attr(`name`).split(`-`);
        const pageName = camelPad(element[0]);
        const action = element[1];

        appendText("", action, pageName);
    });
});

function data(attr) {
    const arr = attr.split(`-`);

    const name = arr[0];
    const action = arr[arr.length - 1];

    const pageName = camelPad(name);
    const elementId = `${name}-${name}Id`;
    const idValue = $(`#${elementId}`).val();

    appendText(idValue, action, pageName);
}

function camelPad(str) {
    return str
        // Look for long acronyms and filter out the last letter
        .replace(/([A-Z]+)([A-Z][a-z])/g, ' $1 $2')
        // Look for lower-case letters followed by upper-case letters
        .replace(/([a-z\d])([A-Z])/g, '$1 $2')
        // Look for lower-case letters followed by numbers
        .replace(/([a-zA-Z])(\d)/g, '$1 $2')
        .replace(/^./, function (str) { return str.toUpperCase(); })
        // Remove any white space left around the word
        .trim();
}

function appendText(id, action, pageName) {
    let logInfo = {
        "id": id,
        "action": action,
        "pageName": pageName
    };
    new Resource(`api/smartapps/Logger`, { "method": `post`, "body": logInfo })
        .fetch()
        .then()
        .catch(error => console.error(error));
}