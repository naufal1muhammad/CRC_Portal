CREATE TABLE [dbo].[StaffDocument]
(
    [StaffDocument_ID] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_StaffDocument] PRIMARY KEY,
    [Staff_ID] VARCHAR(100) NOT NULL,
    [Staff_Name] VARCHAR(100) NULL,
    [FileName] VARCHAR(255) NOT NULL,
    [FilePath] VARCHAR(500) NOT NULL, -- e.g. /uploads/staff/ABC/cert1.pdf
    [ContentType] VARCHAR(100) NULL,
    [UploadedOn] DATETIME2 NOT NULL CONSTRAINT [DF_StaffDocument_UploadedOn] DEFAULT (SYSUTCDATETIME())
);
GO