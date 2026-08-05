# nucentra (CRC Portal) → Microsoft Azure: Step-by-Step Deployment Guide

A beginner-friendly, click-by-click guide to hosting the **whole** nucentra portal in Azure —
the web app, the data access layer, **and** the SQL database — using the **Visual Studio Publish**
wizards, **alongside your existing HEART deployment in the same subscription**.

> Written for the current codebase: `CRC.Web` (ASP.NET Core MVC, **.NET 8**),
> `CRC.Data` (ADO.NET / `DatabaseHelper`), and `CRC.Database` (SSDT / DACPAC).
> nucentra stores uploaded patient and staff documents in a **private Azure Blob container**,
> so this deployment includes a **storage account**.

> ## 🔵 Retrofitting an existing deployment? Read this before anything else.
>
> nucentra **used to** write uploaded documents into `wwwroot/uploads/**` on the App Service file
> system. Anything under `wwwroot` is served by `UseStaticFiles()`, which runs *before* authentication
> and performs no authorisation check — so every uploaded patient and staff document was a
> **permanently public URL**, downloadable by anyone holding the link with no login at all.
>
> That is fixed in two halves:
>
> - **The code half** is planned in [`DocumentStoragePlan.md`](DocumentStoragePlan.md) — six prompts,
>   run in order, that move documents into a private Blob container. **No prompt in that plan touches
>   Azure.**
> - **The Azure half is yours, and it is all in [§10](#10-part-g--document-storage-the-azure-portal-steps)
>   of this guide.** §10 is written click-by-click and says exactly which of its steps to do after
>   which prompt.
>
> **If your site is already live, §10.6 is not optional.** Every document ever uploaded to it is still
> sitting on the App Service disk, still public, and **a deployment will never remove it** — the
> publish setting that protects your logs (§8.4) also protects those files. Moving *new* uploads to
> Blob storage does nothing about the *old* ones.

> **Companion document:** [`HEART_Azure_Deployment_Guide.md`](../HEART/HEART_Azure_Deployment_Guide.md)
> in the HEART repo. This guide deliberately mirrors its structure so you can follow the same rhythm.
> Where nucentra differs from HEART, it is called out with a **⚠️ Differs from HEART** note — there are
> six of them, and **skipping any one of them will break the deployment**.

---

## 0. First, the question you asked: "Will this break HEART?"

**No — provided you follow the naming in this guide.** Here is exactly why, because you should
understand the boundary rather than take it on faith.

Azure is organised in layers. Your **subscription** is the billing and ownership boundary. Inside it,
a **resource group** is a folder. Two applications in two different resource groups share *nothing*
operationally — they are as isolated as if they were in two different Azure accounts, except the
invoice arrives together.

| Layer | HEART | nucentra | Shared? |
|---|---|---|---|
| Subscription | Subscription 1 (`82c5292b-…`) | Subscription 1 (same) | **Yes** — one bill, one Entra tenant |
| Resource group | `rg-heart-prod` | `rg-nucentra-prod` | **No** — separate folders |
| App Service Plan (the VM you pay for) | HEART's existing plan | `asp-nucentra-prod` (**new**) | **No** — separate compute |
| Web App | `heart-web-prod` | `nucentra-web-prod` | **No** |
| SQL logical server | `heart-sql-prod` | `nucentra-sql-prod` (**new**) | **No** |
| Database | `HEART_DB` | `CRC_DB` | **No** |
| Storage account | `heartstorprod` | `nucentrastorprod` (**new**) | **No** — separate accounts, separate keys |
| File system / config / logs | HEART's own | nucentra's own | **No** |

**What this buys you concretely:**

- Restarting, redeploying, scaling, or **deleting** nucentra cannot touch HEART. Deleting
  `rg-nucentra-prod` deletes only nucentra's resources.
- A CPU spike or memory leak in nucentra cannot slow HEART down, because they are on **different
  App Service Plans** — different virtual machines. (This is why §6 has you create a *new* plan
  rather than adding nucentra to HEART's. It costs one extra plan per month; that cost *is* the
  isolation you asked for.)
- A bad SQL query in nucentra cannot exhaust HEART's database DTUs — different logical servers,
  different databases.

**The two things that genuinely are shared, and what they mean:**

1. **The bill.** Both appear on one invoice. Use the *Cost analysis* blade with a **group-by
   Resource group** to see them separately.
2. **Subscription-level quotas.** A subscription has a cap on how many App Service Plan instances
   it can run per region. On Basic tier with two single-instance plans you are nowhere near it —
   this is a non-issue at your scale, but it is the one thing that is technically a shared pool.

**Rule to follow throughout:** never type a name beginning with `heart-` or `rg-heart` while
working through this guide, and never select `rg-heart-prod` from a resource-group dropdown.
That is the entire safety procedure.

---

## 1. The big picture — what goes where

Your solution has three projects. Here's how each maps to an Azure service:

| Your project | What it is | Where it goes in Azure |
|---|---|---|
| **CRC.Web** | The ASP.NET Core website | **Azure App Service** (a managed web host) |
| **CRC.Data** | ADO.NET class library (`DatabaseHelper`) | **Nowhere separate** — it compiles *into* CRC.Web and deploys with it |
| **CRC.Database** | SSDT project → DACPAC | **Azure SQL Database** |
| Patient / staff documents | Files the app uploads | **Azure Storage account** → one **private** Blob container, `nucentra-documents` (§10) |
| Serilog `Logs/` files | app-*.log / audit-*.log | Stay on App Service disk (or upgrade to Application Insights) |

**Important clarification:** the "data access layer" is **not** hosted on its own. It is a library
referenced by the web project (`<ProjectReference Include="..\CRC.Data\CRC.Data.csproj" />`), so when
you publish CRC.Web, `CRC.Data.dll` is bundled inside it automatically. There is nothing extra to deploy for it.

> **⚠️ Differs from HEART #1 — one container, two prefixes, and staff documents too.**
> Both portals keep documents in a private Blob container behind short-lived SAS URLs, but the shapes
> differ and a habit from the HEART deployment will misname things here.
>
> | | HEART | nucentra |
> |---|---|---|
> | Storage account | `heartstorprod` | **`nucentrastorprod`** |
> | Container | `patient-documents` | **`nucentra-documents`** |
> | What's in it | patient documents only | patient **and staff** documents |
> | Blob keys | `patients/{PatientId}/{guid}.ext` | `patients/{Patient_ID}/{guid}.ext` **and** `staff/{Staff_ID}/{guid}.ext` |
> | Container settings | one | still **one** — the two kinds are told apart by key prefix, not by container |
>
> So you create **four** Azure resources here, not three, and there is exactly **one**
> `DocumentStorage__ContainerName` value to set. **All of the storage steps live in §10.**

So the real work is three deployments:

1. **The database** → Azure SQL Database (publish the DACPAC).
2. **The website** (with the DAL inside) → Azure App Service.
3. **The document store** → an Azure Storage account with one private Blob container.

Everything else is wiring config so those find each other.

---

## 2. Prerequisites (one-time)

1. Your existing **Azure account/subscription** (Subscription 1 — the one already running HEART).
   Nothing new to sign up for.
2. **Visual Studio 2022/2026** with these workloads/components (Visual Studio Installer → Modify):
   - *ASP.NET and web development*
   - *Data storage and processing* → **SQL Server Data Tools (SSDT)** (needed to publish the DACPAC)
   - *Azure development*

   You already have all three from the HEART deployment.
3. **SSMS** or **Azure Data Studio** (to inspect the database and run a couple of setup commands).
4. Confirm the solution builds locally first — **and note that the two projects build differently:**

   ```bash
   dotnet build CRC.Web/CRC.Web.csproj
   ```

   `CRC.Database` is a **classic SSDT `.sqlproj`** — `dotnet build` **cannot** build it. Use MSBuild
   from the Visual Studio install:

   ```bash
   "C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
   ```

   Expected: `Build succeeded.`, `0 Error(s)`, and exactly **two** warnings —
   `SQL71502: Procedure: [dbo].[spStaffSlots_CreateRange] has an unresolved reference to object [sys].[all_objects]`
   at lines 46 and 52. **These are the documented baseline** (see `SEEDING.md`). They are harmless,
   `sys.all_objects` exists in Azure SQL exactly as it does locally, and the deployed procedure works.
   Don't chase them.

**A word on naming.** Some Azure names must be **globally unique across all of Azure** (the SQL server
and the web app, because they become public DNS names). The names below are the ones this guide uses
throughout. If Azure rejects one as taken, add a short suffix (e.g. `nucentra-sql-prod-my`) and use
your version consistently everywhere after that.

| Resource | Name to use | Uniqueness |
|---|---|---|
| Resource group | `rg-nucentra-prod` | Unique within your subscription |
| SQL logical server | `nucentra-sql-prod` | **Globally unique** → `nucentra-sql-prod.database.windows.net` |
| Database | `CRC_DB` | Unique within the server |
| App Service Plan | `asp-nucentra-prod` | Unique within the resource group |
| Web App | `nucentra-web-prod` | **Globally unique** → `nucentra-web-prod….azurewebsites.net` |
| SQL admin login | `nucentraadmin` | Your choice — **write it and the password down** |
| Storage account | `nucentrastorprod` | **Globally unique** → `nucentrastorprod.blob.core.windows.net`. Lowercase letters and digits only — **no dashes, no capitals**, 3–24 characters |
| Blob container | `nucentra-documents` | Unique within the storage account. **Must match `DocumentStorage__ContainerName` character for character** — a mismatch is a silent upload failure |

**Why the database stays `CRC_DB` and not `Nucentra_DB`:** the name is baked into
`CRC.Web/appsettings.json`, `SEEDING.md`, and every SqlPackage command in the repo. Keeping it means
your local development database and your Azure database are the same name, so scripts and docs work
in both places without a mental translation step. The *resources* carry the product name; the
*database* keeps the code's name.

---

## 3. Understand the four Azure resources you'll create

Before clicking, here's what each thing is:

- **Resource group** — a folder that holds all the resources for this app, so you can manage/delete
  them together. Create one: `rg-nucentra-prod` in **Malaysia West** (the same region HEART runs in —
  same latency for your users, and it keeps both apps on one map).
- **Azure SQL Database** — two parts: a **logical SQL server** (`…database.windows.net`, the login
  endpoint) and a **database** on it (`CRC_DB`). You create the server once, then the database on it.
  A logical server itself costs nothing — you pay for the database — so a dedicated server for
  nucentra is free isolation.
- **App Service** — also two parts: an **App Service Plan** (the compute/VM tier you pay for) and the
  **Web App** itself (your site). You are creating a **new plan**, not reusing HEART's.
- **Storage account** — holds the **private Blob container** (`nucentra-documents`) that every
  uploaded patient and staff document goes into. The database stores only a *key* into that container,
  never the file and never a URL. **All of its steps are in §10**, because that section is also where
  an already-live deployment gets retrofitted.

All four go in the same resource group and region.

---

## 4. PART A — Create the Azure SQL Database

We'll do the database first because the web app needs its connection string.

1. Go to the **Azure Portal** → <https://portal.azure.com>.
2. Search **"SQL databases"** → **Create**.
3. **Basics** tab:
   - **Subscription**: Subscription 1 (the same one HEART uses).
   - **Resource group**: click **Create new** → `rg-nucentra-prod`.
     **Do not pick `rg-heart-prod` from the dropdown.**
   - **Database name**: `CRC_DB` (match what the app uses).
   - **Server**: click **Create new**:
     - Server name: `nucentra-sql-prod` (must be globally unique).
     - Location: **Malaysia West** (same as HEART).
     - **Authentication method**: choose **"Use both SQL and Microsoft Entra authentication"**
       (this gives you flexibility — a SQL admin login now, and the option of passwordless later).
       - Set a **Server admin login** (e.g. `nucentraadmin`) and a strong password — **write these down.**
         This is a *different* login from HEART's server admin; they are unrelated servers.
       - Set the **Entra admin** to your own account (needed later for Managed Identity, §7 Option B).
   - **Want to use SQL elastic pool?** No.
   - **Workload environment**: Development (you can scale later).
4. **Compute + storage**: click **Configure database**. For a small app start cheap:
   - **General Purpose → Serverless** with a low min vCore (auto-pauses when idle = cheaper), **or**
     the **Basic**/**Standard S0 (DTU)** tier. You can change this anytime. Apply.

   > If nucentra is used sporadically, serverless with auto-pause is markedly cheaper. Be aware the
   > first request after a pause takes a few seconds to wake the database — users see one slow login,
   > then normal speed.
5. **Networking** tab:
   - **Connectivity method**: Public endpoint.
   - **Firewall rules**: set **"Allow Azure services and resources to access this server"** = **Yes**
     (lets your App Service reach the DB), and **"Add current client IP address"** = **Yes**
     (lets *you* connect from SSMS/Visual Studio to publish the DACPAC).
6. Leave the rest as defaults → **Review + create** → **Create**. Wait for deployment to finish.

**Result:** an empty `CRC_DB` on `nucentra-sql-prod.database.windows.net`. Next we put your schema into it.

---

## 5. PART B — Publish the database (DACPAC) from Visual Studio

Your `CRC.Database` project builds a **DACPAC** (a single file describing the whole schema). Publishing
it makes Azure SQL match that schema — and it **also runs your post-deployment seed**
(`Script.PostDeployment.sql` → `Seed_Lookups.sql`, `Seed_Location.sql`, `Seed_Users.sql`), so all
eleven lookup tables, the 3,242-row Malaysian location tree, and the bootstrap `SUPERUSER` account get
created automatically. You will have a **usable, fully populated portal** the moment this finishes —
no CSV import, no manual `INSERT`, no SSMS step.

### 5.1 ✅ The target platform — already done, just confirm it

> **⚠️ Differs from HEART #2 — historical.** This step used to be a blocker: `CRC.Database.sqlproj`
> shipped targeting `Sql160DatabaseSchemaProvider` (SQL Server 2022), and publishing that straight to
> Azure SQL fails with *"…cannot be published to Microsoft Azure SQL Database v12."*
> **It has since been retargeted and committed**, so there is nothing to change — but confirm it before
> your first publish, because the failure mode is confusing if the setting ever regresses.

Confirm line 10 of `CRC.Database/CRC.Database.sqlproj` reads:

```xml
<DSP>Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider</DSP>
```

If it does not, fix it: Visual Studio → right-click **CRC.Database** → **Properties** →
**Project Settings** → **Target platform** = **Microsoft Azure SQL Database**, save, and rebuild with
MSBuild (§2.4).

Expect the **same two `SQL71502` warnings** as before and no new errors. I checked the schema for the
constructs Azure SQL rejects — `xp_cmdshell`, `sp_configure`, `BULK INSERT`, `OPENROWSET`, `USE [db]`,
filegroup clauses, `FILESTREAM`, linked servers, SQL Agent jobs — and **none of them appear in your
tables, procedures, or seeds.** The retarget should be clean. (The one `BULK INSERT` in
`Seed_Location.sql` is entirely inside a comment block and never executes — which is exactly why the
seed uses inline `INSERT`s instead.)

> **Commit this change.** It is a permanent, correct change to the repo — SSDT still deploys an
> Azure-targeted DACPAC to a local SQL Server for development, so nothing about your local workflow breaks.

### 5.2 Publish

1. Right-click **CRC.Database** → **Publish…**.
2. **Target database connection** → **Edit…**:
   - **Server name**: `nucentra-sql-prod.database.windows.net`
   - **Authentication**: **SQL Server Authentication**; enter the admin login/password from §4.
   - **Database name**: `CRC_DB`.
   - **Test Connection** → OK. (If it fails, re-check the firewall step in §4.5 — your IP must be allowed.)
3. (Optional) **Save Profile As…** a `.publish.xml` so you can re-publish later with one click.
4. Click **Publish**. Visual Studio compares the DACPAC to the (empty) database and creates everything.

**Or from the command line** (the `SEEDING.md` command, retargeted at Azure):

```bash
"C:/Program Files/Microsoft Visual Studio/18/Insiders/Common7/IDE/Extensions/Microsoft/SQLDB/DAC/SqlPackage.exe" /Action:Publish /SourceFile:CRC.Database/bin/Debug/CRC.Database.dacpac /TargetServerName:nucentra-sql-prod.database.windows.net /TargetDatabaseName:CRC_DB /TargetUser:nucentraadmin /TargetPassword:YOUR_PASSWORD /TargetEncryptConnection:True
```

Note `/TargetEncryptConnection:True` replaces the local `/TargetTrustServerCertificate:True` — Azure
SQL always uses a valid, publicly-trusted certificate, so you encrypt properly rather than trusting blindly.

### 5.3 Verify in SSMS

1. Open **SSMS** → connect to `nucentra-sql-prod.database.windows.net` with the SQL admin login.
2. Expand `CRC_DB` → **Tables** and **Programmability → Stored Procedures**. You should see the 28
   tables (`Users`, `Staff`, `Branch`, the `Patient*` set, the twelve `LU_*` lookups) and the `sp*` procs.
3. Confirm the seed ran — these are the exact counts `SEEDING.md` documents, so any mismatch means
   something went wrong:

   ```sql
   SELECT COUNT(*) FROM dbo.LU_LOCATION;   -- expect 3242  (16 states + 442 cities + 2,784 postcodes)
   SELECT COUNT(*) FROM dbo.LU_RACE;       -- expect 11
   SELECT COUNT(*) FROM dbo.LU_STAFFTYPE;  -- expect 5
   SELECT Username, User_Type FROM dbo.Users;  -- expect exactly one row: SUPERUSER, 1
   ```

**Your database is now live in Azure**, schema and reference data complete.

---

## 6. PART C — Create the App Service (web host)

1. Portal → search **"App Services"** → **Create → Web App**.
2. **Basics**:
   - **Subscription**: Subscription 1.
   - **Resource group**: **`rg-nucentra-prod`** — select the one you created in §4, **not** `rg-heart-prod`.
   - **Name**: `nucentra-web-prod` → your URL becomes something like
     `https://nucentra-web-prod-<random>.malaysiawest-01.azurewebsites.net`.
     (Azure appends a random suffix and the region to new web app hostnames — HEART's is
     `heart-web-prod-fdd5f6cgehh2fnba.malaysiawest-01.azurewebsites.net`. Yours will differ; copy the
     real URL from the portal after creation.)
   - **Publish**: **Code**.
   - **Runtime stack**: **.NET 8 (LTS)**.

     > **⚠️ Differs from HEART #3 — runtime version.** HEART is on **.NET 10**; `CRC.Web.csproj`
     > targets **`net8.0`**. Pick **.NET 8**, not .NET 10 — the platform must match what you built.
     > .NET 8 is a fully supported, generally-available stack on App Service, so unlike HEART's guide
     > there is **no "preview tag" caveat and no self-contained fallback needed here.**
     > See §13 for the .NET 8 support-end date and the upgrade plan.
   - **Operating System**: **Windows** (simplest with the Visual Studio publish flow, and what HEART uses).
   - **Region**: **Malaysia West**.
   - **Pricing plan (App Service Plan)**: click **Create new** → name it **`asp-nucentra-prod`** →
     choose **Basic B1**.

     > **⚠️ Differs from HEART #4 — this is the isolation step, and it is the one that is easy to get
     > wrong.** The dropdown will helpfully offer you HEART's existing plan. **Do not select it.**
     > Click *Create new*. If nucentra shares HEART's plan, they share one VM's CPU and memory, and
     > restarting or scaling the plan restarts *both portals*. Creating a separate plan is what makes
     > the "must not affect HEART" requirement actually true rather than merely hoped for.
3. **Review + create** → **Create**.
4. When it finishes, open the resource and **copy the real URL** from the Overview blade — you'll need
   it in §9.

---

## 7. PART D — Configure the web app's settings (connect it to SQL)

Your code reads **two** things from configuration that must change for Azure:

- `ConnectionStrings:CRC_DB` → the database. **This section.**
- `DocumentStorage:ConnectionString` and `DocumentStorage:ContainerName` → the Blob container.
  **[§10.4](#104-tell-the-web-app-where-the-container-is-two-app-settings)** — do that section when it
  tells you to, not now.

> **⚠️ Differs from HEART #5 — the connection string is named `CRC_DB`, not `DefaultConnection`.**
> `CRC.Data/Database/DatabaseHelper.cs:21` reads `configuration.GetConnectionString("CRC_DB")` and
> **throws on startup** if it is missing:
> `InvalidOperationException: Connection string 'CRC_DB' not found.`
> Name it anything else and the site returns HTTP 500 on every single request from the first one.
> The `DocumentStorage__*` app settings, by contrast, are named **identically** to HEART's — that pair
> is the one place where copying your HEART habit is correct.

**Never** hard-code production credentials in `appsettings.json`. Set them in App Service, which
overrides the file at runtime. Portal → your Web App → **Settings → Environment variables**.

### 7.1 The database connection — pick ONE option

**Option A — SQL login (simplest to get working):**

1. Under **Connection strings**, **+ Add**:
   - **Name**: `CRC_DB`
   - **Type**: **SQLAzure**
   - **Value**:
     ```
     Server=tcp:nucentra-sql-prod.database.windows.net,1433;Database=CRC_DB;User ID=superuser;Password=#Admin1234;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
     ```
   App Service exposes this to .NET as `ConnectionStrings:CRC_DB`, so `GetConnectionString("CRC_DB")`
   just works — no code change needed.

   Note `TrustServerCertificate=False` here, versus `True` in your local `appsettings.json`. Local SQL
   Server uses a self-signed certificate; Azure SQL uses a real one, so you verify it properly.

**Option B — Managed Identity (recommended, passwordless — no password in config):**

A **managed identity** is an automatic identity Azure gives your web app so it can log in to the DB
without any stored secret. Best practice, and your stack already supports it (`Microsoft.Data.SqlClient` 6.1.3).

1. Web App → **Settings → Identity** → **System assigned** → **Status On** → Save. (Note the app name
   `nucentra-web-prod` — that's the identity's name.)
2. In **SSMS**, connect to the SQL server **as the Entra admin** you set in §4 (not the SQL login), then run
   against `CRC_DB`:
   ```sql
   CREATE USER [nucentra-web-prod] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [nucentra-web-prod];
   ALTER ROLE db_datawriter ADD MEMBER [nucentra-web-prod];
   GRANT EXECUTE TO [nucentra-web-prod];   -- REQUIRED: the app calls everything via stored procedures
   ```

   > **`GRANT EXECUTE` is doubly important in nucentra.** Beyond the obvious — every data call goes
   > through an `sp*` procedure — `DatabaseHelper.SupportsUserIdParameterAsync` queries
   > **`sys.parameters`** before each call to decide whether to auto-inject `@User_ID` for the audit
   > trail. SQL Server only shows a user metadata for objects they hold a permission on. Without
   > `GRANT EXECUTE`, that lookup silently returns "no `@User_ID` parameter" for every procedure — so
   > even in the failure case you would lose audit attribution before you lose anything visible.
   > Grant it and both problems disappear together.
3. Add the **Connection string** (Name `CRC_DB`, Type **SQLAzure**) with **no password**:
   ```
   Server=tcp:nucentra-sql-prod.database.windows.net,1433;Database=CRC_DB;Authentication=Active Directory Default;Encrypt=True;
   ```
   `Authentication=Active Directory Default` tells the SQL client to use the app's managed identity in
   Azure automatically (and your Visual Studio/Azure CLI login when running locally).

Start with Option A if you want it working fast; switch to B when you're ready to harden.

### 7.2 ⚠️ Set the time zone — do not skip this one

> **⚠️ Differs from HEART #6 — and this changes what users see.** HEART's guide says to leave App
> Service at UTC, because HEART stores UTC and converts to Malaysia time in code. **nucentra has no
> such conversion layer.** Several controllers call `DateTime.Today` and `DateTime.Now` directly —
> the admin and staff dashboards (`AdminDashboardController.cs:60`, `StaffDashboardController.cs:108`),
> the appointment calendar, and patient ID generation (`PatientController.cs:686` uses
> `DateTime.Today.Year % 100`).
>
> Windows App Service runs in **UTC** by default. Your development machine runs at **UTC+8**. So on
> Azure, between **midnight and 8 a.m. Malaysia time**, `DateTime.Today` would still return
> *yesterday's* date — dashboards would show the wrong day's appointments every single morning, and
> in the 00:00–08:00 window on 1 January a new patient would be issued an ID carrying the previous
> year's digits.

Fix it with one setting. Portal → Web App → **Settings → Environment variables → App settings** → **+ Add**:

| Name | Value |
|---|---|
| `WEBSITE_TIME_ZONE` | `Singapore Standard Time` |

`Singapore Standard Time` is the Windows time-zone ID for UTC+8, which is Malaysia time. Click
**Apply/Save** — the app restarts and `DateTime.Today` now agrees with the calendar on your users' walls.

**One related thing this does *not* fix, so that you know:** `spPatientDocument_Insert` and
`spStaffDocument_Insert` stamp `UploadedOn` with **`GETDATE()`**, which runs on the *database* server.
Azure SQL is always UTC and ignores `WEBSITE_TIME_ZONE`, so document upload timestamps will read 8
hours behind local time. Everything security-related is already correct — `dbo.Users`, the login
lockout window, and `Last_Login` all use `GETUTCDATE()` and pair with `DateTime.UtcNow` in
`AccountController`, so lockouts and session expiry behave identically in Azure. If the document
timestamps bother you, replace the `GETDATE()` in each procedure. **The two are not identical**, because
the columns differ — `PatientDocument.UploadedOn` is `VARCHAR(100)` and `StaffDocument.UploadedOn` is
`DATETIME`:

```sql
-- spPatientDocument_Insert.sql — replace CONVERT(VARCHAR(100), GETDATE(), 120) with:
CONVERT(VARCHAR(100), GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time', 120)

-- spStaffDocument_Insert.sql — replace GETDATE() with:
CAST(GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time' AS DATETIME)
```

Then re-publish the DACPAC. It is cosmetic, not a blocker — go live first, tidy this after.

---

## 8. PART E — Publish the web app from Visual Studio

1. In Visual Studio, right-click **CRC.Web** → **Publish…**.
2. **Target**: **Azure** → **Azure App Service (Windows)** → **Next**.
3. Sign in to your Azure account, pick **Subscription 1**, expand **`rg-nucentra-prod`**, and select
   **`nucentra-web-prod`** → **Finish**.

   > Take one second here to confirm the resource group reads `rg-nucentra-prod`. The publish dialog
   > lists every app in the subscription, HEART's included, and `heart-web-prod` will be sitting right
   > there in the same list.

4. **⚠️ Before your first publish, check the file-deletion setting.** In the publish profile, click
   **Show all settings** and confirm, under **File Publish Options**:

   | Setting | Required value | Why |
   |---|---|---|
   | **Remove additional files at destination** | **UNCHECKED** | It protects your `Logs/` folder — including the 365-day `audit-*.log` accountability record. |

   **The reason for this changed, so re-read it even if you knew the old one.** It used to be that this
   box protected your *uploaded documents*, which lived inside the very folder a publish overwrites.
   That is no longer true — documents are in Blob storage now (§10) and no deployment can touch them.
   What the box protects today is `site/wwwroot/Logs/`: `app-*.log` (31 days) and `audit-*.log`
   (365 days), which exist only on the App Service disk. Tick it and you lose your audit history.

   Unchecked is the default, and it is the equivalent of `<SkipExtraFilesOnServer>true</SkipExtraFilesOnServer>`
   in HEART's saved `.pubxml` — so the safe behaviour is what you get if you simply don't touch it.

   > **The one and only time to tick it** is §10.6 Route B, as a deliberate one-off to purge the old
   > `wwwroot/uploads` folder from an already-live App Service. That route trades your log history for
   > not having to install an FTP client. §10.6 spells out both sides of that trade.

5. Click **Publish**. Visual Studio builds in Release, bundles `CRC.Data` inside, and uploads.
6. When it finishes, the browser opens your site.

App Service serves **HTTPS by default** on that domain, which nucentra requires — `Program.cs` sets
`CookieSecurePolicy.Always` on both the auth cookie and the `__Host-CSRF` antiforgery cookie, so over
plain HTTP nobody could log in at all. On `azurewebsites.net` this is handled for you.

**A note on the login rate limiter:** `Program.cs` partitions the login rate limit by
`Connection.RemoteIpAddress`. On **Windows** App Service the app runs in-process behind IIS/ANCM, which
puts the real client IP there — so your 10-requests-per-minute-per-IP limit works correctly as
deployed, with no `UseForwardedHeaders` needed. (This is one reason to stay on Windows rather than
switch to Linux containers, where you *would* need to add forwarded-headers handling or every request
would appear to come from one proxy address and a single user could lock out everybody.)

---

## 9. PART F — First login & smoke test

1. Browse to your site URL — you should get the **Login** page (the default route is
   `{controller=Account}/{action=Login}`).
2. Sign in with the seeded bootstrap account:
   - Username: **`SUPERUSER`**  ·  Password: **`ChangeMe!123`**
3. > **🔴 Change this password immediately, before you do anything else.**
   >
   > **Unlike HEART, nothing in nucentra forces you to.** HEART seeds `MustChangePassword = 1` and
   > redirects you. `dbo.Users` in nucentra **has no such column** — there is no first-login redirect,
   > no expiry, no reminder. The account keeps this password until a human changes it.
   >
   > And this password is **public**: it is printed in plain text in `SEEDING.md` and
   > `Scripts/Seed_Users.sql`, both in source control. From the moment §8 finishes, your portal is on
   > the public internet with a superuser account whose credentials are written down in your repo.
   >
   > Go to **Account → Change Password** (`/Account/ChangePassword`) and change it now. The new
   > password must satisfy the policy in `appsettings.json`: 12+ characters, upper, lower, digit,
   > non-alphanumeric, 2+ unique characters.
   >
   > The change is permanent and safe: `Seed_Users.sql` is guarded with `IF NOT EXISTS` on `Username`,
   > so **re-publishing the DACPAC never resets it.** (The corollary: if the SUPERUSER row is ever
   > deleted outright, the next publish recreates it with the seeded password again.)
4. Verify end-to-end, in this order — it's also the order `SEEDING.md` prescribes for setting up a new
   installation, because each step depends on the one before:
   1. **Check a dropdown that reads the location tree** — e.g. any address field. 3,242 rows should be
      there. This proves DB reads and the seed both worked.
   2. **Create a Branch.**
   3. **Create Staff** (a `User_Type = 3` account requires a staff record to point at).
   4. **Configure document settings** — patient documents per discharge type, staff documents per staff type.
   5. **Upload a patient document, then download it.** This proves the storage account, the container
      name and the SAS minting all line up. If the upload fails, the cause is almost always one of the
      two `DocumentStorage__*` settings (§10.4) — see §15. *(Requires §10 to be done.)*
   6. **Check the date on a dashboard** — confirm it shows today's Malaysian date, proving §7.2 took effect.
   - If a page 500s, check logs (§11).

---

## 10. PART G — Document storage: the Azure portal steps

Every uploaded patient and staff document goes into a **private** Blob container. The database stores
only a **key** into that container (`dbo.PatientDocument.BlobName`, `dbo.StaffDocument.BlobName`) —
never the file, never a URL. When someone clicks a document, the app checks their session, then mints
a **read-only SAS URL that expires in five minutes** and hands that to the browser.

**This section is entirely yours to do, by hand, in the portal.** The code side is
[`DocumentStoragePlan.md`](DocumentStoragePlan.md), six prompts, and **no prompt in it touches Azure**.

### 10.0 When to do what

Work down this table. The right-hand column is the gate: do not start a step until its gate is met.

| Step | What you do | Do it **after** |
|---|---|---|
| §10.1 | Create the storage account `nucentrastorprod` | **Prompt 1** |
| §10.2 | Create the private container `nucentra-documents` | §10.1 |
| §10.3 | Copy the storage connection string | §10.2 |
| §10.4 | Add the two `DocumentStorage__*` app settings to the Web App | §10.3 |
| §10.5 | Re-publish the database, then the web app | **Prompt 6** |
| §10.6 | 🔴 Check for, and delete, the old files on the App Service | §10.5 |
| §10.7 | 🔴 Verify on the live site — including the logged-out URL test | §10.6 |

**Why §10.1–§10.4 sit that early.** They are completely inert until the new code is deployed: the
version running on your site right now does not read a `DocumentStorage` section at all, so adding the
settings changes nothing. Doing them first gets the one thing that can *fail for an external reason*
out of the way early — the storage account name is **globally unique across all of Azure** and may
already be taken by a stranger. Far better to discover that at the start than at the finish line.

---

### 10.1 Create the storage account

1. Portal → search **"Storage accounts"** → **+ Create**.
2. **Basics** tab:
   - **Subscription**: Subscription 1 (the one already running HEART).
   - **Resource group**: select the **existing** `rg-nucentra-prod`. **Do not pick `rg-heart-prod`.**
   - **Storage account name**: `nucentrastorprod`.
     **Lowercase letters and digits only — no dashes, no capitals, 3–24 characters.** (This is why it
     is not `nucentra-stor-prod`.) If Azure says the name is taken, add a short suffix — e.g.
     `nucentrastorprodmy` — and **use your version everywhere from here on**, including §10.3.
   - **Region**: **Malaysia West** (same as everything else).
   - **Primary service**: *Azure Blob Storage or Azure Data Lake Storage Gen 2*.
   - **Performance**: **Standard**.
   - **Redundancy**: **Locally-redundant storage (LRS)** — the cheapest, and fine to start.
3. **Advanced** tab — three settings here actually matter:

   | Setting | Set it to | Why |
   |---|---|---|
   | **Require secure transfer for REST API operations** | **✅ Enabled** (the default) | Forces HTTPS. Leave it on. |
   | **Allow enabling anonymous access on individual containers** | **⬜ Disabled** | This is the one worth changing from the default. With it off, **nobody can ever flip a container to public**, by accident or otherwise — the option simply is not offered. Given what this container holds, take it. |
   | **Enable storage account key access** | **✅ Enabled** | 🔴 **Must stay on.** The app authenticates with the connection string and signs SAS URLs with the account key. Turn this off and every upload *and* every download fails. |

   Leave **Minimum TLS version** at 1.2.
4. **Networking** tab: **Enable public access from all networks**. (Locking this to a private endpoint
   is a later hardening step — see §13.)
5. **Data protection** tab: turn on **Enable soft delete for blobs**, retention **7 days**.
   This is a cheap safety net — a document deleted by mistake is recoverable for a week instead of
   being gone instantly. It does not weaken anything: soft-deleted blobs are not readable.
6. **Review + create** → **Create**. Wait for deployment to finish.

### 10.2 Create the private container

1. Open the new storage account → left menu **Data storage → Containers** → **+ Container**.
2. **Name**: `nucentra-documents`
   🔴 Exactly this, all lowercase. It must match the `DocumentStorage__ContainerName` app setting in
   §10.4 **character for character** — a typo here produces uploads that fail with no obvious cause.
   Note it is **not** `patient-documents`; that is HEART's container name, in HEART's storage account.
3. **Anonymous access level**: **Private (no anonymous access)**.
   If you set §10.1's *"Allow enabling anonymous access"* to Disabled, this dropdown will be locked to
   Private already — that is the setting doing its job.
4. **Create**.
5. **Verify**: back on the Containers list, the row for `nucentra-documents` shows **Private** in the
   *Public access level* column. Confirm that with your own eyes now; it is the single most important
   setting in this entire section.

> You do not need to create any folders. The `patients/` and `staff/` prefixes in the blob keys look
> like folders in the portal and in Storage Explorer, but Blob storage has no real directories — they
> appear on their own as soon as the first document is uploaded.

### 10.3 Copy the storage connection string

1. Storage account → left menu **Security + networking → Access keys**.
2. Under **key1**, click **Show** next to **Connection string**, then the copy icon.
3. You want the **Connection string**, not the **Key** on the line above it. It looks like:

   ```
   DefaultEndpointsProtocol=https;AccountName=nucentrastorprod;AccountKey=AbCd…==;EndpointSuffix=core.windows.net
   ```

🔴 **Treat this exactly like a password.** Anyone holding it can read, write and delete every document
in the account. Do not paste it into chat, e-mail, a support ticket, a screenshot, or any file in the
repository. It goes in one place only: the app setting in §10.4.

### 10.4 Tell the Web App where the container is (two app settings)

1. Portal → your Web App **`nucentra-web-prod`** → **Settings → Environment variables** →
   **App settings** tab.
2. **+ Add** twice:

   | Name | Value |
   |---|---|
   | `DocumentStorage__ConnectionString` | *(the connection string from §10.3)* |
   | `DocumentStorage__ContainerName` | `nucentra-documents` |

3. 🔴 **Two underscores, not one.** .NET maps `__` onto the `:` in `DocumentStorage:ConnectionString`.
   A single underscore is not an error — it is silently ignored, and you get a startup failure that
   points at the wrong thing.
4. These go under **App settings**, *not* under **Connection strings**. (The database one in §7 is the
   opposite: it belongs under Connection strings, with type `SQLAzure`. The two lists are different
   and are not interchangeable.)
5. Click **Apply**, then **Confirm**. The app restarts — about 30 seconds.
6. **Nothing changes yet**, and that is expected. The code currently deployed does not read these. They
   sit there doing nothing until §10.5 publishes the new build.

---

### 10.5 Re-publish the database, then the web app

Do this only once **Prompt 6** of the plan reports a clean local end-to-end pass.

**First the database.** `FilePath` was renamed to `BlobName`, which is a schema change — the new code
will not work against the old schema. Two ways, pick one:

- **Option A — clean slate** *(what you said you wanted)*. Portal → **SQL databases** → **`CRC_DB`** →
  **Delete** → type the name to confirm. Then re-create it exactly as in **§4** (steps 2–4 — the server
  `nucentra-sql-prod` already exists, so pick it rather than creating a new one), and re-publish the
  DACPAC as in **§5.2**.
  ⚠️ This deletes **everything**: branches, staff, patients, appointments and the whole
  `dbo.AuditTrails` table. The post-deployment seed then re-creates the lookups, the 3,242 locations
  and a fresh **`SUPERUSER` / `ChangeMe!123`** — so **change that password again immediately** (§9.3).
- **Option B — keep the data**. Just publish the DACPAC over the existing `CRC_DB` (§5.2). SSDT
  performs the column rename in place and nothing else is lost. Then, in SSMS, clear the two document
  tables — their rows now hold `BlobName` values like `/uploads/patient/…`, which are old file paths
  pointing at files that §10.6 is about to delete, so they are dead rows either way:

  ```sql
  DELETE FROM dbo.PatientDocument;
  DELETE FROM dbo.StaffDocument;
  ```

  Users then re-upload the documents they still need, and those go to Blob storage.

**Then the web app.** Publish `CRC.Web` from Visual Studio exactly as in **§8**, with
**"Remove additional files at destination" UNCHECKED**.

### 10.6 🔴 Check for — and delete — the old files on the App Service

**Do not skip this, and do not do it before §10.5.** Every document ever uploaded to your live site is
still sitting in `site/wwwroot/wwwroot/uploads/` and is still publicly downloadable by URL. Publishing
the new code does **not** remove it: the setting that protects your logs (§8.4) protects those files
too. And if you delete them *before* publishing the new build, the next publish puts some of them
straight back — the old build carried a folder of committed PDFs inside the repository.

> **On tooling.** The Azure Portal has no file browser for App Service that is not Kudu — the portal's
> own **Console** and **Advanced Tools** blades are both Kudu behind a different door. So there are
> exactly two ways to do this without it. **Route A** is recommended: it is the only one that lets you
> *look at what is there* before destroying it.

#### Route A — FTPS with an FTP client *(recommended)*

1. **Turn on FTP basic authentication.** Web App → **Settings → Configuration → General settings**.
   Find **FTP Basic Auth Publishing Credentials** and set it to **On**; find **FTP state** and set it
   to **FTPS only** (not *Disabled*). **Save**.
   *(On older portal builds these live on **Deployment Center → FTPS credentials** instead. If the
   toggle is already On, leave it — you will turn it back off in step 8.)*
2. **Get the credentials.** Web App → **Deployment → Deployment Center** → **FTPS credentials** tab.
   Copy three things:
   - **FTPS endpoint** — e.g. `ftps://waws-prod-mw1-001.ftp.azurewebsites.windows.net/site/wwwroot`
   - **Username** — the **Application scope** one, shaped like `nucentra-web-prod\$nucentra-web-prod`
   - **Password** — click the show/copy icon
3. **Install [FileZilla Client](https://filezilla-project.org/)** (free).
   ⚠️ Windows File Explorer will **not** work: it speaks only plain FTP, and your app is FTPS-only.
4. FileZilla → **File → Site Manager → New site**:

   | Field | Value |
   |---|---|
   | Protocol | **FTP – File Transfer Protocol** |
   | Host | the endpoint **hostname only** — drop the `ftps://` and drop the `/site/wwwroot` path |
   | Port | `21` |
   | Encryption | **Require explicit FTP over TLS** |
   | Logon Type | **Normal** |
   | User / Password | from step 2 |

   **Connect**, and accept the certificate when prompted.
5. **Navigate to `/site/wwwroot/wwwroot/uploads`.**
   The doubled `wwwroot` is correct and not a typo: `/site/wwwroot` is where your application lives,
   and the application's own static web root is a folder called `wwwroot` inside it.
   - **If that folder does not exist — you are done.** Nothing was stranded. Skip to §10.7.
   - **If it does**, open `patient/` and `staff/` and look at what is in them. Every one of those files
     is, right now, downloadable by anyone in the world who has its URL.
6. **Decide whether you need them before you delete them.** These are real patient and staff documents.
   If any were uploaded through the live portal, they exist **only** here — they are not in Blob
   storage, and their database rows are gone (§10.5). Deleting them is permanent.
   If you are unsure, drag the whole `uploads` folder onto a local folder first to download a copy,
   store it somewhere offline and access-controlled, and only then continue.
7. **Delete.** Right-click the `uploads` folder → **Delete** → confirm. Refresh and confirm it is gone.
8. **Turn the credentials back off.** Web App → **Settings → Configuration → General settings** →
   **FTP Basic Auth Publishing Credentials** → **Off** (and optionally **FTP state → Disabled**) →
   **Save**. You will not need this again, and an enabled FTP credential is a standing way in.

#### Route B — one deliberate publish with the box ticked

Use this if you would rather not install an FTP client. **It costs you your log history.**

- **What it does.** Web Deploy removes everything on the server that is not in your publish output.
  After Prompt 5 the repository contains no `wwwroot/uploads` at all, so the whole folder goes.
- **What it also does.** It removes `site/wwwroot/Logs/` — `app-*.log` and up to 365 days of
  `audit-*.log`, your security accountability record. That is not recoverable.
- **And what it cannot do.** It gives you no chance to look at the files first, so you will never know
  what was there.

Steps:

1. Visual Studio → right-click **CRC.Web** → **Publish…** → **Show all settings** →
   **File Publish Options** → tick **Remove additional files at destination** → **Save**.
2. **Publish.**
3. **Immediately untick it and Save again**, so your next routine deployment does not wipe the fresh
   logs as well. This is a one-time switch, not a new default.

### 10.7 🔴 Verify on the live site

Do all seven. Steps 5 and 6 are the ones that answer the question that started this work.

1. Browse to your site and log in.
2. **Patient → Edit** an existing patient → **Documents** tab → upload a PDF. It should report success.
   *(If it fails, it is almost always one of the two settings in §10.4 — see §15.)*
3. Portal → storage account → **Containers** → **`nucentra-documents`**. A blob has appeared under
   `patients/PAT-…/`. Confirm the container still shows **Private** in the access-level column.
4. Back in the portal UI, click the document's name in the list. It opens. That link was minted a
   second ago and dies in five minutes.
5. 🔴 **The private-container test.** Copy the URL from the browser's address bar. Delete everything
   from the **`?`** onwards — the part you keep is the bare blob URL:

   ```
   https://nucentrastorprod.blob.core.windows.net/nucentra-documents/patients/PAT-000042/9f1c….pdf
   ```

   Open that in a **private / incognito window**. It must return an XML error —
   `ResourceNotFound` or `AuthenticationFailed` — **not the file**. That is the container being private.
6. 🔴 **The test you ran by hand that started all this.** In the same private window, open

   ```
   https://<your-site>.azurewebsites.net/uploads/patient/anything.pdf
   ```

   It must return **404**. If you wrote down any real filenames in §10.6, try one of those too — same
   404, both because the file is gone and because the app now refuses that whole path outright.
7. Delete the test document through the portal UI, then confirm in the Azure portal that its blob has
   gone from the container.

### 10.8 What this protects, and what it does not

Worth being precise about, because "it's in Azure now" is not the same as "it's safe".

**What is now true:**

- The container is **private**. No URL into it works without a signature.
- The signature is **read-only** and expires after **five minutes**.
- The app **authenticates and authorises the user before it will mint one at all**.
- Uploads are checked server-side for **type and size** (`.pdf .png .jpg .jpeg .docx`, 20 MB) — before
  this work there was no validation whatsoever.
- Deleting a document deletes its blob; deleting a patient or a staff member deletes all of theirs.
- Uploads, downloads and deletions all land in `audit-*.log` and in `dbo.AuditTrails`.

**What to stay aware of:**

- ⚠️ **A minted link works for five minutes wherever it is pasted.** If a user copies the URL out of
  their address bar and messages it to somebody within that window, that person gets the file without
  logging in. This is the deliberate trade-off that keeps downloads off your App Service bandwidth.
  It is a five-minute window instead of a permanent one — but it is not zero. If you want zero, the
  change is small and is written up in `DocumentStoragePlan.md`.
- ⚠️ **The storage account key is a password sitting in your app settings.** Anyone who can read the
  Web App's configuration can read every document in the account. Managed Identity removes it
  entirely — top of the §13 list.
- ⚠️ **Git history.** The repository had 31 uploaded PDFs committed to it. Removing them from the
  working tree does not remove them from history or from GitHub. That is called out in Prompt 5 of the
  plan and is a decision for you.
- ⚠️ **Access restrictions are still worth it.** If nucentra is internal-only, **App Service →
  Networking → Access restrictions** limited to your clinic's public IP range is still the cheapest
  large risk reduction available, and it now protects the *login page* rather than compensating for a
  file-serving hole.

---

## 11. Logging, monitoring & where to find errors

Your app uses **Serilog** writing two daily files to a `Logs/` folder next to the app
(`app-*.log` = operational, `audit-*.log` = security/accountability, split by the `AuditChannel`
property). On App Service:

- **The files persist** under the app's home directory and you can read them via
  **App Service → Development Tools → Advanced Tools (Kudu)** → *Debug console* → browse to
  `site/wwwroot/Logs`. Or **Console** to peek quickly.
- **Recommended upgrade — Application Insights:** Web App → **Settings → Application Insights → Turn on**.
  This gives you searchable request traces, failures, and performance charts in the portal without code
  changes — much easier than reading files. Note that App Insights creates its own resource; let it go
  into **`rg-nucentra-prod`**, not HEART's group.
- **Keep the audit trail durable:** `audit-*.log` retains 365 files and is your accountability record —
  and it now carries every document **upload, download and deletion**, so it is the record of who
  looked at which patient's file. It survives normal redeploys (because §8.4 leaves extra files alone),
  but it lives on one app's disk, and §10.6 Route B deletes it outright. For compliance you'll
  eventually want it somewhere it can't be lost — the `dbo.AuditTrails` table already in your schema is
  the natural destination, and the document procedures already write to it.
- Turn on **App Service Logs** (Web App → **Monitoring → App Service logs**) to capture stdout for
  startup errors. Serilog also writes to console, so a failure before the file sink initialises still
  shows up there — which is exactly where a missing `CRC_DB` connection string would announce itself.

---

## 12. Confirm HEART is untouched (do this once, after go-live)

You asked to see both portals coexisting. Here is how to prove it rather than assume it.

1. **Open HEART and use it.** Browse to
   `https://heart-web-prod-fdd5f6cgehh2fnba.malaysiawest-01.azurewebsites.net`, log in, load a page
   that reads the database. If HEART works, the isolation held.
2. **Check HEART's uptime.** Portal → `heart-web-prod` → **Overview**. The app should show no restart
   correlating with your nucentra work. Nothing you did in this guide touches it.
3. **Verify the resource split.** Portal → **Resource groups**. You should see both
   `rg-heart-prod` and `rg-nucentra-prod`. Open each and confirm nothing from one app appears in the
   other's group. `rg-nucentra-prod` should contain exactly: a SQL server, a SQL database, an App
   Service Plan, a Web App, the storage account **`nucentrastorprod`** (and App Insights if you enabled
   it). Check the storage account carefully — `heartstorprod` must **not** be in this group, and
   `nucentrastorprod` must **not** be in HEART's. They are separate accounts with separate keys, so
   neither portal's connection string can reach the other's documents.
4. **Confirm the plans are genuinely separate.** Portal → **App Service plans**. You should see **two**
   plans. Click `asp-nucentra-prod` → **Apps** — it must list `nucentra-web-prod` **only**. If
   `heart-web-prod` appears in that list, they are sharing compute and you should revisit §6.2.
5. **Build the dashboard you wanted.** Portal → **Dashboard** → **New dashboard** → drag both resource
   groups (or pin the two web apps and two databases) onto it → **Save**. You now have one screen
   showing HEART and nucentra side by side, each with its own health.
6. **Split the bill.** Portal → **Cost Management → Cost analysis** → set **Group by = Resource group**.
   Two clean lines, so you always know what each portal costs you.

---

## 13. Hardening & "when you grow" checklist

Do these once the basics work, roughly in this order of value:

- **✅ The document exposure is fixed** — that used to be the top item here. §10 is now the *procedure*
  rather than the warning. Two follow-ups inherit its place at the top of this list:
  - **Go passwordless for Blob too.** The storage account key currently sits in the Web App's
    configuration (§10.4), so anyone who can read that config can read every patient document. Moving
    to Managed Identity means turning on the app's system-assigned identity, granting it
    **Storage Blob Data Contributor** on `nucentrastorprod`, and switching the code from a connection
    string to `DefaultAzureCredential` with a **user-delegation** SAS. It is a real code change, not a
    config toggle — but it removes the last long-lived secret in the deployment.
  - **Set a retention policy on the container.** Storage account → **Data management → Lifecycle
    management**. Decide how long a discharged patient's documents should be kept, and let Azure
    enforce it instead of nobody enforcing it.
- **⚠️ .NET 8 support ends 10 November 2026.** You are deploying on it deliberately and it is the right
  call for going live now — but it is roughly three months of support away as of this writing. After
  that date App Service keeps *running* .NET 8, but Microsoft stops shipping security patches for it.
  Plan to retarget `CRC.Web` and `CRC.Data` to **`net10.0`** (matching HEART) before then; it is
  normally a `TargetFramework` change plus a package bump and a regression pass. Put a reminder in your
  calendar for **September 2026** so it happens on your schedule rather than under pressure.
- **Secrets in Key Vault.** Instead of pasting the SQL password as a plain connection string, create an
  **Azure Key Vault**, store it there, and reference it from App Service with
  `@Microsoft.KeyVault(SecretUri=...)` (needs the app's managed identity + a Key Vault access role).
- **Go passwordless.** Managed Identity for SQL (§7.1 Option B) removes the stored password entirely.
- **⚠️ Cookie auth across multiple instances (read this before you scale out).** nucentra uses cookie
  authentication. By default ASP.NET Core keeps the cookie-encryption (Data Protection) keys **on the
  local disk of one instance**. If you scale `asp-nucentra-prod` to **2+ instances**, users get randomly
  logged out because instance B can't read instance A's cookies. Before scaling out, persist Data
  Protection keys to a shared store. Fine to ignore while on a single instance.

  **The second reason nucentra used to have for not scaling out is gone**: documents were written to
  the local disk of whichever instance handled the upload. They now go to Blob storage, which every
  instance reaches identically, so the file system is no longer a shared dependency. Only the Data
  Protection key problem above remains. Scaling **up** (a bigger B-series or a P-series machine) has
  neither problem and is still the right first move if the app is slow.
- **Lock the SQL firewall down.** Once verified, you can turn *off* "Allow Azure services" and instead
  use a **Private Endpoint** / VNet integration so the DB isn't exposed publicly. Advanced but the
  right end state for patient data.
- **Access restrictions on the web app.** If nucentra is internal-only, IP-restricting it (§10) is
  cheap, instant, and closes more risk than anything else on this list.
- **Custom domain + managed certificate.** Web App → **Custom domains** to use e.g.
  `nucentra.yourdomain.org` with a free App Service managed certificate.
- **Backups & restore.** Azure SQL does automatic backups — confirm the retention on the DB's
  **Backups** blade and try a point-in-time restore once so you know the drill. This still covers the
  **database only**: a restore brings back the `BlobName` rows, not the blobs. The storage account has
  its own protection, which you enabled in §10.1 — **soft delete, 7 days**. For anything longer,
  turn on **blob versioning** or **point-in-time restore** on the storage account's
  **Data protection** blade, and be aware that a database restore to an earlier point can leave rows
  pointing at blobs that were since deleted.

---

## 14. Rough cost & tiers (verify current prices in the portal)

Prices change and vary by region, so treat these as ballpark and confirm on the
[Azure pricing calculator](https://azure.microsoft.com/pricing/calculator/):

- **App Service Plan Basic B1** — a modest fixed monthly cost, and **this is the one genuinely new
  recurring charge** from this deployment. It is the price of nucentra not being able to affect HEART.
- **Azure SQL** — *Serverless General Purpose* (auto-pause) or *Basic/S0* are the cheapest starting
  points; serverless can be very cheap for intermittent use because it pauses when idle.
- **Storage account** — pennies for a few GB of documents. Standard LRS block-blob storage is charged
  per GB-month plus a tiny per-transaction fee; a clinic's PDFs and scans will not move the needle.
  Soft delete (§10.1) keeps deleted blobs billable for its 7-day window, which is still pennies.
- **Application Insights** — free tier covers low volumes.

Roughly, nucentra should land near what HEART costs you. You can start small and **scale up** any
resource later without redeploying.

---

## 15. Common errors & fixes

| Symptom | Likely cause / fix |
|---|---|
| DACPAC publish: *"cannot be published to Microsoft Azure SQL Database v12"* | Target platform reverted to SQL Server 2022. Set it back to **Microsoft Azure SQL Database** (§5.1) and rebuild. The repo is already retargeted, so seeing this means the setting regressed. |
| `dotnet build` fails on `CRC.Database.sqlproj` | Expected — it's a classic SSDT project. Use MSBuild (§2.4). |
| Two `SQL71502` warnings about `sys.all_objects` | The documented baseline (`SEEDING.md`). Harmless, on Azure too. Ignore. |
| Can't connect from SSMS/VS to the DB | Your client IP isn't in the SQL firewall. Portal → SQL server → **Networking** → add your IP. |
| Site returns 500 on **every** request, including the login page | `InvalidOperationException: Connection string 'CRC_DB' not found.` — the setting is missing or misnamed. It must be exactly `CRC_DB` (§7). Check the console log via App Service Logs. |
| Login page loads but every DB action 500s | Wrong `CRC_DB` value, or (Managed Identity) you skipped `GRANT EXECUTE` / creating the external user (§7.1 B). |
| Everything works but the audit trail shows no user | Managed Identity without `GRANT EXECUTE` — the `sys.parameters` lookup can't see the procedures, so `@User_ID` is never injected (§7.1 B). |
| Dashboards show yesterday's data in the morning | `WEBSITE_TIME_ZONE` not set (§7.2). Set it to `Singapore Standard Time`. |
| Document upload timestamps are 8 hours behind | `GETDATE()` in the two document insert procs; Azure SQL is UTC. **Already fixed in this repo** — both procs use `GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time'`. If you see it, you are running an old build. |
| **Document upload fails / 500 on upload** | One of the two §10.4 app settings. Check, in this order: `DocumentStorage__ContainerName` matches the container name **exactly** (`nucentra-documents`); the name uses **two** underscores; `DocumentStorage__ConnectionString` was pasted whole and unbroken. Then confirm **Enable storage account key access** is still On (§10.1). |
| **Document link does nothing / "Cannot generate a SAS URL"** | The storage client has no account key, so it cannot sign a SAS. Either the connection string was replaced with something key-less, or **storage account key access** was turned Off on the storage account (§10.1). |
| Upload rejected: "not an allowed file type" | Working as designed — only `.pdf .png .jpg .jpeg .docx` up to 20 MB, and **both** the extension and the browser-reported content-type must match. To change the list, edit `CRC.Web/Infrastructure/DocumentValidation.cs` and redeploy; there is no portal setting for it. |
| Old `/uploads/...` links still work | The new build is not deployed yet. Once it is, that whole path returns 404 regardless of what is on disk (§10.7 step 6). If it still resolves, you are on the old build — re-check §10.5. |
| Logs disappeared after a deployment | **"Remove additional files at destination" was ticked** (§8.4). That is expected if you deliberately used §10.6 Route B, and a mistake otherwise. Untick it before the next publish. Documents are unaffected — they are in Blob storage. |
| Can't log in at all — login form posts and returns you to the form | The auth and `__Host-CSRF` cookies are `Secure`-only. You must use the `https://` URL, never `http://`. |
| Users get logged out randomly | You scaled to 2+ instances without shared Data Protection keys (§13). |
| Site shows a generic error page, no detail | Turn on **App Service Logs** + **Application Insights** (§11), or read `site/wwwroot/Logs` via Kudu. |
| HEART restarted when you deployed nucentra | The two apps are sharing an App Service Plan. Check §12.4 and move nucentra to `asp-nucentra-prod`. |

---

## 16. Order-of-operations summary (the whole thing on one page)

**A. Fresh deployment, from nothing:**

1. Create **resource group `rg-nucentra-prod`** + **SQL server `nucentra-sql-prod`** + **`CRC_DB`**
   in **Malaysia West** (open firewall for your IP + Azure services). — §4
2. In VS, set `CRC.Database` **target platform = Microsoft Azure SQL Database**, rebuild with MSBuild,
   **Publish** the DACPAC. Verify the seed: 3,242 locations, 1 SUPERUSER. — §5
3. Create **App Service `nucentra-web-prod`** on a **NEW plan `asp-nucentra-prod`** (Basic B1,
   **.NET 8**, Windows). **Do not reuse HEART's plan.** — §6
4. Create **storage account `nucentrastorprod`** + **private container `nucentra-documents`**; copy the
   connection string. — §10.1–§10.3
5. In App Service **Environment variables**, set the connection string named exactly **`CRC_DB`**, the
   app setting **`WEBSITE_TIME_ZONE = Singapore Standard Time`**, and the two **`DocumentStorage__*`**
   settings. — §7 and §10.4
6. In VS, **Publish CRC.Web** to `nucentra-web-prod`, with **"Remove additional files at destination"
   UNCHECKED**. — §8
7. Log in as `SUPERUSER` / `ChangeMe!123`, **change the password immediately** (nothing forces you to),
   then Branch → Staff → document settings → upload test. — §9
8. Run the two verification tests in **§10.7** before sharing the URL. Add IP access restrictions if the
   portal is internal-only.
9. Turn on **Application Insights**; confirm HEART still works and pin both to a dashboard. — §11–12
10. Later: plan the **.NET 10** upgrade before **10 Nov 2026**, harden with Key Vault and Managed
    Identity (for **both** SQL and Blob). — §13

**B. Retrofitting the document storage onto a site that is already live** — this is the path you are on
if the site went up before this change. Interleaved with `DocumentStoragePlan.md`:

| # | Do | Where |
|---|---|---|
| 1 | Prompt 1 — the storage service, the validator, config | `DocumentStoragePlan.md` |
| 2 | 🔵 Create the storage account, the private container, copy the connection string, add the two app settings | **§10.1–§10.4** |
| 3 | Prompts 2 → 6 — the whole code cutover, verified locally against Azurite | `DocumentStoragePlan.md` |
| 4 | 🔵 Re-publish the database, then the web app | **§10.5** |
| 5 | 🔴 Check for and delete the old `wwwroot/uploads` files on the App Service | **§10.6** |
| 6 | 🔴 Verify on the live site — both URL tests | **§10.7** |

**Connection string is `CRC_DB` (not `DefaultConnection`); the container is `nucentra-documents` (not
`patient-documents`); runtime is .NET 8 (not .NET 10).** Those three are where this differs most from
the HEART guide, and where a habit from that deployment will bite you here.

---

### Sources
- [Azure App Service — .NET version support and platform updates](https://learn.microsoft.com/en-us/azure/app-service/configure-language-dotnetcore)
- [.NET support policy — .NET 8 LTS end of support 10 Nov 2026](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
- [Deploy a DACPAC to Azure SQL Database using Visual Studio — SQLServerCentral](https://www.sqlservercentral.com/articles/deploy-dacpac-to-azure-sql-database-using-visual-studio)
- [SSDT target platform / Azure SQL Database schema provider — Microsoft Learn](https://learn.microsoft.com/en-us/sql/ssdt/how-to-specify-a-target-platform-for-a-database-project)
- [Securely connect .NET apps to Azure SQL using Managed Identity — Microsoft Learn](https://learn.microsoft.com/en-us/azure/app-service/tutorial-connect-msi-sql-database)
- [Metadata visibility in SQL Server (`sys.parameters` and permissions) — Microsoft Learn](https://learn.microsoft.com/en-us/sql/relational-databases/security/metadata-visibility-configuration)
- [Set the time zone for an App Service app (`WEBSITE_TIME_ZONE`) — Microsoft Learn](https://learn.microsoft.com/en-us/azure/app-service/faq-app-service-linux)
- [Azure App Service — operating system functionality and the file system](https://learn.microsoft.com/en-us/azure/app-service/operating-system-functionality)
- [Set up static IP / access restrictions in Azure App Service — Microsoft Learn](https://learn.microsoft.com/en-us/azure/app-service/app-service-ip-restrictions)
- Repo references: `SEEDING.md`, `CRC.Data/Database/DatabaseHelper.cs`, `CRC.Web/Program.cs`, `CRC.Database/Scripts/Seed_Users.sql`
