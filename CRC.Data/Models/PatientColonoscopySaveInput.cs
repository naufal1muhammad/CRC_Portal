namespace CRC.Data.Models
{
    // Everything spPatientColonoscopy_CreateWithJourney and spPatientColonoscopy_UpdateWithJourney need.
    // Same arrangement as PatientAssessmentSaveInput: the two procedures share every parameter except the
    // first — @Patient_ID on the create, @PatientJourney_ID on the update — and SqlData sends only the one
    // the procedure declares. Read that file's header for the @User_ID / Staff_ID distinction; it applies
    // here unchanged, and neither of these procedures declares @User_ID either.
    //
    // ── THE FINDINGS ARE NINE BOWEL SEGMENTS, ANUS TO CAECUM, AND THE BIT IS "NORMAL" ──────────────────
    //
    // 🔴 Findings_X TRUE MEANS THE SEGMENT WAS NORMAL. The details column beside it carries the anomaly,
    // so a FALSE flag is the one with something in it — the opposite of the reading the name suggests.
    // wwwroot/js/staffPatient/templates/patientColonoscopy.js sets `Findings_Anus: anus.isNormal`, which is
    // where that is decided, and this layer does not touch it.
    //
    // 🔴 EACH DETAILS COLUMN IS AN NVARCHAR(MAX) HOLDING A JSON DOCUMENT, not prose. The one key anything
    // server-side reads is TypeOfAnomaly: spStaff_GetPerformance CROSS APPLYs all nine columns into one,
    // keeps the rows where ISJSON() = 1, and counts JSON_VALUE(…, '$.TypeOfAnomaly') per DISTINCT patient
    // (CoreFlow.md §5.5). Nothing validates the JSON on the way in — an unparseable value inserts happily
    // and simply stops appearing in that report.
    public class PatientColonoscopySaveInput
    {
        // CREATE ONLY.
        public string Patient_ID { get; set; } = string.Empty;

        // UPDATE ONLY.
        public int PatientJourney_ID { get; set; }

        public DateTime PatientJourney_Date { get; set; }
        public string Staff_ID { get; set; } = string.Empty;
        public string? Audit_Note { get; set; }

        // Was the scope completed? The details beside it are free text, not JSON.
        public bool ColonoscopyStatus { get; set; }
        public string? ColonoscopyStatus_Details { get; set; }

        // An INT with no lookup table, no check constraint and no meaning recorded anywhere in the
        // database — the Boston-scale grade the form's dropdown posted.
        public int BowelPreparation { get; set; }

        public bool Findings_Anus { get; set; }
        public string? Findings_AnusDetails { get; set; }
        public bool Findings_Rectum { get; set; }
        public string? Findings_RectumDetails { get; set; }
        public bool Findings_SigmoidColon { get; set; }
        public string? Findings_SigmoidColonDetails { get; set; }
        public bool Findings_DescendingColon { get; set; }
        public string? Findings_DescendingColonDetails { get; set; }
        public bool Findings_SplenicFlexure { get; set; }
        public string? Findings_SplenicFlexureDetails { get; set; }
        public bool Findings_TransverseColon { get; set; }
        public string? Findings_TransverseColonDetails { get; set; }
        public bool Findings_HepaticFlexure { get; set; }
        public string? Findings_HepaticFlexureDetails { get; set; }
        public bool Findings_AscendingColon { get; set; }
        public string? Findings_AscendingColonDetails { get; set; }
        public bool Findings_Caecum { get; set; }
        public string? Findings_CaecumDetails { get; set; }

        // Was a specimen sent for histopathology? The RESULT is recorded later, on the FOLLOW UP journey's
        // HPE_Results — two journey types, one clinical thread, and nothing in the schema links them.
        public bool HPE_Status { get; set; }
        public string? HPE_Details { get; set; }

        // VARCHAR(100) NOT NULL, free text, no lookup table — and spStaff_GetPerformance groups its
        // complications report on this exact string, so two spellings are two rows in that report.
        public string Complications { get; set; } = string.Empty;
        public string? Complications_Details { get; set; }

        // VARCHAR(100) NOT NULL, free text. NOT dbo.PatientBasic.DischargeType_ID and not connected to it:
        // discharging a patient is a separate write on a separate screen (CoreFlow.md §3.8).
        public string DischargePlan { get; set; } = string.Empty;

        // A JSON ARRAY of medications given during the procedure, NVARCHAR(MAX). Nothing reads it
        // server-side; the colonoscopy template renders it back into its own table.
        public string? Medication_Details { get; set; }
    }
}
