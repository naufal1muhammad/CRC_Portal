CREATE PROCEDURE [dbo].[spPatientJourney_ListByPatient]
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [PatientJourney_ID],
        [Patient_ID],
        [Patient_Name],
        [Patient_Email],
        [Patient_Phone],
        [PjAppType_Name],
        [PatientJourney_Date],
        [Staff_ID],
        [Staff_Name],
        [PatientJourney_Remarks]
    FROM [dbo].[PatientJourney]
    WHERE [Patient_ID] = @Patient_ID
    ORDER BY [PatientJourney_Date] DESC, [PatientJourney_ID] DESC;
END;
GO