CREATE TABLE [dbo].[PatientJourneyAudit]
(
    [PatientJourneyAudit_ID] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PatientJourneyAudit] PRIMARY KEY,
    [PatientJourney_ID]      INT NOT NULL,
    [Audit_Action]           VARCHAR(20) NOT NULL,
    [Audit_At]               DATETIME2(0) NOT NULL CONSTRAINT [DF_PatientJourneyAudit_AuditAt] DEFAULT SYSUTCDATETIME(),
    [Staff_ID]               VARCHAR(100) NOT NULL,

    [Audit_Note]             VARCHAR(500) NULL
);
GO

CREATE INDEX [IX_PatientJourneyAudit_JourneyId_AuditAt]
ON [dbo].[PatientJourneyAudit] ([PatientJourney_ID], [Audit_At]);
GO
