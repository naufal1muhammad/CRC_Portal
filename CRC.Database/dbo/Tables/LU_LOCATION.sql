CREATE TABLE [dbo].[LU_LOCATION]
(
    [LocationId] INT IDENTITY(1,1) NOT NULL,
    [LocationType] TINYINT NOT NULL,
    [ParentId] INT NULL,
    [Name] NVARCHAR(150) NOT NULL,
    [SortOrder] INT NULL,
    CONSTRAINT [PK_LU_LOCATION] PRIMARY KEY ([LocationId]),
    CONSTRAINT [FK_LU_LOCATION_Parent]
        FOREIGN KEY ([ParentId]) REFERENCES [dbo].[LU_LOCATION]([LocationId])
);
GO

CREATE INDEX [IX_LU_LOCATION_Type_Parent]
ON [dbo].[LU_LOCATION]([LocationType], [ParentId])
INCLUDE ([Name], [SortOrder]);
GO

CREATE UNIQUE INDEX [UX_LU_LOCATION_StateName]
ON [dbo].[LU_LOCATION]([Name])
WHERE [LocationType] = 1;
GO

CREATE UNIQUE INDEX [UX_LU_LOCATION_CityName_PerState]
ON [dbo].[LU_LOCATION]([ParentId], [Name])
WHERE [LocationType] = 2;
GO

CREATE UNIQUE INDEX [UX_LU_LOCATION_Postcode_PerCity]
ON [dbo].[LU_LOCATION]([ParentId], [Name])
WHERE [LocationType] = 3;
GO
