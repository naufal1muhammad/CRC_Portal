CREATE PROCEDURE [dbo].[spPatientJourney_Update]
(
    @PatientJourney_ID      INT,
    @PjAppType_Name         VARCHAR(100),
    @PatientJourney_Date    DATETIME,
    @Staff_ID               VARCHAR(100),
    @PatientJourney_Remarks VARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Staff_Name VARCHAR(100);

    -- Get staff name
    SELECT
        @Staff_Name = [Staff_Name]
    FROM [dbo].[Staff]
    WHERE [Staff_ID] = @Staff_ID;

    IF @Staff_Name IS NULL
    BEGIN
        RAISERROR('Staff not found for the given Staff_ID.', 11, 1);
        RETURN;
    END

    UPDATE [dbo].[PatientJourney]
    SET
        [PjAppType_Name]        = @PjAppType_Name,
        [PatientJourney_Date]   = @PatientJourney_Date,
        [Staff_ID]              = @Staff_ID,
        [Staff_Name]            = @Staff_Name,
        [PatientJourney_Remarks]= @PatientJourney_Remarks
    WHERE [PatientJourney_ID] = @PatientJourney_ID;
END;
GO