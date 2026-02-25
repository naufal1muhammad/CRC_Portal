CREATE PROCEDURE [dbo].[spPatientBasic_Update]
(
    @Patient_ID                    VARCHAR(100),

    @Patient_Name                  VARCHAR(100),
    @Patient_Email                 VARCHAR(100),
    @Patient_Phone                 VARCHAR(100),
    @Patient_NRIC                  VARCHAR(100),
    @Patient_BirthDate             DATETIME,
    @Patient_Age                   INT,
    @Race_ID                       VARCHAR(100),
    @Source_ID                     VARCHAR(100),
    @Patient_Gender                VARCHAR(100),
    @Religion_ID                   VARCHAR(100),
    @MaritalStatus_ID              VARCHAR(100),
    @Occupation_ID                 VARCHAR(100),

    @Patient_ResState              VARCHAR(100),
    @Patient_ResCity               VARCHAR(100),
    @Patient_ResPostcode           VARCHAR(100),
    @Patient_AddLine1              VARCHAR(MAX),
    @Patient_AddLine2              VARCHAR(MAX) = NULL,

    @Patient_EmergencyName         VARCHAR(100),
    @Patient_EmergencyRelationship VARCHAR(100),
    @Patient_EmergencyNumber       VARCHAR(100),

    @Patient_iFOBTStatus             BIT = NULL,
    @Patient_iFOBTCompletionDate     DATE = NULL,
    @Patient_iFOBTResults            BIT = NULL,
    @Patient_IsApproved              BIT = NULL,

    @DischargeType_ID              VARCHAR(100) = NULL,
    @Patient_DischargeDate         DATETIME     = NULL,
    @Patient_DischargeRemarks      VARCHAR(MAX) = NULL,
    @User_ID                       INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AddLine2Clean VARCHAR(MAX) =
        NULLIF(LTRIM(RTRIM(ISNULL(@Patient_AddLine2, ''))), '');

    DECLARE @iFOBTCompletionDateClean DATE =
        CASE WHEN @Patient_iFOBTStatus = 1 THEN @Patient_iFOBTCompletionDate ELSE NULL END;

    DECLARE @iFOBTResultsClean BIT =
        CASE WHEN @Patient_iFOBTStatus = 1 THEN @Patient_iFOBTResults ELSE NULL END;

    ---------------------------------
    -- 1) Update main PatientBasic
    ---------------------------------
    UPDATE [dbo].[PatientBasic]
    SET
        [Patient_Name]                  = @Patient_Name,
        [Patient_Email]                 = @Patient_Email,
        [Patient_Phone]                 = @Patient_Phone,
        [Patient_NRIC]                  = @Patient_NRIC,
        [Patient_BirthDate]             = @Patient_BirthDate,
        [Patient_Age]                   = @Patient_Age,
        [Race_ID]                       = @Race_ID,
        [Source_ID]                     = @Source_ID,
        [Patient_Gender]                = @Patient_Gender,
        [Religion_ID]                   = @Religion_ID,
        [MaritalStatus_ID]              = @MaritalStatus_ID,
        [Occupation_ID]                 = @Occupation_ID,
        [Patient_ResState]              = @Patient_ResState,
        [Patient_ResCity]               = @Patient_ResCity,
        [Patient_ResPostcode]           = @Patient_ResPostcode,
        [Patient_AddLine1]              = @Patient_AddLine1,
        [Patient_AddLine2]              = @AddLine2Clean,
        [Patient_EmergencyName]         = @Patient_EmergencyName,
        [Patient_EmergencyRelationship] = @Patient_EmergencyRelationship,
        [Patient_EmergencyNumber]       = @Patient_EmergencyNumber,
        [Patient_iFOBTStatus]            = @Patient_iFOBTStatus,
        [Patient_iFOBTCompletionDate]    = @iFOBTCompletionDateClean,
        [Patient_iFOBTResults]           = @iFOBTResultsClean,
        [Patient_IsApproved]             = COALESCE(@Patient_IsApproved, [Patient_IsApproved]),
        [DischargeType_ID]              = @DischargeType_ID,
        [Patient_DischargeDate]         = @Patient_DischargeDate,
        [Patient_DischargeRemarks]      = @Patient_DischargeRemarks
    WHERE [Patient_ID] = @Patient_ID;

    DECLARE @RowsAffected INT = @@ROWCOUNT;

    ---------------------------------
    -- 2) Cascade updates to children
    ---------------------------------

    -- PatientJourney: Patient_ID, Patient_Name
    UPDATE pj
    SET
        pj.[Patient_Name]  = @Patient_Name
    FROM [dbo].[PatientJourney] pj
    WHERE pj.[Patient_ID] = @Patient_ID;

    IF @RowsAffected > 0
    BEGIN
        -- -----------------------------
        -- Audit trail
        -- -----------------------------
        INSERT INTO [dbo].[AuditTrails]
        (
            [User_Id],
            [AuditTrail_Action],
            [AuditTrail_Category],
            [AuditTrail_Summary]
        )
        VALUES
        (
            ISNULL(@User_ID, 0),
            'UPDATE',
            'PatientBasic',
            CONCAT(
                'Updated Patient: Patient_ID=', @Patient_ID,
                '; Name=', @Patient_Name,
                '; NRIC=', @Patient_NRIC,
                '; Phone=', @Patient_Phone,
                '; Email=', @Patient_Email,
                '; DischargeType_ID=', ISNULL(@DischargeType_ID, '')
            )
        );
    END
END;
GO