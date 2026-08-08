namespace CRC.Data.Models
{
    // One document type a patient is REQUIRED to have on file before they can be discharged under a given
    // discharge reason, and does not — one row of spPatient_Discharge_CheckMissingDocuments.
    //
    // 🔴 EVERY ROW IS A PROBLEM. AN EMPTY RESULT IS THE PASS CONDITION. The procedure returns what is
    // MISSING, not what is required, so `Count == 0` means "cleared to discharge" and any row at all
    // blocks the save. Reading it the other way round — treating rows as the requirement list — inverts
    // the check completely and lets every discharge through.
    //
    // The requirement itself is data, not code: one row of dbo.PatientDocumentSettings per
    // (DischargeType_ID, PatientDocumentType_ID) pair, where the row's EXISTENCE is the rule — the same
    // shape dbo.StaffDocumentSettings uses for staff (CoreFlow.md §3.6). A freshly published CRC_DB has
    // none, so until somebody configures the Settings screen NOTHING is mandatory and every discharge
    // passes this check with an empty result.
    //
    // Why this is not LookupItem: Dapper maps by name, the procedure's columns are
    // PatientDocumentType_ID / PatientDocumentType_Name, and LookupItem's properties are Id / Name — so
    // QueryAsync<LookupItem> here would return the right number of rows with both properties empty, with
    // no exception and nothing in a log. (The eleven LU_* code procedures dodge that by going through
    // SqlData.QueryLookupAsync, which reads by ORDINAL; that helper takes no parameters and this procedure
    // takes two.) Naming the model after the data is also simply more honest: these are not lookup
    // entries, they are unmet requirements.
    public class PatientDocumentRequirement
    {
        public string PatientDocumentType_ID { get; set; } = string.Empty;
        public string PatientDocumentType_Name { get; set; } = string.Empty;
    }
}
