-- =============================================================================
-- spAgentUsers_GetServiceAccount
--
-- WHAT IT DOES
--   Returns the ONE dbo.Users row the Agent API performs its writes as:
--   Username = 'AGENT_SERVICE', seeded by Scripts/Seed_Users.sql on every publish.
--   Four columns and no more — NOT Password_Hash and NOT User_Email. The caller
--   needs an identity to build a principal from, not a credential to check.
--
-- WHO CALLS IT
--   The Agent API's request filter only (CRC.Api, built in Prompt 1), once per
--   request, before the action runs. No portal screen calls it and no user-facing
--   page ever will.
--
-- 🔴 IT DECLARES NO @User_ID, AND IT COULD NOT HAVE ONE EVEN IF IT WANTED TO
--   It writes no dbo.AuditTrails row — it writes nothing at all — so there is no
--   actor to record (CoreFlow.md 0.1). More than that: it runs BEFORE ANY
--   PRINCIPAL EXISTS. An API-key request arrives with no cookie and therefore no
--   ClaimsPrincipal, DatabaseHelper.CurrentUserId is null, and THIS PROCEDURE IS
--   WHAT CREATES THE IDENTITY the rest of the request runs as. Asking it for an
--   actor would be asking for the answer it is being called to produce.
--
-- 🔴 THE USERNAME LITERAL IS THE CONTRACT — NOT AN ID, AND NOT A SETTING
--   dbo.Users.User_ID is INT IDENTITY, so the agent account's id on a local
--   CRC_DB and its id on Azure SQL ARE DIFFERENT NUMBERS. A configured id would be
--   wrong on one of them, and a wrong id does not throw: it writes a DIFFERENT,
--   REAL user's id into dbo.AuditTrails and reports success. So the lookup is by
--   name. dbo.Users carries UNIQUE INDEX IX_Users_Username (dbo/Tables/Users.sql),
--   which makes this an index seek and makes the answer UNAMBIGUOUS BY
--   CONSTRUCTION — there cannot be two rows to choose between. Nothing anywhere
--   in the solution stores this account's User_ID, and nothing should start.
--
-- 🔴 NO ROW IS NOT A NORMAL OUTCOME. THE CALLER MUST FAIL THE REQUEST.
--   An empty result means the database was published without the seed. The caller
--   answers 503, logs an error, and executes no action — it MUST NOT fall through
--   to a null actor. Name the failure that rule prevents, because it is silent and
--   permanent: spPatientAppointment_Insert writes ISNULL(@User_ID, 0), so every
--   appointment the agent books would be audited as AuditTrails.User_Id = 0 —
--   nobody — with no error, no failed request, and no way to reconstruct who did
--   what afterwards. A corrupt audit trail on a clinical system. This repository
--   has already shipped that bug once (AgentApiPlan.md, "The actor identity"), and
--   the two outcomes on offer are "fail loudly now" or "corrupt it quietly
--   forever".
-- =============================================================================
CREATE PROCEDURE [dbo].[spAgentUsers_GetServiceAccount]
AS
BEGIN
    SET NOCOUNT ON;

    -- TOP 1 is belt-and-braces: IX_Users_Username is UNIQUE, so a second row
    -- cannot exist. Returns an empty result set when the seed is missing.
    SELECT TOP 1
        [User_ID],
        [Username],
        [User_Name],
        [User_Type]
    FROM [dbo].[Users]
    WHERE [Username] = 'AGENT_SERVICE';
END;
GO
