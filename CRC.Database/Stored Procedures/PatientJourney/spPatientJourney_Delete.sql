CREATE PROCEDURE [dbo].[spPatientJourney_Delete]
(
    @PatientJourney_ID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[PatientJourney]
    WHERE [PatientJourney_ID] = @PatientJourney_ID;
END;
GO