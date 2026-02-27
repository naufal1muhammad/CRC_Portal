CREATE TABLE [dbo].[PatientColonoscopy]
(
    [PatientColonoscopy_ID] INT IDENTITY(1,1) NOT NULL,
    [PatientJourney_ID] INT NOT NULL,
    [Patient_ID] VARCHAR(100) NOT NULL,

    [ColonoscopyStatus] BIT NOT NULL,
    [ColonoscopyStatus_Details] VARCHAR(500) NULL,

    [BowelPreparation] INT NOT NULL,

    [Findings_Anus] BIT NOT NULL,
    [Findings_AnusDetails] NVARCHAR(MAX) NULL,

    [Findings_Rectum] BIT NOT NULL,
    [Findings_RectumDetails] NVARCHAR(MAX) NULL,

    [Findings_SigmoidColon] BIT NOT NULL,
    [Findings_SigmoidColonDetails] NVARCHAR(MAX) NULL,

    [Findings_DescendingColon] BIT NOT NULL,
    [Findings_DescendingColonDetails] NVARCHAR(MAX) NULL,

    [Findings_SplenicFlexure] BIT NOT NULL,
    [Findings_SplenicFlexureDetails] NVARCHAR(MAX) NULL,

    [Findings_TransverseColon] BIT NOT NULL,
    [Findings_TransverseColonDetails] NVARCHAR(MAX) NULL,

    [Findings_HepaticFlexure] BIT NOT NULL,
    [Findings_HepaticFlexureDetails] NVARCHAR(MAX) NULL,

    [Findings_AscendingColon] BIT NOT NULL,
    [Findings_AscendingColonDetails] NVARCHAR(MAX) NULL,

    [Findings_Caecum] BIT NOT NULL,
    [Findings_CaecumDetails] NVARCHAR(MAX) NULL,

    [HPE_Status] BIT NOT NULL,
    [HPE_Details] VARCHAR(500) NULL,

    [Complications] VARCHAR(100) NOT NULL,
    [Complications_Details] VARCHAR(500) NULL,

    [DischargePlan] VARCHAR(100) NOT NULL,

    [Medication_Details] NVARCHAR(MAX) NULL,

    CONSTRAINT [PK_PatientColonoscopy]
        PRIMARY KEY CLUSTERED ([PatientColonoscopy_ID] ASC),

    CONSTRAINT [FK_PatientColonoscopy_PatientJourney]
        FOREIGN KEY ([PatientJourney_ID])
        REFERENCES [dbo].[PatientJourney] ([PatientJourney_ID])
)
GO