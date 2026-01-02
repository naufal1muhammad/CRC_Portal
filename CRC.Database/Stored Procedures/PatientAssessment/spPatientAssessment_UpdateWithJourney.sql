CREATE PROCEDURE dbo.spPatientAssessment_UpdateWithJourney
(
    @PatientJourney_ID        INT,
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
    @MedicationHistory_Insulin  BIT,
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

        IF NOT EXISTS (SELECT 1 FROM dbo.PatientJourney WHERE PatientJourney_ID = @PatientJourney_ID)
        BEGIN
            RAISERROR('Journey not found.', 16, 1);
        END

        DECLARE @Staff_Name VARCHAR(100);
        SELECT @Staff_Name = s.Staff_Name
        FROM dbo.Staff s
        WHERE s.Staff_ID = @Staff_ID;

        IF (@Staff_Name IS NULL OR LTRIM(RTRIM(@Staff_Name)) = '')
        BEGIN
            RAISERROR('Staff not found.', 16, 1);
        END

        UPDATE dbo.PatientJourney
        SET
            PatientJourney_Date = @PatientJourney_Date,
            Updated_At = SYSUTCDATETIME(),
            UpdatedBy_Staff_ID = @Staff_ID
        WHERE PatientJourney_ID = @PatientJourney_ID;

        UPDATE dbo.PatientAssessment
        SET
            iFOBTPositive_Date = @iFOBTPositive_Date,
            Risks_Smoking = @Risks_Smoking,
            Risks_AlcoholConsumption = @Risks_AlcoholConsumption,
            Risks_InflammatoryBowelDisease = @Risks_InflammatoryBowelDisease,
            Risks_Diet = @Risks_Diet,
            Risks_SedentaryLifestyle = @Risks_SedentaryLifestyle,

            Symptoms_WeightLoss = @Symptoms_WeightLoss,
            Symptoms_AppetiteLoss = @Symptoms_AppetiteLoss,
            Symptoms_Lethargic = @Symptoms_Lethargic,
            Symptoms_AbdominalPain = @Symptoms_AbdominalPain,
            Symptoms_Constipation = @Symptoms_Constipation,
            Symptoms_Diarrhea = @Symptoms_Diarrhea,
            Symptoms_RectalBleedingMucous = @Symptoms_RectalBleedingMucous,
            Symptoms_RectalBleedingNoMucous = @Symptoms_RectalBleedingNoMucous,
            Symptoms_Tenesmus = @Symptoms_Tenesmus,

            MedicalHistory_Diabetes = @MedicalHistory_Diabetes,
            MedicalHistory_Hypertension = @MedicalHistory_Hypertension,
            MedicalHistory_Dyslipidemia = @MedicalHistory_Dyslipidemia,
            MedicalHistory_Bleeding = @MedicalHistory_Bleeding,
            MedicalHistory_Asthma = @MedicalHistory_Asthma,

            AllergyHistory_Medication = @AllergyHistory_Medication,
            AllergyHistory_MedicationDetails = @AllergyHistory_MedicationDetails,
            AllergyHistory_Food = @AllergyHistory_Food,
            AllergyHistory_FoodDetails = @AllergyHistory_FoodDetails,

            MedicationHistory_Anticoagulant = @MedicationHistory_Anticoagulant,
            MedicationHistory_AnticoagulantDetails = @MedicationHistory_AnticoagulantDetails,
            MedicationHistory_Narcotics = @MedicationHistory_Narcotics,
            MedicationHistory_NarcoticsDetails = @MedicationHistory_NarcoticsDetails,
            MedicationHistory_Insulin = @MedicationHistory_Insulin,
            MedicationHistory_InsulinDetails = @MedicationHistory_InsulinDetails,
            MedicationHistory_AntiHypertensives = @MedicationHistory_AntiHypertensives,
            MedicationHistory_AntiHypertensivesDetails = @MedicationHistory_AntiHypertensivesDetails,

            PreviousScope_Date = @PreviousScope_Date,

            FamilyHistory_FirstDegree = @FamilyHistory_FirstDegree,
            FamilyHistory_SecondDegree = @FamilyHistory_SecondDegree,

            PhysicalExamination_Details = ISNULL(@PhysicalExamination_Details, ''),

            Investigation_FBC = @Investigation_FBC,
            Investigation_BUSE = @Investigation_BUSE,
            Investigation_RBS = @Investigation_RBS,
            Investigation_LFT = @Investigation_LFT,
            Investigation_Coag = @Investigation_Coag,

            Management_BowelPrep = @Management_BowelPrep,
            Management_Procedure = @Management_Procedure,
            Management_Consent = @Management_Consent,
            Management_Advise = @Management_Advise
        WHERE PatientJourney_ID = @PatientJourney_ID;

        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('Assessment row not found for this journey.', 16, 1);
        END

        INSERT INTO dbo.PatientJourneyAudit
        (
            PatientJourney_ID,
            Audit_Action,
            Staff_ID,
            Staff_Name,
            Audit_Note
        )
        VALUES
        (
            @PatientJourney_ID,
            'UPDATED',
            @Staff_ID,
            @Staff_Name,
            @Audit_Note
        );

        COMMIT;

        SELECT 1 AS Success;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO