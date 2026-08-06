namespace CRC.Data.Models
{
    // Generic {Id, Name} row from an LU_* reference table, for the portal's dropdowns.
    //
    // 🔴 Id IS A STRING, AND THAT IS NOT AN OVERSIGHT — it is what nucentra's schema actually says.
    // Eleven of the twelve LU_* tables key on VARCHAR(100), not an identity:
    //
    //     LU_DISCHARGETYPE   LU_ORGANIZATION      LU_RACE          LU_STAFFDOCUMENTTYPE
    //     LU_MARITALSTATUS   LU_PATDOCUMENTTYPE   LU_RELIGION      LU_STAFFTYPE
    //     LU_OCCUPATION      LU_PJ_APP_TYPE       LU_SOURCE
    //
    // Their seed values (CRC.Data/Database/Migrations/*.csv) are two-character zero-padded codes — "01",
    // "02" … — with LU_STAFFTYPE the outlier that uses three-letter mnemonics ("ANE", "END", "NUR").
    // Every one of them is stored as text on the row that references it: PatientBasic.Race_ID,
    // PatientBasic.Occupation_ID, Staff.Staff_Type and the rest are all VARCHAR(100). Parsing an id to an
    // int would work by accident on ten tables and lose the leading zero on all of them.
    //
    // LU_LOCATION is the single exception: it keys on `LocationId INT IDENTITY(1,1)` and its display
    // column is `Name`, not `{Table}_Name`. So the three spLU_LOCATION_* procedures do NOT fit this type.
    //
    // ── PROMPT 1's DECISION, having read all fourteen procedures ─────────────────────────────────────
    //
    // TWO models, not fourteen: this one and LocationLookupItem. Eleven of the fourteen procedures return
    // exactly {code, name} and are represented here; the three spLU_LOCATION_* procedures return
    // {LocationId, [ParentId,] Name, SortOrder} and get LocationLookupItem.
    //
    // 🔴 BUT THE ELEVEN CANNOT BE MAPPED BY NAME, and that is the surprise of this prompt. Dapper matches
    // a result column to a property by NAME, and no two of the eleven agree on one: spLU_Race_List
    // returns Race_ID / Race_Name, spLU_Source_List returns Source_ID / Source_Name, spLU_PJ_AppType_List
    // returns PjAppType_ID / PjAppType_Name, and so on for all eleven. `QueryAsync<LookupItem>` against
    // any of them compiles, runs, returns the right NUMBER of rows and leaves Id and Name empty on every
    // one — no exception, nothing in a log.
    //
    // The three ways out, and why this one:
    //   ✗ Alias the columns in the .sql. Explicitly forbidden by Prompt 1 — name the model after the data,
    //     not the data after the model — and a shared procedure's output columns are a public contract.
    //   ✗ Eleven two-property models, one per table. Also forbidden, and it would be eleven files that
    //     differ only in a prefix.
    //   ✓ Map POSITIONALLY, once, in SqlData.QueryLookupAsync: column 0 is the code, column 1 is the
    //     display name, in every one of the eleven. It is the one property all eleven genuinely share.
    //     The helper is the ONLY place in the data layer that reads a column by ordinal, and it says so.
    //
    // The Web layer maps these into whatever camelCase shape its endpoint already returns
    // (`{ stateId, stateName }`, `{ organizationId, organizationName }`, …). It never serializes a model
    // directly: 59 JavaScript files depend on the current shapes and this migration does not touch them.
    public class LookupItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
