CREATE TABLE [dbo].[AuditTrails]
(
    [AuditTrail_Id]        INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AuditTrails] PRIMARY KEY,
    [AuditTrail_EventUTC]  DATETIME2(0) NOT NULL CONSTRAINT [DF_AuditTrails_EventUTC] DEFAULT SYSUTCDATETIME(),
    [User_Id]              INT NULL,

    [AuditTrail_Action]    VARCHAR(20) NOT NULL,
    [AuditTrail_Category]  VARCHAR(50) NOT NULL,
    [AuditTrail_Summary]   VARCHAR(500) NOT NULL
);
GO

CREATE INDEX [IX_AuditTrails_Category_EventUTC]
ON [dbo].[AuditTrails] ([AuditTrail_Category], [AuditTrail_EventUTC]);
GO