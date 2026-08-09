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
| `AdminOrSuper` | 1, 2 | **class:** `AdminDashboardController`, `AppointmentController`, `PatientController`, `PatientTrackerController` · **action:** most of `StaffController` |
| `AdminOrSuperOrStaff` | 1, 2, 3 | **class:** `MyProfileStaffController`, `StaffScheduleController`, `StaffPerformanceController` · **action:** `StaffController.GetStaffTypes` and most reads in `StaffPatientController` |
| `StaffOnly` | 3 | **class:** `StaffDashboardController` · **action:** the clinical *writes* in `StaffPatientController` (assessment, colonoscopy, follow-up) |
| `AdminOnly` | 2 | 🔴 **nothing.** |

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
(`spStaffDocumentSettings_DeleteByStaffType`, Prompt 8).

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

> 🔴 **§2.3's policy table is wrong about this controller.** It lists
> `StaffController.GetStaffTypes` under `AdminOrSuperOrStaff`. `GetStaffTypes` is `AdminOrSuper`, like
> everything else here; the one `AdminOrSuperOrStaff` action is **`GetStaff`**. The table below is what the
> attributes actually say, read action by action. §2 belongs to Prompt 2 and is not rewritten here — the
> correction is flagged for Prompt 10's consistency pass.

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
// 403, empty body                                     // a STAFF user asking for someone else's id

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
- **`GetStaff` returns 403 with an empty body**, via `Forbid()`, when a STAFF user asks for an id that is
  not their own `StaffId` claim (`CanAccessStaff`). ADMIN and SUPERUSER pass unconditionally. This is the
  **only ownership check in the controller** — every other action is guarded by the policy alone, so an
  ADMIN sees every staff member and a STAFF user cannot reach any of them.
- **`DeleteStaff` is the one action that does not use `ErrorResponse.ForUser`.** Its catch returns a bare
  `{ success = false, message = "An unexpected error occurred." }` with **no `correlationId`**, so a
  user's complaint about a failed delete cannot be tied to a line in `app-*.log`. It *does* log
  (unlike `BranchController.DeleteBranch`, §4.1, which does not). Left exactly as found; Prompt 9 owns the
  logging sweep.
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
five refusal paths in §4.5 and §4.6: the response is
`302 Location: /Account/AccessDenied?ReturnUrl=%2FStaffSchedule%2FDelete`, with an empty body — the cookie
handler's `AccessDeniedPath` (§2.5) turns the forbid into a redirect. §4.4 describes the same `Forbid()` on
`/Staff/GetStaff` as "403 with an empty body"; that is the MVC result, not what goes over the wire.
**Flagged for Prompt 10's consistency pass**; §4.4 belongs to Prompt 3 and is not rewritten here. The
practical consequence is for the `.js`: a `fetch` sees a 302 it follows to an HTML page, so `response.ok` is
`true` and `response.json()` is what fails.

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
> `DeleteAppointment` — is the Appointment tab of the same page, and **Prompt 6 documents it in its own
> sub-section**. The split is not cosmetic: the appointment half owns the hand-rolled `SqlTransaction` that
> becomes `SaveAppointmentAsync` (§6.6), and until Prompt 6 lands **this one class holds both `IDatabaseData
> _data` (the patient half) and `DatabaseHelper _db` (the appointment half)**. That is expected and correct
> for exactly one prompt's worth of time; a comment on the two fields says so.

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
an unreachable path, and the same choice Prompt 3 made for `spStaff_Insert` (§6.6).

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

> This completes the controller §4.7 describes half of. **After Prompt 6, `PatientController` injects
> `IDatabaseData` and nothing else data-related** — the `DatabaseHelper _db` field, its constructor
> parameter and `using System.Data;` are gone, and the file contains no `SqlParameter`, no `DataTable` and
> no `DataRow`. §4.7's note about the class holding both surfaces described a state that lasted exactly
> one prompt.

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

`spUsers_RegisterFailedLogin` is **the only procedure in nucentra with `OUTPUT` parameters**, and the only
`.sql` file this migration has edited. Prompt 2 **appended** one statement to the end of its body:

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
additive**: any caller still using `ParameterDirection.Output` gets byte-identical behaviour and simply
ignores an extra result set. That mattered mid-migration, when `AccountController` was the old code and the
new procedure was already deployed, and it is why the additive-only rule exists at all.

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

`spStaff_GetPerformance` also lives in `Stored Procedures/Staff/` and belongs to Prompt 4.

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

- 🔴 **`spStaffDocument_Delete` IS A SECOND PROCEDURE WITH AN `OUTPUT` PARAMETER, and §5.3 says there is
  only one.** That claim was written in Prompt 2, before this area had been read, and it is wrong:
  `@DeletedBlobName VARCHAR(500) = NULL OUTPUT` carries the blob key of the row that was just deleted, or
  `NULL` when no row matched. The two are not in the same position, though, and the difference is what
  could be done about each: Prompt 2 was permitted to **append** a trailing `SELECT` to
  `spUsers_RegisterFailedLogin`, whereas **Prompt 3 was permitted to touch no `.sql` at all** — and this
  procedure has no result set to append to without changing it. So `SqlData.DeleteStaffDocumentAsync` reads
  the parameter through `DynamicParameters`, in the one place in the data layer that does, with the name
  and the `DbType.AnsiString` written out where a reader can check them against the `.sql`.
- **`spStaffDocument_Insert` does not return the new `StaffDocument_ID`.** It computes it
  (`SCOPE_IDENTITY()`) purely to put it in the `AuditTrails` summary, and then discards it. That is why
  **every `AuditLog.StaffDocumentUploaded` line in the portal records `DocumentId=0`** and identifies the
  row by its blob key instead. The database trail has the id; the Serilog trail does not. Making them agree
  is an additive `.sql` change and a later prompt's call.
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
  rejects a blank `staffId` first — but the capability is there, and Prompt 8's search page is presumably
  what it was written for.
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

### 5.5 Staff slots and staff performance (5 wrapped, 2 deferred)

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

#### 🔴 The two `StaffSlots` procedures that are NOT wrapped, and why

`Stored Procedures/StaffSlots/` holds **six** files. The two missing from the table are
**`spStaffSlots_AssignAppointment`** (`@ApptId INT`, `@StaffSlotIds VARCHAR(MAX)` — a comma-separated list
split with `STRING_SPLIT`, stamping the appointment id onto every named slot) and
**`spStaffSlots_ClearAppointment`** (`@ApptId INT` — clearing it off every slot that carries it).

**They are deliberately absent from `IDatabaseData`, not forgotten.** Neither has a caller of its own:
both are run only from inside `PatientController.SaveAppointment`'s transaction, which reads
`spStaffSlots_List` under a lock, checks that every chosen hour is still free, writes the appointment, and
*then* claims the slots. Publishing them as standalone data-layer methods would offer a second way to change
a slot's booking state — one that is not inside that transaction — and that race is precisely what the
transaction exists to prevent. **Prompt 6 adds them to `SaveAppointmentAsync`**, the second of the two
transactional units of work (§6.6). The banner comment in `IDatabaseData.cs` says the same thing at the
place a future author would otherwise add them.

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
Settings-screen procedures (Prompt 8) but belongs to the discharge flow, so it is documented here.

#### 🔴 `spPatientBasic_Insert` answers through an OUTPUT parameter, and it is the THIRD one that does

**`@NewPatient_ID VARCHAR(100) OUTPUT`. There is no trailing `SELECT`.** That matters because the two other
composed-id inserts in nucentra do the opposite: `spBranch_Insert` ends `SELECT @Branch_ID AS NewBranch_ID`
and `spStaff_Insert` ends `SELECT @Staff_ID AS NewStaff_ID`, so both are a `QuerySingleAsync<string>`
(§5.2, §5.4). Assume the same shape here and `QuerySingleAsync` throws *"Sequence contains no elements"* on
every successful insert. **Read the `.sql` before writing the method; the family resemblance is a trap.**

It also means **§5.3's claim that `spUsers_RegisterFailedLogin` is "the only procedure in nucentra with
OUTPUT parameters" is wrong twice over.** §5.4 already corrected it once with `spStaffDocument_Delete`;
this is the third. The three are in three different positions, and what separates them is what each prompt
was allowed to do about it:

| Procedure | Prompt | `.sql` change allowed? | How `SqlData` reads it |
|---|---|---|---|
| `spUsers_RegisterFailedLogin` | 2 | **yes** — appended a trailing `SELECT` of the same three values | `QuerySingleOrDefaultAsync<FailedLoginResult>` |
| `spStaffDocument_Delete` | 3 | no | `DynamicParameters`, `DbType.AnsiString`, size 500 |
| `spPatientBasic_Insert` | 5 | no | `DynamicParameters`, `DbType.AnsiString`, size 100 |

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

#### 🔴 THREE OF THE FOUR WRITES ANSWER THROUGH OUTPUT PARAMETERS — nucentra now has six such procedures

§5.3 called `spUsers_RegisterFailedLogin` *"the only procedure in nucentra with OUTPUT parameters"*. §5.4
corrected it once (`spStaffDocument_Delete`), §5.6 a second time (`spPatientBasic_Insert`), and this area
adds **three more**, bringing the total to **six**. The claim in §5.3 is simply wrong and should be read as
"the only one Prompt 2 had met".

| Procedure | Prompt | OUTPUT parameters | `.sql` change allowed? | How `SqlData` reads it |
|---|---|---|---|---|
| `spUsers_RegisterFailedLogin` | 2 | 3 | **yes** — appended a trailing `SELECT` | `QuerySingleOrDefaultAsync<FailedLoginResult>` |
| `spStaffDocument_Delete` | 3 | 1 | no | `DynamicParameters` |
| `spPatientBasic_Insert` | 5 | 1 | no | `DynamicParameters` |
| `spPatientAppointment_Insert` | **6** | **1** | no | `DynamicParameters` |
| `spPatientAppointment_Update` | **6** | **8** | no | `DynamicParameters` |
| `spPatientAppointment_UpdateStatus` | **6** | **8** | no | `DynamicParameters` |

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
