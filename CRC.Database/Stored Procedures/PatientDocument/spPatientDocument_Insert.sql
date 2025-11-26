CREATE PROCEDURE [dbo].[spPatientDocument_Insert]
    @Patient_ID VARCHAR(100),
    @FileName   VARCHAR(255),
    @FilePath   VARCHAR(500),
    @ContentType VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[PatientDocument] (
        [Patient_ID],
        [FileName],
        [FilePath],
        [ContentType]
    )
    VALUES (
        @Patient_ID,
        @FileName,
        @FilePath,
        @ContentType
    );
END;