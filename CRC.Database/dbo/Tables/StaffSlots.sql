CREATE TABLE [dbo].[StaffSlots]
(
    [StaffSlot_ID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Staff_ID] VARCHAR(100) NOT NULL,
    [SlotDate] DATE NOT NULL,
    [SlotStartTime] TIME(0) NOT NULL,
    [SlotEndTime] TIME(0) NOT NULL,
    [PatientAppointment_ID] INT NULL,

    CONSTRAINT [CK_StaffSlots_OnTheHour]
        CHECK (DATEPART(MINUTE, [SlotStartTime]) = 0 AND DATEPART(MINUTE, [SlotEndTime]) = 0),

    CONSTRAINT [CK_StaffSlots_OneHour]
        CHECK ([SlotEndTime] = DATEADD(HOUR, 1, [SlotStartTime])),

    CONSTRAINT [FK_StaffSlots_PatientAppointment]
        FOREIGN KEY ([PatientAppointment_ID])
        REFERENCES [dbo].[PatientAppointment]([PatientAppointment_ID])
);
GO

-- Unique slot per staff per hour
CREATE UNIQUE INDEX [UX_StaffSlots_Staff_ID_SlotDate_SlotStartTime]
    ON [dbo].[StaffSlots]([Staff_ID], [SlotDate], [SlotStartTime]);
GO

-- Nonclustered index for listing by staff + date range (covering)
CREATE NONCLUSTERED INDEX [IX_StaffSlots_Staff_ID_SlotDate_SlotStartTime]
    ON [dbo].[StaffSlots]([Staff_ID], [SlotDate], [SlotStartTime])
    INCLUDE ([SlotEndTime], [PatientAppointment_ID]);
GO