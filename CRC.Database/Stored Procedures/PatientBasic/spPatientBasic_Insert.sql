CREATE PROCEDURE [dbo].[spPatientBasic_Insert]
(
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

    @NewPatient_ID                 VARCHAR(100) OUTPUT
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

    INSERT INTO [dbo].[PatientBasic]
    (
        [Patient_ID],
        [Patient_Name],
        [Patient_Email],
        [Patient_Phone],
        [Patient_NRIC],
        [Patient_AdmittedOn],
        [Patient_BirthDate],
        [Patient_Age],
        [Race_Name],
        [Branch_Name],
        [Source_Name],
        [Patient_Gender],
        [Religion_Name],
        [MaritalStatus_Name],
        [Patient_Address],
        [Patient_EmergencyName],
        [Patient_EmergencyRelationship],
        [Patient_EmergencyNumber],
        [Occupation_Name],
        [DischargeType_Name],
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
        @Patient_AdmittedOn,
        @Patient_BirthDate,
        @Patient_Age,
        @Race_Name,
        @Branch_Name,
        @Source_Name,
        @Patient_Gender,
        @Religion_Name,
        @MaritalStatus_Name,
        @Patient_Address,
        @Patient_EmergencyName,
        @Patient_EmergencyRelationship,
        @Patient_EmergencyNumber,
        @Occupation_Name,
        NULL,   -- DischargeType_Name
        NULL,   -- Patient_DischargeDate
        NULL    -- Patient_DischargeRemarks
    );
END;
GO