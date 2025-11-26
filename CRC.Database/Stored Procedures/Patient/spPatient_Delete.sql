CREATE PROCEDURE [dbo].[spPatient_Delete]
    @Patient_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Patient]
    WHERE [Patient_ID] = @Patient_ID;
END;