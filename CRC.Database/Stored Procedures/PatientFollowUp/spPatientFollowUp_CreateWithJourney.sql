CREATE PROCEDURE dbo.spPatientFollowUp_CreateWithJourney
(
    @Patient_ID VARCHAR(100),
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

        -- Match Assessment: PatientBasic.Patient_Name
        DECLARE @Patient_Name VARCHAR(100);
        SELECT @Patient_Name = pb.Patient_Name
        FROM dbo.PatientBasic pb
        WHERE pb.Patient_ID = @Patient_ID;

        IF (@Patient_Name IS NULL OR LTRIM(RTRIM(@Patient_Name)) = '')
            RAISERROR('Patient not found.', 16, 1);

        -- Match Assessment: Staff validation
        DECLARE @Staff_Name VARCHAR(100);
        SELECT @Staff_Name = s.Staff_Name
        FROM dbo.Staff s
        WHERE s.Staff_ID = @Staff_ID;

        IF (@Staff_Name IS NULL OR LTRIM(RTRIM(@Staff_Name)) = '')
            RAISERROR('Staff not found.', 16, 1);

        -- Match Assessment: PatientJourney insert shape
        INSERT INTO dbo.PatientJourney
        (
            Patient_ID,
            PjAppType_Name,
            PatientJourney_Date,
            Staff_ID,
            CreatedBy_Staff_ID
        )
        VALUES
        (
            @Patient_ID,
            'FOLLOW UP',
            @PatientJourney_Date,
            @Staff_ID,
            @Staff_ID
        );

        DECLARE @PatientJourney_ID INT = CAST(SCOPE_IDENTITY() AS INT);

        -- Template row
        INSERT INTO dbo.PatientFollowUp
        (
            PatientJourney_ID,
            Patient_ID,
            HPE_Results,
            DischargePlan,
            DischargeSummary_Status
        )
        VALUES
        (
            @PatientJourney_ID,
            @Patient_ID,
            @HPE_Results,
            @DischargePlan,
            @DischargeSummary_Status
        );

        -- Match Assessment: Audit columns
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
            'CREATED',
            @Staff_ID,
            @Audit_Note
        );

        COMMIT;

        SELECT @PatientJourney_ID AS PatientJourney_ID;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO