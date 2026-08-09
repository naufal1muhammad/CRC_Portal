namespace CRC.Data.Models
{
    // One row of a patient's journey timeline, from spPatientJourney_TimelineByPatient.
    //
    // The timeline is the whole clinical history of one patient in one read: every dbo.PatientJourney row
    // they have, in date order, each carrying who created it and who last changed it.
    //
    // 🔴 THE FIVE AUDIT COLUMNS DO NOT COME FROM dbo.PatientJourney. The procedure OUTER APPLYs
    // dbo.PatientJourneyAudit twice per journey row — the EARLIEST 'CREATED' event, and the LATEST event
    // whose action is 'UPDATED' or 'EDITED' — and joins each one's Staff_ID to dbo.Staff for a name. That is
    // why all five are nullable while Created_At on the journey row itself is not: a journey with no audit
    // rows (one written by hand, or one whose audit rows were removed) still appears on the timeline, with
    // every one of these null. OUTER APPLY, not CROSS APPLY, is what makes that true.
    //
    // 'EDITED' is accepted by the update side of that OUTER APPLY and NOTHING WRITES IT. All six
    // …WithJourney procedures write 'CREATED' or 'UPDATED' and nothing else. It is a vocabulary the read
    // tolerates and the writes never produce; do not read its presence as evidence of a third action.
    //
    // ORDERING IS PART OF THE CONTRACT: PatientJourney_Date ASC, then PatientJourney_ID ASC. That is
    // clinical order — assessment, then colonoscopy, then follow-up — and it is the ONLY thing in nucentra
    // that puts a journey in sequence, because there is no stage column to sort on (CoreFlow.md §7). The
    // id tiebreak decides two events recorded for the same instant, so the row written first shows first.
    // A caller must not re-sort.
    public class PatientJourneyTimelineItem
    {
        public int PatientJourney_ID { get; set; }
        public string Patient_ID { get; set; } = string.Empty;
        public string Patient_Name { get; set; } = string.Empty;

        // The denormalized type string — see PatientJourneyDetail for why "PATIENT FOLLOW UP" is not a
        // LU_PJ_APP_TYPE value.
        public string PjAppType_Name { get; set; } = string.Empty;

        // The BUSINESS date the clinician chose, DATETIME NOT NULL. Not a timestamp: it is when the step
        // happened clinically, which may be before or after the row was written.
        public DateTime PatientJourney_Date { get; set; }

        // ── from the first 'CREATED' audit row, or all null when there is none ──
        //
        // dbo.PatientJourneyAudit.Audit_At is DATETIME2(0) DEFAULT SYSUTCDATETIME(), so these ARE UTC —
        // but SQL Server hands a DATETIME2 back as DateTimeKind.Unspecified, so the endpoint must
        // SpecifyKind(…, Utc) before serializing or the offset it prints would be a lie. Same treatment
        // §4.3's dbo.Users timestamps get, and the opposite of StaffDocumentItem.UploadedOn, which is
        // Malaysian local time and must NOT be relabelled.
        public DateTime? CreatedAt { get; set; }
        public string? CreatedByStaffId { get; set; }

        // LEFT JOIN onto dbo.Staff (nothing constrains Staff_ID to it), so this is null both when there is
        // no audit row AND when the clinician's row has since been deleted.
        public string? CreatedByStaffName { get; set; }

        // ── from the LATEST 'UPDATED'/'EDITED' audit row, or all null when the journey was never changed ──
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByStaffId { get; set; }
        public string? UpdatedByStaffName { get; set; }
    }
}
