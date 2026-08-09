namespace CRC.Data.Models
{
    // One dbo.PatientJourneyAudit row, from spPatientJourney_AuditsByPatient.
    //
    // 🔴 THIS IS NUCENTRA'S SECOND DATABASE AUDIT TRAIL AND IT IS NOT dbo.AuditTrails. The two answer
    // different questions and neither is a substitute for the other:
    //
    //   dbo.AuditTrails         written by the stored procedures, keyed on a USER account (User_Id, the
    //                           @User_ID actor of CoreFlow.md §0.1), a flat CONCATed summary string,
    //                           security-facing, and NOT displayed anywhere in the clinical UI.
    //   dbo.PatientJourneyAudit written by the six …WithJourney procedures, keyed on a STAFF member
    //                           (Staff_ID) and on the journey row, structured, and rendered straight into
    //                           the patient's timeline as its history.
    //
    // 🔴 NONE OF THE TWELVE JOURNEY PROCEDURES WRITES A dbo.AuditTrails ROW AT ALL. Recording a
    // colonoscopy leaves no trace in the security trail; it leaves a row here instead. That is a real gap
    // and it is stated plainly rather than filled, because filling it would mean editing a .sql.
    //
    // WHEN A ROW IS WRITTEN: exactly one per successful …WithJourney call, inside that procedure's own
    // transaction — 'CREATED' on the three creates, 'UPDATED' on the three updates. Nothing else in the
    // product writes here. There is no DELETE action, because there is no way to delete a journey except
    // spPatient_DeleteCascade, which erases dbo.PatientJourney and leaves these rows orphaned (nothing
    // deletes them and no foreign key stops it).
    //
    // ORDERING IS PART OF THE CONTRACT: PatientJourney_ID ASC, Audit_At ASC, PatientJourneyAudit_ID ASC —
    // grouped by journey, oldest event first, with the identity as the tiebreak because Audit_At is
    // DATETIME2(0) and two events in the same second are ordinary.
    public class PatientJourneyAuditItem
    {
        public int PatientJourneyAudit_ID { get; set; }
        public int PatientJourney_ID { get; set; }

        // VARCHAR(20) NOT NULL, with no check constraint and no lookup table. 'CREATED' and 'UPDATED' are
        // the only two values the product writes; 'EDITED' is read by the timeline procedure and written
        // by nothing.
        public string Audit_Action { get; set; } = string.Empty;

        // DATETIME2(0) DEFAULT SYSUTCDATETIME(). UTC, but returned as DateTimeKind.Unspecified — see
        // PatientJourneyTimelineItem.
        public DateTime Audit_At { get; set; }

        // 🔴 The STAFF member who performed the step, NOT a dbo.Users id and NOT the audit actor of §0.1.
        // It arrives from the caller's StaffId claim by way of the controller, as an ordinary argument.
        public string Staff_ID { get; set; } = string.Empty;

        // LEFT JOIN onto dbo.Staff, which nothing constrains Staff_ID to — null when the clinician's row
        // has been deleted, and a deleted clinician is exactly the case somebody reads an audit trail for.
        public string? Staff_Name { get; set; }

        // The free-text note the clinician typed on save. VARCHAR(500) NULL, and it is the ONE column here
        // whose null reaches the browser as JSON null rather than "" — see StaffPatientController.GetTimeline.
        public string? Audit_Note { get; set; }
    }
}
