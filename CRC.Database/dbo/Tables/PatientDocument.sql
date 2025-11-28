CREATE TABLE [dbo].[PatientDocument]
(
    [PatientDocument_ID] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PatientDocument] PRIMARY KEY,
    [Patient_ID] VARCHAR(100) NOT NULL,
    [Patient_Name] VARCHAR(100) NULL,
    [FileName] VARCHAR(255) NOT NULL,
    [FilePath] VARCHAR(500) NOT NULL, -- e.g. /uploads/patient/123/report1.pdf
    [ContentType] VARCHAR(100) NULL,  -- e.g. application/pdf
    [UploadedOn] DATETIME2 NOT NULL CONSTRAINT [DF_PatientDocument_UploadedOn] DEFAULT (SYSUTCDATETIME())
);
GO