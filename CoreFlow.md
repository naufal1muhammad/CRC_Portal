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

> *Grows by appending one `###` sub-section per feature area, across Prompts 1 and 3–9. Prompts 3–9 add
> theirs below; nothing already here is rewritten.*

### 3.1 Reference data (`dbo.LU_*`)

Twelve tables, **fourteen** procedures — `LU_LOCATION` has three, one per level of its tree. Every one is a
plain unfiltered read: **no `LU_*` table has an `IsActive`, `IsDeleted` or `SortOrder` column except
`LU_LOCATION`**, so a row that exists is a row the dropdown shows, and the only way to retire a value is to
delete it (which would orphan every business row still holding the code, since none of these are enforced by
a foreign key either).

| Table | PK column | Type | Display column | Procedure | Rows (local seed) |
|---|---|---|---|---|---|
| `LU_DISCHARGETYPE` | `DischargeType_ID` | `VARCHAR(100)` | `DischargeType_Name` | `spLU_DischargeType_List` | 4 |
| `LU_MARITALSTATUS` | `MaritalStatus_ID` | `VARCHAR(100)` | `MaritalStatus_Name` | `spLU_MaritalStatus_List` | 3 |
| `LU_OCCUPATION` | `Occupation_ID` | `VARCHAR(100)` | `Occupation_Name` | `spLU_Occupation_List` | 8 |
| `LU_ORGANIZATION` | `Organization_ID` | `VARCHAR(100)` | `Organization_Name` | `spLU_ORGANIZATION_List` | 6 |
| `LU_PATDOCUMENTTYPE` | `PatientDocumentType_ID` | `VARCHAR(100)` | `PatientDocumentType_Name` | `spLU_PatientDocumentType_List` | 13 |
| `LU_PJ_APP_TYPE` | `PjAppType_ID` | `VARCHAR(100)` | `PjAppType_Name` | `spLU_PJ_AppType_List` | 4 |
| `LU_RACE` | `Race_ID` | `VARCHAR(100)` | `Race_Name` | `spLU_Race_List` | 11 |
| `LU_RELIGION` | `Religion_ID` | `VARCHAR(100)` | `Religion_Name` | `spLU_Religion_List` | 6 |
| `LU_SOURCE` | `Source_ID` | `VARCHAR(100)` | `Source_Name` | `spLU_Source_List` | 9 |
| `LU_STAFFDOCUMENTTYPE` | `StaffDocumentType_ID` | `VARCHAR(100)` | `StaffDocumentType_Name` | `spLU_STAFFDOCUMENTTYPE_List` | 8 |
| `LU_STAFFTYPE` | `StaffType_ID` | `VARCHAR(100)` | `StaffType_Name` | `spLU_STAFFTYPE_List` | 5 |
| `LU_LOCATION` | `LocationId` | **`INT IDENTITY(1,1)`** | `Name` | `spLU_LOCATION_ListStates` / `…ListCityByState` / `…ListPostcodesByCity` | 16 states, 442 cities, 2,784 postcodes |

Both columns of all eleven code tables are `NOT NULL`. `LU_LOCATION` is `LocationId INT NOT NULL`,
`LocationType TINYINT NOT NULL` (1 = state, 2 = city, 3 = postcode), `ParentId INT NULL` (self-referencing
FK — the only foreign key in the reference data), `Name NVARCHAR(150) NOT NULL`, `SortOrder INT NULL`.

**KEY OBSERVATION — nucentra's lookup keys are `VARCHAR` codes, and `LU_LOCATION` is the exception.**
Eleven of the twelve key on `VARCHAR(100)`, seeded with two-character zero-padded codes (`"01"`, `"02"`, …).
`LU_STAFFTYPE` is the outlier *within* the outlier: three-letter mnemonics — `ANE`, `END`, `ENT`, `GAS`,
`NUR`. Every column that references one is `VARCHAR(100)` as well (`PatientBasic.Race_ID`,
`Staff.Staff_Type`, `Branch.Organization_ID`, …), and none of those references is a foreign key. **Parsing a
lookup id to an `int` appears to work and silently loses the leading zero** — `"01"` is not `1` — which is
why `CRC.Data/Models/LookupItem.Id` is a `string`. `LU_LOCATION` alone is an `INT IDENTITY`, which is why
the three location procedures get their own model, `LocationLookupItem`, and why `stateId`, `cityId` and
`postcodeId` are JSON **numbers** everywhere in the portal while every other lookup id is a JSON string.

**THE SURPRISE, and the one that cost Prompt 1 the most: no two of the eleven code procedures name their
columns the same way.** Each returns `{{Table}_ID, {Table}_Name}` — `Race_ID`/`Race_Name`,
`Source_ID`/`Source_Name`, `PjAppType_ID`/`PjAppType_Name` — so **Dapper, which maps by column name, maps
`LookupItem` to none of them**. `QueryAsync<LookupItem>("dbo.spLU_Race_List")` compiles, runs, returns the
right *number* of rows, and leaves `Id` and `Name` empty on every one, with no exception and nothing in a
log. `SqlData.QueryLookupAsync` therefore reads those two columns **by ordinal** — column 0 is the code,
column 1 is the display name — in one helper, documented at the point where the assumption lives. It is the
only ordinal read in the data layer. The runtime shape was verified against the deployed database with
`sys.dm_exec_describe_first_result_set`, not just against the `.sql` files: all eleven, two columns,
`varchar(100)`, code first.

Two smaller findings:

- **Ordering is part of each procedure's contract.** Ten of the eleven `ORDER BY` the display *name*.
  `spLU_PJ_AppType_List` orders by **ID**, because `01 PATIENT ASSESSMENT → 02 COLONOSCOPY → 03 FOLLOW UP →
  04 SURVEILLANCE` is clinical sequence and alphabetical order would scramble it into COLONOSCOPY first. A
  caller must not re-sort.
- The three `LU_LOCATION` procedures order by `COALESCE(SortOrder, 2147483647)` then `Name`, so curated rows
  float above an alphabetical tail. `spLU_LOCATION_ListStates` does **not** select `ParentId` (a state has
  no parent); the other two do. An unknown parent id is **not an error** — the procedure returns an empty
  set, and the caller decides what "no cities" means.

### 3.2 `dbo.Branch`

An organization's physical site. Staff are based at one; appointments are booked into one.

| Column | Type | Null | Notes |
|---|---|---|---|
| `Branch_ID` | `VARCHAR(100)` | NOT NULL | PK, **generated by `spBranch_Insert`** — see below |
| `Branch_Name` | `VARCHAR(100)` | NOT NULL | |
| `Branch_Location` | `VARCHAR(200)` | NOT NULL | free text; the insert/update procedures declare their `@Branch_Location` as `VARCHAR(100)`, so a location over 100 characters is **silently truncated on the way in** |
| `Branch_State` | `VARCHAR(100)` | NOT NULL | the state's **`Name`**, not its `LocationId` |
| `Branch_Status` | `BIT` | NOT NULL | active / inactive |
| `Organization_ID` | `VARCHAR(100)` | **NULL** | `LU_ORGANIZATION.Organization_ID`, by convention only |
| `Organization_Name` | `VARCHAR(100)` | **NULL** | denormalized copy of the name |

**No foreign keys, in either direction.** `Organization_ID` is not constrained to `LU_ORGANIZATION` and
`Branch_State` is not constrained to `LU_LOCATION`; nothing references `Branch_ID` either — `Staff.Staff_Base`
and `PatientAppointment.Branch_ID` are plain `VARCHAR(100)` columns holding one. Deleting a branch that staff
are based at, or that appointments are booked into, succeeds and orphans them.

**The two nullable organization columns are a schema/procedure mismatch worth knowing about.** They are
`NULL`-able, yet `spBranch_Insert` refuses a blank `@Organization_ID` outright with `RAISERROR`. The only
supported write path cannot produce the state the schema permits — but a row inserted by hand or by an older
build can, and the endpoints must keep coercing those nulls to `""` (see §4.1). `Organization_Name` being
stored on the branch at all is a denormalization: rename an organization in `LU_ORGANIZATION` and every
branch keeps the old name until someone re-saves it.

**How a `Branch_ID` is generated** (read `spBranch_Insert`): it is composed, not an identity.

```
Branch_ID  =  {Organization_ID, '*' stripped}  +  {state LocationId, 4 digits, zero-padded}  +  {sequence, 3 digits}
                            "02"                              "2367"  (SELANGOR)                      "002"
           →  "022367002"
```

The state segment is a genuine join: the procedure looks `@Branch_State` up **by `Name`** against
`LU_LOCATION` where `LocationType = 1`, and `RAISERROR`s if it finds nothing — which is why a state is stored
as text on the branch but still has to exist in the location tree. Three things about the sequence are worth
stating plainly, because they are not what the shape suggests:

1. **It is global, not per prefix.** `MAX(TRY_CAST(RIGHT(Branch_ID, 3) AS INT))` scans the *whole* table, so
   the third branch overall is `…003` no matter which organization or state it belongs to.
2. **It reuses numbers.** The `MAX` is over surviving rows only, so deleting the newest branch makes the next
   insert re-issue its number — and if the deleted branch had a different organization or state, the new id
   is genuinely new. If it had the same, the insert fails on the primary key. Empty table → `…001`.
3. It caps at 999 (`VARCHAR(3)`), and `RIGHT('000' + …, 3)` would wrap a four-digit sequence back to its last
   three digits rather than failing.

---

## 4. Pages, endpoints, policies

> *Grows by appending one `###` sub-section per feature area, across Prompts 1 and 3–9. Prompts 3–9 add
> theirs below; nothing already here is rewritten.*

### 4.1 Branch (Admin > Branch)

`CRC.Web/Controllers/Branch/BranchController.cs` — **`[Authorize(Policy = "SuperUserOnly")]` on the class**,
so every action requires `UserType = 1`. There is no per-action policy and no `[AllowAnonymous]`.
View: `Views/Branch/Index.cshtml`; script: `wwwroot/js/branch/index.js`. Antiforgery is global, so both
POSTs need the `X-CSRF-TOKEN` header (§0). Seven actions:

| Verb | Route | Returns |
|---|---|---|
| GET | `/Branch/Index` | the page. **`/Branch` alone 404s** — the default route is `{controller=Account}/{action=Login}/{id?}`, so the action segment is not optional for anything but the login page |
| GET | `/Branch/GetBranches` | a **bare JSON array** — no envelope |
| GET | `/Branch/GetBranch?branchId=` | `{ success, data }` or `{ success = false, message }` |
| POST | `/Branch/SaveBranch` | `{ success, message, branchId }` |
| POST | `/Branch/DeleteBranch` | `{ success, message }` |
| GET | `/Branch/GetStates` | a bare JSON array |
| GET | `/Branch/GetOrganizations` | a bare JSON array |

The exact shapes, which are the contract `wwwroot/js/branch/index.js` reads:

```jsonc
// GET /Branch/GetBranches                                   → 200, bare array
[{ "branchId": "022367002", "name": "…", "location": "…", "state": "SELANGOR",
   "status": true, "organizationId": "02", "organizationName": "MINISTRY OF HEALTH" }]

// GET /Branch/GetBranch?branchId=022367002                  → 200
{ "success": true, "data": { …the same seven camelCase fields… } }
{ "success": false, "message": "Branch not found." }          // unknown id — 200, not 404
// 400 { "success": false, "message": "Branch ID is required." }   // blank/missing branchId

// POST /Branch/SaveBranch  { isNew, branchId, name, location, state, status,
//                            organizationId, organizationName }
{ "success": true,  "message": "Branch created successfully.", "branchId": "022367002" }
{ "success": true,  "message": "Branch updated successfully.", "branchId": "022367002" }
{ "success": false, "message": "Invalid request." }
{ "success": false, "message": "Please fill in branch name, state and organization." }
{ "success": false, "message": "Branch ID is required for update." }
{ "success": false, "message": "Error saving branch.", "correlationId": "…" }   // ErrorResponse.ForUser

// POST /Branch/DeleteBranch  { branchId }
{ "success": true,  "message": "Branch deleted successfully." }
{ "success": false, "message": "An unexpected error occurred." }
// 400 { "success": false, "message": "Branch ID is required." }

// GET /Branch/GetStates                                     → 200, bare array
[{ "stateId": 2367, "stateName": "SELANGOR" }]               // stateId is a NUMBER

// GET /Branch/GetOrganizations                              → 200, bare array
[{ "organizationId": "01", "organizationName": "NATIONAL CANCER SOCIETY MALAYSIA" }]
```

Four behaviours that look like bugs, are not, and must be preserved:

- **`status` is coerced from null to `false`.** `Branch_Status` is `BIT NOT NULL`, so the coercion can never
  fire — but the model types it `bool?` anyway, because Dapper *throws* mapping a NULL onto a non-nullable
  `bool` and a defensive `false` must not become a 500.
- **`organizationId` and `organizationName` are coerced from null to `""`.** This one is live: those columns
  really are nullable (§3.2), and the `DataTable` code returned `""` for them because `DBNull.ToString()` is
  `""`. Without `?? string.Empty` the Dapper version serializes `null` and the table renders "null". This is
  the single mapping that a before/after JSON diff caught in Prompt 1, and only because the diff included a
  row with a null organization.
- **A missing branch is `200 OK` with `success = false`**, not a 404. A *blank* `branchId` is a 400. Two
  different failures, two different status codes, both in the same action.
- **`DeleteBranch` swallows the exception without logging it** — `catch (Exception)` with no `_logger` call,
  unlike `SaveBranch` — so a delete that fails is invisible outside the database. Left exactly as found;
  Prompt 9 owns the logging sweep.

### 4.2 My Profile (Staff)

`CRC.Web/Controllers/MyProfileStaff/MyProfileStaffController.cs` —
**`[Authorize(Policy = "AdminOrSuperOrStaff")]` on the class** (`UserType` 1, 2 or 3). Read-only page for the
logged-in staff member; view `Views/MyProfileStaff/Index.cshtml`, script `wwwroot/js/myprofileStaff/`. Two
actions, one of them data:

| Verb | Route | Returns |
|---|---|---|
| GET | `/MyProfileStaff/Index` | the page, with `ViewData["StaffId"]` from the caller's own `StaffId` claim |
| GET | `/MyProfileStaff/GetLocationNames?stateId=&cityId=&postcodeId=` | `{ success, stateName, cityName, postcodeName }` |

```jsonc
// GET /MyProfileStaff/GetLocationNames?stateId=1&cityId=2&postcodeId=3
{ "success": true, "stateName": "JOHOR", "cityName": "AYER BALOI", "postcodeName": "82100" }
{ "success": true, "stateName": "JOHOR", "cityName": "", "postcodeName": "" }   // ids that match nothing
{ "success": false, "message": "Error loading location names." }               // any exception
```

**It resolves three names by fetching three whole levels of the tree** — up to 2,784 postcode rows to find
one — because there is no `spLU_LOCATION_GetById`. That is what the page did before the Dapper layer and it
is unchanged; adding the procedure is a later prompt's decision, not a mid-migration one. The cascade is
strict: a city is only looked up if a state id was supplied, a postcode only if a city id was. An id that
matches nothing yields `""` rather than an error, and `success` stays `true` — the page shows a blank field.
The `StaffId` claim never touches this endpoint: the ids arrive as query parameters, so **any authenticated
user can resolve any location name**, which is harmless for public geography and is why the page is allowed
to skip the broader admin lookups.

---

## 5. Stored procedures

> *Grows by appending one `###` sub-section per feature area, across Prompts 1 and 3–9. Prompts 3–9 add
> theirs below; nothing already here is rewritten.*

### 5.1 Lookups — `CRC.Database/Stored Procedures/LU_*/` (14)

**None of the fourteen declares `@User_ID`**, none writes to `dbo.AuditTrails`, and none takes a filter other
than a parent id. All are `SET NOCOUNT ON` and a single `SELECT`.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spLU_DischargeType_List` | — | `DischargeType_ID, DischargeType_Name` — by name | `GetDischargeTypesAsync` | no |
| `spLU_MaritalStatus_List` | — | `MaritalStatus_ID, MaritalStatus_Name` — by name | `GetMaritalStatusesAsync` | no |
| `spLU_Occupation_List` | — | `Occupation_ID, Occupation_Name` — by name | `GetOccupationsAsync` | no |
| `spLU_ORGANIZATION_List` | — | `Organization_ID, Organization_Name` — by name | `GetOrganizationsAsync` | no |
| `spLU_PatientDocumentType_List` | — | `PatientDocumentType_ID, PatientDocumentType_Name` — by name | `GetPatientDocumentTypesAsync` | no |
| `spLU_PJ_AppType_List` | — | `PjAppType_ID, PjAppType_Name` — **by ID** | `GetJourneyAppointmentTypesAsync` | no |
| `spLU_Race_List` | — | `Race_ID, Race_Name` — by name | `GetRacesAsync` | no |
| `spLU_Religion_List` | — | `Religion_ID, Religion_Name` — by name | `GetReligionsAsync` | no |
| `spLU_Source_List` | — | `Source_ID, Source_Name` — by name | `GetSourcesAsync` | no |
| `spLU_STAFFDOCUMENTTYPE_List` | — | `StaffDocumentType_ID, StaffDocumentType_Name` — by name | `GetStaffDocumentTypesAsync` | no |
| `spLU_STAFFTYPE_List` | — | `StaffType_ID, StaffType_Name` — by name | `GetStaffTypesAsync` | no |
| `spLU_LOCATION_ListStates` | — | `LocationId, Name, SortOrder` where `LocationType = 1` | `GetStatesAsync` | no |
| `spLU_LOCATION_ListCityByState` | `@StateId INT` (required) | `LocationId, ParentId, Name, SortOrder` where `LocationType = 2` | `GetCitiesByStateAsync` | no |
| `spLU_LOCATION_ListPostcodesByCity` | `@CityId INT` (required) | `LocationId, ParentId, Name, SortOrder` where `LocationType = 3` | `GetPostcodesByCityAsync` | no |

The eleven code procedures return **`LookupItem`** (mapped by ordinal — see §3.1); the three location
procedures return **`LocationLookupItem`** (mapped by name). The two location procedures with a parameter
return an **empty set**, not an error, for a parent id that matches nothing.

### 5.2 Branch — `CRC.Database/Stored Procedures/Branch/` (6)

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spBranch_ListAll` | — | all 7 `Branch` columns, ordered by `Branch_Name` | `GetAllBranchesAsync` → `List<BranchDetail>` | no |
| `spBranch_GetById` | `@Branch_ID VARCHAR(100)` | `SELECT TOP 1` — the same 7 columns; **empty set** when the id is unknown | `GetBranchByIdAsync` → `BranchDetail?` | no |
| `spBranch_ListActive` | — | `Branch_ID, Branch_Name, Branch_State` where `Branch_Status = 1` | `GetActiveBranchesAsync` → `List<BranchOption>` | no |
| `spBranch_Insert` | `@Branch_Name`, `@Branch_Location`, `@Branch_State`, `@Branch_Status`, `@Organization_ID`, `@Organization_Name`, `@User_ID` | `SELECT @Branch_ID AS NewBranch_ID` — **one row, `VARCHAR(100)`** | `CreateBranchAsync` → `string` | **`INT = NULL` — ACTOR** |
| `spBranch_Update` | the same seven plus `@Branch_ID` | nothing | `UpdateBranchAsync` | **`INT = NULL` — ACTOR** |
| `spBranch_Delete` | `@Branch_ID`, `@User_ID` | nothing | `DeleteBranchAsync` | **`INT = NULL` — ACTOR** |

The three writes each `INSERT` one `dbo.AuditTrails` row with `ISNULL(@User_ID, 0)`, action `INSERT` /
`UPDATE` / `DELETE`, category `Branch`, and a `CONCAT`ed summary naming the branch id. **That `ISNULL` is the
silent-failure surface of §0.1:** drop the parameter and the write still succeeds, with `User_Id = 0`.

Three asymmetries between the writes, all of them real and none of them obviously intended:

- **Only the insert validates.** It `RAISERROR`s (severity 16 → a `SqlException` in C#) on a blank
  `@Organization_ID`, on a blank `@Branch_State`, and when `@Branch_State` matches no `LocationType = 1` row
  in `LU_LOCATION` **by `Name`**. `spBranch_Update` re-writes all the same columns and checks **none** of it,
  so a branch can be updated into a state that does not exist.
- **The update and the delete are silent when the id matches nothing.** Both guard their audit `INSERT` with
  `IF @@ROWCOUNT > 0`, so a no-op writes no audit row — and neither returns a row count, so the caller cannot
  tell a real write from a missed one. Verified against the running site: `POST /Branch/DeleteBranch` with
  `branchId = "NOSUCHBRANCHEVER"` answers `"Branch deleted successfully."`, `POST /Branch/SaveBranch` with
  `isNew = false` and the same id answers `"Branch updated successfully."`, and `dbo.AuditTrails` gains
  nothing from either. Making that visible means returning `@@ROWCOUNT`, which is an additive `.sql` change
  and a later prompt's call.
- **The insert is the only one that returns anything**, which is why `CreateBranchAsync` is the only Branch
  write that is a `QuerySingleAsync` rather than an `ExecuteAsync`.

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
