namespace CRC.Data.Models
{
    // One row of the ACTIVE patient list, as spPatientBasic_ListActive returns it — five columns, ordered
    // by Patient_ID DESC (newest first; the caller must not re-sort).
    //
    // "Active" is not a column and not a status. It is `DischargeType_ID IS NULL`, and that single test is
    // the whole difference between this procedure and spPatientBasic_ListDischarged (CoreFlow.md §3.8).
    //
    // 🔴 WHY THIS IS NOT SHARED WITH PatientDischargedItem, EVEN THOUGH IT COULD BE.
    // spPatientBasic_ListActive's five columns are a STRICT SUBSET of spPatientBasic_ListDischarged's six —
    // the sixth is Patient_DischargeDate. Sharing one model would compile, run, and leave DischargeDate
    // null on every active row, which is exactly the shape of the trap §5.3 describes for spUsers_GetById
    // versus spUsers_ValidateLogin: a property the result set never fills, reading as a fact. The rule this
    // codebase follows is REUSE THE SHAPE, NEVER THE NAME (contrast StaffDocumentItem, where two procedures
    // genuinely select the same nine columns and correctly share one model).
    //
    // The three iFOBT columns are selected by the procedure and are genuinely nullable on dbo.PatientBasic
    // — a patient registered but not yet tested has all three NULL. /Patient/GetActivePatients does not
    // project them today; they are modelled because the procedure returns them, and a model that silently
    // drops columns stops being a description of the result set.
    public class PatientListItem
    {
        public string Patient_ID { get; set; } = string.Empty;
        public string Patient_Name { get; set; } = string.Empty;

        // NULL = the iFOBT has not been recorded at all. The two completion columns are only ever written
        // when the status is 1: both spPatientBasic_Insert and spPatientBasic_Update NULL them out
        // otherwise, in SQL, whatever the caller sent (CoreFlow.md §5.6).
        public bool? Patient_iFOBTStatus { get; set; }
        public DateTime? Patient_iFOBTCompletionDate { get; set; }
        public bool? Patient_iFOBTResults { get; set; }
    }
}
