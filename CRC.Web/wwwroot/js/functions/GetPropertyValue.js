/**
    * To get the value of a key in object.
    * Key can traverse by using period ".". e.g. `department.name`, `user.accessmatrices.roleId`.
    * Sample: __fnGetPropertyValue(myObject, `group.groupAccesses.userId`);
    * @author   Azizan Haniff
    * @param    {object} item                       Required. The object.
    * @param    {string} key                        Required. The key to search in object.
    * @return   {undefined|string|number|boolean}   Value for key in object, or undefined if key not found.
    * 
    * Sample:
    let myObject = { 
        level1_1: `Ayam`, 
        level1_2: { 
            level2_1: `Masak`, 
            level2_2: `Merah`, 
            level2_3: { 
                level3_1: `Sedap` 
            } 
        }
    };
    let myKey = `level1_2.level2_3.level3_1`;
    console.log(__fnGetPropertyValue(myObject, myKey)); // returns `Sedap`;
*/
const __fnGetPropertyValue = (item, key) => (function (item, key) {
    let runProcess = function (item, key) {
        // Imediately return undefined if item is not Json object.
        if (item === undefined) {
            return undefined;
        }

        // Traverse each key.
        for (let i = 0, keys = key.split(`.`); i < keys.length; i++) {
            // item[keys[0]]
            // item[keys[0]][keys[1]]
            // item[keys[0]][keys[1]]...[keys[n-1]]
            item = (item.hasOwnProperty(keys[i]))
                ? item[keys[i]]
                : undefined;

            // Immediately break if undefined.
            if (item === undefined) { break; }
        }

        return item;
    };

    return runProcess(item, key);
})(item, key);
