CREATE PROCEDURE [dbo].[spPatientJourney_Insert]
(
    @Patient_ID             VARCHAR(100),
    @PjAppType_Name         VARCHAR(100),
    @PatientJourney_Date    DATETIME,
    @Staff_ID               VARCHAR(100),
    @PatientJourney_Remarks VARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @Patient_Name  VARCHAR(100),
        @Patient_Email VARCHAR(100),
        @Patient_Phone VARCHAR(100),
        @Staff_Name    VARCHAR(100);

    SELECT
        @Patient_Name  = [Patient_Name],
        @Patient_Email = [Patient_Email],
        @Patient_Phone = [Patient_Phone]
    FROM [dbo].[PatientBasic]
    WHERE [Patient_ID] = @Patient_ID;

    IF @Patient_Name IS NULL
    BEGIN
        RAISERROR('Patient not found for the given Patient_ID.', 11, 1);
        RETURN;
    END

    SELECT
        @Staff_Name = [Staff_Name]
    FROM [dbo].[Staff]
    WHERE [Staff_ID] = @Staff_ID;

    IF @Staff_Name IS NULL
    BEGIN
        RAISERROR('Staff not found for the given Staff_ID.', 11, 1);
        RETURN;
    END

    INSERT INTO [dbo].[PatientJourney]
    (
        [Patient_ID],
        [Patient_Name],
        [Patient_Email],
        [Patient_Phone],
        [PjAppType_Name],
        [PatientJourney_Date],
        [Staff_ID],
        [Staff_Name],
        [PatientJourney_Remarks]
    )
    VALUES
    (
        @Patient_ID,
        @Patient_Name,
        @Patient_Email,
        @Patient_Phone,
        @PjAppType_Name,
        @PatientJourney_Date,
        @Staff_ID,
        @Staff_Name,
        @PatientJourney_Remarks
    );
END;
GO