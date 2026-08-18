-- =============================================================================
-- spAgentStaff_ListByBranch
--
-- WHAT IT DOES
--   Lists the staff BASED at one branch — Staff_Base = @Branch_ID — with the
--   phone number the agent needs to reach a clinician, and the staff type both as
--   its stored code and as its display name. One call instead of the N+1 of
--   reading every clinician individually.
--
-- WHO CALLS IT
--   The Agent API only (CRC.Api, built in Prompt 1) — endpoint 6. The portal's own
--   staff list is spStaff_List, which returns a different and much wider shape.
--
-- @User_ID
--   IT DECLARES NONE, AND THAT IS DELIBERATE. A pure read: no write, no
--   dbo.AuditTrails row, no actor to record (CoreFlow.md 0.1). Neither the ACTOR
--   kind (@User_ID INT = NULL) nor the TARGET kind (@User_ID INT, no default).
--
-- A NOTE ON Staff_Phone
--   It is returned because the agent needs it to ask a clinician to confirm an
--   hour. It is NOT for relaying to a patient, and this procedure is not the place
--   that stops that happening — the agent's own system prompt is. Said here so
--   nobody reads the projection as permission.
-- =============================================================================
CREATE PROCEDURE [dbo].[spAgentStaff_ListByBranch]
    @Branch_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.[Staff_ID],
        s.[Staff_Name],
        s.[Staff_Phone],
        s.[Staff_Type],       -- the LU_STAFFTYPE.StaffType_ID code: 'END', 'NUR', 'ANE', 'ENT', 'GAS'
        t.[StaffType_Name],   -- NULLABLE — see the join note below
        s.[Staff_Base]
    FROM [dbo].[Staff] s
    -- 🔴 LEFT JOIN, AND IT HAS TO BE.
    -- Staff.Staff_Type IS NOT A FOREIGN KEY. Confirmed against the DDL, not
    -- assumed: dbo/Tables/Staff.sql declares PK_Staff and nothing else, so
    -- Staff_Type is a plain VARCHAR(100) that HOLDS a StaffType_ID by convention
    -- only (CoreFlow.md 3.4 — "PK_Staff is the only constraint on the table.
    -- There are no foreign keys, in either direction"). A staff member carrying a
    -- code that has since been removed from dbo.LU_STAFFTYPE is therefore a state
    -- the schema permits, and an INNER JOIN would DROP THAT PERSON FROM THE LIST
    -- silently — the agent would be told a clinician who is standing in the branch
    -- does not work there. LEFT JOIN keeps them, with StaffType_Name NULL, which
    -- is exactly why StaffType_Name is nullable on the portal's own models too.
    LEFT JOIN [dbo].[LU_STAFFTYPE] t
        ON t.[StaffType_ID] = s.[Staff_Type]
    -- Staff_Base holds a Branch.Branch_ID, also by convention only — no foreign
    -- key here either. An unknown @Branch_ID is not an error: it returns nothing.
    WHERE s.[Staff_Base] = @Branch_ID
    ORDER BY s.[Staff_Name];
END;
GO
