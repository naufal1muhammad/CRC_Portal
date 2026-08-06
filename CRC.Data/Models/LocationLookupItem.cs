namespace CRC.Data.Models
{
    // One row of the dbo.LU_LOCATION tree — the state → city → postcode hierarchy every address in
    // nucentra is resolved against. The three spLU_LOCATION_* procedures all return this shape.
    //
    // 🔴 THIS IS WHY LookupItem COULD NOT BE REUSED. LU_LOCATION is the one reference table that does not
    // key on a VARCHAR code: `LocationId INT IDENTITY(1,1)`. Its display column is `Name`, not
    // `{Table}_Name`, and one table holds all three levels, discriminated by `LocationType`
    // (1 = state, 2 = city, 3 = postcode) and linked by `ParentId`. A state has no parent; a city's parent
    // is its state; a postcode's parent is its city. Forcing those into LookupItem's string Id would make
    // `stateId` a string in JSON the moment anyone serialized it directly, and the four .js files that
    // read these endpoints send the id back as a number.
    //
    // The property names deliberately MIRROR THE COLUMN NAMES. Dapper maps by column name, and Prompt 1
    // was not allowed to alias a .sql file, so a model whose properties do not match the SELECT list maps
    // to nothing at all — silently, with no exception and no empty result, just default values on every
    // row. Naming them identically means the POCO and the procedure can be diffed by eye.
    //
    // ParentId and SortOrder are both nullable, and both are genuinely null in practice:
    //   • spLU_LOCATION_ListStates does not SELECT ParentId at all (states have no parent), so it stays
    //     null on every row that procedure returns — an unmapped property, not a null column.
    //   • SortOrder is NULL for most rows; all three procedures order by
    //     COALESCE(SortOrder, 2147483647) then Name, i.e. hand-curated rows float to the top and the rest
    //     fall back to alphabetical. The UI depends on that order, so callers must not re-sort.
    public class LocationLookupItem
    {
        public int LocationId { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? SortOrder { get; set; }
    }
}
