CREATE TABLE [dbo].[StaffDocumentSettings]
(
    [StaffType_ID]           VARCHAR(100) NOT NULL,
    [StaffType_Name]         VARCHAR(100) NOT NULL,
    [StaffDocumentType_ID]   VARCHAR(100) NOT NULL,
    [StaffDocumentType_Name] VARCHAR(100) NOT NULL,
    CONSTRAINT [PK_StaffDocumentSettings]
        PRIMARY KEY ([StaffType_ID], [StaffDocumentType_ID])
);
GO