CREATE PROCEDURE dbo.spPatientAssessment_GetByJourneyId
    @PatientJourney_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        pj.PatientJourney_ID,
        pj.Patient_ID,
        pj.Patient_Name,
        pj.PjAppType_Name,
        pj.PatientJourney_Date,
        pj.Staff_ID,

        pa.PatientAssessment_ID,
        pa.iFOBTPositive_Date,
        pa.Risks_Smoking,
        pa.Risks_AlcoholConsumption,
        pa.Risks_InflammatoryBowelDisease,
        pa.Risks_Diet,
        pa.Risks_SedentaryLifestyle,
        pa.Symptoms_WeightLoss,
        pa.Symptoms_AppetiteLoss,
        pa.Symptoms_Lethargic,
        pa.Symptoms_AbdominalPain,
        pa.Symptoms_Constipation,
        pa.Symptoms_Diarrhea,
        pa.Symptoms_RectalBleedingMucous,
        pa.Symptoms_RectalBleedingNoMucous,
        pa.Symptoms_Tenesmus,
        pa.MedicalHistory_Diabetes,
        pa.MedicalHistory_Hypertension,
        pa.MedicalHistory_Dyslipidemia,
        pa.MedicalHistory_Bleeding,
        pa.MedicalHistory_Asthma,
        pa.AllergyHistory_Medication,
        pa.AllergyHistory_MedicationDetails,
        pa.AllergyHistory_Food,
        pa.AllergyHistory_FoodDetails,
        pa.MedicationHistory_Anticoagulant,
        pa.MedicationHistory_AnticoagulantDetails,
        pa.MedicationHistory_Narcotics,
        pa.MedicationHistory_NarcoticsDetails,
        pa.MedicationHistory_Insulin,
        pa.MedicationHistory_InsulinDetails,
        pa.MedicationHistory_AntiHypertensives,
        pa.MedicationHistory_AntiHypertensivesDetails,
        pa.PreviousScope_Date,
        pa.FamilyHistory_FirstDegree,
        pa.FamilyHistory_SecondDegree,
        pa.PhysicalExamination_Details,
        pa.Investigation_FBC,
        pa.Investigation_BUSE,
        pa.Investigation_RBS,
        pa.Investigation_LFT,
        pa.Investigation_Coag,
        pa.Management_BowelPrep,
        pa.Management_Procedure,
        pa.Management_Consent,
        pa.Management_Advise
    FROM dbo.PatientJourney pj
    INNER JOIN dbo.PatientAssessment pa
        ON pa.PatientJourney_ID = pj.PatientJourney_ID
    WHERE pj.PatientJourney_ID = @PatientJourney_ID;
END
GO