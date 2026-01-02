CREATE TABLE [dbo].[PatientAssessment]
(
    [PatientAssessment_ID] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PatientAssessment] PRIMARY KEY,
    [PatientJourney_ID] INT NULL,
    [Patient_ID] VARCHAR(100) NOT NULL,
    [Patient_Name] VARCHAR(100) NOT NULL,
    [iFOBTPositive_Date] DATETIME NOT NULL,

    [Risks_Smoking] BIT NOT NULL,
    [Risks_AlcoholConsumption] BIT NOT NULL,
    [Risks_InflammatoryBowelDisease] BIT NOT NULL,
    [Risks_Diet] BIT NOT NULL,
    [Risks_SedentaryLifestyle] VARCHAR(100) NOT NULL,

    [Symptoms_WeightLoss] BIT NOT NULL,
    [Symptoms_AppetiteLoss] BIT NOT NULL,
    [Symptoms_Lethargic] BIT NOT NULL,
    [Symptoms_AbdominalPain] BIT NOT NULL,
    [Symptoms_Constipation] BIT NOT NULL,
    [Symptoms_Diarrhea] BIT NOT NULL,
    [Symptoms_RectalBleedingMucous] BIT NOT NULL,
    [Symptoms_RectalBleedingNoMucous] BIT NOT NULL,
    [Symptoms_Tenesmus] BIT NOT NULL,

    [MedicalHistory_Diabetes] BIT NOT NULL,
    [MedicalHistory_Hypertension] BIT NOT NULL,
    [MedicalHistory_Dyslipidemia] BIT NOT NULL,
    [MedicalHistory_Bleeding] BIT NOT NULL,
    [MedicalHistory_Asthma] BIT NOT NULL,

    [AllergyHistory_Medication] BIT NOT NULL,
    [AllergyHistory_MedicationDetails] VARCHAR(100) NULL,
    [AllergyHistory_Food] BIT NOT NULL,
    [AllergyHistory_FoodDetails] VARCHAR(100) NULL,

    [MedicationHistory_Anticoagulant] BIT NOT NULL,
    [MedicationHistory_AnticoagulantDetails] VARCHAR(100) NULL,
    [MedicationHistory_Narcotics] BIT NOT NULL,
    [MedicationHistory_NarcoticsDetails] VARCHAR(100) NULL,
    [MedicationHistory_Insulin] BIT NOT NULL,
    [MedicationHistory_InsulinDetails] VARCHAR(100) NULL,
    [MedicationHistory_AntiHypertensives] BIT NOT NULL,
    [MedicationHistory_AntiHypertensivesDetails] VARCHAR(100) NULL,

    [PreviousScope_Date] DATETIME NULL,

    [FamilyHistory_FirstDegree] BIT NOT NULL,
    [FamilyHistory_SecondDegree] BIT NOT NULL,

    [PhysicalExamination_Details] VARCHAR(500) NOT NULL,

    [Investigation_FBC] BIT NOT NULL,
    [Investigation_BUSE] BIT NOT NULL,
    [Investigation_RBS] BIT NOT NULL,
    [Investigation_LFT] BIT NOT NULL,
    [Investigation_Coag] BIT NOT NULL,

    [Management_BowelPrep] BIT NOT NULL,
    [Management_Procedure] BIT NOT NULL,
    [Management_Consent] BIT NOT NULL,
    [Management_Advise] BIT NOT NULL,

    CONSTRAINT [FK_PatientAssessment_PatientJourney]
        FOREIGN KEY ([PatientJourney_ID]) REFERENCES [dbo].[PatientJourney]([PatientJourney_ID])
);
GO