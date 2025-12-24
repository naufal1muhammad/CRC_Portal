CREATE PROCEDURE [dbo].[spPatientBasic_Update]
(
    @Patient_ID                    VARCHAR(100),

    @Patient_Name                  VARCHAR(100),
    @Patient_Email                 VARCHAR(100),
    @Patient_Phone                 VARCHAR(100),
    @Patient_NRIC                  VARCHAR(100),
    @Patient_AdmittedOn            DATETIME,
    @Patient_BirthDate             DATETIME,
    @Patient_Age                   INT,
    @Race_Name                     VARCHAR(100),
    @Branch_Name                   VARCHAR(100),
    @Source_Name                   VARCHAR(100),
    @Patient_Gender                VARCHAR(100),
    @Religion_Name                 VARCHAR(100),
    @MaritalStatus_Name            VARCHAR(100),
    @Patient_Address               VARCHAR(100),
    @Patient_EmergencyName         VARCHAR(100),
    @Patient_EmergencyRelationship VARCHAR(100),
    @Patient_EmergencyNumber       VARCHAR(100),
    @Occupation_Name               VARCHAR(100),

    @DischargeType_Name            VARCHAR(100) = NULL,
    @Patient_DischargeDate         DATETIME     = NULL,
    @Patient_DischargeRemarks      VARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    ---------------------------------
    -- 1) Update main PatientBasic
    ---------------------------------
    UPDATE [dbo].[PatientBasic]
    SET
        [Patient_Name]                  = @Patient_Name,
        [Patient_Email]                 = @Patient_Email,
        [Patient_Phone]                 = @Patient_Phone,
        [Patient_NRIC]                  = @Patient_NRIC,
        [Patient_AdmittedOn]            = @Patient_AdmittedOn,
        [Patient_BirthDate]             = @Patient_BirthDate,
        [Patient_Age]                   = @Patient_Age,
        [Race_Name]                     = @Race_Name,
        [Branch_Name]                   = @Branch_Name,
        [Source_Name]                   = @Source_Name,
        [Patient_Gender]                = @Patient_Gender,
        [Religion_Name]                 = @Religion_Name,
        [MaritalStatus_Name]            = @MaritalStatus_Name,
        [Patient_Address]               = @Patient_Address,
        [Patient_EmergencyName]         = @Patient_EmergencyName,
        [Patient_EmergencyRelationship] = @Patient_EmergencyRelationship,
        [Patient_EmergencyNumber]       = @Patient_EmergencyNumber,
        [Occupation_Name]               = @Occupation_Name,
        [DischargeType_Name]            = @DischargeType_Name,
        [Patient_DischargeDate]         = @Patient_DischargeDate,
        [Patient_DischargeRemarks]      = @Patient_DischargeRemarks
    WHERE [Patient_ID] = @Patient_ID;

    ---------------------------------
    -- 2) Cascade updates to children
    ---------------------------------

    -- PatientJourney: Patient_ID, Patient_Name
    UPDATE pj
    SET
        pj.[Patient_Name]  = @Patient_Name
    FROM [dbo].[PatientJourney] pj
    WHERE pj.[Patient_ID] = @Patient_ID;

    -- PatientFollowUp: Patient_ID, Patient_Name
    UPDATE pf
    SET
        pf.[Patient_Name]  = @Patient_Name
    FROM [dbo].[PatientFollowUp] pf
    WHERE pf.[Patient_ID] = @Patient_ID;

    -- PatientDocument: Patient_ID, Patient_Name
    UPDATE pd
    SET
        pd.[Patient_Name] = @Patient_Name
    FROM [dbo].[PatientDocument] pd
    WHERE pd.[Patient_ID] = @Patient_ID;

    -- PatientColonoscopy: Patient_ID, Patient_Name
    UPDATE pc
    SET
        pc.[Patient_Name] = @Patient_Name
    FROM [dbo].[PatientColonoscopy] pc
    WHERE pc.[Patient_ID] = @Patient_ID;

    -- PatientAssessment: Patient_ID, Patient_Name
    UPDATE pas
    SET
        pas.[Patient_Name] = @Patient_Name
    FROM [dbo].[PatientAssessment] pas
    WHERE pas.[Patient_ID] = @Patient_ID;

    -- PatientAppointment: Patient_ID, Patient_Name, Patient_Email, Patient_Phone
    UPDATE pa
    SET
        pa.[Patient_Name]  = @Patient_Name,
        pa.[Patient_Email] = @Patient_Email,
        pa.[Patient_Phone] = @Patient_Phone
    FROM [dbo].[PatientAppointment] pa
    WHERE pa.[Patient_ID] = @Patient_ID;

END;
GO