CREATE PROCEDURE [dbo].[spPatientColonoscopy_UpdateWithJourney]
(
    @PatientJourney_ID INT,
    @PatientJourney_Date DATETIME,
    @Staff_ID VARCHAR(100),
    @Audit_Note VARCHAR(500) = NULL,

    @ColonoscopyStatus BIT,
    @ColonoscopyStatus_Details VARCHAR(500) = NULL,

    @BowelPreparation INT,

    @Findings_Anus BIT,
    @Findings_AnusDetails NVARCHAR(MAX) = NULL,
    @Findings_Rectum BIT,
    @Findings_RectumDetails NVARCHAR(MAX) = NULL,
    @Findings_SigmoidColon BIT,
    @Findings_SigmoidColonDetails NVARCHAR(MAX) = NULL,
    @Findings_DescendingColon BIT,
    @Findings_DescendingColonDetails NVARCHAR(MAX) = NULL,
    @Findings_SplenicFlexure BIT,
    @Findings_SplenicFlexureDetails NVARCHAR(MAX) = NULL,
    @Findings_TransverseColon BIT,
    @Findings_TransverseColonDetails NVARCHAR(MAX) = NULL,
    @Findings_HepaticFlexure BIT,
    @Findings_HepaticFlexureDetails NVARCHAR(MAX) = NULL,
    @Findings_AscendingColon BIT,
    @Findings_AscendingColonDetails NVARCHAR(MAX) = NULL,
    @Findings_Caecum BIT,
    @Findings_CaecumDetails NVARCHAR(MAX) = NULL,

    @HPE_Status BIT,
    @HPE_Details VARCHAR(500) = NULL,

    @Complications VARCHAR(100),
    @Complications_Details VARCHAR(500) = NULL,

    @DischargePlan VARCHAR(100)
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

        -- Update journey (match Assessment)
        UPDATE dbo.PatientJourney
        SET
            PatientJourney_Date = @PatientJourney_Date,
            Updated_At = SYSUTCDATETIME(),
            UpdatedBy_Staff_ID = @Staff_ID
        WHERE PatientJourney_ID = @PatientJourney_ID;

        -- Update template row
        UPDATE dbo.PatientColonoscopy
        SET
            ColonoscopyStatus = @ColonoscopyStatus,
            ColonoscopyStatus_Details = @ColonoscopyStatus_Details,
            BowelPreparation = @BowelPreparation,

            Findings_Anus = @Findings_Anus,
            Findings_AnusDetails = @Findings_AnusDetails,
            Findings_Rectum = @Findings_Rectum,
            Findings_RectumDetails = @Findings_RectumDetails,
            Findings_SigmoidColon = @Findings_SigmoidColon,
            Findings_SigmoidColonDetails = @Findings_SigmoidColonDetails,
            Findings_DescendingColon = @Findings_DescendingColon,
            Findings_DescendingColonDetails = @Findings_DescendingColonDetails,
            Findings_SplenicFlexure = @Findings_SplenicFlexure,
            Findings_SplenicFlexureDetails = @Findings_SplenicFlexureDetails,
            Findings_TransverseColon = @Findings_TransverseColon,
            Findings_TransverseColonDetails = @Findings_TransverseColonDetails,
            Findings_HepaticFlexure = @Findings_HepaticFlexure,
            Findings_HepaticFlexureDetails = @Findings_HepaticFlexureDetails,
            Findings_AscendingColon = @Findings_AscendingColon,
            Findings_AscendingColonDetails = @Findings_AscendingColonDetails,
            Findings_Caecum = @Findings_Caecum,
            Findings_CaecumDetails = @Findings_CaecumDetails,

            HPE_Status = @HPE_Status,
            HPE_Details = @HPE_Details,

            Complications = @Complications,
            Complications_Details = @Complications_Details,

            DischargePlan = @DischargePlan
        WHERE PatientJourney_ID = @PatientJourney_ID;

        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('Colonoscopy row not found for this journey.', 16, 1);
        END

        -- Audit (match Assessment)
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