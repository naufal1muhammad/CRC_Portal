CREATE PROCEDURE [dbo].[spPatientDocument_Delete]
    @PatientDocument_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[PatientDocument]
    WHERE [PatientDocument_ID] = @PatientDocument_ID;
END;