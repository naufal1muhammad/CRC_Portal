CREATE TABLE [dbo].[StaffDocument]
(
    [StaffDocument_ID]       INT IDENTITY(1,1) NOT NULL,
    [Staff_ID]               VARCHAR(100) NOT NULL,
    [StaffDocumentType_ID]   VARCHAR(100) NULL,
    [FileName]               VARCHAR(255) NOT NULL,

    -- The key WITHIN the private blob container, e.g. 'staff/END-00003/4b7a....pdf'.
    -- It is NOT a URL and NOT a filesystem path: the file itself lives in Azure Blob
    -- Storage and is reached only through an authenticated endpoint that mints a
    -- short-lived read SAS. VARCHAR(500) is ample -- the longest key this app can
    -- produce is about 145 characters.
    [BlobName]               VARCHAR(500) NOT NULL,
    [ContentType]            VARCHAR(100) NOT NULL,
    [UploadedOn]             DATETIME NOT NULL,
    CONSTRAINT [PK_StaffDocument] PRIMARY KEY ([StaffDocument_ID])
);
GO
