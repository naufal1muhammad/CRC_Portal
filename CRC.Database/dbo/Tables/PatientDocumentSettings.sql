CREATE TABLE [dbo].[PatientDocumentSettings]
(
    [DischargeType_ID]         VARCHAR(100) NOT NULL,
    [DischargeType_Name]       VARCHAR(100) NOT NULL,
    [PatientDocumentType_ID]   VARCHAR(100) NOT NULL,
    [PatientDocumentType_Name] VARCHAR(100) NOT NULL,

    CONSTRAINT PK_PatientDocumentSettings
        PRIMARY KEY ([DischargeType_ID], [PatientDocumentType_ID])
);
GO