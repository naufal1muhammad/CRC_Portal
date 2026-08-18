-- =============================================================================
-- spAgentPatient_FindByPhone
--
-- WHAT IT DOES
--   Resolves an inbound WhatsApp number to ZERO, ONE OR MANY patients. Every one
--   of the three is a normal answer: dbo.PatientBasic has NOTHING UNIQUE EXCEPT
--   ITS PRIMARY KEY (CoreFlow.md 3.8) — not the NRIC, not the e-mail, and not the
--   phone number — so two patients sharing a household phone is a state the
--   schema permits and this procedure must report rather than hide. The caller
--   asks a disambiguating question on the many case; it never picks the first row.
--
-- WHO CALLS IT
--   The Agent API only (CRC.Api, built in Prompt 1) — endpoint 3, when a WhatsApp
--   message arrives and the agent needs to know who is speaking. No portal screen
--   searches on a phone number; nothing else in nucentra does.
--
-- @User_ID
--   IT DECLARES NONE, AND THAT IS DELIBERATE. A pure read: no write, therefore no
--   dbo.AuditTrails row, therefore no actor to record (CoreFlow.md 0.1). Neither
--   the ACTOR kind (@User_ID INT = NULL) nor the TARGET kind (@User_ID INT with no
--   default, which only the five spUsers_* procedures use).
--
-- 🔴 WHY THE MATCH IS ON THE LAST 9 DIGITS AND NOT ON EQUALITY
--   The two sides of the comparison never agree on their prefix. Meta delivers a
--   Malaysian mobile in international form — 60123456789 — while the portal's own
--   forms store whatever an administrator typed: 0123456789, 012-345 6789,
--   +60 12-345 6789. Equality matches none of those against each other. The last
--   nine digits are the subscriber part, identical in every one of those forms
--   once the 60 / 0 prefix is gone, so comparing the tails is the only test that
--   works without normalising the stored data — which this plan does not do,
--   because rewriting a column the portal's own screens write is a behaviour
--   change and not a read's business.
--
--   The nine is not arbitrary: a Malaysian mobile is 60 + 9 or 10 digits, and 9 is
--   the length every form shares. The cost is a theoretical collision between two
--   numbers differing only above the ninth digit; the caller resolves that the
--   same way it resolves a genuinely shared phone — by asking.
--
-- WHAT NEVER LEAVES HERE
--   The NRIC is projected as its LAST FOUR DIGITS ONLY. Identity is confirmed by
--   the patient supplying four digits and the agent comparing; the agent must
--   never be able to state more than it was told.
--
-- ON THE DIGIT-STRIP, AND WHAT IS NOT USED
--   No row generator. Not master.dbo.spt_values, not sys.all_objects. Both are
--   outside the project model, so either would add a THIRD SQL71502 warning to a
--   build whose pass condition is EXACTLY TWO (CoreFlow.md 3.7, 12 #8) — and that
--   count is a tripwire worth more than a tidier strip. The parameter is cleaned
--   with a scalar loop instead, and the column with TRANSLATE.
-- =============================================================================
CREATE PROCEDURE [dbo].[spAgentPatient_FindByPhone]
    @Phone VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- Parameter side: strip every non-digit, one character per pass. The loop runs
    -- at most once per character of a VARCHAR(100), on a single scalar, once per
    -- call — which is why a set-based strip is not worth a build warning here.
    DECLARE @Digits VARCHAR(100) = ISNULL(@Phone, '');

    WHILE PATINDEX('%[^0-9]%', @Digits) > 0
        SET @Digits = STUFF(@Digits, PATINDEX('%[^0-9]%', @Digits), 1, '');

    IF LEN(@Digits) < 9
    BEGIN
        -- Too short to be a Malaysian mobile — nobody to find.
        --
        -- THIS EARLY RETURN EMITS THE SAME COLUMNS, IN THE SAME ORDER, WITH THE
        -- SAME TYPES as the success path below, and it must keep doing so. Dapper
        -- maps by NAME, and the caller reads this result unconditionally; a
        -- differently-shaped grid on one path turns "no match" into a mapping
        -- failure that compiles, returns 200 and logs nothing.
        SELECT TOP 0
            CAST(NULL AS VARCHAR(100)) AS [Patient_ID],
            CAST(NULL AS VARCHAR(100)) AS [Patient_Name],
            CAST(NULL AS VARCHAR(100)) AS [Patient_Phone],
            CAST(NULL AS VARCHAR(4))   AS [NricLast4],
            CAST(NULL AS BIT)          AS [Patient_iFOBTStatus],
            CAST(NULL AS BIT)          AS [Patient_iFOBTResults],
            CAST(NULL AS VARCHAR(100)) AS [DischargeType_ID];
        RETURN;
    END

    -- The subscriber part of every Malaysian mobile number, whether it arrived as
    -- 60123456789 or 0123456789.
    DECLARE @Tail VARCHAR(9) = RIGHT(@Digits, 9);

    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Phone],
        CAST(RIGHT(LTRIM(RTRIM(pb.[Patient_NRIC])), 4) AS VARCHAR(4)) AS [NricLast4],
        pb.[Patient_iFOBTStatus],
        pb.[Patient_iFOBTResults],
        pb.[DischargeType_ID]   -- NULL = active. The caller decides what to do with a discharged patient
    FROM [dbo].[PatientBasic] pb
    -- Column side: TRANSLATE maps a NAMED separator set to '#', REPLACE removes it,
    -- and the last nine characters are compared.
    --
    -- 🔴 THE SET IS EXACTLY:  +  -  (  )  SPACE  .  AND NOTHING ELSE. A stored
    -- number containing any other character — a slash, an 'x' for an extension, a
    -- non-breaking space pasted out of a spreadsheet — survives the strip, fails
    -- the comparison, and the patient IS SIMPLY NOT FOUND. There is no error and
    -- no warning; the agent sees "unknown number" and asks. The audit query in
    -- AgentApiPlan.md Prompt 0 finds every such row, and it returns nothing today:
    --
    --   SELECT [Patient_ID], [Patient_Phone] FROM [dbo].[PatientBasic]
    --    WHERE REPLACE(TRANSLATE([Patient_Phone], '+-() .', '######'), '#', '')
    --          LIKE '%[^0-9]%';
    --
    -- Widening the set is a decision for whoever runs that query and finds rows —
    -- not something to do on a hunch.
    --
    -- THE EXPRESSION IS NOT SARGABLE, so this is a table scan and no index can
    -- help it. That is acceptable on a patient table of this size and against a
    -- caller whose whole traffic is one lookup per conversation. It would NOT be
    -- acceptable on a large table, and the fix then is a persisted computed column
    -- with its own index — not a cleverer predicate here.
    WHERE RIGHT(REPLACE(TRANSLATE(pb.[Patient_Phone], '+-() .', '######'), '#', ''), 9) = @Tail
    ORDER BY pb.[Patient_ID] DESC;
END;
GO
