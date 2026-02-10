CREATE TABLE [dbo].[PatientBasic]
(
    [Patient_ID]                      VARCHAR(100) NOT NULL PRIMARY KEY,
    [Patient_Name]                    VARCHAR(100) NOT NULL,
    [Patient_Email]                   VARCHAR(100) NOT NULL,
    [Patient_Phone]                   VARCHAR(100) NOT NULL,
    [Patient_NRIC]                    VARCHAR(100) NOT NULL,
    [Patient_BirthDate]               DATETIME    NOT NULL,
    [Patient_Age]                     INT         NOT NULL,
    [Race_ID]                         VARCHAR(100) NOT NULL,
    [Source_ID]                       VARCHAR(100) NOT NULL,
    [Patient_Gender]                  VARCHAR(100) NOT NULL,
    [Religion_ID]                     VARCHAR(100) NOT NULL,
    [MaritalStatus_ID]                VARCHAR(100) NOT NULL,
    [Occupation_ID]                   VARCHAR(100) NOT NULL,

    [Patient_ResState]                VARCHAR(100) NOT NULL,
    [Patient_ResCity]                 VARCHAR(100) NOT NULL,
    [Patient_ResPostcode]             VARCHAR(100) NOT NULL,
    [Patient_AddLine1]                VARCHAR(MAX) NOT NULL,
    [Patient_AddLine2]                VARCHAR(MAX) NULL,

    [Patient_EmergencyName]           VARCHAR(100) NOT NULL,
    [Patient_EmergencyRelationship]   VARCHAR(100) NOT NULL,
    [Patient_EmergencyNumber]         VARCHAR(100) NOT NULL,

    -- Discharge info (NULL = Active; NOT NULL = Discharged)
    [DischargeType_ID]                VARCHAR(100) NULL,
    [Patient_DischargeDate]           DATETIME     NULL,
    [Patient_DischargeRemarks]        VARCHAR(MAX) NULL
);
GO