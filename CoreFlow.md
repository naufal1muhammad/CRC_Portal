# nucentra — CoreFlow: the CRC Portal, as built (Specification)

This document is the **single source of truth for what the CRC Portal is**: its domain, its authorization
model, its 28 tables, its 104 stored procedures, every controller action, the Dapper layer that connects
them, and the two audit channels that record what they did. It describes the system **as it is built
today** — not as it was, and not as it might be.

> **Audience:** anyone, human or AI agent, about to change this repo. **Read the section that covers what
> you are touching before you touch it**, and if you are adding a feature, read **§11** first — it is the
> ordered checklist, and every step in it points back at the section that explains why.
>
> **This is a specification, not a change log.** Nothing here is a proposal, nothing here records what the
> code used to do, and nothing here is aspirational. Where a piece of the product is genuinely unfinished,
> missing, or wrong, it says so **in those words** and stops there — §7.7 and §4.4's `SaveStaff` note are
> the pattern. A statement in this document is a claim about the current code that you can go and check.
>
> **Where it disagrees with something else, this file loses to two documents and wins against the rest.**
> `DOCUMENTSTORAGE.md` is authoritative on blob storage (§8); `SEEDING.md` is authoritative on what a
> published database contains. Everything else — including `DapperLayerPlan.md`, which is a finished plan
> and now history — defers to this file.

> ## ⚠️ THE SECTION NUMBERS ARE LOAD-BEARING
>
> All twelve sections are written. **Do not renumber any of them.** Code comments across
> `CRC.Data/Data/`, `CRC.Data/Models/` and `CRC.Web/Controllers/` already cite `CoreFlow.md §n` by number,
> and a renumbering silently invalidates every one of those references without breaking a build.
>
> Sections **3, 4 and 5 are organised by feature area** under `###` sub-headings (`3.1`…`3.17`,
> `4.1`…`4.15`, `5.1`…`5.10`). A new table, a new endpoint or a new procedure is **appended as a new
> sub-heading inside the existing section**, keeping the numbering monotonic. If you need a thirteenth
> topic, add **§13** — do not shuffle §10, §11 or §12 aside to make room.

**The map, so you can jump straight to what you need:**

| § | What it answers |
|---|---|
| **0** | The conventions to apply rather than re-derive — layering, naming, response shape, **and the `@User_ID` rule (§0.1), which is the one thing here that fails silently** |
| **1** | What a colorectal-cancer screening portal actually does, in one paragraph |
| **2** | Who can see what: `UserType` 1/2/3, five policies, antiforgery, lockout |
| **3** | The 28 tables, grouped, with the columns and the missing constraints that matter |
| **4** | Every page and every controller action, with its policy and the exact JSON it returns |
| **5** | All 104 stored procedures: parameters, result sets, which method calls each, `@User_ID` kind |
| **6** | The Dapper layer — `IDatabaseData`, `SqlData`, `DatabaseHelper`, Models, and the two transactions |
| **7** | The patient journey, the core feature — **including, explicitly, that there is no state machine** |
| **8** | Documents: two families, the settings layer that makes one mandatory, the endpoints |
| **9** | The two audit channels, why they are not the same thing, and how to check the actor mechanism |
| **10** | Folder structure / file map — where everything lives and why |
| **11** | End-of-feature checklist — the ordered steps for adding a feature, with the traps named |
| **12** | Decisions locked — settled deliberately, not to be re-opened without a reason |

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
`SqlData` passes the value explicitly, per call, and the distinction below is something every author must
hold in their head.

🔴 **THAT AUTO-INJECTION MACHINERY NO LONGER EXISTS.** `TryInjectUserIdAsync`, `SupportsUserIdParameterAsync`
and the static support cache behind them were **deleted** along with the rest of `DatabaseHelper`'s ADO
surface (§6.5). It is not coming back, and re-introducing it would be a mistake rather than a convenience:
a generic injector cannot see the difference between the two meanings below, and applying it to
`spUsers_Unlock` would unlock the wrong account. `@User_ID` is now **always** written out at the call site.

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

**All five of the TARGET procedures are `spUsers_*`, and the default is the whole tell** — these five
declare `@User_ID INT` with *no* default, where all nineteen audit-actor procedures declare
`@User_ID INT = NULL`. That is not a coincidence to be memorised: it is a rule you can apply to a procedure
you have never seen. A `@User_ID` with a default is a bookkeeping parameter the caller is allowed to omit;
a `@User_ID` without one is a business argument the procedure cannot run without. Every one of the five is
in the `WHERE` clause of a statement over `dbo.Users`, and not one of them writes a `dbo.AuditTrails` row
(§5.3). If you find yourself reaching for `DatabaseHelper.CurrentUserId` inside a `spUsers_*` call, stop.

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

> ### 🔴 If you have come from HEART, read this paragraph before anything else
>
> **nucentra has no permission-key model. There are no permissions, no roles table, no role assignments,
> and nothing to configure.** Authorization is **one integer on `dbo.Users`**, carried as one claim, checked
> by five `RequireClaim` policies. That is the entire mechanism.
>
> HEART's `PermissionsLogins_Brief.md` describes a `Permissions` / `Roles` / `RolePermissions` / `UserRoles`
> system where endpoints are guarded by *keys* (`Roles.Manage`) so that new roles can be added without
> touching endpoint code, and a Role Management screen maintains the mapping. **None of that exists here.**
> There is no `dbo.Permissions`, no `dbo.Roles`, no `dbo.RolePermissions`, no `dbo.UserRoles`, and no
> equivalent of `HeartPermissionKeys.cs`. Adding a fourth kind of user to nucentra means adding a policy in
> `Program.cs` and an attribute to every action that should allow it — a code change and a redeploy, not an
> admin screen. Do not go looking for the tables; do not "restore" them; do not assume an endpoint is
> guarded by a key it does not have.

### 2.1 The `UserType` claim — the whole of authorization

`dbo.Users.User_Type` is an `INT NOT NULL` with exactly three meaningful values:

| `User_Type` | Role name | Lands on | What it is |
|---|---|---|---|
| **1** | `SUPERUSER` | `/Dashboard/Index` | Full administrator. The only type that may manage branches, users, document settings, the Documents search and the audit trails. |
| **2** | `ADMIN` | `/AdminDashboard/Index` | Operational administrator: patients, appointments, staff, the tracker. |
| **3** | `STAFF` | `/StaffDashboard/Index` | A clinician. The only type that **must** have a `Staff_ID` — `spUsers_Register` refuses a blank one, refuses a `Staff_ID` that is not in `dbo.Staff`, and refuses one already linked to another account. One staff member, at most one login. |

**Nothing constrains the column.** There is no check constraint, no foreign key and no lookup table for
user types; the three values are a convention held in `Program.cs`, in `AccountController` and in the
`RegisterUserRequest` DTO's `= 3` default. A row with `User_Type = 7` inserts fine, satisfies no policy, and
its holder can reach only the actions that require authentication alone.

The claims are built in **exactly one place** — `AccountController.Login` (POST), after the password
verifies — and are the only thing the rest of the product ever sees:

| Claim | Value | Read by |
|---|---|---|
| `ClaimTypes.NameIdentifier` | `User_ID` as a plain integer string | **`DatabaseHelper.CurrentUserId`**, which `int.TryParse`s it into `dbo.AuditTrails.User_Id` for all 19 audit-actor procedures (§0.1). A non-numeric value here silently audits everything as user `0`. |
| `ClaimTypes.Name` | `Username` | `User.Identity.Name`; the `[User:…]` field of every log line; the actor named in `AuditLog.AccountUnlocked`. |
| `"UserType"` | `"1"` / `"2"` / `"3"` — **a string** | All five authorization policies. |
| `ClaimTypes.Role` | `"SUPERUSER"` / `"ADMIN"` / `"STAFF"` | `RedirectToLanding`, which decides the post-login page. **Nothing else uses roles** — no action is guarded by `[Authorize(Roles = …)]`. |
| `"FullName"`, `"UserEmail"` | display only | The layout's user menu. |
| `"StaffId"` | `dbo.Users.Staff_ID` | Added **only when `User_Type = 3` and the id is non-blank**. Every staff-scoped page reads it — `MyProfileStaff`, `StaffSchedule`, `StaffPatient`. An ADMIN or SUPERUSER has no such claim, which is why those pages take an id in the query string instead. |

**The role claim and the `UserType` claim carry the same fact in two encodings, and they can disagree.**
`UserType` is the string from the database; the role is derived from it by an `if/else` whose final branch
is `else → "STAFF"`. So `User_Type = 7` produces `UserType = "7"` **and** `Role = "STAFF"` — it satisfies no
policy but does land on the staff dashboard. Guard with the policy, never with the role.

### 2.2 Everything is authenticated by default

```csharp
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());                        // authentication, globally
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());  // antiforgery, globally
});
```

The global `AuthorizeFilter` means **an action with no attributes at all still requires a signed-in user**.
Forgetting `[Authorize]` on a new controller fails closed, which is the right default and the reason the
codebase can be read without hunting for gaps.

**There are exactly two `[AllowAnonymous]` attributes in the entire web project**, both on
`AccountController.Login` — the GET that renders the page and the POST that signs you in. Nothing else in
nucentra is reachable without a cookie: not the access-denied page, not logout, not an error page, not a
health check (there is none). A `grep` for `AllowAnonymous` is a complete audit of the portal's public
surface, and it returns two lines.

### 2.3 The five policies, and which controllers use them

```csharp
options.AddPolicy("SuperUserOnly",        policy => policy.RequireClaim("UserType", "1"));
options.AddPolicy("AdminOrSuper",         policy => policy.RequireClaim("UserType", "1", "2"));
options.AddPolicy("AdminOnly",            policy => policy.RequireClaim("UserType", "2"));
options.AddPolicy("StaffOnly",            policy => policy.RequireClaim("UserType", "3"));
options.AddPolicy("AdminOrSuperOrStaff",  policy => policy.RequireClaim("UserType", "1", "2", "3"));
```

Every one is a **string comparison on a claim value** — `RequireClaim` with several values is an OR. There
is no requirement handler, no resource-based authorization and no policy that looks at anything but this one
claim.

| Policy | Types | Applied to |
|---|---|---|
| `SuperUserOnly` | 1 | **class:** `BranchController`, `DashboardController`, `SettingsController`, `DocumentsController`, `AuditTrailsController` · **action:** `Account.Register`, `Account.RegisterUser`, `Account.GetUsers`, `Account.UnlockUser` |
| `AdminOrSuper` | 1, 2 | **class:** `AdminDashboardController`, `AppointmentController`, `PatientController`, `PatientTrackerController` · **action:** **seventeen of `StaffController`'s eighteen** |
| `AdminOrSuperOrStaff` | 1, 2, 3 | **class:** `MyProfileStaffController`, `StaffScheduleController`, `StaffPerformanceController` · **action:** `StaffController.GetStaff` — the *only* one of its eighteen — and **eleven** actions on `StaffPatientController`: every read **plus all five document actions** |
| `StaffOnly` | 3 | **class:** `StaffDashboardController` · **action:** **four** on `StaffPatientController` — the three clinical writes (assessment, colonoscopy, follow-up) **and `Details`, the page itself** |
| `AdminOnly` | 2 | 🔴 **nothing.** |

Two entries in that table are easy to get wrong from memory and are stated exactly, counted attribute by
attribute against the two controllers that carry per-action policies:

- **`StaffController`'s single `AdminOrSuperOrStaff` action is `GetStaff`, not `GetStaffTypes`.**
  `GetStaffTypes` is `AdminOrSuper` like the other sixteen. `GetStaff` is the exception because
  `/MyProfileStaff` reads a clinician's own record through it, and it carries its own ownership check on
  top of the policy (§4.4).
- **`StaffPatientController` splits eleven / four, and `Details` is on the `StaffOnly` side.** The page is
  `StaffOnly` while every endpoint it calls is `AdminOrSuperOrStaff` — an administrator reaching those
  endpoints directly gets the data and simply has no page to render it in (§4.9).

**`AdminOnly` is declared and never used — not by one controller, not by one action.** It is dead
configuration. Do not read its existence as evidence that some screen is ADMIN-exclusive; nothing is. Every
place an ADMIN can reach, a SUPERUSER can reach too, because every policy that admits `"2"` also admits
`"1"`. The one asymmetry in the product runs the other way: `StaffOnly` excludes the SUPERUSER, so a
SUPERUSER genuinely cannot open the staff dashboard or record a clinical result.

`StaffPatientController` is the only controller that mixes policies per action, and the split is
deliberate: **reads are `AdminOrSuperOrStaff`, clinical writes are `StaffOnly`.** An administrator may look
at a patient journey; only a clinician may record one.

Actions with **no** policy — `Account.ChangePassword` (both overloads), `Account.AccessDenied`,
`Account.Logout`, and `Account.GetPasswordPolicy` / `GetSessionTimeout` (which carry a bare `[Authorize]`) —
are open to **every authenticated user of any type**, which is correct: they are about your own session.

### 2.4 Antiforgery — global, header-named, and HTTPS-bound

`AutoValidateAntiforgeryTokenAttribute` is a **global filter**, so **every non-GET action is validated
whether or not it says `[ValidateAntiForgeryToken]`**. The two places that do say it — both `Login` POST and
`ChangePassword` POST — are redundant and harmless; keep them, because they document intent at the one
place a reader looks.

```csharp
options.HeaderName        = "X-CSRF-TOKEN";
options.Cookie.Name       = "__Host-CSRF";
options.Cookie.SameSite   = SameSiteMode.Strict;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
options.Cookie.HttpOnly   = true;
options.Cookie.Path       = "/";
```

Form posts carry the token in the `__RequestVerificationToken` field; the 59 JavaScript files carry it in
the **`X-CSRF-TOKEN`** header.

🔴 **The `__Host-` cookie prefix is a browser-enforced rule, not a naming style.** A cookie so named is
rejected outright unless it is `Secure`, has `Path=/`, and has **no `Domain` attribute** — and `Secure`
means HTTPS. Over plain HTTP the browser never stores it, so **every POST fails antiforgery validation with
a 400 and no useful message**. This is why local testing must use the `https` launch profile
(`https://localhost:7276`) and not `http://localhost:5289`, and why "my POSTs all return 400" is almost
always "I am on the wrong port".

### 2.5 The session cookie

```csharp
options.LoginPath         = "/Account/Login";
options.LogoutPath        = "/Account/Logout";
options.AccessDeniedPath  = "/Account/AccessDenied";
options.ExpireTimeSpan    = TimeSpan.FromSeconds(sessionTimeout.InactivityTimeoutSeconds);  // 600 s
options.SlidingExpiration = true;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
options.Cookie.HttpOnly     = true;
options.Cookie.SameSite     = SameSiteMode.Lax;
```

Cookie authentication, **no server-side session store** — the ticket is the cookie. `ExpireTimeSpan` comes
from `Account:SessionTimeout:InactivityTimeoutSeconds` in `appsettings.json` (**600 seconds, 10 minutes**),
and with `SlidingExpiration = true` it is an **inactivity** timeout: the cookie is reissued once more than
half its life has elapsed, so a user who keeps clicking is never signed out. `SignInAsync` passes
`IsPersistent = false`, so nothing survives closing the browser.

The client's countdown reads the same number from `GET /Account/GetSessionTimeout`, which exists only so
that the warning dialog and the cookie cannot drift apart. **Changing the timeout means changing
`appsettings.json` — one value feeds both.**

Note the two cookies deliberately differ: the auth cookie is `SameSite=Lax` (it must survive a top-level
navigation back into the site), the antiforgery cookie is `SameSite=Strict`.

### 2.6 Two independent brute-force defences

They protect against different attacks and neither substitutes for the other.

**Per-IP rate limiting** — a fixed-window limiter registered as the `"login-ip"` policy and applied with
`[EnableRateLimiting("login-ip")]` to **`Login` (POST) only**. Nothing else in the portal is rate limited.

| Setting (`Account:LoginLockout`) | Value | Meaning |
|---|---|---|
| `IpRequestsPerWindow` | 10 | login POSTs permitted per window, partitioned by remote IP |
| `IpRateLimitWindowSeconds` | 60 | the window |

`QueueLimit = 0`, so request 11 is **rejected, not queued**: HTTP 429, a `Retry-After` header, the plain-text
body `"Too many login attempts from this address. Please wait and try again."`, and an
`AuditLog.LoginRateLimited` line. This is the defence against *credential stuffing* — many usernames from
one address — which per-account lockout cannot see. It partitions on `RemoteIpAddress`, so a NAT gateway
shares one budget and a botnet defeats it; that is the accepted trade.

**Per-account lockout** — the defence against guessing *one* password, with its counters on `dbo.Users`.

| Setting (`Account:LoginLockout`) | Value | Meaning |
|---|---|---|
| `MaxFailedAttempts` | 5 | lock on the **5th** failure (`@CurrentCount >= @MaxFailedAttempts`) |
| `LockoutMinutes` | 15 | how long `Lockout_End_Utc` is set ahead |
| `AttemptWindowMinutes` | 15 | failures further apart than this do not accumulate |

The whole mechanism is `spUsers_RegisterFailedLogin` plus three columns (§3.3, §5.3). **The thresholds live
in configuration and are passed into the procedure per call** — the database holds no policy of its own, so
changing them is an `appsettings.json` edit with no publish.

Four behaviours that matter and are not obvious:

- **A locked account is refused *before* the password is checked**, in `Login`, and that path does **not**
  increment the counter. A locked-out account is therefore not a password oracle, and an attacker cannot
  extend someone's lockout by hammering it.
- **The counter is a sliding window, not a lifetime total.** If the previous failure is older than
  `AttemptWindowMinutes`, the procedure resets the count to 0 before incrementing — so the stored
  `Failed_Login_Count` can read `4` on an account that is one failure away from nothing.
- **"Locked" is not a column.** It is `Lockout_End_Utc > UtcNow`, computed by the caller. An expired lockout
  leaves the timestamp sitting in the row (the procedure's `COALESCE` never clears it) until the next
  successful login or a SUPERUSER unlock does — which is why `GET /Account/GetUsers` returns both
  `lockoutEndUtc` and a separately computed `isLocked`.
- **The two clocks are not the same clock.** The procedure decides with `GETUTCDATE()` (SQL Server); the
  controller decides with `DateTime.UtcNow` (the web server). On one machine they agree. Split across an
  App Service and Azure SQL they agree only as well as both are synchronised, and the error shows up as a
  lockout that is minutes longer or shorter than 15.

Clearing a lockout has exactly two routes: **log in successfully** (`spUsers_ResetFailedLogins`), or **a
SUPERUSER unlocks the account** (`POST /Account/UnlockUser` → `spUsers_Unlock`). There is no self-service
unlock, no email, no "forgot password", and **no password reset of any kind** — a user who forgets their
password needs someone with database access, because `ChangePassword` requires the current one.

### 2.7 What is *not* here

Stated plainly, because each one is a thing a reader may reasonably expect to find:

- **No permission keys, roles table or role management screen** (see the box at the top of this section).
- **No password reset and no "forgot password" flow.** `ChangePassword` demands the current password.
- **No `MustChangePassword` column and nothing that forces the seeded SUPERUSER password to be changed.**
  `SEEDING.md` publishes `ChangeMe!123` in source control and asks nicely.
- **No password history**: `spUsers_UpdatePassword` overwrites `Password_Hash` and keeps nothing. The only
  reuse rule is "must differ from the current one", enforced in the controller by comparing the two
  *plaintext* form fields — so re-using the password from two changes ago is allowed.
- **No multi-factor authentication, no external identity provider, no API keys or bearer tokens.** Cookie
  authentication is the only scheme registered.
- **No account disable/enable and no delete.** `dbo.Users` has no `IsActive` column; removing someone's
  access means deleting the row by hand in SQL.
- **No per-branch or per-organization scoping.** An ADMIN sees every patient at every branch. The only
  data-scoping claim in the product is `StaffId`, and it scopes a staff member to *their own* schedule and
  profile, not to a site.

---

## 3. Data model

> **All 28 tables in `CRC.Database/dbo/Tables/` are covered below**, one `###` sub-section per table except
> §3.1, which takes the twelve `LU_*` reference tables together. A new table gets the next number.
>
> 🔴 **Read the "no foreign key" notes rather than skimming them.** nucentra has **FIVE enforced foreign
> keys in the whole 28-table schema**, and here they are, in full: `LU_LOCATION.ParentId` → itself (§3.1);
> `PatientAssessment`, `PatientColonoscopy` and `PatientFollowUp` → `PatientJourney` (§3.12–§3.14); and
> `StaffSlots.PatientAppointment_ID` → `PatientAppointment` (§3.7). **Every other relationship in this
> product is a `VARCHAR(100)` column holding somebody else's key by convention** — `Staff.Staff_Base`,
> `Users.Staff_ID`, `PatientAppointment.Patient_ID`, every `LU_*` code, all of them. The referential
> integrity that exists lives inside stored procedures, where a direct `INSERT` or `DELETE` bypasses it
> entirely (§3.4, §5.4). Two of the five are also load-bearing in a way their direction does not suggest:
> the three journey FKs are what make deleting a patient half-succeed (§7.7), and the `StaffSlots` one
> points the wrong way to protect a slot from deletion (§3.7).

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

### 3.3 `dbo.Users`

Every login in the portal. **One row is one account**, and an account is not a person and not a staff
member — see the `Staff_ID` note below.

| Column | Type | Null | Notes |
|---|---|---|---|
| `User_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK. **One of the few numeric keys in nucentra** (§0) — most business ids are `VARCHAR(100)`. Its string form becomes the `NameIdentifier` claim and thence `dbo.AuditTrails.User_Id`. |
| `User_Name` | `VARCHAR(100)` | NOT NULL | Display name ("SYSTEM SUPERUSER"). Not the login. |
| `Username` | `VARCHAR(100)` | NOT NULL | The login. **`UNIQUE INDEX IX_Users_Username`** — the only uniqueness constraint on the table, and the reason `spUsers_Register` can pre-check it. |
| `User_Email` | `VARCHAR(100)` | NOT NULL | Display only. **Nothing validates it, nothing is ever sent to it**, and it is not unique — two accounts may share an address. |
| `Password_Hash` | `VARCHAR(500)` | NOT NULL | See below. |
| `User_Type` | `INT` | NOT NULL | 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF. **No check constraint and no lookup table** (§2.1). |
| `Staff_ID` | `VARCHAR(100)` | **NULL** | A `dbo.Staff.Staff_ID`, **by convention only — there is no foreign key.** Required for `User_Type = 3`, NULL for everyone else. |
| `Created_At` | `DATETIME` | NOT NULL | `DEFAULT (GETUTCDATE())`. UTC. |
| `Last_Login` | `DATETIME` | NOT NULL | `DEFAULT (GETUTCDATE())`. UTC. |
| `Failed_Login_Count` | `INT` | NOT NULL | `DEFAULT (0)`. |
| `Last_Failed_Login_At` | `DATETIME` | **NULL** | UTC. NULL until the first failure. |
| `Lockout_End_Utc` | `DATETIME` | **NULL** | UTC. NULL until the first lockout. |

**`Last_Login` is `NOT NULL` with a default, so a brand-new account reads as though it had just logged in.**
`spUsers_Register` stamps `Created_At` and `Last_Login` with the same `GETUTCDATE()`, which means
`lastLogin == createdAt` is the portal's only signal for "has never signed in" — and it is a guess, not a
fact. There is no nullable "never logged in" state to distinguish.

#### The password hash

`Password_Hash` holds a **PBKDF2 hash produced by `Microsoft.AspNetCore.Identity.PasswordHasher<string>`**,
Base64-encoded, in the Identity **V3** layout — decoding a seeded row's prefix `AQAAAAIAAYagAAAAE…` gives
marker `0x01`, PRF `2` (**HMAC-SHA512**), **100,000 iterations**, **128-bit salt**, 256-bit subkey. Two facts
follow, and both are load-bearing:

- **The hash is salted per row.** Two accounts with the same password have different values here, so
  comparing two hashes for equality answers nothing and a hash cannot be used as an identifier.
- 🔴 **`PasswordHasher<string>` takes the *user* as its first argument, and nucentra passes the
  `Username`.** Every call in `AccountController` — `HashPassword(username, …)`,
  `VerifyHashedPassword(username, storedHash, …)` — passes the username, and it must keep doing so.
  (`PasswordHasher<T>` ignores that argument in its current implementation, which is exactly why an
  inconsistency here would compile, pass every test, and only break if the implementation ever changed.
  Consistency is free; do not vary it.)

The hash is **never** returned to a browser, never logged and never put on an audit line. Two of the three
read procedures select it (`spUsers_ValidateLogin`, `spUsers_GetById`) and are mapped onto models that say
so in a comment; `spUsers_GetAll`, whose result reaches the browser, does not select it at all — which is
why it gets its own model (`UserListItem`) with no hash property to leak.

#### Referential integrity: there is none

`dbo.Users` has **no foreign keys in either direction**. `Staff_ID` is not constrained to `dbo.Staff`, and
nothing references `User_ID` — including `dbo.AuditTrails.User_Id`, which is a plain `INT` holding one.
Three consequences:

- Deleting a staff member leaves their login pointing at nothing; the account still signs in and its
  `StaffId` claim resolves to no staff row.
- Deleting a user account orphans every `dbo.AuditTrails` row that named them. **`User_Id = 0` in the audit
  trail therefore has two possible meanings** — the actor parameter was dropped (§0.1), or the actor's
  account no longer exists. Neither is distinguishable after the fact.
- `spUsers_Register` enforces by hand what the schema does not: `Staff_ID` must exist in `dbo.Staff` and
  must not already be linked to another account. **Those are checks in one procedure, not constraints**, so
  a direct `INSERT` bypasses all of them.

#### The seeded row

Exactly one row is seeded (`CRC.Database/Scripts/Seed_Users.sql`, guarded on `Username`, so a re-publish
never resets it): `SUPERUSER` / `ChangeMe!123`, `User_Type = 1`, `Staff_ID` NULL. See `SEEDING.md`. Nothing
in the application forces that password to be changed — there is no `MustChangePassword` column (§2.7).

### 3.4 `dbo.Staff`

A clinician. Typed by `LU_STAFFTYPE`, based at a `Branch`, the owner of published `StaffSlots`, and the
staff member named on every `PatientAppointment` and every `PatientJourney` step.

| Column | Type | Null | Notes |
|---|---|---|---|
| `Staff_ID` | `VARCHAR(100)` | NOT NULL | PK, **generated by `spStaff_Insert`** — see below |
| `Staff_Name` | `VARCHAR(100)` | NOT NULL | |
| `Staff_NRIC` | `VARCHAR(100)` | NOT NULL | the national identity number. **Not unique** — nothing stops two staff rows sharing one |
| `Staff_BirthDate` | `DATETIME` | NOT NULL | |
| `Staff_Age` | `INT` | NOT NULL | **stored, not derived.** It is whatever the form posted and drifts out of date on its own |
| `Staff_Phone` | `VARCHAR(100)` | NOT NULL | |
| `Staff_Email` | `VARCHAR(100)` | NOT NULL | display only; nothing is ever sent to it and it is not unique |
| `Staff_Gender` | `VARCHAR(100)` | NOT NULL | free text — no lookup table and no check constraint |
| `Staff_ResState` | `VARCHAR(100)` | NOT NULL | the state's **`Name`**, not its `LocationId` — same denormalization as `Branch_State` (§3.2) |
| `Staff_ResCity` | `VARCHAR(100)` | NOT NULL | the city's `Name` |
| `Staff_ResPostcode` | `VARCHAR(100)` | NOT NULL | the postcode's `Name`, i.e. the postcode itself |
| `Staff_AddLine1` | **`VARCHAR(MAX)`** | NOT NULL | |
| `Staff_AddLine2` | **`VARCHAR(MAX)`** | NOT NULL | |
| `Staff_Base` | `VARCHAR(100)` | NOT NULL | a `Branch.Branch_ID`, **by convention only** |
| `Staff_Type` | `VARCHAR(100)` | NOT NULL | a `LU_STAFFTYPE.StaffType_ID` — `"END"`, `"NUR"`, `"ANE"`, `"ENT"`, `"GAS"` — **by convention only** |

**`PK_Staff` is the only constraint on the table. There are no foreign keys, in either direction, and no
unique index besides the key.** `Staff_Type` is not constrained to `LU_STAFFTYPE`, `Staff_Base` is not
constrained to `dbo.Branch`, and nothing references `Staff_ID` — `Users.Staff_ID`,
`StaffDocument.Staff_ID`, `StaffSlots.Staff_ID`, `PatientAppointment.Staff_ID`, `PatientJourney.Staff_ID`
and `PatientJourneyAudit.Staff_ID` are all plain `VARCHAR(100)` columns holding one. Every read that shows
a staff type name is a `LEFT JOIN`, which is why `StaffType_Name` is nullable on both
`CRC.Data/Models/StaffListItem.cs` and `StaffDetail.cs`: a `Staff_Type` that no longer matches a lookup row
is a state the schema permits.

🔴 **Referential integrity for `dbo.Staff` lives entirely inside `spStaff_Delete`** — three `IF EXISTS`
checks that refuse the delete, and four `DELETE` statements that cascade by hand (§5.4). A direct
`DELETE FROM dbo.Staff` bypasses all seven, orphaning appointments and leaving a login pointing at nothing.

**The two address representations are not the same shape as the branch's.** A branch stores only its state,
by name; a staff member stores state, city and postcode, all three by **name**, while the edit form drives
its cascading dropdowns with `LU_LOCATION`'s integer ids. So the ids the form works in are never persisted
and the names it persists are never joined back — nothing detects a staff member whose city is not in the
state they claim.

**How a `Staff_ID` is generated** (read `spStaff_Insert`): a prefixed sequence, not an identity.

```
Staff_ID  =  {Staff_Type}  +  '-'  +  {sequence, 5 digits, zero-padded}
                 "END"                        "00003"
          →  "END-00003"
```

**The prefix is the `LU_STAFFTYPE` code the form selected** — the same three-letter mnemonic stored in
`Staff_Type` (§3.1), which is why a staff id says what kind of clinician it is at a glance. The procedure
`RAISERROR`s on a blank `@Staff_Type` before doing anything else, because a blank prefix would produce the
id `"-00007"`.

Three things about the sequence, all of them shared with `spBranch_Insert` (§3.2) and none of them what the
shape suggests:

1. **It is global, not per prefix.** `MAX(TRY_CAST(RIGHT(Staff_ID, 5) AS INT))` scans the *whole* table, so
   the third staff member overall is `…-00003` whatever their type. Verified on an empty table: an
   ENDOSCOPIST, then an ENDOSCOPY TECHNICIAN, then a REGISTERED NURSE came out as `END-00001`, `ENT-00002`,
   `NUR-00003` — one shared counter, three prefixes.
2. **It reuses numbers.** The `MAX` is over surviving rows only, so deleting the newest staff member makes
   the next insert re-issue its number. A *different* type then yields a genuinely new id; the **same**
   type collides on the primary key and the insert fails with a `SqlException`. Empty table → `…-00001`.
3. It caps at 99999 (`VARCHAR(5)`), and `RIGHT('00000' + …, 5)` would wrap a six-digit sequence back to its
   last five digits rather than failing.

**A staff member's id never changes, including when their type does.** `spStaff_Update` re-writes
`Staff_Type` like any other column, so a nurse promoted to endoscopist keeps `NUR-00003` while their type
reads `END`. The prefix records what they were when the row was created, not what they are.

### 3.5 `dbo.StaffDocument`

One uploaded file belonging to one staff member — a CV, a registration certificate, an indemnity
membership. The bytes are **not** here; they are in the private Azure Blob container, and this row is the
catalogue entry that points at them. See `DOCUMENTSTORAGE.md` and §8.

| Column | Type | Null | Notes |
|---|---|---|---|
| `StaffDocument_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK. One of the few numeric keys in nucentra (§0) |
| `Staff_ID` | `VARCHAR(100)` | NOT NULL | a `Staff.Staff_ID`, **by convention only** |
| `StaffDocumentType_ID` | `VARCHAR(100)` | **NULL** | a `LU_STAFFDOCUMENTTYPE.StaffDocumentType_ID`, by convention only. **The only nullable column on the table** |
| `FileName` | `VARCHAR(255)` | NOT NULL | the user's file name after `DocumentValidation.SafeFileName` — path stripped, bounded to 255 because *this column is 255* |
| `BlobName` | `VARCHAR(500)` | NOT NULL | **the key inside the private container**, e.g. `staff/END-00003/4b7a….pdf`. Not a URL and not a filesystem path |
| `ContentType` | `VARCHAR(100)` | NOT NULL | |
| `UploadedOn` | `DATETIME` | NOT NULL | see below |

**`PK_StaffDocument` is the only constraint on this table** — verified against the live database with
`sys.objects`, not just against the `.sql`. In particular **there is no foreign key on
`StaffDocumentType_ID`**, so an arbitrary string inserts happily and the document simply stops matching any
lookup row. (`spStaffDocument_LookupDocuments` exists precisely because that can happen: it unions the
types actually in use with the types in the lookup, so an orphaned type is still searchable — §5.4.)

🔴 **`UploadedOn` IS THE ONE TIMESTAMP IN NUCENTRA THAT IS NOT UTC.** `spStaffDocument_Insert` stamps it
`CAST(GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time' AS DATETIME)` — Malaysian
local time, stored with no offset and no marker saying so. Every `dbo.Users` timestamp is UTC and §4.3
`SpecifyKind`s them before formatting; doing that here would shift a document eight hours into the future.
The documents table renders this value with a plain `.ToString()` in the **server's current culture**
(`"8/6/2026 9:28:03 PM"`), not ISO-8601 — the one endpoint in the portal whose dates are not machine-shaped.

**Two documents uploaded in one request share a timestamp to the second**, which is why
`spStaffDocument_List` orders by `UploadedOn DESC, StaffDocument_ID DESC`. The tiebreak is part of the
contract, not decoration.

**Nothing enforces one document per type.** A staff member may hold three CVs; the mandatory-document rule
(§4.4) asks only whether *at least one* row of each required type exists.

### 3.6 `dbo.StaffDocumentSettings`

Which document types a staff type is *required* to have. Four columns, and the interesting thing about it
is what is absent.

| Column | Type | Null | Notes |
|---|---|---|---|
| `StaffType_ID` | `VARCHAR(100)` | NOT NULL | PK part 1. A `LU_STAFFTYPE.StaffType_ID` |
| `StaffType_Name` | `VARCHAR(100)` | NOT NULL | denormalized copy of the name |
| `StaffDocumentType_ID` | `VARCHAR(100)` | NOT NULL | PK part 2. A `LU_STAFFDOCUMENTTYPE.StaffDocumentType_ID` |
| `StaffDocumentType_Name` | `VARCHAR(100)` | NOT NULL | denormalized copy of the name |

`PK_StaffDocumentSettings` is a **composite key over the two ids**, so one pair can be configured once. As
everywhere else, neither id is a foreign key, and both `_Name` columns are copies that go stale the moment
a lookup row is renamed.

🔴 **THERE IS NO `IsMandatory` COLUMN. THE EXISTENCE OF THE ROW IS THE FLAG.** A row means "an ENDOSCOPIST
must have a CV"; no row means nothing. The `IsMandatory` that appears in JSON and in
`CRC.Data/Models/StaffDocumentSetting.cs` is **computed by `spStaffDocumentSettings_GetByStaffType`** as
`CASE WHEN s.StaffDocumentType_ID IS NULL THEN 0 ELSE 1 END` over a `LEFT JOIN` — an `INT`, not a `BIT`,
because an unaliased `CASE` over integer literals is typed `INT`. It exists so the Settings screen can
render every document type as a checklist with the configured ones ticked. **Do not go looking for the
column, and do not add one**: a settings row is deleted, not un-flagged
(`spStaffDocumentSettings_DeleteByStaffType`, §5.9).

**An empty table means nothing is mandatory anywhere**, which is the state a freshly published `CRC_DB` is
in — every staff type saves with no documents at all until somebody configures the Settings screen.

### 3.7 `dbo.StaffSlots`

**One row is one hour of one staff member's published availability.** An administrator (or a clinician, for
themselves) opens a range of hours in advance; booking a `PatientAppointment` consumes one or more of them.
This is the only table in nucentra that is *pre-created empty and then consumed* — everything else is
written when the fact it records happens.

| Column | Type | Null | Notes |
|---|---|---|---|
| `StaffSlot_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK. One of the few numeric keys in nucentra (§0) — and **sequential, which is a security fact, not a detail**; see §4.5 |
| `Staff_ID` | `VARCHAR(100)` | NOT NULL | a `Staff.Staff_ID`, **by convention only — no foreign key** |
| `SlotDate` | `DATE` | NOT NULL | date only, no time component |
| `SlotStartTime` | `TIME(0)` | NOT NULL | whole seconds; **always on the hour**, by check constraint |
| `SlotEndTime` | `TIME(0)` | NOT NULL | **always exactly one hour after the start**, by check constraint |
| `PatientAppointment_ID` | `INT` | **NULL** | 🔴 **NULL is what "available" means.** See below |

🔴 **`PatientAppointment_ID` BEING NULL *IS* AVAILABILITY. There is no `IsBooked` column, no status, and no
"released" state.** A slot with a null appointment id is open; a slot with one is consumed by that
appointment. Every availability decision in the portal — the schedule grid's rendering, the appointment
form's slot picker, and the concurrency check inside `PatientController.SaveAppointment` — is that one
`IS NULL` test. Adding a status column would create a second answer to a question that already has one.

**This is the most constrained table in nucentra, and the only one whose rules are in the schema rather than
in a procedure.** Everywhere else — `dbo.Staff`, `dbo.Branch`, `dbo.Users` — referential integrity lives
inside stored procedures (§3.4, §5.4) and a direct `INSERT` bypasses it. Here it does not:

```sql
CK_StaffSlots_OnTheHour   CHECK (DATEPART(MINUTE, SlotStartTime) = 0 AND DATEPART(MINUTE, SlotEndTime) = 0)
CK_StaffSlots_OneHour     CHECK (SlotEndTime = DATEADD(HOUR, 1, SlotStartTime))
FK_StaffSlots_PatientAppointment   FOREIGN KEY (PatientAppointment_ID) REFERENCES PatientAppointment(...)
UX_StaffSlots_Staff_ID_SlotDate_SlotStartTime   UNIQUE (Staff_ID, SlotDate, SlotStartTime)
```

Four things follow, and each is load-bearing somewhere:

- **A slot is always exactly one on-the-hour hour.** A half-hour or a two-hour slot cannot be inserted at
  all — not by a procedure, not by hand. `spStaffSlots_CreateRange` re-checks the on-the-hour rule anyway
  and `THROW`s a friendlier message before the constraint can fire (§5.5); the constraint is the backstop.
- **The unique index is what makes opening a range idempotent.** `spStaffSlots_CreateRange` `MERGE`s against
  exactly those three columns, so re-opening a range that is already open creates nothing and errors on
  nothing — it just reports the hours as skipped (§5.5).
- 🔴 **The one foreign key in this area points the wrong way for deletion.** It constrains
  `StaffSlots.PatientAppointment_ID` to an existing appointment; it does **not** stop the *slot* being
  deleted out from under a live appointment. That protection is a `THROW` inside `spStaffSlots_Delete`
  (§5.5) and nowhere else, so a direct `DELETE FROM dbo.StaffSlots` orphans the appointment silently.
- **Deleting a staff member deletes their slots**, by hand, inside `spStaff_Delete` (§5.4) — but only when
  that delete is not already blocked by a `PatientAppointment` reference, which a booked slot implies.

#### 🔴 The two baseline `SQL71502` warnings — they live here, and they are expected

Building `CRC.Database.sqlproj` reports **exactly two warnings, both in
`Stored Procedures/StaffSlots/spStaffSlots_CreateRange.sql`, at lines 46 and 52**:

```
SQL71502: Procedure: [dbo].[spStaffSlots_CreateRange] has an unresolved reference to object [sys].[all_objects].
```

**What they are.** The procedure builds its day list and its hour list with the standard
`SELECT TOP (n) ROW_NUMBER() OVER (…) FROM sys.all_objects` trick — a system catalogue view used purely as
a row generator, because nucentra has no numbers table. `sys.all_objects` is not part of the project model,
so SSDT cannot resolve the reference and warns. The reference is perfectly valid at runtime; every SQL
Server has that view.

**They pre-date the Dapper migration, they are the *whole* of the database project's warning output, and
they are the pass condition, not a defect.** A `CRC.Database` build is correct when it says
`Build succeeded`, `0 Error(s)` and these two warnings and nothing else. Do not "fix" them by adding a
master database reference or by rewriting the row generator: the first adds a build dependency for a
cosmetic gain, and the second changes a procedure this plan is not permitted to change. **If the count is
anything other than two, something you did caused it.**

### 3.8 `dbo.PatientBasic`

**The registration record every other patient table hangs off.** A person reaches the centre through one of
the nine routes in `LU_SOURCE` and becomes one row here; `PatientAppointment`, `PatientJourney`,
`PatientDocument`, `PatientAssessment`, `PatientColonoscopy` and `PatientFollowUp` all key on its
`Patient_ID`. **Twenty-seven columns, and exactly one constraint** — the primary key, and even that is
unnamed (`PK__PatientB__C1A88B59…`, server-generated, because the DDL writes `PRIMARY KEY` inline). Verified
against the live database with `sys.objects` and `sys.indexes`, not just the `.sql`: no foreign keys in
either direction, no unique index besides the key, no check constraint and no default.

#### Identity

| Column | Type | Null | Notes |
|---|---|---|---|
| `Patient_ID` | `VARCHAR(100)` | NOT NULL | PK, **generated by `spPatientBasic_Insert`** — see below |

#### Demographics

| Column | Type | Null | Notes |
|---|---|---|---|
| `Patient_Name` | `VARCHAR(100)` | NOT NULL | **upper-cased by the controller**, not by the database |
| `Patient_Email` | `VARCHAR(100)` | NOT NULL | display only; nothing is ever sent to it, nothing validates it, **not unique** |
| `Patient_Phone` | `VARCHAR(100)` | NOT NULL | |
| `Patient_NRIC` | `VARCHAR(100)` | NOT NULL | **12 digits, no dashes** — see the NRIC section. **Not unique**: nothing stops two patients sharing one |
| `Patient_BirthDate` | `DATETIME` | NOT NULL | 🔴 **DERIVED from the NRIC server-side, never taken from the form** |
| `Patient_Age` | `INT` | NOT NULL | **stored, not computed** — it is the age on the day the row was last saved and drifts out of date on its own, exactly like `Staff.Staff_Age` (§3.4) |
| `Patient_Gender` | `VARCHAR(100)` | NOT NULL | 🔴 **DERIVED from the NRIC** — `"MALE"` or `"FEMALE"`. Free text in the schema: no lookup table, no check constraint |
| `Race_ID` | `VARCHAR(100)` | NOT NULL | `LU_RACE.Race_ID`, **by convention only** |
| `Source_ID` | `VARCHAR(100)` | NOT NULL | `LU_SOURCE.Source_ID`, by convention only |
| `Religion_ID` | `VARCHAR(100)` | NOT NULL | `LU_RELIGION.Religion_ID`, by convention only |
| `MaritalStatus_ID` | `VARCHAR(100)` | NOT NULL | `LU_MARITALSTATUS.MaritalStatus_ID`, by convention only |
| `Occupation_ID` | `VARCHAR(100)` | NOT NULL | `LU_OCCUPATION.Occupation_ID`, by convention only |

#### Residential address and emergency contact

| Column | Type | Null | Notes |
|---|---|---|---|
| `Patient_ResState` | `VARCHAR(100)` | NOT NULL | the state's **`Name`**, not its `LocationId` |
| `Patient_ResCity` | `VARCHAR(100)` | NOT NULL | the city's `Name` |
| `Patient_ResPostcode` | `VARCHAR(100)` | NOT NULL | the postcode's `Name`, i.e. the postcode itself |
| `Patient_AddLine1` | **`VARCHAR(MAX)`** | NOT NULL | |
| `Patient_AddLine2` | **`VARCHAR(MAX)`** | **NULL** | 🔴 **the only optional free-text field on the whole form** |
| `Patient_EmergencyName` | `VARCHAR(100)` | NOT NULL | **upper-cased by the controller** |
| `Patient_EmergencyRelationship` | `VARCHAR(100)` | NOT NULL | free text — no lookup table |
| `Patient_EmergencyNumber` | `VARCHAR(100)` | NOT NULL | |

**The address is the same denormalization `dbo.Staff` uses (§3.4)**: three names persisted, three integer
`LU_LOCATION` ids used to drive the cascading dropdowns and then thrown away. Nothing detects a patient
whose city is not in the state beside it, and nothing joins these columns back to the location tree.

#### Clinical — the iFOBT

| Column | Type | Null | Notes |
|---|---|---|---|
| `Patient_iFOBTStatus` | `BIT` | **NULL** | NULL = not recorded. 1 = complete, 0 = not complete |
| `Patient_iFOBTCompletionDate` | `DATE` | **NULL** | the one `DATE` column here; the other three dates are `DATETIME` |
| `Patient_iFOBTResults` | `BIT` | **NULL** | 1 = positive — which is what opens a patient journey (§1) |

🔴 **The two completion columns cannot outlive the status, and that rule is enforced twice.** Both
`spPatientBasic_Insert` and `spPatientBasic_Update` compute
`CASE WHEN @Patient_iFOBTStatus = 1 THEN @Patient_iFOBTCompletionDate ELSE NULL END` before writing, so a
caller that sends a completion date with a status of 0 or NULL stores NULL. `PatientController.SaveBasic`
clears the same two fields in C# beforehand, and `edit-basic.js` clears the inputs a third time when the
status dropdown changes. Three layers, one rule; none of them is redundant enough to be the one you remove.

#### Discharge — three columns whose NULL is the state machine

| Column | Type | Null | Notes |
|---|---|---|---|
| `DischargeType_ID` | `VARCHAR(100)` | **NULL** | `LU_DISCHARGETYPE.DischargeType_ID` — NORMAL, BENIGN POLYPS, PRECANCEROUS POLYPS, CANCER. By convention only |
| `Patient_DischargeDate` | `DATETIME` | **NULL** | |
| `Patient_DischargeRemarks` | **`VARCHAR(MAX)`** | **NULL** | |

🔴 **`DischargeType_ID IS NULL` IS THE DEFINITION OF AN ACTIVE PATIENT.** There is no status column, no
`IsActive` flag and no soft-delete marker anywhere on this table. `spPatientBasic_ListActive` filters
`IS NULL` and `spPatientBasic_ListDischarged` filters `IS NOT NULL`, so the two lists partition the table
with no row in both and no row in neither. Adding a status column would create a second answer to a
question that already has one — the same argument §3.7 makes about `StaffSlots.PatientAppointment_ID`.

**Nothing keeps the three in step.** `spPatientBasic_Update` assigns all three unconditionally on every
call, so a `DischargeType_ID` with a NULL date is a state the schema and the procedure both permit — which
is why `PatientDischargedItem.Patient_DischargeDate` is nullable even though the list's own filter implies
it is set. It is also why **saving a patient with the discharge cleared genuinely un-discharges them**: the
portal's only route back to "active" is a side effect of the update being a full-row overwrite, not a
feature anyone designed.

#### Audit — there is none on this table

**No `Created_At`, no `Modified_At`, no `Created_By`, no `Modified_By`, no row version.** Who created or
last touched a patient exists only in `dbo.AuditTrails`, written by the three write procedures themselves
(§5.6), and that trail is keyed by a summary *string* rather than by a foreign key — so it survives the
patient being deleted and cannot be joined back to one. If you need "when was this patient registered",
there is no column that answers it; the nearest thing is the `INSERT` audit row's `AuditTrail_Id` ordering.

#### 🔴 How a `Patient_ID` is generated, and what the prefix does *not* mean

Read `spPatientBasic_Insert`. It is composed, not an identity:

```
@LastNum      =  MAX(CAST(SUBSTRING(Patient_ID, 5, 6) AS INT))   over rows LIKE 'PAT-%'
Patient_ID    =  'PAT-'  +  RIGHT('000000' + CAST(@LastNum + 1 AS VARCHAR(6)), 6)
              →  "PAT-000042"
```

**The prefix is a constant, and that makes it the odd one out among nucentra's three composed ids.**
`Branch_ID` encodes the organization and the state (§3.2); `Staff_ID` encodes the staff type, so
`END-00003` says "endoscopist" at a glance (§3.4). `PAT-` says only "patient". There is one kind of patient,
so there is nothing for it to carry — do not go looking for meaning in it, and do not add any.

Three things about the sequence, shared with the other two and none of them what the shape suggests:

1. **`SUBSTRING(Patient_ID, 5, 6)` is 1-based**, so it reads characters 5–10 — the six digits after `PAT-`.
   The `WHERE Patient_ID LIKE 'PAT-%'` means a row inserted by hand under any other prefix is invisible to
   the `MAX` and cannot advance the counter.
2. **It reuses numbers.** The `MAX` is over surviving rows only, so deleting the newest patient makes the
   next insert re-issue its number, and deleting *all* of them restarts at `PAT-000001`. Verified during
   Prompt 5's smoke test: five patients were created, all five deleted, and the next insert came back
   `PAT-000001`. Unlike `spStaff_Insert` there is no prefix to make the reissued id unique, so **the
   collision case here is a primary-key violation, full stop** — create A, create B, delete B, create C
   succeeds as B's old id; but restore B by hand first and C fails.
3. It caps at 999,999 (`VARCHAR(6)`), and `RIGHT('000000' + …, 6)` would wrap a seven-digit sequence back to
   its last six digits rather than failing. Empty table → `PAT-000001`.

**The insert validates nothing.** No `RAISERROR`, anywhere in the procedure — contrast `spBranch_Insert`
(which refuses a blank organization or an unknown state) and `spStaff_Insert` (which refuses a blank staff
type because it is the id's prefix). Two patients may share an NRIC, an email, a phone number and a name.
Every rule that exists is `PatientController`'s.

#### 🔴 The NRIC convention, and why the birth date and gender are never trusted from the client

`Patient_NRIC` holds the Malaysian national identity number **as twelve digits with the dashes stripped** —
`900215-10-1235` is stored `900215101235`. `PatientController.SaveBasic` removes every non-digit
(`nricRaw.Where(char.IsDigit)`) and refuses anything that is not then exactly twelve characters, with
*"NRIC must be exactly 12 digits."* The layout it relies on is `YYMMDD` + `PB` (place of birth) + `###` +
`G`, and it reads two things out of it:

| Derived | From | Rule |
|---|---|---|
| `Patient_BirthDate` | digits 1–6 | `YYMMDD`, century-pivoted (below), parsed `yyyy-MM-dd` with `DateTimeStyles.None` — an impossible date such as `889930…` fails |
| `Patient_Gender` | digit 12 | **odd → `"MALE"`, even → `"FEMALE"`** |

`Patient_Age` is then `CalculateAge(birthDate)` against `DateTime.Today` on the web server.

🔴 **All three are computed on the server on every save and the posted values are ignored.** The Edit form
shows a birth date, an age and a gender, `edit-basic.js` fills them in as you type the NRIC, and
`SaveBasicRequest` **has no property for any of them** — so there is nothing for a hostile client to send.
A patient's birth date cannot disagree with the identity number beside it, by construction. Two failures
have their own messages and both leave the row untouched: *"Invalid NRIC (unable to derive Birth Date)."*
and *"Invalid NRIC (unable to derive Gender)."*

**The century pivot is a rolling window, and it will be wrong for someone eventually.**

```csharp
var currentYY = DateTime.Today.Year % 100;          // 26 in 2026
var year = (yy <= currentYY) ? 2000 + yy : 1900 + yy;
```

So in 2026 an NRIC beginning `26` reads as **2026**, and a centenarian born in 1926 is recorded as a
newborn. The window shifts by one year every January, which means the same NRIC can derive a different
birth date in two consecutive years. It is the client's rule too — `deriveFromNric` in `edit-basic.js`
implements the identical pivot, so the two agree — and it is left exactly as found.

#### 🔴 Which columns are mandatory, and where that is actually enforced

**Twenty of the twenty-seven columns are `NOT NULL` and seven are nullable** (`Patient_AddLine2`, the three
iFOBT columns, the three discharge columns). That is the *whole* of the database's opinion, and it is
weaker than it looks, because **`NOT NULL` rejects a NULL and accepts an empty string**. Every "required
field" in this feature is required by C# and by nothing else:

| Enforced by | Columns | What happens if you bypass it |
|---|---|---|
| **`NOT NULL` only** | the 20 non-nullable columns | a direct `INSERT … VALUES ('')` succeeds; the patient has a blank name |
| **`PatientController.SaveBasic`, one `IsNullOrWhiteSpace` block** | `Patient_Name`, `Patient_Email`, `Patient_Phone`, `Patient_NRIC`, the five lookup ids, `Patient_ResState/City/Postcode`, `Patient_AddLine1`, and the three emergency-contact columns — **sixteen fields**, answered with *"Please fill in all mandatory fields."* | nothing at all stops it — not a constraint, not the procedure |
| **`PatientController.SaveBasic`, derived** | `Patient_BirthDate`, `Patient_Age`, `Patient_Gender` | they are never posted, so there is nothing to bypass |
| **nothing** | `Patient_AddLine2`, the iFOBT trio, the discharge trio | correct — they are genuinely optional |

**That gap is the thing to know before adding a field here.** A new column gets its `NOT NULL` from the
`.sql`, its two parameters from `spPatientBasic_Insert` and `spPatientBasic_Update`, its property from
`CRC.Data/Models/PatientSaveInput.cs` and `PatientBasicDetail.cs` — and its *requiredness* from a line in
that one `if` block in the controller. Miss the last one and the column is mandatory in the schema, blank
in practice, and nothing anywhere reports it.

Two more absences worth stating plainly, because both are things a reader may expect:

- **Nothing is unique except the primary key.** Not the NRIC, not the email, not the phone number. The
  portal has no duplicate-patient detection of any kind and no merge.
- **There is no soft delete.** A patient is removed by `spPatient_DeleteCascade`, which erases them and six
  dependent tables outright (§5.6). Contrast `dbo.Staff`, whose delete *refuses* while a clinician is still
  referenced — a patient with a completed journey is removed as readily as one registered five minutes ago.

### 3.9 `dbo.PatientAppointment`

**One booking: a patient, with a staff member, at a branch, on a date, for a whole number of on-the-hour
hours.** It is the row that turns a `StaffSlots` hour from available into consumed, and it is what a
`PatientJourney` step is scheduled as (§1).

| Column | Type | Null | Notes |
|---|---|---|---|
| `PatientAppointment_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK. One of the few numeric keys in nucentra (§0) — and, unlike `Branch_ID`, `Staff_ID` and `Patient_ID`, **it is a real identity and is not composed**: there is no prefix, no sequence scan and no number reuse. See below |
| `Patient_ID` | `VARCHAR(100)` | NOT NULL | a `PatientBasic.Patient_ID`, **by convention only** |
| `PatientAppointment_Date` | `DATE` | NOT NULL | date only, no time component |
| `Staff_ID` | `VARCHAR(100)` | NOT NULL | a `Staff.Staff_ID`, by convention only |
| `PatientAppointment_StartTime` | `TIME(0)` | NOT NULL | **always on the hour**, by check constraint |
| `PatientAppointment_EndTime` | `TIME(0)` | NOT NULL | on the hour, and **strictly after the start** |
| `PjAppType_ID` | `VARCHAR(100)` | NOT NULL | a `LU_PJ_APP_TYPE.PjAppType_ID`, by convention only |
| `Branch_ID` | `VARCHAR(100)` | NOT NULL | a `Branch.Branch_ID`, by convention only |
| `PatientAppointment_Status` | `VARCHAR(100)` | NOT NULL | see below — **not a lookup, not constrained** |

```sql
CK_PatientAppointment_OnTheHour   CHECK (DATEPART(MINUTE, StartTime) = 0 AND DATEPART(MINUTE, EndTime) = 0)
CK_PatientAppointment_TimeOrder   CHECK (EndTime > StartTime)
IX_PatientAppointment_Patient_ID                            (Patient_ID)
IX_PatientAppointment_Staff_ID_PatientAppointment_Date      (Staff_ID, PatientAppointment_Date)
```

**Two check constraints, two non-unique indexes, no foreign keys of its own, and — this is the one that
matters — NOTHING UNIQUE except the primary key.** In particular nothing stops two appointments claiming
the same clinician at the same hour: the only thing that does is the availability check inside
`SaveAppointmentAsync` (§6.7), which is why that check has to hold a transaction open rather than trusting
the database to catch a collision it has no constraint for.

**It is the only table in this area with an incoming foreign key**, and it points the wrong way for
deletion: `FK_StaffSlots_PatientAppointment` constrains `StaffSlots.PatientAppointment_ID` to an existing
appointment (§3.7), so **an appointment cannot be deleted while a slot still points at it**. That is what
makes `spPatientAppointment_Delete` release the slots before deleting the row rather than after — its own
comment calls the release "FK safety" (§5.7).

#### 🔴 The `StaffSlots` ↔ `PatientAppointment` relationship, stated once

**A slot is "available" when `StaffSlots.PatientAppointment_ID IS NULL`.** There is no `IsBooked` column,
no status and no released state anywhere in either table (§3.7). The whole lifecycle is that one column:

| Event | What happens to `StaffSlots.PatientAppointment_ID` | By |
|---|---|---|
| an administrator opens hours | rows are created with it **NULL** | `spStaffSlots_CreateRange` |
| an appointment is **booked** | **set** to the new appointment's id, on each chosen hour | `spStaffSlots_AssignAppointment` |
| an appointment is **edited** | **cleared** off every hour it held, then **set** on the new ones | `spStaffSlots_ClearAppointment` then `spStaffSlots_AssignAppointment` |
| an appointment is **deleted** | **cleared** back to NULL on every hour it held | `spPatientAppointment_Delete` itself |
| the patient is deleted | the appointment row goes, and 🔴 **the slot keeps the dead id** | `spPatient_DeleteCascade` — see below |

Four consequences, and each is load-bearing somewhere:

- **The two tables disagree about time, and the appointment is the summary.** A three-hour appointment is
  **three `StaffSlots` rows** — each exactly one hour, by check constraint — and **one
  `PatientAppointment` row** carrying only the first slot's start and the last slot's end. That is why the
  hours must be contiguous (§5.7): a booking with a gap would store a span that covers an hour it does not
  hold, and nothing in the schema would notice.
- **The appointment does not record which slots it consumed.** The link exists only on the slot side, so
  "which hours does this appointment hold" is a query against `StaffSlots`, and
  `spStaffSlots_ClearAppointment` is keyed on the **appointment id** rather than on a slot list precisely
  because the caller cannot be trusted to know the answer.
- **A slot whose appointment id equals THIS appointment is not "taken" during an edit.** Re-saving a
  booking over hours it already holds must succeed, and that single exception is what makes the edit path
  work at all (§5.7, §6.7).
- 🔴 **`spPatient_DeleteCascade` CANNOT DELETE A PATIENT WHO HOLDS A BOOKED SLOT, AND NOTHING SAYS SO.**
  It runs `DELETE FROM dbo.PatientAppointment WHERE Patient_ID = @Patient_ID` as its **first** statement
  (§5.6) and never mentions `dbo.StaffSlots` — it has no equivalent of `spPatientAppointment_Delete`'s
  `UPDATE dbo.StaffSlots SET PatientAppointment_ID = NULL`. Measured directly against `CRC_DB` with a
  patient, one appointment and one slot pointing at it:

  ```
  The DELETE statement conflicted with the REFERENCE constraint "FK_StaffSlots_PatientAppointment".
  The conflict occurred in database "CRC_DB", table "dbo.StaffSlots", column 'PatientAppointment_ID'.
  ```

  The patient row and the appointment row were both still there afterwards. **That it fails safely is
  luck, not design**: the conflict is on statement 1 of 7, and this procedure has no transaction (§5.6),
  so the same constraint firing later would have left the earlier tables already emptied. The user-facing
  effect is that `POST /Patient/DeletePatient` answers with the generic *"Error deleting patient."* and a
  correlation id, and the real reason reaches only `Logs/app-*.log`. Deleting the appointments first is
  what a caller has to do, and it is what the portal does in practice because the Appointment tab is where
  a booking is removed.

#### How a `PatientAppointment_ID` is generated — it is the exception

`INT IDENTITY(1,1)`, and that is the whole of it. **This is worth stating precisely because nucentra's
three *other* business keys are all composed strings** — `Branch_ID` encodes the organization and state
(§3.2), `Staff_ID` encodes the staff type (§3.4), `Patient_ID` is `PAT-` plus a scanned sequence (§3.8),
and all three **reuse numbers after a delete**. An appointment id does none of that: the database assigns
it, it never collides, and a deleted appointment's number is never re-issued.

The cost is that the id is **not returned by a `SELECT`**. `spPatientAppointment_Insert` hands it back
through `@NewPatientAppointment_ID INT OUTPUT` — the same shape as `spPatientBasic_Insert` and **not** the
trailing `SELECT` that `spBranch_Insert` and `spStaff_Insert` use (§5.7). It is also **sequential and
therefore guessable**, exactly like `StaffSlot_ID` (§3.7) — but unlike a slot id nothing in the portal
takes an appointment id as an ownership-free authorization input, so there is no equivalent of
`spStaffSlots_GetOwner` here.

#### 🔴 The status values, and where they are actually defined

`PatientAppointment_Status` is a `VARCHAR(100)` with **no check constraint, no default and no lookup
table** — there is no `LU_APPOINTMENTSTATUS`. The three real values are:

```
Scheduled        Attended        Not Attended
```

**They are defined in C#, four times, and nowhere else.** `PatientController.SaveAppointment` validates
against a `HashSet<string>(StringComparer.OrdinalIgnoreCase)` of those three; `AppointmentController.
UpdateAppointmentStatus` and `AdminDashboardController.UpdateAppointmentStatus` each declare their own
identical `HashSet`; and `PatientController.GetAppointmentLookups` returns a hard-coded `string[]` of them
for the booking form's dropdown. A row with `PatientAppointment_Status = 'Cancelled'` inserts fine by hand,
satisfies no screen, and is invisible to every filter that offers only the three.

**The comparison is case-insensitive on the way in and case-sensitive on the way out.** The `HashSet`s
accept `"attended"`, and the value is then stored **as the caller sent it** — no `ToUpperInvariant`, unlike
`Patient_Name` and `Patient_EmergencyName` (§3.8). Since `spPatientAppointment_Search`'s status filter is a
plain `=` against the database's collation, and `spPatientAppointment_LookupStatuses` returns `DISTINCT`
values verbatim, a row saved as `"attended"` would appear in the filter dropdown as its own separate
entry beside `"Attended"`.

**One status has real behaviour attached**: `spStaff_GetPerformance`'s hours-by-type grid sums only
appointments `WHERE PatientAppointment_Status = 'Attended'` (§5.5) — a plain equality, so the lower-case
variant above would silently stop counting toward a clinician's hours.

🔴 **A status change touches no slots.** Marking an appointment `"Not Attended"` leaves every hour it holds
consumed; only a delete or a re-save releases them. There is no cancellation concept in nucentra — the way
to free an hour is to delete the appointment.

### 3.10 `dbo.PatientJourney`

**The core table of the product, and one row is an EVENT, not a state.** It records that a clinical step
of one of the four `LU_PJ_APP_TYPE` kinds happened to this patient, on this date, under this clinician —
and nothing else. Read §7 before changing anything here.

| Column | Type | Null | Notes |
|---|---|---|---|
| `PatientJourney_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK, **unnamed** (`PK__PatientJ__F2372D7D…`, server-generated — the DDL writes `PRIMARY KEY` inline). One of the few numeric keys in nucentra (§0), a real identity like `PatientAppointment_ID` and unlike the three composed string keys |
| `Patient_ID` | `VARCHAR(100)` | NOT NULL | a `PatientBasic.Patient_ID`, **by convention only — no foreign key** |
| `PjAppType_Name` | `VARCHAR(100)` | NOT NULL | 🔴 the **denormalized NAME**, not the `LU_PJ_APP_TYPE` code, written as a literal by the create procedure. See below |
| `PatientJourney_Date` | `DATETIME` | NOT NULL | the **business** date the clinician chose — when the step happened clinically, not when the row was written |
| `Staff_ID` | `VARCHAR(100)` | NOT NULL | a `Staff.Staff_ID`, by convention only. 🔴 **NOT a `dbo.Users` id and not an audit actor** |
| `Created_At` | `DATETIME2(0)` | NOT NULL | `DEFAULT SYSUTCDATETIME()`. UTC, stamped by the database |
| `Updated_At` | `DATETIME2(0)` | **NULL** | NULL until the first `…_UpdateWithJourney` call |
| `CreatedBy_Staff_ID` | `VARCHAR(100)` | **NULL** | set to the same `Staff_ID` by every create |
| `UpdatedBy_Staff_ID` | `VARCHAR(100)` | **NULL** | set by every update |

```
PK__PatientJ__F2372D7D…            PRIMARY KEY (PatientJourney_ID)      -- unnamed
DF_PatientJourney_CreatedAt        DEFAULT SYSUTCDATETIME() ON Created_At
IX_PatientJourney_Patient_ID       (Patient_ID)                         -- non-unique
```

**A primary key, a default and one non-unique index. No foreign keys OUT — and three IN.** Nothing
constrains `Patient_ID` to `dbo.PatientBasic` or `Staff_ID` to `dbo.Staff`, but all three detail tables
point *at* this one with real, enforced foreign keys. **That direction is what makes deleting a patient
fail** (§3.13, §7).

🔴 **`PjAppType_Name` IS A STRING LITERAL INSIDE EACH CREATE PROCEDURE, AND ONE OF THE THREE DOES NOT
MATCH THE LOOKUP.**

| Written by | Literal | `LU_PJ_APP_TYPE` holds |
|---|---|---|
| `spPatientAssessment_CreateWithJourney` | `'PATIENT ASSESSMENT'` | `01 PATIENT ASSESSMENT` ✅ |
| `spPatientColonoscopy_CreateWithJourney` | `'COLONOSCOPY'` | `02 COLONOSCOPY` ✅ |
| `spPatientFollowUp_CreateWithJourney` | `'PATIENT FOLLOW UP'` | `03 FOLLOW UP` 🔴 **mismatch** |
| *(nothing)* | — | `04 SURVEILLANCE` — no journey row is ever created for it |

Nothing joins the column to the lookup, so **nothing detects the mismatch and nothing ever will**. The
string the procedure writes is the one the portal switches on: `GetJourneyTemplate` tests
`"PATIENT FOLLOW UP"`, and `spStaff_GetPerformance` counts `UPPER(PjAppType_Name) = 'COLONOSCOPY'` (§5.5).
`LU_PJ_APP_TYPE.PjAppType_ID` is used by `PatientAppointment.PjAppType_ID` — a *different column on a
different table* — so the two vocabularies never meet. Do not "fix" either side in isolation.

**The type is also the only thing that says which detail table to look in.** There is no discriminator
column, no pointer to the detail row and no `LEFT JOIN` anywhere that resolves it: `GetPatientAssessment`,
`GetPatientColonoscopy` and `GetPatientFollowUp` are three endpoints, each hard-wired to one procedure that
`INNER JOIN`s one table, and the caller picks by reading `PjAppType_Name` off the timeline first.

### 3.11 `dbo.PatientJourneyAudit`

🔴 **NUCENTRA'S SECOND DATABASE AUDIT TRAIL, AND IT IS NOT `dbo.AuditTrails`.**

| Column | Type | Null | Notes |
|---|---|---|---|
| `PatientJourneyAudit_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK |
| `PatientJourney_ID` | `INT` | NOT NULL | a `PatientJourney.PatientJourney_ID`, **by convention only — no foreign key**, which is why the rows survive the journey |
| `Audit_Action` | `VARCHAR(20)` | NOT NULL | no check constraint, no lookup. Two values are written: `CREATED`, `UPDATED` |
| `Audit_At` | `DATETIME2(0)` | NOT NULL | `DEFAULT SYSUTCDATETIME()`. UTC |
| `Staff_ID` | `VARCHAR(100)` | NOT NULL | 🔴 a **`Staff.Staff_ID`** — the clinician, not a `dbo.Users` id |
| `Audit_Note` | `VARCHAR(500)` | **NULL** | the free text the clinician typed on save |

```
PK_PatientJourneyAudit                    PRIMARY KEY (PatientJourneyAudit_ID)
DF_PatientJourneyAudit_AuditAt            DEFAULT SYSUTCDATETIME() ON Audit_At
IX_PatientJourneyAudit_JourneyId_AuditAt  (PatientJourney_ID, Audit_At)   -- non-unique
```

**Verified against the live database that there is no `User_Id` column of any kind on this table** — the
six columns above are the whole of it. The two trails answer different questions and neither substitutes
for the other:

| | `dbo.AuditTrails` | `dbo.PatientJourneyAudit` |
|---|---|---|
| Written by | 19 stored procedures, from `@User_ID` | the six `…WithJourney` procedures, from `@Staff_ID` |
| Keyed on | a **login** (`dbo.Users.User_ID`) | a **clinician** (`dbo.Staff.Staff_ID`) |
| Shape | one `CONCAT`ed summary string | five typed columns |
| Shown to a user | **never** — SUPERUSER-only `/AuditTrails` page | **yes** — it *is* the patient's timeline history |
| Retention | forever, in the table | forever, in the table |

🔴 **NOT ONE OF THE TWELVE JOURNEY PROCEDURES WRITES A `dbo.AuditTrails` ROW.** Measured on the running
site: creating and updating an assessment, a colonoscopy and a follow-up produced **six**
`PatientJourneyAudit` rows and **zero** `AuditTrails` rows —
`SELECT COUNT(*) FROM dbo.AuditTrails WHERE AuditTrail_Category IN ('PatientJourney','PatientAssessment','PatientColonoscopy','PatientFollowUp')`
returns `0` on a database where the whole flow has just been driven end to end. **Recording a colonoscopy
leaves no trace in nucentra's security trail.** That is a real gap; it is stated here rather than filled,
because filling it means editing a `.sql`.

**A row is written on exactly one occasion: a successful `…WithJourney` call, inside that procedure's own
transaction.** There is no `DELETE` action and no way to produce one — see the orphaning in §7.

### 3.12 `dbo.PatientAssessment`

The first journey type's detail: what the patient's risk factors, symptoms and history were at the time
the iFOBT came back positive. **Forty-six columns, forty-five of them clinical.**

| Column | Type | Null | Notes |
|---|---|---|---|
| `PatientAssessment_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK (`PK_PatientAssessment`) |
| `PatientJourney_ID` | `INT` | 🔴 **NULL** | **`FK_PatientAssessment_PatientJourney` — a real, enforced foreign key.** Nullable, uniquely among the three detail tables |
| `Patient_ID` | `VARCHAR(100)` | NOT NULL | denormalized off the journey row; **no foreign key** |
| `iFOBTPositive_Date` | `DATETIME` | NOT NULL | |
| `Risks_*` | 4 × `BIT`, 1 × `VARCHAR(100)` | NOT NULL | smoking, alcohol, IBD, diet — plus `Risks_SedentaryLifestyle`, the one that is free text |
| `Symptoms_*` | 9 × `BIT` | NOT NULL | weight loss, appetite loss, lethargy, abdominal pain, constipation, diarrhoea, rectal bleeding with/without mucous, tenesmus |
| `MedicalHistory_*` | 5 × `BIT` | NOT NULL | diabetes, hypertension, dyslipidemia, bleeding, asthma |
| `AllergyHistory_*` | 2 × `BIT` + 2 × `VARCHAR(100)` | flags NOT NULL, details **NULL** | medication, food |
| `MedicationHistory_*` | 4 × `BIT` + 4 × `VARCHAR(100)` | flags NOT NULL, details **NULL** | anticoagulant, narcotics, insulin, anti-hypertensives |
| `PreviousScope_Date` | `DATETIME` | **NULL** | the only optional date |
| `FamilyHistory_FirstDegree` / `_SecondDegree` | `BIT` | NOT NULL | |
| `PhysicalExamination_Details` | `VARCHAR(500)` | NOT NULL | both write procedures `ISNULL(…, '')` it, so a null stores a blank |
| `Investigation_*` | 5 × `BIT` | NOT NULL | FBC, BUSE, RBS, LFT, coagulation |
| `Management_*` | 4 × `BIT` | NOT NULL | bowel prep, procedure, consent, advise |

**Two constraints: the primary key and the foreign key.** No unique index on `PatientJourney_ID`, so
**nothing at the schema level stops two assessments hanging off one journey** — what prevents it is that
the only insert path is the create procedure, which always makes a fresh journey row first.

🔴 **`PatientJourney_ID` IS NULLABLE HERE AND `NOT NULL` ON THE OTHER TWO DETAIL TABLES.** An assessment
row with a NULL journey is a state the schema permits, unreachable through the portal, and invisible to
every read — all three detail reads `INNER JOIN dbo.PatientJourney`, so such a row simply never comes back.

**Nothing keeps a flag and its details column in step.** `AllergyHistory_Medication = 0` with
`AllergyHistory_MedicationDetails = 'PENICILLIN'` saves, and so does the reverse. That is the form's rule,
enforced in `.js` and nowhere else.

### 3.13 `dbo.PatientColonoscopy`

The second journey type's detail: how the scope went, segment by segment. **Thirty-two columns.**

| Column | Type | Null | Notes |
|---|---|---|---|
| `PatientColonoscopy_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK (`PK_PatientColonoscopy`) |
| `PatientJourney_ID` | `INT` | NOT NULL | **`FK_PatientColonoscopy_PatientJourney`** — enforced |
| `Patient_ID` | `VARCHAR(100)` | NOT NULL | denormalized; no foreign key |
| `ColonoscopyStatus` | `BIT` | NOT NULL | completed or not |
| `ColonoscopyStatus_Details` | `VARCHAR(500)` | **NULL** | free text, not JSON |
| `BowelPreparation` | `INT` | NOT NULL | 🔴 a bare integer with **no lookup table, no check constraint and no meaning recorded anywhere in the database** — whatever the form's dropdown posted |
| `Findings_{Segment}` | 9 × `BIT` | NOT NULL | anus, rectum, sigmoid colon, descending colon, splenic flexure, transverse colon, hepatic flexure, ascending colon, caecum |
| `Findings_{Segment}Details` | 9 × **`NVARCHAR(MAX)`** | **NULL** | 🔴 each holds a **JSON document**, not prose |
| `HPE_Status` | `BIT` | NOT NULL | was a specimen sent for histopathology |
| `HPE_Details` | `VARCHAR(500)` | **NULL** | |
| `Complications` | `VARCHAR(100)` | NOT NULL | free text; `spStaff_GetPerformance` groups its complications report on this exact string |
| `Complications_Details` | `VARCHAR(500)` | **NULL** | |
| `DischargePlan` | `VARCHAR(100)` | NOT NULL | free text — **not** `PatientBasic.DischargeType_ID` and not connected to it |
| `Medication_Details` | **`NVARCHAR(MAX)`** | **NULL** | a JSON **array** of medications given during the procedure |

🔴 **`Findings_X = 1` MEANS THE SEGMENT WAS NORMAL, SO THE ROW WITH SOMETHING IN ITS DETAILS COLUMN IS THE
`0` ONE.** That is the opposite of what the name suggests and it is decided in
`wwwroot/js/staffPatient/templates/patientColonoscopy.js`, which posts `Findings_Anus: anus.isNormal`.
Neither the data layer nor the procedure touches the polarity.

🔴 **THE NINE DETAILS COLUMNS ARE THE ONLY JSON-IN-A-COLUMN IN NUCENTRA, AND ONE KEY IS READ SERVER-SIDE.**
`spStaff_GetPerformance`'s anomalies grid `CROSS APPLY (VALUES …)`s all nine into a single column, keeps the
rows where `ISJSON() = 1`, and pulls `JSON_VALUE(…, '$.TypeOfAnomaly')` out of each, counting
`DISTINCT Patient_ID` (§5.5). **Nothing validates the JSON on the way in** — an unparseable value inserts
happily and silently stops appearing in that report.

### 3.14 `dbo.PatientFollowUp`

The third journey type's detail, and by far the smallest: **six columns, three of them clinical.**

| Column | Type | Null | Notes |
|---|---|---|---|
| `PatientFollowUp_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK (`PK_PatientFollowUp`) |
| `PatientJourney_ID` | `INT` | NOT NULL | **`FK_PatientFollowUp_PatientJourney`** — enforced |
| `Patient_ID` | `VARCHAR(100)` | NOT NULL | denormalized; no foreign key |
| `HPE_Results` | `VARCHAR(100)` | NOT NULL | the histopathology result for the specimen the COLONOSCOPY journey sent |
| `DischargePlan` | `VARCHAR(100)` | NOT NULL | free text; again **not** `PatientBasic.DischargeType_ID` |
| `DischargeSummary_Status` | `BIT` | NOT NULL | has the discharge summary been issued |

🔴 **`HPE_Results` HERE IS THE ANSWER TO `PatientColonoscopy.HPE_Status` THERE, AND NOTHING IN THE SCHEMA
LINKS THEM.** Two journey rows, two detail tables, one clinical thread — joined by the words a clinician
typed and by the patient id, and by nothing else. There is no column on either row pointing at the other.

🔴 **`DischargeSummary_Status = 1` DOES NOT DISCHARGE THE PATIENT.** Discharging is
`PatientBasic.DischargeType_ID` plus a date and remarks, written by `spPatientBasic_Update` from the
Discharge tab of `/Patient/Edit` — a different screen, a different policy (`AdminOrSuper`, not `StaffOnly`)
and a different table (§3.8). Nothing reads this bit except the follow-up form itself.

### 3.15 `dbo.PatientDocument`

One uploaded file belonging to one patient — identification, a referral letter, an iFOBT result, a consent
form. The bytes are in the private Azure Blob container; this row is the catalogue entry. The staff-side
twin is `dbo.StaffDocument` (§3.5), and **the two disagree about the type of one column**.

| Column | Type | Null | Notes |
|---|---|---|---|
| `PatientDocument_ID` | **`INT IDENTITY(1,1)`** | NOT NULL | PK, **unnamed** (`PK__PatientD__EFD3B01E…`) |
| `Patient_ID` | `VARCHAR(100)` | NOT NULL | a `PatientBasic.Patient_ID`, **by convention only** |
| `PatientDocumentType_ID` | `VARCHAR(100)` | **NULL** | a `LU_PATDOCUMENTTYPE.PatientDocumentType_ID`, by convention only. **The only nullable id on the table** |
| `FileName` | `VARCHAR(255)` | NOT NULL | the user's file name after `DocumentValidation.SafeFileName` — bounded to 255 because *this column is 255* |
| `BlobName` | `VARCHAR(500)` | NOT NULL | **the key inside the private container**, `patients/{Patient_ID}/{guid}{ext}`. Not a URL and not a filesystem path |
| `ContentType` | `VARCHAR(100)` | NOT NULL | |
| `UploadedOn` | 🔴 **`VARCHAR(100)`** | NOT NULL | **a formatted string, not a date.** See below |

**`PK__PatientD__EFD3B01E…` is the only constraint on the table** — verified against the live database with
`sys.objects` and `sys.indexes`. No foreign keys in either direction, no unique index, no check constraint,
no default. In particular nothing stops an arbitrary `PatientDocumentType_ID`, which is exactly why
`spPatientDocument_LookupDocuments` exists (§5.8).

🔴 **`UploadedOn` IS A `VARCHAR`, WHERE `StaffDocument.UploadedOn` IS A `DATETIME`.** `spPatientDocument_Insert`
writes

```sql
CONVERT(VARCHAR(100), GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time', 120)
```

which produces **`"2026-08-09 08:23:21 +08:00"`** — Malaysian local time, with an explicit offset, as text.
Three consequences, and all three are live:

- **It is not UTC.** Do not `SpecifyKind(…, Utc)` it. §3.5 makes the same point about the staff table's
  column for the same reason; here the value at least carries its offset in the string.
- **`spPatientDocument_List`'s `ORDER BY UploadedOn DESC` is a STRING sort.** It is chronological only
  because the format is fixed-width and big-endian, and it would stop being so the day anything wrote a
  differently-shaped string. The `PatientDocument_ID DESC` tiebreak is part of the contract, not decoration:
  two documents uploaded in one request share a timestamp to the second.
- **`CRC.Data/Models/PatientDocumentItem.UploadedOn` is a `string?`, and `StaffDocumentItem.UploadedOn` is a
  `DateTime?`.** Two models, two types, one idea — because the two tables really do differ.

**Nothing enforces one document per type**, and the discharge check (§5.6) asks only whether at least one
row of each required type exists.

### 3.16 `dbo.PatientDocumentSettings`

Which document types a patient must have on file **before being discharged under a given reason**. The
patient twin of `dbo.StaffDocumentSettings` (§3.6), and structurally the same table with the staff type
swapped for a discharge type.

| Column | Type | Null | Notes |
|---|---|---|---|
| `DischargeType_ID` | `VARCHAR(100)` | NOT NULL | PK part 1. A `LU_DISCHARGETYPE.DischargeType_ID` |
| `DischargeType_Name` | `VARCHAR(100)` | NOT NULL | denormalized copy of the name |
| `PatientDocumentType_ID` | `VARCHAR(100)` | NOT NULL | PK part 2. A `LU_PATDOCUMENTTYPE.PatientDocumentType_ID` |
| `PatientDocumentType_Name` | `VARCHAR(100)` | NOT NULL | denormalized copy of the name |

`PK_PatientDocumentSettings` is a **composite key over the two ids** and is the only constraint on the
table. Neither id is a foreign key; both `_Name` columns are copies that go stale the moment a lookup row
is renamed.

🔴 **THERE IS NO `IsMandatory` COLUMN HERE EITHER — THE EXISTENCE OF THE ROW IS THE RULE.** A row means "a
patient discharged as NORMAL must have a DISCHARGE SUMMARY on file"; no row means nothing. **An empty table
means nothing is mandatory anywhere**, which is the state of a freshly published `CRC_DB`: until somebody
uses the Settings screen, every discharge passes the check in §5.6.

**But the two tables are read in opposite directions, and that is the difference to hold on to.** The staff
read (`spStaffDocumentSettings_GetByStaffType`) drives `LU_STAFFDOCUMENTTYPE` and `LEFT JOIN`s the settings,
so it hands back **every** document type with a computed `IsMandatory` flag — a pre-ticked checklist. The
patient read (`spPatientDocumentSettings_GetByDischargeType`) selects straight from this table, so it hands
back **only the configured rows** and no flag at all. Same idea, same shape on disk, two different result
sets, and `CRC.Data/Models/PatientDocumentSetting.cs` versus `StaffDocumentSetting.cs` are two models
because of it. The Settings page reconciles the difference client-side. See §5.9.

**Who writes it:** only `spPatientDocumentSettings_SaveForDischargeType`, which deletes and re-inserts the
whole set for one discharge reason in a single batch, resolving `DischargeType_Name` from `LU_DISCHARGETYPE`
and `PatientDocumentType_Name` from `LU_PATDOCUMENTTYPE` itself — so nothing outside the procedure has to
keep the denormalized names honest at write time. The staff table has no such procedure (§5.9).

### 3.17 `dbo.AuditTrails`

**Six columns, one index, and no foreign keys.** It is the only table in nucentra that no controller ever
inserts into: every row is written **from inside a stored procedure**, by the nineteen that declare the
`@User_ID` actor parameter (§0.1, §9).

| Column | Type | Null | Notes |
|---|---|---|---|
| `AuditTrail_Id` | `INT IDENTITY(1,1)` | NOT NULL | `PK_AuditTrails` |
| `AuditTrail_EventUTC` | `DATETIME2(0)` | NOT NULL | defaults to `SYSUTCDATETIME()`. **UTC, always** — no procedure ever passes a value, so the default is the only writer |
| `User_Id` | `INT` | **NULL** | the ACTOR. Written as `ISNULL(@User_ID, 0)` by all nineteen, so in practice it is never null and `0` means "nobody said" |
| `AuditTrail_Action` | `VARCHAR(20)` | NOT NULL | `INSERT`, `UPDATE` or `DELETE` — by convention, not by constraint |
| `AuditTrail_Category` | `VARCHAR(50)` | NOT NULL | the TABLE that was written: `Branch`, `Staff`, `StaffSlots`, `StaffDocument`, `PatientBasic`, `PatientAppointment`, `PatientDocument` |
| `AuditTrail_Summary` | `VARCHAR(500)` | NOT NULL | a `CONCAT`ed sentence naming the row and its salient fields |

`IX_AuditTrails_Category_EventUTC` on `(AuditTrail_Category, AuditTrail_EventUTC)` is the only index. The
search screen's most common filter is the actor, which that index does not cover — fine at this table size,
and the thing to notice before it grows.

🔴 **`User_Id` IS NOT A FOREIGN KEY, AND THAT IS DELIBERATE — AN AUDIT ROW MUST OUTLIVE THE USER IT NAMES.**
Deleting a user therefore leaves their rows behind with an id that resolves to nothing. `spAuditTrails_Search`
`LEFT JOIN`s and renders those with a blank name; `spAuditTrails_LookupUsers` `INNER JOIN`s and drops them
from the filter dropdown entirely (§5.10). Measured on the local database: one row out of 105, actor `3`,
visible in the results and unselectable in the filter.

**A `User_Id` of `0` and a `User_Id` that no longer resolves look identical on the page — both render as a
blank name — and they mean opposite things.** `0` is the silent failure of §0.1: a `SqlData` method that
forgot to pass the actor. Anything else is a real actor whose account has since gone. **The id is what tells
them apart**, which is why the page shows it next to the name.

```sql
-- the health check for the whole @User_ID mechanism; the correct answer is 0
SELECT COUNT(*) FROM dbo.AuditTrails WHERE User_Id IS NULL OR User_Id = 0;
```

**Nothing prunes this table.** There is no retention job, no archive procedure and no delete path — not even
the cascades: `spPatient_DeleteCascade` and `spStaff_Delete` remove a subject's rows from every other table
and *add* to this one. It grows monotonically for the life of the installation.

---

## 4. Pages, endpoints, policies

> **All 16 controllers are covered below**, one `###` sub-section per screen — which is not always one per
> controller: `PatientController` is split across §4.7 and §4.8.1 because it is two features sharing a
> class, and the three appointment controllers are grouped into §4.8. A new screen gets the next number.
>
> 🔴 **Every `jsonc` block in this section is a CONTRACT, not an illustration.** 59 JavaScript files read
> these shapes by property name, and none of them is generated from anything. A property renamed here is a
> table that silently renders `undefined`. The "behaviours that look like bugs, are not, and must be
> preserved" list under each screen is where the non-obvious parts of that contract are written down.

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
  unlike `SaveBranch` — so a delete that fails is invisible outside the database. 🔴 **It is one of FORTY
  catch blocks in the portal that log nothing**, counted and located in §9.2. A real defect, stated rather
  than fixed, because closing it changes behaviour.

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
one — because there is no `spLU_LOCATION_GetById`. That is a known inefficiency, not a subtlety: adding the
procedure would be a straightforward §11 change and nobody has needed it enough. The cascade is
strict: a city is only looked up if a state id was supplied, a postcode only if a city id was. An id that
matches nothing yields `""` rather than an error, and `success` stays `true` — the page shows a blank field.
The `StaffId` claim never touches this endpoint: the ids arrive as query parameters, so **any authenticated
user can resolve any location name**, which is harmless for public geography and is why the page is allowed
to skip the broader admin lookups.

### 4.3 Account (login, users, change password)

`CRC.Web/Controllers/AccountController.cs` — **no class-level `[Authorize]`**, and it is the only controller
in the portal without one. That is not an omission: its twelve actions need four different levels, and the
global `AuthorizeFilter` (§2.2) makes the default *authenticated*, so the class-level default is already the
right one. Views live in `Views/Account/`; scripts in `wwwroot/js/account/`.

**This controller is also the default route.** `Program.cs` maps
`{controller=Account}/{action=Login}/{id?}`, so `https://localhost:7276/` **is** `/Account/Login` — the one
place in nucentra where a bare path resolves to something (`/Branch` alone still 404s, §4.1).

| # | Verb | Route | Policy | Returns |
|---|---|---|---|---|
| 1 | GET | `/Account/Login` | **`[AllowAnonymous]`** | the login page |
| 2 | POST | `/Account/Login` | **`[AllowAnonymous]`** + `[ValidateAntiForgeryToken]` + `[EnableRateLimiting("login-ip")]` | a **redirect** on success, the **view** on failure — never JSON |
| 3 | GET | `/Account/Logout` | authenticated (global filter) | redirect to `/Account/Login` |
| 4 | GET | `/Account/AccessDenied` | authenticated (global filter) | the access-denied page |
| 5 | GET | `/Account/Register` | `SuperUserOnly` | the create-user page |
| 6 | POST | `/Account/RegisterUser` | `SuperUserOnly` | `{ success, message }` |
| 7 | GET | `/Account/GetUsers` | `SuperUserOnly` | `{ success, users[] }` |
| 8 | POST | `/Account/UnlockUser` | `SuperUserOnly` | `{ success, message }` |
| 9 | GET | `/Account/GetPasswordPolicy` | `[Authorize]` (any type) | a bare policy object — **no envelope** |
| 10 | GET | `/Account/GetSessionTimeout` | `[Authorize]` (any type) | a bare object — **no envelope** |
| 11 | GET | `/Account/ChangePassword` | authenticated (global filter) | the page, with a `ChangePasswordViewModel` |
| 12 | POST | `/Account/ChangePassword` | authenticated (global filter) + `[ValidateAntiForgeryToken]` | **PRG** — a redirect on success, the view on failure |

**Both `Login` and `ChangePassword` exist as a GET/POST pair over one name**, and neither POST returns JSON.
That makes this controller the exception to §0's response-shape rule, and deliberately so: these are form
posts driven by `.cshtml`, not by `fetch`. `Login` failures re-render the view with `ViewData["LoginError"]`;
`ChangePassword` uses **Post/Redirect/Get** with `TempData["SuccessMessage"]` / `TempData["ErrorMessage"]`,
so a refresh after a successful change does not re-post the form. Five of the twelve return JSON; four
return a page; three redirect.

#### The JSON, which is the contract `wwwroot/js` reads

```jsonc
// GET /Account/GetUsers                                    → 200  (SuperUserOnly)
{ "success": true,
  "users": [{ "userId": 1, "name": "SYSTEM SUPERUSER", "username": "SUPERUSER",
              "email": "superuser@crc.local", "userType": 1, "userTypeName": "SUPERUSER",
              "staffId": "", "createdAt": "2026-08-06T02:06:15.0300000Z",
              "lastLogin": "2026-08-06T07:25:09.1030000Z", "failedLoginCount": 0,
              "lastFailedLoginAt": "", "lockoutEndUtc": "", "isLocked": false }] }

// POST /Account/RegisterUser  { name, username, email, password, userType, staffId? }
{ "success": true,  "message": "User registered successfully." }
// 400 { "success": false, "message": "Please fill in all required fields." }
// 400 { "success": false, "message": "<the password-policy errors, space-joined>" }
// 400 { "success": false, "message": "Staff is required for STAFF users." }
{ "success": false, "message": "Unable to register user. Please verify the inputs and try again.",
  "correlationId": "…" }                                   // SqlException — 200, not 500
{ "success": false, "message": "An unexpected error occurred.", "correlationId": "…" }

// POST /Account/UnlockUser  { userId }
{ "success": true,  "message": "Account unlocked." }
// 400 { "success": false, "message": "A valid user is required." }   // userId <= 0 or no body
// 400 { "success": false, "message": "User not found." }             // unknown userId
{ "success": false, "message": "Unable to unlock the account.", "correlationId": "…" }

// GET /Account/GetPasswordPolicy                           → 200, BARE object, no envelope
{ "requireDigit": true, "requireLowercase": true, "requireNonAlphanumeric": true,
  "requireUppercase": true, "requiredLength": 12, "requiredUniqueChars": 2 }

// GET /Account/GetSessionTimeout                           → 200, BARE object, no envelope
{ "inactivityTimeoutSeconds": 600 }
```

`GetUsers` is the one endpoint in the portal whose JSON names are **not** produced by an anonymous object:
they come from `[JsonPropertyName]` attributes on the nested `UserListItemDto`. Same contract, different
mechanism — do not "tidy" it into an anonymous object without checking `wwwroot/js/account/`.

Four shape rules that must survive any change:

- **Dates are ISO-8601 round-trip (`"o"`), and "never" is the EMPTY STRING, not `null`.** `""` is what the
  `DataTable` code produced for a `DBNull` (`DBNull.ToString()` is `""`), and the table renders these
  straight — a `null` shows the word "null".
- **`staffId` is `""` for every non-STAFF account**, same reason.
- **The values are UTC but SQL Server returns `DateTimeKind.Unspecified`.** They are `SpecifyKind(…, Utc)`-ed
  before formatting, or the `Z` the `"o"` format appends would be a lie.
- **`isLocked` is computed, not stored** — `lockoutEndUtc > UtcNow`, which is why the response carries both
  (§2.6). An expired lockout returns a non-empty `lockoutEndUtc` with `isLocked: false`.

#### Login (POST) — the order of the checks is the security

1. Blank username or password → re-render with **`"Please enter username and password."`** (the one login
   message that is *not* generic — it leaks nothing, because it is about the form).
2. `GetUserForLoginAsync` → no row → `AuditLog.LoginFailed(…, "UserNotFound")`, generic error.
3. **Lockout check, before the password is verified** → `AuditLog.LoginAttemptWhileLocked`, generic error,
   **counter not incremented**. Checking the lockout first is what stops a locked account being a password
   oracle.
4. Empty stored hash → `AuditLog.LoginFailed(…, "MissingPasswordHash")` + register the failure.
5. Password mismatch → `AuditLog.LoginFailed(…, "PasswordMismatch")` + register the failure.
6. Success → reset the counters, stamp `Last_Login`, build the claims (§2.1), `SignInAsync`,
   `AuditLog.LoginSucceeded`, redirect by role.

🔴 **Every failure path returns the same string — `"Invalid username or password."`** — and the reason is
recorded only on the audit channel. Do not make any of them more helpful: the distinction between "no such
user", "locked", and "wrong password" is exactly what an attacker wants, and steps 2, 3, 4 and 5 are four
different failures wearing one message on purpose.

An unexpected exception anywhere in the action is caught, logged, and shown as
**`"We couldn't sign you in right now."`** via `ErrorResponse.ForView` — which carries the correlation id
into the page so a user's complaint ties to a line in `Logs/app-*.log`.

#### ChangePassword (POST) — what it validates, in order

Fields are **always reloaded from `dbo.Users`**, never trusted from the form: the view model carries the
read-only profile fields and a hostile client could otherwise post any of them back. Then:
current password must verify against `Password_Hash`; the new password must **differ from the current one**
(compared as *plaintext form fields*, so re-using a password from two changes ago is allowed — there is no
history, §2.7); the new password must satisfy every rule in `Account:Password`; and `[Compare]` on the view
model requires the confirmation to match. On any failure the three password fields are **blanked** before
the view is re-rendered, so a browser back-button or a re-render never redisplays a password.

`ValidatePasswordPolicy` produces **one message per broken rule** and `RegisterUser` space-joins them into a
single `message` string. The rules and their configured values are in `Account:Password` — 12 characters,
upper, lower, digit, non-alphanumeric, 2 unique characters (§2.6's table is the lockout half of the same
config block).

### 4.4 Staff (Admin > Staff)

`CRC.Web/Controllers/Staff/StaffController.cs` — **the only controller in nucentra with per-action
`[Authorize]` attributes and no class-level one** (`StaffPatientController` mixes policies but still
declares a class default). Seventeen of its eighteen actions are `AdminOrSuper`; **`GetStaff` alone is
`AdminOrSuperOrStaff`**, because `/MyProfileStaff` reads a clinician's own record through it.
Views: `Views/Staff/Index.cshtml` and `Views/Staff/StaffEdit.cshtml`; scripts: `wwwroot/js/staff/index.js`
and `wwwroot/js/staff/edit-staffbasic.js`. Antiforgery is global, so every POST needs `X-CSRF-TOKEN` (§0).

> **The one `AdminOrSuperOrStaff` action here is `GetStaff`** — not `GetStaffTypes`, which is
> `AdminOrSuper` like the other sixteen. §2.3 says the same; the table below is what the attributes
> actually say, read action by action.

| # | Verb | Route | Policy | Returns |
|---|---|---|---|---|
| 1 | GET | `/Staff/Index` | `AdminOrSuper` | the staff list page |
| 2 | GET | `/Staff/Edit/{id?}` | `AdminOrSuper` | the `StaffEdit` view, with `ViewData["StaffId"]` = the route id or `""` |
| 3 | GET | `/Staff/GetActiveBranches` | `AdminOrSuper` | a **bare JSON array** |
| 4 | GET | `/Staff/GetStaffList` | `AdminOrSuper` | a **bare JSON array** |
| 5 | GET | `/Staff/GetStaff?staffId=` | **`AdminOrSuperOrStaff`** | `{ success, data }`, `{ success, data: null }`, `{ success: false, message }` — or **403** |
| 6 | POST | `/Staff/SaveStaff` | `AdminOrSuper` | `{ success, message, staffId }` |
| 7 | POST | `/Staff/SaveStaffWithDocuments` | `AdminOrSuper` | `{ success, message, staffId }` |
| 8 | POST | `/Staff/DeleteStaff` | `AdminOrSuper` | `{ success, message }` |
| 9 | GET | `/Staff/GetStaffLookups` | `AdminOrSuper` | `{ success, staffTypes[], states[] }` |
| 10 | GET | `/Staff/GetCitiesByState?stateId=` | `AdminOrSuper` | `{ success, cities[] }` |
| 11 | GET | `/Staff/GetPostcodesByCity?cityId=` | `AdminOrSuper` | `{ success, postcodes[] }` |
| 12 | GET | `/Staff/GetStaffDocumentTypes` | `AdminOrSuper` | `{ success, data[] }` |
| 13 | GET | `/Staff/GetStaffDocuments?staffId=` | `AdminOrSuper` | `{ success, data[] }` |
| 14 | POST | `/Staff/UploadStaffDocuments` | `AdminOrSuper` + `[RequestSizeLimit(120_000_000)]` + `[RequestFormLimits(…)]` | `{ success, message }` |
| 15 | GET | `/Staff/GetStaffDocumentUrl?id=` | `AdminOrSuper` | `{ success, url, fileName }` |
| 16 | POST | `/Staff/DeleteStaffDocument` | `AdminOrSuper` | `{ success, message }` |
| 17 | GET | `/Staff/GetStaffTypes` | `AdminOrSuper` | a **bare JSON array** |
| 18 | GET | `/Staff/GetMandatoryDocumentsForStaffType?staffTypeId=` | `AdminOrSuper` | `{ success, data[] }` |

**Five of the eighteen have no caller anywhere in the product** — grepping every `.js` file and every
`.cshtml` finds none for `GetActiveBranches`, `GetStaffTypes`, `SaveStaff`, `UploadStaffDocuments` or
`DeleteStaffDocument`. They are live, authenticated HTTP endpoints all the same, which is why they were
migrated rather than deleted; removing a public endpoint is the owner's decision, not a refactor's. Two
details make the point concrete: the staff form's **Base** field is a plain `<input type="text">`, not a
dropdown, so nothing consumes `GetActiveBranches`; and `edit-staffbasic.js` posts to
`SaveStaffWithDocuments` for *every* save, so `SaveStaff` — the non-transactional twin — is dead.
(`/Settings/GetStaffTypes` is a different action on a different controller and *is* called.)

#### The JSON, which is the contract `wwwroot/js/staff/` reads

```jsonc
// GET /Staff/GetStaffList                                   → 200, bare array
[{ "staffId": "END-00001", "name": "ALIA BINTI SEED", "nric": "850412-10-1234",
   "phone": "0123456789", "email": "seed@nucentra.local",
   "staffTypeId": "END", "staffTypeName": "ENDOSCOPIST" }]

// GET /Staff/GetStaff?staffId=END-00001                     → 200
{ "success": true, "data": {
    "staffId": "END-00001", "name": "…", "nric": "…",
    "birthDate": "1985-04-12",            // yyyy-MM-dd, for <input type="date">; null when the column is
    "age": 41,                            // 0 when the column is null
    "phone": "…", "email": "…", "gender": "MALE",
    "resState": "SELANGOR", "resCity": "SHAH ALAM", "resPostcode": "40000",
    "addLine1": "…", "addLine2": "…", "staffBase": "022367001",
    "staffTypeId": "END", "staffTypeName": "ENDOSCOPIST" } }
{ "success": true,  "data": null }                     // BLANK staffId — 200, success TRUE, no data
{ "success": false, "message": "Staff not found." }    // unknown id — 200, not 404
// 302 → /Account/AccessDenied, empty body             // a STAFF user asking for someone else's id.
//                                                     // Forbid() under cookie auth is a REDIRECT — §4.5

// POST /Staff/SaveStaff  and  POST /Staff/SaveStaffWithDocuments
{ "success": true,  "message": "Staff created successfully.", "staffId": "NUR-00003" }
{ "success": true,  "message": "Staff updated successfully.", "staffId": "NUR-00003" }
{ "success": false, "message": "Please fill in all required fields." }
{ "success": false, "message": "Invalid birth date." }
{ "success": false, "message": "Staff ID is required for update." }
{ "success": false, "message": "Please upload required documents: CV / RESUME, MMC REGISTRATION CERTIFICATE",
  "staffId": "" }                                      // staffId is "" on a new staff member, the id on an update
{ "success": false, "message": "\"x.exe\" is not an allowed file type. Only PDF, PNG, JPEG and DOCX are accepted.",
  "staffId": "…" }                                     // SaveStaffWithDocuments only
{ "success": false, "message": "One of the selected files could not be read.", "staffId": "…" }
{ "success": false, "message": "An unexpected error occurred. …", "correlationId": "…" }
// 400 { "success": false, "message": "Invalid data." }      // no body at all

// POST /Staff/DeleteStaff  { staffId }
{ "success": true,  "message": "Staff deleted successfully." }
{ "success": false, "message": "Staff not found." }                       // the procedure's own message
{ "success": false, "message": "Cannot delete this staff because they are still referenced by: Patient Journey Audit." }
{ "success": false, "message": "An unexpected error occurred." }          // NOT ErrorResponse — no correlationId
// 400 { "success": false, "message": "Staff ID is required." }

// GET /Staff/GetStaffLookups                                → 200
{ "success": true,
  "staffTypes": [{ "staffTypeId": "END", "staffTypeName": "ENDOSCOPIST" }],
  "states":     [{ "id": "2367", "name": "SELANGOR" }] }    // id is a STRING here — see below
{ "success": false, "message": "Error loading staff lookups." }

// GET /Staff/GetCitiesByState?stateId=2367                  → 200
{ "success": true, "cities": [{ "id": "2368", "name": "AMPANG" }] }
{ "success": false, "message": "State is required." }       // stateId <= 0

// GET /Staff/GetPostcodesByCity?cityId=2368                 → 200
{ "success": true, "postcodes": [{ "id": "2369", "name": "68000" }] }
{ "success": false, "message": "City is required." }        // cityId <= 0

// GET /Staff/GetStaffDocumentTypes                          → 200
{ "success": true, "data": [{ "documentTypeId": "01", "documentTypeName": "CV / RESUME" }] }

// GET /Staff/GetStaffDocuments?staffId=END-00001            → 200
{ "success": true, "data": [
  { "documentId": 2, "staffId": "END-00001", "staffName": "ALIA BINTI SEED",
    "staffDocumentTypeId": "04", "staffDocumentTypeName": "MMC REGISTRATION CERTIFICATE",
    "fileName": "mmc.pdf", "contentType": "application/pdf",
    "uploadedOn": "8/6/2026 9:28:03 PM" }] }                // culture-formatted, NOT ISO-8601, NOT UTC
{ "success": false, "message": "Staff ID is required." }

// GET /Staff/GetStaffDocumentUrl?id=3                       → 200
{ "success": true, "url": "https://…/staff/NUR-00003/9a4e….pdf?sv=…&se=…&sr=b&sp=r&sig=…",
  "fileName": "cv.pdf" }                                    // a 5-minute read SAS, minted per click
{ "success": false, "message": "Invalid document ID." }     // id <= 0
{ "success": false, "message": "Document not found." }      // unknown id, OR a row with a blank BlobName

// GET /Staff/GetMandatoryDocumentsForStaffType?staffTypeId=END  → 200
{ "success": true, "data": [{ "staffDocumentTypeId": "01", "staffDocumentTypeName": "CV / RESUME" }] }
{ "success": false, "message": "Staff type is required." }
```

Five behaviours that look like bugs, are not, and must be preserved:

- 🔴 **`id` is a STRING in these three endpoints and a NUMBER in `/Branch/GetStates`.** `LU_LOCATION.LocationId`
  is an `INT` either way; `/Branch/GetStates` serializes it as `2367` and `GetStaffLookups`,
  `GetCitiesByState` and `GetPostcodesByCity` serialize it as `"2367"`, because those three went through
  `DataRow.ToString()` and the branch one did not. **Two shapes for one column across two screens.** It is
  untidy and it is load-bearing: four `.js` files read one shape or the other, and this plan edits none of
  them. `SqlData` returns an `int` and each controller decides.
- **A blank `staffId` is `200 { success: true, data: null }`**, not a 400 and not an error. `/Branch/GetBranch`
  answers a blank id with a 400 (§4.1); this one answers with a successful nothing, because
  `/Staff/Edit` with no route id opens the *new staff* form and calls this endpoint anyway.
- **`GetStaff` refuses via `Forbid()`** when a STAFF user asks for an id that is not their own `StaffId`
  claim (`CanAccessStaff`). ADMIN and SUPERUSER pass unconditionally. This is the **only ownership check in
  the controller** — every other action is guarded by the policy alone, so an ADMIN sees every staff member
  and a STAFF user cannot reach any of them. 🔴 **`Forbid()` is an MVC result, not a status code: under
  cookie authentication it goes over the wire as `302 Location: /Account/AccessDenied?ReturnUrl=…` with an
  empty body**, never a 403 — measured, and the same for all five refusal paths in §4.5 and §4.6. The
  practical consequence is for the `.js`: a `fetch` follows the redirect to an HTML page, so `response.ok`
  is `true` and `response.json()` is what fails.
- **`DeleteStaff` is the one action that does not use `ErrorResponse.ForUser`.** Its catch returns a bare
  `{ success = false, message = "An unexpected error occurred." }` with **no `correlationId`**, so a
  user's complaint about a failed delete cannot be tied to a line in `app-*.log`. It *does* log
  (unlike `BranchController.DeleteBranch`, §4.1, which does not). A real gap, stated rather than fixed —
  see §9.2 and §12 decision 10.
- **The two `success = false` document messages from `DeleteStaff` are the stored procedure's own strings**,
  passed through untouched. `"Failed to delete staff."` is the controller's fallback for the case that
  cannot currently happen — a status that is neither `Success` nor accompanied by a message.

#### 🔴 The mandatory-document rule — the one piece of real domain logic in this controller

**Which documents a staff type requires is data, not code**: one row of `dbo.StaffDocumentSettings` per
(staff type, document type) pair, where the row's *existence* is the requirement (§3.6). A freshly
published database has none, so nothing is mandatory until the Settings screen is used.

The rule is enforced in two places that ask the same question in opposite directions, and only one of them
actually blocks a save:

| Helper | Used by | Question | Effect |
|---|---|---|---|
| `GetMandatoryDocsByStaffType` | `SaveStaffWithDocuments` | *After this request, will every required type be present?* | **Blocks the save.** Nothing is written — not the staff row, not a document, not a blob |
| `GetMissingMandatoryDocuments` | `SaveStaff` | *Does this already-saved staff member have every required type?* | **Reports only.** The row is already committed and the audit line already written |

`SaveStaffWithDocuments` computes the projected state **before opening the transaction**: the document
types the staff member has today, minus the ones marked for deletion in `deleteDocIds`, plus the types of
the files in this request. If any required type is missing from that set it returns

```
{ "success": false,
  "message": "Please upload required documents: CV / RESUME, MMC REGISTRATION CERTIFICATE",
  "staffId": "" }
```

and **nothing at all happens** — the comment in the code says so in capitals, and it matters because this
is the only validation in the portal that must run before an upload rather than after.

`SaveStaff` cannot do that: it has no files to inspect, so it saves, audits, and *then* reports the gap
with the same message and `success = false`. **A `success = false` from `/Staff/SaveStaff` therefore means
the staff member WAS saved**, which is the opposite of what the flag says. It is a live inconsistency and
it is invisible today only because nothing calls that endpoint.

Both helpers filter `IsMandatory == 1` themselves, because `spStaffDocumentSettings_GetByStaffType` returns
**every** document type with a computed flag rather than only the required ones (§3.6, §5.4). Both compare
document type ids **case-insensitively** (`StringComparer.OrdinalIgnoreCase`), and both treat a blank type
id as "no type" and skip it. Neither counts documents: one row of the required type is enough, and three
are no better.

### 4.5 Staff Schedule (Staff > Edit > Schedule tab)

`CRC.Web/Controllers/Staff/StaffScheduleController.cs` — **`[Authorize(Policy = "AdminOrSuperOrStaff")]` on
the class** (`UserType` 1, 2 or 3), with no per-action policy. **It has no `Index` action and no view of its
own**: the schedule is a tab inside `Views/Staff/StaffEdit.cshtml`, driven by
`wwwroot/js/staff/edit-staffschedule.js`, so the three actions below are the whole controller. Antiforgery
is global, so both POSTs need `X-CSRF-TOKEN` (§0).

| # | Verb | Route | Policy | Returns |
|---|---|---|---|---|
| 1 | GET | `/StaffSchedule/List?staffId=&fromDate=&toDate=` | `AdminOrSuperOrStaff` | `{ success, data[] }` — or the access-denied redirect |
| 2 | POST | `/StaffSchedule/CreateRange` | `AdminOrSuperOrStaff` | `{ success, createdCount, skippedExistingCount }` |
| 3 | POST | `/StaffSchedule/Delete` | `AdminOrSuperOrStaff` | `{ success }` |

```jsonc
// GET /StaffSchedule/List?staffId=END-00001&fromDate=2026-09-01&toDate=2026-09-03   → 200
{ "success": true, "data": [
    { "staffSlotId": 1, "slotDate": "2026-09-01", "slotStartTime": "09:00",
      "slotEndTime": "10:00", "patientAppointmentId": null },      // null = the hour is open
    { "staffSlotId": 7, "slotDate": "2026-09-03", "slotStartTime": "09:00",
      "slotEndTime": "10:00", "patientAppointmentId": 4 }] }       // taken by appointment 4
{ "success": true,  "data": [] }                       // blank staffId, unknown staffId, or no slots
{ "success": false, "message": "Invalid From Date." }  // fromDate not yyyy-MM-dd
{ "success": false, "message": "Invalid To Date." }    // toDate not yyyy-MM-dd
{ "success": false, "message": "An unexpected error occurred. …", "correlationId": "…" }

// POST /StaffSchedule/CreateRange  { staffId, fromDate, toDate, startTime, endTime }
{ "success": true,  "createdCount": 4, "skippedExistingCount": 0 }   // a fresh range
{ "success": true,  "createdCount": 0, "skippedExistingCount": 4 }   // the same range again — see below
{ "success": false, "message": "Please fill in all required fields." }
{ "success": false, "message": "Invalid date range." }               // either date not yyyy-MM-dd
{ "success": false, "message": "Invalid time range." }               // either time not HH:mm
{ "success": false, "message": "An unexpected error occurred. …", "correlationId": "…" }
// 400 { "success": false, "message": "Invalid data." }              // no body at all

// POST /StaffSchedule/Delete  { staffSlotId }
{ "success": true }                                                  // NO message property
{ "success": false, "message": "Slot not found." }                   // no slot has that id
{ "success": false, "message": "An unexpected error occurred. …", "correlationId": "…" }
// 400 { "success": false, "message": "Invalid slot." }              // staffSlotId <= 0 or no body
```

**`{ "success": true }` from `Delete` is the whole response** — no `message`, unlike every other write in the
portal. `edit-staffschedule.js` supplies the toast text itself. Do not add one.

**Re-opening a range that already exists is a SUCCESS, not a conflict.** `spStaffSlots_CreateRange` `MERGE`s
against the unique index (§3.7), so the second call over the same hours answers
`{ createdCount: 0, skippedExistingCount: 4 }` with `success = true`. Both counts verified against the
running site. `createdCount + skippedExistingCount` is the size of the requested range.

#### 🔴 The ownership check — what it protects and why `Delete` is different

All three actions gate on `User.CanAccessStaff(...)` (`CRC.Web/Infrastructure/StaffAccessExtensions.cs`):
ADMIN and SUPERUSER pass unconditionally; a STAFF user passes only when the staff id matches their own
`StaffId` claim, trimmed and compared case-insensitively. **But `List` and `CreateRange` check a staff id
that came from the request, and `Delete` cannot** — its body is `{ staffSlotId }` and carries no staff id at
all.

```csharp
// Ownership check: resolve the owning Staff_ID server-side rather than
// trusting any client-supplied identifier. Without this, a STAFF user could
// enumerate StaffSlot_ID (sequential PK) and delete other staff's slots.
var ownerStaffId = await _data.GetStaffSlotOwnerAsync(model.StaffSlotId);
if (ownerStaffId == null)
    return Ok(new { success = false, message = "Slot not found." });

if (!User.CanAccessStaff(ownerStaffId))
    return Forbid();
```

**That extra round trip to `spStaffSlots_GetOwner` is the only thing standing between a STAFF login and every
other clinician's schedule**, and the reason is §3.7's first row: `StaffSlot_ID` is a sequential `IDENTITY`,
so the ids of slots a user has never seen are `1, 2, 3, …`. Adding a `staffId` to the request body and
checking *that* would be no check at all. Verified end to end: signed in as a STAFF user bound to
`END-00001`, `POST /StaffSchedule/Delete { staffSlotId: <a NUR-00002 slot> }` was refused and the row was
still there afterwards, while the same user deleting their own slot succeeded and audited.

**The order of the two failures matters and is deliberate.** An unknown slot id answers *"Slot not found."*
**before** ownership is consulted, so the endpoint does not tell a STAFF user whether an id they cannot touch
exists. A slot that does exist and is not theirs is refused by the authorization stack instead.

🔴 **`Forbid()` under cookie authentication is a 302 to `/Account/AccessDenied`, not a 403.** Measured on all
five refusal paths in §4.5 and §4.6 and on `/Staff/GetStaff`'s in §4.4: the response is
`302 Location: /Account/AccessDenied?ReturnUrl=%2FStaffSchedule%2FDelete`, with an empty body — the cookie
handler's `AccessDeniedPath` (§2.5) turns the forbid into a redirect. `Forbid()` is the MVC *result*; the
redirect is what goes over the wire, and the same is true of a policy failure on any `[Authorize]` action in
the portal. The practical consequence is for the `.js`: a `fetch` sees a 302 it follows to an HTML page, so
`response.ok` is `true` and `response.json()` is what fails.

#### Two more asymmetries worth knowing

- **A blank `staffId` means different things to the two controllers.** `List` answers
  `{ success: true, data: [] }` **before** it reaches the ownership check, so any authenticated user gets the
  empty payload. `/StaffPerformance/Get` (§4.6) checks ownership **first**, and `CanAccessStaff("")` is false
  for a STAFF user — so the same blank input is an empty list from one endpoint and an access-denied redirect
  from the other. Both are correct for their screen: the schedule tab opens on a *new* staff member with no
  id yet, and the performance tab renders its own placeholder in that case.
- 🔴 **`CreateRange` validates the SHAPE of its inputs and the procedure validates the RULES**, and only the
  first kind produces a useful message. The controller rejects blank fields, a date that is not `yyyy-MM-dd`
  and a time that is not `HH:mm`. Everything else — a range over 31 days, `toDate` before `fromDate`,
  `endTime` at or before `startTime`, a time that is not on the hour — is a `THROW` inside
  `spStaffSlots_CreateRange` (§5.5) that arrives as a `SqlException` and is answered with the generic
  *"An unexpected error occurred."* plus a correlation id. So a user who asks for 40 days gets no hint that
  31 is the limit. The same is true of `Delete` refusing a booked slot: verified against the running site,
  `POST /StaffSchedule/Delete` on a slot with a `PatientAppointment_ID` returns the generic error with a
  correlation id, and the real message — *"Cannot delete a slot that is already taken."* — reaches only
  `Logs/app-*.log`. Both predate this migration and are left exactly as found.

### 4.6 Staff Performance (Staff > Edit > Performance tab)

`CRC.Web/Controllers/Staff/StaffPerformanceController.cs` —
**`[Authorize(Policy = "AdminOrSuperOrStaff")]` on the class**, one action, no view of its own. Like §4.5 it
is a tab in `Views/Staff/StaffEdit.cshtml`; the script is `wwwroot/js/staff/edit-staffperformance.js`, which
loads it **once, lazily, on `shown.bs.tab`** — open the Staff Edit page and never click Performance and this
endpoint is never called.

| Verb | Route | Policy | Returns |
|---|---|---|---|
| GET | `/StaffPerformance/Get?staffId=` | `AdminOrSuperOrStaff` | `{ success, data }` — or the access-denied redirect |

```jsonc
// GET /StaffPerformance/Get?staffId=END-00001                → 200
{ "success": true, "data": {
    "totalColonoscopy": 2,
    "totalColonoscopyThisMonth": 1,
    "hoursByType":   [{ "pjAppTypeId": "02", "pjAppTypeName": "COLONOSCOPY",        "totalHours": 3.00 },
                      { "pjAppTypeId": "01", "pjAppTypeName": "PATIENT ASSESSMENT", "totalHours": 1.00 }],
    "complications": [{ "complication": "BLEEDING", "total": 1 },
                      { "complication": "PERFORATION", "total": 1 }],
    "anomalies":     [{ "typeOfAnomaly": "MASS",  "patientCount": 1 },
                      { "typeOfAnomaly": "POLYP", "patientCount": 2 }] } }

// GET /StaffPerformance/Get?staffId=          (blank, ADMIN or SUPERUSER)   → 200
// GET /StaffPerformance/Get?staffId=NOSUCHSTAFF                             → 200 — the same shape
{ "success": true, "data": { "totalColonoscopy": 0, "totalColonoscopyThisMonth": 0,
                             "hoursByType": [], "complications": [], "anomalies": [] } }

{ "success": false, "message": "An unexpected error occurred. …", "correlationId": "…" }
```

**One endpoint, four result sets, five JSON fields** — grid 1 supplies the two counts and grids 2, 3 and 4
supply the three arrays, in that order (§5.5). `staffId` is `Trim()`med before anything else, so
`?staffId=%20END-00001%20` and `?staffId=END-00001` return the same thing (verified).

Three behaviours that look like bugs, are not, and must be preserved:

- 🔴 **An unknown staff id and a staff member with no history are indistinguishable**, and both are
  `success: true` with zeroes. That is not a swallowed error: `spStaff_GetPerformance` never looks up
  `dbo.Staff` at all — it aggregates three other tables by `Staff_ID` — so "no such clinician" and "nothing
  recorded yet" genuinely are the same query result. The panel renders "No anomalies detected." for both.
- **The two counts come back as SQL `NULL`, not 0, for a staff member with no journey rows**, because grid 1
  is a `SUM` with no `GROUP BY` over an empty set (§5.5). The controller coerces with `?? 0`; the
  `DataTable` code it replaced coerced a `DBNull` the same way. This is the one nullability in this area
  that is real rather than defensive, and it is why `StaffPerformanceResult`'s counts are `int?`.
- **`totalHours` serializes with two decimal places** — `3.00`, not `3` — because the procedure `CAST`s to
  `DECIMAL(10, 2)` and the controller `Math.Round`s to 2. It is a JSON number all the same.

### 4.7 Patient (Admin > Patient) — the patient half

`CRC.Web/Controllers/Patient/PatientController.cs` — **`[Authorize(Policy = "AdminOrSuper")]` on the
class** (`UserType` 1 or 2), no per-action policy and no `[AllowAnonymous]`. Antiforgery is global, so both
POSTs need `X-CSRF-TOKEN` (§0).

> 🔴 **THIS CONTROLLER IS TWO FEATURES SHARING A CLASS NAME, AND ONLY ONE OF THEM IS DOCUMENTED HERE.**
> The **patient half** — the Active and Discharged lists, and the Basic Details + Discharge tabs of
> `/Patient/Edit` — is the thirteen actions below. The **appointment half** — `GetAppointmentLookups`,
> `GetAppointmentStaffList`, `GetAppointmentSlots`, `GetAppointments`, `SaveAppointment`,
> `DeleteAppointment` — is the Appointment tab of the same page and is **§4.8.1**. The split is not
> cosmetic: the appointment half owns `SaveAppointmentAsync`, one of the data layer's only two transactional
> units of work (§6.6, §6.7), while the patient half is thirteen ordinary one-procedure calls.
>
> **The class injects `IDatabaseData` and nothing else data-related** — one field, both halves, no
> `DatabaseHelper` and no `System.Data`. At 1,100-odd lines it is the largest controller in the portal;
> splitting it into two would be a routing change, so it has not been done.

Views: `Views/Patient/Active.cshtml`, `Discharged.cshtml`, `Edit.cshtml`. Scripts:
`wwwroot/js/patient/active-list.js`, `discharged-list.js`, `edit-basic.js`, `edit-discharge.js`.

| # | Verb | Route | Policy | Returns |
|---|---|---|---|---|
| 1 | GET | `/Patient/Active` | `AdminOrSuper` | the active-patients page |
| 2 | GET | `/Patient/GetActivePatients` | `AdminOrSuper` | a **bare JSON array** — no envelope |
| 3 | POST | `/Patient/DeletePatient` | `AdminOrSuper` | `{ success }` — **no `message` on the happy path** |
| 4 | GET | `/Patient/Discharged` | `AdminOrSuper` | the discharged-patients page |
| 5 | GET | `/Patient/GetDischargedPatients` | `AdminOrSuper` | a **bare JSON array** |
| 6 | GET | `/Patient/Edit/{id?}` | `AdminOrSuper` | the Edit view, with `ViewData["PatientId"]` = the route id or `""` |
| 7 | GET | `/Patient/GetBasicLookups` | `AdminOrSuper` | `{ success, races[], sources[], religions[], maritalStatuses[], occupations[] }` |
| 8 | GET | `/Patient/GetStates` | `AdminOrSuper` | `{ success, data[] }` |
| 9 | GET | `/Patient/GetCitiesByState?stateId=` | `AdminOrSuper` | `{ success, data[] }` |
| 10 | GET | `/Patient/GetPostcodesByCity?cityId=` | `AdminOrSuper` | `{ success, data[] }` |
| 11 | GET | `/Patient/GetBasic?patientId=` | `AdminOrSuper` | `{ success, patient }` / `{ success, patient: null }` / `{ success: false, message }` |
| 12 | GET | `/Patient/GetDischargeTypes` | `AdminOrSuper` | `{ success, data[] }` |
| 13 | POST | `/Patient/SaveBasic` | `AdminOrSuper` | `{ success, patientId }` or `{ success: false, message }` |

**There is no ownership check anywhere in this controller** — no equivalent of `CanAccessStaff` (§4.4,
§4.5), because the policy admits only ADMIN and SUPERUSER and there is no per-branch or per-clinician
scoping in nucentra at all (§2.7). Any ADMIN sees every patient.

#### The JSON, which is the contract `wwwroot/js/patient/` reads

```jsonc
// GET /Patient/GetActivePatients                            → 200, bare array
[{ "patientId": "PAT-000002", "name": "SITI MANDATORY ONLY" },
 { "patientId": "PAT-000001", "name": "ZAINAB BINTI FULL FIELDS" }]
[]                                                          // no active patients
{ "success": false, "message": "Error loading active patients." }   // an OBJECT, from a bare array endpoint

// GET /Patient/GetDischargedPatients                        → 200, bare array
[{ "patientId": "PAT-000003", "name": "ROSLI TO BE DISCHARGED", "dischargeDate": "01/07/2026" }]
{ "success": false, "message": "Error loading discharged patients." }

// POST /Patient/DeletePatient  { patientId }
{ "success": true }                                         // NO message — and the same answer for an
                                                            // id that matched nothing
{ "success": false, "message": "Invalid patient ID." }      // blank/missing — 200, not 400
{ "success": false, "message": "Error deleting patient.", "correlationId": "…" }

// GET /Patient/GetBasicLookups                              → 200
{ "success": true,
  "races":           [{ "id": "01", "name": "MALAY" }],
  "sources":         [{ "id": "01", "name": "SELF-REFERRED / WALK-IN" }],
  "religions":       [{ "id": "01", "name": "ISLAM" }],
  "maritalStatuses": [{ "id": "01", "name": "SINGLE" }],
  "occupations":     [{ "id": "01", "name": "TECHNOLOGY / FINANCE" }] }
{ "success": false, "message": "Error loading lookups." }

// GET /Patient/GetStates                                    → 200
{ "success": true, "data": [{ "id": 1, "name": "JOHOR" }] }     // id is a NUMBER
{ "success": false, "message": "Error loading states." }

// GET /Patient/GetCitiesByState?stateId=1                   → 200
{ "success": true, "data": [{ "id": 2, "name": "AYER BALOI" }] }
{ "success": true, "data": [] }                             // stateId <= 0, or a state with no cities
{ "success": false, "message": "Error loading cities." }

// GET /Patient/GetPostcodesByCity?cityId=2                  → 200
{ "success": true, "data": [{ "id": 3, "name": "82100" }] }
{ "success": true, "data": [] }                             // cityId <= 0, or a city with no postcodes
{ "success": false, "message": "Error loading postcodes." }

// GET /Patient/GetBasic?patientId=PAT-000001                → 200
{ "success": true, "patient": {
    "patientId": "PAT-000001", "name": "ZAINAB BINTI FULL FIELDS",
    "email": "…", "phone": "…", "nric": "900215101235",
    "birthDate": "1990-02-15",           // yyyy-MM-dd, for <input type="date">; null when the column is
    "age": 36,                           // 0 when the column is null
    "gender": "MALE",
    "raceId": "01", "sourceId": "02", "religionId": "01",
    "maritalStatusId": "02", "occupationId": "03",
    "resState": "JOHOR", "resCity": "AYER BALOI", "resPostcode": "82100",
    "addLine1": "NO 12 JALAN SATU", "addLine2": "TAMAN DUA, BLOK C",
    "emergencyName": "AHMAD BIN EMERGENCY", "emergencyRelationship": "SPOUSE",
    "emergencyNumber": "0198887777",
    "iFobtStatus": true, "iFobtCompletionDate": "2026-03-14", "iFobtResults": true,
    "dischargeTypeId": null, "dischargeTypeName": null,     // NULL, not "" — see below
    "dischargeDate": null, "dischargeRemarks": "" } }       // "" , not null — see below
{ "success": true,  "patient": null }                       // BLANK patientId — 200, success TRUE
{ "success": false, "message": "Patient not found." }       // unknown id — 200, not 404
{ "success": false, "message": "Error loading patient details." }

// GET /Patient/GetDischargeTypes                            → 200
{ "success": true, "data": [{ "dischargeTypeId": "01", "dischargeTypeName": "NORMAL" }] }
{ "success": false, "message": "Error loading discharge types." }

// POST /Patient/SaveBasic  (the request DTO is SaveBasicRequest — 25 properties)
{ "success": true,  "patientId": "PAT-000042" }             // insert AND update return the same shape
{ "success": false, "message": "Please fill in all mandatory fields." }
{ "success": false, "message": "NRIC must be exactly 12 digits." }
{ "success": false, "message": "Invalid NRIC (unable to derive Birth Date)." }
{ "success": false, "message": "Invalid NRIC (unable to derive Gender)." }
{ "success": false, "message": "Please fill in Discharge Date and Discharge Type." }
{ "success": false, "message": "Invalid Discharge Date." }
{ "success": false, "message": "Please save patient details first, upload required documents, then set Discharge." }
{ "success": false, "message": "Please upload the following mandatory documents before discharging this patient: IDENTIFICATION CARD, IFOBT RESULT" }
{ "success": false, "message": "An unexpected error occurred while saving patient details." }
// 400 { "success": false, "message": "Invalid request." }   // no body at all
```

Seven behaviours that look like bugs, are not, and must be preserved. Every one was measured against the
running site before and after the Dapper migration, and all twelve captured payloads came back
byte-identical.

- 🔴 **`dischargeTypeId` and `dischargeTypeName` stay JSON `null`, while `addLine2` and `dischargeRemarks`
  become `""`.** Four nullable columns, two different coercions in one object, and the difference is
  load-bearing: `edit-basic.js` computes `hasDischarge = !!(p.dischargeTypeId || p.dischargeTypeName)` to
  decide whether to open the Discharge tab in its discharged state. Coercing those two to `""` would change
  nothing today — both are falsy — but it would erase the only signal in this payload that distinguishes an
  active patient from a discharged one. The other two are `""` because the `DataTable` code produced `""`
  for a `DBNull` and the form assigns them straight into input values.
- **`age` falls back to `0` and the three date fields to `null`.** `Patient_Age` is `INT NOT NULL`, so the
  fallback cannot fire; it is typed `int?` in the model anyway, because Dapper *throws* mapping a NULL onto
  a non-nullable `int` and a defensive `0` must not become a 500. Same reasoning for `Patient_BirthDate`.
- **A blank `patientId` is `200 { success: true, patient: null }`**, not a 400 and not an error — because
  `/Patient/Edit` with no route id opens the *new patient* form and calls this endpoint anyway. An
  *unknown* id is `200 { success: false }`. Compare `/Branch/GetBranch`, which answers a blank id with a
  **400** (§4.1), and `/Staff/GetStaff`, which answers it exactly the way this one does (§4.4). Three
  endpoints, two conventions.
- 🔴 **`GetDischargedPatients` formats `dischargeDate` as `dd/MM/yyyy` and every other date in this feature
  is `yyyy-MM-dd`.** It is the only one rendered straight into a table cell rather than into an
  `<input type="date">`, and it is the only one that is **culture-formatted** — `ToString("dd/MM/yyyy")`
  with no `CultureInfo`, so the separator and the calendar are the server's. `""` when the column is NULL.
- **The two list endpoints return a bare array on success and an OBJECT on failure.** `active-list.js` and
  `discharged-list.js` both do `Array.isArray(result) ? result : []`, so the error object renders as an
  empty table with no message. Untidy, live, and unchanged.
- **`DeletePatient` answers `{ success: true }` for an id that matched nothing**, because
  `spPatient_DeleteCascade` has no existence check and returns no row count (§5.6). Verified against the
  running site: `POST /Patient/DeletePatient { "patientId": "NOSUCHPATIENT" }` → `{"success":true}`, and
  `dbo.AuditTrails` gained nothing. Same silent-success shape as `spBranch_Delete` (§5.2).
- **A blank `patientId` on `DeletePatient` is `200`, not `400`** — `{ success: false, message: "Invalid
  patient ID." }`. `/Branch/DeleteBranch` returns a 400 for the same input.

#### 🔴 `SaveBasic` — one endpoint, two procedures, and the order of the checks is the feature

It is the largest action in the patient half (**220 lines**, 488–707) and **only two of them are a database
call**.
Everything else is validation and derivation, and it runs in this order — each step's failure returns
`200` with its own message and writes *nothing*:

1. **Trim everything.** Twenty-odd fields, `(model.X ?? "").Trim()`.
2. **Parse and normalise the iFOBT block**, then **force-clear the completion fields if the status is not
   `true`** — before validation, so a cleared field cannot fail a check it is exempt from.
3. **The sixteen-field mandatory check**, one `if` with sixteen `IsNullOrWhiteSpace` calls →
   *"Please fill in all mandatory fields."*
4. **NRIC: strip non-digits, require exactly twelve** → *"NRIC must be exactly 12 digits."*
5. **Derive the birth date, then the gender, then the age** — two more messages, §3.8.
6. **Discharge shape**: if `isDischarged`, both the type and the date must be present and the date must
   parse → two messages.
7. **`isNew` is `string.IsNullOrWhiteSpace(model.PatientId)`** — the client decides insert versus update by
   whether it sends an id, and nothing verifies that an id it *does* send exists.
8. 🔴 **The discharge-document check, and it is the only step that is a database read.** Discharging a
   *new* patient is refused outright (*"Please save patient details first…"*) because there is no id to
   check documents against. For an existing one, `spPatient_Discharge_CheckMissingDocuments` returns the
   mandatory types that are **missing**, and **an empty result is the pass condition** — any row at all
   blocks the save and names the missing types in the message.
9. **Insert or update**, and only now does anything get written.

All three states of step 8 were driven end to end: with two types configured for discharge reason `01` and
no documents on file the save was refused naming both; with one of the two uploaded it was refused naming
the remaining one; with both present it succeeded. The check asks only whether **at least one document of
each required type exists** — it never counts, and a patient with three copies of one type is no better off
than one with a single copy, exactly like the staff rule (§4.4).

**What the Dapper migration moved out of this action, and what it deliberately did not.** Only the two
procedure calls and the document check moved. Every derivation, every validation, every message string, the
`ToUpperInvariant()` on the two name fields, and the null-versus-empty decision for each optional parameter
stayed in the controller; `CRC.Data/Models/PatientSaveInput.cs` carries the values and decides nothing. The
one thing that genuinely changed shape is that a save which somehow produced no `Patient_ID` now throws
(and is caught into the generic message) where it previously returned `{ success: true, patientId: "" }` —
an unreachable path, and the same choice `SaveStaffWithDocumentsAsync` makes for `spStaff_Insert` (§6.6).

### 4.8 Appointments — three controllers, thirteen actions

Booking is spread across **three** controllers, and the split is by screen rather than by feature:

| Controller | Screen | Actions | What it is |
|---|---|---|---|
| `PatientController` | the **Appointment tab** of `/Patient/Edit` | 6 | booking, editing and deleting one patient's appointments |
| `AppointmentController` | `/Appointment/Index` | 4 | the cross-patient **search** page |
| `AdminDashboardController` | `/AdminDashboard/Index` | 3 | **today's** appointments, at a glance |

**All three carry `[Authorize(Policy = "AdminOrSuper")]` on the class** (`UserType` 1 or 2), none has a
per-action policy, none has `[AllowAnonymous]`, and **none has any ownership check** — there is no
per-branch or per-clinician scoping anywhere in nucentra (§2.7), so every ADMIN sees every appointment.
Antiforgery is global, so every POST needs `X-CSRF-TOKEN` (§0).

**Two procedures are shared across controllers and have exactly one method each**:
`spPatientAppointment_Search` backs both `/Appointment/Search` and `/AdminDashboard/GetTodayAppointments`,
and `spPatientAppointment_UpdateStatus` backs the identically-named action on both of those controllers.

#### 4.8.1 `PatientController` — the appointment half

Views: `Views/Patient/Edit.cshtml`. Script: `wwwroot/js/patient/edit-appointment.js`.

> This completes the controller §4.7 describes half of. The six actions below and the thirteen in §4.7 are
> one class, one `IDatabaseData` field, one `[Authorize(Policy = "AdminOrSuper")]` — and the file contains
> no `SqlParameter`, no `DataTable` and no `DataRow`. The only `Microsoft.Data.SqlClient` reference left in
> it is `catch (SqlException)`, which is exception classification rather than data access (§12).

| # | Verb | Route | Returns |
|---|---|---|---|
| 1 | GET | `/Patient/GetAppointmentLookups` | `{ success, types[], branches[], statuses[] }` |
| 2 | GET | `/Patient/GetAppointmentStaffList` | `{ success, data[] }` |
| 3 | GET | `/Patient/GetAppointmentSlots?staffId=&date=&appointmentId=` | `{ success, data[], appointmentId }` |
| 4 | GET | `/Patient/GetAppointments?patientId=` | `{ success, data[] }` |
| 5 | POST | `/Patient/SaveAppointment` | `{ success, appointmentId }` or `{ success: false, message }` |
| 6 | POST | `/Patient/DeleteAppointment` | `{ success }` — **no `message` on the happy path** |

```jsonc
// GET /Patient/GetAppointmentLookups                        → 200
{ "success": true,
  "types":    [{ "id": "01", "name": "PATIENT ASSESSMENT" },     // spLU_PJ_AppType_List order — BY ID,
               { "id": "02", "name": "COLONOSCOPY" }],           // i.e. clinical sequence. NOT re-sorted
  "branches": [{ "branchId": "022367001", "branchName": "…", "branchState": "SELANGOR" }],
  "statuses": ["Scheduled", "Attended", "Not Attended"] }        // a HARD-CODED string[], not a read
{ "success": false, "message": "Error loading appointment lookups." }

// GET /Patient/GetAppointmentStaffList                      → 200
{ "success": true, "data": [{ "staffId": "END-00001", "staffName": "P6 DOCTOR ALPHA" }] }
{ "success": false, "message": "Error loading staff list." }

// GET /Patient/GetAppointmentSlots?staffId=END-00001&date=2026-09-01&appointmentId=5   → 200
{ "success": true,
  "data": [{ "staffSlotId": 15, "slotDate": "2026-09-01", "slotStartTime": "08:00",
             "slotEndTime": "09:00", "patientAppointmentId": 5 },      // taken by appointment 5
           { "staffSlotId": 17, "slotDate": "2026-09-01", "slotStartTime": "10:00",
             "slotEndTime": "11:00", "patientAppointmentId": null }],  // null = this hour is open
  "appointmentId": 5 }                                         // ECHOED BACK — see below
{ "success": true,  "data": [], "appointmentId": null }         // blank staffId — echoed as null
{ "success": false, "message": "Invalid date." }                // date not yyyy-MM-dd
{ "success": false, "message": "Error loading staff slots." }

// GET /Patient/GetAppointments?patientId=PAT-000001         → 200
{ "success": true, "data": [
    { "appointmentId": 5,
      "appointmentDate": "01/09/2026",          // dd/MM/yyyy, for the table cell — culture-formatted
      "appointmentDateRaw": "2026-09-01",       // yyyy-MM-dd, for the <input type="date">
      "from": "08:00", "to": "10:00",           // VARCHAR(5) straight from the procedure
      "typeId": "02", "typeName": "COLONOSCOPY",
      "branchId": "022367001", "branchName": "P6 SMOKE BRANCH",
      "status": "Scheduled",
      "staffId": "END-00001", "staffName": "P6 DOCTOR ALPHA" }] }
{ "success": true,  "data": [] }                                // blank patientId, or none booked
{ "success": false, "message": "Error loading appointments." }

// POST /Patient/SaveAppointment
//   { appointmentId, patientId, appointmentDate, staffId, slotIds[], pjAppTypeId, branchId, status }
{ "success": true,  "appointmentId": 8 }                        // insert AND update return the same shape
{ "success": false, "message": "Please fill in all mandatory appointment fields and select at least one slot." }
{ "success": false, "message": "Invalid appointment date." }
{ "success": false, "message": "Invalid attendance status." }
{ "success": false, "message": "One or more selected slots are invalid. Please reload the slots and try again." }
{ "success": false, "message": "Selected slots do not match the selected staff." }
{ "success": false, "message": "Selected slots do not match the selected appointment date." }
{ "success": false, "message": "One or more selected slots are no longer available. Please reload the slots and try again." }
{ "success": false, "message": "Please select consecutive slots (e.g. 08:00-09:00 then 09:00-10:00)." }
{ "success": false, "message": "Failed to create appointment." }
{ "success": false, "message": "Error saving appointment.", "correlationId": "…" }
// 400 { "success": false, "message": "Invalid request." }      // no body at all

// POST /Patient/DeleteAppointment  { appointmentId }
{ "success": true }                                             // NO message — and the same answer for
                                                                // an id that matched nothing
{ "success": false, "message": "Error deleting appointment.", "correlationId": "…" }
// 400 { "success": false, "message": "Invalid request." }      // appointmentId <= 0 or no body
```

Five behaviours that look like bugs, are not, and must be preserved. All five were measured against the
running site before and after the Dapper migration, and all fourteen captured payloads came back
byte-identical.

- 🔴 **`slotDate` is the REQUEST's date, not the row's.** `GetAppointmentSlots` formats the `date` query
  parameter it parsed, not `StaffSlotItem.SlotDate`. The two are always equal — `spStaffSlots_List` is
  called with `@FromDate = @ToDate =` that date — so it is invisible today and would stop being invisible
  the moment that bound moved.
- **`appointmentId` is echoed back from the query string, untouched, including as `null`.** The slot
  picker sends it so it can tell "taken by the appointment I am editing" from "taken by someone else"
  without a second request; the endpoint neither validates it nor uses it. A blank `staffId` short-circuits
  to an empty list **before** the date is parsed, so `?staffId=&date=rubbish` is a success and
  `?staffId=END-00001&date=rubbish` is *"Invalid date."*
- 🔴 **The same date is serialized twice, in two formats, and both are load-bearing.**
  `appointmentDate` is `dd/MM/yyyy` for the table cell and **culture-formatted** (`ToString` with no
  `CultureInfo`, so the separator is the server's); `appointmentDateRaw` is `yyyy-MM-dd` for the
  `<input type="date">` the edit dialog opens with. Both are `""` when the column is null, never `null`,
  because both are assigned straight into the page.
- **`DeleteAppointment` answers `{ success: true }` for an id that matched nothing**, because
  `spPatientAppointment_Delete` has no existence check and returns no row count (§5.7). Same silent-success
  shape as `spBranch_Delete` and `spPatient_DeleteCascade`.
- **The three status strings are hard-coded in `GetAppointmentLookups` and are not a database read**,
  while `/Appointment/GetLookups` builds *its* status filter from `spPatientAppointment_LookupStatuses`.
  That is not an inconsistency to fix: **a form wants the values that are allowed, a filter wants the
  values that are stored** (§3.9).

#### 🔴 4.8.2 `SaveAppointment` — the one endpoint with a transaction behind it

It handles **both** insert and update — `appointmentId <= 0` means insert — and the client decides which by
what it sends. The order of the work is the feature:

1. **Trim and normalise.** Seven fields, plus `slotIds` filtered to `> 0`, `Distinct()`ed and listed. The
   de-duplication matters: the slot-count check downstream compares against this list's length.
2. **The seven-field mandatory check**, one `if`, **including `slotIds.Count == 0`** → *"Please fill in all
   mandatory appointment fields and select at least one slot."*
3. **The date must parse as `yyyy-MM-dd` exactly** (`TryParseExact`, `InvariantCulture`) →
   *"Invalid appointment date."*
4. **The status must be one of the three**, case-insensitively → *"Invalid attendance status."*
5. **`IDatabaseData.SaveAppointmentAsync` — everything else, inside one transaction** (§6.7): the slot
   read, the four slot checks, the contiguity check, the insert or update, the slot release and the slot
   assignment.
6. **Map the returned `AppointmentSaveFailure` to a message**, or write the audit line and return the id.

**Steps 1–4 are the controller's and steps 5's contents are the data layer's, and the boundary is not
arbitrary.** Everything above the line can be decided from the request alone; everything below it needs
rows read under a lock. That is the whole test for where a check belongs.

🔴 **The four slot messages are the controller's, and the reasons are the data layer's.** The data method
returns a typed `AppointmentSaveFailure`; a `switch` in this action turns each value into the exact string
above. Both halves are necessary and neither is the other's job — see §6.7, which writes the convention up
in full.

**What the four rejection paths actually do, measured.** The in-transaction read is
`spStaffSlots_List(@Staff_ID, @FromDate = @ToDate = the appointment's date)`, so it is **already narrowed
by staff and by date** — and that makes three of the four inputs land on the same message:

| Input | Reason returned | Message the user sees |
|---|---|---|
| a `StaffSlot_ID` that does not exist | `SlotNotFound` | *"One or more selected slots are invalid. Please reload the slots and try again."* |
| a slot belonging to a **different staff member** | `SlotNotFound` | the same string |
| a slot on a **different date** | `SlotNotFound` | the same string |
| a slot already booked by a **different appointment** | `SlotTaken` | *"One or more selected slots are no longer available. Please reload the slots and try again."* |

**`SlotWrongStaff` and `SlotWrongDate` are therefore unreachable, and they were unreachable before the
migration too.** The pre-Dapper code wrote both checks: the staff one compared a field it had just
populated from the request against that same request value — a tautology, not a test — and the date one
compared a date the query had already bounded. Both strings are kept, and the `switch` still maps them, so
the reasons are available the day `spStaffSlots_List` projects `Staff_ID` or stops filtering. Verified end
to end: all four inputs produce the same messages before and after, and in every case **nothing persisted**
— no appointment row changed, no slot changed, and `MAX(AuditTrail_Id)` did not move.

**The edit path is the one that is easy to break**, and it is `spStaffSlots_ClearAppointment` then
`spStaffSlots_AssignAppointment`, in that order, inside the transaction. Verified: an appointment holding
08:00 and 09:00, re-saved with 09:00 and 10:00, ended up holding 09:00 and 10:00 — **the dropped 08:00 slot
went back to `PatientAppointment_ID IS NULL`, the kept 09:00 slot was still held, and 10:00 was newly
taken** — and the row's stored span moved from 08:00–10:00 to 09:00–11:00.

**Two audit lines, and which values each carries.** `AuditLog.AppointmentCreated` names the **request's**
values (the row is new, so there is nothing else it could describe) plus the two times the data layer
derived from the slots; `AuditLog.AppointmentUpdated` names the values `spPatientAppointment_Update`
**re-read from the row**, because a security line that reports what was asked for can be wrong in exactly
the case somebody is reading it to investigate. 🔴 **Both are written only after the commit has returned** —
the same deferral `SaveStaffWithDocuments` uses, for the same reason (§6.6).

#### 4.8.3 `AppointmentController` — the search page

`CRC.Web/Controllers/Appointment/AppointmentController.cs`. View `Views/Appointment/Index.cshtml`, script
`wwwroot/js/appointment/`. Four actions.

| # | Verb | Route | Returns |
|---|---|---|---|
| 1 | GET | `/Appointment/Index` | the search page |
| 2 | GET | `/Appointment/GetLookups` | `{ success, patients[], staff[], statuses[], types[], branches[] }` |
| 3 | POST | `/Appointment/Search` | `{ success, data[] }` |
| 4 | POST | `/Appointment/UpdateAppointmentStatus` | `{ success }` |

```jsonc
// GET /Appointment/GetLookups                               → 200
{ "success": true,
  "patients": [{ "name": "P6 SMOKE PATIENT" }],       // 🔴 FOUR OF THE FIVE ARE {name} ONLY — no ids
  "staff":    [{ "name": "P6 DOCTOR ALPHA" }],
  "statuses": [{ "name": "Attended" }, { "name": "Scheduled" }],
  "types":    [{ "id": "02", "name": "COLONOSCOPY" },  // the only one with an id — and SORTED BY NAME
               { "id": "01", "name": "PATIENT ASSESSMENT" }],
  "branches": [{ "name": "P6 SMOKE BRANCH" }] }
{ "success": false, "message": "Error loading appointment lookups." }

// POST /Appointment/Search
//   { patientName?, staffName?, status?, fromDate?, toDate?, pjAppTypeName?, branchName? }
{ "success": true, "data": [
    { "patientAppointmentId": 5, "patientId": "PAT-000001",
      "patientName": "P6 SMOKE PATIENT", "patientPhone": "0111222333",
      "patientEmail": "p6@nucentra.local",
      "appointmentType": "COLONOSCOPY", "status": "Scheduled",
      "staffName": "P6 DOCTOR ALPHA", "branchName": "P6 SMOKE BRANCH",
      "appointmentDateTime": "01/09/2026 08:00" }] }   // ONE field — date and start time together
{ "success": false, "message": "Error searching appointments." }
// 400 { "success": false, "message": "Invalid request." }   // no body at all

// POST /Appointment/UpdateAppointmentStatus  { patientAppointmentId, status }
{ "success": true }                                          // NO message
{ "success": false, "message": "Error updating appointment status.", "correlationId": "…" }
                                                             // ↑ ALSO what an unknown id returns
// 400 { "success": false, "message": "Invalid request." }      // id <= 0, blank status, or no body
// 400 { "success": false, "message": "Invalid status value." }  // a status outside the three
```

Four things worth knowing:

- 🔴 **Four of the five lookups are reads over `dbo.PatientAppointment` itself, not over a lookup table.**
  They answer *"what values are actually in use"*, so **a portal with no appointments returns four empty
  dropdowns**, and a branch, patient or clinician that has never been booked never appears. That is what a
  search filter wants — offering a value that can only ever return nothing is worse than omitting it — and
  it is why the branch filter here is `spPatientAppointment_LookupBranches` and **not** the
  `spBranch_ListActive` the booking form uses. The fifth, the appointment type, *is* the plain lookup,
  because filtering by type is filtering by a fixed clinical vocabulary rather than by what happens to be
  booked.
- 🔴 **This endpoint re-sorts the appointment types BY NAME and `/Patient/GetAppointmentLookups` does
  not.** `spLU_PJ_AppType_List` orders by ID because the ids are in clinical sequence (§3.1); the booking
  form keeps that, and this search filter sorts alphabetically because that is what a user scans. Two
  callers, one procedure, two correct orders — removing the `OrderBy` to make them match would change this
  dropdown.
- **Blank must become `null` before it reaches the procedure**, and that conversion is the whole of the
  filtering: every predicate is `@X IS NULL OR column = @X`, so `""` would match only rows whose column is
  the empty string, i.e. nothing. The dates use `DateTime.TryParse` — **not `TryParseExact`** — so this
  endpoint accepts whatever the server's culture parses, and an unparseable date is silently treated as
  *no filter* rather than rejected. Both predate the migration and are left as found.
- **The `_UpdateStatus` procedure `RAISERROR`s on an unknown id**, unlike every other write in this area,
  so *"Appointment not found."* arrives as a `SqlException` and the user gets the generic message plus a
  correlation id. The real sentence reaches only `Logs/app-*.log`.

#### 4.8.4 `AdminDashboardController` — today's appointments

`CRC.Web/Controllers/AdminDashboard/AdminDashboardController.cs`. View
`Views/AdminDashboard/Index.cshtml`. **This is where an ADMIN lands after login** (§2.1). Three actions.

| # | Verb | Route | Returns |
|---|---|---|---|
| 1 | GET | `/AdminDashboard/Index` | the dashboard page |
| 2 | GET | `/AdminDashboard/GetBranches` | `{ success, data[] }` |
| 3 | GET | `/AdminDashboard/GetTodayAppointments?branchName=` | `{ success, data[] }` |
| 4 | POST | `/AdminDashboard/UpdateAppointmentStatus` | `{ success }` |

```jsonc
// GET /AdminDashboard/GetBranches                           → 200
{ "success": true, "data": [{ "name": "P6 SMOKE BRANCH" }] }
{ "success": false, "message": "Error loading branches." }

// GET /AdminDashboard/GetTodayAppointments?branchName=P6%20SMOKE%20BRANCH   → 200
{ "success": true, "data": [                                 // the SAME ten fields as /Appointment/Search
    { "patientAppointmentId": 6, "patientId": "PAT-000001",
      "patientName": "…", "patientPhone": "…", "patientEmail": "…",
      "appointmentType": "PATIENT ASSESSMENT", "status": "Attended",
      "staffName": "…", "branchName": "…",
      "appointmentDateTime": "09/08/2026 14:00" }] }
{ "success": false, "message": "Error loading today's appointments." }

// POST /AdminDashboard/UpdateAppointmentStatus  { patientAppointmentId, status }
// — byte-identical to /Appointment/UpdateAppointmentStatus's four shapes
```

Three things worth knowing:

- **`GetBranches` is `/Appointment/GetLookups`'s branch filter, not the booking form's branch list.** Same
  procedure, same reasoning: a branch nobody has ever been booked into would filter this dashboard down to
  nothing.
- 🔴 **`GetTodayAppointments` re-sorts the search result, reversing the procedure's own order.**
  `spPatientAppointment_Search` orders date **DESC** (newest day first) because it spans a range; this
  panel shows one day and wants clock order, so it sorts **ascending** on the composed start datetime, with
  a null date sorting **last** via `DateTime.MaxValue`. The sort key is then projected away so it never
  reaches the JSON.
- 🔴 **"Today" is the WEB SERVER's `DateTime.Today`, not the database's.** It is passed in as both
  `@FromDate` and `@ToDate`. On one machine that is the same clock; split across an App Service and Azure
  SQL it agrees only as well as the two clocks and time zones do, and this panel is where that would show
  first. Compare `spStaff_GetPerformance`'s "this month", which is decided on the **SQL Server's** clock
  (§4.6) — nucentra decides "now" on both sides of the wire, in different places.
- 🔴 **This action writes NO `AuditLog` line, and its twin on `AppointmentController` does.** Both write the
  same `dbo.AuditTrails` row, because the procedure writes that itself — but only `/Appointment` adds a
  line to the Serilog security channel. Verified after the migration: changing a status here produced the
  database audit row and **zero** new lines in `audit-*.log`. That asymmetry predates this work and is left
  exactly as found; adding a line would be a new audit event, not a migration.

### 4.9 Staff Patient (the STAFF workspace)

`CRC.Web/Controllers/StaffPatient/StaffPatientController.cs` — **the page a clinician actually works in**,
and the only screen in nucentra where clinical data is written. View `Views/StaffPatient/Details.cshtml`
plus three partials under `Views/StaffPatient/Templates/`; scripts `wwwroot/js/staffPatient/details.js`,
`journey.js`, `documents.js` and three under `templates/`. Antiforgery is global, so every POST needs
`X-CSRF-TOKEN` (§0).

> #### 🔴 THE POLICY SPLIT — READ THIS BEFORE TOUCHING AN ATTRIBUTE
>
> **This is the only controller in nucentra that mixes policies per action, it has NO class-level
> `[Authorize]` to fall back on, and the split is deliberate:**
>
> | Policy | Types | Actions | Why |
> |---|---|---|---|
> | `AdminOrSuperOrStaff` | 1, 2, 3 | **eleven** — every read, plus the whole document feature | an administrator may **look at** a patient journey |
> | `StaffOnly` | **3 only** | **four** — `Details` and the three `Save*` | only a clinician may **record** one, and this genuinely excludes the SUPERUSER |
>
> **`StaffOnly` is the one asymmetry in the whole product** (§2.3): every other policy that admits ADMIN
> also admits SUPERUSER, and here a SUPERUSER cannot open the page or save a result. Note where the line
> falls — **`Details`, the page itself, is `StaffOnly`, while every endpoint the page calls is not.** An
> ADMIN reaching those endpoints directly gets the data; they just have no page to render it in.
>
> The global `AuthorizeFilter` (§2.2) is what keeps a forgotten attribute failing closed. Do not "tidy"
> these onto the class — doing so would either lock an administrator out of the journey reads or let a
> SUPERUSER write a clinical record.
>
> **The eleven-and-four split is exact and §2.3 states it the same way.** `StaffOnly` is not merely "the
> clinical writes": it is the three writes **and `Details`**. `AdminOrSuperOrStaff` is not merely "most
> reads": it is **every** read **plus all five document actions**. Counted attribute by attribute against
> the table below.

| # | Verb | Route | Policy | Returns |
|---|---|---|---|---|
| 1 | GET | `/StaffPatient/Details/{id}` | **`StaffOnly`** | the page, with `ViewBag.PatientId` |
| 2 | GET | `/StaffPatient/GetJourneyTemplate?type=` | `AdminOrSuperOrStaff` | a **partial view**, not JSON |
| 3 | GET | `/StaffPatient/GetBasic?patientId=` | `AdminOrSuperOrStaff` | `{ success, patient }` |
| 4 | GET | `/StaffPatient/GetTimeline?patientId=` | `AdminOrSuperOrStaff` | `{ success, data[] }` |
| 5 | GET | `/StaffPatient/GetPatientAssessment?patientJourneyId=` | `AdminOrSuperOrStaff` | `{ success, journey, assessment }` |
| 6 | POST | `/StaffPatient/SavePatientAssessment` | **`StaffOnly`** | `{ success, patientJourneyId }` |
| 7 | GET | `/StaffPatient/GetPatientColonoscopy?patientJourneyId=` | `AdminOrSuperOrStaff` | `{ success, journey, assessment }` |
| 8 | POST | `/StaffPatient/SavePatientColonoscopy` | **`StaffOnly`** | `{ success, patientJourneyId }` |
| 9 | GET | `/StaffPatient/GetPatientFollowUp?patientJourneyId=` | `AdminOrSuperOrStaff` | `{ success, journey, assessment }` |
| 10 | POST | `/StaffPatient/SavePatientFollowUp` | **`StaffOnly`** | `{ success, patientJourneyId }` |
| 11 | GET | `/StaffPatient/GetPatientDocumentTypes` | `AdminOrSuperOrStaff` | `{ success, data[] }` |
| 12 | GET | `/StaffPatient/GetPatientDocuments?patientId=` | `AdminOrSuperOrStaff` | `{ success, data[] }` |
| 13 | POST | `/StaffPatient/UploadPatientDocuments` | `AdminOrSuperOrStaff` + `[RequestSizeLimit(120_000_000)]` + `[RequestFormLimits(…)]` | `{ success }` |
| 14 | GET | `/StaffPatient/GetPatientDocumentUrl?id=` | `AdminOrSuperOrStaff` | `{ success, url, fileName }` |
| 15 | POST | `/StaffPatient/DeletePatientDocument` | `AdminOrSuperOrStaff` | `{ success }` |

**There is no ownership check anywhere in this controller.** No equivalent of `CanAccessStaff` (§4.4,
§4.5): any STAFF user may open any patient, read any journey and record a result against it. The
`StaffId` claim is read only to *stamp* the row, never to *filter* one — nucentra has no per-clinician or
per-branch scoping of patient data at all (§2.7).

#### The JSON, which is the contract `wwwroot/js/staffPatient/` reads

```jsonc
// GET /StaffPatient/GetBasic?patientId=PAT-000001              → 200
{ "success": true, "patient": {
    "patientId": "PAT-000001", "name": "…", "email": "…", "phone": "…", "nric": "900215101235",
    "age": "36",                          // 🔴 A STRING here. /Patient/GetBasic returns a NUMBER
    "birthDate": "1990-02-15",            // yyyy-MM-dd; "" when the column is null
    "raceName": "MALAY", "sourceName": "GP / PRIVATE CLINIC", "gender": "MALE",
    "religionName": "ISLAM", "maritalStatusName": "MARRIED",
    "resState": "JOHOR", "resCity": "AYER BALOI", "resPostcode": "82100",
    "addLine1": "…", "addLine2": "…",
    "emergencyName": "…", "emergencyRelationship": "SPOUSE", "emergencyNumber": "…",
    "occupationName": "HEALTHCARE / EDUCATION",
    "iFobtStatus": true, "iFobtCompletionDate": "2026-03-14", "iFobtResults": true,
    "dischargeTypeName": "", "dischargeDate": "", "dischargeRemarks": "" } }
{ "success": false, "message": "Invalid patient." }          // blank patientId — 200, not 400
{ "success": false, "message": "Patient not found." }        // unknown id — 200, not 404
{ "success": false, "message": "Error loading patient details." }

// GET /StaffPatient/GetTimeline?patientId=PAT-000001          → 200
{ "success": true, "data": [
    { "patientJourneyId": 5, "journeyType": "PATIENT ASSESSMENT",
      "journeyDate": "2026-04-01T09:00:00",                   // the BUSINESS date — no offset
      "createdAt": "2026-08-09T00:20:37+00:00",               // UTC, offset-bearing
      "createdByStaffName": "P7 DOCTOR ALPHA",
      "updatedAt": null, "updatedByStaffName": "",            // never updated → null AND ""
      "auditEvents": [
        { "action": "CREATED", "at": "2026-08-09T00:20:37+00:00",
          "staffId": "END-00001", "staffName": "P7 DOCTOR ALPHA",
          "note": "P7 baseline assessment created" }] }] }
{ "success": false, "message": "Invalid patient." }          // blank patientId
{ "success": true,  "data": [] }                             // unknown patientId — NOT an error
{ "success": false, "message": "Error loading timeline." }

// GET /StaffPatient/GetPatientAssessment?patientJourneyId=5   → 200
{ "success": true,
  "journey": { "patientJourneyId": 5, "journeyType": "PATIENT ASSESSMENT",
               "journeyDateInput": "2026-04-01T09:00" },     // yyyy-MM-ddTHH:mm, for datetime-local
  "assessment": { "PatientJourney_ID": 5, "Patient_ID": "PAT-000001",
                  "Patient_Name": "…", "PjAppType_Name": "PATIENT ASSESSMENT",
                  "PatientJourney_Date": "2026-04-01T09:00:00", "Staff_ID": "END-00001",
                  "PatientAssessment_ID": 1, "iFOBTPositive_Date": "2026-03-14T00:00:00",
                  "Risks_Smoking": true, /* …45 more, RAW COLUMN NAMES… */ } }
{ "success": false, "message": "Invalid journey." }          // patientJourneyId <= 0
{ "success": false, "message": "Journey not found." }        // unknown journey id
{ "success": true, "journey": { … }, "assessment": null }    // journey of ANOTHER TYPE — see below
{ "success": false, "message": "Error loading assessment." }

// POST /StaffPatient/SavePatientAssessment   (PatientJourneyId = 0 ⇒ create)
{ "success": true,  "patientJourneyId": 8 }                  // create AND update return the same shape
{ "success": false, "message": "Your account is not linked to a Staff_ID." }
{ "success": false, "message": "Error saving assessment.", "correlationId": "…" }
// 400 { "success": false, "message": "Invalid request." }    // no body, or a blank PatientId

// POST /StaffPatient/SavePatientColonoscopy → …"Error saving colonoscopy."
// POST /StaffPatient/SavePatientFollowUp    → …"Error saving follow up."
//   — byte-identical shapes; only the error string differs.

// GET /StaffPatient/GetPatientDocumentTypes                   → 200
{ "success": true, "data": [{ "documentTypeId": "01", "documentTypeName": "IDENTIFICATION CARD" }] }
{ "success": false, "message": "Error loading patient document types." }

// GET /StaffPatient/GetPatientDocuments?patientId=PAT-000001  → 200
{ "success": true, "data": [
  { "documentId": 5, "patientId": "PAT-000001", "patientName": "P7 BASELINE PATIENT",
    "docTypeId": "02", "docTypeName": "REFERRAL LETTER (IN)", "fileName": "referral.pdf",
    "uploadedOn": "2026-08-09 08:23:21 +08:00" }] }           // a STRING from the column, unparsed
{ "success": true,  "data": [] }                              // blank patientId — success, empty
{ "success": false, "message": "Error loading patient documents." }

// POST /StaffPatient/UploadPatientDocuments   (multipart)
{ "success": true }                                           // NO message
{ "success": false, "message": "No files uploaded." }
{ "success": false, "message": "One of the selected files could not be read." }
{ "success": false, "message": "\"x.exe\" is not an allowed file type. Only PDF, PNG, JPEG and DOCX are accepted." }
{ "success": false, "message": "Error uploading patient documents.", "correlationId": "…" }
// 400 { "success": false, "message": "Patient ID is required." }

// GET /StaffPatient/GetPatientDocumentUrl?id=5                → 200
{ "success": true, "url": "https://…/patients/PAT-000001/7cdd….pdf?sv=…&se=…&sr=b&sp=r&sig=…",
  "fileName": "referral.pdf" }                                // a 5-minute read SAS, minted per click
{ "success": false, "message": "Invalid document ID." }       // id <= 0
{ "success": false, "message": "Document not found." }        // unknown id, OR a row with a blank BlobName
{ "success": false, "message": "Error opening document.", "correlationId": "…" }

// POST /StaffPatient/DeletePatientDocument  { documentId }
{ "success": true }                                           // NO message — and the SAME answer for an
                                                              // id that matched nothing
{ "success": false, "message": "Invalid document ID." }       // documentId <= 0 or no body — 200, not 400
{ "success": false, "message": "Error deleting patient document.", "correlationId": "…" }
```

Seven behaviours that look like bugs, are not, and must be preserved. Every one was measured against the
running site before and after the Dapper migration, and all **24** captured payloads — the seven endpoints
under a SUPERUSER session and again under a STAFF session, plus ten edge cases — came back byte-identical.

- 🔴 **THE THREE DETAIL PAYLOADS ARE KEYED ON THE PROCEDURE'S RAW COLUMN NAMES, AND THAT IS THE CONTRACT.**
  `"PatientJourney_ID"`, `"iFOBTPositive_Date"`, `"Risks_Smoking"`, `"Findings_Anus"`, `"HPE_Results"` —
  PascalCase with underscores, not camelCase like every other endpoint in the portal. The reason is
  mechanical: the endpoint serializes a **dictionary**, and ASP.NET Core's `JsonSerializerDefaults.Web`
  camelCases *property* names while leaving *dictionary keys* alone. `patientAssessment.js`,
  `patientColonoscopy.js` and `patientFollowUp.js` read exactly those keys. **A POCO here would ship
  `"patientJourney_ID"` and `"risks_Smoking"`, break all three clinical forms, and return `200` doing it** —
  which is why `IDatabaseData` returns `IReadOnlyDictionary<string, object?>` for these three and says so at
  length (§7.8).
- 🔴 **THE JSON PROPERTY IS CALLED `assessment` ON ALL THREE ENDPOINTS**, colonoscopy and follow-up
  included. It is what the three template scripts read. It is not a copy-paste slip to tidy.
- **A journey of the wrong type is `{ success: true, …, "assessment": null }`, not an error.** All three
  detail procedures `INNER JOIN` their detail table, so asking `GetPatientAssessment` for a COLONOSCOPY
  journey returns the journey header and a null body. That is a real state the page renders as an empty
  form; it is also how a caller can tell "no detail row" from "no journey" (which is
  `success: false, "Journey not found."`).
- **`age` is a STRING here and a NUMBER on `/Patient/GetBasic`.** Same column, same procedure
  (`spPatientBasic_GetById`), two endpoints, two shapes — this page prints it into a read-only field, the
  admin form binds it to an input. Both are live and neither moves.
- **Blank versus unknown ids are answered inconsistently across this controller, and each is right for its
  screen.** `GetBasic` and `GetTimeline` both answer a *blank* id with
  `{ success: false, "Invalid patient." }`; then `GetBasic` answers an *unknown* id with
  `{ success: false, "Patient not found." }` while `GetTimeline` answers it with
  `{ success: true, data: [] }` — because a patient genuinely may have no journeys yet, and the timeline
  cannot tell that from a bad id. `GetPatientDocuments` answers a blank id with `success: true` and an
  empty array, because the page calls it before a patient is chosen.
- **`GetBasic` and the three detail reads swallow their exception with a BARE `catch` and no logging.**
  Unlike the Save actions, which log to `app-*.log` with a correlation id, these five return their message
  and nothing reaches any log — a failure there is invisible outside the database. Five of the forty
  unlogged catches §9.2 counts; `BranchController.DeleteBranch` (§4.1) has the same gap.
- **`DeletePatientDocument` answers `{ success: true }` for an id that matched nothing**, because
  `spPatientDocument_Delete` returns NULL in its OUTPUT parameter and the controller treats that as
  "nothing in storage to remove". No `AuditLog` line is written on that path either — the
  `AuditLog.PatientDocumentDeleted` call is inside the `if (blobName is not blank)` branch. Same
  silent-success shape as `spBranch_Delete`, `spPatient_DeleteCascade` and `spPatientAppointment_Delete`.

#### 🔴 The three `Save*` actions — one shape, three types, and the `StaffId` claim

All three are the same nine lines with a different payload:

1. **`model == null` or a blank `PatientId` → `400 { success: false, "Invalid request." }`.** This is the
   only 400 in the controller's write path.
2. **`GetStaffId()` — `User.FindFirst("StaffId")?.Value`.** Blank →
   `200 { success: false, "Your account is not linked to a Staff_ID." }`. That claim is added by
   `AccountController.Login` **only when `User_Type = 3` and the id is non-blank** (§2.1), so this branch
   is how a STAFF account with a missing `Staff_ID` fails — and it is the reason these three actions are
   `StaffOnly` rather than merely authenticated: an ADMIN has no such claim and would hit this message on
   every save.
3. **`PatientJourneyId <= 0` decides create versus update**, exactly as `SaveBasic` uses a blank
   `PatientId` (§4.7) and `SaveAppointment` uses `appointmentId <= 0` (§4.8.2). Nothing verifies that a
   non-zero id exists — the procedure's `RAISERROR 'Journey not found.'` does.
4. **One data-layer call, and the procedure owns the transaction** (§7). No `SqlTransaction` is opened in
   C# and none should be.
5. **`catch (SqlException)` then `catch (Exception)`, both logging and both returning
   `ErrorResponse.ForUser`** with the same string. The two clauses are separate so the log line names the
   kind; the user sees one sentence either way. Every message the procedures raise — *"Patient not found."*,
   *"Staff not found."*, *"Journey not found."*, *"Assessment row not found for this journey."* — reaches
   only `Logs/app-*.log`.

🔴 **`Staff_ID` IS NOT `@User_ID`.** The claim above is a `dbo.Staff.Staff_ID` and it lands in
`PatientJourney.Staff_ID` and `PatientJourneyAudit.Staff_ID`. It is an ordinary business argument, it
appears in the `IDatabaseData` signatures, and it must never be filled from `DatabaseHelper.CurrentUserId`,
which is a `dbo.Users.User_ID`. **None of the twelve journey procedures declares `@User_ID` at all** (§5.8).

#### The document lifecycle, and where the actor DOES come from

The two document *writes* are the only calls in this controller that touch `dbo.AuditTrails`, and both go
through the §0.1 actor mechanism:

| Step | Procedure | `@User_ID` | Audit |
|---|---|---|---|
| upload | `spPatientDocument_Insert` | **ACTOR**, from the claim | `dbo.AuditTrails` INSERT + `AuditLog.PatientDocumentUploaded` |
| download | `spPatientDocument_GetById` | 🔴 **none — the procedure does not declare it** | `AuditLog.PatientDocumentDownloaded` only |
| delete | `spPatientDocument_Delete` | **ACTOR**, from the claim | `dbo.AuditTrails` DELETE + `AuditLog.PatientDocumentDeleted` |

**The upload path validates the whole batch before writing a single blob**, in a pass of its own, because a
file rejected halfway through would leave the earlier files already in the container. After that it loops:
upload the blob, then insert the row, per file. 🔴 **There is no transaction and no compensation** — a
failure on file three leaves files one and two committed, rows and blobs both. That is unlike
`SaveStaffWithDocuments` (§6.6), and the difference is real: the staff side needs atomicity because the
mandatory-document rule makes a staff row without its documents invalid, and no equivalent rule exists for
a patient at upload time (the discharge check runs much later, §5.6).

🔴 **`AuditLog.PatientDocumentUploaded` always records `DocumentId=0`**, because `spPatientDocument_Insert`
computes `SCOPE_IDENTITY()` only to name it in the `AuditTrails` summary and then discards it. The blob key
is what ties the Serilog line to the row. Identical to `spStaffDocument_Insert` (§5.4).

🔴 **THE TWO AUDIT SUMMARIES DISAGREE ABOUT THE DOCUMENT'S TYPE NAME, AND THE INSERT IS THE ONE THAT IS
WRONG.** Measured on the running site, uploading and then deleting one document under type `05`:

```
INSERT  …; DocType=CONSENT FORM (05); FileName=consent.pdf; …
DELETE  …; DocType=HISTORY AND EXAMINATION FORM (05); FileName=consent.pdf; …
```

`spPatientDocument_Insert` writes `@PatientDocumentType_Name` **as the client posted it**; the table stores
only the id, and `spPatientDocument_Delete` re-joins `LU_PATDOCUMENTTYPE` for the real name. So the insert
audit line records whatever label the browser sent — here a stale one from the page — and the delete line
records the truth. The reads (`spPatientDocument_List`, `_GetById`) both re-join too, which is why the
listing showed `HISTORY AND EXAMINATION FORM` all along. **Nothing validates the posted name against the
posted id**, on either side.

#### `GetPatientDocumentUrl` — the SAS, unchanged by this migration

The container is private, so a **five-minute read SAS minted per click by this authenticated action** is
the only way the browser ever reaches the bytes. It is handed back once, never persisted and never rendered
into the page's HTML. `AuditLog.PatientDocumentDownloaded` is written **before** the URL leaves the server,
because the download itself happens against storage where the application can no longer observe it. The
Dapper migration swapped one data call inside this action and touched nothing else; `DOCUMENTSTORAGE.md`
remains authoritative and is unedited.

### 4.10 Settings (Admin > Settings)

`CRC.Web/Controllers/Settings/SettingsController.cs` — **`[Authorize(Policy = "SuperUserOnly")]` on the
class** (`UserType = 1`), no per-action policy. View `Views/Settings/Index.cshtml`, script
`wwwroot/js/settings/index.js`. It edits the two mandatory-document tables and nothing else: **there are no
application settings in nucentra**, and this page's name is broader than its job.

| Verb | Route | Returns |
|---|---|---|
| GET | `/Settings/Index` | the page |
| GET | `/Settings/GetStaffTypes` | 🔴 **a bare JSON array**, not the envelope — `[{ staffTypeId, staffTypeName }]` |
| GET | `/Settings/GetStaffDocumentSettings?staffTypeId=` | `{ success, data: [{ staffDocumentTypeId, staffDocumentTypeName, isMandatory }] }` — **every** type |
| POST | `/Settings/SaveStaffDocumentSettings` | `{ success, message }` |
| GET | `/Settings/GetDischargeTypes` | `{ success, data: [{ dischargeTypeId, dischargeTypeName }] }` |
| GET | `/Settings/GetDischargeDocumentSettings?dischargeTypeId=` | `{ success, data: [{ dischargeTypeId, dischargeTypeName, documentTypeId, documentTypeName }] }` — **only the configured** types |
| POST | `/Settings/SaveDischargeDocumentSettings` | `{ success, message }` |

```jsonc
// GET /Settings/GetStaffDocumentSettings?staffTypeId=END — the full checklist, pre-ticked
{ "success": true, "data": [
  { "staffDocumentTypeId": "01", "staffDocumentTypeName": "CV / RESUME", "isMandatory": true },
  { "staffDocumentTypeId": "02", "staffDocumentTypeName": "BASIC DEGREE CERTIFICATE", "isMandatory": false } ] }
{ "success": false, "message": "Staff type is required." }        // blank staffTypeId

// GET /Settings/GetDischargeDocumentSettings?dischargeTypeId=01 — only what is required
{ "success": true, "data": [
  { "dischargeTypeId": "01", "dischargeTypeName": "NORMAL",
    "documentTypeId": "09", "documentTypeName": "DISCHARGE SUMMARY" } ] }
{ "success": true, "data": [] }        // blank dischargeTypeId — success, NOT an error

// POST /Settings/SaveStaffDocumentSettings   { staffTypeId, staffTypeName, documentTypeIds: [] }
{ "success": true,  "message": "Settings saved successfully." }
{ "success": true,  "message": "Settings saved (no mandatory documents)." }   // empty list — the CLEAR
{ "success": false, "message": "Staff type is required." }
{ "success": false, "message": "Error saving staff document settings." }      // any exception

// POST /Settings/SaveDischargeDocumentSettings   { dischargeTypeId, documentTypeIds: [] }
{ "success": true,  "message": "Settings saved successfully." }               // including the empty list
{ "success": false, "message": "Discharge type is required." }                // HTTP 400, not 200
{ "success": false, "message": "Error saving discharge document settings.", "correlationId": "…" }
```

**Four inconsistencies between the two halves of one screen**, all measured on the running site and all
left exactly as found:

- **The two reads disagree about what "settings" means** — full checklist versus configured rows only
  (§3.16). The page reconciles it in JavaScript.
- **The two blank-parameter reads disagree.** `GetStaffDocumentSettings` with no id answers
  `{ success: false, message: "Staff type is required." }`; `GetDischargeDocumentSettings` with no id
  answers `{ success: true, data: [] }`.
- **The two saves disagree about the HTTP status of a missing id.** The staff save returns `200 Ok` with
  `success: false`; the discharge save returns **`400 BadRequest`**. That is the only `BadRequest` on this
  controller.
- **The two saves disagree about error reporting.** The staff save has a bare `catch (Exception)` that
  returns a fixed string and **logs nothing** — no `_logger.LogError`, no correlation id, so a failed staff
  save cannot be traced to a line in `app-*.log`. The discharge save catches `SqlException` and `Exception`
  separately, logs both, and answers through `ErrorResponse.ForUser`. One of the forty unlogged catches
  §9.2 counts.

🔴 **NOTHING ON THIS SCREEN IS AUDITED, IN EITHER CHANNEL.** None of the five procedures declares `@User_ID`,
so no `dbo.AuditTrails` row is written; and there is no `AuditLog.*` call anywhere in the controller. Adding
or removing a mandatory document changes what the whole programme refuses to discharge, and it leaves no
trace of who did it or when. Verified: running the full round trip below produced zero new `AuditTrails`
rows. That is a statement of the code, not a recommendation.

The save asymmetry — one atomic procedure on the patient side, a delete plus N inserts with no transaction
on the staff side — is the most consequential thing about this controller and is written up in §5.9.

### 4.11 Documents (the SUPERUSER search page)

`CRC.Web/Controllers/Documents/DocumentsController.cs` — **`[Authorize(Policy = "SuperUserOnly")]` on the
class**, no per-action policy, which is the right level: this is the only place in the portal that hands out
**patient and staff documents from one endpoint**. View `Views/Documents/Index.cshtml`, script
`wwwroot/js/documents/index.js`. Four actions:

| Verb | Route | Returns |
|---|---|---|
| GET | `/Documents/Index` | the page |
| GET | `/Documents/GetLookups` | `{ success, patientNames, patientDocTypes, staffNames, staffDocTypes }` |
| POST | `/Documents/Search` | `{ success, data: [{ documentId, id, name, documentType, fileName, uploadedOn }] }` |
| GET | `/Documents/DocumentUrl?mode=&id=` | `{ success, url, fileName }` — a five-minute read SAS |

```jsonc
// GET /Documents/GetLookups — both modes' filters in one response, because the radio switches client-side
{ "success": true,
  "patientNames":   [ { "name": "P8 PATIENT ALPHA" } ],
  "patientDocTypes":[ { "id": "03", "name": "iFOBT RESULTS" } ],
  "staffNames":     [ { "name": "P7 DOCTOR ALPHA" } ],
  "staffDocTypes":  [ { "id": "01", "name": "CV / RESUME" } ] }

// POST /Documents/Search   { mode: "Patient" | "Staff", individualName?, documentType? }
{ "success": true, "data": [
  { "documentId": 8, "id": "PAT-000001", "name": "P8 PATIENT ALPHA",
    "documentType": "iFOBT RESULTS", "fileName": "alpha-ifobt.pdf",
    "uploadedOn": "2026-08-10 12:38:35 +08:00" } ] }
{ "success": false, "message": "Invalid request." }          // HTTP 400, null body only

// GET /Documents/DocumentUrl?mode=Patient&id=8
{ "success": true,  "url": "https://…?sv=…&sig=…", "fileName": "alpha-ifobt.pdf" }
{ "success": false, "message": "Invalid document mode." }    // HTTP 400 — anything but Patient/Staff
{ "success": false, "message": "Invalid document ID." }      // id <= 0
{ "success": false, "message": "Document not found." }       // unknown id, or a row with a blank BlobName
```

**`id` is the OWNER's id and `documentId` is the document's.** `id` is the `Patient_ID` / `Staff_ID` the row
belongs to — what the page shows in its ID column — and `documentId` is the `PatientDocument_ID` /
`StaffDocument_ID`, the only value `DocumentUrl` accepts. Swapping them returns the wrong file or none.

**`BlobName` is returned by the procedure and deliberately not projected.** The container is private, the key
is useless to a browser, and it is exactly the kind of detail that should never leave the server.

🔴 **`Search` AND `DocumentUrl` TREAT AN UNRECOGNISED `mode` DIFFERENTLY, ON PURPOSE.** `Search` folds
anything that is not `"Staff"` into `"Patient"` — a search with no hits is harmless. `DocumentUrl` rejects
it with `400`, because there an unrecognised mode would mean reading the wrong table. Both matches are
case-insensitive after a `Trim()`, so `?mode=staff` works.

**`GetLookups` uses the `*_LookupDocuments` procedures, not the `LU_*` lists**, so a document uploaded under
a type since removed from the lookup is still findable, with its raw id standing in for the missing name
(§5.4, §5.8). It then de-duplicates the staff types in C#, preferring a label that is not just the id — a
defensive pass over a procedure that can return both `(ID, Name)` and `(ID, ID)`. The patient types get a
plain `Distinct()` instead. Both filter lists are sorted **by name in C#**, after the procedure's own
ordering, and both name lists are `Distinct()`ed again even though the procedures already `SELECT DISTINCT`.

**The search is the one read in the portal audited by the application rather than by a procedure.**
`AuditLog.DocumentSearched(HttpContext, mode, individual, docType, list.Count)` records the filters and the
hit count on every call, including the ones with no filters at all — a SUPERUSER listing every patient
document in the system is precisely the read worth having on the security channel. `DocumentUrl` writes
`AuditLog.StaffDocumentDownloaded` or `AuditLog.PatientDocumentDownloaded` **before** the URL leaves the
server, because the download itself happens against storage where the application can no longer see it.

### 4.12 Dashboard (the SUPERUSER landing page)

`CRC.Web/Controllers/Dashboard/DashboardController.cs` — **`[Authorize(Policy = "SuperUserOnly")]` on the
class**. View `Views/Dashboard/Index.cshtml`, script `wwwroot/js/dashboard/`. Five actions, four of them
data, **all reads, none parameterised**: the SUPERUSER dashboard is the whole programme and has no filters
to pass.

| Verb | Route | Returns |
|---|---|---|
| GET | `/Dashboard/Index` | the page |
| GET | `/Dashboard/GetActiveBranchCount` | `{ success, count }` |
| GET | `/Dashboard/GetPatientsByRace` | `{ success, data: [{ label, count }] }` |
| GET | `/Dashboard/GetPatientsByAgeGroup` | `{ success, data: [{ label, count }] }` |
| GET | `/Dashboard/GetPatientsByDischargeType` | `{ success, data: [{ label, count }] }` |

```jsonc
// GET /Dashboard/GetActiveBranchCount
{ "success": true, "count": 1 }
{ "success": false, "message": "Error loading active branch count." }

// GET /Dashboard/GetPatientsByRace
{ "success": true, "data": [ { "label": "MALAY", "count": 4 }, { "label": "CHINESE", "count": 3 },
                             { "label": "INDIAN", "count": 2 }, { "label": "Unknown", "count": 1 } ] }

// GET /Dashboard/GetPatientsByAgeGroup
{ "success": true, "data": [ { "label": "20 and below", "count": 1 }, { "label": "21-40", "count": 3 },
                             { "label": "41-60", "count": 2 }, { "label": "61-80", "count": 2 },
                             { "label": "81 and above", "count": 2 } ] }

// GET /Dashboard/GetPatientsByDischargeType
{ "success": true, "data": [ { "label": "NORMAL", "count": 3 }, { "label": "BENIGN POLYPS", "count": 2 },
                             { "label": "CANCER", "count": 1 } ] }
```

**What each chart actually shows** — the four are not variations on one idea and cannot be read against each
other:

| Card | Population counted | Grouped by | Ordered by |
|---|---|---|---|
| **Active branches** (card) | `dbo.Branch` where `ISNULL(Branch_Status, 0) = 1` | — | — |
| **Patients by race** (pie) | **every** patient in `dbo.PatientBasic` | `LU_RACE.Race_Name`, `COALESCE`d to `"Unknown"` | count **DESC** |
| **Patients by age group** (pie) | **every** patient | a five-band `CASE` over `Patient_Age` | **age**, youngest band first |
| **Patients by discharge type** (bar) | **only patients with a `DischargeType_ID`** | `LU_DISCHARGETYPE.DischargeType_Name`, `COALESCE`d | count **DESC** |

🔴 **THE THREE CHARTS DO NOT SHARE A DENOMINATOR.** Race and age count the whole patient table; discharge
counts only the discharged, because a NULL `DischargeType_ID` *is* the definition of an active patient
(§3.8). A viewer who adds up the third chart and compares it with the first two is comparing two different
questions, and the page does not say so anywhere.

**"Unknown" means something different in each chart.** In the race pie it is a `Race_ID` matching no
`LU_RACE` row, or a race whose name is blank. In the discharge chart it is a discharge reason the lookup no
longer knows — never "not discharged", which the `WHERE` has already excluded. In the age pie it is
**unreachable**: the bucket exists in the `CASE` but `Patient_Age` is `INT NOT NULL`, so nothing can land in
it.

**Two ordering facts a reader should not have to discover empirically.** The two count-ordered charts have
**no tie-breaker**, so two races on the same count arrive in whatever order the engine produced — stable in
practice, guaranteed by nothing, and the reason this area's smoke test uses a fixture with distinct counts.
The age chart is ordered by a second `CASE`, so its axis is fixed regardless of the data.

**Every action catches bare `Exception`, returns `{ success = false, message }` and logs nothing** — no
`_logger`, no `ErrorResponse.ForUser`, so no correlation id. A dashboard that fails is invisible in
`Logs/app-*.log`. Left exactly as found; the same gap `DeleteBranch` has (§4.1). The controller also still
injects `IWebHostEnvironment` and never uses it.

### 4.13 Staff Dashboard (the STAFF landing page)

`CRC.Web/Controllers/StaffDashboard/StaffDashboardController.cs` —
**`[Authorize(Policy = "StaffOnly")]` on the class**, so `UserType = 3` only: a SUPERUSER or ADMIN cannot
open this page at all. View `Views/StaffDashboard/Index.cshtml`, script `wwwroot/js/staffdashboard/`. Four
actions, three of them data.

| Verb | Route | Returns |
|---|---|---|
| GET | `/StaffDashboard/Index` | the page |
| GET | `/StaffDashboard/GetTodayAppointments` | `{ success, data: [ … ] }` |
| GET | `/StaffDashboard/GetThisWeekAppointments` | `{ success, data: [ … ] }` |
| GET | `/StaffDashboard/GetMonthAppointments?year=&month=` | `{ success, data: [ … ], year, month }` |

```jsonc
// GET /StaffDashboard/GetTodayAppointments
{ "success": true, "data": [
  { "patientAppointmentId": 11, "patientId": "PAT-000001", "patientName": "P8 PATIENT ALPHA",
    "appointmentType": "PATIENT ASSESSMENT", "status": "Scheduled", "branchName": "P7 SMOKE BRANCH",
    "appointmentDate": "10/08/2026", "appointmentDateSort": "2026-08-10",
    "fromTime": "09:00", "toTime": "10:00" } ] }

// GET /StaffDashboard/GetMonthAppointments?year=2026&month=9   — empty month, still success
{ "success": true, "data": [], "year": 2026, "month": 9 }
{ "success": false, "message": "Invalid month value." }                                  // month outside 1-12
{ "success": false, "message": "Your user is not linked to a Staff record (StaffId is missing)." }
{ "success": false, "message": "Error loading this month's staff appointments." }
```

🔴 **THE `StaffId` CLAIM IS THE ONLY THING SCOPING THIS PAGE, AND IT IS RESOLVED IN THE CONTROLLER.**
`GetStaffId()` reads `User.FindFirst("StaffId")`, stamped at login from `dbo.Users.Staff_ID`, and each action
passes it to the data layer as an ordinary argument. All three stored procedures filter on
`pa.Staff_ID = @Staff_ID`; there is no second check anywhere. **A STAFF account whose `Staff_ID` is NULL is
refused with a message rather than being run unscoped** — the one case where the absence of a value must not
become "no filter".

**That resolution deliberately does not live in `SqlData`,** and the contrast with `@User_ID` is the point:
the audit actor is bookkeeping nobody should have to remember, so the data layer supplies it; a scoping
predicate is a business argument, and a data-access method that filled one from the ambient claim would be
performing authorization out of sight of the endpoint accountable for it. Moving `GetStaffId()` down a layer
would be a security change, not a refactor.

*Asserted against the running site (Prompt 9):* two STAFF accounts, `END-00002` and `END-00003`, each
holding appointments the other does not. `END-00002` saw ids 11-14 in its three windows and `END-00003` saw
15-17 — no overlap in either direction, and neither saw `END-00001`'s id 18.

**The three windows, which the procedures define and the labels understate:**

- **Today** — `PatientAppointment_Date = @ForDate`, and the endpoint always passes `DateTime.Today`. There
  is no way to ask for another day.
- **"This week"** — a **rolling seven days**, `>= @FromDate` to `< @FromDate + 7`, start inclusive and end
  exclusive. It has nothing to do with Monday: it means "today and the next six days".
- **"This month"** — a real calendar month built by `DATEFROMPARTS(@Year, @Month, 1)`, so month length and
  leap years are the database's problem. **The 1-12 check in the controller is load-bearing**, because
  `DATEFROMPARTS` throws on an out-of-range month rather than returning nothing; the year is not validated
  anywhere and a year outside 1-9999 surfaces as the generic error message.

**Three shaping details that are the page's contract:**

- **`appointmentDate` and `appointmentDateSort` are the same date twice**, `dd/MM/yyyy` for the eye and
  `yyyy-MM-dd` for the table's sort comparator. Both are emitted; neither is derived client-side.
- **`fromTime` / `toTime` are `hh\:mm` strings** cut from `TIME(0)` columns. These procedures return the raw
  time (a `TimeSpan`), unlike `spPatientAppointment_ListByPatient`, which `CONVERT`s to `VARCHAR(5)` in SQL —
  same columns, two different result types, which is why there are two models (§6.3).
- **The controller re-sorts** by date, then start time, then id — the same three keys the procedures already
  order by. Redundant, preserved, and not quite a no-op: a null date or time sorts **last** here
  (`DateTime.MaxValue` / `TimeSpan.MaxValue`) where SQL's `ORDER BY` puts nulls first.

### 4.14 Patient Tracker (Admin > Patient Tracker)

`CRC.Web/Controllers/PatientTracker/PatientTrackerController.cs` —
**`[Authorize(Policy = "AdminOrSuper")]` on the class** (`UserType` 1 or 2, **not** STAFF). View
`Views/PatientTracker/Index.cshtml`, script `wwwroot/js/patienttracker/`. Three actions, two of them data.

| Verb | Route | Returns |
|---|---|---|
| GET | `/PatientTracker/Index` | the page |
| GET | `/PatientTracker/GetAppointmentTypes` | `{ success, data: [{ pjAppTypeId, pjAppTypeName }] }` |
| GET | `/PatientTracker/GetTrackerData` | `{ success, appointmentTypes, patients, appointments, procedures, stalledCount }` |

```jsonc
// GET /PatientTracker/GetTrackerData — the whole page in one response
{ "success": true,
  "appointmentTypes": [ { "pjAppTypeId": "01", "pjAppTypeName": "PATIENT ASSESSMENT" } ],
  "patients": [ { "patientId": "PAT-000003", "name": "P9 PATIENT CHARLIE", "nric": "000003145900",
                  "phone": "0199000003", "age": 18, "gender": "MALE",
                  "dischargeDate": "11/07/2026", "isStalled": true } ],
  "appointments": [ { "patientId": "PAT-000003", "pjAppTypeId": "02",
                      "status": "Completed", "date": "10/08/2026" } ],
  "procedures":   [ { "patientId": "PAT-000003", "pjAppTypeName": "COLONOSCOPY",
                      "date": "08/08/2026" } ],
  "stalledCount": 4 }
{ "success": false, "message": "Error loading tracker data." }
```

**`GetTrackerData` is five sequential procedure calls in one action** — types, patients, appointments,
procedures, stalled count — and is explicitly *not* a unit of work: they are independent parameterless reads
and a row written between the first and the last is seen by some of them and not others. The page loads the
entire programme and filters in the browser; there is no server-side paging, searching or filtering
anywhere on it.

🔴 **WHAT "STALLED" MEANS — the definition lives in SQL and in no other place.**
**A patient is stalled when they have at least one `dbo.PatientAppointment` row AND the status of their most
recent one — latest by date, then start time, then `PatientAppointment_ID`, all descending — is anything
other than `'Scheduled'`.** A patient with **no appointment at all is not stalled**: someone registered and
never booked looks exactly as calm on this page as someone booked for tomorrow. Full derivation in §5.10.

**The flag and the badge are computed twice, by two procedures.** `spPatientTracker_Patients_List` produces
the per-row `isStalled` bit; `spPatientTracker_StalledCount_Get` produces `stalledCount`. They agree today
because the ranking CTE is duplicated character-for-character. **Nothing enforces that**, and the failure
mode is a badge that disagrees with the rows beneath it while both look plausible.

**`appointments` and `procedures` are two different things and the page shows both on purpose.**
`appointments` is what was **booked** — `dbo.PatientAppointment`, reduced to **one row per patient per
journey type**, the newest only. `procedures` is what was **done** — `dbo.PatientJourney`, **every** row.
Nothing in the schema ties a journey row to the appointment that produced it (§3.10), so a patient can have
either without the other, and the grid renders the gap. They also join to the type differently:
`appointments` carries `pjAppTypeId`, `procedures` carries `pjAppTypeName`, because `dbo.PatientJourney`
stores the name and no id at all. **Renaming a row in `LU_PJ_APP_TYPE` silently disconnects the two.**

**`GetAppointmentTypes` is a second endpoint returning what `GetTrackerData` already includes** under
`appointmentTypes`, from the same procedure. Both filter out an entry with a blank id in C#. Both are kept;
neither is dead.

**This controller logs its exceptions** — `_logger.LogError(ex, …)` in both catches — where §4.12 and §4.15
do not. The message to the user is still the bare string, with no correlation id.

### 4.15 Audit Trails (the SUPERUSER audit page)

`CRC.Web/Controllers/AuditTrails/AuditTrailsController.cs` — **`[Authorize(Policy = "SuperUserOnly")]` on the
class**. View `Views/AuditTrails/Index.cshtml`, script `wwwroot/js/audittrails/`. Three actions. **This page
is where the `@User_ID` mechanism of §0.1 becomes visible to a human**, and it is the only reader of
`dbo.AuditTrails` in the portal.

| Verb | Route | Returns |
|---|---|---|
| GET | `/AuditTrails/Index` | the page |
| GET | `/AuditTrails/GetLookups` | `{ success, users, actions, categories }` |
| POST | `/AuditTrails/Search` | `{ success, data: [{ userId, name, dateTime, action, category, summary }] }` |

```jsonc
// GET /AuditTrails/GetLookups — the three filter dropdowns
{ "success": true,
  "users":      [ { "id": "4", "name": "P7 STAFF USER" }, { "id": "1", "name": "SYSTEM SUPERUSER" } ],
  "actions":    [ { "name": "DELETE" }, { "name": "INSERT" }, { "name": "UPDATE" } ],
  "categories": [ { "name": "Branch" }, { "name": "PatientAppointment" }, { "name": "PatientBasic" },
                  { "name": "PatientDocument" }, { "name": "Staff" }, { "name": "StaffDocument" },
                  { "name": "StaffSlots" } ] }
{ "success": false, "message": "Error loading audit trail lookups." }

// POST /AuditTrails/Search  { userId?, fromDate?, toDate?, action?, category? }   — all five optional
{ "success": true, "data": [
  { "userId": 1, "name": "SYSTEM SUPERUSER", "dateTime": "10/08/2026 12:38", "action": "INSERT",
    "category": "PatientBasic",
    "summary": "Created Patient: Patient_ID=PAT-000002; Name=P8 PATIENT BRAVO; …" } ] }
{ "success": false, "message": "Error searching audit trails." }
// 400 { "success": false, "message": "Invalid request." }     // null body only
```

**`"id"` in the users dropdown is a STRING** (`"4"`, not `4`) while `"userId"` in a search result is a
**number**. Both shapes predate this document, both are what `wwwroot/js/audittrails/` reads, and neither is
to be tidied.

🔴 **`@UserId` HERE IS A FILTER, NOT AN ACTOR — and it is the one place in the codebase where the two
spellings sit close enough to be confused.** `spAuditTrails_Search` declares `@UserId INT = NULL`, without
the underscore of §0.1's `@User_ID`, and it means "show me this person's rows". It is passed straight
through from the request body. Filling it from `DatabaseHelper.CurrentUserId` — the reflex nineteen other
call sites in `SqlData` deliberately have — would silently narrow every search to the searcher's own
actions, and the page would look like a working audit trail with almost nothing in it.

**The filters, exactly:**

- **All five are optional and independent**, `AND`ed together, and NULL means "no filter" for each.
- **The dates are Malaysian, the storage is UTC.** The procedure filters and displays the same expression,
  `DATEADD(HOUR, 8, AuditTrail_EventUTC)`. **`toDate` is inclusive of its whole day** (`< @ToDate + 1 day`),
  so `from = to = 2026-08-08` returns that entire Malaysian day.
- **An unparseable date is dropped to "no filter", not rejected.** `DateTime.TryParse` fails, the value stays
  null, and the search runs unfiltered — a typo in a date box silently widens the result set.
- **`action` and `category` are exact matches**, not `LIKE`; blank or whitespace is normalised to "no
  filter". Both come from the dropdowns, which come from the data.

**The dropdowns are built from `dbo.AuditTrails` itself**, `SELECT DISTINCT`, not from any catalogue — so
they can only ever offer combinations that have actually happened. 🔴 **But the users dropdown `INNER JOIN`s
`dbo.Users`, so it hides exactly what an auditor most wants to find**: an actor of `0` (the silent failure of
§0.1) and an actor whose account has since been deleted are both absent from the filter while their rows are
still returned by the search, with a blank name. The dropdown is not a complete list of who is in the table;
`SELECT DISTINCT User_Id FROM dbo.AuditTrails` is.

**Both actions swallow their exception without logging it** — bare `catch (Exception)`, no `_logger`, no
`ErrorResponse.ForUser`, so no correlation id. Same gap as §4.1's `DeleteBranch` and §4.12.

---

## 5. Stored procedures

> **All 104 procedures under `CRC.Database/Stored Procedures/` are catalogued below**, grouped by feature
> area into `###` sub-sections, each with a table giving the procedure's parameters, what it returns, the
> `IDatabaseData` method that calls it, and whether it declares `@User_ID` — and if so, **ACTOR or TARGET**
> (§0.1). Every one of the 104 is called from `SqlData.cs` and from nowhere else; there are no unused
> procedures. **100 have exactly one method; the remaining four — `spPatientAppointment_Insert`,
> `spPatientAppointment_Update`, `spStaffSlots_AssignAppointment` and `spStaffSlots_ClearAppointment` — are
> reachable only through `SaveAppointmentAsync`** (§5.7, §6.7).
>
> The sub-section counts are `14 + 6 + 9 + 12 + 7 + 7 + 12 + 17 + 7 + 16 = 107`, which is **104 plus three
> procedures deliberately tabled twice**: `spStaffDocumentSettings_GetByStaffType` in §5.4 and §5.9, and
> the two `spStaffSlots_*Appointment` procedures in §5.5 and §5.7. Each repetition says so where it occurs.
>
> 🔴 **The recurring lesson of this section is: read the `.sql`, never the family resemblance.** Three of
> nucentra's composed-id inserts return the new id with a trailing `SELECT` and two return it through an
> `OUTPUT` parameter with no result set at all; two procedures named `*_Delete` answer through an `OUTPUT`
> parameter; one returns two result sets and one returns four. Guessing any of these from the name compiles
> perfectly and fails at runtime.

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
  and a behaviour change to the endpoint above it — the owner's call, per §12 decision 10.
- **The insert is the only one that returns anything**, which is why `CreateBranchAsync` is the only Branch
  write that is a `QuerySingleAsync` rather than an `ExecuteAsync`.

### 5.3 Users, authentication and lockout — `CRC.Database/Stored Procedures/Users/` (9)

**Five of the nine declare `@User_ID`, and in all five it is a TARGET, not an actor** — the tell is that
they declare `@User_ID INT` with **no default**, where every audit-actor procedure declares
`@User_ID INT = NULL` (§0.1). **None of the nine writes a `dbo.AuditTrails` row**, which is unusual for
procedures that write: the security trail for login, lockout, unlock and logout is the Serilog audit
channel (`AuditLog.*` → `Logs/audit-*.log`), written by `AccountController`, not by the database.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spUsers_ValidateLogin` | `@Username VARCHAR(100)` | `SELECT TOP 1` — 12 columns incl. `PasswordHash` **and the lockout state**; **empty set** for an unknown username | `GetUserForLoginAsync` → `UserAuthRecord?` | no |
| `spUsers_GetById` | `@User_ID INT` | `SELECT TOP 1` — **9** columns incl. `PasswordHash`, **without** the lockout state; empty set for an unknown id | `GetUserByIdAsync` → `UserAccountRecord?` | **`INT` — TARGET** |
| `spUsers_GetAll` | — | 11 columns, **no `Password_Hash`**, ordered by `User_ID` **DESC** | `GetAllUsersAsync` → `List<UserListItem>` | no |
| `spUsers_Register` | `@User_Name`, `@Username`, `@User_Email`, `@PasswordHash`, `@User_Type`, `@Staff_ID = NULL` | nothing | `RegisterUserAsync` | no |
| `spUsers_RegisterFailedLogin` | `@Username`, `@MaxFailedAttempts`, `@LockoutMinutes`, `@AttemptWindowMinutes`, `@NowUtc = NULL`, **3 OUTPUT params** | **the OUTPUT params, and — new — a one-row result set of the same three values** | `RegisterFailedLoginAsync` → `FailedLoginResult?` | no |
| `spUsers_ResetFailedLogins` | `@User_ID INT` | nothing | `ResetFailedLoginsAsync` | **`INT` — TARGET** |
| `spUsers_Unlock` | `@User_ID INT` | nothing | `UnlockUserAsync` | **`INT` — TARGET** |
| `spUsers_UpdateLastLogin` | `@User_ID INT` | nothing | `UpdateLastLoginAsync` | **`INT` — TARGET** |
| `spUsers_UpdatePassword` | `@User_ID INT`, `@PasswordHash VARCHAR(500)` | nothing | `UpdateUserPasswordAsync` | **`INT` — TARGET** |

#### 🔴 THE ONE `.sql` CHANGE IN THE WHOLE DAPPER MIGRATION

**`spUsers_RegisterFailedLogin` is one of SEVEN procedures in nucentra with `OUTPUT` parameters** — §5.8
carries the complete list, and it is the only one of the seven whose `.sql` was edited. One statement was
**appended** to the end of its body:

```sql
SELECT @LockoutTriggered AS [LockoutTriggered],
       @LockoutEndUtc    AS [LockoutEndUtc],
       @FailedLoginCount AS [FailedLoginCount];
```

**Why.** Dapper reads an `OUTPUT` parameter only through `DynamicParameters` — a string-keyed bag with a
manual `.Get<T>("@Name")` per value, no compiler check on the name or the type. That is precisely the
untyped plumbing this layer exists to delete, and it would have been the only place in `SqlData` doing it.
A result set maps onto `CRC.Data/Models/FailedLoginResult.cs` by name, like everything else in the file.

**What was deliberately NOT changed.** All three `OUTPUT` parameters are still declared, still have their
`= NULL` defaults, and are still `SET` on exactly the paths they were before. The change is **purely
additive**: a caller using `ParameterDirection.Output` gets byte-identical behaviour and simply ignores an
extra result set. Keeping it that way is why an already-deployed procedure could sit in front of an
un-migrated caller without either noticing, and it is the standing rule for editing any `.sql` here (§12).

**The surprise it exposed, and the reason `SqlData` uses `QuerySingleOrDefaultAsync`.** The procedure has
**two early `RETURN` statements** — an unknown `@Username`, and an attempt against an account whose lockout
window is already open — and both **skip the appended `SELECT` entirely, emitting no result set at all**.
`QuerySingleAsync` would throw *"Sequence contains no elements"* on either, turning a failed login into a
500. Neither is reachable from `AccountController.Login`, which calls this only after
`spUsers_ValidateLogin` returned a row and only after its own lockout check has passed — but "unreachable
today" is not "safe", so the method returns `FailedLoginResult?` and null means *no lockout was decided*.
Making the two paths emit a row as well would have meant adding statements before existing `RETURN`s rather
than appending one at the end, and the narrower change was preferred.

The file remains registered in `CRC.Database/CRC.Database.sqlproj` as
`<Build Include="Stored Procedures\Users\spUsers_RegisterFailedLogin.sql" />` (line 206), unmoved and
unreordered. Verified after publishing, against the live `CRC_DB`, with
`sys.dm_exec_describe_first_result_set` (three columns: `bit`, `datetime`, `int`) and `sys.parameters`
(all three parameters still `is_output = 1`) — not just against the `.sql` file.

#### The other findings, from reading all nine

- **`spUsers_ValidateLogin` validates nothing.** It is a plain `SELECT` by username: no password
  comparison, no lockout enforcement, no side effect. A returned row means "this username exists", and
  every decision is made in C#. The name is the most misleading thing in the procedure catalogue.
- **Three procedures over one table disagree on how to spell one column.** `spUsers_ValidateLogin` and
  `spUsers_GetById` alias `Staff_ID` to **`StaffId`**; `spUsers_GetAll` returns it raw as **`Staff_ID`**.
  Since Dapper maps by name, a model that guesses wrong stays silently null. `spUsers_GetAll` *does* alias
  the three lockout columns (`Failed_Login_Count` → `FailedLoginCount`, …), so a single result set mixes
  both conventions. Read the `.sql`.
- **`spUsers_GetById` returns a strict subset of `spUsers_ValidateLogin` — nine columns to twelve — and the
  three it omits are the lockout state.** One shared model would compile and would hand every
  `GetUserByIdAsync` caller `FailedLoginCount = 0` and `LockoutEndUtc = null` on an account that is locked,
  with no exception and nothing in a log. Hence two models, `UserAuthRecord` and `UserAccountRecord`: a
  lockout decision can only be made from the one that has the columns. (Contrast §5.2, where
  `spBranch_ListAll` and `spBranch_GetById` genuinely return the same seven columns and correctly share
  `BranchDetail`. Reuse the shape, never the name.)
- **`spUsers_GetAll` is the only read that omits `Password_Hash`**, and it is also the only one whose result
  reaches a browser. `UserListItem` therefore has no hash property — a hash that is not in the model cannot
  be leaked by a careless `Ok(users)`.
- **Two of the writes `RAISERROR` on a bad id and two are silent, and the split is not principled.**
  `spUsers_Unlock` and `spUsers_UpdatePassword` both check `IF NOT EXISTS … RAISERROR('User not found.', 16, 1)`;
  `spUsers_ResetFailedLogins` and `spUsers_UpdateLastLogin` just run an `UPDATE` that matches nothing and
  return normally. So `UnlockUserAsync` can throw a `SqlException` where `ResetFailedLoginsAsync` cannot —
  which is fine here only because both of the silent ones are called with an id that was just read from the
  database.
- **`spUsers_Register` enforces four rules the schema does not**: unique `Username` (this one *is* backed by
  `IX_Users_Username`), `Staff_ID` required for `User_Type = 3`, `Staff_ID` must exist in `dbo.Staff`, and
  `Staff_ID` must not already be linked to another account. All four are `RAISERROR` severity 16 → a
  `SqlException` in C# → the single user-facing message *"Unable to register user. Please verify the inputs
  and try again."* **They are procedure logic, not constraints** — a direct `INSERT` bypasses every one.
- **`spUsers_RegisterFailedLogin` never clears an expired lockout**: it writes
  `Lockout_End_Utc = COALESCE(@NewLockoutEnd, [Lockout_End_Utc])`, so a stale timestamp survives until a
  successful login or an unlock. That is why "is this account locked" is a comparison and not a column
  (§2.6).
- **`@NowUtc DATETIME = NULL` is a seam nobody uses.** The procedure falls back to `GETUTCDATE()`, and no
  caller passes it. It exists so the lockout arithmetic can be tested at a fixed instant; `SqlData` does not
  send it, so the decision is made on the **SQL Server's** clock while the controller's own lockout check
  uses the **web server's** `DateTime.UtcNow`.

### 5.4 Staff and staff documents — `Stored Procedures/{Staff,StaffDocument,StaffDocumentSettings}/` (12)

**Five of the twelve declare `@User_ID INT = NULL` — the ACTOR** (§0.1): `spStaff_Insert`,
`spStaff_Update`, `spStaff_Delete`, `spStaffDocument_Insert` and `spStaffDocument_Delete`. All five write a
`dbo.AuditTrails` row with `ISNULL(@User_ID, 0)`, which is the silent-failure surface: drop the parameter
and the write still succeeds, naming nobody. The other seven declare no `@User_ID` and write no audit row.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spStaff_List` | — | 7 columns (`Staff_ID`, `Staff_Name`, `Staff_NRIC`, `Staff_Phone`, `Staff_Email`, `Staff_Type`, `StaffType_Name`), `LEFT JOIN LU_STAFFTYPE`, ordered by `Staff_Name` | `GetAllStaffAsync` → `List<StaffListItem>` | no |
| `spStaff_GetById` | `@Staff_ID VARCHAR(100)` | `SELECT TOP 1` — **16** columns, the whole row plus `StaffType_Name`; **empty set** for an unknown id | `GetStaffByIdAsync` → `StaffDetail?` | no |
| `spStaff_Insert` | the 14 `Staff` columns except `Staff_ID`, plus `@User_ID` | `SELECT @Staff_ID AS NewStaff_ID` — **one row, `VARCHAR(100)`** | `CreateStaffAsync` → `string`, **and** `SaveStaffWithDocumentsAsync` | **`INT = NULL` — ACTOR** |
| `spStaff_Update` | the same 14 plus `@Staff_ID` and `@User_ID` | nothing | `UpdateStaffAsync`, **and** `SaveStaffWithDocumentsAsync` | **`INT = NULL` — ACTOR** |
| `spStaff_Delete` | `@Staff_ID`, `@User_ID` | 🔴 **TWO result sets** — see below | `DeleteStaffAsync` → `StaffDeleteResult` | **`INT = NULL` — ACTOR** |
| `spStaffDocument_List` | `@Staff_ID VARCHAR(100) = NULL` | 9 columns incl. `BlobName`, ordered `UploadedOn DESC, StaffDocument_ID DESC` | `GetStaffDocumentsAsync` → `List<StaffDocumentItem>` | no |
| `spStaffDocument_GetById` | `@StaffDocument_ID INT` | `SELECT TOP 1` — the **same 9 columns**; empty set for an unknown id | `GetStaffDocumentByIdAsync` → `StaffDocumentItem?` | no |
| `spStaffDocument_Insert` | `@Staff_ID`, `@Staff_Name`, `@StaffDocumentType_ID`, `@StaffDocumentType_Name`, `@FileName`, `@BlobName`, `@ContentType`, `@User_ID` | nothing — **not even the new identity** | `AddStaffDocumentAsync`, **and** `SaveStaffWithDocumentsAsync` | **`INT = NULL` — ACTOR** |
| `spStaffDocument_Delete` | `@StaffDocument_ID`, `@User_ID`, **`@DeletedBlobName VARCHAR(500) = NULL OUTPUT`** | nothing; the answer is the OUTPUT parameter | `DeleteStaffDocumentAsync` → `string?`, **and** `SaveStaffWithDocumentsAsync` | **`INT = NULL` — ACTOR** |
| `spStaffDocument_LookupDocuments` | — | `StaffDocumentType_ID, StaffDocumentType_Name` — the **union** of types in use and types in the lookup | `GetStaffDocumentTypeFiltersAsync` → `List<LookupItem>` | no |
| `spStaffDocument_StaffNames` | — | `Staff_Name` only, `DISTINCT`, `INNER JOIN`, ordered by name | `GetStaffDocumentStaffNamesAsync` → `List<string>` | no |
| `spStaffDocumentSettings_GetByStaffType` | `@StaffType_ID VARCHAR(100)` | **every** `LU_STAFFDOCUMENTTYPE` row plus a computed `IsMandatory INT` | `GetStaffDocumentSettingsAsync` → `List<StaffDocumentSetting>` | no |

`spStaff_GetPerformance` also lives in `Stored Procedures/Staff/`, but it backs the Performance tab rather
than the staff register, so it is documented in §5.5 with the slot procedures.

#### 🔴 `spStaff_Delete` returns two result sets, and it is three procedures wearing one name

It is the most consequential procedure in this area and the only one in the group that cannot be an
`ExecuteAsync`. It has **three exit paths, and every one of them emits exactly two grids**:

```
grid 1   one row    Status VARCHAR(20), Message VARCHAR(500)
grid 2   N rows     BlobName VARCHAR(500)
```

| `Status` | When | grid 2 | What was written |
|---|---|---|---|
| `NotFound` | no `dbo.Staff` row has that id | `SELECT TOP 0 …` — **empty placeholder** | nothing |
| `Blocked` | the staff member is still referenced | `SELECT TOP 0 …` — **empty placeholder** | nothing |
| `Success` | the delete ran | the keys of every deleted `StaffDocument` | four `DELETE`s + one `AuditTrails` row |

**The `SELECT TOP 0 CAST(NULL AS VARCHAR(500)) AS [BlobName]` on the two early-return paths is the whole
reason a caller can read two grids unconditionally.** Without it, `QueryMultipleAsync` would have to
inspect the status before deciding whether a second grid exists. So **the grid COUNT is stable and a grid
may legitimately be empty** — and an empty grid 2 on the `Success` path just means the staff member had no
documents. `Status` is the only thing that says whether the delete happened. All three paths were driven
through the running site during this prompt's smoke test.

**What blocks a delete.** Three `IF EXISTS` checks, and they are the *only* referential protection
`dbo.Staff` has (§3.4 — there are no foreign keys):

```
dbo.PatientAppointment    → "Patient Appointments"
dbo.PatientJourney        → "Patient Journey"
dbo.PatientJourneyAudit   → "Patient Journey Audit"
```

The matching names are comma-joined into one sentence and handed to the user verbatim by the controller:

```
Cannot delete this staff because they are still referenced by: Patient Appointments, Patient Journey.
```

So a clinician who has ever been booked, or who has ever recorded a journey step, **can never be deleted**
through the portal — there is no soft delete, no "inactive" flag and no override. Nothing in the schema
enforces this, so a direct `DELETE` bypasses it entirely and orphans those rows.

**What it cascades to when it is not blocked**, inside a `BEGIN TRANSACTION` of its own with `THROW` on
failure — four deletes, by hand:

```
dbo.StaffSlots     WHERE Staff_ID = @Staff_ID     -- their published availability
dbo.StaffDocument  WHERE Staff_ID = @Staff_ID     -- rows only; the blobs are the caller's problem
dbo.Users          WHERE Staff_ID = @Staff_ID     -- 🔴 THEIR LOGIN
dbo.Staff          WHERE Staff_ID = @Staff_ID
```

🔴 **Deleting a staff member deletes their user account.** Nothing in the UI says so and the audit summary
mentions it only as `CascadedUsers=Yes`. It is the correct behaviour — a `Users` row whose `Staff_ID`
points at nothing still signs in, with a `StaffId` claim that resolves to no staff member (§3.3) — but it
is a second, silent deletion performed by a procedure named for the first.

**The blob keys are captured into a table variable BEFORE the `DELETE`**, which is the only reason grid 2
can exist at all: after the rows are gone there is nothing left to read them from. Storage takes no part in
the transaction, so the caller removes the objects afterwards, best-effort, and logs a warning if a removal
fails (§6.6).

#### The other findings, from reading all twelve

- 🔴 **`spStaffDocument_Delete` ANSWERS THROUGH AN `OUTPUT` PARAMETER** — one of the seven that do (§5.8).
  `@DeletedBlobName VARCHAR(500) = NULL OUTPUT` carries the blob key of the row that was just deleted, or
  `NULL` when no row matched, and the procedure has **no result set at all** to read it from. Unlike
  `spUsers_RegisterFailedLogin`, which had a trailing `SELECT` appended to it (§5.3), this one has nothing
  to append to without changing its behaviour — so `SqlData.DeleteStaffDocumentAsync` reads the parameter
  through `DynamicParameters`, with the name and the `DbType.AnsiString` written out where a reader can
  check them against the `.sql`.
- **`spStaffDocument_Insert` does not return the new `StaffDocument_ID`.** It computes it
  (`SCOPE_IDENTITY()`) purely to put it in the `AuditTrails` summary, and then discards it. That is why
  **every `AuditLog.StaffDocumentUploaded` line in the portal records `DocumentId=0`** and identifies the
  row by its blob key instead. The database trail has the id; the Serilog trail does not. Making them agree
  is an additive `.sql` change and the owner's call, per §12 decision 10.
- **`spStaff_Update` audits only when a row actually changed** (`IF @RowsAffected > 0`) and returns nothing
  either way, so an update against an unknown id succeeds silently and reports success — the same
  asymmetry `spBranch_Update` has (§5.2). `spStaff_Insert`, by contrast, is the only one of the three that
  validates anything: `RAISERROR` on a blank `@Staff_Type`.
- **`spStaffDocument_List` and `spStaffDocument_GetById` are the same nine-column `SELECT` with different
  `WHERE` clauses**, which is why they correctly share `StaffDocumentItem` — contrast `spUsers_GetById`
  versus `spUsers_ValidateLogin` (§5.3), where one is a strict subset of the other and sharing would hide a
  lockout. Reuse the shape, never the name.
- **Both document reads join on `UPPER(LTRIM(RTRIM(ISNULL(…, ''))))`** on both sides of both joins. That is
  a defence against ids that differ only in case or trailing spaces — which the schema permits, since none
  of these columns is a foreign key — and it makes both joins non-sargable, so neither can use an index on
  `Staff_ID`. At this table's size it does not matter; it is worth knowing before anyone wonders why a
  document query scans.
- **`spStaffDocument_List`'s `@Staff_ID` is optional** (`= NULL`, and `''` is treated the same way), in
  which case it returns **every document in the system**. Nothing calls it that way — `StaffController`
  rejects a blank `staffId` first — but the capability is there, and the Documents search page (§4.11) is
  presumably what it was written for, though that page uses `spDocuments_Search` instead.
- **`spStaffDocument_LookupDocuments` unions the types IN USE with the types in the lookup**, and
  `COALESCE`s a missing name back to the raw id. It exists because `StaffDocument.StaffDocumentType_ID` has
  no foreign key (§3.5): a document uploaded under a type later removed from `LU_STAFFDOCUMENTTYPE` must
  still be findable, so the filter offers the orphaned id with the id as its own label. An upload form must
  **not** offer that type, which is why it uses `spLU_STAFFDOCUMENTTYPE_List` instead. Two procedures, two
  correct answers, one lookup table.
- **`spStaffDocument_StaffNames` returns names, not ids, and `INNER JOIN`s.** A document whose `Staff_ID`
  matches no staff row contributes nothing to the filter, and two staff members sharing a name collapse
  into one entry — which is what a filter control keyed on the displayed name wants, and why the method
  returns `List<string>` rather than a lookup pair.
- **`spStaffDocumentSettings_GetByStaffType` drives `LU_STAFFDOCUMENTTYPE` and `LEFT JOIN`s the settings
  table**, so it returns all eight document types with `IsMandatory` computed per row (§3.6). Its row count
  is therefore constant and answers nothing; every caller filters `IsMandatory = 1` itself. `IsMandatory`
  is an `INT`, not a `BIT`.

### 5.5 Staff slots and staff performance (5 with a method of their own, 2 inside a transaction)

**Two of the five declare `@User_ID INT = NULL` — the ACTOR** (§0.1): `spStaffSlots_CreateRange` and
`spStaffSlots_Delete`. Both write a `dbo.AuditTrails` row with `ISNULL(@User_ID, 0)`, which is the
silent-failure surface. The other three declare no `@User_ID` and write no audit row.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spStaffSlots_List` | `@Staff_ID VARCHAR(100)`, `@FromDate DATE = NULL`, `@ToDate DATE = NULL` | 5 columns, ordered `SlotDate, SlotStartTime`; **empty set** for an unknown staff id | `GetStaffSlotsAsync` → `List<StaffSlotItem>` | no |
| `spStaffSlots_GetOwner` | `@StaffSlot_ID INT` | `SELECT TOP 1 Staff_ID`; **empty set** for an unknown slot | `GetStaffSlotOwnerAsync` → `string?` | no |
| `spStaffSlots_CreateRange` | `@Staff_ID`, `@FromDate DATE`, `@ToDate DATE`, `@StartTime TIME(0)`, `@EndTime TIME(0)`, `@User_ID` | `SELECT @CreatedCount, @SkippedExistingCount` — **one row** | `CreateStaffSlotRangeAsync` → `StaffSlotCreateResult` | **`INT = NULL` — ACTOR** |
| `spStaffSlots_Delete` | `@StaffSlot_ID INT`, `@User_ID` | nothing — it answers by `THROW`ing | `DeleteStaffSlotAsync` | **`INT = NULL` — ACTOR** |
| `spStaff_GetPerformance` | `@Staff_ID VARCHAR(100)` | 🔴 **FOUR result sets** — see below | `GetStaffPerformanceAsync` → `StaffPerformanceResult` | no |

`spStaff_GetPerformance` lives in `Stored Procedures/Staff/` with the other five `spStaff_*` procedures
(§5.4) but belongs to the Performance tab, so it is documented here.

#### 🔴 The two `StaffSlots` procedures with NO method of their own, and why

`Stored Procedures/StaffSlots/` holds **six** files. The two missing from the table above are
**`spStaffSlots_AssignAppointment`** (`@ApptId INT`, `@StaffSlotIds VARCHAR(MAX)` — a comma-separated list
split with `STRING_SPLIT`, stamping the appointment id onto every named slot) and
**`spStaffSlots_ClearAppointment`** (`@ApptId INT` — clearing it off every slot that carries it).

**They are deliberately absent from `IDatabaseData`, not forgotten.** Both run only from inside
`SaveAppointmentAsync` (§6.7), which reads `spStaffSlots_List` under a lock, checks that every chosen hour
is still free, writes the appointment, and *then* claims the slots. Publishing them as standalone data-layer
methods would offer a second way to change a slot's booking state — one that is **not** inside that
transaction — and that race is precisely what the transaction exists to prevent. They are two of the four
procedures in the whole catalogue with no method of their own; the other two are
`spPatientAppointment_Insert` and `_Update`, absent for the same reason (§5.7). The banner comment in
`IDatabaseData.cs` says so at the place a future author would otherwise add them.

#### 🔴 `spStaff_GetPerformance` returns FOUR result sets, and the order is the whole contract

**`DapperLayerPlan.md`'s Prompt 4 calls this a "five-result-set procedure". It has four.** The `.sql` has
four statement-level `SELECT`s; the fifth thing that looks like one is the `SELECT` inside the `Findings`
CTE that feeds grid 4. This was checked three ways and all three agree: the file, the deployed definition in
`CRC_DB` (`sys.sql_modules`), and `StaffPerformanceController`, which has only ever read `ds.Tables[0..3]`.
Four is the contract.

| # | Rows | Columns | From | What it is |
|---|---|---|---|---|
| **1** | **exactly 1** | `TotalColonoscopy INT`, `TotalColonoscopyThisMonth INT` | `dbo.PatientJourney` | Two conditional `SUM`s over this clinician's journey rows, matched on `UPPER(PjAppType_Name) = 'COLONOSCOPY'` — **on the denormalized NAME, not on `PjAppType_ID`**. "This month" is `Created_At` within `[DATEFROMPARTS(YEAR(SYSDATETIME()), MONTH(SYSDATETIME()), 1), +1 month)`. |
| **2** | N | `PjAppType_ID`, `PjAppType_Name`, `TotalHours DECIMAL(10,2)` | `dbo.PatientAppointment` | Hours per appointment type: `SUM(DATEDIFF(MINUTE, start, end)) / 60.0`, **only where `PatientAppointment_Status = 'Attended'`** and the times are present and ordered. `LEFT JOIN LU_PJ_APP_TYPE`, grouped by type, **ordered by `PjAppType_Name`**. |
| **3** | N | `Complication VARCHAR`, `Total INT` | `dbo.PatientColonoscopy` | One row per distinct `Complications` value, `COUNT(*)` of colonoscopies. `INNER JOIN PatientJourney` on `Staff_ID`; NULL and blank values excluded; ordered by the value. |
| **4** | N | `TypeOfAnomaly`, `PatientCount INT` | `dbo.PatientColonoscopy` | One row per anomaly kind, `COUNT(DISTINCT Patient_ID)`. See below — this is the interesting one. |

**Nothing in a result set says which grid it is, and grids 3 and 4 have identical shapes — one string and
one integer.** Read them out of order and the Performance panel renders complications under the Anomalies
heading and vice versa: no exception, no log line, the right number of rows, plausible words in the wrong
box. `SqlData.GetStaffPerformanceAsync` reads them strictly in the order above, and the before/after JSON
diff over a database seeded so that **all four grids are non-empty and grids 3 and 4 differ** is what proves
it. A diff taken against a clinician with no history would have proved nothing.

**Grid 4 is nine JSON columns flattened into one.** `dbo.PatientColonoscopy` records its findings per bowel
segment — anus, rectum, sigmoid colon, descending colon, splenic flexure, transverse colon, hepatic flexure,
ascending colon, caecum — each in its own `NVARCHAR` column holding a JSON document. The procedure
`CROSS APPLY (VALUES …)`s all nine into a single column, keeps the rows where `ISJSON() = 1`, and pulls
`JSON_VALUE(…, '$.TypeOfAnomaly')` out of each. So **a finding recorded in three segments of one patient is
one row with `PatientCount = 1`** — the `COUNT(DISTINCT Patient_ID)` is what makes this grid answer a
different question from grid 3's `COUNT(*)`, despite the identical shape. A JSON document with no
`TypeOfAnomaly` key, or a column that is not valid JSON, contributes nothing and raises nothing.

🔴 **Grid 1 returns one row of NULLs, not zero rows, for a clinician with no journeys.** It is an aggregate
with no `GROUP BY`, so `SUM` over an empty set is `NULL` and the row still comes back. `ReadSingleAsync` is
therefore correct — but the two properties on `StaffPerformanceResult` **must** be `int?`, or Dapper throws
and "a staff member who has done nothing yet" becomes a 500. This is the one nullability in the area that is
real rather than defensive, and it is why `/StaffPerformance/Get?staffId=NOSUCHSTAFF` answers `0` and not an
error (§4.6).

#### The other findings, from reading all five

- 🔴 **`spStaffSlots_Delete` HAS NO RESULT SET AND NO ROW COUNT — IT ANSWERS BY THROWING.**
  `THROW 50002, 'Cannot delete a slot that is already taken.'` when `PatientAppointment_ID` is not null, and
  `THROW 50003, 'Slot not found.'` when the `DELETE` matched nothing. Both arrive in C# as a `SqlException`,
  and `StaffScheduleController` answers both with the generic *"An unexpected error occurred."* plus a
  correlation id (§4.5). **That 50002 is the only thing protecting a booked hour**: the foreign key on
  `StaffSlots.PatientAppointment_ID` constrains the appointment's existence, not the slot's (§3.7), so a
  direct `DELETE` orphans the appointment. Verified against the running site.
- **The `DELETE` is itself guarded a second time** — `WHERE StaffSlot_ID = @x AND PatientAppointment_ID IS
  NULL` — after the `IF EXISTS` check has already thrown for that case. The redundancy is deliberate: the
  check and the delete are two statements and nothing holds a lock between them.
- **`spStaffSlots_CreateRange` validates five rules and reports none of them usefully.** A blank
  `@Staff_ID`, `@ToDate` before `@FromDate`, a range over **31 days**, `@EndTime` at or before `@StartTime`,
  and a time that is not on the hour are all `THROW 50001` with a specific message — which the controller
  never shows, because it catches `SqlException` generically. The 31-day cap exists nowhere else in the
  product and is invisible from the UI.
- **It builds its rows from `sys.all_objects`, twice, and that is where the two baseline `SQL71502` build
  warnings come from** (§3.7). `SELECT TOP (n) ROW_NUMBER() OVER (ORDER BY (SELECT NULL))` over the
  catalogue view is a row generator standing in for a numbers table nucentra does not have: one CTE for the
  days, one for the hours, `CROSS JOIN`ed. Lines 46 and 52. Expected, pre-existing, not to be chased.
- **The insert is a `MERGE … WITH (HOLDLOCK) … WHEN NOT MATCHED`, `OUTPUT $action`** into a table variable,
  which is how it counts what it created without a duplicate-key error. `HOLDLOCK` is what makes
  "check then insert" atomic against a concurrent identical request. `SkippedExistingCount` is derived by
  subtraction, not counted. **One `dbo.AuditTrails` row per call**, summarising the range — not one per
  slot — and it is written **whether or not anything was created**: re-running an existing range writes an
  `INSERT` audit row saying `CreatedCount=0`.
- **`spStaffSlots_Delete` captures the slot's details into local variables BEFORE deleting**, for the same
  reason `spStaff_Delete` captures blob keys into a table variable (§5.4): the audit summary names the staff
  id, date and times of a row that no longer exists by the time the `INSERT` runs.
- **`spStaffSlots_List` returns the two time columns as `VARCHAR(5)`, not `TIME`.**
  `CONVERT(VARCHAR(5), SlotStartTime, 108)` — so `"09:00"` reaches C# as a string even though the column is
  `TIME(0)`. `StaffSlotItem` keeps them as strings and the appointment flow parses them with
  `TimeSpan.Parse` at its own call site; moving that parse into the data layer would move where a malformed
  value throws, which is a behaviour change disguised as a tidy-up. Its trailing comment —
  `-- keeps same ordering behavior` — records that the `ORDER BY SlotDate, SlotStartTime` is inherited
  contract — though the schedule grid does re-sort it: `edit-staffschedule.js` initialises DataTables with
  `ordering: true` and no explicit default, so the browser re-sorts on the first column.
- **`spStaffSlots_GetOwner` exists purely for an authorization check**, which is unusual enough to say out
  loud: it is a one-column, one-row read whose only caller is `StaffScheduleController.Delete`, and its whole
  job is to answer "whose slot is this?" without trusting the request. Its header comment says so. See §4.5.

### 5.6 Patient basic — `Stored Procedures/{PatientBasic,PatientDocumentSettings}/` (7)

**Three of the seven declare `@User_ID INT = NULL` — the ACTOR** (§0.1): `spPatientBasic_Insert`,
`spPatientBasic_Update` and `spPatient_DeleteCascade`. All three write a `dbo.AuditTrails` row with
`ISNULL(@User_ID, 0)`, which is the silent-failure surface: drop the parameter and the write still
succeeds, naming nobody. The other four declare no `@User_ID` and write no audit row.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spPatientBasic_ListActive` | — | 5 columns (`Patient_ID`, `Patient_Name`, the three iFOBT ones) where `DischargeType_ID IS NULL`, ordered `Patient_ID DESC` | `GetActivePatientsAsync` → `List<PatientListItem>` | no |
| `spPatientBasic_ListDischarged` | — | the same 5 **plus `Patient_DischargeDate`** where `DischargeType_ID IS NOT NULL`, ordered `Patient_DischargeDate DESC, Patient_ID DESC` | `GetDischargedPatientsAsync` → `List<PatientDischargedItem>` | no |
| `spPatientBasic_GetById` | `@Patient_ID VARCHAR(100)` | **33 columns** — the whole 27-column row plus six `LEFT JOIN`ed lookup names; **empty set** for an unknown id | `GetPatientByIdAsync` → `PatientBasicDetail?` | no |
| `spPatientBasic_Insert` | the 23 non-discharge columns except `Patient_ID`, plus **`@NewPatient_ID VARCHAR(100) OUTPUT`** and `@User_ID` | 🔴 **nothing — no result set at all.** The new id is the OUTPUT parameter | `CreatePatientAsync` → `string` | **`INT = NULL` — ACTOR** |
| `spPatientBasic_Update` | those 23 plus `@Patient_ID` and the 3 discharge columns — **27 business parameters** — plus `@User_ID` | nothing | `UpdatePatientAsync` | **`INT = NULL` — ACTOR** |
| `spPatient_DeleteCascade` | `@Patient_ID`, `@User_ID` | **one** result set: `BlobName VARCHAR(500)`, N rows — see below | `DeletePatientCascadeAsync` → `PatientDeleteResult` | **`INT = NULL` — ACTOR** |
| `spPatient_Discharge_CheckMissingDocuments` | `@Patient_ID`, `@DischargeType_ID` | `PatientDocumentType_ID, PatientDocumentType_Name` — **the MISSING ones**; an empty set is the pass | `GetMissingDischargeDocumentsAsync` → `List<PatientDocumentRequirement>` | no |

`spPatient_Discharge_CheckMissingDocuments` lives in `Stored Procedures/PatientDocumentSettings/` with the
Settings-screen procedures (§5.9) but belongs to the discharge flow, so it is documented here.

#### 🔴 `spPatientBasic_Insert` answers through an OUTPUT parameter

**`@NewPatient_ID VARCHAR(100) OUTPUT`. There is no trailing `SELECT`.** That matters because the two other
composed-id inserts in nucentra do the opposite: `spBranch_Insert` ends `SELECT @Branch_ID AS NewBranch_ID`
and `spStaff_Insert` ends `SELECT @Staff_ID AS NewStaff_ID`, so both are a `QuerySingleAsync<string>`
(§5.2, §5.4). Assume the same shape here and `QuerySingleAsync` throws *"Sequence contains no elements"* on
every successful insert. **Read the `.sql` before writing the method; the family resemblance is a trap** —
and it is a trap in both directions, since the three `…WithJourney` creates go the other way (§5.8).

It is one of the seven `OUTPUT`-parameter procedures listed in §5.8, and one of the six that `SqlData`
reads with `DynamicParameters` — `DbType.AnsiString`, size 100.

`CreatePatientAsync` therefore builds a `DynamicParameters`, pours the ordinary arguments in with
`AddDynamicParams(new { … })` so the parameter list still reads as one block, adds the OUTPUT parameter,
and reads it back with `.Get<string?>("NewPatient_ID")`. **A blank answer throws rather than returning `""`**
— the procedure `SET`s the id before it `INSERT`s and has no path that skips doing so, so a blank there
would mean the row went in under an empty primary key, and handing that back to the browser as the
patient's identity is worse than a caught exception.

#### 🔴 `spPatient_DeleteCascade` — one grid, seven tables, no transaction, no guard

**`DapperLayerPlan.md`'s Prompt 5 says this procedure "returns a summary AND a set of BlobName keys" and
that the method should be a `QueryMultipleAsync`. It returns ONE result set.** The body has a single
statement-level `SELECT`, its last line, `SELECT [BlobName] FROM @DocBlobs;`. Checked three ways and all
three agree: the `.sql` file, the deployed definition in `CRC_DB`, and the `DataTable` code this migration
replaced, which read `ds.Tables[0]` and indexed it by `"BlobName"` — **grid 0 *is* the keys**. The plan was
reasoning by analogy with `spStaff_Delete` (§5.4), which really does emit `{Status, Message}` and then
`{BlobName}`. So `DeletePatientCascadeAsync` is a `QueryAsync`; a `QueryMultipleAsync` with two `ReadAsync`
calls would throw on the second.

**What it cascades to, in order** — seven `DELETE` statements, by hand:

```
1  dbo.PatientAppointment    WHERE Patient_ID = @Patient_ID   -- every booking, past and future
2  dbo.PatientJourney        WHERE Patient_ID = @Patient_ID   -- the whole clinical journey
   ── capture BlobNames into @DocBlobs here, BEFORE the next statement ──
3  dbo.PatientDocument       WHERE Patient_ID = @Patient_ID   -- rows only; the blobs are the caller's job
4  dbo.PatientFollowUp       WHERE Patient_ID = @Patient_ID
5  dbo.PatientColonoscopy    WHERE Patient_ID = @Patient_ID
6  dbo.PatientAssessment     WHERE Patient_ID = @Patient_ID
7  dbo.PatientBasic          WHERE Patient_ID = @Patient_ID   -- and @@ROWCOUNT off THIS one gates the audit
```

Four things about it are worth knowing, and three of them are asymmetries with `spStaff_Delete`:

- 🔴 **NOTHING BLOCKS IT.** `spStaff_Delete` has three `IF EXISTS` guards that refuse while a clinician is
  still referenced, and they are the only referential protection `dbo.Staff` has. This procedure has none:
  a patient with a completed colonoscopy and a year of appointments is erased as readily as one registered
  five minutes ago. There is no soft delete, no "inactive" flag and no confirmation beyond the browser's.
- 🔴 **THERE IS NO TRANSACTION.** `spStaff_Delete` wraps its four deletes in `BEGIN TRANSACTION … THROW`.
  These seven run bare, so a failure partway through — a lock timeout on statement 5, say — leaves the
  appointments, journey and documents gone and the patient row still there. Nothing detects that state
  afterwards.
- **The blob keys are captured into a table variable BEFORE statement 3**, which is the only reason they
  can be returned at all: after the rows go there is nothing left to read them from. Same technique
  `spStaff_Delete` uses, and for the same reason — **storage cannot join a database transaction**, so the
  keys travel out and `PatientController.DeletePatient` removes the objects afterwards, best effort, one at
  a time, logging a warning per failure (§6.6). Leaving them would not merely waste storage: it would
  retain patient data after the patient record itself has been deleted.
- **The audit row is guarded by `IF @RowsAffected > 0` on statement 7**, so a delete against an unknown id
  writes nothing and returns normally — a silent success, like `spBranch_Update` and `spBranch_Delete`
  (§5.2). **The `dbo.AuditTrails` row is the only record anywhere that this happened**, since
  `dbo.PatientBasic` has no audit columns of its own (§3.8), and it names the patient in a summary *string*
  rather than by a key — so it survives the deletion and cannot be joined back to anything.

Both paths were driven through the running site: a patient with two `PatientDocument` rows came back with
both blob keys, which reached `IDocumentStorage.DeleteAsync` individually (each is named in a warning line
in `app-*.log`, because Azurite was not running locally — which is exactly the best-effort branch working),
and a patient with none came back with an empty list and `BlobCount=0` on the
`AuditLog.PatientDocumentsPurged` line. **An empty list means "no storage to reclaim", never "something
failed"** — and, because there is no status grid, *nothing in the result says whether a row was deleted at
all*.

#### 🔴 `spPatient_Discharge_CheckMissingDocuments` — the inverted result set

```sql
SELECT s.PatientDocumentType_ID, s.PatientDocumentType_Name
FROM dbo.PatientDocumentSettings s
WHERE s.DischargeType_ID = @DischargeType_ID
  AND NOT EXISTS (SELECT 1 FROM dbo.PatientDocument d
                  WHERE d.Patient_ID = @Patient_ID
                    AND ISNULL(d.PatientDocumentType_ID, '') = s.PatientDocumentType_ID);
```

**It returns what is MISSING, not what is required, so an EMPTY RESULT IS THE PASS CONDITION.** Read it the
other way round — treating the rows as the requirement list — and every discharge goes through. The
procedure's own header comment says so: *"If result set is empty => all good; no missing docs."*

What it enforces, precisely:

- **The requirement is data, not code.** One `dbo.PatientDocumentSettings` row per (discharge reason,
  document type) pair, where the row's **existence** is the rule — the same shape
  `dbo.StaffDocumentSettings` uses for staff (§3.6). **A freshly published `CRC_DB` has none, so until
  somebody uses the Settings screen NOTHING is mandatory and every discharge passes.**
- **Existence, not count.** One document of each required type is enough and three are no better, exactly
  like the staff rule (§4.4).
- **It never checks that the patient exists.** `@Patient_ID` appears only inside the `NOT EXISTS`, so an
  unknown patient trivially has no documents and every configured type comes back as missing. Verified by
  accident during the smoke test — an id that had never existed produced the full missing-documents
  message rather than an error. Harmless here, because `SaveBasic` only reaches this call on the update
  path, but it is not the check it looks like.
- **The join is exact and case-sensitive to the database's collation** — `ISNULL(d.PatientDocumentType_ID,
  '') = s.PatientDocumentType_ID`, with no `UPPER`/`LTRIM` normalisation on either side. Contrast the two
  staff-document reads, which normalise both sides of both joins (§5.4). Neither column is a foreign key,
  so a document type stored with different padding would silently fail to satisfy its own requirement.

#### The other findings, from reading all seven

- **`spPatientBasic_Insert` validates nothing at all** — no `RAISERROR` anywhere in the body. That makes it
  the only one of nucentra's three composed-id inserts with no guard: `spBranch_Insert` refuses a blank
  organization, a blank state and a state not in `LU_LOCATION`; `spStaff_Insert` refuses a blank staff type
  because it is the id's prefix. Here the prefix is the constant `'PAT-'`, so there is nothing to refuse,
  and every rule lives in the controller (§3.8).
- **The insert takes no discharge parameters and hard-codes `NULL, NULL, NULL` into those three columns.**
  A new patient is by definition active. Sending them would fail with *"has no parameter named …"* — which
  is why `CreatePatientAsync` and `UpdatePatientAsync` send different parameter sets from one
  `PatientSaveInput`.
- **The update writes all three discharge columns unconditionally**, so they must be sent on every call.
  They all default to `NULL`, so omitting them would not throw — it would silently un-discharge every
  patient the method touched. This is the same class of failure as a dropped `@User_ID`: no error, no page
  break, wrong data.
- **`spPatientBasic_Update` audits only when a row actually changed** (`IF @RowsAffected > 0`) and returns
  nothing either way, so an update against an unknown id succeeds silently and reports success — the same
  asymmetry `spBranch_Update` (§5.2) and `spStaff_Update` (§5.4) have. Three procedures, one habit.
- **Both write procedures `NULLIF(LTRIM(RTRIM(ISNULL(@Patient_AddLine2, ''))), '')`**, so
  `Patient_AddLine2` can never hold the empty string whatever the caller sends. The controller *also* sends
  `null` rather than `""` for a blank one. That belt-and-braces is what made the Prompt 5 null-handling
  assertion easy to state and worth stating: a patient created with only the mandatory fields, through the
  migrated code, was diffed **at row level** against one created the old way, and `Patient_AddLine2`, all
  three iFOBT columns and all three discharge columns were `NULL` in both — no `NULL` became `""` and no
  `""` became `NULL`. A JSON diff alone would not have caught it, because `GetBasic` coerces
  `addLine2` to `""` either way.
- **The two list procedures' column sets differ by exactly one column**, and `spPatientBasic_ListActive`'s
  five are a strict subset of `spPatientBasic_ListDischarged`'s six. They therefore get **two models**,
  `PatientListItem` and `PatientDischargedItem`, and not one — the same call §5.3 makes for
  `spUsers_GetById` versus `spUsers_ValidateLogin`, and the opposite of the call §5.4 makes for
  `spStaffDocument_List` versus `spStaffDocument_GetById`, which genuinely select the same nine columns.
  **Reuse the shape, never the name.**
- **Both list procedures select the three iFOBT columns and neither endpoint projects them.** They are
  modelled anyway, because a model that quietly drops columns stops being a description of the result set —
  and because whichever prompt first wants an iFOBT column on a patient list will look here.
- **`spPatientBasic_GetById` has no `SELECT TOP 1`**, unlike `spBranch_GetById` and `spStaff_GetById`. The
  primary key guarantees at most one row on its own, which is why `QuerySingleOrDefaultAsync` is right; if
  that ever stopped being true it would throw, which is the behaviour worth having.
- **All six of its lookup joins are `LEFT JOIN`s onto tables nothing constrains the codes to**, so any of
  the six `_Name` columns can be NULL while its `_ID` is set. Only `DischargeType_Name` reaches the browser
  today; the other five exist on `PatientBasicDetail` because the procedure returns them, and Prompt 7
  reuses this same procedure for `StaffPatientController.GetBasic`.

### 5.7 Patient appointments — `Stored Procedures/{PatientAppointment,StaffSlots}/` (12)

**Four of the twelve declare `@User_ID INT = NULL` — the ACTOR** (§0.1): `spPatientAppointment_Insert`,
`_Update`, `_Delete` and `_UpdateStatus`. All four write a `dbo.AuditTrails` row with `ISNULL(@User_ID, 0)`,
which is the silent-failure surface: drop the parameter and the write still succeeds, naming nobody. **The
other eight declare no `@User_ID` and write no audit row** — including both slot procedures, which was
checked in the `.sql` rather than inferred from the fact that they mutate a table.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spPatientAppointment_ListByPatient` | `@Patient_ID VARCHAR(100)` | 12 columns, 3 `LEFT JOIN`ed names, ordered date DESC, start DESC, id DESC | `GetAppointmentsByPatientAsync` → `List<PatientAppointmentItem>` | no |
| `spPatientAppointment_Search` | 7, **all `= NULL`** — `@PatientName`, `@StaffName`, `@Status`, `@FromDate DATE`, `@ToDate DATE`, `@PjAppTypeName`, `@BranchName` | 10 columns; ordered date DESC, start **ASC**, id DESC | `SearchAppointmentsAsync` → `List<AppointmentSearchItem>` | no |
| `spPatientAppointment_LookupBranches` | — | `Branch_Name`, `DISTINCT`, `INNER JOIN`, **`Branch_Status = 1`** | `GetAppointmentBranchNamesAsync` → `List<string>` | no |
| `spPatientAppointment_LookupPatientNames` | — | `Patient_Name`, `DISTINCT`, `INNER JOIN` | `GetAppointmentPatientNamesAsync` → `List<string>` | no |
| `spPatientAppointment_LookupStaffNames` | — | `Staff_Name`, `DISTINCT`, `INNER JOIN` | `GetAppointmentStaffNamesAsync` → `List<string>` | no |
| `spPatientAppointment_LookupStatuses` | — | `PatientAppointment_Status`, `DISTINCT`, **no join** | `GetAppointmentStatusesAsync` → `List<string>` | no |
| `spPatientAppointment_Insert` | the 8 business columns, plus **`@NewPatientAppointment_ID INT OUTPUT`** and `@User_ID` | 🔴 **nothing — no result set.** The id is the OUTPUT parameter | `SaveAppointmentAsync` **only** | **`INT = NULL` — ACTOR** |
| `spPatientAppointment_Update` | `@PatientAppointment_ID` + 7 business columns + `@User_ID`, plus **8 `@Out_* OUTPUT`** | nothing; the answer is the 8 OUTPUT parameters | `SaveAppointmentAsync` **only** | **`INT = NULL` — ACTOR** |
| `spPatientAppointment_Delete` | `@PatientAppointment_ID`, `@User_ID` | nothing | `DeleteAppointmentAsync` | **`INT = NULL` — ACTOR** |
| `spPatientAppointment_UpdateStatus` | `@PatientAppointment_ID`, `@PatientAppointment_Status`, `@User_ID`, plus the **same 8 `@Out_* OUTPUT`** | nothing; the 8 OUTPUT parameters | `UpdateAppointmentStatusAsync` → `AppointmentStatusResult` | **`INT = NULL` — ACTOR** |
| `spStaffSlots_AssignAppointment` | `@ApptId INT`, `@StaffSlotIds VARCHAR(MAX)` | nothing | `SaveAppointmentAsync` **only** | no |
| `spStaffSlots_ClearAppointment` | `@ApptId INT` | nothing | `SaveAppointmentAsync` **only** | no |

**Four of the twelve have no method of their own** — `_Insert`, `_Update` and the two slot procedures are
reachable only through `SaveAppointmentAsync`. That is the point of the transaction, not an oversight
(§5.5, §6.7).

#### 🔴 THREE OF THE FOUR WRITES ANSWER THROUGH OUTPUT PARAMETERS

This area holds **three of nucentra's seven `OUTPUT`-parameter procedures** — more than any other — and
**seventeen of the twenty-three `OUTPUT` parameters in the product**. The complete seven-row list is in
§5.8; all three here are read with `DynamicParameters`.

**`spPatientAppointment_Insert` sets the same trap `spPatientBasic_Insert` does.** It ends by assigning
`@NewPatientAppointment_ID = CONVERT(INT, SCOPE_IDENTITY())` and **there is no trailing `SELECT`** — so
`QuerySingleAsync<int>` would throw *"Sequence contains no elements"* on every successful insert. The
family resemblance to `spBranch_Insert` and `spStaff_Insert`, which both *do* end with a `SELECT`, is the
trap. Read the `.sql`.

**The eight `@Out_*` parameters are the interesting ones, and they exist for the audit trail.** Both
`_Update` and `_UpdateStatus` finish by selecting the saved row back into
`@Out_Patient_ID`, `@Out_Staff_ID`, `@Out_Date`, `@Out_StartTime`, `@Out_EndTime`, `@Out_PjAppType_ID`,
`@Out_Branch_ID` and `@Out_Status`, under a comment that says exactly why:

```sql
-- Re-read persisted values so callers can audit DB state, not request payload
```

That is a genuinely good idea and it is worth naming: **an audit line built from the request can be wrong
in precisely the case somebody is reading it to find out what happened.** `_UpdateStatus` is the one that
shows the value — it is handed only an id and a status, so without the re-read its audit line could not name
the patient, the clinician, the branch or the times at all.

🔴 **The two differ in whether the OUTPUT parameters can come back NULL, and the difference is load-bearing.**

| | `_Update` | `_UpdateStatus` |
|---|---|---|
| unknown id | **silent** — `IF @RowsAffected > 0` skips the re-read, the audit row **and** the OUTPUTs | **`RAISERROR('Appointment not found.', 16, 1)` + `RETURN`** |
| so the OUTPUTs are | **NULL**, and the caller must have a fallback | never read — the `SqlException` arrives first |
| hence the model | `AppointmentSaveResult` **seeds** all eight from the request and overwrites only what came back | `AppointmentStatusResult` has eight non-nullable properties and no seed |

The seeding is not defensive decoration: it reproduces the pre-Dapper controller exactly, which initialised
eight locals from the request and overwrote each one only `if (outX.Value is string …)`.

#### 🔴 `spPatientAppointment_Delete` releases the slots itself, and it has to

```sql
-- Release any booked slots first (FK safety)
UPDATE dbo.StaffSlots SET PatientAppointment_ID = NULL WHERE PatientAppointment_ID = @PatientAppointment_ID;
DELETE FROM [dbo].[PatientAppointment] WHERE [PatientAppointment_ID] = @PatientAppointment_ID;
```

**Its comment says "FK safety" and that is literally true**: `FK_StaffSlots_PatientAppointment` (§3.7)
constrains the slot to an existing appointment, so the `DELETE` would fail outright while any slot still
pointed at the row. So **"delete the appointment" and "return its hours to the schedule" are one procedure,
not two**, and there is no separate release step for a caller to forget — or to get wrong.

It captures the appointment's details into local variables **before** deleting, for the same reason
`spStaffSlots_Delete` and `spStaff_Delete` do (§5.4, §5.5): the audit summary names a row that no longer
exists by the time the `INSERT` runs. Both statements run **bare, with no transaction** — so a failure
between them would leave the slots released and the appointment still present, which is the benign
direction to fail in.

The audit row is guarded by `IF @RowsAffected > 0` on the `DELETE`, so **a delete against an unknown id
writes nothing and returns normally** — the same silent success `spBranch_Delete`, `spBranch_Update`,
`spStaff_Update`, `spPatientBasic_Update` and `spPatient_DeleteCascade` all have. Six procedures, one habit.

#### The four validation rules, and the exact strings they produce

The rules live in `SqlData.SaveAppointmentAsync`, against the slots read inside its own transaction. The
data layer returns an `AppointmentSaveFailure`; `PatientController.SaveAppointment` owns every sentence
(§4.8.2, §6.7).

| # | Rule | `AppointmentSaveFailure` | The exact message |
|---|---|---|---|
| 1 | every requested `StaffSlot_ID` came back from the in-transaction read | `SlotNotFound` | `One or more selected slots are invalid. Please reload the slots and try again.` |
| 2 | every slot belongs to the selected staff member | `SlotWrongStaff` | `Selected slots do not match the selected staff.` |
| 3 | every slot is on the selected date | `SlotWrongDate` | `Selected slots do not match the selected appointment date.` |
| 4 | no slot is already booked by a **different** appointment | `SlotTaken` | `One or more selected slots are no longer available. Please reload the slots and try again.` |
| 5 | the hours are contiguous | `SlotsNotConsecutive` | `Please select consecutive slots (e.g. 08:00-09:00 then 09:00-10:00).` |
| 6 | the insert produced a usable identity | `InsertFailed` | `Failed to create appointment.` |

🔴 **Rules 2 and 3 cannot fire, and could not before the migration either.** The read is
`spStaffSlots_List` narrowed to `@Staff_ID` and to `@FromDate = @ToDate =` the appointment's date, so
another clinician's slot and another day's slot are **not in the result at all** and are caught by rule 1
as missing ids. Measured against the running site, before and after: a non-existent id, another staff
member's slot and another date's slot all produce rule 1's message. Rule 3 is still written out in code,
because `spStaffSlots_List` projects `SlotDate` and so there is a real per-row value to assert; **rule 2 is
not**, because the procedure does not project `Staff_ID` and the pre-Dapper check compared a field it had
just populated from the request against that same request value. Both enum values and both strings are
kept, so the reason exists the day the read stops being narrowed.

Rule 4 is the one the transaction exists for, and its exception is what makes editing work: a slot carrying
**this** appointment's own id passes. On an insert the id is `0`, which no `IDENTITY` can be, so every
taken slot fails.

#### The other findings, from reading all twelve

- 🔴 **`spStaffSlots_AssignAppointment` SILENTLY IGNORES AN ID THAT MATCHES NO ROW.** It splits
  `@StaffSlotIds` with `STRING_SPLIT` + `TRY_CAST` (discarding anything non-numeric) and `UPDATE`s through
  an **`INNER JOIN`** against the result — so a slot id that does not exist updates nothing, reports
  nothing and returns success. That is only safe because rule 1 above has already proved every requested id
  was in the in-transaction read; it is the strongest argument against ever publishing this procedure as a
  standalone data-layer method. Its one guard is `THROW 50001, 'At least one staff slot ID is required.'`
  on a blank list, which the controller's "select at least one slot" check makes unreachable.
- **It takes the ids as ONE comma-separated `VARCHAR(MAX)`**, not a table-valued parameter, which is why
  `SqlData` passes `string.Join(",", …)`. `STRING_SPLIT` requires compatibility level 130+; nothing in the
  build checks that.
- **Neither slot procedure is idempotent in the same way `spStaffSlots_CreateRange` is.** Assign is a blind
  `UPDATE`: running it twice with the same arguments is harmless, but running it with a *different*
  appointment id simply overwrites, with no check that the slot was free. The freeness check is entirely
  the caller's, which is the whole design (§6.7).
- **`spStaffSlots_ClearAppointment` is keyed on the APPOINTMENT, not on a slot list** —
  `WHERE PatientAppointment_ID = @ApptId`. That is deliberate: `dbo.PatientAppointment` does not record
  which slots it consumed (§3.9), so the appointment id is the only reliable way to find them, and a caller
  passing a list could miss one.
- **`spPatientAppointment_Search`'s date range is INCLUSIVE at both ends, and it gets there asymmetrically**
  — `>= @FromDate` but `< DATEADD(DAY, 1, @ToDate)`. The second form is the correct one for a column that
  might carry a time; `PatientAppointment_Date` is a `DATE`, so today both work, and the asymmetry is
  future-proofing rather than a bug.
- 🔴 **`spPatientAppointment_Search` RENAMES A COMPOSED VALUE OVER THE TOP OF A REAL COLUMN.** Its last
  selected expression is
  `DATEADD(SECOND, DATEDIFF(SECOND, 0, pa.PatientAppointment_StartTime), CAST(pa.PatientAppointment_Date AS DATETIME)) AS [PatientAppointment_Date]`
  — the date **with the start time folded in**, under the date column's own name, with a comment saying
  *"Keep legacy column name used by existing controllers/JS (start datetime)"*. So `PatientAppointment_Date`
  is a `DATE` everywhere in nucentra and a `DATETIME` in this one result set, which is what lets both
  consuming endpoints render `"01/09/2026 08:00"` from a single field. A model that assumed the column type
  would be wrong; `AppointmentSearchItem` says so at the property.
- **Its appointment-type filter matches THREE ways** — the lookup's name, the lookup's id, or the raw
  `PjAppType_ID` stored on the appointment — all `LTRIM`/`RTRIM`ed. Its own comment explains the third: it
  keeps the search working for legacy rows that stored a *name* where the schema expects a code. The
  `PjAppType_Name` column is likewise `COALESCE(t.PjAppType_Name, pa.PjAppType_ID)`, so such a row still
  displays something.
- **The four `_Lookup*` procedures read `dbo.PatientAppointment`, not a lookup table**, which is why
  §4.8.3 calls them filters rather than lookups. Three `INNER JOIN` their parent table, so a booking whose
  `Branch_ID`, `Patient_ID` or `Staff_ID` no longer resolves contributes nothing — and since none of those
  is a foreign key, that is a state the schema permits. `_LookupBranches` is the only one with a second
  predicate, `Branch_Status = 1`: **a booking into a branch that has since been deactivated drops out of
  the filter while the booking itself remains**, so such an appointment is findable by every filter except
  its own branch.
- **`_LookupStatuses` reports what is STORED, not what is VALID.** There is no status lookup table
  (§3.9), so a database with no `"Not Attended"` appointment offers two entries, and a value written by
  hand would appear beside the three real ones.
- **`spPatientAppointment_ListByPatient` returns the two times as `VARCHAR(5)`**, exactly like
  `spStaffSlots_List` (§5.5) — `CONVERT(VARCHAR(5), …, 108)`, so `"08:00"` reaches C# as a string even
  though the column is `TIME(0)`. `PatientAppointmentItem` keeps them as strings and the endpoint
  serializes them verbatim; parsing them into a `TimeSpan` would only mean formatting them back.
- **`_Insert` validates nothing** — no `RAISERROR` anywhere in the body. It does not check that the patient,
  the staff member, the branch or the appointment type exists, and none of those is a foreign key, so an
  appointment can be booked against four ids that resolve to nothing. Every rule that exists is
  `PatientController`'s and `SaveAppointmentAsync`'s.
- **`_Update` does not re-check anything either**, and in particular it does not verify that the new times
  match the slots being assigned — the two are kept in step only because one method computes both (§6.7).

### 5.8 The patient journey and patient documents — `Stored Procedures/{PatientJourney,PatientAssessment,PatientColonoscopy,PatientFollowUp,PatientDocument}/` (17)

🔴 **THE `@User_ID` PICTURE HERE IS NOT UNIFORM, AND A GREP ACTIVELY MISLEADS.** Every parameter list was
read individually rather than pattern-matched from the other areas. The answer:

```
TWELVE journey procedures   — NOT ONE declares @User_ID, of either kind.
                              NOT ONE writes a dbo.AuditTrails row.
                              The six writes take @Staff_ID instead — a DIFFERENT identity (below).

TWO document writes         — spPatientDocument_Insert and spPatientDocument_Delete both declare
                              `@User_ID INT = NULL`: THE ACTOR. SqlData supplies it from the claim.

THREE document reads        — spPatientDocument_List, _GetById and _LookupDocuments declare none.
                              🔴 _GetById's HEADER COMMENT contains the words "no @User_ID and no audit
                              row", so `grep "@User_ID"` MATCHES THE FILE. Its parameter list is
                              `@PatientDocument_ID INT` and nothing else.
```

🔴 **`@Staff_ID` IS NOT AN AUDIT ACTOR.** The six `…WithJourney` procedures take a `dbo.Staff.Staff_ID` —
the clinician the journey belongs to — as an ordinary business argument, from the controller's `StaffId`
claim. It lands in `PatientJourney.Staff_ID`, `PatientJourney.CreatedBy_Staff_ID` /
`UpdatedBy_Staff_ID`, and `PatientJourneyAudit.Staff_ID`. **Filling it from `DatabaseHelper.CurrentUserId`
would put a `dbo.Users` id into a `dbo.Staff` column**, and five of the six procedures would then
`RAISERROR 'Staff not found.'` — which is the good outcome; `spPatientColonoscopy_CreateWithJourney` would
too, since it validates the staff member even though it does not validate the patient. It appears in every
`IDatabaseData` signature, exactly as a business value should.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spPatientJourney_GetById` | `@PatientJourney_ID INT` | `SELECT TOP 1` — 10 columns, `INNER JOIN PatientBasic` for the name; **empty set** for an unknown id **or a deleted patient** | `GetJourneyByIdAsync` → `PatientJourneyDetail?` | no |
| `spPatientJourney_TimelineByPatient` | `@Patient_ID VARCHAR(100)` | 11 columns; ordered **`PatientJourney_Date ASC, PatientJourney_ID ASC`**. Two `OUTER APPLY`s — see below | `GetJourneyTimelineAsync` → `List<PatientJourneyTimelineItem>` | no |
| `spPatientJourney_AuditsByPatient` | `@Patient_ID VARCHAR(100)` | 7 columns; ordered `PatientJourney_ID ASC, Audit_At ASC, PatientJourneyAudit_ID ASC` | `GetJourneyAuditsAsync` → `List<PatientJourneyAuditItem>` | no |
| `spPatientAssessment_GetByJourneyId` | `@PatientJourney_ID INT` | `SELECT TOP 1` — **51 columns**, `INNER JOIN` both `PatientAssessment` and `PatientBasic`; empty set when the journey has no assessment | `GetAssessmentByJourneyIdAsync` → `IReadOnlyDictionary<string, object?>?` | no |
| `spPatientAssessment_CreateWithJourney` | 50 — `@Patient_ID`, `@PatientJourney_Date`, `@Staff_ID`, `@Audit_Note`, + 46 clinical | `SELECT @PatientJourney_ID AS PatientJourney_ID` — **one row** | `CreateAssessmentWithJourneyAsync` → `int` | no |
| `spPatientAssessment_UpdateWithJourney` | the same 49, with `@PatientJourney_ID` in place of `@Patient_ID` | `SELECT 1 AS Success` — **read by nothing** | `UpdateAssessmentWithJourneyAsync` | no |
| `spPatientColonoscopy_GetByJourneyId` | `@PatientJourney_ID INT` | `SELECT TOP 1` — **37 columns** | `GetColonoscopyByJourneyIdAsync` → `IReadOnlyDictionary<string, object?>?` | no |
| `spPatientColonoscopy_CreateWithJourney` | 35 | `SELECT @PatientJourney_ID AS PatientJourney_ID` | `CreateColonoscopyWithJourneyAsync` → `int` | no |
| `spPatientColonoscopy_UpdateWithJourney` | the same 34, keyed on `@PatientJourney_ID` | 🔴 **nothing — no trailing `SELECT` at all** | `UpdateColonoscopyWithJourneyAsync` | no |
| `spPatientFollowUp_GetByJourneyId` | `@PatientJourney_ID INT` | `SELECT TOP 1` — 10 columns | `GetFollowUpByJourneyIdAsync` → `IReadOnlyDictionary<string, object?>?` | no |
| `spPatientFollowUp_CreateWithJourney` | 7 | `SELECT @PatientJourney_ID AS PatientJourney_ID` | `CreateFollowUpWithJourneyAsync` → `int` | no |
| `spPatientFollowUp_UpdateWithJourney` | the same 6, keyed on `@PatientJourney_ID` | `SELECT 1 AS Success` — read by nothing | `UpdateFollowUpWithJourneyAsync` | no |
| `spPatientDocument_List` | `@Patient_ID VARCHAR(100)` — **required** | 9 columns incl. `BlobName`, ordered `UploadedOn DESC, PatientDocument_ID DESC` | `GetPatientDocumentsAsync` → `List<PatientDocumentItem>` | no |
| `spPatientDocument_GetById` | `@PatientDocument_ID INT` | `SELECT TOP 1` — the **same 9 columns**; empty set for an unknown id | `GetPatientDocumentByIdAsync` → `PatientDocumentItem?` | **no — see the box above** |
| `spPatientDocument_Insert` | `@Patient_ID`, `@Patient_Name`, `@PatientDocumentType_ID`, `@PatientDocumentType_Name`, `@FileName`, `@BlobName`, `@ContentType`, `@User_ID` | nothing — **not even the new identity** | `AddPatientDocumentAsync` | **`INT = NULL` — ACTOR** |
| `spPatientDocument_Delete` | `@PatientDocument_ID`, `@User_ID`, **`@DeletedBlobName VARCHAR(500) = NULL OUTPUT`** | nothing; the answer is the OUTPUT parameter | `DeletePatientDocumentAsync` → `string?` | **`INT = NULL` — ACTOR** |
| `spPatientDocument_LookupDocuments` | — | `PatientDocumentType_ID, PatientDocumentType_Name` — the **union** of types in use and types in the lookup | `GetPatientDocumentTypeFiltersAsync` → `List<LookupItem>` | no |

`spPatientDocument_PatientNames` and `spDocuments_Search` live in the same folder but belong to the
Documents search page and are documented in §5.9.

#### 🔴 THE SIX `…WithJourney` PROCEDURES — WHAT EACH WRITES, IN WHAT ORDER, AND WHERE ATOMICITY COMES FROM

**Each is a SINGLE procedure that writes THREE tables, and each holds its own transaction.** Read as
`BEGIN`, not inferred from the name — all six open the same way:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRAN;
        …
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH
```

**The create path, in order — and the order is forced by a real foreign key:**

```
1  INSERT dbo.PatientJourney       (Patient_ID, PjAppType_Name = a LITERAL, PatientJourney_Date,
                                    Staff_ID, CreatedBy_Staff_ID)
   @PatientJourney_ID = CAST(SCOPE_IDENTITY() AS INT)
2  INSERT dbo.{PatientAssessment | PatientColonoscopy | PatientFollowUp}   (PatientJourney_ID, Patient_ID, …)
3  INSERT dbo.PatientJourneyAudit  (PatientJourney_ID, 'CREATED', Staff_ID, Audit_Note)
   COMMIT
4  SELECT @PatientJourney_ID AS PatientJourney_ID
```

Step 1 **must** come first: `FK_PatientAssessment_PatientJourney` and its two siblings are enforced, so the
detail row cannot exist before the journey row — and the journey's id is only known from `SCOPE_IDENTITY()`
afterwards. Step 2 also copies `Patient_ID` onto the detail row, denormalized off step 1's argument.

**The update path, in order:**

```
1  IF NOT EXISTS (dbo.PatientJourney) → RAISERROR 'Journey not found.'
2  staff lookup                       → RAISERROR 'Staff not found.'  (all six do this)
3  UPDATE dbo.PatientJourney          SET PatientJourney_Date, Updated_At = SYSUTCDATETIME(),
                                          UpdatedBy_Staff_ID
4  UPDATE dbo.{detail table}          SET every clinical column
   IF @@ROWCOUNT = 0                  → RAISERROR '{Assessment|Colonoscopy|Follow up} row not found for this journey.'
5  INSERT dbo.PatientJourneyAudit     (PatientJourney_ID, 'UPDATED', Staff_ID, Audit_Note)
   COMMIT
```

🔴 **STEP 3 IS AN `UPDATE`, NOT AN `INSERT`, AND THAT IS THE WHOLE REASON THE CREATE AND THE UPDATE ARE TWO
PROCEDURES.** An update that inserted a journey row would show the same assessment twice on the timeline,
in the right order, with the right dates and no error anywhere. Asserted during Prompt 7's smoke test: the
journey-row count for the patient was `1` before and `1` after the assessment update, `2 → 2` for the
colonoscopy, `3 → 3` for the follow-up, and each detail table held exactly one row per journey throughout.

**ATOMICITY COMES FROM THE PROCEDURE, NOT FROM C#.** `SqlData` gives each of the six exactly one ordinary
Dapper call — no `SqlConnection` opened by hand, no `SqlTransaction`, no `transaction:` argument. Nucentra
still has exactly **two** transactional units of work in the data layer, `SaveStaffWithDocumentsAsync` and
`SaveAppointmentAsync` (§6.6), and this area adds none. Wrapping one of these in a `SqlTransaction` would
nest a transaction inside a procedure that already has one, and — worse — would advertise in
`IDatabaseData` an atomicity guarantee the data layer is not the source of.

**Four asymmetries between the six, all real and none obviously intended:**

| | assessment | colonoscopy | follow-up |
|---|---|---|---|
| create validates the **patient** exists | ✅ `RAISERROR 'Patient not found.'` | 🔴 **no — only refuses a BLANK id** | ✅ |
| create validates the **staff member** | ✅ | ✅ | ✅ |
| update's trailing `SELECT` | `SELECT 1 AS Success` | 🔴 **none** | `SELECT 1 AS Success` |
| `PjAppType_Name` literal matches `LU_PJ_APP_TYPE` | ✅ | ✅ | 🔴 **no** (§3.10) |

Since `Patient_ID` is not a foreign key anywhere, **a colonoscopy can be recorded against a patient that
does not exist**. Nothing in the data layer or the controller compensates; the check belongs in the
procedure and adding it would be a `.sql` change.

🔴 **THE THREE CREATES END WITH A REAL `SELECT`, NOT AN OUTPUT PARAMETER, AND THAT IS THE OPPOSITE OF THE
OTHER TWO COMPOSED-ID INSERTS IN NUCENTRA.** `spPatientBasic_Insert` (§5.6) and
`spPatientAppointment_Insert` (§5.7) both answer through `OUTPUT` parameters with no trailing `SELECT`, and
both set a trap for anyone reaching for `QuerySingleAsync`. Here the trap runs the other way: these three
*are* `QuerySingleAsync<int>`. **Read the `.sql`; the family resemblance is worthless in both directions.**

#### 🔴 THE COMPLETE LIST — the SEVEN procedures with `OUTPUT` parameters

This is the authoritative list for the whole product, verified against every parameter list under
`CRC.Database/Stored Procedures/`: **seven procedures, twenty-three `OUTPUT` parameters.** Six of the seven are
read with `DynamicParameters` — the one place per method where the data layer works through a string-keyed
bag rather than a typed model, because **an `OUTPUT` parameter is the single thing Dapper cannot reach
through an anonymous object**. The seventh is the one exception, and it is the one `.sql` this migration
edited (§5.3).

| Procedure | `OUTPUT` params | Carries | How `SqlData` reads it |
|---|---|---|---|
| `spUsers_RegisterFailedLogin` | 3 | the lockout decision | a trailing `SELECT` appended additively → `QuerySingleOrDefaultAsync<FailedLoginResult>` |
| `spStaffDocument_Delete` | 1 | the deleted row's blob key | `DynamicParameters`, `DbType.AnsiString`, size 500 |
| `spPatientDocument_Delete` | 1 | the deleted row's blob key | `DynamicParameters`, `DbType.AnsiString`, size 500 |
| `spPatientBasic_Insert` | 1 | the new `Patient_ID` | `DynamicParameters`, `DbType.AnsiString`, size 100 |
| `spPatientAppointment_Insert` | 1 | the new `PatientAppointment_ID` | `DynamicParameters` |
| `spPatientAppointment_Update` | 8 | the re-read row, for the audit line | `DynamicParameters` |
| `spPatientAppointment_UpdateStatus` | 8 | the re-read row, for the audit line | `DynamicParameters` |

**Nothing else in the catalogue declares one.** If you add an eighth, add it here — and prefer a trailing
`SELECT` if the procedure has no callers left on the parameter, because a result set maps onto a model by
name and a `DynamicParameters` key does not.

#### 🔴 `spPatientJourney_TimelineByPatient` — the five audit columns are not on the journey row

The procedure is one `SELECT` over `dbo.PatientJourney` with **two `OUTER APPLY`s** into
`dbo.PatientJourneyAudit`, each `LEFT JOIN`ing `dbo.Staff` for a name:

```sql
OUTER APPLY (SELECT TOP 1 …  WHERE a.Audit_Action = 'CREATED'             ORDER BY a.Audit_At ASC ) ca
OUTER APPLY (SELECT TOP 1 …  WHERE a.Audit_Action IN ('UPDATED','EDITED') ORDER BY a.Audit_At DESC) ua
```

— the **earliest** creation event and the **latest** change event. Four things follow:

- **`OUTER APPLY`, not `CROSS APPLY`**, so a journey with no audit rows still appears on the timeline with
  all five columns NULL. That is why `PatientJourneyTimelineItem` types every one of them nullable.
- **The aliases are `CreatedAt` / `CreatedByStaffId` / `CreatedByStaffName` and the `Updated*` trio, not
  `Audit_At` / `Staff_ID` / `Staff_Name`.** Dapper maps by name, so a model naming the audit table's
  columns would stay null on every row with nothing in a log.
- 🔴 **`'EDITED'` IS ACCEPTED BY THE READ AND WRITTEN BY NOTHING.** All six `…WithJourney` procedures write
  `'CREATED'` or `'UPDATED'` and nothing else; measured across a whole database after the full flow,
  `SELECT Audit_Action, COUNT(*) FROM dbo.PatientJourneyAudit GROUP BY Audit_Action` returns exactly two
  rows, `CREATED` and `UPDATED`. It is a vocabulary the read tolerates. Do not read its presence as
  evidence of a third action.
- **`Created_At` / `Updated_At` on `dbo.PatientJourney` itself are a SECOND, redundant record of the same
  two facts** — and the timeline read ignores them entirely, preferring the audit table. The row's own
  columns keep only the first and the latest; the audit table keeps every event. They can disagree only if
  somebody writes one without the other, which no procedure does.

#### The other findings, from reading all seventeen

- **All three detail reads `INNER JOIN` their detail table AND `dbo.PatientBasic`**, so an empty result
  means *either* "this journey has no detail row of that type" *or* "this journey's patient has been
  deleted". The endpoint cannot tell those apart and reports `assessment: null` for both. Given §7's
  orphaning bug, the second case is reachable.
- **`spPatientJourney_GetById` `INNER JOIN`s `dbo.PatientBasic` too**, so a journey orphaned by a partial
  cascade returns **nothing** — and `/StaffPatient/GetPatientAssessment` answers *"Journey not found."*
  about a row that plainly exists. That is the user-visible face of §7's orphaning.
- **`spPatientDocument_List` and `spPatientDocument_GetById` are the same nine-column `SELECT` with
  different `WHERE` clauses**, which is why they correctly share `PatientDocumentItem` — the same call §5.4
  makes for the staff pair, and the opposite of §5.3's `spUsers_GetById` versus `spUsers_ValidateLogin`.
  **Reuse the shape, never the name.**
- **Both document reads `COALESCE` the type name back to the raw id** —
  `COALESCE(NULLIF(LTRIM(RTRIM(t.PatientDocumentType_Name)), ''), pd.PatientDocumentType_ID)` — and join on
  `UPPER(LTRIM(RTRIM(ISNULL(…, ''))))` on both sides, exactly like the staff document reads (§5.4). Both
  joins are therefore non-sargable. The coalesce can still yield NULL, because
  `PatientDocument.PatientDocumentType_ID` is itself nullable.
- 🔴 **`spPatientDocument_List`'s `@Patient_ID` is REQUIRED and `spStaffDocument_List`'s is OPTIONAL.** The
  staff one defaults to `NULL` and returns every document in the system when omitted (§5.4); this one has
  no such mode, which is why the Documents page (§4.11) uses `spDocuments_Search` rather than calling this
  with a blank id.
- **`spPatientDocument_Insert` writes `UploadedOn` as a CONVERTed string** because the column is a
  `VARCHAR(100)` (§3.15), and it audits with the **client-posted** `@PatientDocumentType_Name` while
  `spPatientDocument_Delete` audits with the name it **re-joined** from `LU_PATDOCUMENTTYPE`. Measured: one
  document's INSERT line says `DocType=CONSENT FORM (05)` and its DELETE line says
  `DocType=HISTORY AND EXAMINATION FORM (05)`. The delete is the truthful one.
- **`spPatientDocument_Delete` captures the row's details into local variables BEFORE the `DELETE`**, for
  the same reason `spStaff_Delete`, `spStaffSlots_Delete` and `spPatientAppointment_Delete` do: the audit
  summary names a row that no longer exists by the time the `INSERT` runs. Its `@DeletedBlobName` is set to
  the key only `WHEN @RowsAffected > 0`, so NULL genuinely means "nothing was deleted".
- **`spPatientDocument_LookupDocuments` unions the types IN USE with the types in the lookup**, and
  `COALESCE`s a missing name back to the id — because `PatientDocument.PatientDocumentType_ID` has no
  foreign key (§3.15), so a document uploaded under a type later removed from `LU_PATDOCUMENTTYPE` must
  still be **findable**. An upload form must **not** offer that type, which is why
  `/StaffPatient/GetPatientDocumentTypes` uses `spLU_PatientDocumentType_List` instead. Two procedures, two
  correct answers, one lookup table — exactly `spStaffDocument_LookupDocuments`'s arrangement (§5.4). Its
  one caller is the **Documents search page**, `/Documents/GetLookups` (§4.11), not the journey screen this
  sub-section otherwise describes.

### 5.9 Document settings and the Documents search — `Stored Procedures/{PatientDocumentSettings,StaffDocumentSettings,PatientDocument}/` (7)

**Not one of the seven declares `@User_ID`** — verified by reading all seven parameter lists, not by
grepping — and not one writes a `dbo.AuditTrails` row. Confirmed empirically: a full save-and-clear round
trip over both settings families produced **zero** new `AuditTrails` rows. Everything here is either a plain
read or a write that nothing records (§4.10).

`spStaffDocumentSettings_GetByStaffType` is the odd one out: its first caller is `StaffController`'s
mandatory-document check (§4.4, §5.4), and the Settings screen **reuses that one method rather than
duplicating it**. It is repeated in the table below only so the area reads as a whole.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spPatientDocumentSettings_GetByDischargeType` | `@DischargeType_ID VARCHAR(100)` | the 4 columns of `dbo.PatientDocumentSettings` for that reason — **only the configured rows** — ordered by `PatientDocumentType_Name` | `GetDischargeDocumentSettingsAsync` → `List<PatientDocumentSetting>` | no |
| `spPatientDocumentSettings_SaveForDischargeType` | `@DischargeType_ID VARCHAR(100)`, `@PatientDocumentType_IDs NVARCHAR(MAX)` — **a CSV** | nothing. Deletes and re-inserts the whole set in one batch | `SaveDischargeDocumentSettingsAsync` | no |
| `spStaffDocumentSettings_GetByStaffType` *(P3)* | `@StaffType_ID VARCHAR(100)` | **every** `LU_STAFFDOCUMENTTYPE` row plus a computed `IsMandatory INT` | `GetStaffDocumentSettingsAsync` → `List<StaffDocumentSetting>` | no |
| `spStaffDocumentSettings_DeleteByStaffType` | `@StaffType_ID VARCHAR(100)` | nothing. A bare `DELETE … WHERE StaffType_ID = @StaffType_ID` | `DeleteStaffDocumentSettingsAsync` | no |
| `spStaffDocumentSettings_Insert` | `@StaffType_ID`, `@StaffType_Name`, `@StaffDocumentType_ID`, `@StaffDocumentType_Name` | nothing. A bare `INSERT`, **one row per call** | `AddStaffDocumentSettingAsync` | no |
| `spDocuments_Search` | `@Mode VARCHAR(10)`, `@IndividualName VARCHAR(200) = NULL`, `@DocumentType VARCHAR(200) = NULL` | 7 columns — `DocumentId, Id, Name, DocumentType, FileName, BlobName, UploadedOn` — from **one of three branches** | `SearchDocumentsAsync` → `List<DocumentSearchItem>` | no |
| `spPatientDocument_PatientNames` | — | `Patient_Name`, `SELECT DISTINCT`, ordered | `GetPatientDocumentPatientNamesAsync` → `List<string>` | no |

#### 🔴 THE SAVE ASYMMETRY — one atomic procedure on the patient side, three round trips on the staff side

This is the thing to know about this area, and it is not visible from either half on its own.

| | Patient (discharge types) | Staff (staff types) |
|---|---|---|
| Procedures involved in a save | **1** | **2** |
| Round trips for a set of N types | 1 | **1 + N** |
| Who sequences the delete and the inserts | the procedure | `SettingsController.SaveStaffDocumentSettings` |
| Transaction | implicit — one statement batch | **none** |
| A failure part-way through | impossible to observe: the whole batch commits or none of it does | **leaves a partial set of mandatory documents**, and reports an error |
| Names on the stored row | resolved in SQL from `LU_DISCHARGETYPE` / `LU_PATDOCUMENTTYPE` | the client-posted staff type name, and a document type name the **controller** looked up |

**`spPatientDocumentSettings_SaveForDischargeType` is the safer of the two, and it is not close.** It
validates `@DischargeType_ID` against `LU_DISCHARGETYPE` and `RAISERROR`s severity 11 before touching
anything — the only server-side validation anywhere in this area — then `DELETE`s the discharge reason's
rows and re-inserts from `STRING_SPLIT(@PatientDocumentType_IDs, ',')` joined to `LU_PATDOCUMENTTYPE`. Both
statements run in one implicit transaction, so **the replace cannot be observed half-done and cannot be left
half-done**. Ids that match no lookup row are dropped silently by the `INNER JOIN` rather than failing the
save.

**The staff side has no equivalent procedure at all.** `SettingsController` calls
`spStaffDocumentSettings_DeleteByStaffType` once and then `spStaffDocumentSettings_Insert` once per selected
document type, each on its own connection, with **nothing wrapped around them**. If the process dies, the
database goes away, or an insert throws between the delete and the last insert, that staff type is left with
**some** of its mandatory documents — a state no user asked for and no error message describes, since the
controller's bare `catch` reports `"Error saving staff document settings."` and logs nothing (§4.10). The
window is small and the failure mode is real.

Two things keep it from misfiring in ordinary use, and both are load-bearing rather than incidental:
`spStaffDocumentSettings_Insert` is a bare `INSERT` against a composite primary key, so the controller's
`Distinct(StringComparer.OrdinalIgnoreCase)` is what stops a duplicated posted id from throwing **after the
delete has already run**; and the controller skips any id that is not in `spLU_STAFFDOCUMENTTYPE_List`
rather than letting the insert fail on it.

**The two staff procedures are two methods**, per the one-method-per-procedure rule (§12 decision 1), and the
sequencing stays in the controller where a reader can see it. Moving it into a `SqlData` unit of work — the
shape §6.6 uses for `SaveStaffWithDocumentsAsync` — would close the atomicity gap and would be a
**behaviour change**, which §12 decision 10 makes the owner's call rather than a passing tidy-up. It is written down
here so that whoever closes the gap does it deliberately.

#### `spDocuments_Search` — one procedure, three branches, and `@Mode` picks the table

`UPPER(LTRIM(RTRIM(ISNULL(@Mode, ''))))` selects between `dbo.PatientDocument` and `dbo.StaffDocument`, and
**anything else falls through to a third branch that returns the same seven columns with `WHERE 1 = 0`**. An
unrecognised mode is therefore **empty, not an error** — the controller never sends one, because it folds
everything that is not `"Staff"` into `"Patient"` first (§4.11).

- **The two branches alias their columns to the SAME seven names**, which is why one model,
  `DocumentSearchItem`, serves both — the opposite call to `PatientDocumentItem` / `StaffDocumentItem`,
  which keep their tables' native names and must stay two types.
- 🔴 **`UploadedOn` is a string in both branches for two different reasons.** The patient branch selects
  `dbo.PatientDocument.UploadedOn` as-is because that column is already a `VARCHAR(100)` holding
  `"2026-08-10 12:38:35 +08:00"` (§3.15); the staff branch `CONVERT`s a real `DATETIME` with style 120,
  producing `"2026-08-10 12:38:36"` — **no offset**. Measured on the running site, side by side. The two
  modes' strings are not the same format and the page prints whichever it is given. **Do not parse it.**
- **Both filters are EXACT, not `LIKE`** — equality after `UPPER(LTRIM(RTRIM(ISNULL(…, ''))))` on both
  sides. The page's controls are dropdowns of values the database already holds, so there is nothing to
  prefix-match. `@DocumentType` matches the type's **name OR its id OR the raw id on the document row**, in
  one `OR`, because the two families disagree about which one the dropdown carries.
- **Every comparison and every join wraps both sides in `UPPER(LTRIM(RTRIM(ISNULL(…))))`, so no index is
  usable.** Fine at this table size; the thing to notice before the tables grow.
- **The two branches order differently and both orderings are the page's contract.** Patient:
  `TRY_CONVERT(DATETIME, UploadedOn, 120) DESC, Patient_Name, PatientDocumentType_Name` — the `TRY_CONVERT`
  is there precisely because the column is text. Staff: the real `DATETIME` column `DESC`, then the two
  names.
- **Both branches `LEFT JOIN` the owner and the type lookup**, so a document whose `Patient_ID` /
  `Staff_ID` matches nothing still appears, with a null `Name`. The `DocumentType` column is a `COALESCE`
  back to the raw type id, and can still be null because the underlying column is nullable.

#### `spPatientDocument_PatientNames` — the filter, and what it quietly excludes

The exact twin of `spStaffDocument_StaffNames` (§5.4), down to the reasoning: `SELECT DISTINCT
pb.Patient_Name … INNER JOIN dbo.PatientBasic`, `WHERE ISNULL(pb.Patient_Name, '') <> ''`, ordered by name.

**It returns NAMES, NOT IDS**, because the filter control filters on the displayed name — so two patients
who share a name collapse into one entry and selecting it returns the documents of both. And the **`INNER
JOIN` is the exclusion**: a document whose `Patient_ID` no longer matches a patient row contributes nothing,
so its owner cannot be picked from the filter, even though the document still appears in an unfiltered
search (that read `LEFT JOIN`s). Given §7's orphaning bug, that case is reachable.

### 5.10 Dashboards, tracker and audit trails — `Stored Procedures/{Dashboard,StaffDashboard,PatientTracker,AuditTrails}/` (16)

**All sixteen are reads. Not one declares `@User_ID`** — verified by reading all sixteen parameter lists,
eleven of which are empty — and **not one writes a `dbo.AuditTrails` row**. This is the largest block of
procedures in the portal with no write in it, which is what makes it the last migration prompt.

| Procedure | Parameters | Returns | `IDatabaseData` method | `@User_ID` |
|---|---|---|---|---|
| `spDashboard_Branch_CountActive` | — | `ActiveBranchCount INT` — one row, one column | `GetActiveBranchCountAsync` → `int` | no |
| `spDashboard_Patient_ByRace` | — | `Race_Name, PatientCount` | `GetPatientsByRaceAsync` → `List<PatientsByRaceItem>` | no |
| `spDashboard_Patient_ByAgeGroup` | — | `AgeGroup, PatientCount` | `GetPatientsByAgeGroupAsync` → `List<PatientsByAgeGroupItem>` | no |
| `spDashboard_Patient_ByDischargeType` | — | `DischargeType_Name, PatientCount` | `GetPatientsByDischargeTypeAsync` → `List<PatientsByDischargeTypeItem>` | no |
| `spStaffDashboard_TodayAppointments` | `@Staff_ID VARCHAR(100)`, `@ForDate DATE` | 9 columns, one day | `GetStaffTodayAppointmentsAsync` → `List<StaffDashboardAppointmentItem>` | no |
| `spStaffDashboard_ThisWeekAppointments` | `@Staff_ID`, `@FromDate DATE` | the same 9, rolling 7 days | `GetStaffWeekAppointmentsAsync` | no |
| `spStaffDashboard_ThisMonthAppointments` | `@Staff_ID`, `@Year INT`, `@Month INT` | the same 9, one calendar month | `GetStaffMonthAppointmentsAsync` | no |
| `spPatientTracker_AppointmentTypes_List` | — | `PjAppType_ID, PjAppType_Name`, by id | `GetTrackerAppointmentTypesAsync` → `List<LookupItem>` | no |
| `spPatientTracker_Patients_List` | — | 7 `PatientBasic` columns + `IsStalled BIT`, by name then id | `GetTrackerPatientsAsync` → `List<PatientTrackerPatientItem>` | no |
| `spPatientTracker_Appointments_List` | — | `Patient_ID, PjAppType_ID, Status, Date` — **latest per pair**, unordered | `GetTrackerAppointmentsAsync` | no |
| `spPatientTracker_Procedures_List` | — | `Patient_ID, PjAppType_Name, PatientJourney_Date` — **all rows** | `GetTrackerProceduresAsync` | no |
| `spPatientTracker_StalledCount_Get` | — | `StalledCount INT` — one row, one column | `GetTrackerStalledCountAsync` → `int` | no |
| `spAuditTrails_Search` | `@UserId INT = NULL`, `@FromDate DATE = NULL`, `@ToDate DATE = NULL`, `@Action VARCHAR(20) = NULL`, `@Category VARCHAR(50) = NULL` | 6 columns, newest first | `SearchAuditTrailsAsync` → `List<AuditTrailSearchItem>` | **no — `@UserId` is a FILTER** |
| `spAuditTrails_LookupUsers` | — | `User_ID, User_Name`, `DISTINCT`, `INNER JOIN dbo.Users` | `GetAuditTrailUsersAsync` → `List<LookupItem>` | no |
| `spAuditTrails_LookupActions` | — | `AuditTrail_Action`, `DISTINCT`, one column | `GetAuditTrailActionsAsync` → `List<string>` | no |
| `spAuditTrails_LookupCategories` | — | `AuditTrail_Category`, `DISTINCT`, one column | `GetAuditTrailCategoriesAsync` → `List<string>` | no |

#### 🔴 "Stalled" — the definition, and where it lives

This is the one piece of clinical business logic in nucentra that exists **only inside stored procedures**,
with no C# to read it from and no name in the schema. Written out in full:

> **A patient is STALLED when they have at least one `dbo.PatientAppointment` row *and* the
> `PatientAppointment_Status` of their most recent one is not `'Scheduled'`.**
> "Most recent" is `ROW_NUMBER() OVER (PARTITION BY Patient_ID ORDER BY PatientAppointment_Date DESC,
> PatientAppointment_StartTime DESC, PatientAppointment_ID DESC) = 1` — date first, start time to break a
> same-day tie, identity id to break the rest, so the ranking is total and deterministic.

Four consequences worth stating, because none is obvious from the word:

- **A patient with no appointment at all is NOT stalled.** In `spPatientTracker_Patients_List` the
  `LEFT JOIN` misses and the `CASE`'s first branch returns `0`; in `spPatientTracker_StalledCount_Get` the
  `INNER JOIN` from the appointment side never sees them. So a patient registered and never booked is
  indistinguishable, on this page, from one booked for tomorrow. **The tracker measures stalled *journeys*,
  not neglected *patients*.**
- **"Not `'Scheduled'`" is an open set, not a list.** The test is `<> 'Scheduled'`, so `Completed`,
  `Cancelled`, `Attended`, and any typo or future status all count as stalled. Adding a new status makes it
  stalled by default; only the exact string `'Scheduled'` is safe. The comparison inherits the column's
  collation, so it is case-insensitive in practice.
- **It is not a date test.** Nothing in either procedure looks at how long ago the latest appointment was.
  A patient whose colonoscopy was completed this morning is stalled; one with a `Scheduled` appointment from
  three years ago is not. **"Stalled" means "the journey has no next step booked", not "nothing has happened
  recently"** — and the second is what most readers assume the word means.
- **Discharged patients are counted.** Neither procedure looks at `DischargeType_ID`, so a patient who has
  completed the programme and left is stalled by this definition. The tracker lists every patient (§4.14),
  so they are on the page to be counted.

**The rule is implemented twice.** The two CTEs are character-for-character identical; the only difference
is the join direction — `Patients_List` `LEFT JOIN`s from `dbo.PatientBasic` to produce a per-row bit,
`StalledCount_Get` `INNER JOIN`s from the ranked appointments to `dbo.PatientBasic` to produce a count. They
agree, and they agree for a reason neither states: an appointment whose `Patient_ID` matches no patient is
dropped by the count's `INNER JOIN` and is invisible to the list's patient-driven `LEFT JOIN` anyway.
**Verified on the running site: `stalledCount = 4` against exactly four rows flagged `isStalled: true`.**
If you change one procedure, change the other.

#### The four dashboard aggregates — grouping and ordering

| Procedure | `FROM` | `WHERE` | `GROUP BY` | `ORDER BY` |
|---|---|---|---|---|
| `spDashboard_Branch_CountActive` | `dbo.Branch` | `ISNULL(Branch_Status, 0) = 1` | — | — |
| `spDashboard_Patient_ByRace` | `PatientBasic` `LEFT JOIN LU_RACE` | none | `COALESCE(NULLIF(LTRIM(RTRIM(Race_Name)),''),'Unknown')` | `PatientCount DESC` |
| `spDashboard_Patient_ByAgeGroup` | `PatientBasic` | none | a `CASE` over `Patient_Age` | a second `CASE` — **age order**, `Unknown` last |
| `spDashboard_Patient_ByDischargeType` | `PatientBasic` `LEFT JOIN LU_DISCHARGETYPE` | **`DischargeType_ID IS NOT NULL`** | `COALESCE(NULLIF(LTRIM(RTRIM(DischargeType_Name)),''),'Unknown')` | `PatientCount DESC` |

- **The age bands are `20 and below` / `21-40` / `41-60` / `61-80` / `81 and above`, inclusive on both ends,
  with no gap and no overlap.** They exist in that `CASE` and nowhere else — no C# constant, no lookup table.
  A sixth branch, `Unknown`, fires on `Patient_Age IS NULL` and is **unreachable** while the column is
  `INT NOT NULL` (§3.8); it is the branch that would start returning rows if that ever changed.
- **Both count-ordered charts have no tie-breaker.** Equal counts come back in an arbitrary order that the
  charts do not care about and a before/after diff does.
- **`ISNULL(Branch_Status, 0)` is defensive only** — the column is `BIT NOT NULL` (§3.2).
- **`spDashboard_Branch_CountActive` and `spPatientTracker_StalledCount_Get` are both a single `COUNT(*)`
  with no `GROUP BY`**, so each always returns exactly one row. `SqlData` reads both with
  `ExecuteScalarAsync<int>` rather than `QuerySingleAsync<int>`: identical for every reachable input, and it
  answers `0` instead of throwing for an empty result set — which is exactly what the `DataTable` guards
  these replaced did.

#### The three staff-dashboard reads — one query, three windows

The same nine columns from the same four tables (`PatientAppointment` `LEFT JOIN` `PatientBasic`,
`LU_PJ_APP_TYPE`, `Branch`), the same `ORDER BY PatientAppointment_Date, PatientAppointment_StartTime,
PatientAppointment_ID`, and the same `Staff_ID` predicate. **Only the date window differs**, which is why
one model serves all three (§6.3):

| | Window | Built by |
|---|---|---|
| Today | `= @ForDate` | the caller, always `DateTime.Today` |
| This week | `>= @FromDate AND < DATEADD(DAY, 7, @FromDate)` | the procedure — **rolling**, start inclusive, end exclusive |
| This month | `>= DATEFROMPARTS(@Year,@Month,1) AND < DATEADD(MONTH,1,…)` | the procedure — a real calendar month |

🔴 **`pa.Staff_ID = @Staff_ID` is the portal's only scoping of the STAFF dashboard**, and the parameter is
supplied from the caller's own claim by the controller, never from this layer (§4.13). A staff id matching
nothing — including a valid id belonging to somebody else — returns an **empty set, not an error**, which is
what makes it a filter rather than a check.

**`DATEFROMPARTS` throws on a month outside 1-12** rather than returning nothing, which is why
`GetMonthAppointments` range-checks before calling and answers `"Invalid month value."` itself. The year is
not validated by anyone.

#### `spAuditTrails_Search` — and why its `@UserId` is not §0.1's `@User_ID`

Six columns out of `dbo.AuditTrails` `LEFT JOIN dbo.Users`, ordered by `AuditTrail_EventUTC DESC`.

- 🔴 **`@UserId` (no underscore) IS A FILTER.** It selects which rows to show, it comes from a dropdown, and
  it is passed straight through from the request. `@User_ID` (with the underscore), in the nineteen writing
  procedures, is the actor being recorded. **Same table, adjacent concepts, one character apart.** Filling
  `@UserId` from `DatabaseHelper.CurrentUserId` would restrict every search to the searcher's own trail and
  break nothing visibly.
- **`AuditTrail_EventMYT` is a computed column name that exists nowhere on disk.** The procedure returns
  `DATEADD(HOUR, 8, a.AuditTrail_EventUTC)` under that alias, and filters the same expression, so display
  and filtering agree. Malaysia has no DST, so the fixed eight hours is correct rather than approximate.
- **`@ToDate` is inclusive of its whole day** — the predicate is `< DATEADD(DAY, 1, @ToDate)`. Both dates are
  `DATE`, so a time of day on either is discarded before it arrives.
- **`ISNULL(a.User_Id, 0) AS User_ID` and `ISNULL(u.User_Name, '') AS User_Name`** mean neither column is
  ever null, and a missed join is a blank name rather than an absent row.
- **The ordering is on the raw UTC column** while the display is on the shifted one — the same ordering, by
  construction, since the shift is monotonic.

*Verified against the running site, filter by filter, against `SELECT COUNT(*)` over the same predicates:*
no filters 105, `userId=1` 101, `userId=4` 3, `action='DELETE'` 40, `category='StaffSlots'` 27, a two-day
Malaysian range 74, and a four-way combination 6 — each matching SQL exactly.

#### The three lookup procedures — built from the data, not from a catalogue

All three are `SELECT DISTINCT … FROM dbo.AuditTrails`, so the filter dropdowns can only ever offer values
that have actually been recorded. The two single-column ones drop NULL and blank before the `DISTINCT`; the
users one filters `User_Id IS NOT NULL`.

🔴 **`spAuditTrails_LookupUsers` uses an `INNER JOIN dbo.Users`, and that hides exactly the rows an auditor
most wants.** `User_Id = 0` (the silent failure of §0.1) joins to nothing; so does a real actor whose account
has since been deleted. Neither appears in the dropdown, though both still appear in the search results with
a blank name. **The dropdown is not a complete list of who is in the table.** Measured on the local database:
105 rows, actors `1` (101), `4` (3) and `3` (1) — and `3` is a deleted account, so the dropdown offers two
users where the table holds three.

Because the users lookup returns `(User_ID INT, User_Name)`, `SqlData` reads it with the same
ordinal-mapping helper as the eleven `spLU_*` code lookups (§5.1) and gets a **string** id — which is
exactly what this endpoint's JSON has always carried, because `DataRow["User_ID"].ToString()` did the same.
The two single-column lookups cannot use that helper — there is no ordinal 1 — and come back as
`List<string>`.

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
| `DatabaseHelper.cs` | Owns the connection string (`ConnectionStrings:CRC_DB`) and the current user's id. **Two members and a constructor, and nothing else** — see §6.5, which also records what used to be here. |
| `IDatabaseData.cs` | The contract, and the **documentation** of the data layer. One method per stored procedure; a `//` comment above each saying what it is for and naming the procedure; methods grouped under `// ----- Area (where it is used) -----` banners. |
| `SqlData.cs` | The only implementation, and **the only place in the solution that names a stored procedure.** The mechanism only — read the interface to find out *what*, this file to find out *how*. |

`CRC.Data/Database/` still exists and still holds `Migrations/`, the seed CSVs. That is correct and not a
leftover: the repo has a `Data/` folder for code and a `Database/Migrations/` folder for data.

### 6.2 The rules

- **One method per stored procedure.** No method calls two — with exactly two named exceptions,
  `SaveStaffWithDocumentsAsync` and `SaveAppointmentAsync` (§6.6), each commented as such where it is
  declared. 102 methods cover 104 procedures: 100 one-to-one, plus those two.
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

**53 files, one type per file** — POCOs for Dapper to map result sets onto: public properties, no logic, no
attributes, named for the data (`BranchListItem`, `StaffDetail`, `PatientDocumentItem`) and not for the
procedure. Four kinds live here and the distinction is worth keeping: **row models** (the majority),
**input models** carrying a write's parameter set (`StaffSaveInput`, `PatientSaveInput`,
`AppointmentSaveInput`), **result models** for procedures that answer with more than a row
(`StaffDeleteResult`, `StaffPerformanceResult`, `AppointmentSaveResult`), and one enum,
`AppointmentSaveFailure` (§6.7).

🔴 **Two models are not one model just because their columns overlap.** `spUsers_GetById` returns a strict
subset of `spUsers_ValidateLogin`, so sharing a type would hand every caller `LockoutEndUtc = null` on a
locked account, silently — hence `UserAccountRecord` and `UserAuthRecord` (§5.3). `spStaffDocument_List`
and `_GetById` genuinely select the same nine columns, so they correctly share `StaffDocumentItem` (§5.4).
**Reuse the shape, never the name.**

**`LookupItem.Id` is a `string`, and that is the schema, not a shortcut.** Eleven of nucentra's twelve
`LU_*` tables key on `VARCHAR(100)` — `LU_DISCHARGETYPE`, `LU_MARITALSTATUS`, `LU_OCCUPATION`,
`LU_ORGANIZATION`, `LU_PATDOCUMENTTYPE`, `LU_PJ_APP_TYPE`, `LU_RACE`, `LU_RELIGION`, `LU_SOURCE`,
`LU_STAFFDOCUMENTTYPE`, `LU_STAFFTYPE` — seeded with two-character zero-padded codes (`"01"`, `"02"`), with
`LU_STAFFTYPE` the outlier using three-letter mnemonics (`"ANE"`, `"END"`, `"NUR"`). Every column that
references one is `VARCHAR(100)` too. Parsing an id to an `int` would appear to work and would lose the
leading zero. **`LU_LOCATION` is the single exception**: `LocationId INT IDENTITY(1,1)`, display column
`Name`, so the three `spLU_LOCATION_*` procedures get their own model, **`LocationLookupItem`** (`int Id`,
`string Name`), and are the only lookups mapped by column name rather than by ordinal (§3.1, §5.1).

🔴 **A model is never serialized straight to the browser.** It is mapped into the camelCase anonymous object
the endpoint already returns (§0), because 59 JavaScript files depend on those shapes by name. That
boilerplate is the point, not an oversight: it keeps the JSON contract independent of the data layer's
types, so renaming a model property is a compile-time change with no wire effect (§12). **The three journey
detail reads are the one deliberate exception**, and they are exceptions to the *return type* rather than
to this rule — see §7.8.

### 6.4 Registration

`CRC.Web/Program.cs`, immediately after the helper:

```csharp
builder.Services.AddScoped<CRC.Data.Data.DatabaseHelper>();
builder.Services.AddScoped<CRC.Data.Data.IDatabaseData, CRC.Data.Data.SqlData>();
```

**Scoped**, because `SqlData` resolves the current user's id per request for the audit-actor parameter — a
singleton would capture one request's `IHttpContextAccessor` state and stamp every later audit row with it.

### 6.5 🔴 `DatabaseHelper` is two members and a constructor — and what used to be there matters

The whole class, in full:

| Member | What it is |
|---|---|
| the constructor | reads `ConnectionStrings:CRC_DB`, throws if it is absent, and keeps `IHttpContextAccessor` |
| `_connectionString` | the string, private |
| `CreateConnection()` | a closed `SqlConnection` for `SqlData` to hand to Dapper |
| `CurrentUserId` | `int?` — the `NameIdentifier` claim, parsed; `null` when there is no authenticated caller |
| `GetCurrentUserId()` | private; the property's implementation |

**That is everything. There is no other data-access surface anywhere in the solution.**

#### What was deleted, and why saying so is worth a section

Before the Dapper layer, this class **was** the data-access layer, and it carried an ADO surface all
sixteen controllers called with hand-built `SqlParameter[]` arrays: `ExecuteNonQueryAsync`,
`ExecuteDataTableAsync`, `ExecuteDataSetAsync` and `CreateStoredProcedureCommandAsync`. Behind them sat a
piece of machinery that is the reason this sub-section exists:

> **Before executing ANY command it queried `sys.parameters`** — behind a static `ConcurrentDictionary`
> cache — asking *"does this stored procedure declare a parameter called `@User_ID`?"*, and if it did it
> **silently appended the caller's `ClaimTypes.NameIdentifier` value.** That is how `dbo.AuditTrails`
> learned who performed a write. No controller ever passed an actor; it arrived by magic.

All of it is gone: the four ADO methods, `TryInjectUserIdAsync`, `SupportsUserIdParameterAsync`,
`NormalizeStoredProcedure`, `HasParameter` and the `_userIdParamSupportCache` dictionary. Dapper sends
exactly the properties of the anonymous parameter object and nothing else, so there was no hook left to
hang the injection on — and once every call site had moved to `SqlData`, the machinery had no callers at
all. The class-level comment on `DatabaseHelper.cs` records the same thing at the file, for a reader who
arrives at it from old git history and wonders where a controller's procedure call went.

🔴 **DO NOT RE-INTRODUCE IT.** It is not merely redundant now; it was **unsafe in a way nobody had to
notice while it existed**. A generic injector keyed on a parameter *name* cannot see that `@User_ID` means
two different things in this codebase (§0.1) — and applied to `spUsers_Unlock`, whose `@User_ID` is the
locked-out account being unlocked, it would unlock the administrator's own account, leave the locked user
locked, and report success. The explicit `User_ID = _databaseHelper.CurrentUserId` at each of the nineteen
actor call sites is the replacement, and its cost — remembering it — is paid by §0.1, by a comment on every
one of the nineteen, and by the standing health check in §9.1.

**`CurrentUserId` is public for exactly one reason**: `SqlData` needs the claim value, because nothing
supplies it automatically any more. It is not a general-purpose "who is logged in" accessor — controllers
read `User.FindFirst(...)` for that, and the `StaffId` claim in particular must never come from here
(§4.13, §7).

### 6.6 Transactional units of work

**The rule is one `SqlData` method per stored procedure. There are exactly two exceptions**, and both are
named, both are commented as such where they are declared, and both exist because a single business fact is
spread across several procedures that must land together or not at all:

| Method | Procedures it runs | Why it must be atomic |
|---|---|---|
| `SaveStaffWithDocumentsAsync` | `spStaff_Insert` **or** `spStaff_Update`, then `spStaffDocument_GetById` + `spStaffDocument_Delete` per removal, then `spStaffDocument_Insert` per upload | the mandatory-document rule (§4.4) says an ENDOSCOPIST without a CV is not a valid record. A staff row that committed while its documents did not is exactly the state the rule exists to prevent, and no screen would show it had happened |
| `SaveAppointmentAsync` | `spStaffSlots_List`, then `spPatientAppointment_Insert` **or** `spPatientAppointment_Update` + `spStaffSlots_ClearAppointment`, then `spStaffSlots_AssignAppointment` | the slot-availability read must stay inside the transaction that consumes the slot, or two administrators book the same hour. See §6.7 |

Each opens its own `SqlConnection`, calls `OpenAsync`, begins a `SqlTransaction`, passes `transaction:` to
**every** Dapper call inside it, commits, and rolls back and rethrows on any exception. That is the only
place in `SqlData` where connection lifetime is managed by hand — everywhere else Dapper opens and closes
around a single call. It is also why `DatabaseHelper.CreateConnection()` returns a concrete `SqlConnection`
rather than an `IDbConnection` (see the house-style table in `DapperLayerPlan.md`): `BeginTransaction()`
has to be there.

**If you are about to write a third one, don't.** Two procedures called from one method is a claim that
they are one operation, and every such claim is a place where a future reader cannot tell from the
interface whether a partial write is possible. If a new flow genuinely needs it, add it to the table above
in the same breath as the code.

#### Why the blob uploads stay in the controller

`CRC.Data` has **no reference to `CRC.Web` and must not gain one.** `IDocumentStorage` is a `CRC.Web`
service, so the data layer cannot upload a byte and should not know that storage exists. The bytes are
written by `StaffController`; `SaveStaffWithDocumentsAsync` receives only the resulting **blob keys** and
writes the rows that point at them.

That leaves an ordering problem, and it is worth stating plainly because the obvious fix is wrong:

> **The blob key is `staff/{Staff_ID}/{guid}{ext}`, and for a NEW staff member the `Staff_ID` does not
> exist until `spStaff_Insert` has run — inside the transaction.**

So "upload everything first, then call the data layer" is impossible for an insert. The alternatives were:

1. **Pre-generate the `Staff_ID` in C#.** Rejected: it moves id generation out of `spStaff_Insert`, where
   it belongs and where the global sequence is computed (§3.4), and it would have to reimplement the
   `MAX(RIGHT(…, 5)) + 1` scan outside the transaction that protects it.
2. **Give the data layer an upload interface of its own.** Rejected: that is the `CRC.Web` reference,
   wearing a hat.
3. **Pass a callback.** Adopted. `SaveStaffWithDocumentsAsync` takes a
   `Func<string, Task<IReadOnlyList<StaffDocumentInput>>>` and invokes it once, after the staff row is
   written and the id is known, before the document rows go in. `Func<>` is a BCL type and carries no
   dependency; the lambda lives in the controller and closes over `IDocumentStorage`, which never crosses
   the boundary.

**What that costs is that a blob can be written and the transaction can still fail afterwards**, so the
guarantee is kept by **compensation** rather than by ordering. The controller keeps two ledgers, and the
asymmetry between them is the whole design:

| Ledger | Holds | Deleted from storage |
|---|---|---|
| `uploadedBlobs` | keys written during **this** request | in the `catch`, if anything throws — the rows were rolled back, the objects were not |
| `deleteBlobNames` | keys of documents the user **removed** | only **after** the commit returns — a rollback puts those rows back, and a deleted blob cannot be un-deleted |

Each direction waits for the outcome that makes its removal safe. `TryDeleteBlobsAsync` performs both,
one key at a time, swallowing and logging every failure: in the rollback case an exception would replace
the real failure with a cleanup failure and tell the user the wrong thing; in the post-commit case the rows
are already gone and the user has already been told it worked. The only casualty of a failed delete is an
orphaned blob, which is an operational clean-up job — hence a warning in `app-*.log`, not a faulted
request. **`spStaff_Delete` uses the same pattern from the other end**: it hands its blob keys back on a
second result set precisely because storage cannot join the transaction (§5.4).

#### The deferred-audit pattern

🔴 **`AuditLog.*` is called only AFTER the data method returns successfully**, never inside the flow that
might roll back. A rolled-back transaction that left audit lines claiming a write happened is worse than no
audit at all: the security channel is retained for 365 days (§0) and is what somebody reads when they need
to know what was done, so a line that names a change that never existed is an actively misleading record.

So `SaveStaffWithDocuments` collects `pendingUploadAudits` and `pendingDeleteAudits` while the transaction
is open and emits nothing, then writes `AuditLog.StaffCreated` / `StaffUpdated`, then every
`StaffDocumentUploaded`, then every `StaffDocumentDeleted`, once the commit has returned. The comment
explaining this is in the controller and predates the Dapper layer; it survived the migration unchanged
because the reasoning did.

**The two audit trails are made honest by opposite means, and that is not an accident:**

| Trail | Written by | Made honest by |
|---|---|---|
| `dbo.AuditTrails` | the stored procedures themselves, **inside** the transaction | the rollback, which takes the audit rows with it |
| `Logs/audit-*.log` | `AuditLog.*` in the controller, **outside** the transaction | deferral, which never writes them at all |

This was verified end to end during Prompt 3 by forcing the document insert to fail mid-transaction: the
staff row kept its previous values, no document row appeared, **`MAX(AuditTrail_Id)` did not move** — the
`spStaff_Update` audit row rolled back with the update it described — and `audit-*.log` grew by **zero
bytes**. That last assertion is the one that proves the transaction moved into `SqlData` intact rather than
merely appearing to.

### 6.7 `SaveAppointmentAsync` — the booking race, and the typed-failure-reason convention

The second of the two units of work, and the more instructive one. `SaveStaffWithDocumentsAsync` is atomic
because several **writes** must land together; this one is atomic because a **read** and a write must, and
that is a different and less obvious reason.

#### The procedure sequence

```
                    ┌─ BEGIN TRANSACTION ──────────────────────────────────────────────┐
   1  READ          │  spStaffSlots_List  (@Staff_ID, @FromDate = @ToDate = the date)  │
   2  VALIDATE      │  four slot checks + contiguity, against the rows step 1 returned  │
   3  WRITE         │  spPatientAppointment_Insert   OR   spPatientAppointment_Update   │
   4  RELEASE       │  spStaffSlots_ClearAppointment  (@ApptId)      — update path only │
   5  CLAIM         │  spStaffSlots_AssignAppointment (@ApptId, "17,18")                │
                    └─ COMMIT ─────────────────────────────────────────────────────────┘
```

Five procedure calls on one `SqlConnection`, every one passing `transaction:`, committed together or rolled
back together. **Step 4 runs before step 5 and the order is the point**: an hour kept across an edit would
otherwise be cleared *after* being re-assigned, and end up free while the appointment believed it held it.
Step 4 is keyed on the **appointment id**, not on a slot list, because `dbo.PatientAppointment` does not
record which slots it consumed (§3.9).

#### 🔴 Why the read is inside the transaction — the booking race

**Step 1 is not a convenience lookup that happens to sit nearby. It is the concurrency check.** It is the
only thing that decides whether the hours being consumed are still free, and that answer is worth something
only for as long as the transaction that asked holds its locks.

Move it out — *"the controller reads the slots, validates, then calls the save method"* — and this happens:

```
   Administrator A                          Administrator B
   read slot 17 → free                      read slot 17 → free
   validate → OK                            validate → OK
                    ── both now believe slot 17 is available ──
   BEGIN; insert appointment 8;             BEGIN; insert appointment 9;
   assign 17 → 8; COMMIT                    assign 17 → 9; COMMIT
                    ── appointment 8 silently holds nothing ──
```

**Nothing in the database catches that.** `dbo.PatientAppointment` has no unique constraint of any kind
beyond its identity, and `dbo.StaffSlots` is unique on `(Staff_ID, SlotDate, SlotStartTime)` but has
**nothing unique on `PatientAppointment_ID`** (§3.7, §3.9) — so the second `UPDATE` is a perfectly legal
overwrite. `spStaffSlots_AssignAppointment` will not object either: it is a blind `UPDATE … INNER JOIN`
that silently ignores what it cannot match and never asks whether a slot was free (§5.7). There is no
constraint, no `MERGE … HOLDLOCK`, and no version column anywhere in this path. **The read-inside-the-
transaction is the entire defence, and it is one line away from not existing.**

The distinction that makes this easy to get wrong is that nucentra has **two** reads of the same procedure,
doing two different jobs:

| Caller | Job | Where |
|---|---|---|
| `/Patient/GetAppointmentSlots` | **paint the slot picker** — what looked free when the page loaded | outside any transaction, and correctly so |
| `SaveAppointmentAsync` step 1 | **decide whether it is still free** — under lock, at the instant of writing | inside the transaction, necessarily |

They return the same five columns and share `StaffSlotItem`. Only one of them is load-bearing, and it is
not the one the user sees.

#### 🔴 The typed-failure-reason convention — the most reusable thing here

Moving the validation into the data layer creates a problem: the checks must live where the read lives, but
the **sentences** must not. A user-facing message is worded for one screen, is what a JavaScript file may
match on, and changing it is a product decision. A message string in `CRC.Data` would mean a controller
could no longer alter its own copy without editing the data layer, and a second screen calling the same
method would be stuck with the first screen's wording.

So:

> **THE DATA LAYER DECIDES WHAT FAILED. THE CONTROLLER DECIDES WHAT THE USER IS TOLD.**

`SaveAppointmentAsync` returns `AppointmentSaveResult` carrying an `AppointmentSaveFailure` enum —
`Ok`, `SlotNotFound`, `SlotWrongStaff`, `SlotWrongDate`, `SlotTaken`, `SlotsNotConsecutive`, `InsertFailed`
— and `PatientController.SaveAppointment` maps each value to the exact string it showed before the
migration, in one `switch`, in the open. That mapping is the only place any of those sentences appears.

**Reason `Ok` means the transaction committed. Every other value means it was rolled back and nothing was
written** — no appointment row, no slot change, no `dbo.AuditTrails` row. A *genuine* fault still throws,
as everywhere else in this layer; the enum is for the validation outcomes the flow is designed to produce.

**Copy this shape for the next flow that fails for several distinct reasons.** The alternatives are all
worse, and each fails in its own way:

| Instead of an enum | Why not |
|---|---|
| return `bool` | loses the reason; the controller cannot tell a taken slot from a missing one |
| throw a different exception per reason | turns expected validation into control flow through `catch`, and makes "nothing was written" indistinguishable from a lock timeout |
| return the message string | moves the product's voice into `CRC.Data`, and freezes it for every future caller |
| return a status *code* string (`"SLOT_TAKEN"`) | an enum with no compiler check |

The enum is also where the **unreachable** reasons are documented rather than deleted. `SlotWrongStaff` and
`SlotWrongDate` cannot fire while step 1's read is narrowed by staff and date (§5.7) — and they could not
fire before the migration either. Keeping them, with a comment saying why, costs one `switch` arm and
preserves the answer for the day that narrowing changes.

#### What was NOT moved, and why the boundary sits there

`SaveAppointment` is still 70 lines of controller. Everything that can be decided **from the request
alone** stayed: the trimming, the seven-field mandatory check, the `yyyy-MM-dd` `TryParseExact`, the
three-status `HashSet`, the `Distinct()` on the slot ids, every message string, and both `AuditLog` calls.
Everything that needs **rows read under a lock** moved: the slot read, the four slot checks, the contiguity
check, the start/end derivation and the five procedure calls.

That is the whole test for where a check belongs, and it is worth stating because the tempting boundary —
"validation in the controller, database calls in the data layer" — is the one that reintroduces the race.

**The start and end times are derived inside the transaction**, as the earliest requested slot's start and
the latest one's end, which is why `AppointmentSaveInput` has no properties for them. A caller that computed
them itself would be computing them from a read taken outside the lock.

#### The audit trails, made honest by opposite means — again

Exactly as in §6.6, and for the same reason:

| Trail | Written by | Made honest by |
|---|---|---|
| `dbo.AuditTrails` | `spPatientAppointment_Insert` / `_Update`, **inside** the transaction | the rollback, which takes the audit rows with it |
| `Logs/audit-*.log` | `AuditLog.AppointmentCreated` / `AppointmentUpdated` in the controller, **outside** | deferral — they are written only after the commit returns |

Verified end to end during Prompt 6, driving the full lifecycle against the running site: a booking of two
slots, an edit that dropped one hour and took another, four rejections, two status changes, and a delete.
Every one of the six `dbo.AuditTrails` rows carried **`User_Id = 1`, the SUPERUSER's id — never `0`** — and
each of the four rejections left `MAX(AuditTrail_Id)` unmoved, no appointment row changed and no slot
changed. The `@User_ID` actor parameter is passed explicitly on all four writes, as §0.1 requires.

---

## 7. The patient journey — the core feature

> ### 🔴 IF YOU HAVE COME FROM HEART, READ THIS PARAGRAPH BEFORE ANYTHING ELSE
>
> **nucentra has no state machine. There is no `StageFlag`, no `LU_STAGEFLAG`, no `StageLog`, no
> `SortOrder`, no transition table, and no column anywhere that says which stage a patient is at.**
>
> HEART's `CoreFlow.md` §2 is titled *"The lifecycle = the `StageFlag` state machine"* and describes five
> stage rows over three steps, forward gating enforced in three places, backward un-toggling that moves
> exactly one step, and a `StageLog` timeline that is rewritten on every move. **None of that exists here,
> in any form.** Do not go looking for the tables; do not "restore" them; do not assume an endpoint is
> gated by a stage it does not have.
>
> What nucentra has instead is **an append-only log of clinical events**. A `dbo.PatientJourney` row means
> *"this happened"*, not *"the patient is here"*. §7.7 states in full what that costs.

### 7.1 The domain, in a paragraph

A colorectal-cancer screening journey is what happens to a person **after their iFOBT comes back
positive**. The immunochemical faecal occult blood test is the programme's front door: a `PatientBasic` row
records the result (§3.8), and `Patient_iFOBTResults = 1` is the clinical trigger for everything below.
From the schema's point of view the journey is then **a sequence of dated clinical events, each owned by one
clinician and each of one of four kinds**. First a **patient assessment** — the clinician sits with the
patient and records risk factors, symptoms, medical, allergy and medication history, family history, a
physical examination, which investigations were ordered and how the patient was prepared. Then a
**colonoscopy** — bowel preparation quality, and then, segment by segment from anus to caecum, whether each
was normal and what was found if it was not, plus any complications and whether a specimen went for
histopathology. Then a **follow up** — the histopathology result comes back, a discharge plan is written,
and a discharge summary may be issued. **Surveillance** is the fourth kind: the outcome *"come back and be
scoped again in N years"*, which is a scheduling decision rather than a clinical record. The patient
finally leaves the programme by being **discharged** — `PatientBasic.DischargeType_ID` set to NORMAL,
BENIGN POLYPS, PRECANCEROUS POLYPS or CANCER — which is written on a **different screen, by a different
kind of user, on a different table**, and is not part of the journey at all.

### 7.2 The flow, end to end

```
                       ┌──────────────────────────────────────────────────┐
   REGISTRATION        │  dbo.PatientBasic                                │   /Patient/Edit
   (ADMIN / SUPERUSER) │  demographics + address + emergency contact      │   AdminOrSuper
                       │  Patient_iFOBTStatus / _CompletionDate           │
                       │  🔴 Patient_iFOBTResults = 1  ── POSITIVE ──┐    │
                       └─────────────────────────────────────────────┼────┘
                                                                     │
                            ▼ nothing enforces this arrow.  A journey can be recorded for a patient
                              whose iFOBT is NULL, 0, or never entered.  It is clinical practice.
                                                                     │
   BOOKING             ┌─────────────────────────────────────────────┴────┐
   (ADMIN / SUPERUSER) │  dbo.PatientAppointment   PjAppType_ID = 01…04   │   /Patient/Edit
                       │  consumes dbo.StaffSlots hours                   │   Appointment tab
                       └─────────────────────────────────────────────┬────┘
                            ▲ ALSO not enforced, in EITHER direction. An appointment does not create a
                              journey row and a journey row does not need an appointment. The two tables
                              share only a patient and a clinician — there is no key between them.
                                                                     │
   ═══ THE JOURNEY ═════════════════════════════════════════════════════════════════════════════════
   (STAFF only)        ┌─────────────────────────────────────────────┴────┐  /StaffPatient/Details/{id}
                       │  dbo.PatientJourney      one row per EVENT       │  StaffOnly
                       │  Patient_ID · PjAppType_Name · Date · Staff_ID   │
                       └──┬──────────────┬──────────────┬─────────────┬───┘
                          │              │              │             │
        ┌─────────────────▼──┐  ┌────────▼─────────┐  ┌─▼──────────┐  │
        │ 01 PATIENT         │  │ 02 COLONOSCOPY   │  │ 03 FOLLOW  │  │  04 SURVEILLANCE
        │    ASSESSMENT      │  │                  │  │    UP      │  │
        ├────────────────────┤  ├──────────────────┤  ├────────────┤  ├──────────────────┐
        │ dbo.Patient        │  │ dbo.Patient      │  │ dbo.       │  │ 🔴 NO DETAIL     │
        │     Assessment     │  │     Colonoscopy  │  │ PatientFo… │  │    TABLE.        │
        │ 46 cols  FK ✓      │  │ 32 cols  FK ✓    │  │ 6 cols FK✓ │  │    NO PROCEDURE. │
        │ risks · symptoms   │  │ bowel prep       │  │ HPE_Results│  │    NO TEMPLATE.  │
        │ history · exam     │  │ 9 segments,      │  │ Discharge  │  │    NO WAY TO     │
        │ investigations     │  │   anus → caecum  │  │   Plan     │  │    CREATE ONE.   │
        │ management         │  │ complications    │  │ Summary    │  │                  │
        └────────────────────┘  │ HPE_Status  ─────┼──┼──▶ answered │  └──────────────────┘
                                └──────────────────┘  │  here, by  │
                                                      │  free text │
                                                      └────────────┘
                          │              │              │
                          └──────────────┴──────────────┘
                                         │  every create AND every update also writes …
                                         ▼
                       ┌──────────────────────────────────────────────────┐
                       │  dbo.PatientJourneyAudit   CREATED / UPDATED     │  ← rendered in the timeline
                       │  keyed on Staff_ID, with the clinician's note    │     as the journey's history
                       └──────────────────────────────────────────────────┘
   ═════════════════════════════════════════════════════════════════════════════════════════════════
                                         │
                            ▼ again not enforced. Nothing requires a follow-up before a discharge.
                                         │
   DISCHARGE           ┌─────────────────▼────────────────────────────────┐  /Patient/Edit
   (ADMIN / SUPERUSER) │  dbo.PatientBasic.DischargeType_ID  ← LU_DISCH…  │  Discharge tab
                       │  + Patient_DischargeDate + _DischargeRemarks     │  AdminOrSuper
                       │  🔴 NULL here IS the definition of "active"      │
                       │  gated ONLY by spPatient_Discharge_Check         │
                       │       MissingDocuments — a DOCUMENT check,       │
                       │       not a journey check                        │
                       └──────────────────────────────────────────────────┘
```

**Read the three dotted arrows.** iFOBT-positive → journey, appointment → journey, and journey → discharge
are all **conventions of clinical practice**. Not one of them is a foreign key, a check constraint, a
procedure guard or a controller validation. The only gate anywhere on the way out is
`spPatient_Discharge_CheckMissingDocuments` (§5.6), and it asks about **uploaded documents**, never about
journeys.

**Note also who does what.** The journey is the one part of the product a SUPERUSER cannot write
(`StaffOnly`, §4.9), and registration, booking and discharge are the parts a STAFF user cannot write
(`AdminOrSuper`). The two halves of a patient's record are owned by two different kinds of user, and the
handover between them is not modelled at all.

### 7.3 The four types, and which has a detail table

`LU_PJ_APP_TYPE` (`CRC.Data/Database/Migrations/LU_PJ_APP_TYPE.csv`, four rows):

| Code | Name | Detail table | Written by | What it records |
|---|---|---|---|---|
| `01` | **PATIENT ASSESSMENT** | `dbo.PatientAssessment` (§3.12) | `spPatientAssessment_CreateWithJourney` | risks, symptoms, medical / allergy / medication / family history, physical examination, investigations, management — as at the iFOBT-positive date |
| `02` | **COLONOSCOPY** | `dbo.PatientColonoscopy` (§3.13) | `spPatientColonoscopy_CreateWithJourney` | bowel preparation, nine per-segment findings with JSON details, complications, whether a specimen went for HPE, discharge plan, medications given |
| `03` | **FOLLOW UP** | `dbo.PatientFollowUp` (§3.14) | `spPatientFollowUp_CreateWithJourney` | the HPE result, the discharge plan, whether a discharge summary was issued |
| `04` | **SURVEILLANCE** | 🔴 **NONE** | 🔴 **nothing** | — |

🔴 **SURVEILLANCE HAS NO DETAIL TABLE, AND THIS WAS CHECKED RATHER THAN ASSUMED.** There is no
`dbo.PatientSurveillance` in `CRC.Database/dbo/Tables/`, no `spPatientSurveillance_*` under
`Stored Procedures/`, no `_PatientSurveillance.cshtml` under `Views/StaffPatient/Templates/`, no
`patientSurveillance.js`, and **`GetJourneyTemplate` recognises exactly three strings** —
`"PATIENT ASSESSMENT"`, `"COLONOSCOPY"`, `"PATIENT FOLLOW UP"` — answering `400 "Unsupported journey type."`
to anything else. **There is no way to create a SURVEILLANCE `PatientJourney` row through the portal at
all.** The code exists in the lookup for one reason: it is a valid
`PatientAppointment.PjAppType_ID`, so a surveillance *visit can be booked* — it just cannot be *recorded*.
That is the honest state of the feature, not an oversight to fill in on the way past.

🔴 **AND THE JOURNEY ROW DOES NOT STORE THE CODE.** `PatientJourney.PjAppType_Name` is a **string literal
written by the create procedure** (§3.10) — and the follow-up's literal, `'PATIENT FOLLOW UP'`, is **not the
lookup's value**, which is `FOLLOW UP`. Nothing joins the column to the table, so nothing has ever noticed.
`PatientAppointment.PjAppType_ID` holds the *code* and is a different column on a different table. Two
vocabularies for one idea, never compared.

### 7.4 The exact write path

Every clinical write in nucentra is one of six procedures, and each one writes **three tables in one call**.
The full statement order, the foreign key that forces it, and the four asymmetries between the six are in
**§5.8**; this is the shape:

```
CREATE                                          UPDATE
──────────────────────────────────────────      ────────────────────────────────────────────────
BEGIN TRAN                                      BEGIN TRAN
  validate patient (2 of 3) + staff (3 of 3)      IF NOT EXISTS journey → RAISERROR
  1  INSERT dbo.PatientJourney                    validate staff       → RAISERROR
     @PatientJourney_ID = SCOPE_IDENTITY()        1  UPDATE dbo.PatientJourney   (date, Updated_At,
  2  INSERT dbo.{detail table}                                                    UpdatedBy_Staff_ID)
  3  INSERT dbo.PatientJourneyAudit 'CREATED'     2  UPDATE dbo.{detail table}
COMMIT                                               IF @@ROWCOUNT = 0 → RAISERROR
SELECT @PatientJourney_ID                         3  INSERT dbo.PatientJourneyAudit 'UPDATED'
                                                COMMIT
```

**🔴 ATOMICITY LIVES INSIDE THE PROCEDURE, NOT IN C#.** All six open `SET XACT_ABORT ON; BEGIN TRY BEGIN
TRAN`, commit at the end, and `ROLLBACK` + `THROW` from their `CATCH`. So **each gets exactly one ordinary
Dapper method and no C# transaction**: no `SqlConnection` opened by hand, no `BeginTransaction()`, no
`transaction:` argument. Nucentra still has exactly **two** transactional units of work in the data layer —
`SaveStaffWithDocumentsAsync` and `SaveAppointmentAsync` (§6.6) — and this area adds none. Wrapping one of
these in a `SqlTransaction` would nest a transaction inside one that already exists, and would claim in
`IDatabaseData` an atomicity guarantee the data layer does not provide.

**Step 1 must come first, and a real foreign key is why.** `FK_PatientAssessment_PatientJourney`,
`FK_PatientColonoscopy_PatientJourney` and `FK_PatientFollowUp_PatientJourney` are enforced, so the detail
row cannot exist before the journey row — and the journey's identity is only known from `SCOPE_IDENTITY()`
after it is inserted. **These three are the only enforced foreign keys in this whole feature area**;
`Patient_ID` and `Staff_ID` are unconstrained everywhere, as they are throughout nucentra.

🔴 **THE UPDATE PATH `UPDATE`s THE JOURNEY ROW. IT DOES NOT INSERT ONE — AND THAT IS THE FAILURE THIS SPLIT
EXISTS TO PREVENT.** An update that inserted a journey row would show the same assessment twice on the
timeline, in the right order, with plausible dates and no error anywhere. Asserted end to end during
Prompt 7's smoke test: the journey-row count for the patient went `1 → 1` across an assessment update,
`2 → 2` across a colonoscopy update and `3 → 3` across a follow-up update, and each detail table held
exactly one row per journey throughout.

**What is NOT written.** 🔴 **Not one of the twelve journey procedures writes a `dbo.AuditTrails` row, and
not one declares `@User_ID` of either kind.** Measured on a database where the entire flow had just been
driven: six `PatientJourneyAudit` rows, **zero** `AuditTrails` rows. Recording a colonoscopy leaves no trace
in nucentra's security trail. What the six writes take instead is `@Staff_ID` — the **clinician**, an
ordinary business argument from the controller's `StaffId` claim, never `DatabaseHelper.CurrentUserId`
(§0.1, §5.8).

### 7.5 How the timeline is assembled and ordered

`spPatientJourney_TimelineByPatient` is one `SELECT` over `dbo.PatientJourney` with two `OUTER APPLY`s into
`dbo.PatientJourneyAudit` — the **earliest** `'CREATED'` event and the **latest** `'UPDATED'`/`'EDITED'` one
— each `LEFT JOIN`ing `dbo.Staff` for a name. `StaffPatientController.GetTimeline` then makes a **second**
call, `spPatientJourney_AuditsByPatient`, for the *full* history, groups it by `PatientJourney_ID` in C#,
and hangs each journey's events off it as `auditEvents`. Two reads, one payload.

**The order is `PatientJourney_Date ASC, PatientJourney_ID ASC`, and it is the only sequencing this feature
has.** There is no stage to sort on, so *the business date the clinician typed* is what puts a patient's
history in clinical order. Three things follow:

- **The caller must not re-sort**, and does not. Compare `/AdminDashboard/GetTodayAppointments`, which
  deliberately reverses its procedure's order (§4.8.4) — there is no equivalent here.
- **The identity tiebreak decides two events recorded for the same instant**, so the row written first
  shows first.
- 🔴 **EDITING A JOURNEY'S DATE MOVES IT IN THE TIMELINE.** The update path re-writes
  `PatientJourney_Date`, so a follow-up back-dated before its colonoscopy reorders the patient's history,
  silently and immediately. Nothing warns, and nothing compares the dates of a patient's journeys to each
  other.

**Timestamps.** `PatientJourney_Date` is a business `DATETIME` and is serialized as-is, with no offset.
`Audit_At`, `CreatedAt` and `UpdatedAt` are `DATETIME2(0)` defaulted to `SYSUTCDATETIME()` — genuinely UTC,
but SQL Server hands them back as `DateTimeKind.Unspecified`, so `GetTimeline` `SpecifyKind(…, Utc)`s each
one and serializes it as a `DateTimeOffset` (`"2026-08-09T00:20:37+00:00"`). Skip that and the `+00:00` the
JSON prints would be a lie. It is the same treatment §4.3's `dbo.Users` timestamps get, and the **opposite**
of `PatientDocument.UploadedOn`, which is Malaysian local time and must not be relabelled (§3.15).

**The audit read is wrapped in its own `try/catch` that swallows everything**, with the comment *"If the SP
isn't deployed yet, timeline still works."* So a failure of `spPatientJourney_AuditsByPatient` degrades the
timeline to empty `auditEvents` rather than failing the page — and it is silent, because that `catch` logs
nothing.

### 7.6 What `dbo.PatientJourneyAudit` records, and when

**One row per successful `…WithJourney` call, inside that procedure's own transaction. That is the whole
rule.** `'CREATED'` from the three creates, `'UPDATED'` from the three updates, carrying the
`PatientJourney_ID`, `SYSUTCDATETIME()`, the clinician's `Staff_ID` and the free-text `Audit_Note` they
typed on save.

Driving the full flow — create and update each of the three types for one patient — produced exactly this,
and nothing else:

```
PatientJourneyAudit_ID | Journey | Type               | Action  | Staff_ID   | Note
        5              |    8    | PATIENT ASSESSMENT | CREATED | END-00001  | P7 baseline assessment created
        6              |    8    | PATIENT ASSESSMENT | UPDATED | END-00001  | P7 assessment EDITED
        7              |    9    | COLONOSCOPY        | CREATED | END-00001  | P7 baseline colonoscopy created
        8              |    9    | COLONOSCOPY        | UPDATED | END-00001  | P7 colonoscopy EDITED
        9              |   10    | PATIENT FOLLOW UP  | CREATED | END-00001  | P7 follow up created
       10              |   10    | PATIENT FOLLOW UP  | UPDATED | END-00001  | P7 follow up EDITED
```

**So what is the table actually for?** It is the **clinical provenance of the record**, shown to the user:
who wrote this assessment, who changed it, when, and what they said about the change. It is the only place
the `Audit_Note` exists, and it is rendered in the timeline. Four things it is *not*:

- 🔴 **It is not `dbo.AuditTrails` and it does not name a login.** Its actor column is a
  `dbo.Staff.Staff_ID`; verified against the live database, **there is no `User_Id` column on this table at
  all**. Two accounts sharing one `Staff_ID` are indistinguishable here — which `spUsers_Register`'s
  one-login-per-staff-member rule (§3.3) makes unlikely rather than impossible.
- **It is not a diff.** It records *that* a journey was updated, never *what changed*. The previous values
  are gone; `dbo.PatientJourney` keeps only `Updated_At` and `UpdatedBy_Staff_ID`, i.e. the latest.
- **It records no deletions**, because nothing deletes a journey except `spPatient_DeleteCascade` — and see
  §7.7.
- **It has no foreign key to `dbo.PatientJourney`.** That is what lets its rows outlive the journey they
  describe, which is exactly what happens below.

### 7.7 🔴 A HONEST STATEMENT OF WHAT IS *NOT* ENFORCED

This is the most important sub-section in §7. Everything above describes what the code does; this is what a
reader will assume and be wrong about.

#### There is no state machine, and journey rows are events rather than states

- **No column says which stage a patient is at.** Not on `PatientBasic`, not on `PatientJourney`, not
  anywhere. "Where is this patient up to?" is answered — if at all — by looking at the *set* of journey
  rows they have and reasoning about it. Nothing in the product does that reasoning.
- **No gate stops a follow-up being recorded before an assessment**, or a colonoscopy with no assessment,
  or three assessments and nothing else, or an assessment dated after the follow-up. Every one of those
  saves cleanly. There is no ordering check in the `.js`, the controller, the data layer or the procedure.
- **No transition table, because there are no transitions.** Compare HEART's §2.3, which enumerates every
  legal move with its guard, its `StageFlag` change, its `StageLog` effect and its audit event. The nucentra
  equivalent would have one row: *"a clinician saved a form; a row was appended"*.
- **Nothing is derived from the journey.** No dashboard tile, no list filter and no report reads
  `PjAppType_Name` to decide a patient's status. `/Patient/Active` versus `/Patient/Discharged` partitions
  on `DischargeType_ID IS NULL` (§3.8) and never looks at journeys at all.
- **A journey cannot be deleted, corrected away, or superseded.** There is no delete endpoint, no procedure
  and no soft-delete column. A journey recorded against the wrong patient stays there until somebody opens
  SSMS.
- **`SURVEILLANCE` is unreachable** (§7.3): a fourth type exists in the lookup and cannot be recorded.

**Ordering is a matter of clinical practice, not a constraint.** That is a legitimate design for an
append-only clinical log — but it means *the schema will not stop a mistake*, and any feature that needs to
know "has this patient had their colonoscopy yet?" has to compute it, from `PjAppType_Name` strings, itself.

#### Two integrity gaps that are live today

🔴 **1 — DELETING A PATIENT WITH A JOURNEY HALF-SUCCEEDS, AND REPORTS FAILURE.** `spPatient_DeleteCascade`
(§5.6) deletes in this order: appointments, **`dbo.PatientJourney`**, documents, follow-ups, colonoscopies,
assessments, then the patient. But the three detail tables hold **enforced foreign keys pointing at
`dbo.PatientJourney`** (§7.4), so statement 2 is refused:

```
The DELETE statement conflicted with the REFERENCE constraint "FK_PatientAssessment_PatientJourney".
The conflict occurred in database "CRC_DB", table "dbo.PatientAssessment", column 'PatientJourney_ID'.
The statement has been terminated.
```

**"The statement has been terminated" — not the batch.** The procedure has no transaction and no
`TRY/CATCH`, and a foreign-key violation aborts the *statement* only, so **execution continues**. Measured
directly, deleting two patients who each had three journeys:

| Table | After |
|---|---|
| `dbo.PatientBasic` | **deleted** ✅ |
| `dbo.PatientDocument`, `PatientFollowUp`, `PatientColonoscopy`, `PatientAssessment` | **deleted** ✅ |
| `dbo.PatientJourney` | 🔴 **six rows survive, pointing at patients that no longer exist** |
| `dbo.PatientJourneyAudit` | 🔴 **nine rows survive** (no FK to anything) |
| `dbo.AuditTrails` | a `DELETE`/`PatientBasic` row **was** written, saying the patient was deleted |
| the HTTP response | `{ "success": false, "message": "Error deleting patient.", "correlationId": "…" }` |

So the endpoint says it failed, the audit trail says it succeeded, the patient is gone from every list, and
their clinical history is stranded. **§5.6 predicted the opposite failure** — *"leaves the appointments,
journey and documents gone and the patient row still there"* — because it reasoned from the statement order
without the foreign keys. The measured behaviour is the reverse and worse.

The orphans are invisible through the portal, because every journey read `INNER JOIN`s `dbo.PatientBasic`:
`spPatientJourney_GetById` returns nothing for them, so `/StaffPatient/GetPatientAssessment` answers
*"Journey not found."* about a row that plainly exists. **There is no procedure that cleans them up**;
removing them is a hand-written `DELETE`. Deleting the detail rows first, then the journeys, then the
patient would work — which is what a fix would have to do, and it is a `.sql` change no prompt in this plan
is permitted to make.

🔴 **2 — A COLONOSCOPY CAN BE RECORDED AGAINST A PATIENT WHO DOES NOT EXIST.**
`spPatientAssessment_CreateWithJourney` and `spPatientFollowUp_CreateWithJourney` both look
`dbo.PatientBasic` up and `RAISERROR 'Patient not found.'`;
`spPatientColonoscopy_CreateWithJourney` only refuses a **blank** `@Patient_ID`. Since `Patient_ID` is not a
foreign key anywhere in this area, the row goes in and the journey is immediately invisible (the reads
`INNER JOIN` the patient). Same shape as gap 1, arrived at from the other end.

#### What IS enforced

For balance, because it is a short list and every item is load-bearing:

- **The three detail foreign keys.** A detail row cannot exist without its journey row. This is the only
  referential integrity in the feature — and, per gap 1, it is also what breaks the cascade.
- **`@Staff_ID` must resolve to a `dbo.Staff` row.** All six writes check it and `RAISERROR` if not, which
  is the one thing standing between the `StaffId` claim and a junk value.
- **`@Patient_ID` must resolve** — on two creates of the three.
- **The journey must exist, and must already have a detail row of the right type, before an update.** Two
  separate `RAISERROR`s, so re-saving a colonoscopy as an assessment fails rather than inserting.
- **The policy split** (§4.9): only a `UserType = 3` account can write here, and only an account with a
  non-blank `StaffId` claim can get past the controller's own check.

### 7.8 The data layer's one departure from the house rules, and why

`IDatabaseData` rule 5 says *return `List<T>`, `T?` or a scalar — never `DataTable`, never `object`, never
`dynamic`*. The three detail reads return **`IReadOnlyDictionary<string, object?>?`** instead, and the
reason is a contract rather than a convenience:

> **The endpoint serializes the detail row AS THE PROCEDURE NAMES ITS COLUMNS.** The browser receives
> `{"PatientJourney_ID":5,"iFOBTPositive_Date":"…","Risks_Smoking":true,…}`, and
> `patientAssessment.js`, `patientColonoscopy.js` and `patientFollowUp.js` read exactly those keys —
> `d.iFOBTPositive_Date`, `model.HPE_Results`, `Findings_Anus`.
>
> ASP.NET Core serializes with `JsonSerializerDefaults.Web`, which **camelCases property names and leaves
> dictionary keys untouched**. A POCO would therefore ship `"patientJourney_ID"` and `"risks_Smoking"`,
> break all three clinical forms, and **return `200` while doing it**.

`SqlData.QueryJourneyDetailRowAsync` reads the field names off the reader rather than accepting a `dynamic`
row — so no `dynamic` enters the layer, key order is the procedure's `SELECT` order, and `DBNull` becomes
`null` exactly as the `DataTable` code it replaced produced. It is the **second** helper in `SqlData` that
reads a result set without a model, after `QueryLookupAsync`'s ordinal mapping (§3.1), and both exist for
the same reason: one place doing something unusual deliberately beats three call sites doing it by accident.

A model per detail type would also be ~50 properties of pure transcription that no C# code ever reads by
name — the endpoint passes the whole object straight through. **If a future change needs one of these
fields server-side, add a purpose-named model for that read and leave the pass-through alone.**

---

## 8. Documents

> **`DOCUMENTSTORAGE.md` owns the operator picture and is not repeated here** — where the blobs live, the
> SAS flow, the accepted file types and sizes, the two `DocumentStorage` settings, Azurite, and the stranded
> `wwwroot/uploads` files on old deployments. **This section is the code-level map**: the two families, the
> settings layer that makes a document *mandatory*, and the endpoints. When the two disagree about storage,
> `DOCUMENTSTORAGE.md` is right.

### 8.1 Two families, mirrored but not identical

| | Patient | Staff |
|---|---|---|
| Catalogue table | `dbo.PatientDocument` (§3.15) | `dbo.StaffDocument` (§3.5) |
| Type lookup | `LU_PATDOCUMENTTYPE` (13 rows) | `LU_STAFFDOCUMENTTYPE` (8 rows) |
| Blob key prefix | `patients/{Patient_ID}/` | `staff/{Staff_ID}/` |
| Procedures | 7 in `Stored Procedures/PatientDocument/` (§5.8, §5.9) | 6 in `Stored Procedures/StaffDocument/` (§5.4) |
| Model | `PatientDocumentItem` | `StaffDocumentItem` |
| Owning controller | `StaffPatientController` | `StaffController` |

The two are close enough to look interchangeable and differ in four places that have bitten already:
**`PatientDocument.UploadedOn` is a `VARCHAR(100)` and `StaffDocument.UploadedOn` is a `DATETIME`** (§3.15);
`spPatientDocument_List` **requires** its `@Patient_ID` while `spStaffDocument_List`'s is optional and
returns everything when omitted; the staff save is **transactional** and the patient upload loop is not
(§4.9); and only the staff side has a mandatory-document check at save time.

### 8.2 The settings layer — what makes a document *mandatory*

Neither settings table has an `IsMandatory` column. **A row's existence is the rule**, and an empty table
means nothing is mandatory anywhere — which is the state of a freshly published `CRC_DB`.

| Table | Keyed by | The rule | Enforced by | What it blocks |
|---|---|---|---|---|
| `dbo.StaffDocumentSettings` (§3.6) | staff type | an ENDOSCOPIST must have a CV | `StaffController.GetMandatoryDocsByStaffType` and `GetMissingMandatoryDocuments`, both in C# over `spStaffDocumentSettings_GetByStaffType` | **saving a staff member** — `SaveStaffWithDocuments` writes nothing at all; `SaveStaff` only reports, after committing (§4.4) |
| `dbo.PatientDocumentSettings` (§3.16) | discharge reason | a patient discharged as NORMAL must have a DISCHARGE SUMMARY | `spPatient_Discharge_CheckMissingDocuments`, in SQL (§5.6) | **discharging a patient** — `PatientController.SaveBasic` returns `"Please upload the following mandatory documents before discharging this patient: …"` and writes nothing |

Both are edited by the one SUPERUSER Settings screen (§4.10), whose two halves save through different
mechanisms — see the asymmetry in §5.9. **Neither check counts documents**: one row of each required type is
enough. **The patient check returns what is MISSING**, so an empty result is the pass condition; reading it
the other way round lets every discharge through.

### 8.3 The endpoints — three controllers, one search page

| Verb | Route | Policy | What it does |
|---|---|---|---|
| POST | `/StaffPatient/UploadPatientDocuments` | `AdminOrSuperOrStaff` | validates the whole batch, then uploads + inserts per file. **No transaction, no compensation** (§4.9) |
| GET | `/StaffPatient/GetPatientDocumentUrl?id=` | `AdminOrSuperOrStaff` | mints a 5-minute read SAS for one patient document |
| POST | `/StaffPatient/DeletePatientDocument` | `AdminOrSuperOrStaff` | deletes the row, then the blob, best-effort |
| POST | `/Staff/UploadStaffDocuments` | `AdminOrSuper` | the staff twin of the upload |
| GET | `/Staff/GetStaffDocumentUrl?id=` | `AdminOrSuper` | the staff twin of the SAS mint |
| POST | `/Staff/DeleteStaffDocument` | `AdminOrSuper` | the staff twin of the delete |
| POST | `/Staff/SaveStaffWithDocuments` | `AdminOrSuper` | staff row + documents in **one transaction** (§6.6) |
| POST | `/Documents/Search` | `SuperUserOnly` | the only read across **both** families (§4.11) |
| GET | `/Documents/DocumentUrl?mode=&id=` | `SuperUserOnly` | the SAS mint for either family, dispatched on `mode` |

**A patient document is one policy level more reachable than a staff document** — `AdminOrSuperOrStaff`
versus `AdminOrSuper` — because clinicians work in the patient journey and never in the staff register.
`/Documents/*` sits above both at `SuperUserOnly`, which is the right level for a page that lists everything.

**Deleting a whole patient or a whole staff member deletes their documents too**: `spPatient_DeleteCascade`
and `spStaff_Delete` each return the blob keys they orphaned so the controller can remove the objects
afterwards (§5.6, §5.4). Storage cannot join a database transaction, so the row goes first and the object
second; a failed removal is logged as a warning, not raised, because from the user's side the document is
gone and what is left is an orphaned blob for an operator.

### 8.4 Validation and storage

- **`CRC.Web/Infrastructure/DocumentValidation.cs`** — the *only* place the rules live, shared by all three
  upload endpoints: allowed extensions **and** content types (both must pass), a 20 MB cap, `SafeFileName`
  bounded to 255 because both `FileName` columns are `VARCHAR(255)`, and `BuildBlobName`.
- **`CRC.Web/Services/AzureBlobDocumentStorage.cs`** — the only place that talks to Blob storage, behind
  `IDocumentStorage` (`UploadAsync`, `GetReadSasUrl`, `DeleteAsync`), registered as a singleton.

**See [`DOCUMENTSTORAGE.md`](DOCUMENTSTORAGE.md)** for the container, the key layout, the SAS trade-off, the
configuration, and local development with Azurite.

---

## 9. Audit and logging

> ### 🔴 nucentra HAS TWO INDEPENDENT AUDIT CHANNELS. THEY ARE NOT THE SAME THING AND MUST NEVER BE CONFLATED.
>
> **1 — `dbo.AuditTrails`.** Rows written **from inside stored procedures**, using the `@User_ID` actor
> parameter. Queryable data: joinable, filterable, and readable by a SUPERUSER on `/AuditTrails` without a
> server login. It records **what changed in the database**.
>
> **2 — The Serilog channels.** Append-only text files under `CRC.Web/Logs/`, split into a security channel
> (`audit-*.log`, 365 days) and an operational one (`app-*.log`, 31 days). No SQL, no UI, no join. It
> records **what happened in the application** — logins, lockouts, document downloads, rate-limit
> rejections, exceptions — including a great deal that never touches a table.
>
> **A write that matters gets both**, and neither is a substitute for the other. Creating an appointment
> writes an `AuditTrails` row (`INSERT` / `PatientAppointment`, naming the actor) *and* an
> `AuditLog.AppointmentCreated` line (naming the patient, staff, branch, times and correlation id). Losing
> one loses half the story: the table cannot tell you a document was **downloaded**, and the file cannot be
> joined to `dbo.Users`.

### 9.1 Channel one — `dbo.AuditTrails`, written by the procedures

The table is §3.17: `AuditTrail_Id`, `AuditTrail_EventUTC` (defaulted, UTC, never passed by anyone),
`User_Id`, `AuditTrail_Action`, `AuditTrail_Category`, `AuditTrail_Summary`.

**Nineteen stored procedures write to it, and nothing else does.** No controller inserts a row; no
`SqlData` method inserts a row; there is no `spAuditTrails_Insert`. Each of the nineteen ends with an
`INSERT INTO [dbo].[AuditTrails]` of its own, inside the same statement batch as the write it is recording,
so the audit row and the change it describes commit or roll back together.

```
spBranch_Insert              spPatientAppointment_Insert       spStaff_Insert
spBranch_Update              spPatientAppointment_Update       spStaff_Update
spBranch_Delete              spPatientAppointment_Delete       spStaff_Delete
spPatientBasic_Insert        spPatientAppointment_UpdateStatus spStaffDocument_Insert
spPatientBasic_Update        spPatientDocument_Insert          spStaffDocument_Delete
spPatient_DeleteCascade      spPatientDocument_Delete          spStaffSlots_CreateRange
                                                               spStaffSlots_Delete
```

**What the three text columns hold:**

- **`AuditTrail_Action`** — `INSERT`, `UPDATE` or `DELETE`, hard-coded as a literal in each procedure. The
  column is a `VARCHAR(20)` with no constraint, so this is a convention the writers happen to keep, not a
  guarantee the schema makes.
- **`AuditTrail_Category`** — the **table** that was written: `Branch`, `Staff`, `StaffSlots`,
  `StaffDocument`, `PatientBasic`, `PatientAppointment`, `PatientDocument`. Not the screen, not the feature.
  Seven categories for nineteen procedures.
- **`AuditTrail_Summary`** — a `CONCAT`ed sentence built in SQL from the procedure's own parameters, naming
  the row and the fields worth seeing later: `'Created Appointment: PatientAppointment_ID=9;
  Patient_ID=PAT-000001; Date=2026-09-02; Time=…'`. It is **denormalized prose**, capped at 500 characters,
  and it is the only record of the *values* involved — the table stores no before/after images.

**Three consequences of that design worth knowing before relying on it:**

- **It records that a row changed, not what it changed from.** There is no old-value column and no diff.
- **`spStaffSlots_CreateRange` writes ONE row for a range**, not one per slot created — its summary carries
  the from/to dates and the counts. Row counts in this table are not change counts.
- **Reads are not recorded here at all.** No procedure writes an audit row for a `SELECT`, which is exactly
  why the Documents search is audited by the *application* instead (§4.11).

#### 🔴 The actor is now passed EXPLICITLY by `SqlData`

**This is the single most important sentence in this section.** `dbo.AuditTrails.User_Id` comes from each
procedure's `@User_ID INT = NULL` parameter, written as `ISNULL(@User_ID, 0)`. Historically **nobody passed
it**: `DatabaseHelper` queried `sys.parameters` before every command, asked *"does this procedure declare
`@User_ID`?"*, and appended the caller's `ClaimTypes.NameIdentifier` value if it did. The actor appeared by
magic and no controller knew it existed.

**Dapper has no such hook.** `connection.ExecuteAsync("dbo.spBranch_Delete", new { … })` sends the
properties of that anonymous object and nothing else. So `SqlData` passes the actor itself, once per call
site, in the open:

```csharp
// spBranch_Delete declares @User_ID INT = NULL for its dbo.AuditTrails row: the ACTOR, not a target.
await connection.ExecuteAsync(
    "dbo.spBranch_Delete",
    new { Branch_ID = branchId, User_ID = _databaseHelper.CurrentUserId },
    commandType: CommandType.StoredProcedure);
```

**What breaks when a future method forgets it: nothing visible.** Because all nineteen declare a **default**,
the parameter is optional — the procedure runs, the write succeeds, the page reports success, the build is
clean, no exception is thrown and no log line is written. The only trace is `AuditTrails.User_Id = 0`, and
on `/AuditTrails` that renders as a blank name in a row that otherwise looks perfectly normal. **You find
out the day you need the audit trail, about the period you no longer have.**

Worse, a `0` is indistinguishable *on the page* from a real actor whose account has since been deleted —
both show a blank name. The id is the tell: `0` is the bug, anything else is a departed user (§3.17).

**How to check.** After any change that touches one of the nineteen:

```bash
sqlcmd -S localhost -d CRC_DB -E -C -Q "SELECT TOP 5 AuditTrail_Id, User_Id, AuditTrail_Action, AuditTrail_Category, AuditTrail_Summary FROM dbo.AuditTrails ORDER BY AuditTrail_Id DESC"
```

`User_Id` must be the logged-in user's id. And as a standing health check over the whole table:

```sql
SELECT COUNT(*) FROM dbo.AuditTrails WHERE User_Id IS NULL OR User_Id = 0;   -- must be 0
```

*Measured on the local `CRC_DB` at the end of the migration, over 124 accumulated rows: **zero**
unattributed rows. Every row names a real `dbo.Users` identity. The last sweep drove one write of each
kind — a branch, a staff member, a patient, an appointment, a staff document, a patient document, a slot
range — then the matching deletes, as two different signed-in users, and **every resulting row carried the
id of the user who actually performed it**: `1` for the SUPERUSER's writes and `7` for the two appointment
status changes an ADMIN made. Not one `0`.* That is the evidence that the explicit-actor mechanism works;
re-run the query rather than trusting this paragraph, because the number it protects only ever goes wrong
in the future.

The five `spUsers_*` procedures that declare `@User_ID INT` **without** a default are the other kind — a
target user row, not an actor — and none of them writes to this table at all. §0.1 has both lists.

### 9.2 Channel two — the Serilog files

One pipeline, configured in `CRC.Web/Program.cs` (lines ~17-41), with **two file sinks split by a single
property**:

| | `Logs/audit-*.log` | `Logs/app-*.log` |
|---|---|---|
| Written by | `CRC.Web/Infrastructure/AuditLog.cs` | every `ILogger<T>`, plus `UseSerilogRequestLogging()` |
| Selected by | `Filter.ByIncludingOnly(e => e.Properties.ContainsKey("AuditChannel"))` | `Filter.ByExcluding(…)` — the same key |
| Rolling | daily | daily |
| **Retention** | **365 files** | **31 files** |
| Template | timestamp, `[Cid:]`, `[User:]`, `[Ip:]`, message | the same **plus `[{Level:u3}]` and `{Exception}`** |

`AuditLog` is a static class holding one `Serilog.ILogger` built as `Log.ForContext("AuditChannel", true)`.
**That property is the entire routing mechanism**: everything written through `AuditLog` carries it and lands
in the audit file; everything else lacks it and lands in the app file. There is no second logger
configuration, no category filter and no level filter doing this work — which also means **writing
`_logger.LogInformation("AUDIT …")` by hand does not put a line on the audit channel**, it just puts the
word "AUDIT" in `app-*.log`. Use `AuditLog`.

A **console sink** sits outside the split and receives both.

**What goes down the audit channel** — 24 methods, in five groups, at `Information` for normal events and
`Warning` for the ones a reader should stop at:

| Group | Methods | Level |
|---|---|---|
| Authentication | `LoginSucceeded`, `Logout` | Information |
| | `LoginFailed`, `LoginLockoutTriggered`, `LoginAttemptWhileLocked`, `LoginRateLimited` | **Warning** |
| Account administration | `AccountUnlocked` | Information |
| Appointments and slots | `AppointmentCreated`, `AppointmentUpdated`, `StaffSlotRangeCreated` | Information |
| | `AppointmentDeleted`, `StaffSlotDeleted` | **Warning** |
| Staff records | `StaffCreated`, `StaffUpdated` / `StaffDeleted` | Information / **Warning** |
| Documents | `*DocumentUploaded`, `*DocumentDownloaded`, `DocumentSearched` | Information |
| | `*DocumentDeleted`, `*DocumentsPurged` | **Warning** |

**The document methods are the ones the database cannot replace.** A download mints a five-minute read SAS
and the bytes are then fetched from storage directly, where the application can no longer see them — so
`PatientDocumentDownloaded` / `StaffDocumentDownloaded`, written **before** the URL leaves the server, is the
only record that someone was handed access to a patient's file. Nothing writes a `dbo.AuditTrails` row for a
read.

#### `CorrelationIdMiddleware` and the enrichers

`CRC.Web/Infrastructure/CorrelationIdMiddleware.cs` runs on every request and does four things: it takes the
inbound `X-Correlation-ID` header **or mints a `Guid.NewGuid().ToString("N")`**, stores it in
`HttpContext.Items["CorrelationId"]`, **echoes it back on the response**, and pushes three properties onto
Serilog's `LogContext` for the duration of the request:

```
CorrelationId   the id above
UserName        context.User?.Identity?.Name ?? "anonymous"
RemoteIp        context.Connection.RemoteIpAddress
```

`Enrich.FromLogContext()` is what carries those onto every event; `Enrich.WithMachineName()` adds the host
(available to any template that asks for it — neither of the two file templates does). `ErrorResponse.ForUser`
returns the same id to the browser as `correlationId`, which is how a user's complaint becomes a `grep` over
`app-*.log`.

**Two consequences of the pipeline order, both real and both observable in the files:**

- **`UseMiddleware<CorrelationIdMiddleware>()` runs AFTER `UseAuthentication()`**, so `UserName` is the
  signed-in user for ordinary requests — but **a successful login logs `[User:anonymous]`**, because the
  cookie is issued in the response of the very request being logged. Every `AUDIT Login succeeded` line
  says `anonymous`; the `Username=` inside the message is the one to read.
- **`UseRateLimiter()` runs BEFORE it**, so a 429 rejection is logged outside the `LogContext` scope
  entirely: `AUDIT Login rate limited` lines carry **empty `[Cid:]` and `[User:]`**. Verified —
  `2026-08-10 15:29:01 [Cid:] [User:] [Ip:::1] AUDIT Login rate limited. RemoteIp=::1`. The `[Ip:]` is
  filled only because that message happens to carry its own `RemoteIp` property, which the output template
  picks up by name.

`UseSerilogRequestLogging()` adds one `HTTP {Method} {Path} responded {StatusCode} in {Elapsed} ms` line per
request to `app-*.log`. `MinimumLevel` is `Information` with `Microsoft.AspNetCore` overridden to `Warning`,
which is what keeps the framework's own per-request chatter out.

#### 🔴 FORTY CATCH BLOCKS LOG NOTHING, AND THAT IS THE OPERATIONAL CHANNEL'S BIGGEST HOLE

Counted across `CRC.Web/Controllers/`: **40 `catch` blocks return a user-facing message without calling
`_logger`**, spread over eleven of the sixteen controllers.

| Controller | Unlogged catches |
|---|---:|
| `PatientController` | 13 |
| `StaffPatientController` | 8 |
| `DashboardController` | 4 |
| `StaffDashboardController`, `SettingsController` | 3 each |
| `AuditTrailsController`, `AppointmentController`, `AdminDashboardController` | 2 each |
| `StaffController`, `MyProfileStaffController`, `BranchController` | 1 each |

**What that costs is precise, not vague.** Those actions answer `{ success: false, message: "…" }` with
**no `correlationId`**, because `ErrorResponse.ForUser` is what mints one — so a user reporting "the branch
list is empty" hands you nothing to `grep` for, and `app-*.log` contains no record that anything failed.
The reads are the worse half: a failing `GET` renders an empty table, which looks exactly like "no data".

The pattern to follow instead is the one `SaveBranch`, `SaveStaff` and the three clinical writes already
use: `catch (SqlException ex)` then `catch (Exception ex)`, both `_logger.LogError(ex, "…", args)`, both
returning `Ok(ErrorResponse.ForUser(HttpContext, "…"))`. **Every new action must do that** (§11). The forty
are recorded here rather than fixed because changing them alters what a caught failure returns, which is a
behaviour change and the owner's call — but a new one is a defect, not a precedent.

#### 🔴 Secrets are never logged, and the rule is wider than passwords

**No password, password hash, antiforgery token, session cookie, SAS URL or connection string is ever
written to either channel, and none is ever to be added.** The places this is deliberately upheld:

- **`AuditLog.LoginFailed(context, username, reason)`** takes the username and a *reason string* — never the
  attempted password. `AccountController` also returns one generic `"Invalid username or password."` to the
  browser for every failure path, so the log is more specific than the response, never less.
- **`ChangePassword` and `RegisterUser` log the outcome and the username**, never the value or the hash.
- **The document methods log `BlobName`** — the key inside the private container — but **never the SAS URL**,
  which carries a signature that is itself a bearer credential. `BlobName` is useless without one.
- **Exceptions go to `app-*.log` through `_logger.LogError(ex, …)` and never to the browser**; the user gets
  a fixed message plus the correlation id (§0).

`CRC.Web/Logs/*.log` is git-ignored. On Azure the files live on the App Service disk and **a publish does not
delete them** — "Remove additional files at destination" is deliberately off precisely so that a deploy
cannot erase a year of audit history.

### 9.3 Which channel answers which question

| Question | Channel |
|---|---|
| Who changed this branch, and when? | `dbo.AuditTrails` — filter by category and actor on `/AuditTrails` |
| Who *looked at* this patient's documents? | `audit-*.log` — the table records no reads |
| Which account tried to log in eleven times from one address? | `audit-*.log` — never reaches the database |
| What did the summary of that delete say? | `dbo.AuditTrails.AuditTrail_Summary` |
| Why did this request 500 for that user? | `app-*.log`, by the correlation id the user was shown |
| Is the actor mechanism still working? | `SELECT COUNT(*) … WHERE User_Id = 0` — §9.1 |

**What neither channel has:** no retention or archiving job for the table (§3.17), no alerting, no
aggregation, and no cross-channel correlation — an `AuditTrails` row carries no correlation id, so tying a
database row to the request that produced it means matching on time and actor by hand.

---

## 10. Folder structure / file map

**Three projects in one solution (`CRC_Portal.slnx`), and the dependency runs one way only:**
`CRC.Web → CRC.Data`. `CRC.Database` is referenced by neither — it is a classic SSDT project that produces
a `.dacpac`, not an assembly, and is deployed by hand.

🔴 **`CRC.Data` HAS NO REFERENCE TO `CRC.Web` AND MUST NOT GAIN ONE.** That single rule explains several
shapes that otherwise look like awkwardness: why `IDocumentStorage` lives in `CRC.Web` and the blob work
stays in controllers, why `SaveStaffWithDocumentsAsync` takes a `Func<>` callback instead of an uploader,
and why nothing in `CRC.Data` knows that HTTP or Azure Storage exist (§6.6, §12).

```
CRC_Portal/
  CoreFlow.md                       THIS FILE — the specification. Read §11 before adding anything.
  DapperLayerPlan.md                the finished 11-prompt plan that produced the Dapper layer. HISTORY:
                                    it records how the layer was built and in what order. Where it and
                                    CoreFlow.md disagree, CoreFlow.md is right (it was written last).
  DOCUMENTSTORAGE.md                AUTHORITATIVE on blob storage — container, key layout, SAS, Azurite,
                                    the two DocumentStorage settings. §8 defers to it.
  DocumentStoragePlan.md            the finished plan that moved documents off wwwroot/uploads into Blob.
  SEEDING.md                        AUTHORITATIVE on what a published database contains, and the source
                                    of the bootstrap SUPERUSER / ChangeMe!123 warning.
  Nucentra_Azure_Deployment_Guide.md  the click-by-click Azure deployment runbook. The owner performs
                                    every Azure action by hand from this file; nothing automates it.
  Export-NucentraPortal.ps1         packages the repo for hand-off.
  CRC_Portal.slnx                   the solution.

CRC.Data/            net10.0 · Dapper 2.1.79 · Microsoft.Data.SqlClient 6.1.3 · Nullable + ImplicitUsings on
  Data/
    DatabaseHelper.cs               connection factory + CurrentUserId. TWO MEMBERS (§6.5). Its class
                                    comment records the sys.parameters auto-injection that was deleted.
    IDatabaseData.cs                THE CONTRACT AND THE DOCUMENTATION. 102 methods, one per procedure
                                    (plus the two transactions), each with a // comment naming the
                                    procedure it calls, grouped under `// ----- Area -----` banners.
                                    Read this to find out WHAT the layer does.
    SqlData.cs                      THE ONLY PLACE IN THE SOLUTION THAT NAMES A STORED PROCEDURE.
                                    Same banners, same order, so the two files read side by side.
                                    Read this to find out HOW. 3 private helpers (§3.1, §7.8) + the
                                    3 shared staff writers used by the transaction and its non-
                                    transactional twins.
  Models/                           53 POCOs, one type per file (§6.3): row models, *SaveInput write
                                    models, *Result models, and the AppointmentSaveFailure enum.
  Database/
    Migrations/                     the twelve LU_* seed CSVs + MigrationQuery.txt. DATA, not code —
                                    this folder is why CRC.Data has both a Data/ and a Database/.

CRC.Database/        classic SSDT .sqlproj — MSBuild only, `dotnet build` CANNOT build it (§11)
  dbo/Tables/                       28 .sql files, one per table (§3). FIVE foreign keys in total.
  Stored Procedures/                104 .sql files in 30 per-feature subfolders:
    LU_*/                 (14)      the twelve lookup tables' reads; LU_LOCATION has three
    Branch/               (6)   Users/              (9)   Staff/               (6, incl. _GetPerformance)
    StaffDocument/        (6)   StaffDocumentSettings/ (3) StaffSlots/         (6)
    PatientBasic/         (6)   PatientDocument/    (7)   PatientDocumentSettings/ (3)
    PatientAppointment/   (10)  PatientJourney/     (3)   PatientAssessment/   (3)
    PatientColonoscopy/   (3)   PatientFollowUp/    (3)   PatientTracker/      (5)
    Dashboard/            (4)   StaffDashboard/     (3)   AuditTrails/         (4)
  Scripts/
    Script.PostDeployment.sql       the one <PostDeploy> item; :r-includes the three seeds in order
    Seed_Lookups.sql                the eleven small LU_* tables, guarded per row
    Seed_Location.sql               LU_LOCATION — 3,242 generated rows, guarded whole-table
    Seed_Users.sql                  the bootstrap SUPERUSER, guarded on Username
    Tools/New-SeedLocation.ps1      regenerates Seed_Location.sql from the CSV; never runs on publish
  CRC.Database.sqlproj              🔴 EVERY .sql above needs a <Build Include="…" /> HERE. An
                                    unregistered file builds locally and is SILENTLY ABSENT from the
                                    .dacpac — the page then fails only against a freshly published DB.

CRC.Web/             net10.0 MVC · Serilog · Azure.Storage.Blobs
  Program.cs                        the whole composition root, top to bottom: Serilog's two-sink split,
                                    the options binding, global AuthorizeFilter +
                                    AutoValidateAntiforgeryToken, DI (DatabaseHelper, IDatabaseData →
                                    SqlData, IDocumentStorage), cookie auth, the login rate limiter, the
                                    five policies (§2.3), the /uploads 404 branch, and the default route
                                    {controller=Account}/{action=Login}/{id?}.
  Controllers/                      16 controllers. AccountController.cs sits at the root; every other
    {Feature}/                      one lives in a per-feature subfolder — Branch/, Staff/ (three
                                    controllers), Patient/, StaffPatient/, Appointment/, Dashboard/,
                                    AdminDashboard/, StaffDashboard/, PatientTracker/, Documents/,
                                    Settings/, AuditTrails/, MyProfileStaff/. Request DTOs are NESTED
                                    CLASSES inside their controller, not files in Models/ (§11).
  Models/                           only four files, and NONE of them is a request DTO:
                                    ErrorViewModel.cs + the three IOptions classes bound in Program.cs
                                    (PasswordPolicyOptions, SessionTimeoutOptions, LoginLockoutOptions).
  Infrastructure/
    AuditLog.cs                     the security channel — 24 static methods (§9.2). Routing is the
                                    "AuditChannel" property; _logger.LogInformation("AUDIT…") does NOT
                                    reach audit-*.log.
    ErrorResponse.cs                ForUser / ForView — { success=false, message, correlationId }
    CorrelationIdMiddleware.cs      mints/echoes X-Correlation-ID, pushes CorrelationId/UserName/RemoteIp
    DocumentValidation.cs           THE ONLY place upload rules live: extensions AND content types, the
                                    20 MB cap, SafeFileName (255), BuildBlobName
    StaffAccessExtensions.cs        User.CanAccessStaff(staffId) — the ownership check (§4.4, §4.5)
  Services/
    IDocumentStorage.cs             UploadAsync / GetReadSasUrl / DeleteAsync
    AzureBlobDocumentStorage.cs     the only code that talks to Blob storage. Singleton.
    DocumentStorageOptions.cs       bound from the DocumentStorage config section
  Views/                            30 .cshtml in 15 folders, one per screen, plus Shared/ (_Layout and
                                    the error pages). StaffPatient/Templates/ holds the three journey
                                    partials GetJourneyTemplate returns as HTML rather than JSON (§4.9).
  wwwroot/js/                       59 files. {area}/ per screen — branch/, staff/, patient/,
                                    staffPatient/ (+templates/), account/, appointment/, dashboard/,
                                    adminDashboard/, staffDashboard/, patientTracker/, documents/,
                                    settings/, auditTrails/, myprofileStaff/ — plus the shared
                                    builders/, classes/, common/, functions/, helpers/.
  Logs/                             app-*.log (31 days) and audit-*.log (365 days), git-ignored (§9.2).
                                    On Azure a publish deliberately does NOT delete them.
  Properties/launchSettings.json    🔴 use the `https` profile (https://localhost:7276). The __Host-CSRF
                                    antiforgery cookie requires HTTPS, so every POST 400s over http.
  appsettings.json                  ConnectionStrings:CRC_DB, DocumentStorage, Account:{Password,
                                    SessionTimeout,LoginLockout} — the lockout and password thresholds
                                    are config, not database policy (§2.6).
```

**Two folder facts that surprise people, both deliberate:**

- **`CRC.Data/Data/` (code) and `CRC.Data/Database/Migrations/` (seed CSVs) both exist.** They are not
  duplicates and neither is a leftover.
- **Request DTOs are nested classes inside controllers**, so `CRC.Web/Models/` holds four files and none of
  them is one. Follow the local convention; do not start a parallel `Models/{Feature}/` tree.

---

## 11. End-of-feature checklist

**Work top to bottom. The order is the layering of §0** — a procedure before a method, a method before an
action, an action before a script, a script before a view — and it is chosen so that each step can be
proved before the next one depends on it.

### 11.1 The database

- [ ] **Write `sp{Table}_{What}.sql` in the right per-feature subfolder** of
      `CRC.Database/Stored Procedures/`. Match the neighbours: `SET NOCOUNT ON;` first, a header comment
      saying what it does and — if it takes `@User_ID` — **which kind**, `@PascalCase` parameters,
      `[bracketed]` identifiers.
- [ ] 🔴 **Register it in `CRC.Database.sqlproj`** as
      `<Build Include="Stored Procedures\{Folder}\{File}.sql" />`, in the existing block, reordering
      nothing. **An unregistered file builds locally and is silently absent from the `.dacpac`** — the
      failure surfaces only against a freshly published database, on somebody else's machine.
- [ ] **Decide `@User_ID` deliberately** (§0.1). Writing a row somebody should be accountable for? Declare
      `@User_ID INT = NULL`, `INSERT` the `dbo.AuditTrails` row with `ISNULL(@User_ID, 0)`, and guard it
      with `IF @@ROWCOUNT > 0` if the write can miss. Operating *on* a user row? `@User_ID INT` with **no
      default** — and then it is an ordinary argument that belongs in the method signature.
- [ ] **Decide how it answers, and write it down in the header comment.** A trailing `SELECT` is preferred
      over an `OUTPUT` parameter, because a result set maps onto a model **by name** and an `OUTPUT`
      parameter needs `DynamicParameters` (§5.8). If the procedure must emit several result sets, emit a
      **stable number of them on every path** — `spStaff_Delete` `SELECT TOP 0`s a placeholder grid on its
      two early returns precisely so a caller can read grid 2 unconditionally (§5.4).
- [ ] **If you are editing an existing `.sql`, be additive.** Add a trailing `SELECT`, add a column alias,
      add an optional parameter with a default. **Never** remove or rename a parameter or an output column,
      and never change existing behaviour (§12).
- [ ] **Build it:** MSBuild, **not `dotnet build`** —
      `"C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /t:Rebuild /p:Configuration=Debug /p:VisualStudioVersion=18.0`.
      Pass condition: `0 Error(s)` and **exactly two** `SQL71502` warnings, both in
      `spStaffSlots_CreateRange.sql` (§3.7). **Warnings only appear on `/t:Rebuild`** — an incremental
      build prints none and proves nothing.
- [ ] **Publish it to your local `CRC_DB`** before touching C#, or the method you are about to write has
      nothing to run against.

### 11.2 The data layer

- [ ] **Add ONE `IDatabaseData` method**, named for what it does (`GetActiveBranchesAsync`), never for the
      procedure (`SpBranchListActiveAsync`), under the right `// ----- Area -----` banner.
- [ ] **Write the `//` comment above it** — this is where the layer is documented. Name the procedure, say
      what it is for, and say anything surprising about the result: an empty set that means "pass", a grid
      order that is the contract, a column that is a string where you would expect a date.
- [ ] **Implement it in `SqlData.cs` at the matching position**, under the same banner in the same order.
      Nothing enforces that the two files stay parallel; it is on you, and it is what lets them be read
      side by side.
- [ ] **`commandType: CommandType.StoredProcedure`, an anonymous parameter object, and no inline SQL** —
      not a `SELECT`, not a one-line `UPDATE`, not "just for this" (§12).
- [ ] **Pick the Dapper verb from what the procedure GUARANTEES, not from what today's caller wants.**
      `QuerySingleOrDefaultAsync` for a row that may not exist; `QuerySingleAsync` only when exactly one is
      certain; `QueryAsync` for a set; `ExecuteAsync` for a write with no result set; `QueryMultipleAsync`
      for several grids, read **in the order the procedure emits them**.
- [ ] 🔴 **Pass `@User_ID` explicitly if the procedure declares the actor kind** —
      `User_ID = _databaseHelper.CurrentUserId` — with a comment saying it is the actor. **Omitting it
      throws nothing, fails no page, and writes `AuditTrails.User_Id = 0`.** If it is one of the five
      TARGET procedures, it is a method argument instead and must never come from `CurrentUserId`.
- [ ] **Add the model to `CRC.Data/Models/`**, one type per file, named for the data. **Type a property
      nullable if the column is nullable OR the join is a `LEFT JOIN` OR it is an aggregate over a possibly
      empty set** — Dapper *throws* mapping `NULL` onto a non-nullable `int`/`bool`/`DateTime`, and that
      turns "this clinician has done nothing yet" into a 500 (§4.6).
- [ ] **Do not share a model between two procedures unless they select the same columns.** A strict subset
      is not the same shape; sharing hides the missing ones as silent defaults (§5.3). **Reuse the shape,
      never the name.**
- [ ] **If you are tempted to make one method call two procedures, don't** (§12). There are exactly two
      transactional units of work and adding a third needs a reason written down beside it.

### 11.3 The controller

- [ ] **Add the action to the existing controller for that screen**, or a new one in
      `Controllers/{Feature}/`. Inject `IDatabaseData` — never `DatabaseHelper`.
- [ ] 🔴 **State the policy explicitly**: `[Authorize(Policy = "…")]` on the class, or per action if the
      screen genuinely mixes levels (only `StaffController` and `StaffPatientController` do). The global
      `AuthorizeFilter` means a forgotten attribute fails **closed**, which is right — but "authenticated"
      is rarely the level you meant.
- [ ] **Antiforgery is global.** A POST needs no attribute, and a caller needs the `X-CSRF-TOKEN` header.
- [ ] **Return `Ok(new { success, message, … })` with camelCase properties** — or a bare array if you are
      extending a list endpoint that already returns one. **Build the anonymous object by hand and map the
      model into it**; never serialize a model directly (§12).
- [ ] **Coerce nulls the way the neighbouring properties do, and check what the page expects.** `""` for
      anything assigned straight into an input; `null` only where a `.js` file tests truthiness to make a
      decision (`dischargeTypeId` is the example worth reading — §4.7).
- [ ] **Catch, log, return** — `catch (SqlException ex)` then `catch (Exception ex)`, both
      `_logger.LogError(ex, "…", args)`, both `Ok(ErrorResponse.ForUser(HttpContext, "…"))`. 🔴 **A new
      unlogged catch is a defect**, whatever the forty existing ones do (§9.2). Never return an exception
      message to the browser.
- [ ] **Add `AuditLog.*` for every write**, and **only after the data call has returned successfully** —
      never inside a flow that might roll back (§6.6). Log the outcome, never a password, a hash, a token
      or a SAS URL (§9.2).
- [ ] **Put the request DTO in a nested class inside the controller**, like every other one.
- [ ] **Keep business decisions out of `SqlData`.** A scoping predicate such as the `StaffId` claim is
      resolved in the controller and passed as an argument; only the audit actor comes from the data layer
      (§4.13). Moving that boundary is a security change, not a refactor.

### 11.4 The front end

- [ ] **Add the JS in `wwwroot/js/{area}/`**, sending `X-CSRF-TOKEN` on every POST and reading the exact
      property names the action returns.
- [ ] **Add or extend the view in `Views/{Area}/`**, loading the script and passing ids through `ViewData`
      / `ViewBag` rather than baking them into the script.
- [ ] **Remember `Forbid()` is a 302, not a 403** (§4.5) — a `fetch` follows it to an HTML page, so
      `response.ok` is `true` and `response.json()` is what throws.

### 11.5 Prove it, then write it down

- [ ] **`dotnet build CRC.Web/CRC.Web.csproj`** — 0 errors, 0 new warnings. (It builds `CRC.Data` too.)
- [ ] 🔴 **RUN THE SITE AND DRIVE THE ENDPOINT.** A Dapper mapping mistake compiles perfectly: a column the
      model does not match comes back as the property's default, with **no exception and nothing in a
      log**. Nothing but a running request catches it. Use the `https` profile.
- [ ] **Check `CRC.Web/Logs/app-*.log` afterwards, not just the page.** An empty table with a logged
      exception behind it is the characteristic shape of this failure.
- [ ] 🔴 **If the write touches an actor procedure, check the audit row by hand:**
      ```bash
      sqlcmd -S localhost -d CRC_DB -E -C -Q "SELECT TOP 5 AuditTrail_Id, User_Id, AuditTrail_Action, AuditTrail_Category, AuditTrail_Summary FROM dbo.AuditTrails ORDER BY AuditTrail_Id DESC"
      ```
      `User_Id` must be the signed-in user's id. **`0` means the actor parameter was dropped.**
- [ ] **Update `CoreFlow.md`** — §3 for a table or a column, §4 for an endpoint and **its exact JSON**, §5
      for the procedure and its `@User_ID` kind. Append a sub-heading; renumber nothing. If the thing you
      built has a surprise in it, that surprise is the most valuable sentence you will write.
- [ ] **If you changed anything §12 locks, stop and re-open the decision deliberately** — with the owner,
      and in writing — rather than in passing.

---

## 12. Decisions locked

Each of these was **decided deliberately**, and each looks, from one angle, like something worth tidying.
They are recorded here so that the next person to have that idea finds the reason before they act on it.
None is a law of nature — but re-opening one is a decision with a cost, taken with the owner, not a
refactor taken in passing.

**1 — One `SqlData` method per stored procedure. There are exactly two exceptions and they are named.**
`SaveStaffWithDocumentsAsync` and `SaveAppointmentAsync` (§6.6, §6.7) each run several procedures inside one
`SqlTransaction`, and each exists because a single business fact would otherwise be able to land half-way:
a staff row without the documents the mandatory rule requires, or an appointment holding an hour somebody
else took while the check was in flight. **Do not write a third without adding it to §6.6 in the same
breath as the code.** Two procedures behind one method is a claim that they are one operation, and every
such claim is a place a reader can no longer tell from the interface whether a partial write is possible.

**2 — No inline SQL, anywhere, ever.** Not a `SELECT`, not a one-line `UPDATE`, not "just for this one
report". Every database call in the product is `commandType: CommandType.StoredProcedure` from
`CRC.Data/Data/SqlData.cs`, which is **the only file in the solution that names a procedure** — and that is
the property worth protecting: grepping for `"sp` answers "who calls this?" completely, and a procedure's
signature changing is a compiler error rather than a user's bad afternoon. A new query is a new `.sql` file,
registered in the `.sqlproj`, plus a new interface method (§11).

**3 — 🔴 `@User_ID` IS ALWAYS PASSED EXPLICITLY. THE `sys.parameters` AUTO-INJECTION IS GONE AND IS NOT
COMING BACK.** `DatabaseHelper` used to ask the catalogue whether each procedure declared `@User_ID` and
silently append the caller's claim if it did; that machinery was **deleted** with the rest of its ADO
surface (§6.5). Re-introducing it would not merely be redundant — a generic injector keyed on a parameter
*name* cannot see that the name means two different things here:

> **THE ACTOR — 19 procedures, declared `@User_ID INT = NULL`.** Who performed the write, for the
> `dbo.AuditTrails` row. Never in a method signature; `SqlData` supplies it from
> `DatabaseHelper.CurrentUserId`:
>
> ```
> spBranch_Insert              spPatientAppointment_Insert       spStaff_Insert
> spBranch_Update              spPatientAppointment_Update       spStaff_Update
> spBranch_Delete              spPatientAppointment_Delete       spStaff_Delete
> spPatientBasic_Insert        spPatientAppointment_UpdateStatus spStaffDocument_Insert
> spPatientBasic_Update        spPatientDocument_Insert          spStaffDocument_Delete
> spPatient_DeleteCascade      spPatientDocument_Delete          spStaffSlots_CreateRange
>                                                                spStaffSlots_Delete
> ```
>
> **A TARGET USER ROW — 5 procedures, declared `@User_ID INT` with no default.** Which user the procedure
> operates *on*. An ordinary argument, in the signature, from the caller:
>
> ```
> spUsers_GetById        spUsers_Unlock          spUsers_UpdatePassword
> spUsers_ResetFailedLogins                      spUsers_UpdateLastLogin
> ```

**The default is the tell, and it is a rule you can apply to a procedure you have never seen.** Confusing
them costs differently in each direction: dropping an actor writes `AuditTrails.User_Id = 0` and breaks
nothing visible, while auto-filling `spUsers_Unlock`'s target would unlock the administrator's own account,
leave the locked user locked, and report success. §0.1 and §9.1 carry the full argument and the health
check.

**4 — Controllers map data models into hand-built anonymous objects. A model is never serialized
directly.** Yes, it is boilerplate. It is also what keeps the JSON contract — which 59 JavaScript files
read by property name — **independent of the data layer's types**: renaming a model property is a
compile-time change with no effect on the wire, and a procedure gaining a column does not silently gain a
JSON field. It is equally what made this migration verifiable rather than hopeful, because every endpoint's
payload could be diffed byte-for-byte before and after. **The three journey detail reads are the one
exception** — they return `IReadOnlyDictionary<string, object?>` because the browser must receive the
procedure's raw column names, and §7.8 explains why a POCO there would break three clinical forms while
returning `200`.

**5 — Authorization is one integer and five policies. There is no permission-key model, and adding one is
a project.** `dbo.Users.User_Type` (1 SUPERUSER / 2 ADMIN / 3 STAFF) becomes a `UserType` claim, checked by
five `RequireClaim` policies (§2). **There is no `dbo.Permissions`, no `dbo.Roles`, no `dbo.RolePermissions`,
no `dbo.UserRoles`, and no equivalent of HEART's `HeartPermissionKeys.cs`** — unlike the sibling portal,
which guards endpoints by key so that new roles need no endpoint changes. Adding a fourth kind of user here
means a policy in `Program.cs` and an attribute on every action that should admit it: a code change and a
redeploy, not an admin screen. Do not go looking for the tables and do not "restore" them.

**6 — 🔴 `CRC.Data` NEVER REFERENCES `CRC.Web`.** The dependency runs one way, and that is why
`IDocumentStorage` lives in `CRC.Web` and the blob work stays in controllers. It is not tidiness: it is what
keeps the data layer from knowing that HTTP, Azure Storage or a user's browser exist. The visible cost is
`SaveStaffWithDocumentsAsync`'s `Func<string, Task<IReadOnlyList<StaffDocumentInput>>>` callback — needed
because a new staff member's blob key contains a `Staff_ID` that does not exist until `spStaff_Insert` has
run *inside* the transaction (§6.6). The two rejected alternatives, pre-generating the id in C# and giving
the data layer an uploader of its own, are recorded there with their reasons.

**7 — Stored procedures were NOT renamed. nucentra keeps `sp{Table}_{What}`.** HEART uses
`sp_{Table}_{What}`, with the underscore, and the Dapper layer was copied from HEART in *shape* only. Not
one of the 104 names changed during the migration, because a rename touches the `.sql`, the `.sqlproj`, the
deployed database and `SqlData` at once, buys nothing at runtime, and would have made the before/after
verification that justified the whole exercise impossible to read. **New procedures follow nucentra's
convention**, not HEART's — and the same goes for the rest of the house style: block-scoped namespaces,
`Ok(new { … })` rather than `Json(...)`, `ErrorResponse.ForUser` for caught exceptions.

**8 — The two `SQL71502` warnings are the baseline, not a defect.** A `CRC.Database` rebuild reports
`Build succeeded`, `0 Error(s)` and **exactly two** warnings, both
`[dbo].[spStaffSlots_CreateRange] has an unresolved reference to object [sys].[all_objects]`, at lines 46
and 52. The procedure uses `sys.all_objects` as a row generator because nucentra has no numbers table; the
reference is valid at runtime and unresolvable in the project model. **Do not "fix" them** — a master
database reference adds a build dependency for a cosmetic gain, and rewriting the row generator changes a
procedure for no functional reason. 🔴 **If the count is anything other than two, something you did caused
it** (§3.7).

**9 — `.sql` edits are additive only.** Add a trailing `SELECT`, add a column alias, add an optional
parameter with a default. Never remove or rename an existing parameter or output column, and never change
existing behaviour. **One `.sql` was edited during the whole Dapper migration** —
`spUsers_RegisterFailedLogin` gained a trailing `SELECT` of its three `OUTPUT` values so the result could
map onto a model by name (§5.3) — and it stayed compatible with the caller that was still reading the
`OUTPUT` parameters. Every touched file stays registered in `CRC.Database.sqlproj` with nothing reordered.

**10 — What is written down as broken stays written down, not quietly fixed.** §7.7's two integrity gaps,
§4.4's `SaveStaff` returning `success: false` after committing, §4.10's unaudited Settings screen, §5.9's
non-transactional staff-settings save and §9.2's forty unlogged catches are **real defects, recorded
precisely, deliberately not repaired here** — every one of them is a behaviour change, and a behaviour
change is the owner's call. Fixing one is welcome; fixing one *on the way past something else*, without
saying so, is not. Update the section in the same commit.
