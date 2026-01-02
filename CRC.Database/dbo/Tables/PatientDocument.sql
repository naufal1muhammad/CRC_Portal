CREATE TABLE [dbo].[PatientDocument]
(
    [PatientDocument_ID]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Patient_ID]            VARCHAR(100) NOT NULL,
    [Patient_Name]          VARCHAR(100) NOT NULL,

    [PatientDocumentType_ID]   VARCHAR(100) NULL,
    [PatientDocumentType_Name] VARCHAR(100) NULL,

    [FileName]             VARCHAR(255) NOT NULL,
    [FilePath]             VARCHAR(500) NOT NULL,
    [ContentType]          VARCHAR(100) NOT NULL,
    [UploadedOn]           VARCHAR(100) NOT NULL
);
GO