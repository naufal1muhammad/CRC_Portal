CREATE PROCEDURE dbo.spPatientAssessment_CreateWithJourney
(
    @Patient_ID               VARCHAR(100),
    @PatientJourney_Date      DATETIME,
    @Staff_ID                 VARCHAR(100),
    @Audit_Note               VARCHAR(500) = NULL,

    @iFOBTPositive_Date       DATETIME,
    @Risks_Smoking            BIT,
    @Risks_AlcoholConsumption BIT,
    @Risks_InflammatoryBowelDisease BIT,
    @Risks_Diet               BIT,
    @Risks_SedentaryLifestyle VARCHAR(100),

    @Symptoms_WeightLoss      BIT,
    @Symptoms_AppetiteLoss    BIT,
    @Symptoms_Lethargic       BIT,
    @Symptoms_AbdominalPain   BIT,
    @Symptoms_Constipation    BIT,
    @Symptoms_Diarrhea        BIT,
    @Symptoms_RectalBleedingMucous BIT,
    @Symptoms_RectalBleedingNoMucous BIT,
    @Symptoms_Tenesmus        BIT,

    @MedicalHistory_Diabetes  BIT,
    @MedicalHistory_Hypertension BIT,
    @MedicalHistory_Dyslipidemia BIT,
    @MedicalHistory_Bleeding  BIT,
    @MedicalHistory_Asthma    BIT,

    @AllergyHistory_Medication BIT,
    @AllergyHistory_MedicationDetails VARCHAR(100) = NULL,
    @AllergyHistory_Food      BIT,
    @AllergyHistory_FoodDetails VARCHAR(100) = NULL,

    @MedicationHistory_Anticoagulant BIT,
    @MedicationHistory_AnticoagulantDetails VARCHAR(100) = NULL,
    @MedicationHistory_Narcotics BIT,
    @MedicationHistory_NarcoticsDetails VARCHAR(100) = NULL,
    @MedicationHistory_Insulin BIT,
    @MedicationHistory_InsulinDetails VARCHAR(100) = NULL,
    @MedicationHistory_AntiHypertensives BIT,
    @MedicationHistory_AntiHypertensivesDetails VARCHAR(100) = NULL,

    @PreviousScope_Date       DATETIME = NULL,

    @FamilyHistory_FirstDegree BIT,
    @FamilyHistory_SecondDegree BIT,

    @PhysicalExamination_Details VARCHAR(500),

    @Investigation_FBC        BIT,
    @Investigation_BUSE       BIT,
    @Investigation_RBS        BIT,
    @Investigation_LFT        BIT,
    @Investigation_Coag       BIT,

    @Management_BowelPrep     BIT,
    @Management_Procedure     BIT,
    @Management_Consent       BIT,
    @Management_Advise        BIT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @Patient_Name VARCHAR(100);
        SELECT @Patient_Name = pb.Patient_Name
        FROM dbo.PatientBasic pb
        WHERE pb.Patient_ID = @Patient_ID;

        IF (@Patient_Name IS NULL OR LTRIM(RTRIM(@Patient_Name)) = '')
            RAISERROR('Patient not found.', 16, 1);

        DECLARE @Staff_Name VARCHAR(100);
        SELECT @Staff_Name = s.Staff_Name
        FROM dbo.Staff s
        WHERE s.Staff_ID = @Staff_ID;

        IF (@Staff_Name IS NULL OR LTRIM(RTRIM(@Staff_Name)) = '')
            RAISERROR('Staff not found.', 16, 1);

        -- Let IDENTITY generate PatientJourney_ID
        INSERT INTO dbo.PatientJourney
        (
            Patient_ID,
            PjAppType_Name,
            PatientJourney_Date,
            Staff_ID,
            CreatedBy_Staff_ID
        )
        VALUES
        (
            @Patient_ID,
            'ASSESSMENT',
            @PatientJourney_Date,
            @Staff_ID,
            @Staff_ID
        );

        DECLARE @PatientJourney_ID INT = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO dbo.PatientAssessment
        (
            PatientJourney_ID,
            Patient_ID,
            iFOBTPositive_Date,
            Risks_Smoking,
            Risks_AlcoholConsumption,
            Risks_InflammatoryBowelDisease,
            Risks_Diet,
            Risks_SedentaryLifestyle,
            Symptoms_WeightLoss,
            Symptoms_AppetiteLoss,
            Symptoms_Lethargic,
            Symptoms_AbdominalPain,
            Symptoms_Constipation,
            Symptoms_Diarrhea,
            Symptoms_RectalBleedingMucous,
            Symptoms_RectalBleedingNoMucous,
            Symptoms_Tenesmus,
            MedicalHistory_Diabetes,
            MedicalHistory_Hypertension,
            MedicalHistory_Dyslipidemia,
            MedicalHistory_Bleeding,
            MedicalHistory_Asthma,
            AllergyHistory_Medication,
            AllergyHistory_MedicationDetails,
            AllergyHistory_Food,
            AllergyHistory_FoodDetails,
            MedicationHistory_Anticoagulant,
            MedicationHistory_AnticoagulantDetails,
            MedicationHistory_Narcotics,
            MedicationHistory_NarcoticsDetails,
            MedicationHistory_Insulin,
            MedicationHistory_InsulinDetails,
            MedicationHistory_AntiHypertensives,
            MedicationHistory_AntiHypertensivesDetails,
            PreviousScope_Date,
            FamilyHistory_FirstDegree,
            FamilyHistory_SecondDegree,
            PhysicalExamination_Details,
            Investigation_FBC,
            Investigation_BUSE,
            Investigation_RBS,
            Investigation_LFT,
            Investigation_Coag,
            Management_BowelPrep,
            Management_Procedure,
            Management_Consent,
            Management_Advise
        )
        VALUES
        (
            @PatientJourney_ID,
            @Patient_ID,
            @iFOBTPositive_Date,
            @Risks_Smoking,
            @Risks_AlcoholConsumption,
            @Risks_InflammatoryBowelDisease,
            @Risks_Diet,
            @Risks_SedentaryLifestyle,
            @Symptoms_WeightLoss,
            @Symptoms_AppetiteLoss,
            @Symptoms_Lethargic,
            @Symptoms_AbdominalPain,
            @Symptoms_Constipation,
            @Symptoms_Diarrhea,
            @Symptoms_RectalBleedingMucous,
            @Symptoms_RectalBleedingNoMucous,
            @Symptoms_Tenesmus,
            @MedicalHistory_Diabetes,
            @MedicalHistory_Hypertension,
            @MedicalHistory_Dyslipidemia,
            @MedicalHistory_Bleeding,
            @MedicalHistory_Asthma,
            @AllergyHistory_Medication,
            @AllergyHistory_MedicationDetails,
            @AllergyHistory_Food,
            @AllergyHistory_FoodDetails,
            @MedicationHistory_Anticoagulant,
            @MedicationHistory_AnticoagulantDetails,
            @MedicationHistory_Narcotics,
            @MedicationHistory_NarcoticsDetails,
            @MedicationHistory_Insulin,
            @MedicationHistory_InsulinDetails,
            @MedicationHistory_AntiHypertensives,
            @MedicationHistory_AntiHypertensivesDetails,
            @PreviousScope_Date,
            @FamilyHistory_FirstDegree,
            @FamilyHistory_SecondDegree,
            ISNULL(@PhysicalExamination_Details, ''),
            @Investigation_FBC,
            @Investigation_BUSE,
            @Investigation_RBS,
            @Investigation_LFT,
            @Investigation_Coag,
            @Management_BowelPrep,
            @Management_Procedure,
            @Management_Consent,
            @Management_Advise
        );

        INSERT INTO dbo.PatientJourneyAudit
        (
            PatientJourney_ID,
            Audit_Action,
            Staff_ID,
            Audit_Note
        )
        VALUES
        (
            @PatientJourney_ID,
            'CREATED',
            @Staff_ID,
            @Audit_Note
        );

        COMMIT;

        SELECT @PatientJourney_ID AS PatientJourney_ID;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO