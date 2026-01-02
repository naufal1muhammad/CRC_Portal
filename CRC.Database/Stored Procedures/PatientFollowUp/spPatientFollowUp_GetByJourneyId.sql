CREATE PROCEDURE dbo.spPatientFollowUp_GetByJourneyId
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

        pf.PatientFollowUp_ID,
        pf.HPE_Results,
        pf.DischargePlan,
        pf.DischargeSummary_Status
    FROM dbo.PatientJourney pj
    INNER JOIN dbo.PatientFollowUp pf
        ON pf.PatientJourney_ID = pj.PatientJourney_ID
    WHERE pj.PatientJourney_ID = @PatientJourney_ID;
END
GO