CREATE TABLE [dbo].[Patient]
(
    [Patient_ID] VARCHAR(100) NOT NULL CONSTRAINT [PK_Patient] PRIMARY KEY,
    [Patient_Name] VARCHAR(100) NOT NULL,
    [Patient_NRIC] VARCHAR(100) NOT NULL,
    [Patient_Phone] VARCHAR(50) NOT NULL,
    [Patient_Email] VARCHAR(100) NOT NULL,
    [Branch_ID] VARCHAR(100) NOT NULL,
    [Branch_Name] VARCHAR(100) NOT NULL,
    [Patient_Stage] VARCHAR(10) NOT NULL, -- e.g. T2, T3, T4, T5
    [Patient_Remarks] VARCHAR(MAX) NULL,
    [Appointment_Date] DATETIME2 NULL
);
GO