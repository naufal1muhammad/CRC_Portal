namespace CRC.Data.Models
{
    // Everything spPatientAssessment_CreateWithJourney and spPatientAssessment_UpdateWithJourney need,
    // carried in one object because the two procedures share 49 of their 50 parameters and differ in
    // exactly one: the create takes @Patient_ID (whose journey is this?) and the update takes
    // @PatientJourney_ID (which journey is this?). Both fields are on this model and SqlData sends only
    // the one the procedure declares — sending the other would fail with "has no parameter named …".
    // Same arrangement PatientSaveInput uses for spPatientBasic_Insert versus _Update.
    //
    // 🔴 PROPERTY NAMES MIRROR THE PROCEDURE'S PARAMETER NAMES EXACTLY, INCLUDING iFOBTPositive_Date's
    // lower-case first letter. Dapper builds each parameter name from the property name, so a "tidier"
    // IFOBTPositive_Date is a change to the wire, not to style. Read the .sql before renaming anything here.
    //
    // THIS MODEL DECIDES NOTHING. Every value on it is whatever StaffPatientController's request DTO held;
    // the trimming, the ?? "" on the two non-nullable text fields and the null-versus-blank choice for the
    // optional ones all stay in the controller, exactly where they were before the Dapper layer existed.
    //
    // 🔴 Staff_ID IS THE CLINICIAN THIS JOURNEY BELONGS TO — a business argument taken from the caller's
    // "StaffId" claim by the controller, NOT the @User_ID audit actor of CoreFlow.md §0.1. Neither of these
    // two procedures declares @User_ID and neither writes a dbo.AuditTrails row; the Staff_ID goes onto
    // dbo.PatientJourney and onto the dbo.PatientJourneyAudit row instead. Filling it from
    // DatabaseHelper.CurrentUserId would put a Users identity into a Staff column and both procedures would
    // then RAISERROR 'Staff not found.' — which is the good outcome. Do not do it.
    public class PatientAssessmentSaveInput
    {
        // CREATE ONLY. The patient the new journey is for.
        public string Patient_ID { get; set; } = string.Empty;

        // UPDATE ONLY. The existing journey being re-saved.
        public int PatientJourney_ID { get; set; }

        // The clinical date the clinician chose, on BOTH paths — the update re-writes it on
        // dbo.PatientJourney, so editing an assessment can move its place in the timeline.
        public DateTime PatientJourney_Date { get; set; }

        public string Staff_ID { get; set; } = string.Empty;

        // Free text, straight onto the dbo.PatientJourneyAudit row this call writes. NULL is allowed.
        public string? Audit_Note { get; set; }

        // ── The assessment itself: risk factors, symptoms and history from the iFOBT-positive date ──
        public DateTime iFOBTPositive_Date { get; set; }

        public bool Risks_Smoking { get; set; }
        public bool Risks_AlcoholConsumption { get; set; }
        public bool Risks_InflammatoryBowelDisease { get; set; }
        public bool Risks_Diet { get; set; }

        // The one risk factor that is not a BIT — VARCHAR(100) NOT NULL, free text, no lookup table.
        public string Risks_SedentaryLifestyle { get; set; } = string.Empty;

        public bool Symptoms_WeightLoss { get; set; }
        public bool Symptoms_AppetiteLoss { get; set; }
        public bool Symptoms_Lethargic { get; set; }
        public bool Symptoms_AbdominalPain { get; set; }
        public bool Symptoms_Constipation { get; set; }
        public bool Symptoms_Diarrhea { get; set; }
        public bool Symptoms_RectalBleedingMucous { get; set; }
        public bool Symptoms_RectalBleedingNoMucous { get; set; }
        public bool Symptoms_Tenesmus { get; set; }

        public bool MedicalHistory_Diabetes { get; set; }
        public bool MedicalHistory_Hypertension { get; set; }
        public bool MedicalHistory_Dyslipidemia { get; set; }
        public bool MedicalHistory_Bleeding { get; set; }
        public bool MedicalHistory_Asthma { get; set; }

        // Each flag has an optional VARCHAR(100) detail beside it, and NOTHING keeps the pair in step:
        // a false flag with details, or a true flag with none, both save.
        public bool AllergyHistory_Medication { get; set; }
        public string? AllergyHistory_MedicationDetails { get; set; }
        public bool AllergyHistory_Food { get; set; }
        public string? AllergyHistory_FoodDetails { get; set; }

        public bool MedicationHistory_Anticoagulant { get; set; }
        public string? MedicationHistory_AnticoagulantDetails { get; set; }
        public bool MedicationHistory_Narcotics { get; set; }
        public string? MedicationHistory_NarcoticsDetails { get; set; }
        public bool MedicationHistory_Insulin { get; set; }
        public string? MedicationHistory_InsulinDetails { get; set; }
        public bool MedicationHistory_AntiHypertensives { get; set; }
        public string? MedicationHistory_AntiHypertensivesDetails { get; set; }

        // The only genuinely optional date on the form.
        public DateTime? PreviousScope_Date { get; set; }

        public bool FamilyHistory_FirstDegree { get; set; }
        public bool FamilyHistory_SecondDegree { get; set; }

        // VARCHAR(500) NOT NULL — and both procedures ISNULL it to '' anyway, so a null here stores a blank
        // rather than failing.
        public string PhysicalExamination_Details { get; set; } = string.Empty;

        public bool Investigation_FBC { get; set; }
        public bool Investigation_BUSE { get; set; }
        public bool Investigation_RBS { get; set; }
        public bool Investigation_LFT { get; set; }
        public bool Investigation_Coag { get; set; }

        public bool Management_BowelPrep { get; set; }
        public bool Management_Procedure { get; set; }
        public bool Management_Consent { get; set; }
        public bool Management_Advise { get; set; }
    }
}
