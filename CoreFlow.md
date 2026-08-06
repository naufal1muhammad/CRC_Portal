# nucentra — CoreFlow: the CRC Portal, as built (Specification)

This document is the **single source of truth for what the CRC Portal is**: its domain, its data model,
its endpoints, its stored procedures, and the layer that connects them. It describes the system **as it is
built today** — not as it was, and not as it might be.

> **Audience:** anyone, human or AI agent, about to change this repo. Read the section that covers what you
> are touching before you touch it. Nothing here is a proposal and nothing here is a change log; where a
> piece is genuinely unfinished, it says so in those words.

> ## ⚠️ THE SECTION NUMBERS ARE LOAD-BEARING
>
> This document is written across the eleven prompts of `DapperLayerPlan.md`. Prompt 0 fixes all twelve
> headings **now**, before most of them have content, precisely so that later prompts can **fill sections
> in and never renumber them**. Code comments already cite `CoreFlow.md §n`, and every future one will.
>
> Sections **3, 4 and 5 grow by appending**: each prompt adds its own feature area under a new `###`
> sub-heading and never rewrites what an earlier prompt put there. A section marked
> *"not yet filled in"* is a promise about where something will go, not an invitation to renumber around
> it. If you need a thirteenth topic, add **§13**.

---

## 0. Conventions (do not re-derive — apply)

| Concern | Rule |
|---|---|
| **Layering** | `sp{Table}_{What}` (stored procedure) → `IDatabaseData` / `SqlData` (Dapper, one method per procedure) → `{Feature}Controller` (`[Authorize(Policy = "…")]`, returns `Ok(new { success, message, … })`) → `wwwroot/js/{area}/{page}.js` → `Views/{Area}/{Page}.cshtml`. **Never skip a layer.** A controller does not open a connection; a view does not fetch; a `.js` file does not know a procedure's name. |
| **Procedure naming** | `sp{Table}_{What}` — `spBranch_ListActive`, `spPatientAppointment_UpdateStatus`. Lookups keep their table's full name: `spLU_LOCATION_ListStates`. Each lives in a **per-feature subfolder** of `CRC.Database/Stored Procedures/` (`Branch/`, `PatientBasic/`, `LU_RACE/`, …). **Every `.sql` file must be registered in `CRC.Database/CRC.Database.sqlproj`** as `<Build Include="Stored Procedures\{Folder}\{File}.sql" />` — an unregistered file builds locally and is silently absent from the `.dacpac`. |
| **No inline SQL** | There is none in nucentra, anywhere, and none is to be added. Every database call is `commandType: CommandType.StoredProcedure` from `SqlData`. A new query is a new `.sql` file and a new interface method — not a string in C#. |
| **Namespaces** | **Block-scoped** — `namespace CRC.Data.Data { … }`. Every existing file in the solution does this. (The sibling HEART repo uses file-scoped namespaces; do not copy that when copying its shapes.) |
| **Controller responses** | `Ok(new { … })` with **camelCase** property names, because 59 JavaScript files read them by name. Two shapes are in use and both are deliberate: a **list** endpoint returns a bare JSON array (`Ok(list)` — e.g. `/Branch/GetBranches`); a **single-item read or any write** returns the envelope `{ success, message, … }`, with the payload under `data` for reads (`/Branch/GetBranch`) and named fields for writes (`{ success, message, branchId }`). **A JSON shape is a public contract.** Do not rename a property, reorder nothing that a `.js` file indexes, and do not return a Dapper model directly — map it into the object the endpoint already returns. |
| **Authorization** | `builder.Services.AddControllersWithViews` installs a **global `AuthorizeFilter`**, so every action requires authentication unless it carries `[AllowAnonymous]`. On top of that, every controller or action states its policy explicitly: `[Authorize(Policy = "…")]`. The five policies are claim checks on `UserType` — `SuperUserOnly` (1), `AdminOnly` (2), `StaffOnly` (3), `AdminOrSuper` (1,2), `AdminOrSuperOrStaff` (1,2,3). See §2. |
| **Antiforgery** | Applied **globally** by `AutoValidateAntiforgeryTokenAttribute` in `Program.cs` — every non-GET action is validated whether or not it says so. The header name is **`X-CSRF-TOKEN`** and the cookie is `__Host-CSRF`, `SameSite=Strict`, `SecurePolicy=Always`. **The `__Host-` prefix requires HTTPS**, which is why local testing must use the `https` launch profile (`https://localhost:7276`), not `http`. |
| **Errors** | Catch, log, return. `_logger.LogError(ex, "…", args)` carries the operational detail; the caller gets `Ok(ErrorResponse.ForUser(HttpContext, "…"))`, which is `{ success = false, message, correlationId }`. The correlation id (stamped by `CorrelationIdMiddleware`) is what ties a user's complaint to a line in `Logs/app-*.log`. **Never return an exception message to the browser.** |
| **Logging** | Two channels, two files, one Serilog pipeline. `AuditLog.*` writes the security channel (`Logs/audit-*.log`, kept 365 days); `ILogger` writes the operational channel (`Logs/app-*.log`, kept 31 days). They are split by the presence of an `AuditChannel` property. A third, separate audit trail lives **in the database** — `dbo.AuditTrails`, written by the stored procedures themselves. See §9. |
| **Ids** | Most business keys are **`VARCHAR(100)`, not identities**: `Patient_ID`, `Staff_ID`, `Branch_ID` and every `LU_*` code are strings, generated inside the insert procedure. `PatientJourney_ID`, `PatientAppointment_ID`, `StaffSlot_ID`, `User_ID` and `LU_LOCATION.LocationId` are `INT IDENTITY`. Do not assume a key is numeric because it looks numeric — `"01"` is not `1`. |

### 0.1 🔴 The `@User_ID` rule — the one thing here that fails silently

**24 stored procedures declare a parameter called `@User_ID`, and it means two different things.** The tell
is the parameter's default. Getting it wrong breaks no build, fails no page, and corrupts the audit trail
or acts on the wrong account.

Historically nobody had to know this. `DatabaseHelper` queried `sys.parameters` before every command, asked
*"does this procedure declare `@User_ID`?"*, and appended the caller's `ClaimTypes.NameIdentifier` value if
it did. That is how `dbo.AuditTrails` learned who did what, with no controller ever passing an actor.
**Dapper has no such hook** — it sends the anonymous parameter object's properties and nothing else — so
`SqlData` passes the value explicitly, per call, and the distinction below became something every author
must hold in their head.

**`@User_ID INT = NULL` — THE ACTOR (19 procedures).** Who performed the write, for the `dbo.AuditTrails`
row the procedure inserts. It is not a business argument, so it **never appears in an `IDatabaseData`
method signature**; `SqlData` supplies it from `DatabaseHelper.CurrentUserId`.

```
spBranch_Insert              spPatientAppointment_Insert       spStaff_Insert
spBranch_Update              spPatientAppointment_Update       spStaff_Update
spBranch_Delete              spPatientAppointment_Delete       spStaff_Delete
spPatientBasic_Insert        spPatientAppointment_UpdateStatus spStaffDocument_Insert
spPatientBasic_Update        spPatientDocument_Insert          spStaffDocument_Delete
spPatient_DeleteCascade      spPatientDocument_Delete          spStaffSlots_CreateRange
                                                               spStaffSlots_Delete
```

**Because all nineteen declare a default, omitting the parameter does not throw.** It writes
`AuditTrails.User_Id = 0` and the audit trail quietly stops naming anyone — until the day somebody needs it.

**`@User_ID INT` (no default) — A TARGET USER ROW (5 procedures).** Which user row the procedure operates
*on*. This **is** an ordinary argument, it **does** appear in the method signature, and it has nothing to do
with who is logged in.

```
spUsers_GetById        spUsers_Unlock          spUsers_UpdatePassword
spUsers_ResetFailedLogins                      spUsers_UpdateLastLogin
```

`spUsers_Unlock` is the one to stare at: its `@User_ID` is **the locked-out account being unlocked**, by a
SUPERUSER, on somebody else's behalf. Auto-filling it from the caller's claim would unlock the
administrator's own account, leave the locked-out user locked, and report success.

That asymmetry is why the injection is **not** hidden inside a generic helper. Each of the 19 actor calls in
`SqlData` writes `User_ID = _databaseHelper.CurrentUserId` in the open, with a comment saying it is the
actor, so a reader can see which of the two kinds every call is making.

> `spPatientDocument_GetById` **looks** like it declares `@User_ID` and does not. Its header comment reads
> *"Read-only: no `@User_ID` and no audit row"*, and a naïve `grep "@User_ID"` matches the comment. Trust
> the two lists above.

Every change that touches an actor procedure must **check the audit row by hand** afterwards:

```bash
sqlcmd -S localhost -d CRC_DB -E -C -Q "SELECT TOP 5 AuditTrail_Id, User_Id, AuditTrail_Action, AuditTrail_Category, AuditTrail_Summary FROM dbo.AuditTrails ORDER BY AuditTrail_Id DESC"
```

`User_Id` must be the logged-in user's id. `0` or `NULL` means the parameter was dropped.

---

## 1. Domain in one paragraph

nucentra runs a **colorectal-cancer screening programme**. A person reaches the centre through one of nine
routes (`LU_SOURCE` — walk-in, GP, hospital referral, corporate, online) and is registered as a
**`PatientBasic`** row: demographics, residential address resolved against the state → city → postcode tree
in `LU_LOCATION`, an emergency contact, and an **iFOBT** result — the immunochemical faecal occult blood
test that the whole programme turns on. A positive iFOBT opens a **patient journey**: a sequence of
`PatientJourney` rows, each stamped with a date, an owning staff member, and one of the four types in
`LU_PJ_APP_TYPE` — **PATIENT ASSESSMENT**, **COLONOSCOPY**, **FOLLOW UP**, **SURVEILLANCE** — where the
first three each write a detail table of their own (`PatientAssessment` records risk factors, symptoms and
medical history from the iFOBT-positive date; `PatientColonoscopy` records bowel preparation and per-segment
findings from anus to caecum; `PatientFollowUp` records the HPE result and the discharge plan), and
SURVEILLANCE has no detail table because it is a scheduling outcome rather than a clinical record. Each step
is booked as a **`PatientAppointment`** — a patient, a staff member, a branch, a date and a strictly
on-the-hour one-hour window — which consumes one of that staff member's published **`StaffSlots`**, the
unique-per-staff-per-hour rows an administrator opens in advance. **`Staff`** are typed by `LU_STAFFTYPE`
(endoscopist, registered nurse, anaesthesia provider, endoscopy technician, gastrointestinal assistant) and
based at a **`Branch`**, which belongs to an organization in `LU_ORGANIZATION` and sits in a state.
Documents — identification, referral letters, iFOBT results, consent forms; CVs and certificates for staff —
attach to patients and to staff, catalogued by `LU_PATDOCUMENTTYPE` and `LU_STAFFDOCUMENTTYPE` and stored
in a private Azure Blob container. A patient leaves the programme by being **discharged**: setting
`DischargeType_ID` (`LU_DISCHARGETYPE` — NORMAL, BENIGN POLYPS, PRECANCEROUS POLYPS, CANCER) with a date and
remarks. A NULL discharge type is the definition of an active patient.

---

## 2. Who can see what — user types and policies

> *Written in Prompt 2 — not yet filled in.*

---

## 3. Data model

> *Written in Prompts 1 and 3–9, by appending one `###` sub-section per feature area — not yet filled in.*

---

## 4. Pages, endpoints, policies

> *Written in Prompts 1 and 3–9, by appending one `###` sub-section per feature area — not yet filled in.*

---

## 5. Stored procedures

> *Written in Prompts 1 and 3–9, by appending one `###` sub-section per feature area — not yet filled in.*

---

## 6. The data access layer

**Before this layer existed, the controller *was* the data-access layer.** Every endpoint built a
`SqlParameter[]` by hand, named a stored procedure in a string literal, and walked a `DataTable` by column
name. Nothing tied a C# call site to a `.sql` file except that literal; `row["Branch_Location"]` compiled
whether or not the procedure returned the column; and `spLU_LOCATION_ListStates` was wired up from scratch
in four different controllers, two of which had already drifted apart in how they handled a `DBNull` state
id. This section describes what replaces that.

### 6.1 The three files in `CRC.Data/Data/`

| File | Role |
|---|---|
| `DatabaseHelper.cs` | Owns the connection string (`ConnectionStrings:CRC_DB`) and the current user's id. **Moved here** from `CRC.Data/Database/`; its namespace is now `CRC.Data.Data`. |
| `IDatabaseData.cs` | The contract, and the **documentation** of the data layer. One method per stored procedure; a `//` comment above each saying what it is for and naming the procedure; methods grouped under `// ----- Area (where it is used) -----` banners. |
| `SqlData.cs` | The only implementation, and **the only place in the solution that names a stored procedure.** The mechanism only — read the interface to find out *what*, this file to find out *how*. |

`CRC.Data/Database/` still exists and still holds `Migrations/`, the seed CSVs. That is correct and not a
leftover: the repo has a `Data/` folder for code and a `Database/Migrations/` folder for data.

### 6.2 The rules

- **One method per stored procedure.** No method calls two — with exactly two named exceptions, the
  transactional units of work in Prompts 3 and 6, each commented as such where it is declared.
- **Named for what it does, not for the procedure.** `GetActiveBranchesAsync`, not `SpBranchListActiveAsync`.
- **Anonymous parameter objects**, property names matching the procedure's parameters without the `@`.
- **`commandType: CommandType.StoredProcedure` on every call.** No inline SQL, ever (§0).
- **Returns `List<T>`, `T?` or a scalar** — never `DataTable`, never `object`, never `dynamic`.
- **The Dapper verb follows what the procedure guarantees**, not what today's caller wants:
  `QuerySingleOrDefaultAsync` for a row that may not exist, `QuerySingleAsync` when exactly one is
  guaranteed, `QueryAsync` for a set, `ExecuteAsync` for a write with no result set, `QueryMultipleAsync`
  for several result sets, read in the order the procedure emits them.
- **Banners and method order match between `IDatabaseData.cs` and `SqlData.cs`**, so the two files can be
  read side by side. Nothing enforces this; it is on the author.
- **`@User_ID` is decided per call, in the open** — see §0.1.

### 6.3 `CRC.Data/Models/`

POCOs for Dapper to map result sets onto — public properties, no logic, no attributes, one type per file,
named for the data (`BranchListItem`, `StaffDetail`, `PatientDocumentItem`) and not for the procedure.
Prompt 0 establishes the pattern with a single file, `LookupItem.cs`.

**`LookupItem.Id` is a `string`, and that is the schema, not a shortcut.** Eleven of nucentra's twelve
`LU_*` tables key on `VARCHAR(100)` — `LU_DISCHARGETYPE`, `LU_MARITALSTATUS`, `LU_OCCUPATION`,
`LU_ORGANIZATION`, `LU_PATDOCUMENTTYPE`, `LU_PJ_APP_TYPE`, `LU_RACE`, `LU_RELIGION`, `LU_SOURCE`,
`LU_STAFFDOCUMENTTYPE`, `LU_STAFFTYPE` — seeded with two-character zero-padded codes (`"01"`, `"02"`), with
`LU_STAFFTYPE` the outlier using three-letter mnemonics (`"ANE"`, `"END"`, `"NUR"`). Every column that
references one is `VARCHAR(100)` too. Parsing an id to an `int` would appear to work and would lose the
leading zero. **`LU_LOCATION` is the single exception**: `LocationId INT IDENTITY(1,1)`, display column
`Name`, so the three `spLU_LOCATION_*` procedures do not fit `LookupItem` — Prompt 1 decides what they get
instead, after reading all fourteen lookup procedures.

A model is never serialized straight to the browser. It is mapped into the camelCase anonymous object the
endpoint already returns (§0), because 59 JavaScript files depend on those shapes and this migration does
not touch any of them.

### 6.4 Registration

`CRC.Web/Program.cs`, immediately after the helper:

```csharp
builder.Services.AddScoped<CRC.Data.Data.DatabaseHelper>();
builder.Services.AddScoped<CRC.Data.Data.IDatabaseData, CRC.Data.Data.SqlData>();
```

**Scoped**, because `SqlData` resolves the current user's id per request for the audit-actor parameter — a
singleton would capture one request's `IHttpContextAccessor` state and stamp every later audit row with it.

### 6.5 `DatabaseHelper` currently has two surfaces, and one of them is dying

This is the state of a migration in progress, and it is deliberate:

| Surface | Used by | Fate |
|---|---|---|
| `ExecuteNonQueryAsync`, `ExecuteDataTableAsync`, `ExecuteDataSetAsync`, `CreateStoredProcedureCommandAsync`, and the `sys.parameters` `@User_ID` auto-injection behind them | the 16 controllers not yet migrated | **Deleted in Prompt 10**, whatever of it is dead by then |
| `CreateConnection()` and `CurrentUserId` | `SqlData` | Kept — this is the Dapper path |

Both live side by side until Prompt 10, and that is why nothing broke when the file moved. **`CurrentUserId`
is new in Prompt 0**: it exposes the previously private `GetCurrentUserId()` because the auto-injection the
old surface performs cannot be reproduced by Dapper, so `SqlData` must read the claim itself (§0.1).

Prompt 0 deliberately leaves `IDatabaseData` **empty of methods**. The layer is created, referenced and
registered before a single call moves into it, so that the file move and the DI change can be proved
harmless on their own — the one change in this whole plan with no behavioural surface at all.

---

## 7. The patient journey — the core feature

> *Written in Prompt 7 — not yet filled in.*

---

## 8. Documents

> *Written in Prompt 8 — not yet filled in. Until then, `DOCUMENTSTORAGE.md` is authoritative.*

---

## 9. Audit and logging

> *Written in Prompt 9 — not yet filled in.*

---

## 10. Folder structure / file map

> *Written in Prompt 10 — not yet filled in.*

---

## 11. End-of-feature checklist

> *Written in Prompt 10 — not yet filled in.*

---

## 12. Decisions locked

> *Written in Prompt 10 — not yet filled in. Until then, the four decisions in `DapperLayerPlan.md`'s
> "The four decisions locked before writing this plan" hold and are not to be re-opened.*
