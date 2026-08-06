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
    // Prompt 1 decides what they get instead, once it has read all fourteen lookup procedures — do not
    // pre-empt that here by widening this model or adding a second one on a guess.
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
