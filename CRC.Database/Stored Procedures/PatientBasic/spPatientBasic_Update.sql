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
    @Patient_DischargeDate         DATETIME = NULL,
    @Patient_DischargeRemarks      VARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

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
END;
GO