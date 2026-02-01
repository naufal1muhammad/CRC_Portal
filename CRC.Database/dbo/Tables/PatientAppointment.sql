CREATE TABLE [dbo].[PatientAppointment]
(
    [PatientAppointment_ID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Patient_ID] VARCHAR(100) NOT NULL,
    [PatientAppointment_Date] DATE NOT NULL,
    [Staff_ID] VARCHAR(100) NOT NULL,
    [PatientAppointment_StartTime] TIME(0) NOT NULL,
    [PatientAppointment_EndTime] TIME(0) NOT NULL,
    [PjAppType_ID] VARCHAR(100) NOT NULL,
    [Branch_ID] VARCHAR(100) NOT NULL,
    [PatientAppointment_Status] VARCHAR(100) NOT NULL,

    CONSTRAINT [CK_PatientAppointment_OnTheHour]
        CHECK (DATEPART(MINUTE, [PatientAppointment_StartTime]) = 0
           AND DATEPART(MINUTE, [PatientAppointment_EndTime]) = 0),

    CONSTRAINT [CK_PatientAppointment_TimeOrder]
        CHECK ([PatientAppointment_EndTime] > [PatientAppointment_StartTime])
);
GO

CREATE INDEX IX_PatientAppointment_Patient_ID
    ON [dbo].[PatientAppointment]([Patient_ID]);
GO

CREATE INDEX IX_PatientAppointment_Staff_ID_PatientAppointment_Date
    ON [dbo].[PatientAppointment]([Staff_ID], [PatientAppointment_Date]);
GO