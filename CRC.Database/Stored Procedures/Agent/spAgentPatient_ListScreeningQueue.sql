-- =============================================================================
-- spAgentPatient_ListScreeningQueue
--
-- WHAT IT DOES
--   One read, no parameters: every ACTIVE patient (DischargeType_ID IS NULL —
--   CoreFlow.md 3.8, where NULL is the whole of the "active" definition) with the
--   three iFOBT columns, a screening state computed here rather than in the
--   caller, a duplicate-booking guard, and whether an assessment has ever been
--   recorded.
--
-- WHO CALLS IT
--   The Agent API only (CRC.Api, built in Prompt 1) — endpoint 1, driven by the
--   external n8n WhatsApp agent's daily sweep. No portal screen calls it: the
--   portal's own list is spPatientBasic_ListActive, which projects five columns
--   and none of the derived ones here. Reached from C# through IDatabaseData /
--   SqlData like every other procedure (CoreFlow.md 0, 12 #2).
--
-- @User_ID
--   IT DECLARES NONE, AND THAT IS DELIBERATE — the first question a reader asks.
--   This is a pure read: it writes no row anywhere and therefore inserts no
--   dbo.AuditTrails row, so there is no actor to record (CoreFlow.md 0.1). It is
--   neither of the two kinds: not the ACTOR kind (@User_ID INT = NULL, the 19
--   write procedures) and not the TARGET kind (@User_ID INT with no default, the
--   five spUsers_* procedures that operate ON a user row).
--
-- WHAT NEVER LEAVES HERE
--   The NRIC is projected as its LAST FOUR DIGITS ONLY. The agent confirms
--   identity by asking the patient for four digits and comparing; it must never
--   be able to state more, so the full column is not selected at all.
-- =============================================================================
CREATE PROCEDURE [dbo].[spAgentPatient_ListScreeningQueue]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Phone],
        pb.[Patient_iFOBTStatus],           -- BIT NULL: NULL = not recorded, 1 = complete, 0 = not complete
        pb.[Patient_iFOBTCompletionDate],   -- DATE NULL
        pb.[Patient_iFOBTResults],          -- BIT NULL: 1 = positive, which is what opens a journey
        CAST(RIGHT(LTRIM(RTRIM(pb.[Patient_NRIC])), 4) AS VARCHAR(4)) AS [NricLast4],

        -- The agent's branch key. Computed here so one definition of "positive"
        -- exists and the n8n prompt stays simple.
        --
        -- NO_PHONE IS TESTED FIRST ON PURPOSE, ahead of every clinical branch:
        -- without a number there is nothing the agent can do with this patient at
        -- all, whatever their result says. Ordering it below POSITIVE would put a
        -- patient into the "message them" bucket that no message can reach, and
        -- the sweep would report work it never did.
        --
        -- Patient_Phone is VARCHAR(100) NOT NULL (see dbo/Tables/PatientBasic.sql),
        -- so the case this catches is an EMPTY STRING, not a NULL — NOT NULL
        -- rejects a NULL and accepts '' (CoreFlow.md 3.8). The ISNULL stays as a
        -- belt-and-braces guard; do not go looking for the null case, there is not
        -- one in the schema today.
        --
        -- SIX LITERALS, AND THE ELSE IS REACHABLE IN EXACTLY ONE STATE. The proof,
        -- because the ELSE reads like a catch-all and is not one — walk what is
        -- left after each branch has taken its rows:
        --     phone blank                  -> NO_PHONE    (caught, first)
        --     Patient_iFOBTStatus IS NULL  -> UNRECORDED  (caught)
        --     Patient_iFOBTStatus = 0      -> INCOMPLETE  (caught)
        --   so anything still falling through has status = 1.
        --     Patient_iFOBTResults = 1     -> POSITIVE    (caught)
        --     Patient_iFOBTResults = 0     -> NEGATIVE    (caught)
        --   and a NULL result satisfies NEITHER of those two: a comparison with
        --   NULL is UNKNOWN, not false, and CASE takes only branches that are TRUE.
        -- Therefore the ELSE fires if and only if
        --     Patient_iFOBTStatus = 1 AND Patient_iFOBTResults IS NULL
        -- — the sample is done and the lab result is not in yet. It has its own
        -- label, RESULT_PENDING, and is no longer indistinguishable from
        -- UNRECORDED, which means "nothing was recorded at all". Both states mean
        -- "do not message this patient about a result", but they mean different
        -- things to a human: chase the lab, versus chase the patient. n8n's WF1
        -- switches on this value (CoreFlow.md 13.1 finding 4, 13.4).
        CASE
            WHEN LTRIM(RTRIM(ISNULL(pb.[Patient_Phone], ''))) = '' THEN 'NO_PHONE'
            WHEN pb.[Patient_iFOBTStatus] IS NULL                  THEN 'UNRECORDED'
            WHEN pb.[Patient_iFOBTStatus] = 0                      THEN 'INCOMPLETE'
            WHEN pb.[Patient_iFOBTResults] = 1                     THEN 'POSITIVE'
            WHEN pb.[Patient_iFOBTResults] = 0                     THEN 'NEGATIVE'
            ELSE 'RESULT_PENDING'                                   -- status = 1 with a NULL result
        END AS [ScreeningState],

        -- THE DUPLICATE-BOOKING GUARD, AND IT IS THE ONLY ONE THERE IS.
        -- dbo.PatientAppointment has NOTHING UNIQUE EXCEPT ITS PRIMARY KEY
        -- (CoreFlow.md 3.9) — two appointments may claim the same patient, the
        -- same clinician and the same hour, and nothing in the schema or in any
        -- procedure notices. So this count is the single thing standing between a
        -- re-run of the daily sweep and a patient receiving a second booking; the
        -- agent's workflow skips any patient whose count is greater than zero.
        -- Future-dated 'Scheduled' bookings only: a past appointment, or one
        -- marked 'Attended' / 'Not Attended', does not block a new one.
        (
            SELECT COUNT(*)
            FROM [dbo].[PatientAppointment] pa
            WHERE pa.[Patient_ID] = pb.[Patient_ID]
              AND pa.[PatientAppointment_Status] = 'Scheduled'
              AND pa.[PatientAppointment_Date] >= CAST(GETDATE() AS DATE)
        ) AS [OpenAppointmentCount],

        -- Has a PATIENT ASSESSMENT ever been recorded for this patient?
        --
        -- THE MATCH IS ON THE DENORMALIZED NAME BECAUSE THERE IS NOTHING ELSE TO
        -- MATCH ON. dbo.PatientJourney stores PjAppType_Name — the name written as
        -- a STRING LITERAL by each create procedure — and does not store the
        -- LU_PJ_APP_TYPE code at all (CoreFlow.md 3.10). There is no foreign key,
        -- no discriminator column and no join that resolves the two vocabularies:
        -- LU_PJ_APP_TYPE.PjAppType_ID is used by PatientAppointment.PjAppType_ID,
        -- a different column on a different table. The literal below is the one
        -- spPatientAssessment_CreateWithJourney actually writes, which is the only
        -- thing that makes this test true or false.
        --
        -- (For the record, the lookup and the literals disagree on a third type:
        -- spPatientFollowUp_CreateWithJourney writes 'PATIENT FOLLOW UP' while the
        -- lookup holds '03 FOLLOW UP'. Nothing detects that and nothing ever will.
        -- It does not affect this procedure — ASSESSMENT is one of the two that
        -- agree — but it is why matching on the name is correct rather than lazy.)
        CAST(
            CASE WHEN EXISTS (
                SELECT 1
                FROM [dbo].[PatientJourney] pj
                WHERE pj.[Patient_ID] = pb.[Patient_ID]
                  AND UPPER(pj.[PjAppType_Name]) = 'PATIENT ASSESSMENT'
            ) THEN 1 ELSE 0 END
        AS BIT) AS [HasAssessment]

    FROM [dbo].[PatientBasic] pb
    WHERE pb.[DischargeType_ID] IS NULL   -- Active = not discharged (CoreFlow.md 3.8)
    ORDER BY pb.[Patient_ID] DESC;
END;
GO
