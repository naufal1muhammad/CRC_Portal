# Database Seeding

`CRC.Database` seeds itself. Publishing the project against an empty database produces a portal you can log
into immediately — every dropdown populated, no SSMS step, no CSV import, no manual `INSERT`.

---

## ⚠️ First login — change this password immediately

| | |
|---|---|
| **Username** | `SUPERUSER` |
| **Password** | `ChangeMe!123` |

**Change it the moment you first log in, via `Account > Change Password` (`/Account/ChangePassword`).**

`dbo.Users` has no `MustChangePassword` column, so **nothing in the application forces this**. This password
is public — it is written in plain text in this file and in `Scripts/Seed_Users.sql`, both of which are in
source control. A published database is reachable by anyone who can reach the site.

Changing the password is permanent and safe: re-publishing never resets it. The seed is guarded on
`Username`, so a later publish sees the row already exists and skips it.

---

## What publishing does

Right-click **CRC.Database → Publish** in Visual Studio, or run SqlPackage directly:

```bash
"C:/Program Files/Microsoft Visual Studio/18/Insiders/Common7/IDE/Extensions/Microsoft/SQLDB/DAC/SqlPackage.exe" /Action:Publish /SourceFile:CRC.Database/bin/Debug/CRC.Database.dacpac /TargetServerName:localhost /TargetDatabaseName:CRC_DB /TargetTrustServerCertificate:True
```

Either route deploys the schema and then runs the post-deployment script, which seeds the lookup tables, the
location tree, the bootstrap SUPERUSER and the `AGENT_SERVICE` account.

The post-deployment script runs on **every** publish, not just the first. Every seed is idempotent, so a
second publish over a populated database inserts nothing and fails nothing. SSDT inlines the `:r` includes
into the `.dacpac` at **build** time, so the deployed server never needs the seed files on disk.

## File map

| File | Role |
|---|---|
| `CRC.Database/Scripts/Script.PostDeployment.sql` | The one `<PostDeploy>` item SSDT allows; `:r`-includes the three seeds in order |
| `CRC.Database/Scripts/Seed_Lookups.sql` | The eleven small `LU_*` tables, guarded per row |
| `CRC.Database/Scripts/Seed_Location.sql` | `dbo.LU_LOCATION`, generated, guarded whole-table |
| `CRC.Database/Scripts/Seed_Users.sql` | Two accounts — the bootstrap SUPERUSER and the `AGENT_SERVICE` actor — each guarded on `Username` |
| `CRC.Database/Scripts/Tools/New-SeedLocation.ps1` | Regenerates `Seed_Location.sql` from the CSV; never runs during a publish |

## What is seeded

| Table | Rows | Contents |
|---|---:|---|
| `LU_DISCHARGETYPE` | 4 | Discharge outcomes |
| `LU_MARITALSTATUS` | 3 | Marital status |
| `LU_OCCUPATION` | 8 | Occupation groups |
| `LU_ORGANIZATION` | 6 | Referring organisations |
| `LU_PATDOCUMENTTYPE` | 13 | Patient document catalogue |
| `LU_PJ_APP_TYPE` | 4 | Patient-journey appointment types |
| `LU_RACE` | 11 | Race |
| `LU_RELIGION` | 6 | Religion |
| `LU_SOURCE` | 9 | How the patient reached the centre |
| `LU_STAFFDOCUMENTTYPE` | 8 | Staff document catalogue |
| `LU_STAFFTYPE` | 5 | Staff types (three-letter ids) |
| **Eleven lookups total** | **77** | |
| `LU_LOCATION` | 3,242 | 16 states + 442 cities + 2,784 postcodes |
| `Users` | 2 | The SUPERUSER above (`User_Type = 1`, `Staff_ID` NULL) and `AGENT_SERVICE` (`User_Type = 2`, `Staff_ID` NULL) |

After a publish, `IDENT_CURRENT('dbo.LU_LOCATION')` sits at 3242, so the next location created through the
UI gets 3243 and does not collide.

### The `AGENT_SERVICE` account

The second `dbo.Users` row is a **machine account, not a person**: it is the audit actor every write the
Agent API makes is attributed to, and it has no other purpose. An API-key request arrives with no cookie and
therefore no principal, so without this row every appointment the agent books would be audited as
`AuditTrails.User_Id = 0` — silently, with no error.

**It has no usable password.** The hash in `Seed_Users.sql` covers a random secret that was generated once
and thrown away, so unlike the SUPERUSER row a few lines above it cannot be logged into by anyone, and the
plaintext is not recoverable. The account is resolved **by username**, never by id — `User_ID` is
`INT IDENTITY` and differs between a local `CRC_DB` and Azure SQL, so nothing stores the number.

**Deleting the row breaks every agent API call with a `503`, by design** — the alternative is a corrupt audit
trail. The guard on `[Username]` means a re-publish never re-seeds it; if it is deleted, the next publish
recreates it with a new random password nobody knows, which is fine, because nothing depends on its password
or its id. See [`CoreFlow.md`](CoreFlow.md) §13.3.

## What is deliberately NOT seeded

`dbo.Branch`, `dbo.Staff`, `dbo.PatientDocumentSettings`, `dbo.StaffDocumentSettings`, every `Patient*`
table, `dbo.StaffSlots` and `dbo.AuditTrails`.

These hold operational or per-site configuration data — one installation's branches and staff are not the
product's shipped defaults. Create them through the portal's own admin screens after the first login, in
this order:

1. Log in as `SUPERUSER`.
2. **Change the password.**
3. Create a **Branch**.
4. Create **Staff** (a `User_Type = 3` account requires a staff record to point at).
5. Configure the **document settings** — patient documents per discharge type, staff documents per staff type.

The documents themselves are not stored in this database — only their metadata and a blob key. See
[`DOCUMENTSTORAGE.md`](DOCUMENTSTORAGE.md) for where the files actually live and what to configure before the
first upload works.

## Adding a lookup value later

A publish only ever **INSERTs missing rows**. It never updates and never deletes an existing one.

So adding a value to a live installation takes **two** steps:

1. Add the row to `Scripts/Seed_Lookups.sql`, so every future publish carries it.
2. Insert it into the live database **by hand**.

Doing only step 1 leaves the live database unchanged. The same applies to correcting a name that is already
in a live database — editing the seed alone will not change it there.

## Reloading `LU_LOCATION`

`LU_LOCATION` is guarded **whole-table**: `IF NOT EXISTS (SELECT 1 FROM [dbo].[LU_LOCATION])`. If the table
holds even one row, a publish skips the entire seed. A partially populated table is therefore never repaired
by publishing.

To reload it:

```sql
DELETE FROM [dbo].[LU_LOCATION];
```

then publish again. (The `DELETE` fails while any row is still referenced — correct behaviour, since a
patient address pointing at a `LocationId` must not be orphaned.)

Alternatively, use the fully commented-out `BULK INSERT` / `bcp` block in the head of `Seed_Location.sql`,
which re-imports the CSV without re-publishing. Note that `BULK INSERT` reads the file from the **SQL Server
machine**, not the client, and does not work against Azure SQL at all — which is why the publish path uses
inline `INSERT`s instead.

If the postcode list itself is revised, replace `reformatted_Malaysia_Postcode-postcodes.csv` at the
repository root and re-run the generator rather than hand-editing 3,242 rows:

```bash
pwsh -File CRC.Database/Scripts/Tools/New-SeedLocation.ps1
```

## Build gate

`CRC.Database` is a classic SSDT `.sqlproj` — **`dotnet build` cannot build it.** Use MSBuild from the
Visual Studio install:

```bash
"C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
```

Expected: `Build succeeded.`, `0 Error(s)`, and exactly **two** warnings:

```
SQL71502: Procedure: [dbo].[spStaffSlots_CreateRange] has an unresolved reference to object [sys].[all_objects].
```

at lines 46 and 52 of `Stored Procedures/StaffSlots/spStaffSlots_CreateRange.sql`. **These are the baseline.**
They pre-date the seeding work, they do not affect the deployed procedure, and they are not a sign of a
broken build — don't chase them.

A build error mentioning a missing file or a SQLCMD variable means a `:r` line in
`Script.PostDeployment.sql` points at a file that isn't there.
