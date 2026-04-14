CREATE PROCEDURE [dbo].[spPatientBasic_Insert]
(
    @Patient_Name                    VARCHAR(100),
    @Patient_Email                   VARCHAR(100),
    @Patient_Phone                   VARCHAR(100),
    @Patient_NRIC                    VARCHAR(100),
    @Patient_BirthDate               DATETIME,
    @Patient_Age                     INT,
    @Race_ID                         VARCHAR(100),
    @Source_ID                       VARCHAR(100),
    @Patient_Gender                  VARCHAR(100),
    @Religion_ID                     VARCHAR(100),
    @MaritalStatus_ID                VARCHAR(100),
    @Occupation_ID                   VARCHAR(100),

    @Patient_ResState                VARCHAR(100),
    @Patient_ResCity                 VARCHAR(100),
    @Patient_ResPostcode             VARCHAR(100),
    @Patient_AddLine1                VARCHAR(MAX),
    @Patient_AddLine2                VARCHAR(MAX) = NULL,

    @Patient_EmergencyName           VARCHAR(100),
    @Patient_EmergencyRelationship   VARCHAR(100),
    @Patient_EmergencyNumber         VARCHAR(100),

@Patient_iFOBTStatus             BIT = NULL,
@Patient_iFOBTCompletionDate     DATE = NULL,
@Patient_iFOBTResults            BIT = NULL,

    @NewPatient_ID                   VARCHAR(100) OUTPUT,
    @User_ID                         INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LastNum INT;

    SELECT @LastNum =
        MAX(CAST(SUBSTRING(Patient_ID, 5, 6) AS INT))
    FROM [dbo].[PatientBasic]
    WHERE Patient_ID LIKE 'PAT-%';

    IF @LastNum IS NULL SET @LastNum = 0;

    DECLARE @NextNum INT = @LastNum + 1;

    SET @NewPatient_ID = 'PAT-' + RIGHT('000000' + CAST(@NextNum AS VARCHAR(6)), 6);

    DECLARE @AddLine2Clean VARCHAR(MAX) =
        NULLIF(LTRIM(RTRIM(ISNULL(@Patient_AddLine2, ''))), '');

    DECLARE @iFOBTCompletionDateClean DATE =
        CASE WHEN @Patient_iFOBTStatus = 1 THEN @Patient_iFOBTCompletionDate ELSE NULL END;

    DECLARE @iFOBTResultsClean BIT =
        CASE WHEN @Patient_iFOBTStatus = 1 THEN @Patient_iFOBTResults ELSE NULL END;

    INSERT INTO [dbo].[PatientBasic]
    (
        [Patient_ID],
        [Patient_Name],
        [Patient_Email],
        [Patient_Phone],
        [Patient_NRIC],
        [Patient_BirthDate],
        [Patient_Age],
        [Race_ID],
        [Source_ID],
        [Patient_Gender],
        [Religion_ID],
        [MaritalStatus_ID],
        [Occupation_ID],
        [Patient_ResState],
        [Patient_ResCity],
        [Patient_ResPostcode],
        [Patient_AddLine1],
        [Patient_AddLine2],
        [Patient_EmergencyName],
        [Patient_EmergencyRelationship],
        [Patient_EmergencyNumber],
        [Patient_iFOBTStatus],
        [Patient_iFOBTCompletionDate],
        [Patient_iFOBTResults],
        [DischargeType_ID],
        [Patient_DischargeDate],
        [Patient_DischargeRemarks]
    )
    VALUES
    (
        @NewPatient_ID,
        @Patient_Name,
        @Patient_Email,
        @Patient_Phone,
        @Patient_NRIC,
        @Patient_BirthDate,
        @Patient_Age,
        @Race_ID,
        @Source_ID,
        @Patient_Gender,
        @Religion_ID,
        @MaritalStatus_ID,
        @Occupation_ID,
        @Patient_ResState,
        @Patient_ResCity,
        @Patient_ResPostcode,
        @Patient_AddLine1,
        @AddLine2Clean,
        @Patient_EmergencyName,
        @Patient_EmergencyRelationship,
        @Patient_EmergencyNumber,
        @Patient_iFOBTStatus,
        @iFOBTCompletionDateClean,
        @iFOBTResultsClean,
        NULL,   -- DischargeType_ID
        NULL,   -- Patient_DischargeDate
        NULL    -- Patient_DischargeRemarks
    );

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
        'INSERT',
        'PatientBasic',
        CONCAT(
            'Created Patient: Patient_ID=', @NewPatient_ID,
            '; Name=', @Patient_Name,
            '; NRIC=', @Patient_NRIC,
            '; Phone=', @Patient_Phone,
            '; Email=', @Patient_Email
        )
    );
END;
GO