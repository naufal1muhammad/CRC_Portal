namespace CRC.Data.Models
{
    // One dbo.PatientJourney row with its patient's name, from spPatientJourney_GetById.
    //
    // A JOURNEY ROW IS AN EVENT, NOT A STATE. It records that a clinical step of one of the four
    // LU_PJ_APP_TYPE kinds happened to this patient, on this date, under this clinician — and nothing
    // more. There is no stage column, no status and no transition table anywhere in nucentra; the
    // patient's "position" in the programme is whatever the set of these rows implies. See CoreFlow.md §7,
    // which says so in full because an agent arriving from HEART will go looking for a state machine.
    //
    // 🔴 Staff_ID IS NOT AN AUDIT ACTOR. It is the clinician the journey belongs to — an ordinary business
    // value that arrives from the caller. Nothing in this area is filled from the NameIdentifier claim;
    // none of the twelve journey procedures declares @User_ID at all (CoreFlow.md §5.8).
    //
    // The PjAppType_Name here is a DENORMALIZED STRING, not a LU_PJ_APP_TYPE code: the create procedures
    // write the literal 'PATIENT ASSESSMENT', 'COLONOSCOPY' or 'PATIENT FOLLOW UP' into the column, and
    // 🔴 the third of those is not a value the lookup table holds — LU_PJ_APP_TYPE seeds "FOLLOW UP".
    // Nothing joins the two, so nothing notices. Do not "fix" it here: /StaffPatient's own GetJourneyTemplate
    // switches on the string "PATIENT FOLLOW UP", and spStaff_GetPerformance matches on 'COLONOSCOPY'.
    public class PatientJourneyDetail
    {
        public int PatientJourney_ID { get; set; }
        public string Patient_ID { get; set; } = string.Empty;

        // INNER JOIN onto dbo.PatientBasic, so a journey whose patient has gone is not returned at all
        // rather than returned with a null name — which is why this is non-nullable and Patient_Name on
        // PatientDocumentItem is not.
        public string Patient_Name { get; set; } = string.Empty;

        public string PjAppType_Name { get; set; } = string.Empty;
        public DateTime PatientJourney_Date { get; set; }
        public string Staff_ID { get; set; } = string.Empty;

        // DATETIME2(0), defaulted to SYSUTCDATETIME() — UTC, stamped by the database, never by C#.
        public DateTime Created_At { get; set; }

        // NULL until the first …_UpdateWithJourney call. These four are on the row itself and are a
        // SEPARATE record from dbo.PatientJourneyAudit: this one keeps only the first and the latest, the
        // audit table keeps every event.
        public DateTime? Updated_At { get; set; }
        public string? CreatedBy_Staff_ID { get; set; }
        public string? UpdatedBy_Staff_ID { get; set; }
    }
}
