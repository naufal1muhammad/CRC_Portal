CREATE TABLE [dbo].[PatientJourney]
(
    [PatientJourney_ID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Patient_ID]            VARCHAR(100)  NOT NULL,
    [Patient_Name]          VARCHAR(100)  NOT NULL,
    [PjAppType_Name]        VARCHAR(100)  NOT NULL,
    [PatientJourney_Date]   DATETIME      NOT NULL,
    [Staff_ID]              VARCHAR(100)  NOT NULL,

    [Created_At]            DATETIME2(0)  NOT NULL CONSTRAINT [DF_PatientJourney_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [Updated_At]            DATETIME2(0)  NULL,
    [CreatedBy_Staff_ID]    VARCHAR(100)  NULL,
    [UpdatedBy_Staff_ID]    VARCHAR(100)  NULL
);
GO

CREATE INDEX [IX_PatientJourney_Patient_ID]
    ON [dbo].[PatientJourney]([Patient_ID]);
GO