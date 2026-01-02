CREATE TABLE [dbo].[Branch]
(
    [Branch_ID] VARCHAR(100) NOT NULL CONSTRAINT [PK_Branch] PRIMARY KEY,
    [Branch_Name] VARCHAR(100) NOT NULL,
    [Branch_Location] VARCHAR(200) NOT NULL,
    [Branch_State] VARCHAR(100) NOT NULL,
    [Branch_Status] BIT NOT NULL,
    [Organization_ID]   VARCHAR(100) NULL,
    [Organization_Name] VARCHAR(100) NULL
);
GO