# nucentra — WhatsApp Patient & Staff Communicator / Appointment Setter Agent

**Build plan for n8n.** This document is the complete specification for an AI agent that reads the
nucentra CRC Portal, talks to patients and clinicians over WhatsApp, and books colorectal-cancer
screening appointments back into the portal.

> ## ✅ THE PORTAL SIDE IS FINISHED — THERE IS NO CODE FOR YOU TO WRITE
>
> **Every change the portal needed has been made, built, published and smoke-tested.** The Agent API is
> live: eight endpoints under `/api/agent`, authenticated by an `X-Agent-Key` header, five new stored
> procedures, a service account that makes the audit trail name the agent, and a PowerShell smoke test
> that drives all eight endpoints plus both negative tests. **§4 is no longer a change request — it is
> the working contract of an API that answers today.**
>
> **Everything still to be built lives in n8n and in Meta.** That is §5 onwards.
>
> **Who this is for.** Attach this whole file to n8n's AI Assistant. §4 is the API your workflows call,
> §5 is the WhatsApp Cloud API setup you do by hand in a browser, and §6 to §10 are the build
> instructions: workflows, nodes, credentials, tool definitions and the agent's system prompt.
>
> **Where this disagrees with `CoreFlow.md`, `CoreFlow.md` wins.** It is the specification of the portal
> as built, and its **§13 is the Agent API's full specification** — §4 below is the working summary an
> n8n builder needs, drawn from it.
>
> ---
>
> ## 🔴 §12 IS AN OPEN QUESTIONNAIRE. READ IT BEFORE YOU BUILD ANYTHING.
>
> **§1 to §11 are decided and safe to build from. §12 is not** — it holds **27 decisions the owner has
> not yet taken**, and several of them change nodes in §6.3, the prompt in §9 and the state table in
> §6.2. Four of them (§12 Q1–Q4) describe things that **will break the build on day one** if you follow
> §6.3 literally: phone-number format, the coordinator's identity, gate 1's missing proposal id, and a
> shared household phone.
>
> 🔴 **If a §12 item is still open when you reach the part of the build it governs, STOP AND ASK. Do not
> guess, and do not fill the gap with something plausible** — every one of them was found by reading the
> flow end to end, and a plausible guess is how they got missed the first time. When an item is answered
> it is written into §1–§11 and struck through in §12; a struck-through item is decided and the section
> that owns it is authoritative.

---

## 0. Read this first — the three-sentence version

The agent runs a daily sweep over active patients, finds the ones whose iFOBT is **positive**, and
opens a WhatsApp conversation to schedule their **PATIENT ASSESSMENT**. It works out which partner
hospital the patient wants, finds a real open hour in that hospital's clinicians' published schedule,
asks that clinician to confirm, then asks a human coordinator to approve — and only then writes the
booking through the portal. Patients whose iFOBT is **negative** are told so and handed to a
coordinator for their future surveillance visit — **the agent never books one** (§3.5); patients whose
iFOBT is **incomplete** get chased to finish the test.

**The portal side of all this is already built and answering** — see the box above. What is left is
§5 (WhatsApp) and §6 (n8n).

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
          │              🔴 NO BOOKING. Write the intended
          │              surveillance date to the state table
          │              and put the patient on the coordinator
          │              digest — a human opens the range and
          │              books it. The API refuses "04" (§3.5).
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
| 1 | How n8n reaches portal data | ✅ **Done — the Agent API exists.** `CRC.Api`, API-key auth on an `X-Agent-Key` header, five new stored procedures, eight endpoints. Built and smoke-tested. §4 is its contract. |
| 2 | WhatsApp provider | **Meta WhatsApp Cloud API** (official Business). n8n's native `WhatsApp Trigger` + `WhatsApp Business Cloud` nodes. |
| 3 | Autonomy | **Propose → human approves → book.** The agent never writes an appointment without a coordinator's approval. |
| 4 | Which appointment type to book | **Always `01` PATIENT ASSESSMENT** for a positive iFOBT. Everything downstream (colonoscopy, follow-up) stays with staff in the portal. |
| 5 | What "incomplete" means | **The iFOBT test itself** — `Patient_iFOBTStatus = 0` or `NULL`. The agent chases the patient to complete the test; it does not chase demographics. |
| 6 | Negative-result path | 🔴 **PROPOSE ONLY — the agent never books a `04` SURVEILLANCE appointment.** §3.5 **option C**, settled when the API shipped: `POST /api/agent/appointments` accepts `pjAppTypeId` `"01"` and **refuses `"04"` by name**. On a NEGATIVE result the agent sends the reassurance template and writes the intended surveillance date to a coordinator digest; a human opens the slot range and books it in the portal. §3.5, §7.5. |
| 7 | Staff messaging | **Always confirm with the clinician before booking**, even when the hour is free. |
| 8 | "DDOG DATA" in the source diagram | **Nothing — ignore it.** Not modelled. |

---

## 3. 🔴 Constraints you cannot design around

Every item here was verified against the code, not assumed. **The first three were solved by the Agent
API and are recorded as solved** — they are kept because they explain *why* the API looks the way it
does, and because nobody should "simplify" a workflow by going around it. The rest are live constraints
that no amount of portal code can remove.

### 3.1 ✅ SOLVED — the portal now has machine authentication, and it is the ONLY door

`CoreFlow.md` §2.7: the portal's **own** pages and endpoints have *no multi-factor authentication, no
external identity provider, no bearer tokens.* Every one of them is cookie-authenticated with a
**600-second sliding inactivity timeout**, behind a **global antiforgery filter** requiring
`X-CSRF-TOKEN` on every non-GET, behind a **10-logins-per-minute-per-IP** rate limit on
`POST /Account/Login`. There is no CORS configuration. **None of that changed.**

**What changed is that one controller was added beside them.** `/api/agent/*` authenticates on an
`X-Agent-Key` header, needs no cookie and no CSRF token, and is the only surface in nucentra that
works that way (§4).

🔴 **n8n calls `/api/agent/*` and nothing else.** Do not point a workflow at `/Patient/…`,
`/StaffSchedule/…` or any other portal route: they will answer a redirect to the login page or a `400`,
and making them work would mean scraping tokens and juggling cookies. If the agent needs something the
eight endpoints do not return, that is a portal change request — not an n8n workaround.

### 3.2 ✅ SOLVED — iFOBT-positive patients are now findable in one call

`spPatientBasic_ListActive` **selects** `Patient_iFOBTStatus`, `Patient_iFOBTCompletionDate` and
`Patient_iFOBTResults` — but `GET /Patient/GetActivePatients` projects only `patientId` and `name`
(`CoreFlow.md` §4.7), so the agent's entire trigger condition was invisible over HTTP.

**Solved by `GET /api/agent/patients/queue`** (§4.3, endpoint 1), which returns one row per active
patient with a pre-computed `screeningState` — so WF1 branches on a string instead of re-deriving
"positive" in an n8n expression.

### 3.3 ✅ SOLVED — but a phone number still does not identify one person

WhatsApp identifies a person by phone. `CoreFlow.md` §3.8 is explicit that **nothing on
`dbo.PatientBasic` is unique except the primary key** — not the NRIC, not the email, not the phone.

**Solved by `GET /api/agent/patients/by-phone?phone=`** (§4.3, endpoint 2), which matches on the **last
nine digits** so Meta's `60123456789` finds a stored `012-345 6789`.

🔴 **The lookup being solved does not make the answer unique, and this is the half that stays your
problem.** It returns **zero, one or many** rows and all three are normal — a shared household phone,
and `01X` / `011` mobiles whose last nine digits collide. The response carries `matchCount` so the
branch is impossible to overlook. **The agent must ask a disambiguating question when
`matchCount > 1`, and must never pick the first row.**

### 3.4 There is no state machine, and nothing prevents a duplicate booking

`CoreFlow.md` §7.7: no column says which stage a patient is at, nothing is derived from the journey,
and no gate stops any ordering. Separately, `dbo.PatientAppointment` has **nothing unique except the
primary key** (§3.9) — two appointments can claim the same patient, and nothing in the portal notices.

**Consequence:** the queue read returns `openAppointmentCount`, and **workflow WF1 skips any patient
whose count is greater than zero**. This is the only thing standing between a sweep bug and a patient
receiving five bookings.

### 3.5 🔴 SURVEILLANCE IS NOT AUTOMATED — the API refuses `"04"`, and this is settled

This was the sharpest open question in the plan. **It has been answered, in code: option C below.**

The reasoning stands. `POST /api/agent/appointments` **requires `slotIds[]`** — you cannot book an hour
that has no `dbo.StaffSlots` row. Slots are pre-created by an administrator, and
`spStaffSlots_CreateRange` refuses a range longer than **31 days** per call (`CoreFlow.md` §5.5). So a
surveillance appointment 12 or 24 months out has, in practice, **no slot to consume**. And a `04`
appointment **can never be clinically recorded even if it were booked** — `GetJourneyTemplate`
recognises exactly three strings and `04` is not one of them (`CoreFlow.md` §7.3), so it would consume
a real clinician hour to create a record nobody can complete.

| Option | What it would have meant | Status |
|---|---|---|
| **A. Short surveillance horizon** | Set `SURVEILLANCE_HORIZON_DAYS` inside the window an administrator actually opens slots for (e.g. 90 days) and let the agent book normally. | ❌ Not chosen. Reversible later — it is one constant in the portal, `AgentApiController.AllowedAppointmentTypeId`, plus a widened refusal message. |
| **B. Agent opens the slots** | The agent creates clinician availability months ahead, then books. | ❌ Not chosen, and there is **no endpoint for it** — the API exposes no slot creation at all. |
| **C. Propose only** | The agent records the surveillance date and notifies the coordinator, who opens the range and books by hand. | ✅ **THIS IS WHAT SHIPPED.** |

🔴 **What that means for your n8n build.** `pjAppTypeId` is `"01"` on every booking the agent ever
makes. Sending `"04"` is refused before the database is touched, with
`{"success": false, "message": "pjAppTypeId must be the string \"01\" …"}` — so **do not build a
surveillance booking branch**. WF1's NEGATIVE path sends the reassurance template and writes a
coordinator digest (§7.5). Nothing more.

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

### 3.8 Exact formats the API will refuse you for

**The Agent API is deliberately STRICTER than the portal's own screens**, and it refuses rather than
guesses — because an n8n expression can produce a malformed value in ways a human clicking a form
cannot, and a booking consumes a clinician hour that nothing in this system can give back (§3.6).

| Field | Requirement | If wrong |
|---|---|---|
| `appointmentDate` | `yyyy-MM-dd`, parsed with `TryParseExact` + `InvariantCulture` | refused before any data call. 🔴 `01/09/2026` is **not** accepted — it would read as 1 September on one server and 9 January on another |
| `status` | 🔴 **exactly `"Scheduled"`, case-sensitive.** Not `"scheduled"`, not `"SCHEDULED"` | refused. The portal's own screens are case-insensitive here; this endpoint is not, on purpose — a stored `"attended"` silently stops counting toward clinician hours in `spStaff_GetPerformance` |
| `slotIds[]` | at least one, **every id `> 0`**, **contiguous hours** | a non-positive id is **refused**, where the portal silently drops it. A request that means two hours must never quietly become one. *Duplicates are collapsed for you* — `[17,17]` is read as `[17]`, because that asks for the identical set of hours; only a non-positive id is a refusal |
| `pjAppTypeId` | 🔴 **exactly the string `"01"`** — with the leading zero, quoted, never the integer `1` | refused by name. `"04"` is refused too — §3.5 |
| all times | strictly on-the-hour, one-hour blocks — one `slotId` **is** one hour | enforced by the slot rows themselves; you never send a time |

**Always send `"Scheduled"` and `"01"`, both as quoted strings.** They are the only values this
endpoint accepts.

### 3.9 Clinical data over WhatsApp

The agent will be sending screening-result language to patients over a third-party messaging platform.
Two rules are baked into the system prompt in §9 and must not be relaxed:

- **Never send the full NRIC, full address, document contents, or any clinical detail beyond
  "your screening test result is ready / normal / needs follow-up".**
- **Identity is confirmed with the last 4 digits of the NRIC only, and the patient supplies them —
  the agent never states them.**

Confirm your own PDPA position before go-live. This plan does not attempt to answer it.

---

## 4. PART A — The Agent API ✅ **BUILT. PUBLISHED. SMOKE-TESTED. NOTHING TO DO HERE.**

> 🔴 **READ THIS BEFORE YOU READ ANYTHING ELSE IN THIS SECTION.**
>
> **This section used to be a change request. It is not one any more.** Every stored procedure, every
> data-layer method, the controller, the API-key filter, the service account and the configuration
> **exist in the portal today**. The API answers over HTTP right now. There is **no .NET work, no SQL
> work and no portal work left to do before the n8n build can start.**
>
> What follows is the **contract**: the URLs, the headers, the exact JSON that comes back, and the
> rules the API enforces. Build your HTTP Request nodes from it.
>
> `CoreFlow.md` **§13** is the full specification of everything below, written as built. If a detail is
> missing here, it is there.

### 4.1 What exists, in one table

| Piece | Where | Status |
|---|---|---|
| **5 stored procedures** | `CRC.Database/Stored Procedures/Agent/` — `spAgentPatient_ListScreeningQueue`, `spAgentPatient_FindByPhone`, `spAgentStaff_ListByBranch`, `spAgentSlots_FindOpenByBranch`, `spAgentUsers_GetServiceAccount` | ✅ written, registered in the `.sqlproj`, published to `CRC_DB` |
| **5 data-layer methods + 5 models** | `CRC.Data` — `IDatabaseData` / `SqlData`, `CRC.Data/Models/` | ✅ done, no inline SQL anywhere |
| **`CRC.Api`** | a class library loaded into `CRC.Web` as an MVC application part — **one host, one deployment, one config file, one log pipeline.** There is no second App Service and no second port | ✅ done |
| **`AgentApiController`** | `CRC.Api/Controllers/` — **8 endpoints**, seven reads and one write | ✅ done |
| **`AgentApiKeyFilter`** | `CRC.Api/Infrastructure/` — validates `X-Agent-Key` in fixed time, then builds the service principal | ✅ done, both negative tests pass |
| **The `AGENT_SERVICE` account** | a real seeded `dbo.Users` row, resolved **by username** on every request | ✅ done and **asserted** — see 4.6 |
| **`Test-AgentApi.ps1`** | repo root — drives all eight endpoints plus both 401 tests, **twelve checks** | ✅ passing |

**The booking write reuses the portal's own transaction whole and unchanged** — the slot lock, the
availability check, the contiguity check, the slot assignment and the audit row all live inside
`SaveAppointmentAsync`, exactly as they do when a human books through the portal. There is **no second
write path**, which is why an appointment the agent makes is indistinguishable from one an
administrator makes, except in who the audit trail names.

### 4.2 How to call it

| | |
|---|---|
| **Base URL** | `{{PORTAL}}` — the App Service URL, e.g. `https://nucentra-web-prod-<suffix>.malaysiawest-01.azurewebsites.net` |
| **Route prefix** | `/api/agent` |
| **Authentication** | one header: `X-Agent-Key: <the shared secret>`. **That is the whole of it** |
| **No cookie, no login, no session** | this surface never sees one |
| **No `X-CSRF-TOKEN`** | the antiforgery filter is switched off for this controller. Do not send one; you could not obtain one |
| **Content type on the POST** | `application/json` |
| **Dates on the wire** | **always `yyyy-MM-dd`**, in and out. Never a locale format, never a `DateTime` with a time part |
| **Times on the wire** | **`HH:mm`**, 24-hour, as strings (`"09:00"`) |
| **Property names** | camelCase, and they are a **published contract** — the portal will not rename one without telling you |

#### 🔴 The two response shapes, and the one that trips every n8n build

```jsonc
{ "success": true,  "data": [ … ] }                       // or "data": { … }, or "appointmentId": 21
{ "success": false, "message": "…", "correlationId": "…" } // something went wrong
```

🔴 **A FAILURE COMES BACK AS HTTP `200` WITH `success: false` — NOT AS A `4xx`.** The only status codes
that are not `200` are:

| Code | Meaning | What you do |
|---|---|---|
| **401** | bad or missing `X-Agent-Key`, **or the portal's key setting is empty**. The body is always `{"success":false,"message":"Unauthorized."}` and never says which | check the credential in n8n; if it is right, the portal's `Agent__ApiKey` app setting is the problem (4.7) |
| **503** | the `AGENT_SERVICE` row is missing from the database — the portal was published without its seed. The body is `{"success":false,"message":"The service is temporarily unavailable."}` | tell whoever deploys the portal. Nothing you can fix in n8n |
| **400** | your JSON body did not parse at all, or `slotIds` was not an array of numbers. 🔴 **This is the ONE response with no `success` property** — it is ASP.NET's `ProblemDetails` (`{"type":…,"title":"One or more validation errors occurred.","status":400,"errors":{…}}`), produced before the controller runs | a malformed n8n expression. `$json.success` is *undefined*, not `false`, so a Switch testing `success === false` will not catch it — test `success !== true` |

**So every HTTP Request node that calls this API must be set to `Never Error` / "Continue on error"**,
and must branch on `$json.success` — not on the status code. A node left on its default will treat a
perfectly normal `SlotTaken` refusal as a workflow crash, and will treat a `401` as no output at all.

`correlationId` appears on failures. **Quote it when you report a problem** — it is the one string that
ties your failed call to a line in the portal's own logs.

### 4.3 The eight endpoints, with the exact JSON they return

Every payload below is a **real response copied from the wire**, not an illustration.

---

**1 · `GET /api/agent/patients/queue`** — the daily sweep. No parameters.

```jsonc
{ "success": true,
  "data": [
    { "patientId": "PAT-000011", "name": "HUSSEIN AKMAL", "phone": "0166542542",
      "nricLast4": "5805", "screeningState": "POSITIVE",
      "iFobtStatus": true, "iFobtResult": true, "iFobtCompletionDate": "2026-08-06",
      "openAppointmentCount": 1, "hasAssessment": false },
    { "patientId": "PAT-000010", "name": "P9 PATIENT JULIET", "phone": "0199000010",
      "nricLast4": "5900", "screeningState": "UNRECORDED",
      "iFobtStatus": null, "iFobtResult": null, "iFobtCompletionDate": null,
      "openAppointmentCount": 0, "hasAssessment": false }
  ] }
```

Only **active** patients appear — a discharged patient is not in this list at all.

🔴 **`screeningState` is the branch and `openAppointmentCount` is the guard.** `screeningState` is one
of `NO_PHONE` · `UNRECORDED` · `INCOMPLETE` · `POSITIVE` · `NEGATIVE`, computed in the database so one
definition of "positive" exists and your n8n expression does not have to invent a second one.
`openAppointmentCount` counts **future `Scheduled`** appointments and is **the only duplicate-booking
guard in the whole system** — WF1 drops every row where it is not zero (§3.4).

`iFobtStatus` and `iFobtResult` are **nullable on purpose**: `null` means "never recorded", which is a
different fact from `false`. Do not coerce them.

---

**2 · `GET /api/agent/patients/by-phone?phone=60166542542`** — resolve an inbound WhatsApp number.

```jsonc
{ "success": true, "matchCount": 1,
  "data": [
    { "patientId": "PAT-000011", "name": "HUSSEIN AKMAL", "phone": "0166542542",
      "nricLast4": "5805", "iFobtStatus": true, "iFobtResult": true,
      "dischargeTypeId": null, "isActive": true }
  ] }
```

Send the number in **any format** — `60166542542`, `+60 16-654 2542`, `0166542542` all work. The match
is on the last nine digits.

🔴 **Branch on `matchCount`, and treat all three cases as normal.** `0` is a **successful** response
(an unknown number messaged you — escalate). `1` continues. `>1` means the agent must ask a
disambiguating question and **must never take the first row** (§3.3).

A missing or blank `phone` is refused before the database is touched:
`{"success": false, "message": "A phone number is required. Supply it as ?phone= …", "correlationId": "…"}`

---

**3 · `GET /api/agent/patients/PAT-000011`** — one patient, by portal id.

```jsonc
{ "success": true,
  "data": { "patientId": "PAT-000011", "name": "HUSSEIN AKMAL", "phone": "0166542542",
            "nricLast4": "5805",
            "iFobtStatus": true, "iFobtResult": true, "iFobtCompletionDate": "2026-08-06",
            "dischargeTypeId": null, "dischargeTypeName": null, "isActive": true } }

{ "success": false, "message": "Patient not found." }        // unknown id
```

**Ten fields, and that is the whole of it.** The full NRIC, the address, the e-mail, the emergency
contact, the birth date and age, race, religion, marital status and occupation **are on the record and
are deliberately not returned** (4.5).

---

**4 · `GET /api/agent/patients/PAT-000011/appointments`**

```jsonc
{ "success": true,
  "data": [
    { "appointmentId": 20, "appointmentDate": "2026-09-01",
      "startTime": "09:00", "endTime": "10:00", "status": "Scheduled",
      "staffId": "END-00001", "staffName": "P7 DOCTOR ALPHA",
      "branchId": "022367001", "branchName": "P7 SMOKE BRANCH",
      "appointmentTypeId": "01", "appointmentType": "PATIENT ASSESSMENT" } ] }
```

🔴 **The order is part of the contract — newest first (date, then start time, then id, all descending).
Do not re-sort it.**

🔴 **An unknown patient id returns an empty list, NOT an error.** So `data: []` means "this patient has
no appointments" — it does **not** mean "this patient is not registered". Endpoint 3 answers that
question and it is the only one that does.

---

**5 · `GET /api/agent/branches`** — every active partner facility.

```jsonc
{ "success": true,
  "data": [ { "branchId": "022367001", "name": "P7 SMOKE BRANCH", "state": "SELANGOR" } ] }
```

**A hospital that is not in this list is not a partner and cannot be booked.** This is the list the
agent matches a patient's answer against (§7.2).

---

**6 · `GET /api/agent/staff?branchId=022367001`**

```jsonc
{ "success": true,
  "data": [
    { "staffId": "END-00001", "name": "P7 DOCTOR ALPHA", "phone": "0123456789",
      "staffType": "END", "staffTypeName": "ENDOSCOPIST", "branchId": "022367001" } ] }
```

`staffTypeName` can be `null` — a clinician holding a staff-type code that has since been removed from
the lookup table still works at the branch, and dropping them would tell the agent a doctor standing in
the building does not work there. Handle the null; do not filter on it.

🔴 **`phone` here is the clinician's own mobile, and it exists for gate 1 only.** Nothing in the API
stops you from putting it in a patient's message — **the system prompt is what stops you** (§9). Never
send it to a patient.

An unknown `branchId` returns an empty list; a missing one is refused.

---

**7 · `GET /api/agent/slots/open?branchId=022367001&fromDate=2026-09-01&toDate=2026-09-02&staffType=END`**

```jsonc
{ "success": true,
  "data": [
    { "slotId": 34, "staffId": "END-00001", "staffName": "P7 DOCTOR ALPHA",
      "staffPhone": "0123456789", "staffType": "END",
      "slotDate": "2026-09-01", "startTime": "10:00", "endTime": "11:00" } ] }
```

**One row is exactly one hour.** A clinician free 09:00–12:00 is three rows, and grouping them into
"9am to noon" for the patient is your job. `slotId` is the value you hand back as `slotIds` on the
booking.

`fromDate` and `toDate` are **required** and must be `yyyy-MM-dd`. `staffType` is optional: leave it out
entirely to mean "any clinician". **An empty string is safe** — the controller converts blank to null
before the call, precisely so that an n8n expression that resolves to `""` does not answer "no
availability" to a question that meant "anyone". You do not need a conditional around it.

A bad date is refused, never guessed:
`{"success": false, "message": "fromDate is required and must be yyyy-MM-dd (for example 2026-09-01).", "correlationId": "…"}`

🔴 **THIS READ IS ADVISORY.** It runs outside any transaction and holds no lock. An hour it returns can
be taken by an administrator working in the portal one second later. That is not a bug and it is not
avoidable — see endpoint 8 and §7.4.

---

**8 · `POST /api/agent/appointments`** — the only write in the entire system.

```jsonc
POST /api/agent/appointments
X-Agent-Key: «the shared secret»
Content-Type: application/json

{ "patientId":       "PAT-000010",
  "appointmentDate": "2026-09-01",     // yyyy-MM-dd. Nothing else.
  "staffId":         "END-00001",
  "slotIds":         [34],             // slotId values from endpoint 7. One id = one hour.
  "pjAppTypeId":     "01",             // 🔴 THE STRING "01" AND NOTHING ELSE. Not 1, not "1", not "04".
  "branchId":        "022367001",
  "status":          "Scheduled" }     // exactly that, with that capitalisation
```

```jsonc
// committed
{ "success": true, "appointmentId": 21 }

// the database was asked and the answer was no — NOTHING was written, the transaction rolled back
{ "success": false, "reason": "SlotTaken",
  "message": "One or more slotIds were taken by another booking after they were read. …" }

// refused before any database call — this is a bug in your request body
{ "success": false,
  "message": "pjAppTypeId must be the string \"01\" (PATIENT ASSESSMENT). …",
  "correlationId": "3738f76b79d14c6e8fabfccbcd190e8c" }
```

🔴 **THE PRESENCE OF `reason` IS THE SIGNAL, AND YOUR SWITCH NODE SHOULD TEST FOR IT.** A response
carrying `reason` is an **outcome** — the request was well formed, the database was asked, the answer
was no. **The two are never both present.**

A response carrying `correlationId` instead is one of two things, and they are **not** distinguishable
from the body:

- **A client bug** — a validation refusal (`pjAppTypeId`, `status`, the date format, `slotIds`). Nothing
  was opened, read or begun, and retrying the identical body will fail identically.
- **A server fault** — the controller caught a `SqlException` or an unexpected exception and answered
  `"Error saving appointment."` (or `"Error retrieving …"` on a read). **Retrying the same body may well
  succeed**, and on the POST the booking's outcome is genuinely unknown: the transaction rolled back on
  a thrown exception, but a connection dropped after the commit looks identical from here.

🔴 **So do not treat every `correlationId` as permanent.** Branch on the `message`: the four validation
refusals name the field they refused (`pjAppTypeId must be…`, `status must be…`, `appointmentDate is
required and must be…`, `slotIds must contain…`) and are permanent. Anything beginning `Error ` is a
fault — escalate to a human rather than retry the booking, because a silent retry after a possible
commit is how a patient gets two appointments (§3.4).

### 4.4 The `reason` values — build your Switch node from this table

🔴 **`reason` is the enum member's name, verbatim.** Not `slot_taken`, not `SLOT_TAKEN`, not
`"Slot taken"`. Match these strings exactly.

| `reason` | What happened | What the workflow does |
|---|---|---|
| *(absent, `success: true`)* | Committed. `appointmentId` is the real portal id | store it, tell the patient, and hand the id to a human if it ever has to be undone |
| **`SlotTaken`** | Somebody took that hour between your slot read and this write | 🔴 **The one every build must handle.** Re-run endpoint 7 and offer another hour. **Not an error and not a retry-the-same-body** |
| **`SlotNotFound`** | At least one `slotId` was not among the rows the portal's own in-transaction read returned. **The broadest of the six** — it also swallows *another clinician's* slot and *another day's* slot | re-run endpoint 7 and book only from ids it just returned. Do not retry the same body |
| **`SlotsNotConsecutive`** | Sorted by start time, some hour does not begin exactly one hour after the one before it | send a contiguous run, or book fewer hours |
| **`SlotWrongStaff`** | A slot does not belong to the requested clinician | same as `SlotNotFound`. Cannot fire today — handle it anyway |
| **`SlotWrongDate`** | A slot is not on the appointment's date | same as `SlotNotFound`. Cannot fire today — handle it anyway |
| **`InsertFailed`** | The insert produced no id. Defensive; should never happen | escalate to a human. Nothing was written |

### 4.5 The three privacy rules the API enforces for you

1. **`nricLast4`, and never the full NRIC.** No endpoint returns the full twelve digits — not one, not
   ever, not even endpoint 3 which reads a record that holds them. The agent confirms identity by
   asking the patient for four digits and comparing; **it cannot state them because it is never given
   them.**
2. **The patient projection is deliberately narrow** — ten fields out of a record with more than thirty.
   The address, e-mail, emergency contact, birth date, age, race, religion, marital status and
   occupation are all withheld. A conversational agent repeats what it is handed; this is the defence.
3. **`staffPhone` is for gate 1 only** (endpoints 6 and 7). The API returns it because the agent has to
   message a clinician. **Nothing in the API stops it reaching a patient — §9's system prompt is the
   only thing that does.**

### 4.6 The one silent failure this design already prevents — and the check that proves it

An API-key request arrives with no cookie, so there is no logged-in user for the portal's audit trail
to name. Left alone, **every appointment the agent booked would be recorded as having been made by
"nobody"** — no error, no failed request, a corrupt audit trail on a clinical system, discovered
whenever somebody first needed it.

**That is solved.** A real `dbo.Users` row — `AGENT_SERVICE` — is seeded with the database and resolved
**by username** on every single request, and the filter attaches it as the acting user before any
action runs. If the row is missing the request fails loudly with a `503` rather than quietly writing a
zero.

It has been asserted against a real booking, and the result is recorded in `CoreFlow.md` §13.5:

```
AuditTrail_Id|User_Id|Username     |Action|Category           |Summary
150          |9      |AGENT_SERVICE|INSERT|PatientAppointment |Created Appointment: PatientAppointment_ID=21; …
```

**You do not need to do anything about this.** It is recorded here because §10.2 step 10 asks you to
confirm it once, end to end, after your first booking through n8n — and because it is the one failure
in this feature that no response body would ever have told you about.

### 4.7 The two things that are settings, not code — confirm before pointing n8n at production

Neither is an n8n task, and neither is a code change. They belong to whoever owns the App Service, and
`Nucentra_Azure_Deployment_Guide.md` is where the click-by-click lives.

| # | What | Why it matters to you |
|---|---|---|
| 1 | **`Agent__ApiKey`** — an App Service **app setting** (🔴 **two underscores**), holding the real key. Never in a file, never in source control | This is the value you paste into n8n's Header Auth credential (§6.1). 🔴 **If it is missing or misspelled, the API answers `401` to a caller holding the correct key** — indistinguishable, from n8n's end, from a wrong key |
| 2 | **An App Service access restriction on `/api/agent`** — allow n8n's egress addresses, deny the rest | The key is the authentication; this is what stops the internet from reaching the endpoint at all. 🔴 **Without it, the entire security of a patient register is one string in an HTTP header** — and there is no rate limiting on this surface by design |

**Rotating the key is one setting plus a restart — and a hard cutover.** There is one key with no
overlap window, so n8n's credential must be updated in the same minute. Worth knowing before it is
needed rather than during.

---

## 5. PART B — WhatsApp Cloud API setup, click by click

**Start this TODAY, before you build anything in n8n.** Two things here take real waiting time and
nothing you do in n8n can shorten them: **business verification** (days) and **template approval**
(minutes to days, per template). Everything else is an afternoon.

> **Written for somebody who has never opened a Meta developer page in their life.** Every step says
> which website, which menu, which button, and what you should see when it worked. Where Meta has
> renamed a button between releases — and it does, often — the alternative label is given in brackets.
> If a screen does not match, look for the label rather than the position: the wording survives
> redesigns better than the layout does.

### 5.0 The four places you will work, and what lives in each

🔴 **All of this is done in a DESKTOP WEB BROWSER. None of it can be done in the Meta Business Suite
phone app, and none of it is done in WhatsApp itself.** The only time your phone is involved is to
receive a verification code, and to free up the number you are going to use (5.5).

| # | Site | What it is | What you do there |
|---|---|---|---|
| 1 | **business.facebook.com** | **Meta Business Suite** and, inside it, **Business settings** | Create the business portfolio; create the **system user** and its permanent token; run business verification |
| 2 | **developers.facebook.com** | the **App Dashboard** | Create the app; add the WhatsApp product; read the ids; configure the **webhook**; flip the app Live |
| 3 | **business.facebook.com/wa/manage** | **WhatsApp Manager** (reachable from 1, but bookmark it separately) | Add and verify the **phone number**; the two-step PIN; **message templates**; messaging limits; quality rating; billing |
| 4 | **your n8n** | | Produce the webhook URL that site 2 needs, and hold the four credentials at the end |

They are three views of the same account, they cross-link constantly, and it is completely normal to
be bounced between them. **Keep all three open in three browser tabs** — you will go back and forth.

### 5.1 Before you start — have these in hand

| | |
|---|---|
| **A personal Facebook account** | Meta has no other way in. It is only an identity; nothing is ever posted to it |
| 🔴 **A phone number you can dedicate to this** | It receives an SMS or a voice call once, and then **belongs to the API forever** |
| **The business's legal details** | Registered name, address, and a registration document (SSM certificate) plus a recent utility bill or bank statement — for verification (5.9) |
| **A credit or debit card** | WhatsApp templates are not free past a small allowance (5.9) |
| **An n8n instance reachable from the public internet over HTTPS** | Meta calls *you*. `localhost` will never work (5.8) |
| **A scratch note (a password manager, not a text file)** | You will collect six values. 5.11 is the list |

#### 🔴 The phone number rule that catches everyone, said before you pick one

**A number registered to the WhatsApp Cloud API can no longer be used in the WhatsApp app on a
phone — not the normal app, not the WhatsApp Business app.** It becomes an API endpoint and stops
being a handset.

- **Do not use your personal number.**
- **Do not use the clinic's existing WhatsApp number** unless you are ready for staff to lose access
  to it in the app, and for its chat history to be deleted.
- If the number you want is currently active in either app, you must **delete the account from inside
  that app first** — open WhatsApp → **Settings → Account → Delete my account**. This erases that
  number's chat history. Do it deliberately.
- A brand-new prepaid SIM is the cleanest choice. It only ever needs to receive one code.

### 5.2 Step 1 — the Business portfolio *(business.facebook.com)*

This is the container that owns the app, the WhatsApp account and the phone number. Meta used to call
it a "Business Manager account"; you will still see that name in older guides.

1. Open **https://business.facebook.com** and log in with your Facebook account.
2. **If you have never created one:** you land on a "Create a portfolio" (older: "Create account")
   screen. Fill in:
   - **Business name** — this is public. Use the clinic's real trading name.
   - **Your name** and **business email** — the email gets a confirmation link. Click it.
   Then **Submit**.
3. **If you already have one:** the portfolio switcher is the button at the very top-left. Confirm the
   right business is selected before doing anything else — creating the app under a personal or wrong
   portfolio is the single most common reason step 5.7 later refuses to show you the WhatsApp account.
4. In the left rail, at the bottom, click the **gear / Settings** icon, then **Business settings**.
   That opens `business.facebook.com/settings`. **Bookmark this URL** — you come back to it in 5.7 and
   5.9.

✅ **You know it worked when** the left rail of Business settings shows sections called *Users*,
*Accounts* and *Data sources*.

### 5.3 Step 2 — create the app *(developers.facebook.com)*

1. Open **https://developers.facebook.com** and click **Log in** at the top right — same Facebook
   account.
2. **First visit only:** Meta asks you to register as a developer — confirm a phone number or email,
   accept the platform terms, and pick "Developer" when asked your role. This takes a minute.
3. Top right → **My Apps** → **Create App** (green button).
4. Meta's wording here has changed twice. You will get one of two flows:
   - **Newer flow:** *"What do you want your app to do?"* → choose **Other** → **Next** → app type
     **Business** → **Next**.
   - **Older flow:** it asks for the app type first → choose **Business**.
5. Fill in:
   - **App name** — internal only, patients never see it. `nucentra-whatsapp` is fine. 🔴 Do **not**
     put the word "WhatsApp" in it — Meta rejects app names containing its product names.
   - **App contact email**.
   - **Business portfolio** → select the one from 5.2. 🔴 **Do not leave this as "No business
     portfolio".** The WhatsApp product needs it, and attaching it afterwards is more work than
     choosing it now.
6. **Create app**, and re-enter your Facebook password when prompted.
7. You land on the App Dashboard. Go to **App settings → Basic** in the left rail and copy two values
   into your note:
   - **App ID** — a long number, also visible in the page URL.
   - **App secret** — click **Show**, re-enter your password. 🔴 Treat this like a password.

✅ **You know it worked when** the left rail shows *App settings*, *Roles*, *Alerts* and a
**Dashboard** with a grid of products you can add.

### 5.4 Step 3 — add the WhatsApp product, and collect the ids *(developers.facebook.com)*

1. On the App Dashboard, scroll the product tiles to **WhatsApp** and click **Set up**. (If you do not
   see tiles: left rail → **Add product**.)
2. Meta may ask you to select or create a **WhatsApp Business Account (WABA)** — accept the one it
   offers, or create one under your portfolio. Click through.
3. The left rail now has **WhatsApp** with children. Click **API Setup** (older: *Getting started*,
   *Quickstart*).
4. This one page holds three of the six values you need. Copy them into your note:
   - **Temporary access token** — at the top. 🔴 **It expires in 24 hours.** It is for poking around
     only. Do **not** build n8n on it; you replace it in 5.7 and, if you skip that, your agent will
     stop dead tomorrow morning with `401`s.
   - **Phone number ID** — under the **From** dropdown. Right now this is the ID of Meta's free **test
     number**, not yours. You will replace it in 5.5.
   - **WhatsApp Business Account ID** (WABA ID) — just below.
5. In the **To** section, click **Manage phone number list** and add your own mobile as a recipient.
   You will get a code on WhatsApp; enter it. The test number can only ever message the **five**
   numbers on this list.
6. **Sanity test, worth doing:** click **Send message**. A `hello_world` template should arrive on your
   phone within seconds.

✅ **When that message arrives, the app, the WABA and the token are all correctly wired together.** If
it does not arrive, nothing later in this section will work — fix it here.

> **The test number is for development and nothing else.** It cannot message a patient who is not on
> that five-number list, so the daily sweep is impossible with it. That is what 5.5 exists for.

### 5.5 Step 4 — add and verify your real phone number *(developers.facebook.com → WhatsApp Manager)*

🔴 Re-read the phone number rule in 5.1 before you do this. It is not reversible in any convenient way.

1. On the **API Setup** page, next to the **From** dropdown, click **Add phone number**. (Equivalently:
   WhatsApp Manager → **Phone numbers** → **Add phone number**.)
2. A form appears. Fill it:
   - **Display name** — 🔴 **this is what patients see as the sender name.** It must plausibly relate
     to the business; Meta reviews it. `nucentra Screening` or the clinic's trading name. Do not put a
     phone number, a URL, or a promotional phrase in it.
   - **Business category** — Medical and health.
   - **Business description**, **website** — optional but they help the display-name review pass.
   - **Time zone** — Asia/Kuala_Lumpur.
3. **Next**. Enter the phone number **with its country code** (Malaysia is `+60`, and you drop the
   leading `0`: `012-345 6789` is entered as `+60 12 345 6789`).
4. Choose **Text message** or **Phone call** for the verification code. If the number is a SIM in a
   handset, SMS is easiest. Click **Next**, then type the **6-digit code** in.
5. Meta may ask you to set the **two-step verification PIN** during this step — if it does, jump to 5.6
   now, do it, and come back.
6. When it completes, the number appears in the **From** dropdown on the API Setup page. Select it and
   copy its **Phone number ID**. 🔴 **This is a DIFFERENT number from the test number's ID.** Replace
   the value in your note. Using the test number's ID in n8n is a silent failure that looks like "only
   five people ever get my messages".

✅ **You know it worked when** WhatsApp Manager → **Phone numbers** lists your number with status
**Connected**, and a *Display name status* of **Pending review** or **Approved**. Pending is fine —
it does not block sending.

### 5.6 Step 5 — the two-step verification PIN *(WhatsApp Manager)*

A 6-digit PIN that locks the number to your account. Meta requires it, and you will need it again if
you ever move or re-register the number.

1. Open **https://business.facebook.com/wa/manage** → **Phone numbers** (older: *Account tools → Phone
   numbers*).
2. Click your number → the **gear / Settings** icon → **Two-step verification**.
3. Enter a 6-digit PIN, confirm it, **Save**.
4. 🔴 **Write it down in the same password manager as everything else.** There is no "forgot PIN" flow
   that does not involve waiting.

### 5.7 Step 6 — the permanent access token *(business.facebook.com → Business settings)*

🔴 **This is the step people skip, and it is why agents die overnight.** The token from 5.4 expires in
24 hours. A **system user** token does not expire at all.

1. Go to your bookmark: **business.facebook.com/settings**.
2. Left rail → **Users** → **System users** → **Add** (older: *Add a new system user*).
3. **System user name**: `nucentra-agent-system-user`. **Role**: **Admin**. → **Create system user**.
4. Select it in the list, then click **Assign assets**:
   - Asset type **Apps** → tick the app from 5.3 → turn on **Full control (Manage app)** →
     **Save changes**.
5. Click **Assign assets** a second time:
   - Asset type **WhatsApp accounts** → tick your WABA → turn on **Full control** → **Save changes**.
   - 🔴 **If "WhatsApp accounts" is empty or your WABA is not listed**, the WABA is not owned by this
     business portfolio. Go back to 5.3 step 5 — the app was created without a portfolio, or under the
     wrong one.
6. With the system user still selected, click **Generate new token**:
   - **App**: the app from 5.3.
   - **Token expiration**: 🔴 **Never**.
   - **Permissions**: tick **`whatsapp_business_messaging`** and **`whatsapp_business_management`**.
     (Add `business_management` only if you later want the same token to read the portfolio itself.)
   - **Generate token**.
7. 🔴 **Copy the token now. Meta shows it exactly once.** Put it in the password manager next to
   everything else. It is a bearer credential that can message every patient you have.

✅ **You know it worked when** the token starts with `EAA…` and the system user's row shows the app and
the WhatsApp account under *Assigned assets*.

### 5.8 Step 7 — the webhook: n8n FIRST, then Meta

🔴 **Order matters.** Meta calls your URL the instant you click *Verify and save*, and fails the whole
form if nothing answers. So the n8n end has to be live before you touch Meta.

**In n8n:**

1. Create a new workflow. Add a **WhatsApp Trigger** node.
2. Create its credential — call it `nucentra-whatsapp-webhook`. It asks for the **App ID** and **App
   secret** from 5.3. If your n8n version also asks for a **verify token**, invent a random string
   (20+ characters, letters and digits) and paste it there; if it *gives* you one instead, copy it.
   Either way, **that exact string goes into Meta in the next block.**
3. In the node's parameters, tick the **messages** event.
4. Copy the node's **Production URL**. 🔴 **The Production URL, not the Test URL** — the test URL only
   listens for about two minutes after you press "Listen for event".
5. **Save the workflow and switch it Active.** The production URL returns 404 while the workflow is
   inactive, and Meta will report exactly that as a failed verification.
6. **Self-hosted n8n:** Meta must reach that URL from the public internet, over HTTPS, with a valid
   certificate. A Cloudflare Tunnel or an ngrok tunnel in front of n8n is the usual answer. A
   `localhost` or `192.168.x.x` URL can never work.

**Then in Meta:**

7. App Dashboard → left rail **WhatsApp** → **Configuration** (older: the *Webhooks* box on the API
   Setup page).
8. In the **Webhook** section click **Edit**.
9. **Callback URL** = the n8n production URL from step 4. **Verify token** = the exact same string as
   step 2 — character for character, no trailing space.
10. **Verify and save**. Meta sends a `GET` to your URL with a challenge; if n8n answers, the dialog
    closes without complaint.
11. 🔴 **You are not done.** On the same page, next to **Webhook fields**, click **Manage**, find
    **`messages`** in the list, and tick **Subscribe**. **Without this the webhook is verified and no
    message is ever delivered to you** — the most common "everything is configured and nothing
    happens" failure in this whole section.

✅ **The real test:** from your own phone, send any WhatsApp message to your business number. Within a
second or two an execution should appear in n8n's execution list. **Do not proceed until it does.**

### 5.9 Step 8 — go Live, get verified, and turn on billing

None of these are optional for a production sweep, and the first two take days, so start them now.

1. **Flip the app Live.** App Dashboard → the toggle at the top of the page, **Development → Live**.
   Meta may first require a **Privacy Policy URL** under *App settings → Basic* — any public page that
   states what you do with the data is enough to satisfy the field.
2. **Business verification.** Business settings → **Security Centre** → **Start verification**. You
   submit the legal business name, address, phone, and a document — a business registration
   certificate, plus a utility bill or bank statement showing the same address. Meta reviews in
   anything from a day to a couple of weeks.
   🔴 **Unverified, you are capped at messaging 250 unique customers per 24 hours** and at two phone
   numbers. For a screening sweep that cap is real: budget for it, or verify early.
3. **Messaging limits.** WhatsApp Manager → **Phone numbers** → your number shows a tier: **250 → 1K →
   10K → 100K → unlimited** unique customers per day. Meta raises it automatically as you send
   quality traffic. 🔴 **Design WF1's daily batch around 250 until you are actually promoted.**
4. **Payment method.** WhatsApp Manager → **Billing** / *Payment settings* → add a card. **Every
   business-initiated template message is billable** past the free allowance, and the sweep's first
   message to each patient is exactly that. With no card on file, sends start failing — and they fail
   as an API error your workflow has to notice, not as a warning email.
   *(Replies you send inside the 24-hour window a patient opened are a different, cheaper — often
   free — category. §3.7 is why the agent's conversation lives there.)*
5. **Quality rating.** WhatsApp Manager → your number → **Quality rating**: Green, Yellow or Red.
   Patients blocking or reporting the number pushes it down; **Red gets the number restricted or
   suspended.** 🔴 This is the mechanism that makes §7.6's opt-out and §5.10's plain wording a
   *technical* requirement rather than good manners: a handful of "block" taps can end the programme's
   ability to send anything at all.

### 5.10 Step 9 — submit the seven message templates *(WhatsApp Manager)*

Every message the agent sends **first** — the sweep, the clinician request, the coordinator request,
and any confirmation that lands after the window closed — must be one of these pre-approved templates
(§3.7). Free-form text only works inside a window a patient opened by messaging you.

**Where:** WhatsApp Manager → **Manage templates** (`business.facebook.com/wa/manage/message-templates`)
→ **Create template**.

**For each of the seven below:**

1. **Category: `Utility`.** 🔴 Not *Marketing*. A marketing template is priced differently, is refused
   for content like this, and is suppressed for anyone who has opted out of marketing — which would
   silently drop patients from your sweep.
2. **Name:** exactly as in the table — lowercase letters, digits and underscores only. n8n nodes refer
   to these names verbatim, so a typo here is a broken workflow later.
3. **Language:** English. (Add Bahasa Melayu copies if you want Malay — templates are per-language and
   a missing translation is a send failure, not a fallback. §12.5.)
4. **Body:** paste the text from the table. Variables are `{{1}}`, `{{2}}`, … in order.
   🔴 Meta rejects a body that **starts or ends** with a variable, or that has **two variables next to
   each other**. The bodies below already obey this.
5. **Sample values:** Meta will not submit the form without a realistic example for every variable —
   `Ahmad`, `nucentra`, `Tuesday 1 September`, `9:00am`. **A missing sample is the single most common
   rejection.**
6. **Buttons (optional, recommended for #1 and #4):** add **Quick reply** buttons — `YES` / `CALL` on
   template 1, `YES` / `NO` on template 4. A tapped button arrives as that exact text, which makes
   WF2's parsing trivial and stops a patient replying "ya boleh" from falling through your switch.
7. **Submit.** Status goes **Pending** → **Approved** (usually minutes, sometimes a day). A rejected
   template can be edited and resubmitted — read the stated reason first; it is usually the sample
   values or a marketing tone.

#### The seven templates (category **UTILITY**)

Keep them plain — Meta rejects anything that reads like marketing, and a clinical service should read
plainly anyway.

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

### 5.11 The crib sheet — the six values, and where each one goes

By now your note should hold exactly these. This is what §6.1 consumes.

| Value | Where you got it | Where it goes in n8n |
|---|---|---|
| **Permanent access token** (`EAA…`) | 5.7 step 7 | credential `nucentra-whatsapp` |
| **Phone number ID** (of the **real** number) | 5.5 step 6 | credential `nucentra-whatsapp` / the WhatsApp node's *Phone Number ID* field |
| **WhatsApp Business Account ID** (WABA) | 5.4 step 4 | credential `nucentra-whatsapp`, where asked |
| **App ID** | 5.3 step 7 | credential `nucentra-whatsapp-webhook` |
| **App secret** | 5.3 step 7 | credential `nucentra-whatsapp-webhook` |
| **Webhook verify token** | 5.8 step 2 | credential `nucentra-whatsapp-webhook`, and Meta's Configuration page — **identical in both** |

Two more, from the portal side, complete §6.1: the **`X-Agent-Key`** value (§4.7) and an **Anthropic
API key**.

### 5.12 When it does not work — the eight failures, in the order you will meet them

| Symptom | Cause | Fix |
|---|---|---|
| *Verify and save* fails on the webhook | n8n workflow is not **Active**, you used the **Test URL**, or the verify tokens differ by a character | 5.8 steps 4–5 and 9 |
| Webhook verified, but no execution ever fires | you did not **Subscribe** to the `messages` field | 5.8 step 11 |
| Webhook verified, n8n unreachable | self-hosted n8n is not public over HTTPS with a valid certificate | 5.8 step 6 |
| Everything worked yesterday, `401` today | you built on the **temporary 24-hour token** | 5.7 — replace it with the system user token |
| Only five people ever receive messages | you are still sending from the **test number's** Phone Number ID | 5.5 step 6 |
| Template send fails with a not-found error | the template is still **Pending**, or the name or language does not match exactly | 5.10 steps 2–3, 7 |
| Sends stop after a while, no obvious error | you hit the **250 unique customers / 24h** unverified cap, or there is **no payment method** | 5.9 steps 2 and 4 |
| Number restricted or suspended | **quality rating** fell to Red — blocks and reports | 5.9 step 5, and honour every opt-out (§7.6) |

---

## 6. PART C — The n8n build

### 6.1 Credentials to create (n8n → Credentials → Add)

| Credential type | Name | Holds |
|---|---|---|
| **WhatsApp API** | `nucentra-whatsapp` | The permanent access token (§5.7) + the **real** number's Phone Number ID (§5.5) + the WABA ID (§5.4) |
| **WhatsApp Trigger** | `nucentra-whatsapp-webhook` | App ID, App Secret (§5.3), verify token (§5.8) |
| **Header Auth** (Generic) | `nucentra-agent-api` | Name `X-Agent-Key`, Value = the portal's `Agent__ApiKey` (§4.7). 🔴 Test this one first — §11 step 3 |
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
| 2 | **HTTP Request** | `POST {{PORTAL}}/api/agent/appointments` · Auth: `nucentra-agent-api` · JSON body exactly per §4.3 endpoint 8 · **`status` hard-coded to `"Scheduled"`, `pjAppTypeId` hard-coded to `"01"`** · *Never Error* on non-2xx so node 3 can read the body |
| 3 | **Switch** on `{{ $json.success }}` / `{{ $json.reason }}` | `true` → node 4 · `SlotTaken` → node 5 · anything else → node 6. The full reason list is §4.4 |
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
| 7b | **WhatsApp** template `crc_result_normal` | then the surveillance path (§7.5) — **a coordinator digest, not a booking.** The API refuses `"04"` (§3.5) |
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
`GET /api/agent/slots/open` (§4.3 endpoint 7) runs outside any transaction. Between that read and the
booking, an administrator in the portal can take the hour. The portal catches it and answers
`SlotTaken` — WF0 node 5 returns `RETRY_SLOTS` and the conversation resumes at slot discovery. **This
is a normal outcome, not an error.**

### 7.5 The surveillance path — propose only, never book
On `NEGATIVE`: send `crc_result_normal`, write the intended surveillance date to the state table, and
put the patient on the **coordinator digest**. A human opens the slot range and books it in the portal.

🔴 **Do not build a booking branch here.** §3.5 is settled as option C and the portal enforces it:
`POST /api/agent/appointments` accepts `pjAppTypeId` `"01"` and refuses `"04"` by name, before it
touches the database. There is no `SURVEILLANCE_HORIZON_DAYS` to configure and no slot far enough out
to consume — the honest hand-off is the design, not a fallback.

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

> 🔴 **This block is fenced with FOUR backticks, because the prompt itself contains a three-backtick
> `json` example.** Copy everything between the four-backtick lines and nothing else.

````
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
- slotIds are the slotId values from find_open_slots, as numbers. One hour = one id.
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
````

---

## 10. Test plan

### 10.1 Portal API — prove it answers *your* environment before you build anything

The API is already tested; what you are testing here is **this deployment, from where n8n will call
it** — the key, the URL and the network path.

**The fast way:** `Test-AgentApi.ps1` at the repo root drives all eight endpoints and both negative
tests — twelve checks — and prints a pass/fail line for each. It takes `-BaseUrl` and `-ApiKey`, and
the write is opt-in behind `-IncludeWrite` because it consumes a real clinician hour the API cannot
give back.

```bash
powershell -File Test-AgentApi.ps1 -BaseUrl "$PORTAL" -ApiKey "$KEY"
```

**The manual way**, if you would rather see the bodies. `$KEY` is the value of `Agent__ApiKey`:

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

🔴 **`User_Id` must be the `AGENT_SERVICE` account's id — not `0`, not blank** (§4.6). It reads
`AGENT_SERVICE` on the development database; if it reads `0` here, the portal was published without its
seed and that is a deployment problem, not an n8n one.

> **That POST consumes a real clinician hour and nothing in this system can release it** (§3.6). Use a
> throwaway patient and a slot you opened for the purpose, and expect to delete the appointment from
> the portal by hand afterwards.

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

You are staring at the n8n homepage. Here is the order.

**✅ The portal is done — §4 needs nothing from you.** The API answers today. That removes the one
blocker this plan used to open with.

**Today, in a browser, before n8n:**
1. **Start §5 now.** Business verification and template approval are the long poles — days, not hours,
   and nothing in n8n shortens them. Get through §5.8 (the webhook) and submit all seven templates,
   then come back while they sit in review.
2. **Confirm §4.7 with whoever owns the App Service** — that `Agent__ApiKey` is set on the environment
   n8n will point at, and that the access restriction allows n8n's egress addresses. These are the two
   settings that make a working API look broken.

**Then, in n8n, in this order:**
3. **Credentials** (§6.1). Four of them. 🔴 **Test the Header Auth one first**, with a throwaway
   workflow: `Manual Trigger → HTTP Request → GET {{PORTAL}}/api/agent/patients/queue`. **When that
   returns your patient list, the entire portal half is proven** and everything after it is n8n work.
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

> 🔴 **THIS SECTION IS A QUESTIONNAIRE, NOT A SPECIFICATION. DO NOT BUILD FROM IT.**
>
> Everything in §1–§11 is decided and safe to build. Everything below is **not yet decided**, and each
> item names the exact place in §1–§11 that is currently silent or ambiguous. When an answer is chosen
> it gets written into that place, and the item here is struck through. **A builder who reaches an item
> that is still open must stop and ask — never guess.**
>
> Every question carries a 🟢 **recommended** option. The recommendation is what closes the loop with
> the least new machinery, unless a stated reason overrides that.

### Already settled — no action

| # | Item | Outcome |
|---|---|---|
| S1 | ~~The surveillance horizon~~ | ✅ **§3.5 option C, propose only.** The portal enforces it: `pjAppTypeId` must be `"01"`, `"04"` is refused by name. No horizon to configure. |
| S2 | ~~`CoreFlow.md` §2.2 needs updating~~ | ✅ **Done.** The `AllowAnonymous` count reads three, and §13 documents the Agent API as built. |

---

### Group A — Identity and routing: 🔴 four things that will break the build on day one

Every inbound WhatsApp message is routed by `crc_agent_state.waId` (WF2 node 3). Four kinds of message
cannot currently find their row.

---

**Q1 · Phone-number format — the portal stores `0166542542`, Meta sends `60166542542`, and nothing in
this plan converts between them.**

`GET /api/agent/patients/queue` and `/staff` return the number **as an administrator typed it**. Meta's
`wa_id` is always country-code-first with no `+` and no separators. WF1 7a, WF1 8 and WF2a a10 all write
a state row keyed on a number, and WF2 node 3 reads it back keyed on Meta's. **If the two formats differ,
every state row written by an outbound message is orphaned and every inbound reply looks like a stranger.**
(Endpoint 2 is immune — it matches on the last nine digits, in any format. The state table is not.)

| Option | What it means |
|---|---|
| 🟢 **A. One Code node, `toWaId()`, called everywhere a number is written or read** | Strip non-digits; drop a leading `0`; prepend `60` if it is not already there. Applied in WF1 before the send and the upsert, in WF2a a10 before the staff send and upsert, and to the coordinator number. One function, four call sites, defined once in §6.2. |
| B. Normalise the column in the portal | A migration plus form validation on `dbo.PatientBasic.Patient_Phone` and `dbo.Staff.Staff_Phone`. Fixes it at source but is a portal change, touches screens this project has not touched, and does nothing for numbers typed tomorrow. |
| C. Store both formats in the state table | `waId` and `portalPhone` as separate columns. Doubles the key surface for no benefit — you still have to convert to write one of them. |

> 🔴 **Assume nothing about Malaysian numbers beyond `+60`.** If the register can hold a non-Malaysian
> number, say so in your answer — option A's rule is `60`-only and would silently mangle one.

Answer: A (recommended option)

---

**Q2 · The coordinator has no identity anywhere in this system, and their `APPROVE` is unroutable.**

WF2b b2 sends `crc_coordinator_approval` to "the coordinator's number" — a value that is defined nowhere,
in no credential, no Data Table and no environment variable. Worse: **nothing ever creates a state row
with `role = COORDINATOR`.** So when the coordinator replies `APPROVE abc123`, WF2 node 4 finds no row,
falls to node 5, looks the coordinator up as a *patient*, gets `matchCount: 0`, and sends them
`crc_handover_human`. **Gate 2 cannot complete as written.** The same hole swallows the clinician's `YES`
if a10's upsert is skipped or mis-keyed (Q1).

This is also the old open item *"who is the coordinator?"* — and it now owns four other things: the
follow-up `crc_handover_human` promises after a REJECT (WF2c c8), the surveillance hand-off (§7.5), the
`NO_PHONE` digest (WF1 7d), and every escalation (§7.6).

| Option | What it means |
|---|---|
| 🟢 **A. One number, held in an n8n environment variable, plus a permanent seeded `COORDINATOR` state row** | `COORDINATOR_WA_ID` is set once. A one-off manual run seeds a state row with that `waId` and `role = COORDINATOR` so WF2 node 3 finds it. Simplest thing that closes the loop; a rota is a later change to one variable. |
| B. A rota — a second Data Table of coordinator numbers with an on-duty flag | Correct for a real clinic with shifts, and roughly one more Data Table plus a lookup node in three places. Choose this only if approvals genuinely have to follow a shift; otherwise it is machinery guarding an empty room. |
| C. Slack / e-mail instead of WhatsApp for gate 2 | Removes the 24-hour-window problem (Q13) for approvals entirely and gives you a real audit thread. Costs a fifth credential and a second inbound trigger, and WF2c stops being a WhatsApp branch. |

🔴 **Whichever you pick, name a human.** `crc_handover_human` tells a patient "a member of our team will
contact you shortly", and nothing in n8n can make that true.

Answer: C (not Slack, just email)

---

**Q3 · Gate 1 carries no proposal id, so two proposals to the same clinician are indistinguishable.**

Template 4 `crc_staff_slot_request` has five variables and **none of them is `proposalId`**. Its quick-reply
buttons deliver the bare strings `YES` and `NO`. WF2b b1 switches on that text and updates *the clinician's
one state row* — and §6.2 is explicit that the table is keyed on `waId`, one row per number. So a second
proposal sent to the same doctor **overwrites the first**, and their `YES` is applied to whichever
proposal happens to be in the row. WF2c c1 already solves exactly this problem for the coordinator, by
matching the id and never "the most recent proposal". Gate 1 has no equivalent.

| Option | What it means |
|---|---|
| 🟢 **A. Add a sixth variable to template 4 and drop the quick-reply buttons** | Body ends `Reply YES {{6}} or NO {{6}}`, parsed exactly like WF2c c1. Costs the buttons (Meta cannot put a per-send value inside a quick-reply payload the way this needs) and one template resubmission — do it **before** §5.10, not after approval. |
| B. Keep the buttons; serialise per clinician | Never send a second proposal to a clinician who is already `AWAITING_STAFF`; queue it. Keeps template 4 as designed, but a busy clinician becomes a bottleneck for the whole sweep and the queue is more state to build. |
| C. Keep the buttons and accept the collision | Only defensible if one clinician can never hold two open proposals — which the daily sweep makes untrue the first busy morning. |

Answer: A (recommended option)

---

**Q4 · A shared household phone can only hold one state row, so the second patient silently disappears.**

§3.3 states plainly that two patients sharing a number is normal and that `matchCount > 1` must be handled.
But `crc_agent_state` is keyed on `waId` with a single `patientId`. If WF1's sweep finds two POSITIVE
patients on one number, node 8's upsert writes the row twice and **the second overwrites the first** — one
patient receives a message intended for the other, and the other is never contacted again (their row is
gone, and the surviving row's `stage` keeps the sweep away).

| Option | What it means |
|---|---|
| 🟢 **A. Key the table on `waId` + `patientId`, and let WF1 send only one message per `waId` per sweep** | The second patient stays in the table, is not messaged today, and is picked up on a later sweep once the first conversation closes. WF2's lookup by `waId` may now return several rows — which is correct, and is the same disambiguation §3.3 already requires the agent to perform. |
| B. Leave the key alone; WF1 skips any `waId` that already has a row for a different patient, and digests them to the coordinator | Nobody is silently dropped, and no schema change. But those patients never get an automated conversation at all. |
| C. Leave it as is | 🔴 Not viable. This is silent patient loss in a cancer screening programme, and nothing anywhere would report it. |

Answer: A (recommended option)

---

### Group B — Loops that never close: 🔴 five dead ends with no timeout anywhere

**There is not one timer in this entire design.** Every wait is "write the expected state and end the
execution" (§6.3's WF2 note), which is correct — but it means that when the expected message never
arrives, **the conversation stops forever and nothing notices.** WF1 node 4 then drops the patient on
every subsequent sweep, because their `stage` is not `CLOSED`/empty. A patient can be told *"I'll confirm
this with the doctor and message you back shortly"* and never be contacted again by anything.

---

**Q5 · The gates have no timeout. `AWAITING_STAFF` and `AWAITING_APPROVAL` are permanent.**

WF2b handles `YES`, `NO` and "anything else". It does not handle **silence**, and neither does WF2c. A
clinician who is on leave, or a coordinator who is asleep, freezes a real patient indefinitely.

| Option | What it means |
|---|---|
| 🟢 **A. A fifth workflow — WF3 `CRC · Reaper` — on a schedule, say hourly** | Reads `crc_agent_state` for rows whose `stage` is `AWAITING_STAFF`/`AWAITING_APPROVAL` and whose `updatedAt` is older than a threshold. **Gate 1 stale → re-send template 4 once, then `attempts + 1`, then at 3 escalate. Gate 2 stale → nudge the coordinator, then escalate.** One workflow closes Q5, Q6 and Q7 together, which is the argument for it. Suggested thresholds: **gate 1 = 4 working hours, gate 2 = 12 hours** — answer with your own if these are wrong. |
| B. Let the next daily sweep do it | Change WF1 node 4 to re-admit rows stuck in a gate for over 24h. No new workflow, but the granularity is a day and WF1 stops being a pure sweep. |
| C. No timeout; the coordinator watches the table | Honest only if somebody genuinely opens that table every morning. 🔴 If you choose this, say who — and it belongs in §7.6 as a named human duty, not as an absence. |

Answer: A (recommended option)
---

**Q6 · A patient who never replies to the sweep is contacted once and then dropped forever.**

WF1 7a writes `stage = AWAITING_CONSENT`. Node 4 drops any row whose stage is not `CLOSED`/empty. So the
first message is also the last. `attempts` exists in §6.2 ("give up and escalate after 3") but **nothing
on this path ever increments it** — no reply means no execution means no node runs.

| Option | What it means |
|---|---|
| 🟢 **A. WF3 chases: re-send the sweep template at day 3 and day 7, then escalate to the coordinator at 3 attempts** | Uses the `attempts` column as §6.2 already intends. 🔴 Each chase is a **billable business-initiated template** (§5.9 step 4) and counts against the 250/24h cap — budget for roughly 3× the cohort. |
| B. One message only; unanswered patients go on a weekly coordinator digest for a phone call | Cheapest, and arguably better clinically — a positive screening result deserves a human call, not a third WhatsApp. |
| C. One message only, no digest | 🔴 Not viable for a POSITIVE result. It is the current behaviour, and it is the biggest silent hole in the design. |

Answer: A (recommended option)
---

**Q7 · A patient who abandons mid-conversation is in the same trap.**

`CHOOSING_BRANCH`, `CHOOSING_SLOT`, `IDENTIFYING` — all are "not `CLOSED`", so the sweep skips them and
nothing resumes them. Same mechanism as Q6, different stage. **Answer this together with Q5 and Q6 if you
choose the reaper** — one workflow, one threshold table, three problems.

🟢 **Recommended: WF3 handles it. Stale mid-conversation row → one "are you still there?" nudge inside the
window if it is open, a template if it is not, then `stage = CLOSED` with `lastReason = "Patient stopped
replying"` — which makes them sweepable again tomorrow, exactly as a c7 reject is.**

Answer: A (recommended option)
---

**Q8 · Nothing ever clears `stage = ESCALATED`, and no workflow tells anyone it was set.**

An escalated patient is out of the sweep permanently (node 4 drops them) and the only exit is a human
editing an n8n Data Table by hand. That may be right — §7.6 does say *"no further automated messaging"* —
but it is nowhere stated as a duty, and 🔴 **nothing notifies the coordinator that an escalation happened
at all.** The patient is told a person will contact them; no person is told.

| Option | What it means |
|---|---|
| 🟢 **A. Every escalation sends the coordinator a message naming the patient, the `waId` and `lastReason`, and §7.6 gains a sentence saying a human clears the row by hand** | Makes the promise in `crc_handover_human` true. The manual clear stays manual, on purpose: re-admitting a patient to automated messaging should be a decision. |
| B. Auto-clear after N days back to `CLOSED` | Puts them back in the sweep without anyone having looked. Fine for "stopped replying", 🔴 wrong for "mentioned bleeding". |
| C. As now | Escalations vanish. Not viable given §7.6's promise. |

Answer: A (recommended option)
---

**Q9 · The agent has no way to *signal* an escalation, so a9 cannot detect one.**

§9 defines exactly one structured output: the `proposal` JSON block. a9's rule is *"if it contains a
proposal → a10, otherwise → a11 (send the text)"*. So when the model correctly decides to escalate — a
patient mentions bleeding, identity fails twice, someone asks for a prognosis — **it says the escalation
sentence to the patient and the workflow treats it as ordinary chat.** `stage` is never set to `ESCALATED`,
no human is told, and the next inbound message goes straight back into the agent. Every escalation trigger
in §7.6 and §9 is currently decorative. The same gap is why a0's sidebar can ask a9 to write
`lastReason = "Possible opt-out — review"` with nothing for a9 to match on.

| Option | What it means |
|---|---|
| 🟢 **A. Add a second fenced block to §9's output contract** | `{"escalate":{"reason":"SYMPTOMS"}}` with a small closed set — `SYMPTOMS`, `DISTRESS`, `CLINICAL_QUESTION`, `IDENTITY_FAILED`, `POSSIBLE_OPT_OUT`, `OTHER`. a9 branches on `proposal` → a10, `escalate` → a new a13 (set `stage = ESCALATED`, `lastReason`, notify the coordinator per Q8), else → a11. Symmetrical with the proposal block the agent already emits reliably. |
| B. String-match the escalation sentence in a9 | No prompt change, and brittle in exactly the way §7's opening rule warns about — the model paraphrases, and a missed match is a patient who mentioned bleeding being answered by a bot. |
| C. A sixth tool, `escalate_to_human`, that the agent calls | Cleanest agent ergonomics, and it makes the escalation an *action* rather than a parse. Costs a webhook or sub-workflow tool and breaks §7.1's "its five tools are all reads" — which is a rule worth keeping literal. |

Answer: A (recommended option)
---

### Group C — Paths the design describes but does not have a mechanism for

**Q10 · "Back into WF2a's slot discovery" is written three times and cannot happen as described.**

WF2b b4, WF2c c4 and WF0 node 5 (`RETRY_SLOTS`) all hand control "back to WF2a slot discovery". But WF2a
is an **AI Agent node driven by an inbound patient message**, and at those three moments there is no
inbound message — the trigger was a clinician's `NO`, a coordinator's `APPROVE`, or a `SlotTaken` from the
portal. There is no defined way to make the agent take a turn on its own.

| Option | What it means |
|---|---|
| 🟢 **A. A synthetic turn** | Call the AI Agent node with a system-authored user message — *"[SYSTEM] The 09:00 hour is no longer available. Apologise briefly and offer up to three alternatives."* — on the same `waId` memory key, so the conversation keeps its history. Define the exact wording of the three synthetic turns in §9 so the model treats them as instructions and never repeats them to the patient. |
| B. No agent turn; a fixed templated message plus a fresh `find_open_slots` call in plain nodes, patient replies, and *that* reply re-enters WF2a normally | Deterministic and cheaper. Loses the conversational quality on exactly the turn where the patient is being let down. |
| C. Escalate to a human on every retry | Safest, and it converts a routine race (§7.4 calls `SlotTaken` "a normal outcome") into a human interruption. |

Answer: A (recommended option)
---

**Q11 · On `matchCount > 1` the agent is asked to disambiguate with data it was never given.**

WF2 node 5 routes `>1` to *"ask a disambiguating question, stay in `IDENTIFYING`"*, and §9 tells the agent
to *"match on both"* name and last-4. But the agent's five tools contain **no phone lookup** — `get_patient`
needs a `patientId` it does not have, and the by-phone response that holds the candidates is consumed by
node 5 and thrown away. **The agent cannot see who the candidates are.** The same shortfall applies in the
ordinary `matchCount = 1` case: §9 step 1 says compare the last 4 digits and never says where the agent
gets them from.

| Option | What it means |
|---|---|
| 🟢 **A. Node 5 injects the candidates into the state block** | Add `candidates: [{patientId, name, nricLast4}]` to §9's "context injected each turn". The agent compares in-context, and 🔴 the full NRIC never enters the conversation — only the four digits the API already returns. Also solves the `matchCount = 1` case with the same field. |
| B. A sixth tool, `find_patient_by_phone` | Gives the agent the raw endpoint. It also hands a conversational model a *list of other people's names and phone numbers* on one call, which is the one thing §4.5's narrow projection exists to avoid. |
| C. Never let the agent disambiguate — escalate every `matchCount > 1` to a human | Zero risk, and it sends a shared household phone — an ordinary case, not an edge case — to a person every time. |

Answer: A (recommended option)
---

**Q12 · The system prompt is written for the POSITIVE path only, and three templates invite replies it
cannot handle.**

Templates 2 and 3 end with *"Reply **QUESTION**"* and *"Reply **HELP**"*, and template 1 offers **CALL**.
All three arrive at WF2a and land on the §9 prompt, whose STEP 2 says *"their screening test result is
ready and the team would like to arrange a follow-up assessment"* — **which is wrong for a NEGATIVE patient
and wrong for an INCOMPLETE one.** §9 receives `screeningState` in its context block and never branches on
it. `CALL` has no handling at all.

| Option | What it means |
|---|---|
| 🟢 **A. §9 gains a STEP 0 that branches on `screeningState`** | `POSITIVE` → the existing flow. `NEGATIVE` → reassure, answer nothing clinical, escalate on any question. `INCOMPLETE`/`UNRECORDED` → explain how to complete the test, take no booking, escalate on any question. `CALL` on any path → escalate immediately (per Q9's mechanism). One prompt, four openings — the rest of the prompt is already path-neutral. |
| B. Three separate AI Agent nodes, one per state, routed by a Switch before a1 | Cleaner prompts, three times the prompt maintenance, and a patient whose state changes mid-conversation crosses nodes. |
| C. Only POSITIVE reaches the agent; NEGATIVE and INCOMPLETE replies go straight to the coordinator | Very safe, and it makes templates 2 and 3 promises the coordinator must personally keep — count the volume before choosing it. |

Answer: A (recommended option)
---

**Q13 · Four outbound messages ignore the 24-hour window they are subject to.**

§3.7 is unambiguous and §6.2 gives `lastInboundAt` as the check — but only WF2c c8 actually applies it.
**WF2b b4** (telling the patient the clinician declined), **WF2c c5**'s note to the clinician, and
**WF2c c9**'s two messages are all free-form and can all land hours after that person's last inbound
message. Outside the window Meta refuses them, and the workflow does not look at the response.

| Option | What it means |
|---|---|
| 🟢 **A. One shared "send" sub-workflow: if `now - lastInboundAt < 24h` send free-form, else send the mapped template** | Every outbound message in the system goes through it, and the window stops being something four separate nodes have to remember. Needs one more template — an eighth, `crc_staff_notice`, for the two clinician notes; the patient path reuses `crc_handover_human`. |
| B. Fix the four nodes individually | No new workflow, and the fifth node someone adds later will get it wrong. |
| C. Send anything to staff and coordinators free-form and accept the failures | 🔴 Staff and coordinators are subject to the same window as patients. A clinician who has not messaged in a day simply does not receive the request, silently. |

Answer: A (recommended option)
---

### Group D — Data and semantics

**Q14 · `UNRECORDED` means two different things, and the agent tells one of them something false.**

`spAgentPatient_ListScreeningQueue` returns `UNRECORDED` both when the iFOBT was **never recorded**
(`Patient_iFOBTStatus IS NULL`) and when the test is marked **complete but the result is still null**
(`status = 1`, `results IS NULL` — the procedure's own `ELSE` branch). WF1 node 6 routes both to 7c, which
sends `crc_test_incomplete`: *"Our records show your screening test was not completed."* 🔴 **To a patient
who has completed it and is waiting for a result, that message is simply untrue** — and it is the state
every patient passes through between the lab and the data entry.

*(Related: the §1 diagram shows four branches and omits `UNRECORDED` entirely, though it is one of the
five states and the most common one on a fresh database. It will be redrawn with whatever you decide.)*

| Option | What it means |
|---|---|
| 🟢 **A. Split it in the portal: a sixth state `RESULT_PENDING` for `status = 1` + `results IS NULL`** | About six lines in `spAgentPatient_ListScreeningQueue`'s `CASE`, no C# change (`screeningState` is passed through as a string), a re-publish of the DACPAC — and it retires §12's only remaining portal change. Those patients then get **no message at all** and go on the coordinator digest, which is the honest answer: there is nothing to tell them yet. |
| B. Leave the API alone; WF1 splits on `iFobtStatus === true && iFobtResult === null` | No portal change, and it re-derives in an n8n expression the thing §3.2 says the API exists to stop you re-deriving. |
| C. Send them nothing and log them | Same outcome as A without naming the state, so nobody reading the queue can see the category exists. |

Answer: A (recommended option)
---

**Q15 · The surveillance path has no interval, no column and no digest.**

§7.5 says *"write the intended surveillance date to the state table"*, but **§6.2 has no such column**; the
§1 diagram says *"we'll re-check in N months"* and **N is never given**; and template 2 has two variables
(name, brand) and **cannot carry a date**. §7.5 and WF1 7b and 7d all end at "the coordinator digest",
which has **no node, no channel, no format and no schedule** — and WF1 node 5 is a per-item loop, so a
digest that is *"one message listing every unreachable patient"* needs an aggregation step the node table
does not have.

Answer all four parts:

- **The interval.** 🟢 **Recommended: 24 months**, the usual interval after a negative iFOBT — 🔴 **confirm
  it with the clinical lead; this is a clinical parameter and this plan has no authority to set it.**
- **Where it is stored.** 🟢 **A `surveillanceDueDate` column on `crc_agent_state`** (add it to §6.2),
  computed as `iFobtCompletionDate + interval`.
- **Whether the patient is told.** 🟢 **No.** Template 2 says *"we will contact you again for your next
  routine check"* and stays as it is — a date the portal has not booked is not a date to promise.
- **The digest.** 🟢 **A digest is a workflow, not a node.** Give it to WF3 (Q5): a daily run that reads the
  state table and sends the coordinator **one** message covering the day's `NO_PHONE` patients, surveillance
  due dates, escalations and stalled gates. WF1's 7b and 7d then only write state, and WF1 needs no
  aggregation step at all. **Alternative:** an *Aggregate* node after WF1's loop and a separate send —
  more nodes, and a second digest to keep consistent with the first.

Answer: A (recommended option)
---

**Q16 · The agent can offer an hour that has already passed.**

`spAgentSlots_FindOpenByBranch` filters on `SlotDate BETWEEN @FromDate AND @ToDate` and **nothing else** —
there is no "not in the past" predicate, on the date or on the time. §9 step 5 tells the agent to search
"the next 14 days" starting from today, so at 3pm it can be handed, and will happily offer, this morning's
09:00. It would even book: `SaveAppointmentAsync` validates the slots, not the clock.

| Option | What it means |
|---|---|
| 🟢 **A. §9 gains a hard rule, and `fromDate` is always tomorrow** | *"Never offer an hour that has already started. The earliest bookable day is tomorrow."* Zero code, and it also gives the clinic a night's notice — which a same-day assessment booked by a bot at 4:55pm does not. |
| B. Filter in the procedure | `SlotDate > CAST(GETDATE() AS DATE) OR (SlotDate = today AND SlotStartTime > CAST(GETDATE() AS TIME))`. Correct at the source and fixes it for every future caller — but 🔴 **`GETDATE()` on Azure SQL is UTC, and Malaysia is UTC+8**, so it needs `AT TIME ZONE` to be right, and it is a portal change for a rule the agent can simply obey. |
| C. Leave it | A patient is offered an appointment that started three hours ago, and a clinician's past hour is consumed. |

Answer: A (recommended option)
---

**Q17 · Nothing notices a message that fails to send.**

§5.9 warns that sends fail as API errors "your workflow has to notice" — no node notices. A blocked
number, an unregistered number, a quality-rating restriction or a missing payment method all fail per
message, and WF1 records `lastOutboundAt = now` regardless, so the patient is marked contacted and
dropped from the sweep. 🟢 **Recommended: every WhatsApp node set to *Continue on error*, with the failure
branch writing `lastReason` and adding the patient to the coordinator digest (Q15) instead of writing
`lastOutboundAt`.** A patient who could not be reached must not be recorded as reached.

Answer: A (recommended option)
---

**Q18 · `hasAssessment` is returned by the API and used by nothing.**

Endpoint 1 computes it (has a `PATIENT ASSESSMENT` journey ever been recorded?) and no workflow reads it.
A patient who has already been through an assessment and has no *future* appointment is swept again with
*"your result is ready, let's book your assessment"*.

| Option | What it means |
|---|---|
| 🟢 **A. WF1 node 3 also drops `hasAssessment == true`, and those patients go on the coordinator digest** | A completed assessment means the programme has moved past the agent's one job (§0). Whatever happens next — colonoscopy, follow-up — is staff work in the portal (decision #4). |
| B. Sweep them anyway | Defensible only if a second positive iFOBT should re-open a second assessment. 🔴 That is a clinical question, not a workflow one. |
| C. Leave it unread and say so in §4.3 | Honest, and it leaves a field on the wire that means nothing — which is how the next builder ends up guessing. |

Answer: A (recommended option)
---

**Q19 · `SlotNotFound` is routed two different ways by two different sections.**

§4.4's table says *"re-run endpoint 7 and book only from ids it just returned"* — i.e. retry slot
discovery. WF0 node 3 sends everything that is not `success` or `SlotTaken` to node 6 → `ESCALATE`. Both
are defensible and they contradict each other, so a builder will pick one at random. 🟢 **Recommended:
WF0 wins — escalate.** By the time WF0 runs, a clinician *and* a coordinator have approved a specific hour;
`SlotNotFound` means the stored proposal no longer describes reality, which is a different and more
worrying thing than the ordinary race `SlotTaken` describes. §4.4's row will be reworded to say so.

Answer: A (recommended option)
---

**Q20 · `crc_agent_events` — history, or not.**

§6.2 already flags this and says *"decide before go-live, not after — history you did not write is not
recoverable."* The state table is keyed on `waId` and overwritten, so `lastReason` holds only the most
recent ending. It can answer *"why did this conversation end?"* and never *"how many proposals did
coordinators reject last month, and why?"*.

🟢 **Recommended: build it.** An append-only Data Table (`waId`, `patientId`, `proposalId`, `at`, `event`,
`reason`) written at every terminal outcome — booked, rejected, opted out, escalated, timed out. It is one
extra node on paths you are already building, it is the only way to audit an automation that talks to
patients about cancer screening, and it cannot be added retroactively. **Decline only if you have a
different plan for that audit trail** — note that `dbo.AuditTrails` records the *booking* and nothing about
the conversation that led to it.

Answer: A (recommended option)
---

**Q21 · WF0 stores an `appointmentId` in a column that does not exist.**

WF0 node 4 says *"`stage = BOOKED`, store `appointmentId`"*, and §6.2's column list has no
`appointmentId`. 🟢 **Recommended: add it** — it is the id a human needs for
`POST /Patient/DeleteAppointment`, which §3.6 makes the only way to undo a booking, and losing it means
finding the appointment by hand.

Answer: A (recommended option)
---

### Group E — Carried over, still open

**Q22 · PDPA position** on clinical messaging over WhatsApp, and whether patients must opt in first. The
Agent API moved nothing outside the portal — **the PDPA question arrives with WhatsApp**, in §5. No
recommendation offered: this needs a legal answer, not an engineering one. 🔴 It is the one item here that
can invalidate the whole design, so answer it early. If opt-in is required, WF1's first message becomes a
consent request and every branch in §6.3 gains a gate in front of it.

Answer: No consent needed

**Q23 · Out-of-hours.** WF1 fires at 09:00 MYT, but inbound replies arrive at any hour and WF2 answers
immediately. 🟢 **Recommended: reply at any hour** — a worried patient at 2am is better served by an answer
than by silence, the agent never says anything clinical, and every escalation path already ends at a human.
**But cap the outbound sweep to business hours** so the *first* message of a conversation is never sent at
night. Alternative: queue overnight inbound messages to 08:00, which needs a queue and a release schedule.

Answer: A (recommended option)

**Q24 · Language.** §9 mirrors English/Malay in free-form, but **templates are per-language in Meta and a
missing translation is a send failure, not a fallback** (§5.10 step 3). Today the seven templates are
English-only, so every business-initiated message is English regardless of what the patient writes.
🟢 **Recommended: submit Malay copies of all seven** before go-live, and add a `language` column to
`crc_agent_state` set from the patient's first reply. **Alternative:** English-only templates and Malay
free-form, which is inconsistent but honest — say so if you choose it, so nobody reports it as a bug.

Answer: A (recommended option)

**Q25 · `NO_PHONE` patients.** They become a coordinator digest and nothing more, in both this plan and
`AgentApiPlan.md`'s open item 4. 🔴 The deeper problem is upstream: `Patient_Phone` is `VARCHAR(100) NOT
NULL` and **nothing stops it being an empty string** — the register permits a patient with no way to be
contacted. 🟢 **Recommended: the digest is the agent-side answer, and separately add phone validation to
the portal's patient form** so the category stops growing. Out of scope for n8n; named because this API is
what made it visible for the first time.

Answer: A (recommended option)

**Q26 · Key rotation** (§4.7). One key, no version, no overlap window, so rotating it is a hard cutover —
n8n's credential must change in the same minute as the App Service setting. 🟢 **Recommended: make
`AgentApiOptions.ApiKey` a string array and have `AgentApiKeyFilter` accept any member.** It is a small
portal change, it is the only change that also solves per-consumer keys later
(`CoreFlow.md` §13.7 recommends those first if a second consumer appears), and doing it before the first
rotation is much cheaper than doing it during one. **Alternative:** accept the hard cutover and write the
runbook now.

Answer: A (recommended option)

**Q27 · `AGENT_SERVICE` is a real ADMIN account** — `AgentApiPlan.md` open item 1, never answered, and the
only item in this section that is about the portal's own security rather than the agent's behaviour. It is
seeded with `User_Type = 2`, and `spUsers_ValidateLogin` selects any row by username, so **if its password
were ever learned or reset it would be a working portal administrator.** The password is a discarded random
secret, so this is theoretical. 🟢 **Recommended: take the cheap hardening** — re-seed it with a `User_Type`
no policy admits (all five require `1`, `2` or `3`), so even a successful login lands on a principal every
page refuses. It costs one digit and one line in `Seed_Users.sql`; the only cost is that the account shows
an unknown type on any screen that maps the integer to a name, and no screen lists it today.

Answer: A (recommended option)
---

### 🔴 What is NOT in this list, and is deliberately out of scope

Say so now if you disagree — each of these is a thing a reader might expect and will not find anywhere in
§1–§11:

- **Appointment reminders.** Nothing messages a patient the day before. The portal has no reminder concept
  and this agent does not add one.
- **No-shows.** Marking `Not Attended` is staff work in the portal, and §3.6 means it frees no hour.
- **Rescheduling and cancellation.** 🔴 There is no cancellation in nucentra (§3.6) and the agent has no
  write except a new booking. **Template 6 currently ends *"Reply CHANGE if you need to reschedule"* — a
  promise nothing in this system can keep.** 🟢 **Recommended: leave the line in template 6 and route
  `CHANGE` straight to escalation (Q9's mechanism), so a human reschedules it in the portal.** The
  alternative is to remove the line before §5.10 submits the template — cleaner, and it leaves a patient
  who needs to change an appointment with no obvious way to say so.
- **Anything after the assessment** — colonoscopy, follow-up, surveillance booking. Decision #4 and §3.5.
- **Patient registration.** The agent cannot create a patient; an unknown number escalates (§7.6).
- **Document access.** No endpoint in the Agent API touches `dbo.PatientDocument` (`CoreFlow.md` §13.7).
