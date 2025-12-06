CREATE TABLE [dbo].[PatientAppointment]
(
    [PatientAppointment_ID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    [Patient_ID]            VARCHAR(100) NOT NULL,
    [Patient_Name]          VARCHAR(100) NOT NULL,
    [Patient_Email]         VARCHAR(100) NOT NULL,
    [Patient_Phone]         VARCHAR(100) NOT NULL,

    [PjAppType_Name]        VARCHAR(100) NOT NULL,
    [PatientAppointment_Date]   DATETIME    NOT NULL,
    [PatientAppointment_Status] VARCHAR(100) NOT NULL
);
GO

CREATE INDEX IX_PatientAppointment_Patient_ID
    ON [dbo].[PatientAppointment]([Patient_ID]);
GO