CREATE TABLE [dbo].[PatientBasic]
(
    [Patient_ID]                   VARCHAR(100) NOT NULL PRIMARY KEY,
    [Patient_Name]                 VARCHAR(100) NOT NULL,
    [Patient_Email]                VARCHAR(100) NOT NULL,
    [Patient_Phone]                VARCHAR(100) NOT NULL,
    [Patient_NRIC]                 VARCHAR(100) NOT NULL,
    [Patient_AdmittedOn]           DATETIME    NOT NULL,
    [Patient_BirthDate]            DATETIME    NOT NULL,
    [Patient_Age]                  INT         NOT NULL,
    [Race_Name]                    VARCHAR(100) NOT NULL,
    [Branch_Name]                  VARCHAR(100) NOT NULL,
    [Source_Name]                  VARCHAR(100) NOT NULL,
    [Patient_Gender]               VARCHAR(100) NOT NULL,
    [Religion_Name]                VARCHAR(100) NOT NULL,
    [MaritalStatus_Name]           VARCHAR(100) NOT NULL,
    [Patient_Address]              VARCHAR(100) NOT NULL,
    [Patient_EmergencyName]        VARCHAR(100) NOT NULL,
    [Patient_EmergencyRelationship] VARCHAR(100) NOT NULL,
    [Patient_EmergencyNumber]      VARCHAR(100) NOT NULL,
    [Occupation_Name]              VARCHAR(100) NOT NULL,

    -- Discharge info (NULL = Active; NOT NULL = Discharged)
    [DischargeType_Name]           VARCHAR(100) NULL,
    [Patient_DischargeDate]        DATETIME     NULL,
    [Patient_DischargeRemarks]     VARCHAR(MAX) NULL
);
GO