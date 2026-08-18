# CRC Portal — Agent API Plan (the portal grows a machine-callable surface, and `CoreFlow.md` gets §13)

This file slices **§4 of `Nucentra_WhatsApp_Agent_Plan.md`** — *"PART A — The Agent API (portal code change)"* —
into an ordered series of **5 self-contained prompts**. Each prompt is meant to be pasted into a **fresh chat**
of an AI coding tool that has **no memory** of the earlier prompts. Run them **in order**; each one assumes
every earlier prompt is finished and builds on it.

> ## What this is, and what it is not
>
> **This is the portal half.** `Nucentra_WhatsApp_Agent_Plan.md` describes a WhatsApp agent built in n8n.
> Four of the things that agent needs **do not exist in the portal today**, and §3 of that plan proves it
> against the code: there is no machine authentication, no way to find iFOBT-positive patients over HTTP, no
> patient lookup by phone number, and no single call that answers *"who at this hospital is free this week?"*.
> **Nothing in n8n can run until those exist.** This plan builds them.
>
> **It is not the agent.** No workflow, no template, no system prompt, no n8n node. When these five prompts
> are done, the portal exposes eight authenticated endpoints and `curl` can drive every one of them — and the
> agent has not been started. That is the correct boundary, and it is why the owner asked for this half on its
> own.
>
> ```
> Nucentra_WhatsApp_Agent_Plan.md §4   ──▶   THIS PLAN   ──▶   CRC.Api + 5 procedures + CoreFlow.md §13
> Nucentra_WhatsApp_Agent_Plan.md §5-§10       (later)          the n8n build, once the API answers
> ```
>
> **Where this disagrees with `CoreFlow.md`, `CoreFlow.md` wins.** It is the specification of the portal as
> built. Where this disagrees with `Nucentra_WhatsApp_Agent_Plan.md` §4, **this file wins** — the four
> decisions in the next section were taken after that plan was written, by the owner, against the real code.

---

## What Part 4 actually asks for, precisely

Eight endpoints under `api/agent`, authenticated by a shared key in an `X-Agent-Key` header, returning
camelCase JSON in nucentra's existing envelope shape.

| # | Verb | Route | Backed by | New? |
|---|---|---|---|---|
| 1 | GET | `/api/agent/patients/queue` | `spAgentPatient_ListScreeningQueue` | 🆕 procedure |
| 2 | GET | `/api/agent/patients/by-phone?phone=` | `spAgentPatient_FindByPhone` | 🆕 procedure |
| 3 | GET | `/api/agent/patients/{patientId}` | `spPatientBasic_GetById` | existing |
| 4 | GET | `/api/agent/patients/{patientId}/appointments` | `spPatientAppointment_ListByPatient` | existing |
| 5 | GET | `/api/agent/branches` | `spBranch_ListActive` | existing |
| 6 | GET | `/api/agent/staff?branchId=` | `spAgentStaff_ListByBranch` | 🆕 procedure |
| 7 | GET | `/api/agent/slots/open?branchId=&fromDate=&toDate=&staffType=` | `spAgentSlots_FindOpenByBranch` | 🆕 procedure |
| 8 | POST | `/api/agent/appointments` | **`SaveAppointmentAsync`** | existing |

**Seven reads and one write.** The write reuses the portal's own booking transaction unchanged — the slot
lock, the availability check, the contiguity check, the slot assignment and the audit row all live inside
`SaveAppointmentAsync` (`CoreFlow.md` §6.7) and there is no correct way to reimplement them.

A **fifth** procedure that Part 4 does not name is added by this plan: `spAgentUsers_GetServiceAccount`.
Decision 3 below explains why it is not optional.

---

## What is being built (executive summary)

1. **`CRC.Api/`** — a new **class library** in the solution, `net10.0`, referencing `CRC.Data`. It holds the
   controller, the API-key filter, the options class, the request/response DTOs and its own two small
   infrastructure files. **`CRC.Web` references it and loads its controllers as an MVC application part.**
   One host, one deployment, one config file.

2. **Five stored procedures** in a new `CRC.Database/Stored Procedures/Agent/` folder, every one registered
   in `CRC.Database.sqlproj`.

3. **Five methods** on `IDatabaseData` / `SqlData`, five models in `CRC.Data/Models/`. **No new data-access
   path** — `SqlData` stays the only file in the solution that names a stored procedure.

4. **One seeded `dbo.Users` row** — `AGENT_SERVICE` — added to `Scripts/Seed_Users.sql` and resolved by
   username at request time, so the audit trail names the agent instead of nobody.

5. **`CoreFlow.md` §13** — a new section, written across the five prompts, plus four surgical amendments to
   §2.2, §10, §11 and §12 where this feature changes a statement that is already there.

**What is explicitly *not* being built:** no n8n workflow, no WhatsApp integration, no change to any existing
page, any existing endpoint's JSON, any `.js` file, any `.cshtml` file, or any existing stored procedure. No
second booking path. No test project. No Azure action of any kind — the owner does all Azure work by hand.

---

## The four decisions locked before writing this plan

These were answered by the owner and are settled. Every prompt below obeys them; **do not re-open them while
building.**

| # | Question | Decision |
|---|---|---|
| 1 | What kind of project is `CRC.Api`? | **A class library, loaded into `CRC.Web` as an MVC application part.** Not a second host and not a second App Service. One deployment, one `appsettings.json`, one Serilog pipeline, one set of Azure app settings. The cost is stated honestly below: `CoreFlow.md` §2.2's `AllowAnonymous` count goes from two to three. |
| 2 | Where do the new procedures get called from? | **`CRC.Data`, like everything else.** The five methods go on `IDatabaseData`/`SqlData` and the five row models in `CRC.Data/Models/`. `CRC.Api` holds the controller, the DTOs, the filter and the options — no SQL, no procedure name, no Dapper. `CoreFlow.md` §12 #2 stays true. |
| 3 | How does the API-key filter learn the agent's `User_ID`? | **Seed the row and look it up by username.** `AGENT_SERVICE` is seeded in `Scripts/Seed_Users.sql` guarded on `Username`, and a fifth procedure `spAgentUsers_GetServiceAccount` resolves its id per request. **No `Agent__ServiceUserId` config value** — the id differs between local `CRC_DB` and Azure SQL, and a stale one is a silent wrong-actor audit rather than an error. |
| 4 | The surveillance path (`Nucentra_WhatsApp_Agent_Plan.md` §3.5) | **Option C — propose only.** The agent never books a `04` SURVEILLANCE appointment; the coordinator opens the range and books by hand. **Endpoint 8 therefore accepts `pjAppTypeId` `"01"` and nothing else**, and rejects anything else with a typed reason. One constant, one comment, reversible in a line if the owner later moves to option A. |

### What decision 1 costs, said plainly

`CoreFlow.md` §2.2 currently reads:

> *"There are exactly two `[AllowAnonymous]` attributes in the entire web project… A `grep` for
> `AllowAnonymous` is a complete audit of the portal's public surface, and it returns two lines."*

**After this ships it returns three**, and the third is a controller that reads patient names, phone numbers,
screening results and clinician schedules. That sentence is load-bearing for anyone auditing the portal, so
Prompt 4 rewrites it rather than leaving it quietly false.

**`AgentApiKeyFilter` is the only thing standing in the gap.** The global `AuthorizeFilter` is switched off
for this controller on purpose, and if the filter is ever removed, mis-registered, or silently fails open,
`/api/agent/patients/queue` becomes an unauthenticated dump of every active patient in the programme. Prompt 1
exists to build and prove that filter **before a single patient-shaped endpoint exists**, and its verification
step is the most important one in this document.

---

## 🔴 The actor identity — read this before writing a single line

This is the trap `Nucentra_WhatsApp_Agent_Plan.md` §4.1 was written to prevent, and **this repository has
already fallen into it once.**

`SqlData` passes `@User_ID` explicitly to the 19 audit-actor procedures, taking it from
`DatabaseHelper.CurrentUserId`, which reads `HttpContext.User`'s `ClaimTypes.NameIdentifier`
(`CRC.Data/Data/DatabaseHelper.cs`, and `CoreFlow.md` §0.1).

**An API-key request arrives with no cookie and therefore no principal.** `CurrentUserId` returns `null`,
`spPatientAppointment_Insert` writes `ISNULL(@User_ID, 0)`, and **every appointment the agent books is audited
as user `0` — nobody.** No error. No failed request. A corrupt audit trail on a clinical system.

### It has already been written once, in this repo

The deleted `CRC.Api` (commit `291ab458`, removed in `a6c9d16e`) shipped this, verbatim:

```csharp
var claims = new List<Claim>
{
    // Keeps DatabaseHelper happy (it injects @User_ID if supported; missing claim => defaults to 0)
    new(ClaimTypes.NameIdentifier, "0"),
    new("ApiClient", "PublicRegistration")
};
```

That comment is wrong in the way that matters: `0` does not keep anything happy, it **is** the failure. Read
it before you write the new filter, and do not reach for the same shortcut.

### The fix, as decided

1. **`AGENT_SERVICE` is a real, seeded `dbo.Users` row** — `Username = 'AGENT_SERVICE'`, `User_Type = 2`,
   `Staff_ID = NULL`, `Password_Hash` a valid hash of a random secret that is generated once and **never
   written down**. It ships in `Scripts/Seed_Users.sql`, guarded on `Username` exactly like the bootstrap
   SUPERUSER, so it exists on every published database including a fresh one.

2. **The filter resolves its `User_ID` per request** by calling `spAgentUsers_GetServiceAccount` through
   `IDatabaseData`, and builds a `ClaimsPrincipal` carrying:
   - `ClaimTypes.NameIdentifier` = that row's `User_ID`, as a plain invariant-culture integer string
   - `ClaimTypes.Name` = `"AGENT_SERVICE"`
   - `"UserType"` = that row's `User_Type`, as a string

3. 🔴 **If the row is missing, the request fails.** `503`, an `_logger.LogError`, and no action executes.
   **Never fall through to a null actor.** A missing row means the database was published without the seed,
   and the only two outcomes available are "fail loudly now" and "corrupt the audit trail quietly forever".

4. **`spAgentUsers_GetServiceAccount` declares no `@User_ID` of its own.** It is a read, it writes no
   `dbo.AuditTrails` row, and it runs *before* the principal exists — so it could not have an actor even if it
   wanted one. Do not give it one.

### Why by-username and not a config setting

`Nucentra_WhatsApp_Agent_Plan.md` §4.5 proposes an `Agent__ServiceUserId` app setting. It was rejected for one
reason: `dbo.Users.User_ID` is `INT IDENTITY`, so the agent account's id on your local `CRC_DB` and its id on
Azure SQL **are different numbers**, and nothing checks that the setting matches the row. A wrong value does
not throw — it writes a different, real user's id into `dbo.AuditTrails` and reports success. `Username`
carries `UNIQUE INDEX IX_Users_Username`, so the lookup is an index seek and the answer cannot be ambiguous.

**No cache.** The lookup runs on every agent request. That is a single-row seek on a table with a handful of
rows, against a caller whose entire traffic is one daily sweep plus a conversation, and a cache here would be
a staleness bug waiting for the day somebody edits the row.

### The assertion that proves it

After the first booking through endpoint 8:

```bash
sqlcmd -S localhost -d CRC_DB -E -C -Q "SELECT TOP 5 a.AuditTrail_Id, a.User_Id, u.Username, a.AuditTrail_Action, a.AuditTrail_Category, a.AuditTrail_Summary FROM dbo.AuditTrails a LEFT JOIN dbo.Users u ON u.User_ID = a.User_Id ORDER BY a.AuditTrail_Id DESC"
```

`Username` must read `AGENT_SERVICE`. **`0` or an empty username means step 2 was not done**, and everything
else in Prompt 3 is worthless until it does.

---

## Why a class library, and what that actually means mechanically

`CRC.Api` is `Microsoft.NET.Sdk` — **not** `Microsoft.NET.Sdk.Web`. There is no `Program.cs`, no
`appsettings.json`, no `launchSettings.json`, no port. It is a library that happens to contain a controller.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\CRC.Data\CRC.Data.csproj" />
  </ItemGroup>
</Project>
```

**`CRC.Data` already does exactly this** — `FrameworkReference Include="Microsoft.AspNetCore.App"` on a plain
`Microsoft.NET.Sdk` project, because `DatabaseHelper` needs `IHttpContextAccessor`. There is precedent in this
repo; follow it.

**The dependency runs `CRC.Web → CRC.Api → CRC.Data`.** Three consequences, all load-bearing:

| | |
|---|---|
| 🔴 **`CRC.Api` must never reference `CRC.Web`** | It would be a cycle, and the build would say so. This is why `CRC.Api` gets its own two infrastructure files instead of using `CRC.Web/Infrastructure/AuditLog.cs` and `ErrorResponse.cs`. |
| **`CRC.Api` may reference `CRC.Data` freely** | `IDatabaseData`, the models, `AppointmentSaveInput`/`Result`/`Failure` — all available, all used. |
| **`CoreFlow.md` §12 #6 is untouched** | `CRC.Data` still references neither. The one-way rule was about the data layer, and it still holds. |

**Controllers in a referenced assembly are not discovered by accident.** `CRC.Web/Program.cs` says so
explicitly:

```csharp
builder.Services.AddControllersWithViews(options => { … })
    .AddApplicationPart(typeof(CRC.Api.Controllers.AgentApiController).Assembly);
```

Attribute-routed actions ride along with the existing `app.MapControllerRoute(…)` — that call creates the
controller endpoint data source, and attribute-routed actions are always part of it. There is nothing else to
add to the routing block. **Prove it with a real request anyway** (Prompt 1); if `/api/agent/branches` 404s,
add `app.MapControllers();` immediately above the existing `MapControllerRoute` and re-test.

### What comes free, and what does not

| Concern | Status under the class-library shape |
|---|---|
| **Serilog, both channels** | ✅ Free. `AuditLog` routes on `Log.ForContext("AuditChannel", true)` against the **static** `Serilog.Log.Logger`, which `CRC.Web/Program.cs` configures for the whole process. `CRC.Api/Infrastructure/AgentAuditLog.cs` does the same thing and lands in the same `Logs/audit-*.log`. |
| **Correlation id** | ✅ Free. `CorrelationIdMiddleware` runs for every request, and stores the id at `HttpContext.Items["CorrelationId"]`. `CRC.Api` reads that key directly — the string literal, not the `CRC.Web` constant. |
| **Connection string, DI, `IDatabaseData`** | ✅ Free. Same host, same container, same `appsettings.json`, same `ConnectionStrings:CRC_DB`. |
| **The global `AuthorizeFilter`** | ❌ Must be escaped with `[AllowAnonymous]`, and replaced by `AgentApiKeyFilter`. |
| **The global `AutoValidateAntiforgeryTokenAttribute`** | ❌ Must be escaped with `[IgnoreAntiforgeryToken]`, or **every POST to endpoint 8 returns `400`**. n8n has no cookie and cannot obtain a CSRF token. |
| **`ErrorResponse.ForUser`** | ❌ Lives in `CRC.Web`. `CRC.Api` gets `AgentErrorResponse`, the same three-property shape. |

### 🔴 The surprise worth writing down now

`CorrelationIdMiddleware` pushes its `UserName` Serilog property from `context.User?.Identity?.Name` **before
the endpoint runs** — which is before `AgentApiKeyFilter` has set the principal. So every line an agent request
writes to `Logs/app-*.log` and `Logs/audit-*.log` will read **`[User:anonymous]`**, while
`dbo.AuditTrails.User_Id` correctly names `AGENT_SERVICE`.

**Both are right.** The Serilog field reports who the request arrived as; the database column reports who the
write was performed by. Nobody should "fix" the audit trail by looking at the log channel and concluding the
actor is broken. This goes in `CoreFlow.md` §13 and it is the kind of sentence that section exists for. Every
`AgentAuditLog` message therefore names the actor explicitly in its own text rather than relying on the
enricher.

---

## House style for the new project

Copy nucentra's conventions exactly. `CoreFlow.md` §0 is the authority and none of it is relaxed here.

| Concern | Rule |
|---|---|
| **Namespaces** | **Block-scoped** — `namespace CRC.Api.Controllers { … }`. The deleted `CRC.Api` used file-scoped namespaces; that is the one thing about it not to copy. |
| **Response shape** | `Ok(new { success, … })`, **camelCase**, built by hand. A list read returns `{ success, data = [...] }`; a single read returns `{ success, data }`; the write returns `{ success, appointmentId }` or `{ success = false, reason, message }`. **Never serialize a `CRC.Data` model directly** (`CoreFlow.md` §12 #4). |
| **Request DTOs** | **Nested classes inside the controller**, like every other controller in the repo (`CoreFlow.md` §10, §11.3). Do not start a `CRC.Api/Models/` tree for them. |
| **No data annotations on the request DTO** | `[ApiController]` auto-`400`s on an invalid `ModelState` with a `ProblemDetails` body, which is not the envelope n8n branches on. With no validation attributes, `ModelState` stays valid and **every validation answer is the house envelope, written by hand in the action**. A malformed JSON body still yields the framework's `400`; that is correct and n8n reads it as "not success". |
| **Errors** | `catch (SqlException ex)` then `catch (Exception ex)`, both `_logger.LogError(ex, "…", args)`, both returning `Ok(AgentErrorResponse.ForUser(HttpContext, "…"))`. 🔴 **A new unlogged catch is a defect** (`CoreFlow.md` §11.3). Never return an exception message. |
| **No SQL** | Not a `SELECT`, not a procedure name, not a `SqlParameter`, not a `using Microsoft.Data.SqlClient`. `CRC.Api` calls `IDatabaseData` and nothing lower. |
| **Ids are strings** | `"01"` is not `1`. `pjAppTypeId` is a quoted string with a leading zero, and `Patient_ID` / `Staff_ID` / `Branch_ID` are `VARCHAR(100)`. |

### 🔴 What must never leave through this API

- **The full NRIC.** `spPatientBasic_GetById` selects `Patient_NRIC` in full. **Endpoint 3 projects
  `nricLast4` and never the column itself.** The agent confirms identity by asking the patient for four digits
  and comparing; it never states them, and it must never be *able* to state more.
- **Anything a document contains**, and any blob key or SAS URL. No endpoint here touches
  `dbo.PatientDocument` or `dbo.StaffDocument`.
- **A clinician's phone number to a patient.** Endpoints 6 and 7 return `Staff_Phone` because gate 1 needs it;
  the system prompt forbids relaying it. The API is not the enforcement point, but the comment above the
  projection should say who is.

---

## What this changes in `CoreFlow.md`

**§13 is new, and the section numbers stay put.** `CoreFlow.md` §10 already anticipates this: *"If you need a
thirteenth topic, add §13 — do not shuffle §10, §11 or §12 aside to make room."* Prompt 0 creates the skeleton;
each prompt fills its own slice while it is fresh; Prompt 4 closes it.

| § | Sub-section | Written by |
|---|---|---|
| **13** | 13.0 What the Agent API is, and what it is not | P0 |
| | 13.1 The five procedures | P0, table completed P2 |
| | 13.2 `CRC.Api` — the project, and why it is a library | P1 |
| | 13.3 🔴 Authentication and the service actor | P1 |
| | 13.4 The seven read endpoints, and their exact JSON | P2 |
| | 13.5 The write endpoint, and the typed failure reasons | P3 |
| | 13.6 Configuration, deployment and the platform lock-down | P4 |
| | 13.7 What is deliberately not here | P4 |

**Four existing sections are amended, all in Prompt 4, all surgically:**

| § | Amendment |
|---|---|
| **2.2** | The `AllowAnonymous` count goes **two → three**, with the third named and the filter named as what closes it. |
| **10** | `CRC.Api/` is added to the file map; the opening line changes from "three projects" to four, and the dependency arrow becomes `CRC.Web → CRC.Api → CRC.Data`. The `Stored Procedures/` inventory gains `Agent/ (5)` and the count moves 104 → 109. |
| **11** | §11.3 gains one line: a machine-callable endpoint has no cookie, so its actor comes from the service account, not `CurrentUserId`'s claim — pointing at §13.3. |
| **12** | A new locked decision recording that `CRC.Api` is a library and not a second host, and why; and an amendment to #2 confirming that `SqlData` is *still* the only file that names a procedure. |

**Nothing is renumbered. Nothing already written is deleted.** Where a sentence becomes false, it is rewritten
in place — §2.2's is the only one that does.

---

## How to use this plan

1. Work top to bottom. Open a **new chat** for each prompt and paste that prompt's **copy block** (the fenced
   `text` block) verbatim.
2. Every prompt re-orients the AI from scratch and tells it to read `CoreFlow.md` first. From Prompt 1 onwards
   §13 exists and carries some of that weight itself.
3. **Build gates. Two projects, two different builders:**

   ```bash
   dotnet build CRC.Web/CRC.Web.csproj
   ```

   ```bash
   "C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /t:Rebuild /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
   ```

   `dotnet build` **cannot** build the classic SSDT `.sqlproj`. The database build must report
   `Build succeeded.`, `0 Error(s)` and **exactly two** `SQL71502` warnings, both in
   `spStaffSlots_CreateRange.sql` at lines 46 and 52. 🔴 **Two, not three.** `CoreFlow.md` §12 #8 makes that
   count a tripwire, and this plan deliberately does not spend it — see Prompt 0's note on
   `spAgentPatient_FindByPhone`. **Warnings appear only on `/t:Rebuild`**; an incremental build prints none and
   proves nothing.
4. **Everything runs against your LOCAL `CRC_DB`** at `Server=localhost` — the connection string already in
   `CRC.Web/appsettings.json`. **No prompt connects to the Azure database and no prompt touches Azure at all.**
5. **The smoke-test harness is different from `DapperLayerPlan.md`'s, and simpler.** There is no cookie and no
   CSRF token — that is the entire point of the feature:

   ```powershell
   # 1. Start the site (https profile — the rest of the portal needs it, and so should you)
   dotnet run --project CRC.Web --launch-profile https
   # wait for https://localhost:7276

   # 2. The key comes from appsettings.Development.json — Agent:ApiKey
   $key = 'dev-only-agent-key-change-me'
   $h   = @{ 'X-Agent-Key' = $key }

   # 3. GET
   Invoke-RestMethod https://localhost:7276/api/agent/branches -Headers $h -SkipCertificateCheck

   # 4. POST — no X-CSRF-TOKEN, because [IgnoreAntiforgeryToken] is on the controller
   Invoke-RestMethod https://localhost:7276/api/agent/appointments -Method POST -Headers $h `
     -SkipCertificateCheck -ContentType 'application/json' -Body ($payload | ConvertTo-Json)

   # 5. THE NEGATIVE TESTS — run these every time, they are the ones that matter
   Invoke-WebRequest https://localhost:7276/api/agent/branches -SkipCertificateCheck `
     -SkipHttpErrorCheck | Select-Object StatusCode                      # expect 401
   Invoke-WebRequest https://localhost:7276/api/agent/branches -SkipCertificateCheck `
     -Headers @{ 'X-Agent-Key' = 'wrong' } -SkipHttpErrorCheck | Select-Object StatusCode   # expect 401
   ```

   🔴 **The 401s are not an afterthought.** A read endpoint that answers `200` without a key is a patient-data
   leak, and it looks exactly like a working endpoint from the happy path. Run both negative tests after every
   prompt from Prompt 1 onwards, not just the prompt that builds the filter.
6. **Every prompt that touches an ACTOR procedure must check the audit row by hand**, with the joined query in
   the actor-identity section above. `Username` must read `AGENT_SERVICE`.
7. **The app is never broken between prompts.** Every prompt leaves the solution compiling, every existing page
   working and every endpoint built so far still answering. Stopping after any prompt is safe — and stopping
   after Prompt 1 leaves a portal with exactly one new endpoint, which returns a list of branch names.
8. The last instruction in every prompt is **"mark this prompt complete in `AgentApiPlan.md`"** — the Progress
   Tracker box and the prompt's **Status** line get ticked.

---

## Progress Tracker

- [x] **Prompt 0** — The database: five procedures, the `.sqlproj`, the `AGENT_SERVICE` seed, `SEEDING.md`, and the `CoreFlow.md` §13 skeleton
- [x] **Prompt 1** — The project and the guard: `CRC.Api`, the application part, `AgentApiKeyFilter`, the service account, and endpoint 5
- [ ] **Prompt 2** — The six remaining reads: four new data-layer methods, four models, endpoints 1, 2, 3, 4, 6, 7
- [ ] **Prompt 3** — The write: endpoint 8, `SaveAppointmentAsync` reused, the typed failure reasons, and the audit assertion
- [ ] **Prompt 4** — Harden and hand off: `CoreFlow.md` §2.2 / §10 / §11 / §12, §13 finished, the smoke script, the Azure settings sheet

---

## Coverage map (prompt → files, procedures, `CoreFlow.md`)

| Prompt | Creates / edits | Procedures | Endpoints | `CoreFlow.md` |
|---|---|---|---|---|
| **0** | `Stored Procedures/Agent/*.sql` ×5, `CRC.Database.sqlproj`, `Scripts/Seed_Users.sql`, `SEEDING.md` | **5 new** | 0 | §13 skeleton + §13.0, §13.1 |
| **1** | `CRC.Api/` (project, controller, filter, options, 2 infra files), `CRC_Portal.slnx`, `CRC.Web.csproj`, `Program.cs`, `appsettings*.json`, `IDatabaseData`/`SqlData` +1, `Models/` +1 | 1 wired (`spAgentUsers_GetServiceAccount`) + 1 reused (`spBranch_ListActive`) | **5** | §13.2, §13.3 |
| **2** | `AgentApiController`, `IDatabaseData`/`SqlData` +4, `Models/` +4 | 4 wired + 2 reused | **1, 2, 3, 4, 6, 7** | §13.4, §13.1 completed |
| **3** | `AgentApiController` | 0 new — `SaveAppointmentAsync` reused whole | **8** | §13.5 |
| **4** | `CoreFlow.md`, `AgentApiPlan.md`, a smoke script | 0 | 0 — verification only | §2.2, §10, §11, §12, §13.6, §13.7 |

**Dependency order — strictly sequential:**

```
P0 ─→ P1 ─→ P2 ─→ P3 ─→ P4
 │     │     │     │     │
 └─────┴─────┴─────┴─────┴─→ CoreFlow.md §13
```

Every prompt from 1 onwards edits `IDatabaseData.cs`, `SqlData.cs`, `AgentApiController.cs` and `CoreFlow.md`;
running two in parallel means merging four files by hand. **P0 is the only prompt that changes nothing about
how the application behaves** — it adds procedures nothing calls yet and one user row nothing uses yet — so it
is also the cheapest place to stop and look around.

---

## The hard cases, and where they land

| # | What | Where | How this plan handles it |
|---|---|---|---|
| 1 | **The actor identity.** No cookie means no principal means `AuditTrails.User_Id = 0`, silently — and this repo has already shipped that bug once | **P0 + P1** | A seeded `AGENT_SERVICE` row, a fifth procedure resolving it by username, a filter that builds the principal before the action runs, and a `503` if the row is missing. Asserted in P3 against `dbo.AuditTrails` joined to `dbo.Users`. |
| 2 | **The digit-strip in `spAgentPatient_FindByPhone`.** Meta sends `60123456789`; the portal stores `0123456789` or `012-345 6789`. §4.2.2 of the source plan uses `master.dbo.spt_values` as a row generator, which **adds a third `SQL71502` warning** | **P0** | **Rejected.** `CoreFlow.md` §12 #8 makes "exactly two warnings" a tripwire and this plan will not spend it on a cosmetic. The parameter is normalised with a `PATINDEX`/`STUFF` loop over a ≤100-character string; the column is normalised with `TRANSLATE`. Match is on the **last 9 digits**. The column-side strip covers a **named** separator set, and P0 ships the query that finds any row it would miss. |
| 3 | **The booking write.** `SaveAppointmentAsync` owns a transaction, a slot lock, an availability check, a contiguity check and the slot assignment (`CoreFlow.md` §6.7) | **P3** | **Reused unchanged.** No second write path, no reimplementation, no "simplified" version. The controller builds an `AppointmentSaveInput`, calls the method, and maps `AppointmentSaveFailure` to `reason` **verbatim by enum name** so n8n can branch on `SlotTaken`. |
| 4 | **`SlotTaken` is a normal outcome, not an error.** Endpoint 7 reads slots outside any transaction; an administrator can take that hour a second later | **P3** | The response is `200 { success: false, reason: "SlotTaken", … }` — not a `4xx`. n8n's WF0 re-runs slot discovery on it. A `4xx` here would make an expected race look like a client bug. |
| 5 | **Two escapes from global filters** — `AuthorizeFilter` and `AutoValidateAntiforgeryTokenAttribute` | **P1** | `[AllowAnonymous]` + `[IgnoreAntiforgeryToken]` on the controller, each with a comment saying what it disables and what replaces it. P1's verification is the two 401 tests; forgetting `[IgnoreAntiforgeryToken]` shows up in P3 as a `400` on every POST. |
| 6 | **Option C's single appointment type** | **P3** | `pjAppTypeId` must equal `"01"`. A constant, a comment naming decision 4, and a typed refusal — not a silent acceptance of `"04"` that books an appointment nobody will ever be able to clinically record (`CoreFlow.md` §7.3). |

---

## Shared preamble (embedded in every prompt)

Every copy block tells the AI to:

- **Read `CoreFlow.md` first** — it is the specification of the portal as built, and §0, §0.1, §11 and §12 are
  the conventions this work must not break.
- **Never write inline SQL, and never name a stored procedure outside `CRC.Data/Data/SqlData.cs`.** A new query
  is a new `.sql` file, registered in the `.sqlproj`, plus one new `IDatabaseData` method (`CoreFlow.md` §12 #2).
- **`CRC.Api` never references `CRC.Web`.** It would be a cycle. Its two small infrastructure files exist for
  exactly this reason; do not "de-duplicate" them by adding a reference.
- **Do not change any existing endpoint's JSON, any route, any `[Authorize]` policy, any validation rule, or
  any user-facing message.** 59 JavaScript files read the existing shapes and no prompt in this plan edits a
  `.js` or `.cshtml` file.
- **Handle the actor explicitly.** An API-key request has no principal. The filter builds one from the seeded
  `AGENT_SERVICE` row before any action runs, and a missing row is a `503`, never a fall-through.
- **`.sql` edits are additive only** and confined to the files the prompt names. Every new `.sql` file is
  registered in `CRC.Database.sqlproj` as `<Build Include="…" />` with nothing reordered or reformatted.
- **Never touch Azure** — no resource, no app setting, no CLI, no Kudu, no FTPS. The owner does all Azure work
  by hand from `Nucentra_Azure_Deployment_Guide.md`. **Do not edit that guide**, and do not edit
  `DOCUMENTSTORAGE.md` or `DapperLayerPlan.md`.
- **Never put a real key in `appsettings.json`.** A development placeholder goes in
  `appsettings.Development.json`; the production key is an App Service app setting the owner sets by hand, the
  same rule `DocumentStorage` already follows.
- **Verify by running the site**, not by reading the diff. Build, run against local `CRC_DB`, drive the
  endpoints with `curl` or `Invoke-RestMethod`, and run the two 401 tests every time.
- **Write your `CoreFlow.md` §13 sub-sections from what you actually found**, not from what this plan predicted.
  If a procedure or a filter does something surprising, that surprise is the most valuable sentence in the
  document.
- **Use the right builder per project** — `dotnet build` for `CRC.Web`, MSBuild `/t:Rebuild` for the classic
  SSDT `CRC.Database.sqlproj` — and hold the baseline at **exactly two** `SQL71502` warnings.

---

# The Prompts

---

## Prompt 0 — The database, the service account, and the `CoreFlow.md` §13 skeleton

**Status:** ✅ Done
**Depends on:** the existing project only

> **What exists before this prompt:** 104 stored procedures in 30 per-feature folders under
> `CRC.Database/Stored Procedures/`. `Scripts/Seed_Users.sql` seeds one row, the bootstrap SUPERUSER.
> `CoreFlow.md` has twelve sections, all written. There is no `Agent/` folder, no agent account, and no §13.
> **Nothing in C# changes in this prompt** — it adds five procedures nothing calls yet and one user row nothing
> uses yet, which is why it is the safest place in this plan to stop and look around.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 10 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal. The web project is CRC.Web
(net10.0), the data project is CRC.Data (net10.0), and the database project is CRC.Database (classic SSDT
.sqlproj, MSBuild only).

WHY: nucentra is about to grow a small machine-callable API so an external WhatsApp agent (built separately,
in n8n) can read the screening queue and book assessment appointments. Four things that agent needs cannot be
answered by any existing stored procedure, and a fifth is needed so the agent's writes are attributable. This
is Prompt 0 of 5: it builds the DATABASE half only. NO C# CHANGES AT ALL.

READ FIRST — all of these, before writing anything:
  CoreFlow.md                       (§0 and §0.1 in full — the conventions and the @User_ID rule;
                                     §3.7 dbo.StaffSlots; §3.8 dbo.PatientBasic; §3.9 dbo.PatientAppointment;
                                     §3.10 dbo.PatientJourney; §5.1 for procedure-catalogue style;
                                     §11.1 the database checklist; §12 #8 and #9)
  AgentApiPlan.md                   (this plan — the "actor identity" section and "hard cases" #2 in full)
  Nucentra_WhatsApp_Agent_Plan.md   (§3 the constraints, and §4.2 the four procedure sketches. NOTE: §4.2.2's
                                     digit-strip is DELIBERATELY NOT USED — see step 3 below)
  SEEDING.md                        (all of it — you are editing it)
  CRC.Database/Scripts/Seed_Users.sql            (the pattern you are extending — read its header comment,
                                                  it explains the hash and the guard)
  CRC.Database/dbo/Tables/Users.sql, PatientBasic.sql, StaffSlots.sql, Staff.sql, PatientAppointment.sql,
                                                  PatientJourney.sql
  CRC.Database/Stored Procedures/Branch/spBranch_ListActive.sql   (the smallest example of house style)
  CRC.Database/Stored Procedures/StaffSlots/spStaffSlots_List.sql (how slots are read today)
  CRC.Database/CRC.Database.sqlproj               (the <Folder Include> block and the <Build Include> block)

HOUSE STYLE FOR EVERY .sql YOU WRITE (match the neighbours exactly):
• SET NOCOUNT ON; first inside BEGIN.
• A header comment saying what it does, who calls it, and — if it takes @User_ID — WHICH KIND. None of these
  five takes one; say so explicitly in each header, because a reader's first question will be why not.
• @PascalCase parameters, [bracketed] identifiers, ending GO.
• sp{Table}_{What} naming. These are sp{Agent…}_{What} because they exist for one caller and grouping them
  under Agent/ is what makes that visible.

YOUR TASK (Prompt 0).

1. CREATE THE FOLDER CRC.Database/Stored Procedures/Agent/ and write FIVE .sql files in it.

2. spAgentPatient_ListScreeningQueue.sql — no parameters. This is the daily sweep's single read.
   Start from the sketch in Nucentra_WhatsApp_Agent_Plan.md §4.2.1 but VERIFY EVERY COLUMN NAME AND TYPE
   against CRC.Database/dbo/Tables/PatientBasic.sql before you trust it. It must return, per active patient
   (DischargeType_ID IS NULL):
     Patient_ID, Patient_Name, Patient_Phone, Patient_iFOBTStatus, Patient_iFOBTCompletionDate,
     Patient_iFOBTResults, NricLast4 (RIGHT(...,4) — NEVER the full column),
     ScreeningState  — the CASE from §4.2.1: NO_PHONE wins over everything, then UNRECORDED (status NULL),
                       INCOMPLETE (status = 0), POSITIVE (results = 1), NEGATIVE (results = 0), else
                       UNRECORDED. Comment WHY NO_PHONE is first.
     OpenAppointmentCount — COUNT of that patient's 'Scheduled' appointments dated today or later. Comment
                       that dbo.PatientAppointment has nothing unique except its PK (CoreFlow.md §3.9), so
                       this count is the ONLY thing stopping a re-sweep booking the same patient twice.
     HasAssessment   — BIT, EXISTS over dbo.PatientJourney where UPPER(PjAppType_Name) = 'PATIENT ASSESSMENT'.
                       Read CoreFlow.md §3.10 first and comment on WHY the match is on the denormalized NAME.
   Order by Patient_ID DESC.
   🔴 Patient_iFOBTStatus and Patient_iFOBTResults are BIT NULL and Patient_iFOBTCompletionDate is DATE NULL.
   Patient_Phone is VARCHAR(100) NOT NULL — so the NO_PHONE branch is about an EMPTY STRING, not a NULL.
   Keep the ISNULL anyway as a belt-and-braces guard, and say in the comment that the column is NOT NULL so a
   reader does not go looking for the null case.

3. spAgentPatient_FindByPhone.sql — @Phone VARCHAR(100). Resolves an inbound WhatsApp number to zero, one or
   MANY patients. Returns Patient_ID, Patient_Name, Patient_Phone, NricLast4, Patient_iFOBTStatus,
   Patient_iFOBTResults, DischargeType_ID. Order by Patient_ID DESC.

   🔴 DO NOT USE master.dbo.spt_values, AND DO NOT USE sys.all_objects. The sketch in
   Nucentra_WhatsApp_Agent_Plan.md §4.2.2 uses a row generator, which would add a THIRD SQL71502 warning to
   the build. CoreFlow.md §12 #8 makes "exactly two warnings" a tripwire that tells you when you have broken
   something, and this plan will not spend it on a digit-strip. Instead:

     • Normalise @Phone with a scalar loop — it runs at most ~100 times on a ~100-character string:
           DECLARE @Digits VARCHAR(100) = ISNULL(@Phone, '');
           WHILE PATINDEX('%[^0-9]%', @Digits) > 0
               SET @Digits = STUFF(@Digits, PATINDEX('%[^0-9]%', @Digits), 1, '');
     • If @Digits is shorter than 9 characters, return an EMPTY RESULT SET WITH THE SAME COLUMNS AND TYPES as
       the success path — SELECT TOP 0 ... — never a differently-shaped grid. Dapper maps by name and a caller
       must be able to read the result unconditionally.
     • DECLARE @Tail VARCHAR(9) = RIGHT(@Digits, 9);  -- the subscriber part of every Malaysian mobile
       number, whether it arrived as 60123456789 or 0123456789
     • Match the stored column with a single TRANSLATE-based strip of a NAMED separator set, then RIGHT(...,9):
           RIGHT(REPLACE(TRANSLATE([Patient_Phone], '+-() .', '######'), '#', ''), 9) = @Tail
       Comment plainly that this covers + - ( ) space and full stop AND NOTHING ELSE, that a number containing
       any other character will not match, and that the expression is not sargable so this is a scan — which
       is acceptable on a patient table of this size and would not be on a large one.
     • Say in the header comment WHY the match is on the last 9 digits rather than on equality.

   THEN RUN AND REPORT this query, which finds every row the column-side strip would miss:
       SELECT [Patient_ID], [Patient_Phone] FROM [dbo].[PatientBasic]
        WHERE REPLACE(TRANSLATE([Patient_Phone], '+-() .', '######'), '#', '') LIKE '%[^0-9]%';
   An empty result is the pass condition. If it returns rows, DO NOT widen the TRANSLATE set on your own
   judgement — report the rows and the characters they contain, and stop.

4. spAgentStaff_ListByBranch.sql — @Branch_ID VARCHAR(100). Returns Staff_ID, Staff_Name, Staff_Phone,
   Staff_Type, StaffType_Name, Staff_Base for the staff based at that branch, ordered by Staff_Name.
   LEFT JOIN dbo.LU_STAFFTYPE — it is NOT a foreign key (read CoreFlow.md §3.4 and confirm), so an INNER JOIN
   would silently drop a staff member holding a retired type code. Comment that.

5. spAgentSlots_FindOpenByBranch.sql — @Branch_ID VARCHAR(100), @FromDate DATE, @ToDate DATE,
   @Staff_Type VARCHAR(100) = NULL. Returns StaffSlot_ID, Staff_ID, Staff_Name, Staff_Phone, Staff_Type,
   SlotDate, and SlotStartTime/SlotEndTime as VARCHAR(5) via CONVERT(..., 108) — read
   CRC.Database/dbo/Tables/StaffSlots.sql to confirm the stored types first, and say in the header why the
   times are projected as strings rather than TIME.
   🔴 PatientAppointment_ID IS NULL *IS* AVAILABILITY (CoreFlow.md §3.7). Write that in the comment above the
   predicate — it is the single most misreadable line in the procedure.
   Join dbo.Staff on Staff_ID (by convention, not by FK) and filter s.Staff_Base = @Branch_ID.
   Order by SlotDate, SlotStartTime, Staff_Name.
   Add a header note that this read runs OUTSIDE any transaction and is ADVISORY ONLY: a slot it returns can
   be consumed a second later, and SaveAppointmentAsync re-reads under its own lock and answers SlotTaken
   (CoreFlow.md §6.7). A caller must handle that, not assume this read is still true.

6. spAgentUsers_GetServiceAccount.sql — no parameters. Returns TOP 1 User_ID, Username, User_Name, User_Type
   from dbo.Users WHERE Username = 'AGENT_SERVICE'. Nothing else — NOT Password_Hash, NOT User_Email.
   🔴 Its header comment must say all three of these:
     • It declares NO @User_ID, writes NO dbo.AuditTrails row, and runs BEFORE any principal exists — it is
       what CREATES the principal, so it could not have an actor even if it wanted one.
     • The username literal is the contract. dbo.Users carries UNIQUE INDEX IX_Users_Username, so this is an
       index seek and the answer cannot be ambiguous.
     • Returning no row is NOT a normal outcome — it means the database was published without the seed, and
       the caller is required to FAIL the request rather than continue with a null actor. Name the failure it
       prevents: every write audited as AuditTrails.User_Id = 0.

7. 🔴 REGISTER ALL FIVE IN CRC.Database/CRC.Database.sqlproj.
   • Add <Folder Include="Stored Procedures\Agent" /> to the existing <Folder Include> block.
   • Add five <Build Include="Stored Procedures\Agent\{File}.sql" /> lines to the existing <Build Include>
     block, at the END of it. REORDER NOTHING AND REFORMAT NOTHING ELSE IN THAT FILE.
   An unregistered .sql builds locally and is SILENTLY ABSENT from the .dacpac (CoreFlow.md §11.1). This is
   the step that fails quietly, and the diff of this file is the thing to check twice.

8. SEED THE AGENT_SERVICE ACCOUNT in CRC.Database/Scripts/Seed_Users.sql.
   Append a SECOND guarded insert below the existing SUPERUSER one, in the same shape:
       IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = 'AGENT_SERVICE') INSERT ...
   Values: User_Name 'AGENT SERVICE ACCOUNT', Username 'AGENT_SERVICE', User_Email 'agent@crc.local',
   User_Type 2 (ADMIN), Staff_ID NULL.

   🔴 THE PASSWORD HASH. Generate a hash of a FRESH RANDOM SECRET AND THROW THE SECRET AWAY. Do not reuse
   the SUPERUSER hash (that would give this account the publicly known password 'ChangeMe!123'), and do not
   invent a string that is not valid base64 (PasswordHasher throws FormatException on one, turning a login
   attempt into a 500 instead of a clean rejection).
     • Write a THROWAWAY console project in your scratch directory — NOT in the repo — that references
       Microsoft.AspNetCore.App, generates a random secret with RandomNumberGenerator, and prints
       new Microsoft.AspNetCore.Identity.PasswordHasher<string>().HashPassword("", secret).
       That is the same hasher CRC.Web/Controllers/AccountController.cs uses (see its _hasher field), so the
       format is right by construction.
     • Paste ONLY the hash into Seed_Users.sql. DO NOT print, log, commit or tell the owner the secret, and
       DO NOT write it into the file's comment. The account is meant to be un-loginable.
     • DELETE the throwaway project.
   Write a comment block above the insert, in the voice of the file's existing one, covering: what the account
   is for (it is the audit actor for every write the agent API makes, and nothing else); that it never logs in;
   that its password is a discarded random secret and is therefore not recoverable and not in source control —
   explicitly contrasting it with the SUPERUSER row a few lines above, whose password IS public; that the
   guard on [Username] means a re-publish never re-seeds it; and that deleting the row breaks every agent API
   call with a 503 by design.

9. UPDATE SEEDING.md:
   • The "What is seeded" table: dbo.Users goes from 1 row to 2. Add the AGENT_SERVICE row with its User_Type
     and Staff_ID, next to the SUPERUSER row.
   • Add a short sub-section under the Users entry explaining the agent account in two or three sentences —
     what it is for, that it has no usable password, and that the API returns 503 if it is missing. Point at
     CoreFlow.md §13.3.
   • DO NOT touch the SUPERUSER warning at the top of the file; it is still true and still the first thing a
     reader must see.

10. CREATE THE CoreFlow.md §13 SKELETON.
   🔴 APPEND A NEW §13 AT THE END OF THE FILE. RENUMBER NOTHING. CoreFlow.md's own §10 says: "If you need a
   thirteenth topic, add §13 — do not shuffle §10, §11 or §12 aside to make room." Also add one row to the
   jump-table near the top of the file (the "| § | What it answers |" table) for §13, and to nothing else.

   Create these sub-headings, in this order, each followed by `> *Written in Prompt N — not yet filled in.*`
   for the ones you are not writing now:

     ### 13.0 What the Agent API is, and what it is not          [you write this now]
     ### 13.1 The five procedures                                [you write this now]
     ### 13.2 CRC.Api — the project, and why it is a library     [Prompt 1]
     ### 13.3 🔴 Authentication and the service actor            [Prompt 1]
     ### 13.4 The seven read endpoints, and their exact JSON     [Prompt 2]
     ### 13.5 The write endpoint, and the typed failure reasons  [Prompt 3]
     ### 13.6 Configuration, deployment and the platform lock-down  [Prompt 4]
     ### 13.7 What is deliberately not here                      [Prompt 4]

   §13.0 — WRITE IT PROPERLY. What the surface is (eight endpoints, one shared key, one external caller), what
   it is NOT (not the agent, not a public API, not a second host), and 🔴 the one sentence that matters most:
   this is the first authenticated-by-something-other-than-a-cookie surface in nucentra, and the reason it can
   exist safely is that one filter replaces the global AuthorizeFilter for exactly one controller. Match
   CoreFlow.md's register: present tense, as built, opinionated, no aspiration. Where the code does not exist
   yet, say "Prompt N builds this" rather than describing it as though it does.

   §13.1 — the five procedures in a table like §5's: name, parameters, what it returns, and a 🔴 column or note
   saying NONE of the five declares @User_ID and why that is correct for each. Add, underneath, the three
   observations you actually made while writing them — the NULL-is-availability rule, the last-9-digits match
   and what it cannot match, and the advisory-read caveat. Write what you FOUND, including anything that
   surprised you.

VERIFY BEFORE YOU FINISH — do all six, in order, and report each:

  a) MSBUILD REBUILD, and read the warning count:
     "C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /t:Rebuild /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
     🔴 PASS CONDITION: "Build succeeded", 0 Error(s), and EXACTLY TWO SQL71502 warnings, both in
     spStaffSlots_CreateRange.sql at lines 46 and 52. THREE MEANS YOU USED A ROW GENERATOR IN STEP 3 — go back
     and use the PATINDEX loop. Paste the warning lines verbatim.

  b) PUBLISH TO LOCAL CRC_DB, so the procedures actually exist:
     "C:/Program Files/Microsoft Visual Studio/18/Insiders/Common7/IDE/Extensions/Microsoft/SQLDB/DAC/SqlPackage.exe" /Action:Publish /SourceFile:CRC.Database/bin/Debug/CRC.Database.dacpac /TargetServerName:localhost /TargetDatabaseName:CRC_DB /TargetTrustServerCertificate:True

  c) CONFIRM THE SEED LANDED:
     sqlcmd -S localhost -d CRC_DB -E -C -Q "SELECT User_ID, Username, User_Type, Staff_ID FROM dbo.Users ORDER BY User_ID"
     Report the rows. AGENT_SERVICE must be there with User_Type 2 and a NULL Staff_ID. Note its User_ID and
     say plainly in your write-up that this number is LOCAL ONLY and will be different on Azure SQL — which is
     the whole reason nothing stores it.

  d) RUN ALL FIVE PROCEDURES and paste the output (truncate long result sets to ~10 rows):
     sqlcmd -S localhost -d CRC_DB -E -C -Q "EXEC dbo.spAgentUsers_GetServiceAccount"
     sqlcmd -S localhost -d CRC_DB -E -C -Q "EXEC dbo.spAgentPatient_ListScreeningQueue"
     sqlcmd -S localhost -d CRC_DB -E -C -Q "EXEC dbo.spAgentPatient_FindByPhone @Phone = '60123456789'"
     sqlcmd -S localhost -d CRC_DB -E -C -Q "EXEC dbo.spAgentStaff_ListByBranch @Branch_ID = '<a real branch id from dbo.Branch>'"
     sqlcmd -S localhost -d CRC_DB -E -C -Q "EXEC dbo.spAgentSlots_FindOpenByBranch @Branch_ID = '<same>', @FromDate = '<today>', @ToDate = '<today + 30>'"
     An empty result from the three data reads is a legitimate answer on a lightly seeded database — SAY SO
     rather than treating it as a failure, but confirm the COLUMN HEADERS are present and correct in each,
     because that is what Prompt 2 maps onto models.

  e) THE PHONE-STRIP AUDIT from step 3. Run it, report it, and do not widen the character set on your own.

  f) CONFIRM NO C# CHANGED: `git status --short` shows only .sql files, CRC.Database.sqlproj, SEEDING.md,
     CoreFlow.md and AgentApiPlan.md. If a .cs file appears, you did something this prompt did not ask for.

WHEN DONE: lead with the warning count from (a) and the AGENT_SERVICE row from (c). Then list every file
created or edited, paste the five procedure outputs, and confirm CoreFlow.md has a §13 with eight sub-headings
of which two are genuinely written. Then edit AgentApiPlan.md — tick the Prompt 0 box in the Progress Tracker
and set Prompt 0's Status to "✅ Done".

DO NOT touch Azure. DO NOT write, edit or delete a single .cs file. DO NOT edit any existing .sql file except
Scripts/Seed_Users.sql. DO NOT edit Nucentra_Azure_Deployment_Guide.md, DOCUMENTSTORAGE.md or
DapperLayerPlan.md.
```

---

## Prompt 1 — The project and the guard

**Status:** ✅ Done
**Depends on:** Prompt 0

> **Why the filter ships before the data.** This prompt creates the project, wires it into the host, builds
> `AgentApiKeyFilter`, and adds **one** endpoint — `GET /api/agent/branches`, which returns a list of branch
> names and nothing else. That is deliberate. The whole security shape of this feature gets built and proven
> against an endpoint whose worst-case leak is a list of hospitals, **before any endpoint exists that returns a
> patient**. If the filter is wrong, you find out here, cheaply. Prompt 2 then adds six endpoints to a guard
> that is already known to work.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 10 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal; CRC.Web (net10.0) is the host,
CRC.Data (net10.0) is the Dapper data-access layer, CRC.Database is a classic SSDT project.

WHAT'S ALREADY DONE (Prompt 0): five stored procedures exist under CRC.Database/Stored Procedures/Agent/ and
are registered in CRC.Database.sqlproj — spAgentPatient_ListScreeningQueue, spAgentPatient_FindByPhone,
spAgentStaff_ListByBranch, spAgentSlots_FindOpenByBranch and spAgentUsers_GetServiceAccount. A dbo.Users row
with Username 'AGENT_SERVICE' (User_Type 2, Staff_ID NULL, an unrecoverable random password) is seeded by
Scripts/Seed_Users.sql and is present in your local CRC_DB. CoreFlow.md has a new §13 with eight sub-headings,
of which §13.0 and §13.1 are written. NO C# HAS BEEN WRITTEN YET.

WHY: n8n cannot call nucentra's existing endpoints — they are cookie-authenticated behind a global
antiforgery filter with a 600-second sliding timeout. This prompt builds the API-key surface that replaces
that, in a NEW PROJECT, and proves it with the least dangerous endpoint available.

READ FIRST:
  CoreFlow.md                       (§0 and §0.1 in full; §2.1 the UserType claim; §2.2 the AllowAnonymous
                                     audit — YOU ARE ABOUT TO MAKE THAT SENTENCE FALSE; §2.4 antiforgery;
                                     §6.5 DatabaseHelper; §9.2 the two log channels; §10 the file map;
                                     §11.2 and §11.3 the checklists; §12 #2, #4 and #6; §13)
  AgentApiPlan.md                   (this plan — read "The actor identity" and "Why a class library" IN FULL.
                                     They contain the two mistakes this prompt exists to avoid.)
  Nucentra_WhatsApp_Agent_Plan.md   (§4.1 and §4.4)
  CRC.Web/Program.cs                (all of it — you are editing it, and the ORDER of the pipeline matters)
  CRC.Web/Infrastructure/AuditLog.cs, ErrorResponse.cs, CorrelationIdMiddleware.cs
                                    (the SHAPES you are copying — you may not reference them, see below)
  CRC.Data/Data/DatabaseHelper.cs   (CurrentUserId, and its remarks — read them, they explain the trap)
  CRC.Data/Data/IDatabaseData.cs    (the header comment block and the comment style; find
                                     GetActiveBranchesAsync and BranchOption — you are calling them)
  CRC.Web/Controllers/Branch/BranchController.cs   (a migrated controller: the try/catch shape, the
                                                    hand-built anonymous object, the AuditLog calls)
  CRC.Data/CRC.Data.csproj          (the FrameworkReference pattern you are copying)
  CRC_Portal.slnx

🔴 TWO THINGS THIS REPO HAS ALREADY GOT WRONG ONCE. Read both before you write any code.

  (1) A deleted CRC.Api (git commit 291ab458, removed in a6c9d16e) shipped an API-key handler containing:
          new(ClaimTypes.NameIdentifier, "0"),   // "Keeps DatabaseHelper happy"
      That comment is wrong. `0` does not keep anything happy — it IS the failure. SqlData passes @User_ID to
      19 audit-actor procedures from DatabaseHelper.CurrentUserId, which reads that exact claim. A `0` means
      every write the agent makes is audited as nobody, with no error and no failed request. You are going to
      resolve a REAL User_ID from dbo.Users instead. `git show a6c9d16e^:CRC.Api/Security/ApiKeyAuthenticationHandler.cs`
      if you want to see the whole of it.

  (2) The deleted project was a standalone Microsoft.NET.Sdk.Web host with its own Program.cs. THIS ONE IS
      NOT. It is a class library. Do not create Program.cs, appsettings.json, launchSettings.json or a port.
      A stray CRC.Api/CRC.Api.csproj.user may still be on disk from that attempt; it is gitignored and
      irrelevant — ignore it, do not commit it, do not build around it.

YOUR TASK (Prompt 1).

PART A — THE PROJECT.

1. CREATE CRC.Api/CRC.Api.csproj — Microsoft.NET.Sdk (NOT .Web), net10.0, Nullable enable, ImplicitUsings
   enable, <FrameworkReference Include="Microsoft.AspNetCore.App" />, and a ProjectReference to
   ..\CRC.Data\CRC.Data.csproj. CRC.Data already uses exactly this FrameworkReference pattern — copy it.
   🔴 NO ProjectReference to CRC.Web, EVER. CRC.Web is about to reference THIS project; the reverse is a cycle
   and it is why steps 4 and 5 exist.

2. ADD CRC.Api to CRC_Portal.slnx, next to the existing three <Project Path=…> entries. Match the existing
   formatting; CRC.Data's entry is the closest model (no <Build>/<Deploy> children — those belong to the
   .sqlproj only).

3. ADD a ProjectReference to CRC.Api in CRC.Web/CRC.Web.csproj, in the ItemGroup that already holds the
   CRC.Data reference.

PART B — THE TWO INFRASTRUCTURE FILES.

4. CRC.Api/Infrastructure/AgentAuditLog.cs, namespace CRC.Api.Infrastructure. Static, modelled on
   CRC.Web/Infrastructure/AuditLog.cs: `Log.ForContext("AuditChannel", true)` against Serilog's STATIC logger.
   Write a header comment explaining why this file exists as a copy rather than a reference — CRC.Web
   references CRC.Api, so CRC.Api cannot reference CRC.Web — and why the copy is nonetheless cheap: both write
   to the SAME process-wide Serilog pipeline configured in CRC.Web/Program.cs, so these lines land in the same
   Logs/audit-*.log as everything else.
   Three methods to start: AgentRequestAuthenticated(HttpContext, string endpoint, int serviceUserId),
   AgentRequestRejected(HttpContext, string endpoint, string reason),
   AgentServiceAccountMissing(HttpContext, string endpoint).
   🔴 EVERY MESSAGE MUST NAME THE ACTOR IN ITS OWN TEXT. Do not rely on Serilog's [User:…] field — see step 6.
   🔴 NEVER LOG THE KEY, not even a prefix, not even on rejection.

5. CRC.Api/Infrastructure/AgentErrorResponse.cs, namespace CRC.Api.Infrastructure. The same three-property
   shape CRC.Web/Infrastructure/ErrorResponse.cs returns — { success = false, message, correlationId } — with
   the same GenericUserMessage default. Read the correlation id from
   HttpContext.Items["CorrelationId"] BY THE STRING LITERAL, falling back to HttpContext.TraceIdentifier
   exactly as HttpContextCorrelationExtensions.GetCorrelationId does. Comment that the literal is duplicated
   from CRC.Web/Infrastructure/CorrelationIdMiddleware.HttpContextItemKey on purpose, for the reference-cycle
   reason above, and that the two must be changed together.

6. 🔴 WHILE YOU ARE IN THERE, VERIFY AND THEN DOCUMENT THIS, because it will otherwise look like a bug later:
   CorrelationIdMiddleware pushes its Serilog "UserName" property from context.User?.Identity?.Name BEFORE the
   endpoint runs — i.e. before your filter sets the principal. Confirm it by reading the middleware, then
   confirm it EMPIRICALLY in step 15 by grepping the log. Every agent request will log [User:anonymous] while
   dbo.AuditTrails correctly names AGENT_SERVICE. Both are right and neither is to be "fixed". This belongs in
   §13.3.

PART C — THE DATA-LAYER METHOD.

7. ADD ONE method to CRC.Data/Data/IDatabaseData.cs and CRC.Data/Data/SqlData.cs, under a NEW banner
   `// ----- Agent API (machine-callable surface — CoreFlow.md §13) -----` placed at the END of both files, in
   the same position in each:
       Task<AgentServiceAccount?> GetAgentServiceAccountAsync();
   Calls spAgentUsers_GetServiceAccount. QuerySingleOrDefaultAsync — the row may not exist, and that case is
   meaningful. NO @User_ID: read the procedure's header comment and repeat its reasoning in yours.
   The interface comment must say, in the voice of the file's other comments: what it is for, that a NULL
   return means the database was published without the seed, and that the CALLER IS REQUIRED TO FAIL rather
   than continue — naming the failure it prevents (AuditTrails.User_Id = 0).

8. ADD CRC.Data/Models/AgentServiceAccount.cs, namespace CRC.Data.Models. Properties matching the procedure's
   columns exactly: User_ID (int), Username, User_Name, User_Type (int). One type per file, POCO, no logic.

PART D — THE OPTIONS AND THE FILTER.

9. CRC.Api/AgentApiOptions.cs, namespace CRC.Api. One property, `public string ApiKey { get; set; } = "";`,
   and `public const string SectionName = "Agent";`. Model it on
   CRC.Web/Services/DocumentStorageOptions.cs. Comment that the real key is an App Service app setting
   (Agent__ApiKey, TWO underscores) and never lives in appsettings.json — the same rule DocumentStorage
   follows, and DOCUMENTSTORAGE.md explains why a single underscore is silently ignored.

10. CRC.Api/Infrastructure/AgentApiKeyFilter.cs, namespace CRC.Api.Infrastructure.
    `public sealed class AgentApiKeyFilter : IAsyncAuthorizationFilter`. Constructor-inject
    IOptions<AgentApiOptions>, IDatabaseData and ILogger<AgentApiKeyFilter>.
    An AUTHORIZATION filter, not an action filter — comment why: it runs before model binding and before the
    action, so a bad key never reaches a line of business code and never causes a query.

    THE ORDER OF THESE STEPS IS THE SECURITY PROPERTY. Do not rearrange them.
      i.   Configured key is null/empty  → 401, AgentRequestRejected(…, "not configured"),
           AND _logger.LogError — this is a MISCONFIGURATION, not a caller error, and it must be loud in
           app-*.log even though the caller correctly gets a 401. Fail closed.
      ii.  Header X-Agent-Key missing or empty → 401 + AgentRequestRejected(…, "missing key").
      iii. Keys do not match → 401 + AgentRequestRejected(…, "invalid key").
           🔴 COMPARE IN FIXED TIME. SHA-256 both values with UTF-8 bytes and pass the two 32-byte digests to
           CryptographicOperations.FixedTimeEquals. Do NOT use ==, and do NOT compare the raw bytes with a
           length check first — hashing makes both operands the same length, so the comparison leaks neither
           the key nor its length. Comment that reasoning; it is the kind of thing a later reader "simplifies".
      iv.  ONLY NOW call IDatabaseData.GetAgentServiceAccountAsync(). An unauthenticated caller must never be
           able to make the portal touch the database — say so in the comment.
      v.   Row is null → 503 ServiceUnavailable + AgentServiceAccountMissing + _logger.LogError naming the
           seed file. 🔴 DO NOT CONTINUE. Do not fall through, do not use 0, do not use any default. This
           branch is the entire reason Prompt 0 seeded a row.
      vi.  Build a ClaimsPrincipal with a single ClaimsIdentity whose authenticationType is a named constant
           (e.g. "AgentApiKey") so IsAuthenticated is true, carrying:
               ClaimTypes.NameIdentifier = account.User_ID.ToString(CultureInfo.InvariantCulture)
               ClaimTypes.Name           = account.Username
               "UserType"                = account.User_Type.ToString(CultureInfo.InvariantCulture)
           Assign it to context.HttpContext.User.
           🔴 Comment, above NameIdentifier, that this is THE claim DatabaseHelper.CurrentUserId reads and
           SqlData passes as @User_ID to the 19 audit-actor procedures, that it must be a plain integer string,
           and that the deleted CRC.Api set it to "0" — cite CoreFlow.md §0.1.
      vii. AgentRequestAuthenticated(…) naming the endpoint, the caller IP and the resolved user id.

    For the 401 and 503 answers return a Microsoft.AspNetCore.Mvc.ObjectResult / StatusCodeResult carrying a
    body in the house shape — { success = false, message } — so a caller always gets JSON. NEVER include the
    configured key, the supplied key, an exception message, or the reason distinguishing (i) from (ii) from
    (iii) in the RESPONSE body: those distinctions go in the log, not on the wire.

PART E — THE CONTROLLER, WITH ONE ENDPOINT.

11. CRC.Api/Controllers/AgentApiController.cs, namespace CRC.Api.Controllers.

        [ApiController]
        [Route("api/agent")]
        [AllowAnonymous]
        [ServiceFilter(typeof(AgentApiKeyFilter))]
        [IgnoreAntiforgeryToken]
        public class AgentApiController : ControllerBase

    🔴 A HEADER COMMENT BLOCK IS MANDATORY ON THIS CLASS, and it must say all four of these plainly:
      • [AllowAnonymous] disables the GLOBAL AuthorizeFilter for this controller. CoreFlow.md §2.2 said a grep
        for AllowAnonymous returns two lines; after this it returns three, and this is the third. That is a
        deliberate, documented widening of the portal's public surface.
      • AgentApiKeyFilter is the ONLY thing closing that gap. If it is removed, mis-registered in Program.cs,
        or made to fail open, these endpoints become an unauthenticated read of patient data.
      • [IgnoreAntiforgeryToken] disables the GLOBAL AutoValidateAntiforgeryTokenAttribute. Without it every
        POST here returns 400, because an external caller has no cookie and cannot obtain an X-CSRF-TOKEN.
      • Every write made through this controller is audited as AGENT_SERVICE, resolved per request by the
        filter — see CoreFlow.md §13.3.
    Constructor-inject IDatabaseData and ILogger<AgentApiController>.

12. THE ONE ENDPOINT: `[HttpGet("branches")] public async Task<IActionResult> GetBranches()`.
    Calls the EXISTING IDatabaseData.GetActiveBranchesAsync() (spBranch_ListActive) — DO NOT add a new method
    and DO NOT add a new procedure. Return
        Ok(new { success = true, data = list.Select(b => new { branchId = …, name = …, state = … }) })
    — camelCase, hand-built, mapped from BranchOption. NEVER serialize the model directly (CoreFlow.md §12 #4).
    Read BranchOption first and pick property names that make sense to an external caller; write them down,
    because §13.4 will publish them as a contract and Prompt 2 must match the style.
    try/catch (SqlException) then catch (Exception), both _logger.LogError, both returning
    Ok(AgentErrorResponse.ForUser(HttpContext, …)).

PART F — WIRING THE HOST.

13. CRC.Web/Program.cs — three additions, each with a comment, and NOTHING ELSE CHANGED:
    • On the existing AddControllersWithViews(…) call, chain
          .AddApplicationPart(typeof(CRC.Api.Controllers.AgentApiController).Assembly)
      with a comment saying controllers in a referenced assembly are not discovered without it.
    • builder.Services.Configure<CRC.Api.AgentApiOptions>(builder.Configuration.GetSection(CRC.Api.AgentApiOptions.SectionName));
      placed beside the existing DocumentStorageOptions binding, which is its model.
    • builder.Services.AddScoped<CRC.Api.Infrastructure.AgentApiKeyFilter>();  — required for [ServiceFilter].
      Comment that it is SCOPED because it resolves IDatabaseData per request.
    🔴 DO NOT reorder the middleware pipeline, do not touch the /uploads branch, do not touch the rate limiter,
    the cookie options, the five policies or the antiforgery configuration.

14. CONFIG. appsettings.json gets an "Agent" section with an EMPTY ApiKey and a comment-free but obvious
    placeholder shape; appsettings.Development.json gets a real development value such as
    "dev-only-agent-key-change-me".
    🔴 NEVER put a production key in either file. Both are in source control. The production value is the
    App Service app setting Agent__ApiKey (TWO underscores), set by the owner by hand — DO NOT set it, DO NOT
    go near Azure, and DO NOT edit Nucentra_Azure_Deployment_Guide.md.

VERIFY BEFORE YOU FINISH — do all six, in order, and report each:

  a) `dotnet build CRC.Web/CRC.Web.csproj` — 0 errors, 0 new warnings. Confirm in the output that CRC.Api and
     CRC.Data both built.

  b) THE SITE STILL WORKS. Run `dotnet run --project CRC.Web --launch-profile https`, log in at
     https://localhost:7276 as SUPERUSER (SEEDING.md), and load the Branch page. This proves the
     AddApplicationPart and the DI additions broke nothing. Report what you saw.

  c) 🔴 THE TWO NEGATIVE TESTS, FIRST AND MOST IMPORTANT:
        Invoke-WebRequest https://localhost:7276/api/agent/branches -SkipCertificateCheck -SkipHttpErrorCheck | Select-Object StatusCode
        Invoke-WebRequest https://localhost:7276/api/agent/branches -Headers @{'X-Agent-Key'='wrong'} -SkipCertificateCheck -SkipHttpErrorCheck | Select-Object StatusCode
     BOTH MUST BE 401. A 200 on either is a patient-data leak waiting for Prompt 2 — stop and fix it before
     going further. A 404 on both means the application part is not registered: add `app.MapControllers();`
     immediately above the existing app.MapControllerRoute(…) and re-test. Report the status codes verbatim.

  d) THE POSITIVE TEST:
        Invoke-RestMethod https://localhost:7276/api/agent/branches -Headers @{'X-Agent-Key'='<your dev key>'} -SkipCertificateCheck
     Paste the JSON. Note there is NO cookie and NO CSRF token in that call — that is the feature working.

  e) 🔴 THE ACTOR TEST. Grep CRC.Web/Logs/audit-*.log for your AgentRequestAuthenticated line and PASTE IT.
     It must name the resolved AGENT_SERVICE user id. Then confirm the surprise from step 6 empirically: the
     line's [User:…] field reads `anonymous` while the message body names the agent. Report both halves — that
     contradiction is what §13.3 has to explain.

  f) THE 503 PATH, DRIVEN DELIBERATELY. In your local CRC_DB:
        sqlcmd -S localhost -d CRC_DB -E -C -Q "UPDATE dbo.Users SET Username = 'AGENT_SERVICE_OFF' WHERE Username = 'AGENT_SERVICE'"
     Call the endpoint with the CORRECT key — it must return 503, not 200 and not 500 — then put it back:
        sqlcmd -S localhost -d CRC_DB -E -C -Q "UPDATE dbo.Users SET Username = 'AGENT_SERVICE' WHERE Username = 'AGENT_SERVICE_OFF'"
     Confirm the endpoint answers 200 again afterwards, and paste the LogError line the 503 wrote. An untested
     fail-closed path is an assumption, not a property.

THEN WRITE UP WHAT YOU LEARNED — CoreFlow.md.
Fill in §13.2 and §13.3. Do not touch §13.0 or §13.1, do not renumber anything, and do not edit §2.2 — Prompt
4 owns that amendment and doing it twice makes a mess.

  §13.2 — CRC.Api as built: the SDK, the framework reference, the ProjectReference chain
     CRC.Web → CRC.Api → CRC.Data, and 🔴 WHY THE ARROW POINTS THAT WAY — the cycle that forbids referencing
     CRC.Web, and the two duplicated infrastructure files that follow from it, named, with the note that the
     correlation-id string literal must be changed in two places. State what comes free from sharing the host
     (Serilog's two channels, the connection string, DI, CorrelationIdMiddleware) and what does not (the two
     global filters). Include the three Program.cs lines.

  §13.3 — the authentication path, step by step in the filter's real order, with the fixed-time comparison and
     WHY it hashes first. Then 🔴 THE SERVICE ACTOR, at length: the seeded row, the per-request lookup by
     username, why not a config value (User_ID is an IDENTITY and differs per database; a stale setting is a
     silent wrong-actor audit), the 503, and the claim that DatabaseHelper.CurrentUserId reads. Record the
     deleted CRC.Api's `NameIdentifier = "0"` as the concrete mistake this design prevents. End with the
     [User:anonymous] finding from (e), stated as a correct behaviour of two channels answering two different
     questions — and say explicitly that it is not to be "fixed".

WHEN DONE: lead with the two 401s from (c) and the 503 from (f). Then the JSON from (d), the audit line from
(e), and a list of every file created or edited. Then edit AgentApiPlan.md — tick the Prompt 1 box and set
Prompt 1's Status to "✅ Done".

DO NOT touch Azure. DO NOT edit any .js, .cshtml or .sql file. DO NOT edit any existing controller. DO NOT add
a second endpoint — Prompt 2 owns those, and the point of this prompt is that the guard is proven against an
endpoint that returns nothing sensitive.
```

---

## Prompt 2 — The six remaining reads

**Status:** ⬜ Not started
**Depends on:** Prompts 0–1

> **The guard is already proven, so this prompt is mechanical — with one exception.** Endpoint 3 reads a row
> that contains a full NRIC, and the single most important line in this prompt is the one that does not project
> it.

```text
You are an AI coding agent on the CRC Portal ("nucentra") — ASP.NET Core 10 MVC + a classic SSDT database
project. Fresh chat — no prior memory. Repo root CRC_Portal; CRC.Web is the host, CRC.Api is a class library
whose controllers CRC.Web loads as an application part, CRC.Data is the Dapper layer.

WHAT'S ALREADY DONE (Prompts 0–1): five agent stored procedures exist and are published to your local CRC_DB.
CRC.Api exists as a class library with AgentApiController (one endpoint, GET /api/agent/branches),
AgentApiKeyFilter, AgentApiOptions, AgentAuditLog and AgentErrorResponse. The filter resolves the seeded
AGENT_SERVICE dbo.Users row per request and assigns a ClaimsPrincipal to HttpContext.User; a missing row is a
503. IDatabaseData/SqlData have one agent method, GetAgentServiceAccountAsync, under an
`// ----- Agent API … -----` banner at the end of both files. CoreFlow.md §13.0–§13.3 are written.

WHY: the agent needs six more reads before it can hold a conversation — the screening queue it sweeps, the
phone lookup that turns an inbound WhatsApp number into a patient, one patient's record, one patient's
appointments, the clinicians at a branch, and the open hours at a branch.

READ FIRST:
  CoreFlow.md                       (§13 in full — it is now most of your brief; then §0, §3.8 PatientBasic,
                                     §3.9 PatientAppointment, §3.7 StaffSlots, §4.7, §5.6, §5.7,
                                     §11.2 and §11.3, §12 #4)
  AgentApiPlan.md                   ("What must never leave through this API")
  Nucentra_WhatsApp_Agent_Plan.md   (§4.4's endpoint table and §8's tool definitions — §8 is what the agent
                                     will actually send, and it is the best statement of what each endpoint
                                     is for)
  CRC.Database/Stored Procedures/Agent/*.sql   (all four data procedures — the EXACT column names and types
                                                they return are what you are mapping; do not trust this
                                                prompt's summary of them over the files)
  CRC.Api/Controllers/AgentApiController.cs    (the endpoint you are copying the shape of)
  CRC.Data/Data/IDatabaseData.cs, SqlData.cs   (the agent banner, and the comment style)
  CRC.Data/Models/BranchOption.cs, PatientBasicDetail.cs, PatientAppointmentItem.cs

YOUR TASK (Prompt 2) — four data-layer methods, four models, six endpoints.

PART A — THE FOUR METHODS AND FOUR MODELS.

1. ADD to CRC.Data/Data/IDatabaseData.cs and SqlData.cs, under the EXISTING agent banner, in the same order in
   both files, after GetAgentServiceAccountAsync:

       Task<List<AgentScreeningQueueItem>> GetAgentScreeningQueueAsync();
       Task<List<AgentPatientMatch>>       FindAgentPatientsByPhoneAsync(string phone);
       Task<List<AgentStaffItem>>          GetAgentStaffByBranchAsync(string branchId);
       Task<List<AgentOpenSlotItem>>       FindAgentOpenSlotsByBranchAsync(string branchId, DateTime fromDate, DateTime toDate, string? staffType);

   QueryAsync → ToList for all four. commandType: CommandType.StoredProcedure. Anonymous parameter objects
   whose property names match the procedure parameters without the @.
   🔴 NONE OF THE FOUR TAKES @User_ID. They are reads and they write no audit row. Say so in each comment —
   a reader who has just read CoreFlow.md §0.1 will ask.

2. ADD four models to CRC.Data/Models/, one type per file, namespace CRC.Data.Models, named for the data.
   🔴 NULLABILITY IS THE THING THAT WILL BITE YOU. Dapper THROWS mapping a NULL onto a non-nullable value type,
   which turns "this patient's iFOBT was never recorded" into a 500. Read each .sql and each dbo/Tables/*.sql
   and type a property nullable if the column is nullable, OR the join is a LEFT JOIN, OR it is an aggregate
   over a possibly empty set. At minimum: Patient_iFOBTStatus and Patient_iFOBTResults are bool?,
   Patient_iFOBTCompletionDate is DateTime?, DischargeType_ID is string?, and StaffType_Name is string?
   because spAgentStaff_ListByBranch LEFT JOINs it. VERIFY EVERY OTHER COLUMN YOURSELF rather than trusting
   that list, and write a one-line comment on each nullable property saying why it is one.
   Comment on AgentOpenSlotItem that SlotStartTime/SlotEndTime are STRINGS ("HH:mm") because the procedure
   CONVERTs them — and that this is deliberate, not an oversight, because the caller is a JSON API and not a
   .NET consumer.

PART B — THE SIX ENDPOINTS.

3. Add all six to the EXISTING CRC.Api/Controllers/AgentApiController.cs. Do not create a second controller.
   Every one keeps the shape endpoint 5 already established: try/catch (SqlException) then catch (Exception),
   both _logger.LogError, both Ok(AgentErrorResponse.ForUser(HttpContext, …)), and a HAND-BUILT camelCase
   anonymous object mapped from the model — never the model itself (CoreFlow.md §12 #4).

   [HttpGet("patients/queue")]                 → GetAgentScreeningQueueAsync
        Ok(new { success = true, data = … }). Project screeningState and openAppointmentCount with those
        names — n8n's sweep branches on both — and nricLast4, never a full NRIC.

   [HttpGet("patients/by-phone")]  ?phone=      → FindAgentPatientsByPhoneAsync
        🔴 Returns { success, matchCount, data[] }. matchCount is NOT decoration: the procedure returns ZERO,
        ONE or MANY rows because dbo.PatientBasic has nothing unique except its primary key (CoreFlow.md
        §3.8), and the agent is required to ask a disambiguating question on >1 rather than take the first.
        Comment that. Reject a null/whitespace phone with the house envelope and a clear message — do not call
        the procedure with an empty string.

   [HttpGet("patients/{patientId}")]            → the EXISTING GetPatientByIdAsync (spPatientBasic_GetById)
        Return { success = true, data = … } for a hit, and { success = false, message = "…" } for a miss —
        read PatientController's equivalent and match how it words a not-found before inventing your own.
        🔴🔴 THE MOST IMPORTANT LINE IN THIS PROMPT: PatientBasicDetail CARRIES THE FULL Patient_NRIC.
        PROJECT nricLast4 ONLY — computed in C# from the last four characters of the trimmed value — AND NEVER
        THE COLUMN ITSELF. Put a comment above it saying the agent confirms identity by asking the patient for
        four digits and comparing, that it must never be ABLE to state more, and that this endpoint is the
        only place in the API where the full value is even in memory.
        Project the fields the agent actually uses — name, phone, iFOBT status/result/date, discharge type,
        branch if present — and leave out address, email, NRIC, and anything clinical beyond the iFOBT trio.
        Say in a comment that the projection is deliberately narrow and that widening it is a privacy decision,
        not a convenience.

   [HttpGet("patients/{patientId}/appointments")] → the EXISTING GetAppointmentsByPatientAsync
        Ok(new { success = true, data = … }). Include appointmentDate, startTime, endTime, status, staff name,
        branch name and the appointment type — the agent uses this to refuse to propose a second booking when
        a future "Scheduled" one already exists. An unknown patient id returns an EMPTY LIST, not an error;
        confirm that against the procedure and say so in a comment.
        🔴 The order is the contract: date DESC, start time DESC, id DESC. Do not re-sort.

   [HttpGet("staff")]              ?branchId=   → GetAgentStaffByBranchAsync
   [HttpGet("slots/open")]         ?branchId=&fromDate=&toDate=&staffType=  → FindAgentOpenSlotsByBranchAsync
        🔴 Parse fromDate and toDate with DateTime.TryParseExact("yyyy-MM-dd", CultureInfo.InvariantCulture).
        A failure is a house-envelope refusal naming the expected format — not an exception, not a 500, and
        not a silent DateTime.Parse that would accept a locale-dependent string. Reject a missing branchId the
        same way. staffType is genuinely optional: null or blank means "do not filter", and blank must be
        converted to null before the call.
        Both endpoints return Staff_Phone. Comment that this is for the clinician-confirmation step and that
        the API is NOT the thing that stops it reaching a patient — the agent's own prompt is — so anyone
        reusing these endpoints for a patient-facing surface must strip it themselves.
        Add a comment on slots/open repeating the procedure's advisory-read warning: a slot returned here can
        be taken a second later, and SaveAppointmentAsync answers SlotTaken (CoreFlow.md §6.7).

VERIFY BEFORE YOU FINISH — report each:

  a) `dotnet build CRC.Web/CRC.Web.csproj` — 0 errors, 0 new warnings.

  b) 🔴 THE TWO NEGATIVE TESTS, ON A NEW ENDPOINT, BEFORE ANYTHING ELSE. Pick /api/agent/patients/queue —
     the one that returns the most patient data — and confirm no key and a wrong key both give 401. These are
     not a formality: you have just added six endpoints to a filter you did not write, and a controller-level
     attribute that failed to apply looks exactly like a working endpoint from the happy path.

  c) DRIVE ALL SIX with a valid key and PASTE THE JSON for each. Use real ids from your local CRC_DB (find
     them with sqlcmd first). If a result is empty because the local database is lightly seeded, SAY SO and
     seed just enough through the portal's own screens to get one non-empty response from
     /patients/queue and /slots/open — an endpoint that has only ever returned [] is an endpoint whose model
     mapping has never been tested.

  d) 🔴 THE MAPPING CHECK. A Dapper mapping mistake COMPILES PERFECTLY and returns the property's default with
     no exception and nothing in a log. For each of the four NEW endpoints, compare the JSON from (c)
     field-by-field against the `sqlcmd EXEC` output of the procedure behind it. A property that is 0, null,
     false or "" in the JSON but populated in sqlcmd is a name mismatch. Report the comparison, not just
     "it worked".

  e) 🔴 THE NRIC CHECK. Search the response body from /api/agent/patients/{id} for the patient's full NRIC as
     it appears in dbo.PatientBasic. IT MUST NOT BE THERE. Then grep the whole of CRC.Api for `NRIC` and
     confirm every hit is either the last-4 projection or a comment about it. Paste both results.

  f) CHECK CRC.Web/Logs/app-*.log for exceptions after driving all six. An empty result with a logged
     exception behind it is the characteristic shape of a mapping failure and it does not fail the request.

THEN WRITE UP WHAT YOU LEARNED — CoreFlow.md.
  §13.4 — the seven read endpoints (the six here plus /branches from Prompt 1), as a table like §4's: verb,
     route, the IDatabaseData method, the procedure, and 🔴 THE EXACT JSON each returns. Then a paragraph on
     the three privacy rules you enforced — nricLast4, the narrow patient projection, Staff_Phone's intended
     reader — and what each one is protecting against.
  §13.1 — complete the procedure table now that all four are wired, adding the IDatabaseData method that calls
     each. Do not rewrite what Prompt 0 wrote there; fill in the column it left.
  Add anything that SURPRISED you. A column that came back null when you expected a value, a procedure whose
  ordering matters more than it looks, a name you had to change because it read wrong from outside — those
  sentences are the reason §13 exists.

WHEN DONE: lead with (b), (d) and (e). Then the six JSON payloads, then every file touched. Then edit
AgentApiPlan.md — tick the Prompt 2 box and set Prompt 2's Status to "✅ Done".

DO NOT touch Azure. DO NOT edit any .js, .cshtml or .sql file. DO NOT add a POST endpoint — Prompt 3 owns the
write. DO NOT edit CoreFlow.md §2.2; Prompt 4 owns it.
```

---

## Prompt 3 — The write

**Status:** ⬜ Not started
**Depends on:** Prompts 0–2

> **This is the prompt §4.1 of the agent plan was written for.** Everything before it reads; this one writes,
> and the thing that can go wrong is invisible: the appointment appears, the page looks right, and
> `dbo.AuditTrails.User_Id` says `0`. The verification step is the point of the prompt.

```text
You are an AI coding agent on the CRC Portal ("nucentra") — ASP.NET Core 10 MVC + a classic SSDT database
project. Fresh chat — no prior memory. Repo root CRC_Portal; CRC.Web hosts CRC.Api's controllers as an
application part; CRC.Data is the Dapper layer.

WHAT'S ALREADY DONE (Prompts 0–2): five agent stored procedures, published locally. CRC.Api holds
AgentApiController with SEVEN GET endpoints, all guarded by AgentApiKeyFilter, which resolves the seeded
AGENT_SERVICE dbo.Users row per request and assigns a ClaimsPrincipal carrying its User_ID as
ClaimTypes.NameIdentifier. IDatabaseData/SqlData have five agent methods. CoreFlow.md §13.0–§13.4 are written.
There is NO write endpoint yet.

WHY: this prompt adds the one and only write — POST /api/agent/appointments — and it adds NO new procedure and
NO new data-layer method, because the portal already has the correct one.

READ FIRST:
  CoreFlow.md                       (🔴 §6.7 SaveAppointmentAsync IN FULL — the booking race and the typed-
                                     failure convention; then §0.1, §3.9, §5.7, §7.3, §11.3, §12 #1 and #3,
                                     and all of §13)
  AgentApiPlan.md                   ("The actor identity" in full, and hard cases #3, #4 and #6)
  Nucentra_WhatsApp_Agent_Plan.md   (§3.8 the exact formats the portal rejects you for; §4.4's endpoint 8;
                                     §3.5 and §3.6 — why a wrong booking is expensive and cannot be cancelled)
  CRC.Data/Models/AppointmentSaveInput.cs, AppointmentSaveResult.cs, AppointmentSaveFailure.cs
                                    (ALL THREE, comments included — they are the best-documented files in the
                                     repo and they tell you exactly what this endpoint must do)
  CRC.Data/Data/SqlData.cs          (SaveAppointmentAsync — read the whole method)
  CRC.Web/Controllers/Patient/PatientController.cs  (its SaveAppointment action: the validation, the switch
                                     that maps AppointmentSaveFailure to a user-facing sentence, the AuditLog
                                     call. YOU ARE WRITING THE MACHINE-FACING TWIN OF THIS.)
  CRC.Api/Controllers/AgentApiController.cs

🔴 THE ONE THING THAT FAILS SILENTLY, AND THE WHOLE REASON THIS PROMPT HAS A VERIFICATION STEP.
spPatientAppointment_Insert declares @User_ID INT = NULL — the ACTOR for its dbo.AuditTrails row — and
SqlData supplies it from DatabaseHelper.CurrentUserId, which reads ClaimTypes.NameIdentifier off
HttpContext.User. AgentApiKeyFilter sets that principal, so this SHOULD work. If it does not, nothing tells
you: the appointment is created, the slot is consumed, the response says success, and the audit row names
user 0. VERIFICATION STEP (e) IS NOT OPTIONAL AND IT IS NOT A FORMALITY.

YOUR TASK (Prompt 3) — one endpoint.

1. ADD a nested request DTO inside AgentApiController — a nested class, like every other controller's request
   type in this repo (CoreFlow.md §10, §11.3). NO DATA ANNOTATIONS ON IT: [ApiController] auto-400s on an
   invalid ModelState with a ProblemDetails body, which is not the envelope the caller branches on. With no
   attributes, ModelState stays valid and every validation answer is yours to word. Comment that reasoning.
   Fields: patientId, appointmentDate (string), staffId, slotIds (int[]), pjAppTypeId (string), branchId,
   status (string).

2. [HttpPost("appointments")]. Validate BY HAND, in this order, returning the house envelope with a clear
   message on the first failure. Read Nucentra_WhatsApp_Agent_Plan.md §3.8 and CoreFlow.md §3.9 for what the
   portal actually rejects, and match PatientController's rules rather than inventing parallel ones:
     • patientId, staffId, branchId non-blank.
     • appointmentDate: DateTime.TryParseExact "yyyy-MM-dd", CultureInfo.InvariantCulture. Nothing else.
     • slotIds: at least one, all > 0, DE-DUPLICATED before the call. AppointmentSaveInput's comment says the
       caller owns the de-duplication and that the COUNT is load-bearing for the slot-existence check — read it.
     • status: must be exactly "Scheduled". 🔴 CoreFlow.md §3.9 records that the column is stored AS SENT and
       has no check constraint, and that a lower-case "attended" silently stops counting toward clinician
       hours in spStaff_GetPerformance. This endpoint only ever creates NEW bookings, so accept "Scheduled"
       and refuse anything else — do not accept a case-insensitive variant and normalise it, because
       accepting a shape you then rewrite hides the caller's bug.
     • 🔴 pjAppTypeId: MUST EQUAL THE STRING "01". Put it in a named constant with a comment saying this is
       AgentApiPlan.md decision 4 — surveillance option C, propose-only — that the agent never books a "04"
       SURVEILLANCE appointment because the coordinator opens the range and books those by hand, and that a
       "04" booked here could never be clinically recorded anyway since GetJourneyTemplate recognises exactly
       three strings and "04" is not one of them (CoreFlow.md §7.3). Say in the same comment exactly what to
       change to move to option A. "01" is A STRING WITH A LEADING ZERO and is not 1.

3. Build an AppointmentSaveInput and call the EXISTING IDatabaseData.SaveAppointmentAsync. 
   🔴 PatientAppointment_ID = 0 — this endpoint only ever INSERTS. It never updates and never deletes.
   Comment that, and comment that there is NO cancellation concept in nucentra (CoreFlow.md §3.9): a wrong
   booking consumes a real clinician hour and can only be undone with POST /Patient/DeleteAppointment, by a
   human, in the portal. That is why the agent's design puts a coordinator's approval in front of this call.
   🔴 DO NOT WRITE A SECOND BOOKING PATH. The transaction, the slot lock, the in-transaction availability
   check, the contiguity check and the slot assignment all live inside SaveAppointmentAsync and there is no
   correct way to reimplement them (CoreFlow.md §6.7, §12 #1).

4. MAP THE RESULT:
     • Ok(new { success = true, appointmentId = result.PatientAppointment_ID })
     • Otherwise Ok(new { success = false, reason = result.Reason.ToString(), message = "…" }) — 🔴 A 200 WITH
       success:false, NOT a 4xx. Comment why: SlotTaken means somebody booked that hour between the agent's
       slot read and this write, which is an EXPECTED outcome of an advisory read (CoreFlow.md §6.7, §13.4),
       and the caller re-runs slot discovery on it. A 4xx would make a normal race look like a client bug.
     • 🔴 `reason` IS THE ENUM NAME, VERBATIM — "SlotTaken", "SlotsNotConsecutive", "SlotNotFound". The caller
       branches on that string, so it is a public contract: do not lower-case it, do not prettify it, do not
       map two reasons onto one. `message` is the human sentence and may differ from PatientController's
       wording — comment that the data layer decides WHAT FAILED and the controller decides WHAT THE CALLER IS
       TOLD, which is the argument AppointmentSaveFailure's own comment makes at length.
     • Note in a comment that SlotWrongStaff and SlotWrongDate are currently unreachable and why
       (AppointmentSaveFailure documents it) — a caller must still handle them, because "unreachable today" is
       not "impossible tomorrow".

5. AUDIT IT. Call AgentAuditLog with a new method for a booking — patient id, appointment id, staff id,
   branch id, date, slot ids — 🔴 ONLY AFTER SaveAppointmentAsync HAS RETURNED SUCCESSFULLY. Never inside a
   flow that might roll back (CoreFlow.md §11.3). Log the outcome on the failure path too, with the reason.
   Never log the key.

VERIFY BEFORE YOU FINISH — report each, and do them in this order:

  a) `dotnet build CRC.Web/CRC.Web.csproj` — 0 errors, 0 new warnings.

  b) THE TWO NEGATIVE TESTS on the POST: no key and a wrong key must both give 401.

  c) 🔴 THE ANTIFORGERY TEST. POST with a valid key and NO X-CSRF-TOKEN header. It must NOT return 400. A 400
     here means [IgnoreAntiforgeryToken] is missing or not applying — the global
     AutoValidateAntiforgeryTokenAttribute is rejecting the call. This is the failure mode that only ever
     shows up on the first POST, which is why it is tested here and not in Prompt 1.

  d) THE HAPPY PATH, END TO END. Using your local CRC_DB: find or create a branch, a staff member and an open
     StaffSlot for a future date through the PORTAL'S OWN SCREENS (not by INSERT), read the slot id from
     GET /api/agent/slots/open, then POST the booking. Paste the request and the response. Then confirm in the
     portal that the appointment appears on the patient's Appointment tab, and confirm with sqlcmd that the
     slot's PatientAppointment_ID is NO LONGER NULL.

  e) 🔴🔴 THE AUDIT ASSERTION — THE MOST IMPORTANT CHECK IN THIS ENTIRE PLAN:
        sqlcmd -S localhost -d CRC_DB -E -C -Q "SELECT TOP 5 a.AuditTrail_Id, a.User_Id, u.Username, a.AuditTrail_Action, a.AuditTrail_Category, a.AuditTrail_Summary FROM dbo.AuditTrails a LEFT JOIN dbo.Users u ON u.User_ID = a.User_Id ORDER BY a.AuditTrail_Id DESC"
     PASTE THE ROWS VERBATIM. `Username` MUST READ `AGENT_SERVICE`. If User_Id is 0 or NULL or the Username is
     blank, the principal is not reaching DatabaseHelper.CurrentUserId — STOP, fix it, and re-run. Do not
     report this prompt as done with a failing actor; everything else it built is worthless without it.

  f) THE FAILURE PATHS, DRIVEN DELIBERATELY. Report the response for each:
        • pjAppTypeId "04"           → refused, before any data call
        • status "attended"          → refused
        • appointmentDate "01/09/2026" → refused, naming yyyy-MM-dd
        • slotIds [999999]           → reason "SlotNotFound"
        • the SAME slot id booked twice in a row → reason "SlotTaken" on the second
        • two NON-CONSECUTIVE slot ids on the same day → reason "SlotsNotConsecutive"
     🔴 The last three prove SaveAppointmentAsync's typed reasons reach the wire intact, which is the thing the
     caller branches on. If you cannot construct the non-consecutive case from your local data, say so
     explicitly rather than skipping it silently.

  g) CHECK CRC.Web/Logs/audit-*.log for your booking line, and app-*.log for exceptions. Paste the audit line.

THEN WRITE UP WHAT YOU LEARNED — CoreFlow.md §13.5:
  the endpoint, its exact request body and both response shapes; the full list of `reason` values with what
  each means and what a caller should do about it; 🔴 the "01"-only rule with decision 4 named and the one-line
  change that relaxes it; the 200-not-4xx convention and why; and the audit assertion from (e) with the rows
  pasted, presented as the health check anyone can re-run. If any failure path behaved differently from what
  this prompt predicted, THAT is the sentence worth writing.

WHEN DONE: lead with (e), then (c), then (f). Then the happy path from (d) and every file touched. Then edit
AgentApiPlan.md — tick the Prompt 3 box and set Prompt 3's Status to "✅ Done".

DO NOT touch Azure. DO NOT edit any .js, .cshtml or .sql file. DO NOT add a new stored procedure, a new
IDatabaseData method, or a second write path of any kind. DO NOT edit CoreFlow.md §2.2; Prompt 4 owns it.
```

---

## Prompt 4 — Harden, document, hand off

**Status:** ⬜ Not started
**Depends on:** Prompts 0–3

> **The feature works before this prompt runs.** What it does not yet have is a true `CoreFlow.md`: §2.2 still
> claims the portal has two public endpoints, §10 still describes three projects, and §11's checklist does not
> mention that a machine-callable endpoint has no cookie. This prompt closes the gap between the document and
> the code, and produces the two artefacts the owner needs to deploy: a smoke script and an app-settings sheet.

```text
You are an AI coding agent on the CRC Portal ("nucentra") — ASP.NET Core 10 MVC + a classic SSDT database
project. Fresh chat — no prior memory. Repo root CRC_Portal.

WHAT'S ALREADY DONE (Prompts 0–3): the Agent API is built and working locally. Five stored procedures under
CRC.Database/Stored Procedures/Agent/, all registered in the .sqlproj. A seeded AGENT_SERVICE dbo.Users row.
CRC.Api, a class library whose AgentApiController CRC.Web loads as an application part, with EIGHT endpoints —
seven GETs and one POST — behind AgentApiKeyFilter. Five methods and five models in CRC.Data. CoreFlow.md
§13.0 through §13.5 are written.

WHY: the code is right and the specification is now partly WRONG. CoreFlow.md §2.2 states that a grep for
AllowAnonymous returns two lines and that this is a complete audit of the portal's public surface; it now
returns three. §10 describes three projects. Those sentences are load-bearing for anyone auditing this repo,
and a specification that is quietly false is worse than one that is missing. This prompt fixes them, finishes
§13, and produces what the owner needs to deploy.

READ FIRST:
  CoreFlow.md                       (ALL OF IT — you are editing five sections and you must not contradict
                                     the other seven)
  AgentApiPlan.md                   (this plan — "What this changes in CoreFlow.md", and the open items)
  Nucentra_WhatsApp_Agent_Plan.md   (§4.5 configuration and the platform lock-down; §10.1 the curl smoke test)
  DOCUMENTSTORAGE.md                (the Configuration section — the two-underscore rule, which you are
                                     restating for Agent__ApiKey. DO NOT EDIT THIS FILE.)
  CRC.Api/** (everything you are describing), CRC.Web/Program.cs, CRC.Web/appsettings.json

YOUR TASK (Prompt 4).

PART A — AMEND THE FOUR EXISTING CoreFlow.md SECTIONS. Surgically. Renumber nothing, delete nothing that is
still true, and do not restructure a section to make room.

1. 🔴 §2.2 — REWRITE THE "exactly two" PARAGRAPH. It currently reads that there are exactly two
   [AllowAnonymous] attributes, both on AccountController.Login, and that a grep is a complete audit returning
   two lines. Run the grep yourself first and report what it actually returns. Then rewrite the paragraph to
   say: THREE, the third being AgentApiController; that it is a deliberate, documented widening of the public
   surface; that AgentApiKeyFilter is the only thing closing it; and 🔴 that unlike the two login attributes —
   which guard a page and a sign-in — this one fronts patient names, phone numbers, screening results and
   clinician schedules, so its guard is not optional in the way a missing [Authorize] elsewhere would be
   (there, the global filter fails closed; here, there is nothing behind it). Point at §13.3. Keep the
   sentence about a grep being a complete audit — it is still TRUE and still useful, just with a different
   number.

2. §10 — the file map. "Three projects in one solution" becomes four; the dependency line becomes
   `CRC.Web → CRC.Api → CRC.Data`. Add a CRC.Api/ block to the tree in the same style as the others, listing
   its real files with one-line roles. In the CRC.Database block, add `Agent/ (5)` to the Stored Procedures
   inventory and correct the procedure count from 104 to 109 EVERYWHERE it appears in the file — grep for
   "104" and fix every hit, including the ones in the document's opening paragraph and the §5 heading. Add a
   line to the repo-root listing for AgentApiPlan.md, describing it the way DapperLayerPlan.md is described:
   a finished plan, and history.
   🔴 Also update the "🔴 CRC.Data HAS NO REFERENCE TO CRC.Web" note: it is still true and still important, and
   it now has a sibling — CRC.Api has no reference to CRC.Web either, for the different reason that CRC.Web
   references IT.

3. §11.3 — add ONE checklist item, in the existing voice: a machine-callable endpoint has no cookie and
   therefore no principal, so its actor comes from a resolved service account and not from a signed-in user's
   claim; naming §13.3, and naming the silent failure (AuditTrails.User_Id = 0). Do not restructure the list.

4. §12 — add a new numbered locked decision at the END of the list, in the existing voice: CRC.Api is a CLASS
   LIBRARY loaded as an MVC application part, not a second host and not a second App Service. Record what that
   buys (one deployment, one config, one Serilog pipeline, the correlation id and the connection string for
   free), what it costs (the §2.2 count, and two small duplicated infrastructure files that cannot be shared
   because the reference runs the other way), and what re-opening it would mean. Then amend decision #2 with
   one sentence confirming that the Agent API changed nothing about it: SqlData is STILL the only file in the
   solution that names a stored procedure, and CRC.Api contains no SQL.

PART B — FINISH §13.

5. §13.6 — Configuration, deployment and the platform lock-down. Write, from the code:
     • The ONE app setting: Agent__ApiKey. 🔴 TWO underscores — App Service's section separator — and a single
       underscore is silently ignored, which starts the app with an empty key. Note that an empty key FAILS
       CLOSED (the filter's first branch) and that this is correct but confusing to debug, and say where the
       LogError lands. Same rule as DocumentStorage; point at DOCUMENTSTORAGE.md rather than restating it.
     • That NO service user id is configured anywhere, and why — dbo.Users.User_ID is an IDENTITY, so the
       agent account's id on Azure SQL is not the id on localhost. Point at §13.3.
     • The publish order: DACPAC first (it carries both the five procedures and the AGENT_SERVICE seed), then
       the web app. "Remove additional files at destination" stays UNCHECKED, as the deployment guide says.
     • 🔴 THE PLATFORM LOCK-DOWN, stated as a requirement and not a nicety: the API key is the authentication;
       an App Service access restriction limiting /api/agent/* to n8n's egress addresses is what stops the
       internet from reaching it at all. Say plainly that without it, the entire security of this surface is
       one shared secret in an HTTP header, and that a leaked key is a full read of the patient register until
       someone rotates it. Note that rotation is a one-setting change plus an app restart, and that no code
       change is needed — that is worth knowing before it is needed.
   🔴 DO NOT EDIT Nucentra_Azure_Deployment_Guide.md. The owner owns it and performs every Azure action by
   hand. INSTEAD, at the end of your final report, output a READY-TO-PASTE markdown section — written in that
   guide's voice and numbering style — that the owner can drop in as a new §17, covering the app setting, the
   publish order and the access restriction, click by click. Do not write it into the file.

6. §13.7 — What is deliberately not here. Short, honest, and each item with its reason:
     • No n8n. Nothing in this repo knows the agent exists.
     • No SURVEILLANCE booking — decision 4, option C. The negative path is a coordinator's job.
     • No slot creation, no cancellation, no patient creation, no document access, no staff writes.
     • No per-caller keys, no key rotation schedule, no OAuth, no rate limiting on /api/agent/* beyond the
       platform access restriction — and say which of those you would reach for FIRST if a second consumer
       ever appears, and why.
     • No test project — nucentra has none, and this feature did not start one.

PART C — THE TWO ARTEFACTS THE OWNER NEEDS.

7. CREATE a smoke-test script at the repo root — `Test-AgentApi.ps1`. It takes -BaseUrl and -ApiKey as
   parameters, defaulting BaseUrl to https://localhost:7276, and drives every one of the eight endpoints plus
   BOTH negative tests, printing a PASS/FAIL line per check. 🔴 It must NOT default the key to anything, must
   not read a key from a file, and must not write one to the console or to disk. The POST check is
   opt-in behind a -IncludeWrite switch, defaulting OFF, with a comment saying why: it consumes a real
   clinician hour and there is no cancellation in nucentra (CoreFlow.md §3.9), so a smoke test must not book
   by accident. Model the shape on the curl block in Nucentra_WhatsApp_Agent_Plan.md §10.1, but in PowerShell
   to match the repo's other tooling.

8. RUN IT against your local site with a valid key and PASTE THE FULL OUTPUT. Every check must pass. Then run
   it with a deliberately wrong key and confirm it fails in the way it should — a script whose failure path
   has never been seen is not a test.

PART D — THE FINAL CONSISTENCY PASS.

9. 🔴 READ ALL OF §13 END TO END AND CHECK IT AGAINST THE OTHER TWELVE SECTIONS FOR CONTRADICTIONS. You wrote
   it across five prompts with no memory between them. Specifically verify, and report on each:
     • §13's procedure count and endpoint count agree with §5's and §10's numbers.
     • Nothing in §13 contradicts §0.1 about @User_ID, §6.7 about SaveAppointmentAsync, or §12 about anything.
     • Every "CoreFlow.md §n" reference inside CRC.Api's comments points at a section that exists and says
       what the comment claims. Grep CRC.Api for "CoreFlow" and check every hit.
     • §13.0's forward references ("Prompt N builds this") are gone — the whole thing is now written in the
       present tense, as built. §13 is a specification now, not a plan.

10. Verify the whole thing still builds and runs: `dotnet build CRC.Web/CRC.Web.csproj`, the MSBuild rebuild of
    CRC.Database with EXACTLY TWO SQL71502 warnings, the site starting, a human logging in and loading a
    normal page, and Test-AgentApi.ps1 passing. Report all four.

WHEN DONE: lead with the §2.2 grep — before and after. Then the Test-AgentApi.ps1 output, then the
contradiction report from (9), then the ready-to-paste Azure section from (5). Then edit AgentApiPlan.md — tick
the Prompt 4 box, set Prompt 4's Status to "✅ Done", and add one short "What actually shipped" note at the
bottom of the Definition of done section recording anything that turned out differently from what this plan
predicted.

DO NOT touch Azure. DO NOT edit Nucentra_Azure_Deployment_Guide.md, DOCUMENTSTORAGE.md, SEEDING.md or
DapperLayerPlan.md. DO NOT edit any .js, .cshtml or .sql file. DO NOT add an endpoint.
```

---

## Open items — decide before go-live

These are **not** blockers for the five prompts. They are decisions the owner should take before n8n points at
a production key, and each one is recorded here so it is not discovered later.

1. **🔴 `AGENT_SERVICE` is a real ADMIN account.** This plan follows `Nucentra_WhatsApp_Agent_Plan.md` §4.1 and
   seeds it with `User_Type = 2`. `spUsers_ValidateLogin` selects any row by username, so **if anyone ever
   learns or resets its password, it is a working portal administrator.** The password is a discarded random
   secret, so this is theoretical — but there is a cheap hardening available: seed it with a `User_Type` that
   **no policy admits** (all five policies require `1`, `2` or `3`), so even a successful login lands on a
   principal that is refused by every page. The cost is that the account then displays with an unknown type on
   any screen that maps the integer to a name. **Recommendation: take it.** It costs one digit and removes the
   only path by which this row could ever become a way in.

2. **Rate limiting on `/api/agent/*`.** `CRC.Web` already has a rate limiter installed with a `login-ip`
   policy; adding an `agent-api` policy is about six lines in `Program.cs` plus an attribute. This plan leaves
   it out deliberately — `Nucentra_WhatsApp_Agent_Plan.md` §4.5 puts the perimeter at the platform layer with
   an App Service access restriction, and one control is easier to reason about than two half-controls.
   **Revisit the moment the access restriction is not in place**, because then the key is the only thing
   between the internet and `/patients/queue`, and a key can be brute-forced given enough requests.

3. **Key rotation.** There is one key, shared by one caller, with no version, no overlap window and no
   expiry. Rotating it is a one-setting change plus a restart — and a hard cutover, so n8n must be updated in
   the same minute. If that is unacceptable, the change is to make `AgentApiOptions.ApiKey` a string array and
   have the filter accept any member, which allows an overlap. **Decide before the first rotation, not
   during one.**

4. **The `NO_PHONE` patients.** `spAgentPatient_ListScreeningQueue` classifies them and the API returns them,
   and then nothing happens — the agent digests them to a coordinator and stops. `Patient_Phone` is
   `VARCHAR(100) NOT NULL` but nothing stops it being an empty string or a placeholder. **That may be a real
   gap in the programme worth closing in the portal**, with validation on the patient form rather than in the
   agent. Out of scope here; named because this API is what makes the gap visible for the first time.

5. **PDPA.** Unchanged from `Nucentra_WhatsApp_Agent_Plan.md` §3.9 and §12.3, and this plan does not attempt to
   answer it. Note that Part 4 alone moves no clinical data outside the portal — it exposes it to one
   authenticated caller inside your own network perimeter. The PDPA question arrives with **WhatsApp**, not
   with this API.

6. **Reverting to surveillance option A or B.** Decision 4 chose option C, and endpoint 8 enforces `"01"`.
   Moving to option A is one constant. Moving to option B needs a **ninth endpoint** wrapping
   `spStaffSlots_CreateRange` — which is an ACTOR procedure, so it would be the agent's **second** write, and
   it would let an automation create clinician availability months ahead. That is a bigger decision than it
   looks; re-open it deliberately, with the clinicians.

---

## Definition of done

All five boxes ticked in the Progress Tracker, and every one of these true:

- [ ] `dotnet build CRC.Web/CRC.Web.csproj` — 0 errors, 0 new warnings. Four projects in `CRC_Portal.slnx`.
- [ ] `CRC.Database` MSBuild `/t:Rebuild` — `Build succeeded`, `0 Error(s)`, and **exactly two** `SQL71502`
      warnings. 🔴 **Two.** Three means the row generator went in after all.
- [ ] The portal itself is unchanged: a human logs in, every existing page loads, no endpoint's JSON moved.
      No `.js`, `.cshtml` or existing `.sql` file was edited.
- [ ] `Test-AgentApi.ps1` passes all eight endpoints **and both negative tests** against a running local site.
- [ ] 🔴 `dbo.AuditTrails`, joined to `dbo.Users`, names **`AGENT_SERVICE`** on an appointment booked through
      endpoint 8. Not `0`, not blank, not a human's id.
- [ ] 🔴 A `grep` for `AllowAnonymous` returns **three** lines, and `CoreFlow.md` §2.2 **says three**.
- [ ] The full NRIC does not appear in any agent API response, and `grep -r NRIC CRC.Api` returns only the
      last-4 projection and comments about it.
- [ ] `CoreFlow.md` §13 is written end to end in the present tense, §2.2 / §10 / §11 / §12 are amended, and
      nothing is renumbered.
- [ ] `SEEDING.md` records two seeded `dbo.Users` rows.
- [ ] The owner has the ready-to-paste Azure section from Prompt 4 and has not been asked to run anything
      themselves that this plan could have proved locally.

**Then, and only then, §5 of `Nucentra_WhatsApp_Agent_Plan.md` becomes the next thing to read** — the WhatsApp
Business setup, which is the long pole and should have been started in parallel with all of the above.
