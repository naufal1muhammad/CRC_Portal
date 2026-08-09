namespace CRC.Data.Models
{
    // Everything spPatientFollowUp_CreateWithJourney and spPatientFollowUp_UpdateWithJourney need — the
    // smallest of the three detail types by a wide margin: three clinical columns against the assessment's
    // forty-five. Same create/update split as PatientAssessmentSaveInput, whose header covers the
    // @User_ID / Staff_ID distinction that applies here too.
    //
    // 🔴 THE CREATE PROCEDURE WRITES PjAppType_Name = 'PATIENT FOLLOW UP', AND LU_PJ_APP_TYPE SEEDS
    // "FOLLOW UP". The two do not match and nothing joins them, so nothing reports it. The string the
    // procedure writes is the one the portal uses: GetJourneyTemplate switches on "PATIENT FOLLOW UP" and
    // the timeline renders whatever the column holds. The lookup value is used only by the appointment
    // form's type dropdown, which is a different column on a different table. See CoreFlow.md §7.
    public class PatientFollowUpSaveInput
    {
        // CREATE ONLY.
        public string Patient_ID { get; set; } = string.Empty;

        // UPDATE ONLY.
        public int PatientJourney_ID { get; set; }

        public DateTime PatientJourney_Date { get; set; }
        public string Staff_ID { get; set; } = string.Empty;
        public string? Audit_Note { get; set; }

        // The histopathology result for the specimen the COLONOSCOPY journey sent. VARCHAR(100) NOT NULL,
        // free text — the clinical link between the two journey rows exists in the words, not the schema.
        public string HPE_Results { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL, free text. Again NOT dbo.PatientBasic.DischargeType_ID.
        public string DischargePlan { get; set; } = string.Empty;

        // Has the discharge summary been issued? A BIT on this row and nothing more — it does not
        // discharge the patient, and no code anywhere reads it back except the follow-up template.
        public bool DischargeSummary_Status { get; set; }
    }
}
