CREATE TABLE [dbo].[PatientDocument]
(
    [PatientDocument_ID]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Patient_ID]             VARCHAR(100) NOT NULL,

    [PatientDocumentType_ID] VARCHAR(100) NULL,

    [FileName]               VARCHAR(255) NOT NULL,

    -- The key WITHIN the private blob container, e.g. 'patients/PAT-000042/9f1c....pdf'.
    -- It is NOT a URL and NOT a filesystem path: the file itself lives in Azure Blob
    -- Storage and is reached only through an authenticated endpoint that mints a
    -- short-lived read SAS. VARCHAR(500) is ample -- the longest key this app can
    -- produce is about 145 characters.
    [BlobName]               VARCHAR(500) NOT NULL,
    [ContentType]            VARCHAR(100) NOT NULL,
    [UploadedOn]             VARCHAR(100) NOT NULL
);
GO