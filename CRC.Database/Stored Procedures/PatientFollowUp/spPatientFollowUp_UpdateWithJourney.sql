CREATE PROCEDURE dbo.spPatientFollowUp_UpdateWithJourney
(
    @PatientJourney_ID INT,
    @PatientJourney_Date DATETIME,
    @Staff_ID VARCHAR(100),
    @Audit_Note VARCHAR(500) = NULL,

    @HPE_Results VARCHAR(100),
    @DischargePlan VARCHAR(100),
    @DischargeSummary_Status BIT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        IF NOT EXISTS (SELECT 1 FROM dbo.PatientJourney WHERE PatientJourney_ID = @PatientJourney_ID)
        BEGIN
            RAISERROR('Journey not found.', 16, 1);
        END

        DECLARE @Staff_Name VARCHAR(100);
        SELECT @Staff_Name = s.Staff_Name
        FROM dbo.Staff s
        WHERE s.Staff_ID = @Staff_ID;

        IF (@Staff_Name IS NULL OR LTRIM(RTRIM(@Staff_Name)) = '')
        BEGIN
            RAISERROR('Staff not found.', 16, 1);
        END

        UPDATE dbo.PatientJourney
        SET
            PatientJourney_Date = @PatientJourney_Date,
            Updated_At = SYSUTCDATETIME(),
            UpdatedBy_Staff_ID = @Staff_ID
        WHERE PatientJourney_ID = @PatientJourney_ID;

        UPDATE dbo.PatientFollowUp
        SET
            HPE_Results = @HPE_Results,
            DischargePlan = @DischargePlan,
            DischargeSummary_Status = @DischargeSummary_Status
        WHERE PatientJourney_ID = @PatientJourney_ID;

        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('Follow up row not found for this journey.', 16, 1);
        END

        INSERT INTO dbo.PatientJourneyAudit
        (
            PatientJourney_ID,
            Audit_Action,
            Staff_ID,
            Audit_Note
        )
        VALUES
        (
            @PatientJourney_ID,
            'UPDATED',
            @Staff_ID,
            @Audit_Note
        );

        COMMIT;

        SELECT 1 AS Success;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO