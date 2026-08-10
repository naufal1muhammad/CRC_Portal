namespace CRC.Data.Models
{
    // One row of "this document type is mandatory before a patient can be discharged under this reason" —
    // one row of dbo.PatientDocumentSettings, as returned by spPatientDocumentSettings_GetByDischargeType.
    //
    // 🔴 IT IS THE OPPOSITE SHAPE TO ITS STAFF TWIN, AND THE DIFFERENCE IS EASY TO MISS.
    // StaffDocumentSetting comes from a procedure that drives LU_STAFFDOCUMENTTYPE and LEFT JOINs the
    // settings table, so it returns EVERY document type with an IsMandatory flag — a full checklist. This
    // one selects straight from dbo.PatientDocumentSettings, so it returns ONLY the mandatory types and
    // carries no flag at all: THE ROW'S EXISTENCE IS THE RULE. An empty result means nothing is required
    // for that discharge reason, not that the discharge reason is unknown.
    //
    // The page renders them the same way — a checklist of every type with some boxes ticked — but the
    // Settings screen has to build the patient one by starting from the LU_PATDOCUMENTTYPE list and
    // ticking what this returns, whereas the staff one arrives pre-ticked. See CoreFlow.md §5.9.
    //
    // The discharge type is denormalized onto every row (both id and name), which is why those two columns
    // are here at all: the endpoint echoes them back and the settings table is the only place they are
    // stored per row. spPatientDocumentSettings_SaveForDischargeType resolves the name from
    // LU_DISCHARGETYPE itself, so nothing outside the procedure has to keep the pair consistent.
    public class PatientDocumentSetting
    {
        public string? DischargeType_ID { get; set; }
        public string? DischargeType_Name { get; set; }
        public string? PatientDocumentType_ID { get; set; }
        public string? PatientDocumentType_Name { get; set; }
    }
}
