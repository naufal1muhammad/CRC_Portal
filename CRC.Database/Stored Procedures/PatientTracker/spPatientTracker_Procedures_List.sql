CREATE PROCEDURE [dbo].[spPatientTracker_Procedures_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pj.[Patient_ID],
        pj.[PjAppType_Name],
        pj.[PatientJourney_Date]
    FROM [dbo].[PatientJourney] pj
    ORDER BY pj.[Patient_ID] ASC,
             pj.[PatientJourney_Date] ASC,
             pj.[PatientJourney_ID] ASC;
END;
GO
