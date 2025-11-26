CREATE TABLE [dbo].[Asset]
(
    [Asset_ID] VARCHAR(100) NOT NULL CONSTRAINT [PK_Asset] PRIMARY KEY,
    [Asset_Name] VARCHAR(100) NOT NULL,
    [Branch_ID] VARCHAR(100) NOT NULL,
    [Branch_Name] VARCHAR(100) NOT NULL,
    [Asset_Quantity] INT NOT NULL,
    [Asset_Cost] DECIMAL(18, 2) NOT NULL,
    [Asset_TotalCost] AS ([Asset_Quantity] * [Asset_Cost]) PERSISTED
);
GO