CREATE TABLE [dbo].[PatientJourney]
(
    [PatientJourney_ID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    [Patient_ID]            VARCHAR(100) NOT NULL,
    [Patient_Name]          VARCHAR(100) NOT NULL,
    [Patient_Email]         VARCHAR(100) NOT NULL,
    [Patient_Phone]         VARCHAR(100) NOT NULL,

    [PjAppType_Name]        VARCHAR(100) NOT NULL,
    [PatientJourney_Date]   DATETIME     NOT NULL,

    [Staff_ID]              VARCHAR(100) NOT NULL,
    [Staff_Name]            VARCHAR(100) NOT NULL,

    [PatientJourney_Remarks] VARCHAR(MAX) NULL
);
GO

CREATE INDEX IX_PatientJourney_Patient_ID
    ON [dbo].[PatientJourney]([Patient_ID]);
GO