CREATE TABLE [dbo].[PatientColonoscopy]
(
    [PatientColonoscopy_ID] INT IDENTITY(1,1) NOT NULL,
    [PatientJourney_ID] INT NOT NULL,
    [Patient_ID] VARCHAR(100) NOT NULL,

    [ColonoscopyStatus] BIT NOT NULL,
    [ColonoscopyStatus_Details] VARCHAR(500) NULL,

    [BowelPreparation] INT NOT NULL,

    [Findings_Anus] BIT NOT NULL,
    [Findings_AnusDetails] VARCHAR(500) NULL,

    [Findings_Rectum] BIT NOT NULL,
    [Findings_RectumDetails] VARCHAR(500) NULL,

    [Findings_SigmoidColon] BIT NOT NULL,
    [Findings_SigmoidColonDetails] VARCHAR(500) NULL,

    [Findings_DescendingColon] BIT NOT NULL,
    [Findings_DescendingColonDetails] VARCHAR(500) NULL,

    [Findings_SplenicFlexure] BIT NOT NULL,
    [Findings_SplenicFlexureDetails] VARCHAR(500) NULL,

    [Findings_TransverseColon] BIT NOT NULL,
    [Findings_TransverseColonDetails] VARCHAR(500) NULL,

    [Findings_HepaticFlexure] BIT NOT NULL,
    [Findings_HepaticFlexureDetails] VARCHAR(500) NULL,

    [Findings_AscendingColon] BIT NOT NULL,
    [Findings_AscendingColonDetails] VARCHAR(500) NULL,

    [Findings_Caecum] BIT NOT NULL,
    [Findings_CaecumDetails] VARCHAR(500) NULL,

    [HPE_Status] BIT NOT NULL,
    [HPE_Details] VARCHAR(500) NULL,

    [Complications] VARCHAR(100) NOT NULL,
    [Complications_Details] VARCHAR(500) NULL,

    [DischargePlan] VARCHAR(100) NOT NULL,

    CONSTRAINT [PK_PatientColonoscopy]
        PRIMARY KEY CLUSTERED ([PatientColonoscopy_ID] ASC),

    CONSTRAINT [FK_PatientColonoscopy_PatientJourney]
        FOREIGN KEY ([PatientJourney_ID])
        REFERENCES [dbo].[PatientJourney] ([PatientJourney_ID])
)
GO