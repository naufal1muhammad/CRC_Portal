CREATE PROCEDURE [dbo].[spPatientColonoscopy_CreateWithJourney]
(
    @Patient_ID VARCHAR(100),
    @PatientJourney_Date DATETIME,
    @Staff_ID VARCHAR(100),
    @Audit_Note VARCHAR(500) = NULL,

    @ColonoscopyStatus BIT,
    @ColonoscopyStatus_Details VARCHAR(500) = NULL,

    @BowelPreparation INT,

    @Findings_Anus BIT,
    @Findings_AnusDetails VARCHAR(500) = NULL,
    @Findings_Rectum BIT,
    @Findings_RectumDetails VARCHAR(500) = NULL,
    @Findings_SigmoidColon BIT,
    @Findings_SigmoidColonDetails VARCHAR(500) = NULL,
    @Findings_DescendingColon BIT,
    @Findings_DescendingColonDetails VARCHAR(500) = NULL,
    @Findings_SplenicFlexure BIT,
    @Findings_SplenicFlexureDetails VARCHAR(500) = NULL,
    @Findings_TransverseColon BIT,
    @Findings_TransverseColonDetails VARCHAR(500) = NULL,
    @Findings_HepaticFlexure BIT,
    @Findings_HepaticFlexureDetails VARCHAR(500) = NULL,
    @Findings_AscendingColon BIT,
    @Findings_AscendingColonDetails VARCHAR(500) = NULL,
    @Findings_Caecum BIT,
    @Findings_CaecumDetails VARCHAR(500) = NULL,

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

        DECLARE @Patient_Name VARCHAR(100);
        SELECT @Patient_Name = pb.Patient_Name
        FROM dbo.PatientBasic pb
        WHERE pb.Patient_ID = @Patient_ID;

        IF (@Patient_Name IS NULL OR LTRIM(RTRIM(@Patient_Name)) = '')
            RAISERROR('Patient not found.', 16, 1);

        DECLARE @Staff_Name VARCHAR(100);
        SELECT @Staff_Name = s.Staff_Name
        FROM dbo.Staff s
        WHERE s.Staff_ID = @Staff_ID;

        IF (@Staff_Name IS NULL OR LTRIM(RTRIM(@Staff_Name)) = '')
            RAISERROR('Staff not found.', 16, 1);

        -- Let IDENTITY generate PatientJourney_ID (same structure as Assessment)
        INSERT INTO dbo.PatientJourney
        (
            Patient_ID,
            Patient_Name,
            PjAppType_Name,
            PatientJourney_Date,
            Staff_ID,
            CreatedBy_Staff_ID
        )
        VALUES
        (
            @Patient_ID,
            @Patient_Name,
            'PATIENT COLONOSCOPY',
            @PatientJourney_Date,
            @Staff_ID,
            @Staff_ID
        );

        DECLARE @PatientJourney_ID INT = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO dbo.PatientColonoscopy
        (
            PatientJourney_ID,
            Patient_ID,

            ColonoscopyStatus,
            ColonoscopyStatus_Details,
            BowelPreparation,

            Findings_Anus,
            Findings_AnusDetails,
            Findings_Rectum,
            Findings_RectumDetails,
            Findings_SigmoidColon,
            Findings_SigmoidColonDetails,
            Findings_DescendingColon,
            Findings_DescendingColonDetails,
            Findings_SplenicFlexure,
            Findings_SplenicFlexureDetails,
            Findings_TransverseColon,
            Findings_TransverseColonDetails,
            Findings_HepaticFlexure,
            Findings_HepaticFlexureDetails,
            Findings_AscendingColon,
            Findings_AscendingColonDetails,
            Findings_Caecum,
            Findings_CaecumDetails,

            HPE_Status,
            HPE_Details,

            Complications,
            Complications_Details,

            DischargePlan
        )
        VALUES
        (
            @PatientJourney_ID,
            @Patient_ID,

            @ColonoscopyStatus,
            @ColonoscopyStatus_Details,
            @BowelPreparation,

            @Findings_Anus,
            @Findings_AnusDetails,
            @Findings_Rectum,
            @Findings_RectumDetails,
            @Findings_SigmoidColon,
            @Findings_SigmoidColonDetails,
            @Findings_DescendingColon,
            @Findings_DescendingColonDetails,
            @Findings_SplenicFlexure,
            @Findings_SplenicFlexureDetails,
            @Findings_TransverseColon,
            @Findings_TransverseColonDetails,
            @Findings_HepaticFlexure,
            @Findings_HepaticFlexureDetails,
            @Findings_AscendingColon,
            @Findings_AscendingColonDetails,
            @Findings_Caecum,
            @Findings_CaecumDetails,

            @HPE_Status,
            @HPE_Details,

            @Complications,
            @Complications_Details,

            @DischargePlan
        );

        -- Audit insert now matches Assessment structure exactly
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