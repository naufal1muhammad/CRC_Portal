-- =============================================================================
-- spAgentSlots_FindOpenByBranch
--
-- WHAT IT DOES
--   Answers "who at this branch is free between these two dates?" in one call,
--   optionally narrowed to one staff type. One row is ONE OPEN HOUR of one
--   clinician's published availability — dbo.StaffSlots holds exactly one
--   on-the-hour hour per row, by check constraint (CoreFlow.md 3.7), so a
--   three-hour gap comes back as three rows and it is the caller that groups them.
--
-- WHO CALLS IT
--   The Agent API only (CRC.Api, built in Prompt 1) — endpoint 7. The portal reads
--   slots through spStaffSlots_List, which is per-clinician; this one is
--   per-branch, which is the whole reason it exists.
--
-- @User_ID
--   IT DECLARES NONE, AND THAT IS DELIBERATE. A pure read: no write, no
--   dbo.AuditTrails row, no actor to record (CoreFlow.md 0.1). Neither the ACTOR
--   kind (@User_ID INT = NULL) nor the TARGET kind (@User_ID INT, no default).
--
-- 🔴 THIS READ IS ADVISORY ONLY, AND THE CALLER MUST TREAT IT THAT WAY
--   It runs OUTSIDE any transaction and holds no lock. A slot it returns can be
--   consumed by an administrator working in the portal a second later, and nothing
--   here would know: dbo.PatientAppointment has no unique constraint of any kind
--   beyond its identity, and dbo.StaffSlots has nothing unique on
--   PatientAppointment_ID, so there is no constraint anywhere that would catch a
--   double-claim (CoreFlow.md 3.9, 6.7). The ONLY defence is inside
--   SaveAppointmentAsync, which re-reads the same slots INSIDE its transaction,
--   under its own locks, and refuses with SlotTaken. So: a SlotTaken answer to a
--   booking is a NORMAL OUTCOME of a correct system, not a bug in this read and
--   not a client error — the caller re-runs slot discovery and tries again. Do not
--   "fix" that by moving this read into a transaction; it is the picker, not the
--   check, and the distinction is CoreFlow.md 6.7's central point.
--
-- WHY THE TIMES COME BACK AS STRINGS
--   SlotStartTime and SlotEndTime are TIME(0) in the table (confirmed against
--   dbo/Tables/StaffSlots.sql). They are projected as VARCHAR(5) 'HH:mm' via
--   CONVERT(..., 108), which truncates the seconds — the same projection
--   spStaffSlots_List already uses, so both slot reads in nucentra hand back the
--   identical wire shape. The reason is the consumer: a TIME maps onto a .NET
--   TimeSpan and then onto JSON as "17:00:00" or as a duration object depending on
--   the serializer, which is a shape an external caller then has to parse and
--   re-format. A five-character string is unambiguous, sorts correctly, and is
--   what an appointment payload needs to send back anyway.
--
-- A NOTE ON Staff_Phone
--   Returned for the same reason as in spAgentStaff_ListByBranch: the agent needs
--   it to confirm an hour with the clinician. It is not for relaying to a patient,
--   and the enforcement of that lives in the agent's system prompt, not here.
-- =============================================================================
CREATE PROCEDURE [dbo].[spAgentSlots_FindOpenByBranch]
    @Branch_ID  VARCHAR(100),
    @FromDate   DATE,
    @ToDate     DATE,
    @Staff_Type VARCHAR(100) = NULL   -- optional filter: 'END', 'NUR', … ; NULL = every type
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        sl.[StaffSlot_ID],
        sl.[Staff_ID],
        s.[Staff_Name],
        s.[Staff_Phone],
        s.[Staff_Type],
        sl.[SlotDate],
        CONVERT(VARCHAR(5), sl.[SlotStartTime], 108) AS [SlotStartTime],
        CONVERT(VARCHAR(5), sl.[SlotEndTime],   108) AS [SlotEndTime]
    FROM [dbo].[StaffSlots] sl
    -- Joined on Staff_ID BY CONVENTION, NOT BY FOREIGN KEY. StaffSlots.Staff_ID is
    -- a plain VARCHAR(100) holding a Staff.Staff_ID; the table's only foreign key
    -- points at dbo.PatientAppointment (CoreFlow.md 3.7). INNER is right here
    -- because a slot whose staff row no longer exists has no name, no phone and no
    -- branch — there is nothing to offer the agent and nowhere to send a patient.
    INNER JOIN [dbo].[Staff] s
        ON s.[Staff_ID] = sl.[Staff_ID]
    WHERE s.[Staff_Base] = @Branch_ID
      AND sl.[SlotDate] BETWEEN @FromDate AND @ToDate
      -- 🔴 PatientAppointment_ID IS NULL *IS* AVAILABILITY. THIS IS THE SINGLE
      -- MOST MISREADABLE LINE IN THE PROCEDURE, so read it once and remember it:
      -- dbo.StaffSlots HAS NO IsBooked COLUMN, NO STATUS AND NO "RELEASED" STATE
      -- (CoreFlow.md 3.7). A slot with a null appointment id is open; a slot with
      -- one is consumed by that appointment. Every availability decision in
      -- nucentra — the schedule grid, the booking form's slot picker, and the
      -- concurrency check inside SaveAppointmentAsync — is this one IS NULL test.
      -- Do not add a status column to make this "clearer": it would create a
      -- second answer to a question that already has one.
      AND sl.[PatientAppointment_ID] IS NULL
      AND (@Staff_Type IS NULL OR s.[Staff_Type] = @Staff_Type)
    ORDER BY sl.[SlotDate], sl.[SlotStartTime], s.[Staff_Name];
END;
GO
