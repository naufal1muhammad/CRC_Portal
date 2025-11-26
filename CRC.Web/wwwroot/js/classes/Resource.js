/**
 * Creates a Resource object to fetch data from an API.
 * @author   Azizan Haniff
 * @typedef  {object} Resource
 * @param    {string} route                                                         The URL to fetch dataset. e.g. `api/smartapps/users`.
 * @param    {object} configuration                                                 Configuration of the Resource object.
 * @param    {string} [configuration.name]                                          Name.
 * @param    {string} [configuration.method=get]                                    HTTP request method to use. Defaults to `get`.
 * @param    {string} [configuration.displayColumn=id]                              Column to display. Defaults to `id`.
 * @param    {string} [configuration.handler]                                       API handler name.
 * @param    {{key: string, value: string|number|boolean}} [configuration.query]    Handler name. Defaults to ``.
 * @param    {object[]} [configuration.body]                                        HTTP request body. Required if configuration.method is `post` or `put`.
*/
function Resource(route, configuration) {
    this.dataset = null;
    this.route = route;
    this.name = configuration !== undefined && configuration.name !== undefined ? configuration.name : ``;
    this.elementName = configuration !== undefined && configuration.name !== undefined ? configuration.name.toLowerCase() : ``;
    this.displayColumn = configuration !== undefined && configuration.displayColumn !== undefined ? configuration.displayColumn : `id`;
    this.handler = configuration !== undefined && configuration.handler !== undefined ? configuration.handler : ``;
    this.method = configuration !== undefined && configuration.method !== undefined ? configuration.method.toLowerCase() : `get`;
    this.body = configuration !== undefined && configuration.body !== undefined ? configuration.body : [];
    this.query = configuration !== undefined && configuration.query !== undefined ? configuration.query : {};

    this.queryText = function () {
        let queryText = ``;
        for (const [key, value] of Object.entries(this.query)) {
            queryText += `&${key}=${value}`;
        }

        if (this.handler === `` && queryText.length > 0) {
            return `?` + queryText.substring(1);
        } else {
            return queryText;
        }
    };

    /**
     * Returns the URL absolute path of Resource object.
     * @return {string}
    */
    this.url = function () {
        return encodeURI(window.location.origin.replace(/\/$/, ``) + (($(`#main-url`).attr(`href`) == `/`) ? `` : $(`#main-url`).attr(`href`)) + `/` + this.route + ((this.handler !== ``) ? `?handler=` + this.handler : ``) + this.queryText());
    };

    /**
     * Returns the Fetch object.
     * @return {Promise<Response>}
    */
    this.fetch = function () {
        let requestInit = {
            "method": this.method,
            "headers": {
                "Accept": `application/json`,
                "Content-Type": `application/json`,
                "XSRF-TOKEN": $(`input:hidden[name="__RequestVerificationToken"]`).val()
            }
        };

        if (this.method.toLowerCase() !== `get` && this.body !== []) {
            requestInit[`body`] = JSON.stringify(this.body);
        }

        return fetch(this.url(), requestInit);
    };
}
