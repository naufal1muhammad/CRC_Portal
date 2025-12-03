CREATE TABLE [dbo].[StaffDocument]
(
    [StaffDocument_ID]       INT IDENTITY(1,1) NOT NULL,
    [Staff_ID]               VARCHAR(100) NOT NULL,
    [Staff_Name]             VARCHAR(100) NOT NULL,
    [StaffDocumentType_ID]   VARCHAR(100) NULL,
    [StaffDocumentType_Name] VARCHAR(100) NULL,
    [FileName]               VARCHAR(255) NOT NULL,
    [FilePath]               VARCHAR(500) NOT NULL,
    [ContentType]            VARCHAR(100) NOT NULL,
    [UploadedOn]             DATETIME NOT NULL,
    CONSTRAINT [PK_StaffDocument] PRIMARY KEY ([StaffDocument_ID])
);
GO