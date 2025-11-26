let globalData;

function ApplicationGroupSelection(container = ``, rowData = null, colData = null, formName = ``, elementName = ``) {
    this.container = container;
    this.rowData = rowData;
    this.colData = colData;

    this.elementId = function (i, j) {
        return i + `-` + j;
    };

    this.elementName = elementName;

    this.generateElementName = function () {
        if (this.elementName == ``) {
            let name = formName + `-`;

            name += this.rowData.elementName + `-`;
            name += this.colData.elementName;

            this.elementName = name;
        }
    };

    this.render = function () {
        if (container === `` || rowData === null || colData === null) {
            console.error(`Unable to render matrix, missing required data.`);
            return;
        }

        this.generateElementName();

        Promise.all(this.fetches())
            .then(responses => {
                // Get a JSON object from each of the responses
                return Promise.all(responses.map(function (response) {
                    return response.json();
                }));
            })
            .then(data => {
                globalData = data;
                this.draw(data);
            })
            .catch(error => { console.log(error); });
    };

    this.fetches = function () {
        let list = [];

        list.push(
            fetch(this.rowData.url(), {
                headers: {
                    "Accept": `application/json`,
                    "Content-Type": `application/json`,
                    "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
                }
            })
        );

        list.push(
            fetch(this.colData.url(), {
                headers: {
                    "Accept": `application/json`,
                    "Content-Type": `application/json`,
                    "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
                }
            })
        );

        return list;
    };

    this.draw = function (data) {
        $(`#${this.container}`).empty().append(`
            <div class="card">
                <div class="table-responsive">
                    <table class="table table-striped text-center m-0">
                        ${this.drawThead(data)}
                        ${this.drawTbody(data)}
                    </table>
                </div>
            </div>
        `);
    };

    this.drawThead = function (data) {
        let thead = `<thead>`;
        thead += `<tr>`;
        thead += `<th class="align-middle">${this.rowData.name}</th>`;
        thead += `<th class="align-middle">${this.colData.name}</th>`;
        thead += `</tr>`;
        thead += `</thead>`;

        return thead;
    };

    this.drawTbody = function (data) {
        let tbody = `<tbody>`;
        for (let i = 0; i < data[0].length; i++) {
            let index = [];

            if (index.length == 0) {                
                tbody += `<tr class="text-center">`;
                tbody += `<td class="align-middle">${data[0][i][this.rowData.displayColumn]}</td>`;
                tbody += `
                            <td class="align-middle">
                                <div class="col-sm-12">
                                    <select class="custom-control custom-select" id="${this.elementName}-${data[0][i][`id`]}" name="${this.elementName}-${data[0][i][`id`]}" aria-describedby="${this.elementName}-${data[0][i][`id`]}Help">
                                    <option value="on" selected>Select application group..</option>`;

                let lastData = data[data.length - 1];

                for (let j = 0; j < lastData.length; j++) {
                    if (data[0][i].id == lastData[j].applicationId) {
                        tbody += `
                                    <option value="${data[0][i].id},${lastData[j].id}">${lastData[j].name}</option>
                    `;
                    }
                }
                tbody += `
                                    </select>
                                    <small id="${this.elementName}-${data[0][i][`id`]}Help" class="form-text text-muted"></small>
                                    <div class="invalid-feedback"></div>
                                </div>
                            </td>
                        </tr>`;
            }
        }

        tbody += `</tbody>`;

        return tbody;
    };

    this.post = function (resource, userId, toastMessage = false) {
        var records = [];

        let rows = this.rowData;
        let col = this.colData;

        for (let i = 0; i < globalData[0].length; i++) {
            if ($(`[name=${this.elementName}-${globalData[0][i][`id`]}]`).length > 0) {
                $.each($(`[name=${this.elementName}-${globalData[0][i][`id`]}]`), function () {
                    if ($(this).val() !== `on`) {
                        var record = {};

                        record[`${formName}Id`] = userId;

                        record[`${rows.elementName}Id`] = $(this).val().split(`,`)[0];

                        record[`${col.elementName}Id`] = $(this).val().split(`,`)[$(this).val().split(`,`).length - 1];

                        records.push(record);
                    }
                });
            } else {
                var record = {};
                record[`${formName}Id`] = userId;
                records.push(record);
            }

        }

        if (toastMessage) {
            $(`.formActionModal-button i.fa-spin`).removeClass(`d-none`);
        }

        fetch(resource.url(), {
            method: resource.method,
            headers: {
                "Accept": `application/json`,
                "Content-Type": `application/json`,
                "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
            },
            body: JSON.stringify(records)
        })
            .then(response => {
                if (toastMessage) {
                    if (response.ok) {
                        _toast.success(`Successfully update ${resource.elementName}!`);

                        $(`#formActionModal`).modal(`hide`);
                    } else {
                        _toast.warning(`Failed to update ${resource.elementName}.`);
                    }
                }
            })
            .catch(function (error) { _toast.error(`Error in posting link: ${error}`); })
            .then(() => {
                if (toastMessage) {
                    $(`.formActionModal-button i.fa-spin`).addClass(`d-none`);
                }
            });
    };

    this.reset = function () {
        for (let i = 0; i < globalData[0].length; i++) {
            $(`#${this.container} [name=${this.elementName}-${globalData[0][i][`id`]}]`).val(`on`);
        }
    }

    this.show = function () {
        $(`#${this.container}`).removeClass(`d-none`);
    }

    this.hide = function () {
        $(`#${this.container}`).addClass(`d-none`);
    }
}
