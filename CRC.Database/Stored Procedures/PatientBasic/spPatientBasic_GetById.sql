CREATE PROCEDURE [dbo].[spPatientBasic_GetById]
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[PatientBasic]
    WHERE [Patient_ID] = @Patient_ID;
END;
GO