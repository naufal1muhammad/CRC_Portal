/*
 * Read a page's GET URL variables and return them as an associative array.
 * 
 * You can check using getUrlVars().hasOwnProperty("anyquery")
 * The value can be fetched using getUrlVars().anyquery
 * The array of all the query can be fetched using Object.keys(getUrlVars())
 * 
 * */

(function (_urlVariable, $, undefined) {
    _urlVariable.hasProperty = function (propertyName) {
        return urlVariables().hasOwnProperty(propertyName);
    };

    _urlVariable.getProperty = function (propertyName) {
        return urlVariables()[propertyName].split(`#`)[0];
    };

    _urlVariable.getAllProperties = function () {
        return Object.keys(urlVariables());
    };

    function urlVariables() {
        var vars = [], hash;
        var hashes = window.location.href.slice(window.location.href.indexOf(`?`) + 1).split(`&`);

        for (var i = 0; i < hashes.length; i++) {
            hash = hashes[i].split(`=`);
            vars.push(hash[0]);
            vars[hash[0]] = hash[1];
        }

        return vars;
    }
}(window._urlVariable = window._urlVariable || {}, jQuery));
