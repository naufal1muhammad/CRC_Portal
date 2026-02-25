CREATE TABLE [dbo].[PatientFollowUp]
(
    [PatientFollowUp_ID] INT IDENTITY(1,1) NOT NULL,
    [PatientJourney_ID] INT NOT NULL,
    [Patient_ID] VARCHAR(100) NOT NULL,

    [HPE_Results] VARCHAR(100) NOT NULL,
    [DischargePlan] VARCHAR(100) NOT NULL,
    [DischargeSummary_Status] BIT NOT NULL,

    CONSTRAINT [PK_PatientFollowUp]
        PRIMARY KEY CLUSTERED ([PatientFollowUp_ID] ASC),

    CONSTRAINT [FK_PatientFollowUp_PatientJourney]
        FOREIGN KEY ([PatientJourney_ID])
        REFERENCES [dbo].[PatientJourney] ([PatientJourney_ID])
)
GO