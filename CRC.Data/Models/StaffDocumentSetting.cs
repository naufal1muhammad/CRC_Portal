namespace CRC.Data.Models
{
    // One row of "which document types matter for this staff type", from
    // spStaffDocumentSettings_GetByStaffType.
    //
    // THE PROCEDURE RETURNS EVERY DOCUMENT TYPE, NOT ONLY THE MANDATORY ONES. It drives LU_STAFFDOCUMENTTYPE
    // and LEFT JOINs dbo.StaffDocumentSettings, so the result is all eight types with IsMandatory = 1 on the
    // ones configured for this staff type and 0 on the rest. That shape is what Prompt 8's Settings screen
    // needs — a full checklist with the boxes ticked — and it is why both callers that only want the
    // mandatory ones filter on IsMandatory themselves rather than trusting the row count.
    //
    // IsMandatory is an INT, not a BIT: the procedure computes it as `CASE WHEN … IS NULL THEN 0 ELSE 1 END`,
    // and an unaliased CASE over integer literals is typed INT. Mapping it to bool would work today by
    // accident and break the moment the expression changes; the callers compare it to 1, exactly as the
    // DataTable code did.
    public class StaffDocumentSetting
    {
        public string StaffDocumentType_ID { get; set; } = string.Empty;
        public string StaffDocumentType_Name { get; set; } = string.Empty;
        public int? IsMandatory { get; set; }
    }
}
