# nucentra — WhatsApp Patient & Staff Communicator / Appointment Setter Agent

**Build plan for n8n.** This document is the complete specification for an AI agent that reads the
nucentra CRC Portal, talks to patients and clinicians over WhatsApp, and books colorectal-cancer
screening appointments back into the portal.

> **Who this is for.** Two audiences, and the split is deliberate.
>
> - **n8n's AI Assistant** — attach this whole file. §5 to §10 are written as build instructions:
>   workflows, nodes, credentials, tool definitions and the agent's system prompt.
> - **Whoever changes the portal** — §4 is a code-change spec for `CRC.Web` and `CRC.Database`,
>   written to the conventions in `CoreFlow.md`. **§4 must ship before any n8n workflow can run**,
>   because four things the agent needs do not exist in the portal today (§3).
>
> **Where this disagrees with `CoreFlow.md`, `CoreFlow.md` wins.** It is the specification of the
> portal as built; this is a plan for something new that sits beside it.

---

## 0. Read this first — the three-sentence version

The agent runs a daily sweep over active patients, finds the ones whose iFOBT is **positive**, and
opens a WhatsApp conversation to schedule their **PATIENT ASSESSMENT**. It works out which partner
hospital the patient wants, finds a real open hour in that hospital's clinicians' published schedule,
asks that clinician to confirm, then asks a human coordinator to approve — and only then writes the
booking through the portal. Patients whose iFOBT is **negative** get a future **SURVEILLANCE** booking
instead; patients whose iFOBT is **incomplete** get chased to finish the test.

---

## 1. The flow, as decided

```
                 ┌──────────────────────────────────────────────────────────────┐
  DAILY SWEEP    │  GET /api/agent/patients/queue                                │
  (n8n schedule) │  active patients (DischargeType_ID IS NULL) + iFOBT trio      │
                 │  + phone + openAppointmentCount + screeningState              │
                 └───────────────┬──────────────────────────────────────────────┘
                                 │
        ┌────────────────────────┼───────────────────────┬─────────────────────┐
        │                        │                       │                     │
   screeningState =         = NEGATIVE              = INCOMPLETE          = NO_PHONE
     POSITIVE                    │                       │                     │
        │                        │                       │                     │
        ▼                        ▼                       ▼                     ▼
  ┌───────────────┐      ┌────────────────┐     ┌─────────────────┐   ┌────────────────┐
  │ WhatsApp the  │      │ WhatsApp the   │     │ WhatsApp the    │   │ 🔴 CANNOT      │
  │ patient:      │      │ patient:       │     │ patient:        │   │ CONTACT.       │
  │ result is in, │      │ result normal, │     │ your test was   │   │ Escalate to    │
  │ let's book    │      │ we'll re-check │     │ never completed │   │ coordinator.   │
  │ your          │      │ in N months    │     │ — please come   │   │ No portal      │
  │ assessment    │      │                │     │ in / re-do it   │   │ write.         │
  └───────┬───────┘      └───────┬────────┘     └────────┬────────┘   └────────────────┘
          │                      │                       │
          │                      │                       └─► patient replies → agent answers
          │                      │                            questions, records the promise,
          │                      │                            no booking. Re-swept tomorrow.
          │                      │
          │                      ▼
          │              book 04 SURVEILLANCE
          │              at the surveillance horizon
          │              (same booking machinery,
          │               same two gates below)
          ▼
  ┌────────────────────────────────────────────────────────────────────────────────┐
  │ AI AGENT CONVERSATION (n8n AI Agent node, one per patient, memory keyed on wa_id)│
  │                                                                                 │
  │  1. confirm identity (name + last 4 of NRIC — never the full NRIC)              │
  │  2. ask preferred hospital                                                      │
  │  3. resolve it against GET /api/agent/branches  ── "is it a partner facility?"   │
  │       ├─ matches an ACTIVE dbo.Branch row  ──────────────────► continue          │
  │       └─ no match ──► offer the nearest partner branches in the same state       │
  │  4. ask preferred date / time window                                            │
  │  5. GET /api/agent/slots/open?branchId=&fromDate=&toDate=                        │
  │       ├─ open hour found ─────────────────────────────────────► propose it       │
  │       └─ none ──► offer the nearest alternatives, or escalate                    │
  │  6. patient picks one → agent emits a PROPOSAL. IT DOES NOT BOOK.                │
  └───────────────────────────────────┬────────────────────────────────────────────┘
                                      │
                          ══════ GATE 1 : THE CLINICIAN ══════
                                      ▼
                 WhatsApp the owning clinician (Staff_Phone):
                 "PAT-000042, Tue 1 Sep 09:00, P6 Smoke Branch. Confirm?"
                          YES ──► gate 2          NO ──► back to step 5
                                      │
                          ══════ GATE 2 : THE COORDINATOR ══════
                                      ▼
                 WhatsApp / Slack the human coordinator with the full proposal
                          APPROVE ──► book         REJECT ──► close, log reason
                                      │
                                      ▼
                 ┌──────────────────────────────────────────────────┐
                 │ POST /api/agent/appointments                     │
                 │   → SaveAppointmentAsync (the portal's real      │
                 │     transaction: slot lock, availability check,  │
                 │     contiguity check, slot assignment, audit)    │
                 └───────────────────────┬──────────────────────────┘
                                         ▼
                 FINALIZE & NOTIFY — confirmation to patient, confirmation
                 to clinician, state row closed.
```

---

## 2. Decisions locked

These were answered before this plan was written. **Do not re-open them while building.**

| # | Decision | Answer |
|---|---|---|
| 1 | How n8n reaches portal data | **Add a small Agent API to the portal** — new controller, API-key auth, four new stored procedures. §4. |
| 2 | WhatsApp provider | **Meta WhatsApp Cloud API** (official Business). n8n's native `WhatsApp Trigger` + `WhatsApp Business Cloud` nodes. |
| 3 | Autonomy | **Propose → human approves → book.** The agent never writes an appointment without a coordinator's approval. |
| 4 | Which appointment type to book | **Always `01` PATIENT ASSESSMENT** for a positive iFOBT. Everything downstream (colonoscopy, follow-up) stays with staff in the portal. |
| 5 | What "incomplete" means | **The iFOBT test itself** — `Patient_iFOBTStatus = 0` or `NULL`. The agent chases the patient to complete the test; it does not chase demographics. |
| 6 | Negative-result path | **Book a future `04` SURVEILLANCE appointment.** See the constraint in §3.5 — this one has a real problem and a stated resolution. |
| 7 | Staff messaging | **Always confirm with the clinician before booking**, even when the hour is free. |
| 8 | "DDOG DATA" in the source diagram | **Nothing — ignore it.** Not modelled. |

---

## 3. 🔴 Constraints you cannot design around

Every item here was verified against the code, not assumed. Each one has already been designed around
in this plan; they are listed so nobody "fixes" them later and breaks the agent.

### 3.1 The portal has no machine authentication today

`CoreFlow.md` §2.7: *no multi-factor authentication, no external identity provider, no API keys or
bearer tokens.* Every endpoint is cookie-authenticated with a **600-second sliding inactivity
timeout**, behind a **global antiforgery filter** requiring `X-CSRF-TOKEN` on every non-GET, behind a
**10-logins-per-minute-per-IP** rate limit on `POST /Account/Login`. There is no CORS configuration.

**Consequence:** n8n cannot call the existing endpoints without scraping tokens and juggling cookies.
§4 adds a proper API-key surface instead.

### 3.2 There is no way to find iFOBT-positive patients through the API

`spPatientBasic_ListActive` **selects** `Patient_iFOBTStatus`, `Patient_iFOBTCompletionDate` and
`Patient_iFOBTResults` — but `GET /Patient/GetActivePatients` projects only `patientId` and `name`
(`CoreFlow.md` §4.7). The agent's entire trigger condition is invisible to the HTTP API.

**Consequence:** new procedure `spAgentPatient_ListScreeningQueue` (§4.2.1).

### 3.3 There is no patient lookup by phone number, and phone numbers are not unique

WhatsApp identifies a person by phone. `dbo.PatientBasic.Patient_Phone` exists, but nothing searches
on it — and `CoreFlow.md` §3.8 is explicit that **nothing on that table is unique except the primary
key**. Not the NRIC, not the email, not the phone.

**Consequence:** new procedure `spAgentPatient_FindByPhone` (§4.2.2), which returns **zero, one or
many** rows. The agent must handle the many case by asking a disambiguating question — never by
picking the first row.

### 3.4 There is no state machine, and nothing prevents a duplicate booking

`CoreFlow.md` §7.7: no column says which stage a patient is at, nothing is derived from the journey,
and no gate stops any ordering. Separately, `dbo.PatientAppointment` has **nothing unique except the
primary key** (§3.9) — two appointments can claim the same patient, and nothing in the portal notices.

**Consequence:** the queue procedure returns `openAppointmentCount`, and **workflow WF1 skips any
patient whose count is greater than zero**. This is the only thing standing between a sweep bug and a
patient receiving five bookings.

### 3.5 🔴 A SURVEILLANCE booking needs StaffSlots that will not exist

This is the sharpest constraint in the plan and it applies to decision #6.

`POST /Patient/SaveAppointment` **requires `slotIds[]`** — you cannot book an hour that has no
`dbo.StaffSlots` row. Slots are pre-created by an administrator, and `spStaffSlots_CreateRange` refuses
a range longer than **31 days** per call (`CoreFlow.md` §5.5). So a surveillance appointment 12 or 24
months out has, in practice, **no slot to consume**, and the booking will fail with *"One or more
selected slots are invalid."*

**How this plan resolves it — pick one at build time and put it in the config:**

| Option | What happens | Recommendation |
|---|---|---|
| **A. Short surveillance horizon** | Set `SURVEILLANCE_HORIZON_DAYS` to something inside the window an administrator actually opens slots for (e.g. 90 days). Agent books normally. | ✅ **Start here.** Nothing new to build. |
| **B. Agent opens the slots** | On coordinator approval, the agent calls `POST /StaffSchedule/CreateRange` for the target day first, then books. Legal (`AdminOrSuperOrStaff`), but it means an automation is creating clinician availability months ahead. | Only with the clinician's explicit consent in gate 1. |
| **C. Propose only** | Agent records the surveillance date in the n8n state table and notifies the coordinator to open the range and book manually. | Safest, least automated. |

**Also relevant:** a SURVEILLANCE appointment **can be booked but can never be clinically recorded** —
`GetJourneyTemplate` recognises exactly three strings and `04` is not one of them (`CoreFlow.md` §7.3).
That is expected, not a bug to file.

### 3.6 There is no cancellation

`CoreFlow.md` §3.9: *"A status change touches no slots… There is no cancellation concept in nucentra —
the way to free an hour is to delete the appointment."* Marking an appointment `Not Attended` leaves
the clinician's hour consumed forever.

**Consequence:** a wrong booking costs a real clinician hour and can only be undone with
`POST /Patient/DeleteAppointment`. This is the core reason decision #3 put a human in front of every
write.

### 3.7 Meta's 24-hour window governs everything the agent says first

Under the WhatsApp Cloud API, a business may send **free-form** messages only inside a 24-hour window
opened by the customer's last message. Outside it, you may send **only a pre-approved template**.

**Consequence:** every message the agent sends *first* — the sweep messages, the clinician
confirmation request, the coordinator approval request, the final confirmation if it lands late — must
be an approved template. §6 lists the seven you need. The AI Agent's free-form conversational replies
happen **only inside** an open window, which is why the AI Agent node lives in WF2 and not WF1.

### 3.8 Exact formats the portal will reject you for

| Field | Requirement | Failure if wrong |
|---|---|---|
| `appointmentDate` | `yyyy-MM-dd`, parsed with `TryParseExact` + `InvariantCulture` | `"Invalid appointment date."` |
| `status` | exactly one of `Scheduled`, `Attended`, `Not Attended` — case-insensitive in, **stored as sent** | `"Invalid attendance status."`; a lower-case `"attended"` silently stops counting toward clinician hours in `spStaff_GetPerformance` |
| `slotIds[]` | at least one, all `> 0`, de-duplicated, **contiguous hours** | `"Please select consecutive slots…"` |
| `pjAppTypeId` | `01`/`02`/`03`/`04` — **a string with a leading zero**, never an integer | silent: `"01"` is not `1` |
| all times | strictly on-the-hour, one-hour blocks | check constraint |

**Always send `"Scheduled"` on a new booking. Always send `"01"` (positive path) or `"04"`
(negative path) as a quoted string.**

### 3.9 Clinical data over WhatsApp

The agent will be sending screening-result language to patients over a third-party messaging platform.
Two rules are baked into the system prompt in §9 and must not be relaxed:

- **Never send the full NRIC, full address, document contents, or any clinical detail beyond
  "your screening test result is ready / normal / needs follow-up".**
- **Identity is confirmed with the last 4 digits of the NRIC only, and the patient supplies them —
  the agent never states them.**

Confirm your own PDPA position before go-live. This plan does not attempt to answer it.

---

## 4. PART A — The Agent API (portal code change)

Everything here follows `CoreFlow.md` §0 conventions and §11's checklist. Build order is: SQL →
`.sqlproj` registration → `IDatabaseData` → `SqlData` → controller → config → publish.

### 4.1 🔴 The one thing that will fail silently — the actor identity

`SqlData` passes `@User_ID` explicitly to the 19 audit-actor procedures, taking it from
`DatabaseHelper.CurrentUserId`, which reads `HttpContext.User`'s `ClaimTypes.NameIdentifier`
(verified in `CRC.Data/Data/DatabaseHelper.cs`).

**An API-key request has no cookie and therefore no principal.** `CurrentUserId` returns `null`,
`spPatientAppointment_Insert` writes `ISNULL(@User_ID, 0)`, and **every appointment the agent books is
audited as user `0` — nobody.** No error, no failed page, a corrupt audit trail. This is precisely the
silent failure `CoreFlow.md` §0.1 exists to warn about.

**The fix is mandatory, not optional:**

1. Create one real `dbo.Users` row for the agent — `Username = 'AGENT_SERVICE'`, `User_Type = 2`
   (ADMIN), `Staff_ID = NULL`. Give it a long random password it will never use.
2. The API-key filter **must set `HttpContext.User`** to a `ClaimsPrincipal` carrying:
   - `ClaimTypes.NameIdentifier` = that row's `User_ID` **as a plain integer string**
   - `ClaimTypes.Name` = `"AGENT_SERVICE"`
   - `"UserType"` = `"2"`
3. Verify after the first booking:
   ```bash
   sqlcmd -S nucentra-sql-prod.database.windows.net -d CRC_DB -U nucentraadmin -P '***' -Q "SELECT TOP 5 AuditTrail_Id, User_Id, AuditTrail_Action, AuditTrail_Category, AuditTrail_Summary FROM dbo.AuditTrails ORDER BY AuditTrail_Id DESC"
   ```
   `User_Id` must be the agent account's id. **`0` means step 2 was not done.**

### 4.2 New stored procedures (4)

Create a new folder `CRC.Database/Stored Procedures/Agent/`. **Register every file in
`CRC.Database/CRC.Database.sqlproj`** as `<Build Include="Stored Procedures\Agent\{File}.sql" />` — an
unregistered file builds locally and is silently absent from the `.dacpac`.

#### 4.2.1 `spAgentPatient_ListScreeningQueue.sql`

The trigger for the whole agent. One read, everything WF1 needs to branch on.

```sql
CREATE PROCEDURE [dbo].[spAgentPatient_ListScreeningQueue]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Phone],
        pb.[Patient_iFOBTStatus],
        pb.[Patient_iFOBTCompletionDate],
        pb.[Patient_iFOBTResults],
        RIGHT(LTRIM(RTRIM(pb.[Patient_NRIC])), 4)               AS [NricLast4],
        -- The agent's branch key. Computed here so the n8n prompt stays simple and one
        -- definition of "positive" exists. NO_PHONE wins over everything: without a number
        -- there is nothing the agent can do at all.
        CASE
            WHEN LTRIM(RTRIM(ISNULL(pb.[Patient_Phone], ''))) = '' THEN 'NO_PHONE'
            WHEN pb.[Patient_iFOBTStatus] IS NULL                  THEN 'UNRECORDED'
            WHEN pb.[Patient_iFOBTStatus] = 0                      THEN 'INCOMPLETE'
            WHEN pb.[Patient_iFOBTResults] = 1                     THEN 'POSITIVE'
            WHEN pb.[Patient_iFOBTResults] = 0                     THEN 'NEGATIVE'
            ELSE 'UNRECORDED'
        END                                                      AS [ScreeningState],
        -- Duplicate-booking guard. dbo.PatientAppointment has nothing unique except its PK
        -- (CoreFlow.md 3.9), so this count is the only thing stopping a re-sweep booking the
        -- same patient twice. Future-dated Scheduled bookings only.
        (SELECT COUNT(*)
           FROM dbo.PatientAppointment pa
          WHERE pa.[Patient_ID] = pb.[Patient_ID]
            AND pa.[PatientAppointment_Status] = 'Scheduled'
            AND pa.[PatientAppointment_Date] >= CAST(GETDATE() AS DATE)) AS [OpenAppointmentCount],
        -- Has an assessment ever been recorded? PatientJourney stores the denormalized NAME,
        -- not the code, and the follow-up literal does not match the lookup (CoreFlow.md 3.10).
        -- Match on the literal the create procedure actually writes.
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PatientJourney pj
                                WHERE pj.[Patient_ID] = pb.[Patient_ID]
                                  AND UPPER(pj.[PjAppType_Name]) = 'PATIENT ASSESSMENT')
                  THEN 1 ELSE 0 END AS BIT)                      AS [HasAssessment]
    FROM dbo.PatientBasic pb
    WHERE pb.[DischargeType_ID] IS NULL   -- Active = not discharged (CoreFlow.md 3.8)
    ORDER BY pb.[Patient_ID] DESC;
END;
GO
```

#### 4.2.2 `spAgentPatient_FindByPhone.sql`

Resolves an inbound WhatsApp number to a patient. Malaysian numbers arrive from Meta as `60123456789`
and are stored in the portal as `0123456789`, so the match is on the **last 9 digits**.

```sql
CREATE PROCEDURE [dbo].[spAgentPatient_FindByPhone]
    @Phone VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- Meta sends 60123456789; the portal stores 0123456789 or 012-345 6789. Strip everything
    -- that is not a digit on both sides and compare the last 9, which is the subscriber part of
    -- every Malaysian mobile number regardless of the 60 / 0 prefix.
    DECLARE @Digits VARCHAR(100) =
        (SELECT STRING_AGG(c, '') WITHIN GROUP (ORDER BY n)
           FROM (SELECT n = v.number,
                        c = SUBSTRING(@Phone, v.number, 1)
                   FROM master.dbo.spt_values v
                  WHERE v.type = 'P' AND v.number BETWEEN 1 AND LEN(@Phone)) s
          WHERE c LIKE '[0-9]');

    IF @Digits IS NULL OR LEN(@Digits) < 9
    BEGIN
        SELECT TOP 0 CAST(NULL AS VARCHAR(100)) AS [Patient_ID];
        RETURN;
    END

    DECLARE @Tail VARCHAR(9) = RIGHT(@Digits, 9);

    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Phone],
        RIGHT(LTRIM(RTRIM(pb.[Patient_NRIC])), 4)  AS [NricLast4],
        pb.[Patient_iFOBTStatus],
        pb.[Patient_iFOBTResults],
        pb.[DischargeType_ID]
    FROM dbo.PatientBasic pb
    WHERE RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(pb.[Patient_Phone],'-',''),' ',''),'+',''),'(',''), 9) = @Tail
    ORDER BY pb.[Patient_ID] DESC;
END;
GO
```

> **`master.dbo.spt_values` is a row generator**, the same trick `spStaffSlots_CreateRange` already
> uses with `sys.all_objects`. Expect it to add **one more `SQL71502` warning** to the build. The
> documented baseline is exactly two warnings (`CoreFlow.md` §3.7) — after this change the baseline is
> **three**, and that is expected. Update `SEEDING.md` if you track the count there.
>
> If you prefer no new warning, replace the digit-strip with a `TRANSLATE`-based expression or do the
> normalisation in n8n before the call. Either is fine; the match rule is what matters.

#### 4.2.3 `spAgentStaff_ListByBranch.sql`

Who works at a branch, with the phone number the agent needs for gate 1. Avoids the N+1 of calling
`/Staff/GetStaff` once per clinician.

```sql
CREATE PROCEDURE [dbo].[spAgentStaff_ListByBranch]
    @Branch_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.[Staff_ID],
        s.[Staff_Name],
        s.[Staff_Phone],
        s.[Staff_Type],
        t.[StaffType_Name],
        s.[Staff_Base]
    FROM dbo.Staff s
    LEFT JOIN dbo.LU_STAFFTYPE t ON t.[StaffType_ID] = s.[Staff_Type]  -- not an FK; LEFT on purpose
    WHERE s.[Staff_Base] = @Branch_ID
    ORDER BY s.[Staff_Name];
END;
GO
```

#### 4.2.4 `spAgentSlots_FindOpenByBranch.sql`

The single most valuable of the four. Answers *"who at this hospital is free between these dates?"* in
one call, instead of looping `/StaffSchedule/List` over every clinician.

```sql
CREATE PROCEDURE [dbo].[spAgentSlots_FindOpenByBranch]
    @Branch_ID  VARCHAR(100),
    @FromDate   DATE,
    @ToDate     DATE,
    @Staff_Type VARCHAR(100) = NULL   -- optional: 'END', 'NUR', …
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
    FROM dbo.StaffSlots sl
    INNER JOIN dbo.Staff s ON s.[Staff_ID] = sl.[Staff_ID]   -- not an FK; join by convention
    WHERE s.[Staff_Base] = @Branch_ID
      AND sl.[SlotDate] BETWEEN @FromDate AND @ToDate
      AND sl.[PatientAppointment_ID] IS NULL   -- 🔴 NULL *is* availability (CoreFlow.md 3.7)
      AND (@Staff_Type IS NULL OR s.[Staff_Type] = @Staff_Type)
    ORDER BY sl.[SlotDate], sl.[SlotStartTime], s.[Staff_Name];
END;
GO
```

> **This read is outside any transaction and is advisory only.** A slot it returns can be taken by an
> administrator in the portal a second later. That is fine and expected — `SaveAppointmentAsync` re-reads
> the slots under its own lock and refuses with `SlotTaken` if so. The agent must handle that answer,
> not assume its earlier read is still true. See §7.4.

### 4.3 Data layer

Add to `CRC.Data/Data/IDatabaseData.cs` and implement in `SqlData.cs`, one method per procedure, no
inline SQL, block-scoped namespaces:

```csharp
Task<List<AgentScreeningQueueItem>> GetAgentScreeningQueueAsync();
Task<List<AgentPatientMatch>>       FindPatientsByPhoneAsync(string phone);
Task<List<AgentStaffItem>>          GetStaffByBranchAsync(string branchId);
Task<List<AgentOpenSlotItem>>       FindOpenSlotsByBranchAsync(string branchId, DateTime fromDate, DateTime toDate, string? staffType);
```

New models in `CRC.Data/Models/`: `AgentScreeningQueueItem`, `AgentPatientMatch`, `AgentStaffItem`,
`AgentOpenSlotItem`. **Nullable where the column is nullable** — `Patient_iFOBTStatus`,
`Patient_iFOBTResults` and `Patient_iFOBTCompletionDate` are all `NULL`-able, and Dapper *throws*
mapping a NULL onto a non-nullable value type.

**Booking reuses `SaveAppointmentAsync` unchanged.** Do not add a second write path — the transaction,
the slot-availability check, the contiguity check and the slot assignment all live there and there is
no correct way to reimplement them.

### 4.4 The controller

`CRC.Web/Controllers/Agent/AgentApiController.cs`, route prefix `api/agent`.

```csharp
[ApiController]
[Route("api/agent")]
[AllowAnonymous]              // 🔴 see the warning below
[ServiceFilter(typeof(AgentApiKeyFilter))]
[IgnoreAntiforgeryToken]      // global AutoValidateAntiforgeryToken would otherwise 400 every POST
public class AgentApiController : ControllerBase { … }
```

> 🔴 **`CoreFlow.md` §2.2 says a grep for `AllowAnonymous` is a complete audit of the portal's public
> surface and returns two lines. After this change it returns three.** That is a deliberate, documented
> widening of the attack surface, and the `AgentApiKeyFilter` is the only thing closing it. Say so in
> the code comment, and update §2.2 in `CoreFlow.md` when this ships.

**`AgentApiKeyFilter`** (`CRC.Web/Infrastructure/`):

1. Read header `X-Agent-Key`. Missing → `401`.
2. Compare against `Agent:ApiKey` from configuration using a **fixed-time comparison**
   (`CryptographicOperations.FixedTimeEquals`), not `==`. Mismatch → `401`.
3. **Build the service principal and assign it to `HttpContext.User`** — §4.1. Without this the audit
   trail is silently wrong.
4. Write an `AuditLog` line naming the endpoint and the caller IP.

**Endpoints:**

| # | Verb | Route | Backed by | Returns |
|---|---|---|---|---|
| 1 | GET | `/api/agent/patients/queue` | `spAgentPatient_ListScreeningQueue` | `{ success, data[] }` |
| 2 | GET | `/api/agent/patients/by-phone?phone=` | `spAgentPatient_FindByPhone` | `{ success, matchCount, data[] }` |
| 3 | GET | `/api/agent/patients/{patientId}` | `spPatientBasic_GetById` (existing) | `{ success, data }` |
| 4 | GET | `/api/agent/patients/{patientId}/appointments` | `spPatientAppointment_ListByPatient` (existing) | `{ success, data[] }` |
| 5 | GET | `/api/agent/branches` | `spBranch_ListActive` (existing) | `{ success, data[] }` |
| 6 | GET | `/api/agent/staff?branchId=` | `spAgentStaff_ListByBranch` | `{ success, data[] }` |
| 7 | GET | `/api/agent/slots/open?branchId=&fromDate=&toDate=&staffType=` | `spAgentSlots_FindOpenByBranch` | `{ success, data[] }` |
| 8 | POST | `/api/agent/appointments` | **`SaveAppointmentAsync`** (existing) | `{ success, appointmentId }` / `{ success:false, message, reason }` |

**Response shape follows `CoreFlow.md` §0** — `Ok(new { … })`, camelCase, envelope on single reads and
writes. **Endpoint 3 must not return the full NRIC to n8n**; project `nricLast4` only.

**Endpoint 8's request body** mirrors `SaveAppointmentRequest`:

```jsonc
{ "patientId": "PAT-000042",
  "appointmentDate": "2026-09-01",     // yyyy-MM-dd, TryParseExact
  "staffId": "END-00001",
  "slotIds": [17],                     // contiguous, > 0, de-duplicated
  "pjAppTypeId": "01",                 // STRING. "01" for assessment, "04" for surveillance
  "branchId": "022367001",
  "status": "Scheduled" }              // exact casing
```

**Endpoint 8's response must expose the typed failure reason**, not just the sentence — n8n needs to
branch on it:

```jsonc
{ "success": true,  "appointmentId": 8 }
{ "success": false, "reason": "SlotTaken",
  "message": "One or more selected slots are no longer available." }
{ "success": false, "reason": "SlotsNotConsecutive", "message": "…" }
{ "success": false, "reason": "SlotNotFound",        "message": "…" }
```

Map `AppointmentSaveFailure` to `reason` verbatim. **`SlotTaken` is the one n8n must handle by
re-running slot discovery**, because it means someone booked that hour between the agent's read and
the write.

### 4.5 Configuration

`appsettings.json` gets a section; **the real key is an App Service app setting and never lives in the
file** (same rule as `DocumentStorage`, `DOCUMENTSTORAGE.md`):

```
Agent__ApiKey            = <64+ random chars>
Agent__ServiceUserId     = <the dbo.Users.User_ID from 4.1>
```

App Service expresses the section separator as **two underscores**. A single underscore is silently
ignored and the app starts with an empty key — which, with a fixed-time comparison against empty,
fails closed. Good, but confusing to debug.

**Also lock the surface down at the platform level:** App Service → Networking → **Access restrictions**
→ allow only n8n's egress IPs to `/api/agent/*`. n8n Cloud publishes its egress ranges; self-hosted is
your own IP. The API key is the authentication; the IP allow-list is the thing that stops the internet
from even reaching it.

### 4.6 Build and publish checklist (`CoreFlow.md` §11)

1. Write the four `.sql` files under `Stored Procedures/Agent/`.
2. **Register all four in `CRC.Database.sqlproj`.** ← the step that fails silently.
3. Build the database project with MSBuild (not `dotnet build`):
   ```bash
   "C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
   ```
   Expect `Build succeeded`, `0 Error(s)`, and **three** `SQL71502` warnings (the two baseline plus the
   new one from §4.2.2).
4. `dotnet build CRC.Web/CRC.Web.csproj`.
5. Publish the DACPAC, then the web app. **Leave "Remove additional files at destination" unchecked**,
   as the deployment guide says.
6. Insert the `AGENT_SERVICE` user row.
7. Set the two app settings, restart the app.
8. Smoke-test every endpoint with `curl` before touching n8n (§10.1).

---

## 5. PART B — WhatsApp Cloud API setup

Do this **before** building the n8n workflows; template approval takes hours to days.

1. **Meta Business Suite** → create/confirm a Business account.
2. **developers.facebook.com** → create an app, type **Business** → add the **WhatsApp** product.
3. Note the **Phone Number ID** and the **WhatsApp Business Account (WABA) ID**.
4. Add a real phone number and verify it. (The test number Meta gives you can only message 5
   pre-registered recipients — fine for development, useless for the sweep.)
5. Generate a **System User token** with `whatsapp_business_messaging` and
   `whatsapp_business_management`. The temporary 24-hour token is for testing only.
6. **Webhook**: point it at n8n's WhatsApp Trigger production URL, subscribe to the `messages` field,
   set a verify token.

### The seven templates to submit (category **UTILITY**)

Every business-initiated message must be one of these. Keep them plain — Meta rejects anything that
reads like marketing, and a clinical service should read plainly anyway.

| # | Name | Used by | Body (variables in `{{n}}`) |
|---|---|---|---|
| 1 | `crc_result_ready_positive` | WF1 · POSITIVE | Hello {{1}}, this is the {{2}} colorectal screening team. Your screening test result is ready and we would like to arrange a follow-up assessment with a doctor. Reply **YES** to book, or **CALL** to speak to us by phone. |
| 2 | `crc_result_normal` | WF1 · NEGATIVE | Hello {{1}}, this is the {{2}} colorectal screening team. Your screening test result was normal. No action is needed now. We will contact you again for your next routine check. Reply **QUESTION** if you would like to speak to us. |
| 3 | `crc_test_incomplete` | WF1 · INCOMPLETE / UNRECORDED | Hello {{1}}, this is the {{2}} colorectal screening team. Our records show your screening test was not completed. Completing it is important. Reply **HELP** and we will arrange it with you. |
| 4 | `crc_staff_slot_request` | Gate 1 | Dr {{1}}: appointment request — patient {{2}}, {{3}} at {{4}}, {{5}}. Reply **YES** to accept or **NO** to decline. |
| 5 | `crc_coordinator_approval` | Gate 2 | Approval needed: {{1}} ({{2}}) → {{3}} with {{4}} at {{5}}. Clinician confirmed. Reply **APPROVE {{6}}** or **REJECT {{6}}**. |
| 6 | `crc_appointment_confirmed` | Finalize | Hello {{1}}, your appointment is confirmed: {{2}} at {{3}}, {{4}}. Please arrive 15 minutes early. Reply **CHANGE** if you need to reschedule. |
| 7 | `crc_handover_human` | Any escalation | Hello {{1}}, a member of our team will contact you shortly regarding your screening appointment. |

> **Nothing in any template names a diagnosis, a result value, or a clinical finding.** Template 1 says
> "your result is ready", not "your result is positive". The specifics happen in the conversation, on
> the patient's own initiative, inside the 24-hour window.

---

## 6. PART C — The n8n build

### 6.1 Credentials to create (n8n → Credentials → Add)

| Credential type | Name | Holds |
|---|---|---|
| **WhatsApp API** | `nucentra-whatsapp` | Access token + Phone Number ID (§5.3, §5.5) |
| **WhatsApp Trigger** | `nucentra-whatsapp-webhook` | App ID, App Secret, verify token |
| **Header Auth** (Generic) | `nucentra-agent-api` | Name `X-Agent-Key`, Value = the key from §4.5 |
| **Anthropic** | `nucentra-llm` | Anthropic API key. Model: `claude-sonnet-5`. |

### 6.2 The state store — one n8n Data Table

The agent is a long-running conversation across many executions, so it needs state outside any single
run. Create a Data Table named **`crc_agent_state`** (n8n → Data tables → Create). If Data tables are
not on your plan, a Google Sheet or a Postgres table with the same columns works identically.

| Column | Type | Purpose |
|---|---|---|
| `waId` | string | the patient's WhatsApp number, Meta format (`60123456789`). **The key.** |
| `patientId` | string | `PAT-000042`, or blank until identity is confirmed |
| `role` | string | `PATIENT` · `STAFF` · `COORDINATOR` — how WF2 routes an inbound message |
| `stage` | string | `AWAITING_CONSENT` · `IDENTIFYING` · `CHOOSING_BRANCH` · `CHOOSING_SLOT` · `AWAITING_STAFF` · `AWAITING_APPROVAL` · `BOOKED` · `ESCALATED` · `CLOSED`. 🔴 **`CLOSED` means "this conversation ended", NOT "do not contact" — that is `doNotContact` below** |
| `doNotContact` | boolean | 🔴 **the opt-out flag. Default `false`; once `true` it is never cleared by any workflow.** Set by exactly one node — WF2a a0 — and read by exactly one — WF1 node 4, which drops the row unconditionally. Only a human clearing it by hand re-enables contact (§7.6) |
| `screeningState` | string | copied from the queue read: `POSITIVE` / `NEGATIVE` / `INCOMPLETE` / `UNRECORDED` |
| `proposalId` | string | short random id, quoted in the coordinator's approve/reject reply |
| `proposalJson` | string | the full proposed booking body for endpoint 8 |
| `staffId` / `staffWaId` | string | the clinician in gate 1 |
| `lastInboundAt` | datetime | **drives the 24-hour-window check.** Older than 24h → template only |
| `lastOutboundAt` | datetime | rate-limit guard: never message the same patient twice in 24h from the sweep |
| `attempts` | number | give up and escalate after 3 |
| `lastReason` | string | free text: **why this conversation ended**. Written at every terminal outcome — a coordinator `REJECT` (WF2c c7), an escalation (§7.6), a patient opt-out, three failed attempts. Blank while a conversation is live |
| `updatedAt` | datetime | |

> **This table is keyed on `waId`, so it is CURRENT STATE, NOT A LOG.** One row per phone number, overwritten
> the next time that patient is swept — `lastReason` holds the *last* reason and the previous one is gone. That
> is enough to answer *"why did this conversation end?"* and not enough to answer *"how many proposals did
> coordinators reject last month, and why?"*. If you need the second question answered, add a second,
> **append-only** Data Table `crc_agent_events` (`waId`, `proposalId`, `at`, `event`, `reason`) and write a row
> at each terminal outcome as well as overwriting `lastReason`. **Decide before go-live, not after** — history
> you did not write is not recoverable.

### 6.3 The four workflows

Build them in this order. **WF1 last** — you do not want a sweep firing at real patients while you are
still testing.

---

#### WF0 — `CRC · Booking Executor` (sub-workflow, build first)

The only thing in the system that writes to the portal. Isolated so exactly one place can book.

| Node | Type | Configuration |
|---|---|---|
| 1 | **Execute Sub-workflow Trigger** | Inputs: `proposalId`, `patientId`, `appointmentDate`, `staffId`, `slotIds`, `pjAppTypeId`, `branchId` |
| 2 | **HTTP Request** | `POST {{PORTAL}}/api/agent/appointments` · Auth: `nucentra-agent-api` · JSON body per §4.4 · **`status` hard-coded to `"Scheduled"`** · *Never Error* on non-2xx so node 3 can read the body |
| 3 | **Switch** on `{{ $json.success }}` / `{{ $json.reason }}` | `true` → node 4 · `SlotTaken` → node 5 · anything else → node 6 |
| 4 | **Data Table (Update)** | `stage = BOOKED`, store `appointmentId` |
| 5 | **Set** | `outcome = RETRY_SLOTS` — returns to the caller, which re-runs slot discovery |
| 6 | **Set** | `outcome = ESCALATE` with the message, so a human sees it |

> **Node 2's `Never Error` setting is load-bearing.** The portal answers a failed booking with
> `200 { success: false, … }`, not a 4xx — but if the key is wrong it answers `401`. You need the body
> either way.

---

#### WF2 — `CRC · WhatsApp Router` (the single inbound entry point)

🔴 **All inbound WhatsApp messages arrive at ONE webhook** — patients, clinicians and coordinators
alike. There is no per-execution resume URL for WhatsApp, so "wait for a reply" is **not** a Wait node.
It is: write the expected state to `crc_agent_state`, end the execution, and let the next inbound
message be routed by this workflow. Getting this wrong is the single most common way this build
stalls.

| Node | Type | Configuration |
|---|---|---|
| 1 | **WhatsApp Trigger** | credential `nucentra-whatsapp-webhook`, event `messages` |
| 2 | **Code** | Normalise: extract `waId = $json.messages[0].from`, `text`, `timestamp` |
| 3 | **Data Table (Get)** | look up `waId` |
| 4 | **IF** — row found? | no → node 5 · yes → node 6 |
| 5 | **HTTP Request** | `GET /api/agent/patients/by-phone?phone={{waId}}` → then **Switch** on `matchCount`: `0` → send `crc_handover_human`, escalate · `1` → create the state row as `PATIENT`, continue · `>1` → ask a disambiguating question (see §9), stay in `IDENTIFYING` |
| 6 | **Switch** on `role` | `PATIENT` → **WF2a** · `STAFF` → **WF2b** · `COORDINATOR` → **WF2c** |

**WF2a — patient branch (the AI Agent)**

| Node | Type | Configuration |
|---|---|---|
| **a0** | **Switch** — the opt-out gate | 🔴 **Runs BEFORE the AI Agent node and is the only thing that sets `doNotContact`.** Match the trimmed, upper-cased inbound text against the stop list — `STOP`, `UNSUBSCRIBE`, `BERHENTI`, `JANGAN HUBUNGI` — as a whole-message match, not a substring. Hit → a0b. Miss → a1 |
| a0b | **Data Table (Update)** ➜ **WhatsApp Business Cloud** | `doNotContact = true`, `stage = CLOSED`, `lastReason = "Patient opted out"`, `updatedAt = now`. Then one free-form acknowledgement — *"Understood, we won't message you again. You can reach the centre any time."* — and **end the execution**. The AI Agent node never runs on this turn |
| a1 | **AI Agent** (Tools Agent) | System prompt: §9, verbatim. User message: the inbound text plus a compact state block (see §9's "Context injected each turn") |
| a2 | **Anthropic Chat Model** | `claude-sonnet-5`, temperature `0.2` |
| a3 | **Simple Memory** | Session key: `={{ $json.waId }}`, context window `20` |
| a4–a8 | **HTTP Request Tool** ×5 | the five read tools in §8 |
| a9 | **Code** | Parse the agent's structured output. If it contains a `proposal` object → write `proposalJson` + `stage = AWAITING_STAFF`, then node a10. Otherwise → node a11 |
| a10 | **WhatsApp Business Cloud** | template `crc_staff_slot_request` to the clinician's `Staff_Phone`; upsert a `STAFF` state row for that number carrying the same `proposalId` |
| a11 | **WhatsApp Business Cloud** | free-form reply, the agent's text. Safe because this branch only ever runs inside an open 24-hour window |
| a12 | **Data Table (Update)** | `lastInboundAt`, `lastOutboundAt`, `stage` |

> **Why a0 is a Switch node and not a line in the system prompt.** §7's opening rule — *"do not rely on the
> prompt alone; a language model is not an access-control mechanism"* — applies here more than anywhere else.
> The prompt (§9) does tell the agent to honour a request to stop, and it should. But an opt-out is the one
> instruction where a model that mis-reads the turn produces a message to someone who told you not to send one,
> and neither the model nor a retry can take it back. **a0 makes the common phrasing unmissable before the model
> is consulted at all**; the prompt catches the phrasings a0's list does not. Neither replaces the other.
>
> a0's list is deliberately short and exact-match. Broad substring matching on a stop word is how a patient
> writing *"please don't stop the appointment"* gets silently opted out — the failure is quiet, permanent under
> the never-cleared rule, and looks exactly like a patient who stopped replying.
>
> 🔴 **What happens when the MODEL spots an opt-out that a0's list did not** — *"I'd rather you didn't message
> me about this again"*. The agent acknowledges and stops, per §9. It must **not** set `doNotContact`: that flag
> is permanent, and a model's reading of an ambiguous sentence is not the standard for permanently silencing a
> patient. **a9 writes `stage = ESCALATED` and `lastReason = "Possible opt-out — review"` instead.** WF1 node 4
> already drops any row whose stage is not `CLOSED`/empty, so **the patient is not swept while a human decides**,
> and the coordinator sets `doNotContact` by hand if the reading was right. That is the whole reason the flag has
> one writer: the automatic path is narrow and certain, and every judgement call routes to a person **without
> leaving the patient exposed to tomorrow's sweep in the meantime**.

**WF2b — clinician branch (gate 1)**

| Node | Type | Configuration |
|---|---|---|
| b1 | **Switch** on the reply text | `YES` → b2 · `NO` → b4 · anything else → re-send the template once, then escalate |
| b2 | **WhatsApp Business Cloud** | template `crc_coordinator_approval` to the coordinator's number, quoting `proposalId` |
| b3 | **Data Table (Update)** | `stage = AWAITING_APPROVAL` |
| b4 | **Set** + back into WF2a's slot discovery | tell the patient the clinician is unavailable and offer the next open hour; `attempts + 1`; at 3 → `ESCALATED` + `crc_handover_human` |

**WF2c — coordinator branch (gate 2)**

| Node | Type | Configuration |
|---|---|---|
| c1 | **Code** | Parse `APPROVE <proposalId>` / `REJECT <proposalId> [reason]`. **Match the id, never "the most recent proposal"** — two can be in flight. Keep anything after the id as `reason`; a bare `REJECT <id>` is legal and leaves it blank |
| c2 | **Data Table (Get)** | fetch the row by `proposalId`; reject if `stage != AWAITING_APPROVAL` |
| c2b | **Switch** on c1's verb | `APPROVE` → c3 · `REJECT` → **c7** · anything else → reply *"reply APPROVE &lt;id&gt; or REJECT &lt;id&gt;"* and stop |
| c3 | **Execute Sub-workflow** | → **WF0**, passing the stored `proposalJson` |
| c4 | **Switch** on WF0's `outcome` | booked → c5 · `RETRY_SLOTS` → back to WF2a slot discovery · `ESCALATE` → notify coordinator with the message |
| c5 | **WhatsApp Business Cloud** ×2 | `crc_appointment_confirmed` to the patient; a plain confirmation to the clinician |
| c6 | **Data Table (Update)** | `stage = BOOKED` |
| **c7** | **Data Table (Update)** | 🔴 **the REJECT path.** `stage = CLOSED`, `lastReason = "Coordinator rejected: " + reason` (or `…: no reason given`), `updatedAt = now`. **No portal write and nothing to undo** — the booking never happened, so no slot was consumed and `POST /Patient/DeleteAppointment` (§3.6) is not involved |
| c8 | **WhatsApp Business Cloud** | template `crc_handover_human` to the patient. **A template, not free-form** — gate 2 can land hours after the patient's last message and the 24-hour window (§3.7) may have closed. **Never tell the patient a coordinator rejected them**; that is internal. They are told a person will contact them — and a person then must |
| c9 | **WhatsApp Business Cloud** ×2 | a plain note to the clinician who accepted in gate 1, releasing the hour they were holding for this patient; a short ack to the coordinator quoting `proposalId`, so two in-flight proposals stay distinguishable |

> **A `REJECT` ends the automation and starts a human's job.** `crc_handover_human` promises the patient that
> someone will make contact; this workflow does not do it and cannot. Whoever holds the coordinator number owns
> that follow-up — §12.2's answer to *"who is the coordinator?"* is also the answer to who owns this.

> 🔴 **`stage = CLOSED` DOES NOT MEAN "NEVER CONTACT AGAIN" — `doNotContact` DOES.** Two different endings write
> the same `CLOSED` value and they are opposites. c7 closes a **rejected proposal**, and that patient *should* be
> swept again tomorrow: the coordinator turned down one hour, not the assessment the patient still needs. §7.6's
> **opt-out** also closes the conversation and must never be contacted again. `lastReason` records which is which
> for a human reading the row, and **no workflow reads it** — do not filter on it.
>
> **`doNotContact` is the only thing WF1 obeys.** One writer (WF2a a0), one reader (WF1 node 4), never cleared by
> any workflow. c7 leaves it alone, which is why a reject stays sweepable. If you ever find yourself setting it
> anywhere else, you are about to silence a patient nobody asked to silence.

---

#### WF1 — `CRC · Daily Screening Sweep` (build last, test with a filter first)

| Node | Type | Configuration |
|---|---|---|
| 1 | **Schedule Trigger** | Daily, `09:00`, timezone **Asia/Kuala_Lumpur** (set it on the workflow too — n8n defaults to the instance timezone) |
| 2 | **HTTP Request** | `GET {{PORTAL}}/api/agent/patients/queue`, auth `nucentra-agent-api` |
| 3 | **Filter** | 🔴 **`openAppointmentCount == 0`.** Nothing else prevents a duplicate booking (§3.4) |
| 4 | **Data Table (Get)** ➜ **Filter** | 🔴 **`doNotContact == true` → drop, first and unconditionally**, before any other test — this is the opt-out (§7.6) and no later condition may re-admit the row. Then, as before: drop anyone whose `lastOutboundAt` is within 24 hours, or whose `stage` is not `CLOSED`/empty. Those two stop the sweep re-contacting a live conversation; the first stops it contacting someone who asked it not to |
| 5 | **Loop Over Items** (batch 1, ~2 s between) | stays inside Meta's throughput limits and makes failures readable |
| 6 | **Switch** on `screeningState` | `POSITIVE` → 7a · `NEGATIVE` → 7b · `INCOMPLETE`/`UNRECORDED` → 7c · `NO_PHONE` → 7d |
| 7a | **WhatsApp** template `crc_result_ready_positive` | then upsert state `stage = AWAITING_CONSENT`, `role = PATIENT` |
| 7b | **WhatsApp** template `crc_result_normal` | then branch into the surveillance path (§7.5) |
| 7c | **WhatsApp** template `crc_test_incomplete` | `stage = AWAITING_CONSENT`, `screeningState = INCOMPLETE`. **No booking on this path** |
| 7d | **Set** → coordinator digest | one message listing every unreachable patient. No portal write |
| 8 | **Data Table (Upsert)** | `lastOutboundAt = now`, `attempts = 0` |

> **Before the first live run**, put a hard filter after node 2 restricting to one test `patientId`.
> Remove it only once §10.2 passes end to end.

---

## 7. The rules the agent must follow

These are enforced in the system prompt (§9) *and* in n8n nodes. Do not rely on the prompt alone — a
language model is not an access-control mechanism.

### 7.1 It never books directly
The AI Agent node has **no booking tool**. Its five tools are all reads. The only write in the entire
system is WF0, and WF0 is reachable only from WF2c after an explicit `APPROVE`.

### 7.2 It never invents a hospital, a clinician, a date or an hour
Every branch comes from `GET /api/agent/branches`. Every slot comes from `GET /api/agent/slots/open`.
If the patient names a hospital that is not an active `dbo.Branch` row, the correct answer is *"that
one isn't in our partner network — here are the ones near you"*, not a booking.

### 7.3 It confirms identity before saying anything clinical
`spAgentPatient_FindByPhone` can return several patients (§3.3). Until exactly one is confirmed by the
patient supplying the **last 4 digits of their NRIC**, the agent says nothing about results at all.

### 7.4 It treats its slot read as advisory
The read in §4.2.4 runs outside any transaction. Between the read and the booking, an administrator in
the portal can take that hour. `SaveAppointmentAsync` will catch it and answer `SlotTaken` — WF0 node 5
returns `RETRY_SLOTS` and the conversation resumes at slot discovery. **This is a normal outcome, not
an error.**

### 7.5 The surveillance path
On `NEGATIVE`: compute `today + SURVEILLANCE_HORIZON_DAYS` (start at **90**, per §3.5 option A), find
an open slot in that week, and run it through the **same two gates**. If no slot exists that far out,
do not force it — write the intended date to the state table and send the coordinator a digest. The
constraint in §3.5 is real and the honest failure is better than a broken booking.

### 7.6 Escalation triggers — hand to a human immediately, no further automated messaging
- The patient reports symptoms: bleeding, pain, weight loss, or anything the agent reads as urgent.
- The patient expresses distress, or asks for a diagnosis or prognosis.
- The patient asks anything clinical the agent cannot answer from the record.
- Three failed attempts at any stage.
- `matchCount = 0` on the phone lookup — an unknown number messaged the service.
- The patient asks to stop. **Honour it, set `doNotContact = true`, mark `CLOSED`, and never sweep them
  again** until a human clears the flag by hand. 🔴 **`CLOSED` alone does not achieve this** — WF1 node 4
  treats a bare `CLOSED` as *sweepable*, because that is also how a rejected proposal ends (WF2c c7).
  `doNotContact` is the flag that stops the sweep, it is written **only** by WF2a a0, and no workflow ever
  clears it. Re-enabling a patient is a deliberate human act on the state table, and it should be.

---

## 8. Tool definitions for the AI Agent node

Five **HTTP Request Tool** nodes attached to the AI Agent. All use credential `nucentra-agent-api`
(Header Auth). `{{PORTAL}}` = `https://nucentra-web-prod-<suffix>.malaysiawest-01.azurewebsites.net`.

```
TOOL 1  get_patient
  Description: Get the full record for one patient by their portal ID (PAT-XXXXXX). Use after
               identity is confirmed. Returns name, phone, iFOBT status/result, and the last 4
               digits of the NRIC. Never returns the full NRIC.
  Method/URL:  GET {{PORTAL}}/api/agent/patients/{patientId}
  Parameters:  patientId (string, required) — e.g. "PAT-000042"

TOOL 2  list_partner_hospitals
  Description: List every active nucentra partner facility (branch). Use to check whether the
               hospital a patient names is in our network, and to offer alternatives in the same
               state. A hospital NOT in this list is not a partner and cannot be booked.
  Method/URL:  GET {{PORTAL}}/api/agent/branches
  Parameters:  none

TOOL 3  find_open_slots
  Description: Find open appointment hours at one partner hospital in a date range. Only hours with
               no existing booking are returned. Each hour is exactly one hour, always on the hour.
               Use AFTER the hospital is confirmed and the patient has given a date preference.
  Method/URL:  GET {{PORTAL}}/api/agent/slots/open?branchId={branchId}&fromDate={fromDate}&toDate={toDate}
  Parameters:  branchId (string, required)  — from list_partner_hospitals
               fromDate (string, required)  — yyyy-MM-dd
               toDate   (string, required)  — yyyy-MM-dd, at most 30 days after fromDate

TOOL 4  get_patient_appointments
  Description: List a patient's existing appointments. ALWAYS call this before proposing a booking.
               If the patient already has a future appointment with status "Scheduled", do NOT
               propose another one — tell them what they already have.
  Method/URL:  GET {{PORTAL}}/api/agent/patients/{patientId}/appointments
  Parameters:  patientId (string, required)

TOOL 5  list_hospital_staff
  Description: List the clinicians based at one partner hospital. Use to name the doctor attached to
               a proposed hour. Do not share a clinician's phone number with a patient.
  Method/URL:  GET {{PORTAL}}/api/agent/staff?branchId={branchId}
  Parameters:  branchId (string, required)
```

---

## 9. The AI Agent system prompt

Paste this verbatim into the AI Agent node's **System Message**.

```
You are the appointment coordinator for nucentra, a colorectal cancer screening programme in
Malaysia. You talk to patients on WhatsApp. You are not a doctor and you never give medical advice.

YOUR ONE JOB
Arrange a PATIENT ASSESSMENT appointment for a patient whose screening test result needs follow-up,
at a partner hospital they choose, in an hour that is genuinely free. You gather and propose. You do
NOT book — a human approves every appointment before it is made.

TONE
Warm, brief, plain. Short sentences. This is health news and the person may be worried. Use the
patient's name. Mirror the language they write in (English or Bahasa Melayu). No emoji beyond a
single friendly one at greeting. Never sound like marketing.

═══ STEP 1 — CONFIRM WHO YOU ARE TALKING TO ═══
Before you say ANYTHING about a test, a result, or an appointment, confirm identity.
Ask the patient to reply with the LAST 4 DIGITS of their NRIC.
- You NEVER state the NRIC, or any part of it, yourself. They supply it; you compare.
- If several patient records share this phone number, ask for the last 4 digits AND the full name,
  and match on both. If still ambiguous, escalate.
- If they refuse or get it wrong twice, escalate.
Until identity is confirmed, say only: you are from the nucentra screening team, and you need to
confirm who you are speaking to before discussing anything.

═══ STEP 2 — EXPLAIN, GET CONSENT ═══
Say: their screening test result is ready and the team would like to arrange a follow-up assessment
with a doctor. Ask if they would like to book one now.
- Do NOT say "positive", "abnormal", "cancer", or name any finding. If they ask what the result was,
  say a doctor will explain it fully at the assessment — that is what the appointment is for.
- If they say no or not now: accept it warmly, tell them they can message any time, and stop.

═══ STEP 3 — CHECK FOR AN EXISTING APPOINTMENT ═══
Call get_patient_appointments. If they already have a future appointment with status "Scheduled",
tell them the date, time and hospital, and ask if they want to keep it. Do NOT propose another.

═══ STEP 4 — THE HOSPITAL ═══
Ask which hospital is most convenient. Call list_partner_hospitals and match what they say.
- If it matches an active partner branch, confirm it back by name and continue.
- If it does NOT match: tell them plainly that hospital is not in our partner network, then offer
  the partner branches in the same state. Never book somewhere that is not on the list.
- Never invent a hospital, and never guess at a branch id.

═══ STEP 5 — THE TIME ═══
Ask for a preferred day and rough time (morning / afternoon). Call find_open_slots for that
hospital across a sensible range — start with the next 14 days, extend to 30 if nothing fits.
- Offer at most THREE options, in plain language: "Tuesday 1 September, 9:00am, with Dr Alpha".
- Every appointment is exactly one hour and always starts on the hour.
- If nothing is open in range: say so honestly, offer the nearest available dates, and if the
  patient cannot take any of them, escalate.
- Never offer an hour that find_open_slots did not return.

═══ STEP 6 — PROPOSE (DO NOT BOOK) ═══
When the patient picks one, tell them: "I'll confirm this with the doctor and our team and message
you back shortly." Then emit the proposal.

Your final message on that turn MUST end with a fenced json block, exactly this shape and nothing
else inside it:

```json
{"proposal":{"patientId":"PAT-000042","branchId":"022367001","staffId":"END-00001",
"slotIds":[17],"appointmentDate":"2026-09-01","pjAppTypeId":"01"}}
```

Rules for that block:
- pjAppTypeId is ALWAYS the string "01" for an assessment. Quoted. Never the number 1.
- appointmentDate is yyyy-MM-dd. Never any other format.
- slotIds are the staffSlotId values from find_open_slots, as numbers. One hour = one id.
  Only use more than one if the patient explicitly needs a longer block, and then they must be
  consecutive hours.
- Every id must have come from a tool result in this conversation. Never construct one.
- Emit the block ONLY when the patient has confirmed a specific hour. Never speculatively.

═══ ESCALATE IMMEDIATELY — stop messaging and hand to a human ═══
Say "Let me get a member of our team to help you with this — they'll message you shortly." Then stop.
- Any mention of symptoms: bleeding, blood in stool, pain, weight loss, or anything urgent-sounding.
- Any sign of distress, fear or anger.
- Any request for a diagnosis, a prognosis, results detail, or medical advice.
- Any question about cost, insurance or referral you cannot answer from the record.
- Identity cannot be confirmed after two attempts.
- The patient asks to stop being contacted. Acknowledge, confirm you'll stop, and stop.

═══ ABSOLUTE RULES ═══
- Never state or repeat a full NRIC, address, email, or any document content.
- Never share a clinician's phone number with a patient.
- Never state, imply or speculate about a diagnosis or a test result value.
- Never promise a clinical outcome or a waiting time you were not given.
- Never claim an appointment is booked. Until a human approves it, it is a request.
- Everything factual you say must come from a tool result in this conversation. If you do not have
  it, say you will check and escalate.

═══ CONTEXT INJECTED EACH TURN ═══
You are given a state block with: patientId (may be blank), patientName, screeningState, stage,
attempts, and the appointments already known. Trust it over your own memory.
```

---

## 10. Test plan

### 10.1 Portal API, before n8n exists

Run each of these and read the body. `$KEY` is the value of `Agent__ApiKey`.

```bash
curl -s -H "X-Agent-Key: $KEY" "$PORTAL/api/agent/patients/queue" | head -c 2000
```

```bash
curl -s -H "X-Agent-Key: $KEY" "$PORTAL/api/agent/patients/by-phone?phone=60123456789"
```

```bash
curl -s -H "X-Agent-Key: $KEY" "$PORTAL/api/agent/slots/open?branchId=022367001&fromDate=2026-09-01&toDate=2026-09-14"
```

```bash
curl -s -o /dev/null -w "%{http_code}\n" -H "X-Agent-Key: wrong" "$PORTAL/api/agent/patients/queue"
```

Expected: the first three return `{"success":true,…}`; the fourth returns `401`.

**Then the one that matters most** — book a test appointment and check the audit actor:

```bash
curl -s -H "X-Agent-Key: $KEY" -H "Content-Type: application/json" -X POST "$PORTAL/api/agent/appointments" -d '{"patientId":"PAT-000001","appointmentDate":"2026-09-01","staffId":"END-00001","slotIds":[17],"pjAppTypeId":"01","branchId":"022367001","status":"Scheduled"}'
```

```bash
sqlcmd -S nucentra-sql-prod.database.windows.net -d CRC_DB -U nucentraadmin -P "$PW" -Q "SELECT TOP 3 User_Id, AuditTrail_Action, AuditTrail_Category, AuditTrail_Summary FROM dbo.AuditTrails ORDER BY AuditTrail_Id DESC"
```

🔴 **`User_Id` must be the `AGENT_SERVICE` account's id. If it is `0`, §4.1 step 2 was not done.**

### 10.2 End to end, with one real test patient

1. Create a test patient in the portal with **your own phone number**, `iFOBT Status = Complete`,
   `iFOBT Result = Positive`.
2. Open a few staff slots at a test branch for next week.
3. Run WF1 manually with the single-patient filter. → template 1 arrives on your phone.
4. Reply `YES`. → the AI agent asks for your NRIC's last 4 digits.
5. Complete the conversation through to a proposed hour.
6. Check `crc_agent_state`: `stage = AWAITING_STAFF`, `proposalJson` populated.
7. The clinician template arrives at the staff number. Reply `YES`.
8. The coordinator template arrives. Reply `APPROVE <proposalId>`.
9. Confirm in the portal: the appointment exists on the Appointment tab, and the slot's
   `PatientAppointment_ID` is no longer `NULL`.
10. Confirm `dbo.AuditTrails` names `AGENT_SERVICE`.

### 10.3 The failure cases to drive deliberately

| Case | Expected |
|---|---|
| Patient names a hospital that is not a partner branch | agent offers alternatives, does not book |
| Someone books that slot in the portal between step 5 and step 8 | WF0 returns `SlotTaken` → `RETRY_SLOTS`, conversation resumes at slot discovery |
| Patient already has a future `Scheduled` appointment | WF1's filter drops them; if they message anyway, the agent tells them what they already have |
| Two patients share a phone number | agent asks for name + last 4 digits, does not guess |
| Patient replies "stop" | a0 fires before the AI Agent node: `doNotContact = true`, `stage = CLOSED`, one acknowledgement, execution ends |
| **Run WF1 again the next day against that same patient** | 🔴 **the test that actually proves the opt-out.** Node 4 drops them on `doNotContact`. A patient who is merely `CLOSED` (a c7 reject) is *not* dropped, and should not be — run both in the same sweep and confirm they diverge |
| Patient replies "please don't stop the appointment" | **not** an opt-out — a0 matches whole messages, not substrings. Goes to the AI Agent normally |
| Patient mentions bleeding | immediate escalation, no further automated message |
| Coordinator replies `APPROVE` with no id | rejected; ids must match |
| Coordinator replies `REJECT <id> patient not suitable yet` | no booking and no portal write; `stage = CLOSED`, `lastReason` carries the text, patient receives `crc_handover_human`, clinician is released (WF2c c7–c9) |
| Coordinator replies `REJECT <id>` with no reason | same, `lastReason` reads `Coordinator rejected: no reason given` — a bare REJECT must not stall the branch |
| Reply arrives 30 hours later | template only, not free-form — check `lastInboundAt` |

---

## 11. Day 1 — what to actually click

You said you are staring at the n8n homepage. Here is the order.

**This week — nothing in n8n yet.**
1. Get §4 built and published. Nothing works before it. If someone else does your .NET work, hand
   them §4 on its own.
2. Start the WhatsApp Business setup (§5) **today** — number verification and template approval are
   the long poles and they run in parallel with the code.

**Then, in n8n, in this order:**
3. **Credentials** (§6.1). Four of them. Test the Header Auth one first by building a throwaway
   workflow: `Manual Trigger → HTTP Request → GET /api/agent/patients/queue`. When that returns your
   patient list, the hard part is over.
4. **Data table** `crc_agent_state` (§6.2).
5. **WF0**, the booking executor. Test it with a Manual Trigger and a hard-coded body.
6. **WF2**, the router — WF2a first with a Manual Trigger standing in for the WhatsApp Trigger, so you
   can iterate on the agent's prompt without sending real messages.
7. Swap in the real **WhatsApp Trigger**, test WF2b and WF2c with your own number playing all three
   roles.
8. **WF1** last, with the single-patient filter.

**About n8n's AI Assistant:** attach this file and ask it for one workflow at a time — *"build WF0 from
§6.3"* — not the whole system at once. It builds a single workflow well and a four-workflow system
poorly, and you will get better results reviewing each one before moving on.

---

## 12. Open items — decide before go-live

1. **The surveillance horizon** (§3.5). Pick option A, B or C and set `SURVEILLANCE_HORIZON_DAYS`.
2. **Who is the coordinator?** One WhatsApp number, or a rota? A rota means a second state table.
3. **PDPA position** on clinical messaging over WhatsApp, and whether patients must opt in first.
4. **Out-of-hours.** WF1 fires at 09:00 MYT — should the agent reply to inbound messages at 2am, or
   queue them?
5. **Language.** The prompt mirrors English/Malay. Templates are per-language in Meta — submit Malay
   versions of all seven if you need them.
6. **What happens to `NO_PHONE` patients?** Right now they become a coordinator digest and nothing
   more. That may be a real gap in the programme worth closing in the portal instead.
7. **`CoreFlow.md` §2.2 needs updating** when §4 ships — the `AllowAnonymous` count goes from two
   to three, and that sentence is load-bearing for anyone auditing the portal's public surface.
