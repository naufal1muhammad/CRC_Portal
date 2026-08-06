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
| `SaveAppointmentAsync` | *Prompt 6* | the slot-availability read must stay inside the transaction that consumes the slot, or two administrators book the same hour |

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
