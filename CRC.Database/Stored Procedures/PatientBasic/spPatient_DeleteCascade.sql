CREATE PROCEDURE [dbo].[spPatient_DeleteCascade]
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Delete children first
    DELETE FROM [dbo].[PatientAppointment]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientJourney]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientDocument]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientFollowUp]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientColonoscopy]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientAssessment]
    WHERE [Patient_ID] = @Patient_ID;

    -- Finally delete from master
    DELETE FROM [dbo].[PatientBasic]
    WHERE [Patient_ID] = @Patient_ID;
END;
GO