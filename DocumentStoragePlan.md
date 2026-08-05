# CRC Portal — Document Storage Plan (patient and staff documents move to a private Azure Blob container)

This file slices **getting every uploaded document out of `wwwroot/uploads/` and into a private Azure Blob
container** into an ordered series of **6 self-contained prompts**. Each prompt is meant to be pasted into a
**fresh chat** of an AI coding tool that has **no memory** of the earlier prompts. Run them **in order**;
each one assumes every earlier prompt is finished and builds on it.

> ## 🔵 The division of labour: prompts do code, you do Azure
>
> **No prompt in this file touches the Azure portal, and none of them may.** Every prompt is confined to
> source code in this repository, verified locally against the **Azurite** emulator and a **scratch**
> database. The AI never creates a resource, never changes an app setting, never deletes a file on the
> App Service, and never connects to anything of yours in Azure.
>
> **Everything you do by hand in the portal lives in
> [`Nucentra_Azure_Deployment_Guide.md` §10](Nucentra_Azure_Deployment_Guide.md).** That section is
> written click-by-click and is yours to work through. This file tells you *when* to go there; §10.0
> tells you *what* to do when you arrive.
>
> The two interleave at exactly two points, both marked **🔵 GO TO THE GUIDE** below:
>
> ```
> Prompt 1  ──▶  🔵 GUIDE §10.1–§10.4   (storage account, container, app settings)
>                        │
>                        ▼
> Prompts 2 → 3 → 4 → 5 → 6            (all code, all local, no Azure)
>                        │
>                        ▼
>               🔵 GUIDE §10.5–§10.7    (publish, delete the old files, verify)
> ```

When all six are done, a document uploaded from **Patient → Edit → Documents** is written to a **private**
Blob container. The database stores only a **blob key**, never a URL. The browser gets the file through a
**5-minute read SAS** minted on click by an authenticated endpoint. Pasting an old
`https://…/uploads/patient/{guid}.pdf` URL returns **404** whether you are logged in or not, and nothing in
the app ever writes a byte under `wwwroot` again.

---

## The hole, precisely

You found it by hand: upload a document on `Patient/Edit`, copy the link, log out, paste it — the PDF opens.
Here is the full blast radius, because it is wider than the one page you tested.

`Program.cs:150` calls `app.UseStaticFiles()`. Static-file serving sits **before** `UseAuthentication()` in the
pipeline and performs **no authorisation check at all** — it cannot, it does not know what a user is. Uploads
are written **inside `wwwroot`**, so every uploaded file is a public static asset by construction:

| Where files are written | Written by | Public URL shape |
|---|---|---|
| `wwwroot/uploads/patient/` | `StaffPatientController.cs:964-1007` (`UploadPatientDocuments`) | `/uploads/patient/{guid}_{original name}.pdf` |
| `wwwroot/uploads/staff/` | `StaffController.cs:453-583` (`SaveStaffWithDocuments`) | `/uploads/staff/{guid}_{original name}.pdf` |
| `wwwroot/uploads/staff/` | `StaffController.cs:1065-1106` (`UploadStaffDocuments`) | same |

and four separate places render that path straight into an `<a href>`:

| File | Line | Page |
|---|---|---|
| `wwwroot/js/patient/edit-documents.js` | 126 | Patient → Edit → Documents (ADMIN / SUPERUSER) |
| `wwwroot/js/staffPatient/documents.js` | 126 | StaffPatient → Details → Documents (STAFF) |
| `wwwroot/js/staff/edit-staffbasic.js` | 614 | Staff → Edit → Documents |
| `wwwroot/js/documents/index.js` | 226 | Documents search page (SUPERUSER) |

**So it is not only patient documents.** Staff CVs, NRIC scans, MMC registration certificates and malpractice
indemnity memberships are readable by URL on exactly the same terms. That is why this plan covers both.

**Three further facts that make the fix bigger than "change where the file is written":**

1. **31 uploaded PDFs are committed to git.** `git ls-files CRC.Web/wwwroot/uploads` returns 19 patient files
   (`PERSONAL IDENTIFICATION.pdf`, `iFOBT RESULTS.pdf`, `PDPA FORM.pdf`, `COLONOSCOPY CONSENT FORM.pdf`, …)
   and 12 staff files. `.gitignore` has no rule for `wwwroot/uploads`, so every file anyone ever uploaded on a
   developer machine got committed. They are all ~15 KB and look like the same placeholder PDF renamed, but
   **confirm that yourself** before deciding how far to go (Prompt 5 handles this and flags the history
   question rather than deciding it for you).

2. **The files already on the Azure App Service will still be there after the code change.** The deployment
   guide correctly tells you to leave *"Remove additional files at destination"* **unchecked** — which means a
   publish never deletes anything. Every document uploaded to the live site since go-live is sitting in
   `site\wwwroot\wwwroot\uploads\` and will keep being served by `UseStaticFiles()` no matter what the new
   code does. **You** delete them by hand at 🔵 **AZURE B**
   ([guide §10.6](Nucentra_Azure_Deployment_Guide.md)) — the guide gives two non-Kudu routes. Prompt 5 adds a
   middleware block so a leftover file is unreachable even if a delete is missed, but the block is a backstop,
   not a substitute: the files are still physically there until you remove them.

3. **There is no upload validation anywhere in nucentra.** No extension check, no content-type check, no size
   cap. Any file of any size, straight to disk, under `wwwroot`. An uploaded `.html` or `.svg` would be served
   as an active document from your own origin. This plan adds validation as part of the move, matching HEART.

---

## What is being built (executive summary)

1. **A storage service, copied in shape from HEART.** `CRC.Web/Services/` gains `IDocumentStorage.cs`,
   `AzureBlobDocumentStorage.cs` and `DocumentStorageOptions.cs` — `UploadAsync`, `GetReadSasUrl`,
   `DeleteAsync` over a **private** container, DI-registered as a singleton in `Program.cs` and bound to a
   `DocumentStorage` config section. This is the same arrangement as
   `HEART/HEART.Web/Services/AzureBlobDocumentStorage.cs`, and the prompts point at it as the reference.

2. **A shared validator**, `CRC.Web/Infrastructure/DocumentValidation.cs`. HEART keeps its validation private
   inside `FeedPatientController` because HEART has exactly one upload endpoint. **nucentra has three**, in two
   controllers, so the rules live in one shared place instead of being copy-pasted three times and drifting.

3. **One private container, two prefixes.**

   | | Blob key | Example |
   |---|---|---|
   | Patient | `patients/{Patient_ID}/{guid:N}{ext}` | `patients/PAT-000042/9f1c…d3.pdf` |
   | Staff | `staff/{Staff_ID}/{guid:N}{ext}` | `staff/END-00003/4b7a…91.pdf` |

   `Patient_ID` is `PAT-000042` (`spPatientBasic_Insert`) and `Staff_ID` is `END-00003` (`spStaff_Insert`) —
   both are safe blob-path segments, so the key is human-readable in Storage Explorer and files group by owner.

4. **The two document tables rename `FilePath` → `BlobName`.** The column stops lying: it holds a container-
   relative key, not a URL path. `VARCHAR(500)` is unchanged (the longest possible key is ~145 characters).
   **11 SQL files** name that column today and every one of them is updated in a single prompt, because a
   half-renamed schema is worse than either end of the change.

5. **Downloads go through a 5-minute read SAS**, exactly as HEART does. Each list row renders a link that, on
   click, calls an authenticated `…DocumentUrl` action; that action reads the row, mints
   `GetReadSasUrl(blobName, TimeSpan.FromMinutes(5))` and returns the URL as JSON. The URL is never persisted
   and never appears in the rendered HTML.

6. **`/uploads/**` is hard-blocked.** A short `app.UseWhen(...)` branch **before** `UseStaticFiles()` returns
   404 for any path under `/uploads`, so files stranded on an App Service disk by an earlier deployment become
   unreachable regardless of whether anyone remembered to delete them.

7. **One new operator document**, `DOCUMENTSTORAGE.md`, written by Prompt 6.
   `Nucentra_Azure_Deployment_Guide.md` has **already been rewritten** — its §10 used to describe this hole
   as an accepted risk and now carries the full portal walkthrough instead, and the dozen other passages
   claiming nucentra has no storage account are fixed. **No prompt edits that guide**; it is the document you
   follow while the prompts run.

---

**Owner decisions already locked (do not re-open):**

- **Scope is patient AND staff documents.** Both live under `wwwroot/uploads`, both are equally exposed, and
  the SUPERUSER Documents search page lists them side by side. Fixing one and not the other would leave that
  page handing out working public links for half its rows.
- **Downloads use a short-lived SAS URL — HEART parity.** `DocumentUrl` mints a **5-minute** read SAS and the
  browser fetches straight from Blob. **Understand the one consequence:** inside that five-minute window the
  minted URL *does* open without a session, so if you repeat your paste-the-URL test with a freshly minted SAS
  it will still work. That is by design and is a very different thing from today's permanent public URL — the
  old link is dead the moment the window closes, and the link in the page is no longer the file's address. If
  you would rather no URL ever works without a cookie, the alternative is to stream the bytes through an
  authenticated action instead; say so and this plan changes in exactly one place (the `…DocumentUrl` actions
  become `…DownloadDocument` actions returning `FileStreamResult`).
- **Local development uses the Azurite emulator**, like HEART: `appsettings.json` ships
  `"ConnectionString": "UseDevelopmentStorage=true"`. No Azure resource is needed to run or debug nucentra
  locally, and no real patient file leaves your machine during development.
- **The database column is renamed `FilePath` → `BlobName`.** You are dropping and re-publishing the database
  from Visual Studio, so there is no migration and no data to preserve. **No prompt in this plan migrates or
  reads existing `FilePath` rows.**
- **Allowed file types match HEART exactly**: `.pdf`, `.png`, `.jpg`, `.jpeg`, `.docx`, **20 MB per file**,
  and **both** the extension and the reported content-type must be in their allowed sets so a renamed or
  spoofed file is rejected. Every file in a batch is validated **before** any blob is written.
- **One container, `nucentra-documents`**, with `patients/` and `staff/` prefixes. One
  `DocumentStorage__ContainerName` setting; one place to check access policy and lifecycle rules.
- **The storage account is reached by connection string**, as HEART does, not Managed Identity. Managed
  Identity for Blob needs `DefaultAzureCredential` plus a **user-delegation** SAS instead of an account-key
  SAS — a real code change, and the right second step, not the first. Prompt 6 records it as the top hardening
  item.

**Assumptions this plan bakes in (flagged so you can veto in review):**

- **`spPatient_DeleteCascade` gains a result set of blob keys.** Today it deletes the `PatientDocument` rows
  and returns nothing, so deleting a patient already orphans their files on disk — a live bug you have not hit
  yet. With Blob storage that orphan costs money forever *and* keeps patient data after the patient was
  deleted, which is the more serious half. The proc is changed to mirror `spStaff_Delete`, which already
  returns its file paths for exactly this reason, and `PatientController.DeletePatient` is changed to consume
  it. **This is the only behaviour change in the plan that is not strictly "move the file".**
- **`UploadStaffDocuments` and `DeleteStaffDocument` are migrated even though no page calls them.** Grepping
  every view and every `.js` file finds no caller — `staff/edit-staffbasic.js` does all its document work
  through `SaveStaffWithDocuments`. They are nonetheless **live, authenticated HTTP endpoints that write into
  `wwwroot`**, so leaving them behind would leave the hole open behind an unused door. If you would rather
  delete them outright, say so — it is a smaller change than migrating them, but it is a deletion and deletions
  are your call.
- **Document deletes stay HARD deletes.** HEART soft-deletes metadata (`IsDeleted = 1`) and hard-deletes the
  blob. nucentra's procs hard-delete the row and write an `AuditTrails` entry recording what was removed. That
  is a coherent design already; converting it to soft delete would touch every document proc and every list
  query for no benefit this change requires. **A blob delete that fails is logged, not fatal** — the row is
  already gone and the audit entry already stands.
- **No `SizeBytes` and no `UploadedBy_User_ID` column.** The uploader is already captured: `DatabaseHelper`
  auto-injects `@User_ID` from the caller's claims into every proc that declares it, and both document insert
  procs write it into `dbo.AuditTrails`. Adding columns is easy later; adding them now is scope you did not
  ask for.
- **`UploadedOn` is left exactly as it is.** Both insert procs already store Malaysia time via
  `GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time'`, so the deployment guide's §7.2
  caveat about 8-hour-behind timestamps is already fixed in this repo. Do not touch it.
- **The download is audited.** Minting a SAS for a patient record is a read of patient data and gets an
  `AuditLog.*` line on the `audit-*.log` channel, alongside the existing `DocumentSearched`. HEART does not log
  downloads; nucentra should, because its audit channel already exists and this is the single most
  sensitive read in the product.
- **`CRC.Database.sqlproj` already targets Azure SQL.** Line 10 reads `SqlAzureV12DatabaseSchemaProvider`, so
  the deployment guide's §5.1 retarget step is **already done** and no prompt here repeats it.
- **The two existing `SQL71502` build warnings are the baseline.** `spStaffSlots_CreateRange.sql` has
  unresolved references to `[sys].[all_objects]` at lines 46 and 52. They pre-date this work; do not chase
  them, and do not let them be read as "the build is broken".

---

## How to use this plan

1. Work top to bottom. Open a **new chat** for each prompt and paste the prompt's **copy block** (the fenced
   `text` block under each prompt) verbatim.
2. Every prompt re-orients the AI from scratch. **CRC_Portal carries no architecture briefs** (there is no
   `CoreFlow.md` / `FeatureBuildFlow_Brief.md` here — those belong to the sibling HEART repo, which several
   prompts do point at as a *reference implementation*), so each copy block states the conventions it needs.
3. **Build gates.** Two projects, two different builders:

   ```bash
   dotnet build CRC.Web/CRC.Web.csproj
   ```

   ```bash
   "C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
   ```

   `dotnet build` **cannot** build the classic SSDT `.sqlproj`. The database build is expected to report
   `Build succeeded.`, `0 Error(s)` and exactly **two** baseline `SQL71502` warnings.
4. **Scratch-database verification** uses the `SqlPackage.exe` that ships with the same Visual Studio install:
   `C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\Extensions\Microsoft\SQLDB\DAC\SqlPackage.exe`.
   Prompts 2 and 6 publish into a **scratch** database (`CRC_DB_BlobTest`) and drop it afterwards.
   **No prompt in this plan writes to the live `CRC_DB`.**
5. **Azurite must be running** from Prompt 1 onwards for any end-to-end check. It ships with Visual Studio:
   `"C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe"`,
   or `npx azurite` if you have Node. It listens on `127.0.0.1:10000` (blob). Prompt 1 checks this first and
   stops with instructions if it is not up.
6. **⚠️ After Prompt 2 and before Prompt 5, document screens are broken.** Prompt 2 renames the database
   column; the C# still sends `@FilePath` until Prompts 3 and 4 land. Everything compiles the whole way
   through and **nothing outside the document features is affected** — but do not deploy from a half-finished
   state. This is the deliberate cost of changing the schema atomically instead of in pieces.
7. **Azure is yours.** Two checkpoints below say **🔵 GO TO THE GUIDE**. Stop, do that part of
   `Nucentra_Azure_Deployment_Guide.md` §10 in the portal yourself, then come back. No prompt will do it for
   you, and every prompt is written to refuse if asked.
8. The last instruction in every prompt is **"mark this prompt complete in `DocumentStoragePlan.md`"** — after
   the AI finishes, this file's **Progress Tracker** and the prompt's **Status** line get ticked.

---

## Progress Tracker

Code steps are prompts; **🔵 blue steps are yours, in the Azure portal.**

- [x] **Prompt 1** — The storage service, the shared validator, config, and an Azurite round-trip proof
- [x] 🔵 **AZURE A** — Create the storage account + private container, add the two app settings
      → **[Guide §10.1–§10.4](Nucentra_Azure_Deployment_Guide.md)**
- [x] **Prompt 2** — The database: `FilePath` → `BlobName` across 2 tables and 10 procedures
- [x] **Prompt 3** — Patient documents on Blob (`StaffPatientController`, `PatientController.DeletePatient`, 2 JS)
- [x] **Prompt 4** — Staff documents on Blob (`StaffController`, 1 JS)
- [x] **Prompt 5** — The Documents search page, and closing the door on `wwwroot/uploads`
- [x] **Prompt 6** — Local end-to-end proof against Azurite, and `DOCUMENTSTORAGE.md`
- [ ] 🔵 **AZURE B** — Re-publish the database and the web app; delete the stranded files; verify live
      → **[Guide §10.5–§10.7](Nucentra_Azure_Deployment_Guide.md)**

---

## Coverage map (prompt → change)

| Prompt | What it delivers |
|---|---|
| 1 | `CRC.Web/Services/{IDocumentStorage,AzureBlobDocumentStorage,DocumentStorageOptions}.cs`; `CRC.Web/Infrastructure/DocumentValidation.cs`; `Azure.Storage.Blobs` package ref; DI + config binding in `Program.cs`; the `DocumentStorage` section in `appsettings.json`. Nothing consumes it yet — the app behaves exactly as before. Proven against Azurite with a throwaway console app. |
| 2 | `dbo/Tables/PatientDocument.sql` + `dbo/Tables/StaffDocument.sql` rename `FilePath` → `BlobName`; **9 procedures** updated (`spPatientDocument_Insert/List/Delete`, `spStaffDocument_Insert/List/GetById/Delete`, `spDocuments_Search`, `spStaff_Delete`); **1 new** procedure `spPatientDocument_GetById`; `spPatient_DeleteCascade` gains a blob-key result set. `.sqlproj` registers the new file. |
| 3 | `StaffPatientController`: `UploadPatientDocuments` validates then uploads to Blob, `GetPatientDocuments` stops returning a path, **new** `GetPatientDocumentUrl` mints the SAS, `DeletePatientDocument` deletes the blob. `PatientController.DeletePatient` deletes the patient's blobs. `js/patient/edit-documents.js` + `js/staffPatient/documents.js` fetch the SAS on click. New `AuditLog` methods. |
| 4 | `StaffController`: `SaveStaffWithDocuments`, `UploadStaffDocuments`, `DeleteStaffDocument`, `DeleteStaff` all move to Blob; `TryDeletePhysicalFile` / `CleanupCreatedFiles` / `GetWebRootPath` deleted; **new** `GetStaffDocumentUrl`. `js/staff/edit-staffbasic.js` fetches the SAS on click. |
| 5 | `DocumentsController` gains `DocumentUrl(mode, id)`; `js/documents/index.js` fetches the SAS on click. Then the door closes: `wwwroot/uploads/**` deleted and `git rm`'d, `.gitignore` rule added, the two `<Folder Include>` entries removed from `CRC.Web.csproj`, `IWebHostEnvironment` removed from the three controllers that no longer need it, and a middleware branch 404s `/uploads/**` before `UseStaticFiles()`. |
| 6 | A full local end-to-end proof — the site running against Azurite and a scratch database, driven over HTTP, asserting that a file lands in Blob, that nothing lands under `wwwroot`, that a SAS URL works and the same URL without its signature does not — and a new operator document, `DOCUMENTSTORAGE.md`. |
| 🔵 **A** | *(you, in the portal)* Storage account `nucentrastorprod`, private container `nucentra-documents`, connection string, the two `DocumentStorage__*` app settings — **[Guide §10.1–§10.4](Nucentra_Azure_Deployment_Guide.md)** |
| 🔵 **B** | *(you, in the portal)* Re-publish the database and the web app; find and delete the stranded `wwwroot/uploads` files on the App Service; run the two live URL tests — **[Guide §10.5–§10.7](Nucentra_Azure_Deployment_Guide.md)** |

**Dependency order:**

```
Prompt 1 ─→ 🔵 AZURE A ─→ Prompt 2 ─→ 3 ─→ 4 ─→ 5 ─→ Prompt 6 ─→ 🔵 AZURE B
            §10.1–§10.4                                          §10.5–§10.7
```

> **Prompt 1** is purely additive — stop after it and nothing has changed.
> **🔵 AZURE A** is deliberately early: those settings are inert until the new build ships, and the storage
> account name must be globally unique across all of Azure, so a clash with a stranger's account is far
> better discovered now than at the finish line.
> **Prompt 2** is the schema break; from there until **Prompt 5** the document screens throw and nothing else
> does. **Prompt 5** is the point at which the hole is closed *in the code*.
> **🔵 AZURE B** is the point at which it is closed *in reality* — until §10.6 is done, every document ever
> uploaded to the live site is still on the App Service disk and still public, no matter what the code says.

---

## Shared preamble (embedded in every prompt)

Every copy block tells the AI to:

- **Match nucentra's house style, not HEART's.** HEART is the *reference implementation* for the Blob work and
  several prompts name specific HEART files to read — but nucentra uses **block-scoped namespaces**
  (`namespace CRC.Web.Services { … }`), **ADO.NET + `DatabaseHelper` + stored procedures** (no Dapper, no
  inline SQL, ever), `Ok(new { success = …, message = … })` rather than `Json(...)`, `_logger.LogError` for
  operational detail and `AuditLog.*` for the security channel. Copy HEART's **shape**, never its code
  verbatim.
- **🔵 NEVER TOUCH AZURE.** Every prompt is confined to source code in this repository, verified against the
  local **Azurite** emulator and a **scratch** SQL database. Do not create, modify or delete any Azure
  resource; do not read or write App Service configuration; do not delete files on the App Service; do not
  use Kudu, FTPS, the Azure CLI, Azure PowerShell or `az` for anything. The owner performs every portal
  action themselves, following `Nucentra_Azure_Deployment_Guide.md` §10. **If a prompt appears to require an
  Azure action, stop and report it instead of doing it.**
- **Do not edit `Nucentra_Azure_Deployment_Guide.md`.** It has already been rewritten for this change and is
  the document the owner follows while these prompts run. If you find something in it that is wrong, report
  it; do not change it.
- **Never write a file under `wwwroot`.** That is the entire point of this work. If a prompt seems to need it,
  stop and report.
- **Change no unrelated schema and no unrelated procedure.** The tables and procedures each prompt may touch
  are listed explicitly in that prompt.
- **Keep every stored-procedure change registered in `CRC.Database/CRC.Database.sqlproj`** as
  `<Build Include="…" />`, in the existing `<ItemGroup>` layout, with nothing reordered or reformatted.
- **Respect `DatabaseHelper`'s `@User_ID` auto-injection.** It queries `sys.parameters` and adds `@User_ID`
  from the caller's `NameIdentifier` claim to any procedure that declares one. Never pass `@User_ID` by hand,
  and never remove it from a procedure that has it — the `AuditTrails` row depends on it.
- **Never write to the live `CRC_DB`.** Verification happens against a scratch database, dropped afterwards.
- **Use the right builder per project** — `dotnet build` for `CRC.Web`, MSBuild for the classic SSDT
  `CRC.Database.sqlproj` — and treat the two pre-existing `SQL71502` warnings in `spStaffSlots_CreateRange.sql`
  as the baseline.

---

# The Prompts

---

## Prompt 1 — The storage service, the validator, and an Azurite round-trip

**Status:** ✅ Done
**Depends on:** the existing project only

> **What exists before this prompt:** nucentra has no `Services` folder, no Azure package reference, and no
> `DocumentStorage` configuration. Every uploaded file goes to `wwwroot/uploads/`.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 8 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal; the web project is CRC.Web
(net8.0) and the data project is CRC.Data (ADO.NET, DatabaseHelper, stored procedures only — no EF, no
Dapper). There are NO architecture brief documents in this repo. A sibling repo, HEART, sits at
../HEART relative to this one and is the REFERENCE IMPLEMENTATION for the work below.

WHY: nucentra writes every uploaded patient and staff document into CRC.Web/wwwroot/uploads/** and stores
the web path in the database. Program.cs calls app.UseStaticFiles(), which runs BEFORE authentication and
performs no authorisation check, so every uploaded document is a permanently public URL — anyone holding
the link downloads patient records with no login at all. This has been confirmed by hand on the live Azure
deployment. The fix is to move documents into a PRIVATE Azure Blob container, exactly as HEART does.

THIS PROMPT IS PURELY ADDITIVE. You are building the storage service and the validator. NOTHING consumes
them yet, and no existing behaviour changes. Later prompts do the cutover.

READ FIRST (the reference implementation — copy the SHAPE, not the code):
  ../HEART/HEART.Web/Services/IDocumentStorage.cs
  ../HEART/HEART.Web/Services/AzureBlobDocumentStorage.cs
  ../HEART/HEART.Web/Services/DocumentStorageOptions.cs
  ../HEART/HEART.Web/Program.cs        (lines ~68-77: the Configure<> + AddSingleton registration)
  ../HEART/HEART.Web/appsettings.json  (the DocumentStorage section)
  ../HEART/HEART.Web/Controllers/FeedPatientController.cs (lines ~912-966: document validation)

READ ALSO, to match nucentra's own conventions:
  CRC.Web/Program.cs, CRC.Web/appsettings.json, CRC.Web/CRC.Web.csproj
  CRC.Web/Infrastructure/AuditLog.cs   (note: BLOCK-SCOPED namespaces, not file-scoped)

STYLE RULES FOR EVERY FILE YOU WRITE HERE:
• nucentra uses BLOCK-SCOPED namespaces — `namespace CRC.Web.Services { … }`. HEART uses file-scoped
  namespaces. Follow nucentra.
• XML doc comments on the interface and the options class, in the same explanatory voice HEART uses: say
  what the thing is FOR and why it is shaped that way, not just what it does.
• Nullable is enabled and ImplicitUsings is enabled in CRC.Web.csproj. Respect both.

YOUR TASK (Prompt 1).

1. ADD the package reference to CRC.Web/CRC.Web.csproj, in the existing PackageReference ItemGroup:

       <PackageReference Include="Azure.Storage.Blobs" Version="12.29.1" />

   That is the exact version HEART is on; keeping them identical means one known-good combination across
   both portals. Do NOT add Azure.Identity — this plan uses a connection string, not Managed Identity.

2. CREATE CRC.Web/Services/DocumentStorageOptions.cs
   A plain options class in namespace CRC.Web.Services with `public const string SectionName =
   "DocumentStorage";` and two string properties, ConnectionString and ContainerName, both defaulting to
   string.Empty. Doc comment: bound from the DocumentStorage config section; ConnectionString is
   "UseDevelopmentStorage=true" for the local Azurite emulator and the storage-account connection string in
   Azure App Service (set there as the app setting DocumentStorage__ConnectionString — the double
   underscore is how App Service expresses a colon).

3. CREATE CRC.Web/Services/IDocumentStorage.cs — the abstraction, three members:

       Task UploadAsync(Stream content, string blobName, string contentType);
       Uri  GetReadSasUrl(string blobName, TimeSpan ttl);
       Task DeleteAsync(string blobName);

   Doc comments must record the design, because it is the whole point of the change: documents live in a
   PRIVATE container; SQL holds only metadata and the blob key (dbo.PatientDocument.BlobName /
   dbo.StaffDocument.BlobName after Prompt 2); no blob bytes ever touch SQL; downloads are served through
   short-lived SAS URLs so the container never has to be public; a SAS URL is never persisted.

4. CREATE CRC.Web/Services/AzureBlobDocumentStorage.cs — the Azure implementation.
   • Constructor takes IOptions<DocumentStorageOptions>, builds a BlobServiceClient from the connection
     string and holds one BlobContainerClient. Comment that BlobServiceClient is thread-safe and meant to
     be reused, which is why the service is registered as a SINGLETON.
   • UploadAsync: `await _container.CreateIfNotExistsAsync(PublicAccessType.None);` first — a harmless
     no-op once the container exists, and it means a fresh Azurite install works with no manual
     provisioning step. Then upload with BlobUploadOptions carrying BlobHttpHeaders { ContentType }.
     PublicAccessType.None is load-bearing: it is what keeps the container private.
   • GetReadSasUrl: throw InvalidOperationException with a clear message if `!blob.CanGenerateSasUri` (that
     means the client has no account key — e.g. if someone later switches to Managed Identity without
     switching to a user-delegation SAS). Build a BlobSasBuilder with Resource = "b",
     StartsOn = UtcNow.AddMinutes(-5) to absorb clock skew, ExpiresOn = UtcNow.Add(ttl), and
     SetPermissions(BlobSasPermissions.Read) — READ ONLY, never Write or Delete.
   • DeleteAsync: DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots).

5. CREATE CRC.Web/Infrastructure/DocumentValidation.cs — a static class in namespace
   CRC.Web.Infrastructure. HEART keeps this logic private inside its one upload controller; nucentra has
   THREE upload endpoints across TWO controllers, so it lives in one shared place. Say that in the header
   comment. Members:

   • `public const long MaxDocumentBytes = 20L * 1024 * 1024;`  // 20 MB, matching HEART
   • A HashSet<string> of allowed EXTENSIONS (OrdinalIgnoreCase): .pdf .png .jpg .jpeg .docx
   • A HashSet<string> of allowed CONTENT TYPES (OrdinalIgnoreCase): application/pdf, image/png, image/jpeg,
     application/vnd.openxmlformats-officedocument.wordprocessingml.document
   • `public static (bool Ok, string? Message) Validate(IFormFile file)` — rejects an empty file, a file
     over the cap, and anything whose extension OR reported content-type is not in its allowed set. BOTH
     must pass, so a renamed or spoofed file is rejected. Messages name the file, e.g.
     "\"scan.exe\" is not an allowed file type. Only PDF, PNG, JPEG and DOCX are accepted."
   • `public static string SafeFileName(string? fileName)` — strips any path the browser included via
     Path.GetFileName, falls back to "file" when empty, and bounds the result to 255 characters.
     255 is NOT arbitrary: dbo.PatientDocument.FileName and dbo.StaffDocument.FileName are both
     VARCHAR(255), and today's code stores Path.GetFileName unbounded, which would throw on a long name.
     (HEART bounds to 260 because its column is NVARCHAR(260) — do not copy that number.)
   • `public static string BuildBlobName(string prefix, string ownerId, string originalFileName)` — returns
     $"{prefix}/{ownerId}/{Guid.NewGuid():N}{extension}" with the extension lower-cased. `prefix` is
     "patients" or "staff". Comment the two real id shapes so the reader knows the keys are readable:
     Patient_ID looks like PAT-000042 (spPatientBasic_Insert) and Staff_ID like END-00003 (spStaff_Insert).

6. REGISTER the service in CRC.Web/Program.cs, immediately after
   `builder.Services.AddScoped<CRC.Data.Database.DatabaseHelper>();`:

       builder.Services.Configure<DocumentStorageOptions>(
           builder.Configuration.GetSection(DocumentStorageOptions.SectionName));
       builder.Services.AddSingleton<IDocumentStorage, AzureBlobDocumentStorage>();

   with a lead comment saying what it is for and that the BlobServiceClient inside is reused, hence
   singleton. Add the `using CRC.Web.Services;` at the top with the existing usings.
   Do NOT touch the pipeline (UseStaticFiles, UseAuthentication, …) in this prompt — Prompt 5 does that.

7. ADD to CRC.Web/appsettings.json, as a new top-level section after ConnectionStrings:

       "DocumentStorage": {
         "ConnectionString": "UseDevelopmentStorage=true",
         "ContainerName": "nucentra-documents"
       },

   "UseDevelopmentStorage=true" is the well-known Azurite shorthand. Note in your report that Azure App
   Service overrides both at runtime via the app settings DocumentStorage__ConnectionString and
   DocumentStorage__ContainerName, so this file never carries a production secret.

VERIFY BEFORE YOU FINISH — do all four, in order, and report each:

  a) `dotnet build CRC.Web/CRC.Web.csproj` succeeds with 0 errors and 0 new warnings.

  b) AZURITE IS RUNNING. Check that 127.0.0.1:10000 accepts a connection (PowerShell:
     `Test-NetConnection -ComputerName 127.0.0.1 -Port 10000`). If it is NOT running, start it — it ships
     with Visual Studio at
     "C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe"
     (or use `npx azurite` if Node is available). Run it from your scratch directory, not the repo. If you
     genuinely cannot start it, STOP and report that clearly rather than skipping step (c).

  c) PROVE THE ROUND TRIP with a throwaway net8.0 console app in your SCRATCH directory (NOT in the repo),
     with a PackageReference to Azure.Storage.Blobs 12.29.1. It must, against
     "UseDevelopmentStorage=true" and container "nucentra-documents":
       1. create the container with PublicAccessType.None;
       2. upload a few bytes to blob key "patients/PAT-000001/selftest.pdf" with content type
          "application/pdf";
       3. mint a 5-minute read SAS for it and assert CanGenerateSasUri was true;
       4. HTTP GET the SAS URL with a plain HttpClient and assert 200 plus the exact bytes back;
       5. HTTP GET the blob URL WITHOUT the SAS query string and assert it FAILS (404 or 409 — the
          container is private, which is the property that matters). PRINT this result explicitly: it is
          the single most important assertion in this prompt.
       6. delete the blob and assert a fresh GET with a new SAS now 404s.
     Print each step's outcome. Then DELETE the throwaway app — nothing from it belongs in the repo.

  d) Confirm nothing under CRC.Web/wwwroot changed: `git status --short CRC.Web/wwwroot` is empty.

WHEN DONE: report the six assertion outcomes from (c) verbatim, confirm the throwaway app was deleted, and
list every file you created or edited. Then edit DocumentStoragePlan.md — tick the Prompt 1 box in the
Progress Tracker and set Prompt 1's Status to "✅ Done".

DO NOT touch anything in Azure — no resource, no app setting, no CLI. The owner does all of that by hand.
```

---

## 🔵 AZURE A — Create the storage account and wire up the Web App

**Status:** ✅ Done
**Do this after:** Prompt 1 · **In:** [`Nucentra_Azure_Deployment_Guide.md` §10.1 – §10.4](Nucentra_Azure_Deployment_Guide.md)

**This one is yours.** Nothing to paste into a chat — open the guide and work through four short sections in
the Azure portal:

| Guide § | What you do | Roughly |
|---|---|---|
| **§10.1** | Create the storage account `nucentrastorprod` in `rg-nucentra-prod`, Malaysia West | 5 min |
| **§10.2** | Create the container `nucentra-documents`, access level **Private** | 1 min |
| **§10.3** | Copy the storage connection string from **Access keys → key1** | 1 min |
| **§10.4** | Add `DocumentStorage__ConnectionString` and `DocumentStorage__ContainerName` to the Web App | 3 min |

**Three things the guide will make you get right, listed here so you know what to watch for:**

- The storage account name is **globally unique across all of Azure** and allows **no dashes**. If
  `nucentrastorprod` is taken, pick a suffix and use your version from then on.
- In §10.1's **Advanced** tab, turn **"Allow enabling anonymous access on individual containers"** *off* and
  leave **"Enable storage account key access"** *on*. The first one means nobody can ever make this container
  public by accident; the second is what lets the app sign SAS URLs at all — turning it off breaks every
  upload and every download.
- The app settings use **two** underscores. A single underscore is silently ignored.

**Nothing changes on your live site when you finish this**, and that is correct — the build currently
deployed does not read a `DocumentStorage` section at all. These settings sit inert until 🔵 **AZURE B**.

**Then come back here for Prompt 2.**

---

## Prompt 2 — The database: `FilePath` → `BlobName`

**Status:** ✅ Done
**Depends on:** Prompt 1

> **⚠️ This is the schema break.** After this prompt the solution still compiles, but every document screen
> throws at runtime until Prompts 3–5 land. Nothing outside the document features is affected.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 8 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal and the database project is
CRC.Database. There are NO architecture brief documents in this repo; everything you need is in this prompt.

WHAT'S ALREADY DONE (Prompt 1): CRC.Web/Services/ holds IDocumentStorage.cs, AzureBlobDocumentStorage.cs
and DocumentStorageOptions.cs, registered in Program.cs and bound to a DocumentStorage section in
appsettings.json (Azurite locally). CRC.Web/Infrastructure/DocumentValidation.cs holds the shared upload
rules. NOTHING consumes any of it yet — the app still writes files to wwwroot/uploads/**.

WHY THIS PROMPT: uploaded documents are moving from wwwroot/uploads/** to a private Azure Blob container,
because a file under wwwroot is served by UseStaticFiles() with no authorisation check and is therefore a
permanently public URL. The database currently stores a WEB PATH ('/uploads/patient/{guid}.pdf'); it must
store a BLOB KEY ('patients/PAT-000042/{guid}.pdf'). The column is renamed so it stops lying about what it
holds. The owner is dropping and re-publishing the database from Visual Studio, so there is NO DATA TO
MIGRATE and NO backward compatibility to preserve — do not write any migration, mapping or fallback.

YOUR TASK (Prompt 2) — rename the column everywhere, in ONE atomic change, and add one new read procedure.

Exactly ELEVEN existing files name this column. Find them yourself first (grep FilePath under
CRC.Database) and confirm the list matches this one before you start:

  dbo/Tables/PatientDocument.sql                              1 occurrence
  dbo/Tables/StaffDocument.sql                                1
  Stored Procedures/PatientDocument/spPatientDocument_Insert.sql   3
  Stored Procedures/PatientDocument/spPatientDocument_List.sql     1
  Stored Procedures/PatientDocument/spPatientDocument_Delete.sql   4
  Stored Procedures/PatientDocument/spDocuments_Search.sql         3
  Stored Procedures/StaffDocument/spStaffDocument_Insert.sql       3
  Stored Procedures/StaffDocument/spStaffDocument_List.sql         1
  Stored Procedures/StaffDocument/spStaffDocument_GetById.sql      1
  Stored Procedures/StaffDocument/spStaffDocument_Delete.sql       4
  Stored Procedures/Staff/spStaff_Delete.sql                       8

1. THE TWO TABLES. In dbo/Tables/PatientDocument.sql and dbo/Tables/StaffDocument.sql rename
   [FilePath] VARCHAR(500) NOT NULL  ->  [BlobName] VARCHAR(500) NOT NULL.
   KEEP VARCHAR(500) — the longest key this app can produce is about 145 characters
   ('patients/' + a VARCHAR(100) Patient_ID + '/' + 32 hex + '.jpeg'), so 500 is ample and resizing would
   be churn. Add a short comment above the column on both tables: it is the key WITHIN the private blob
   container, e.g. 'patients/PAT-000042/9f1c….pdf' or 'staff/END-00003/4b7a….pdf'; it is NOT a URL and NOT
   a filesystem path; the file itself is in Azure Blob Storage and is reached only through an authenticated
   endpoint that mints a short-lived read SAS.
   Change NOTHING else on either table — not FileName, not ContentType, not UploadedOn.

2. THE INSERT PROCEDURES. In spPatientDocument_Insert.sql and spStaffDocument_Insert.sql rename the
   parameter @FilePath -> @BlobName and the target column [FilePath] -> [BlobName].
   Leave the UploadedOn expressions EXACTLY as they are — both already store Malaysia time via
   `GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time'` and that is correct on Azure
   SQL, which runs in UTC. Leave the dbo.AuditTrails insert alone apart from nothing; it never mentioned
   the path. Leave @User_ID alone — CRC.Data/Database/DatabaseHelper.cs auto-injects it by inspecting
   sys.parameters, and removing it would silently lose audit attribution.

3. THE DELETE PROCEDURES. In spPatientDocument_Delete.sql and spStaffDocument_Delete.sql:
     • rename the local variable @FilePath -> @BlobName;
     • rename the OUTPUT parameter @DeletedFilePath -> @DeletedBlobName (keep VARCHAR(500) = NULL OUTPUT);
     • rename the source column in the SELECT.
   The audit block does not mention the path today; leave it as it is.
   Add a comment on the OUTPUT parameter: the caller uses this to delete the corresponding BLOB after the
   row is gone; a NULL means no row was deleted and nothing should be removed from storage.

4. THE READ PROCEDURES. In spPatientDocument_List.sql, spStaffDocument_List.sql and
   spStaffDocument_GetById.sql rename the selected column d.[FilePath] -> d.[BlobName]. Do NOT alias it
   back to FilePath — the callers are being updated in Prompts 3-5 and a lying alias would defeat the
   point. Change nothing else about these procedures (the COALESCE/UPPER/LTRIM joins stay exactly as they
   are).

5. spDocuments_Search.sql — BOTH branches. The PATIENT branch and the STAFF branch must return the SAME
   column names, so change both in this prompt:
       d.[FilePath] AS [FilePath]   ->   d.[BlobName] AS [BlobName]
   The 'neither mode' fallback branch at the bottom also declares the column shape
   (`CAST(NULL AS VARCHAR(500)) AS [FilePath]`) — rename that too, or the empty result set will not match
   the real ones.

6. spStaff_Delete.sql — this procedure ALREADY does the right thing and only needs renaming. It captures
   file paths into a @DocFiles table variable BEFORE deleting the StaffDocument rows and returns them as a
   SECOND RESULT SET so the caller can remove the files after the transaction commits. Rename the table
   variable's column, the SELECT, and the returned column name to [BlobName], including the
   `SELECT TOP 0 CAST(NULL AS VARCHAR(500)) AS [FilePath];` shape-declaring line in the early-return path.
   Update its comments to say "blob keys" rather than "file paths".

7. spPatient_DeleteCascade.sql — ADD the equivalent, because it is MISSING and that is a real bug.
   Today this procedure deletes the patient's dbo.PatientDocument rows and returns nothing at all, so
   deleting a patient already orphans their files. Once files are in Blob storage an orphan costs money
   forever AND keeps patient data after the patient was deleted, which is the more serious half.
   Mirror spStaff_Delete exactly:
     • BEFORE the `DELETE FROM [dbo].[PatientDocument]`, capture the keys:
           DECLARE @DocBlobs TABLE ([BlobName] VARCHAR(500));
           INSERT INTO @DocBlobs ([BlobName])
           SELECT [BlobName] FROM [dbo].[PatientDocument]
           WHERE [Patient_ID] = @Patient_ID
             AND [BlobName] IS NOT NULL AND LEN(LTRIM(RTRIM([BlobName]))) > 0;
     • at the very END of the procedure, after the AuditTrails insert, return them:
           SELECT [BlobName] FROM @DocBlobs;
   Comment it in the same voice as spStaff_Delete: captured before the delete so the caller can remove the
   blobs after the rows are gone. Do NOT restructure the rest of the procedure and do NOT wrap it in a
   transaction — that is a separate concern and not this change.

8. CREATE Stored Procedures/PatientDocument/spPatientDocument_GetById.sql — a NEW read procedure.
   dbo.StaffDocument has spStaffDocument_GetById but dbo.PatientDocument has no single-row read, and
   Prompt 3 needs one to mint a download SAS for one document. Model it on spStaffDocument_GetById.sql
   exactly — same TOP 1 shape, same LEFT JOINs, same COALESCE fallback on the type name:

       CREATE PROCEDURE [dbo].[spPatientDocument_GetById]
           @PatientDocument_ID INT
       AS ...
       SELECT TOP 1
           pd.[PatientDocument_ID], pd.[Patient_ID], pb.[Patient_Name],
           pd.[PatientDocumentType_ID],
           COALESCE(NULLIF(LTRIM(RTRIM(t.[PatientDocumentType_Name])), ''), pd.[PatientDocumentType_ID])
               AS [PatientDocumentType_Name],
           pd.[FileName], pd.[BlobName], pd.[ContentType], pd.[UploadedOn]
       FROM [dbo].[PatientDocument] pd
       LEFT JOIN [dbo].[PatientBasic] pb ON pb.[Patient_ID] = pd.[Patient_ID]
       LEFT JOIN [dbo].[LU_PATDOCUMENTTYPE] t ON UPPER(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], ''))))
                                               = UPPER(LTRIM(RTRIM(ISNULL(pd.[PatientDocumentType_ID], ''))))
       WHERE pd.[PatientDocument_ID] = @PatientDocument_ID;

   It is read-only, so it takes NO @User_ID and writes NO audit row (the DOWNLOAD is audited by the app on
   the Serilog audit channel in Prompt 3, not here). Header comment: it exists so the app can resolve one
   document's blob key and original filename in order to mint a short-lived read SAS.

9. REGISTER the new file in CRC.Database/CRC.Database.sqlproj as
       <Build Include="Stored Procedures\PatientDocument\spPatientDocument_GetById.sql" />
   next to the other spPatientDocument_* entries. Do not reorder or reformat any existing item.

DO NOT: touch any table other than the two named; touch any procedure other than the ten named; add
IsDeleted / SizeBytes / UploadedBy columns; change UploadedOn; change any FileName or ContentType column;
write any data-migration script; connect to or write to any database except the scratch one below; touch
ANYTHING in Azure (the owner created the storage account and set the app settings by hand between Prompt 1
and this one — you neither need nor may verify that); edit Nucentra_Azure_Deployment_Guide.md.

VERIFY BEFORE YOU FINISH:
  • `grep -ri "FilePath" CRC.Database` returns NOTHING. Zero occurrences. Report the command output.
  • Both build gates:
        "C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
    Expected: "Build succeeded.", 0 Error(s), and exactly TWO warnings — the pre-existing SQL71502
    unresolved [sys].[all_objects] references in Stored Procedures/StaffSlots/spStaffSlots_CreateRange.sql
    at lines 46 and 52. Those are the baseline; do not touch them. A NEW SQL71501/SQL71502 naming a
    document column means you missed a rename.
        dotnet build CRC.Web/CRC.Web.csproj
    still succeeds — the C# does not reference SQL column names at compile time, so this must stay green.
  • PUBLISH TO A SCRATCH DATABASE — never to CRC_DB:
        "C:/Program Files/Microsoft Visual Studio/18/Insiders/Common7/IDE/Extensions/Microsoft/SQLDB/DAC/SqlPackage.exe" /Action:Publish /SourceFile:CRC.Database/bin/Debug/CRC.Database.dacpac /TargetServerName:localhost /TargetDatabaseName:CRC_DB_BlobTest /TargetTrustServerCertificate:True /p:CreateNewDatabase=True
    Then assert with sqlcmd against CRC_DB_BlobTest and report a pass/fail line for each:
      - COL_LENGTH('dbo.PatientDocument','BlobName') and COL_LENGTH('dbo.StaffDocument','BlobName') are
        both 500, and COL_LENGTH(...,'FilePath') is NULL on both;
      - `SELECT COUNT(*) FROM sys.parameters WHERE name LIKE '%FilePath%'` is 0;
      - `SELECT COUNT(*) FROM sys.sql_modules WHERE definition LIKE '%FilePath%'` is 0;
      - dbo.spPatientDocument_GetById exists and has exactly one parameter, @PatientDocument_ID;
      - dbo.spPatientDocument_Delete and dbo.spStaffDocument_Delete each have a @DeletedBlobName parameter
        with is_output = 1.
    Then DROP DATABASE CRC_DB_BlobTest (set it SINGLE_USER WITH ROLLBACK IMMEDIATE first if it refuses) and
    confirm it is gone.

WHEN DONE: report the grep result, both build results, the assertion table, and confirm the scratch
database was dropped and CRC_DB was never touched. State plainly in your report that the document screens
are now broken at runtime until Prompts 3-5 land, and that nothing else in the portal is affected. Then
edit DocumentStoragePlan.md — tick the Prompt 2 box and set Prompt 2's Status to "✅ Done".
```

---

## Prompt 3 — Patient documents on Blob

**Status:** ✅ Done
**Depends on:** Prompts 1–2

> **The page the owner reported.** After this prompt, uploading on `Patient/Edit → Documents` writes to Blob
> and the list links go through a SAS. Staff documents are still on disk until Prompt 4.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 8 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal; the web project is CRC.Web
(net8.0). Data access is ADO.NET through CRC.Data/Database/DatabaseHelper.cs — STORED PROCEDURES ONLY, no
EF, no Dapper, no inline SQL, ever. There are NO architecture brief documents in this repo.

WHAT'S ALREADY DONE:
• Prompt 1 — CRC.Web/Services/{IDocumentStorage,AzureBlobDocumentStorage,DocumentStorageOptions}.cs exist,
  registered in Program.cs as a SINGLETON and bound to the "DocumentStorage" section of appsettings.json
  ("UseDevelopmentStorage=true" + container "nucentra-documents"; Azurite locally).
  CRC.Web/Infrastructure/DocumentValidation.cs holds MaxDocumentBytes (20 MB), the allowed extension and
  content-type sets (.pdf .png .jpg .jpeg .docx), Validate(IFormFile), SafeFileName(string?) and
  BuildBlobName(prefix, ownerId, originalFileName).
• Prompt 2 — dbo.PatientDocument.FilePath and dbo.StaffDocument.FilePath were RENAMED to BlobName, and
  every procedure that named the column was updated. dbo.spPatientDocument_GetById is NEW.
  dbo.spPatient_DeleteCascade now returns a SECOND RESULT SET of the deleted patient's blob keys
  (`SELECT [BlobName] FROM @DocBlobs;`), mirroring what dbo.spStaff_Delete has always done.
  The C# still sends @FilePath, so patient document screens are CURRENTLY BROKEN. This prompt fixes them.

WHY: documents used to be written into CRC.Web/wwwroot/uploads/patient/ and the web path stored in the
database. Program.cs calls app.UseStaticFiles(), which runs before authentication and does no authorisation
check, so those URLs were public forever — confirmed by hand on the live site. Files now go to a PRIVATE
Azure Blob container and are downloaded through a 5-minute read SAS minted by an authenticated endpoint.

READ FIRST:
  CRC.Web/Controllers/StaffPatient/StaffPatientController.cs   (the DOCUMENTS region, ~line 874 to the end)
  CRC.Web/Controllers/Patient/PatientController.cs             (DeletePatient, ~line 66)
  CRC.Web/wwwroot/js/patient/edit-documents.js
  CRC.Web/wwwroot/js/staffPatient/documents.js
  CRC.Web/Infrastructure/AuditLog.cs
  CRC.Web/Infrastructure/DocumentValidation.cs
  ../HEART/HEART.Web/Controllers/FeedPatientController.cs      (lines ~580-726 — the reference: Documents,
      DocumentUrl, UploadDocuments and DeleteDocument. Copy the SHAPE, not the code: HEART uses Dapper and
      Json(...); nucentra uses DatabaseHelper + stored procedures and Ok(new { success, message }).)

TWO NUCENTRA FACTS THAT WILL BITE YOU IF YOU MISS THEM:
• DatabaseHelper AUTO-INJECTS @User_ID. It queries sys.parameters and adds @User_ID from the caller's
  NameIdentifier claim to any procedure that declares one. NEVER pass @User_ID by hand — you would get a
  duplicate-parameter exception.
• Program.cs registers a GLOBAL AuthorizeFilter and a GLOBAL AutoValidateAntiforgeryTokenAttribute. Every
  action is authenticated by default and every non-GET needs the antiforgery token. The existing JS already
  sends it as the 'X-CSRF-Token' header. Keep explicit [Authorize(Policy = "…")] attributes on new actions
  anyway, to match the file's existing style.

YOUR TASK (Prompt 3).

1. StaffPatientController.cs — inject IDocumentStorage. Add `private readonly IDocumentStorage
   _documentStorage;` and a constructor parameter. REMOVE the `IWebHostEnvironment _env` field and its
   constructor parameter IF nothing else in the file uses it — check first; if something else does, leave
   it and say so in your report.

2. REWRITE UploadPatientDocuments (currently ~lines 943-1016). Keep the existing form signature
   (string patientId, string patientName, List<IFormFile> files, List<string> docTypeIds,
   List<string> docTypeNames) and the existing [Authorize(Policy = "AdminOrSuperOrStaff")] [HttpPost].
   ADD these attributes, and comment WHY (several 20 MB files must fit in one multipart body; the ASP.NET
   Core default of ~30 MB would reject a two-file batch outright):

       [RequestSizeLimit(120_000_000)]
       [RequestFormLimits(MultipartBodyLengthLimit = 120_000_000)]

   New body:
     a) reject a blank patientId and an empty file list, exactly as now;
     b) VALIDATE EVERY FILE FIRST, in its own loop, with DocumentValidation.Validate(file), and return
        Ok(new { success = false, message }) on the first failure. Comment why the loop is separate: a bad
        file in the batch must fail BEFORE any blob is written, so a rejected upload leaves nothing behind;
     c) then, per file: `var blobName = DocumentValidation.BuildBlobName("patients", patientId, file.FileName);`
        upload with `await using var stream = file.OpenReadStream();` then
        `await _documentStorage.UploadAsync(stream, blobName, file.ContentType);`
        then call spPatientDocument_Insert with @BlobName (not @FilePath) and
        @FileName = DocumentValidation.SafeFileName(file.FileName);
     d) after each successful insert, call a NEW AuditLog.PatientDocumentUploaded(...);
     e) wrap the upload loop in try/catch, log with _logger.LogError, and return the house-style
        Ok(ErrorResponse.ForUser(HttpContext, "…")) shape used elsewhere in this controller.
   Under NO circumstances construct a path, call Directory.CreateDirectory, File.Create or FileStream, or
   reference _env.WebRootPath. If you have written the word "wwwroot" you have made a mistake.

3. CHANGE GetPatientDocuments (~line 905) to STOP RETURNING A PATH. Drop `filePath` from the projection
   entirely — the browser must never see a storage key, and there is nothing it could do with one. Keep
   documentId, patientId, patientName, docTypeId, docTypeName, fileName, uploadedOn. Add a comment saying
   the file itself is fetched through GetPatientDocumentUrl.

4. ADD a new action GetPatientDocumentUrl:

       [Authorize(Policy = "AdminOrSuperOrStaff")]
       [HttpGet]
       public async Task<IActionResult> GetPatientDocumentUrl(int id)

   It calls spPatientDocument_GetById, returns Ok(new { success = false, message = "Document not found." })
   when there is no row, otherwise mints
   `_documentStorage.GetReadSasUrl(blobName, TimeSpan.FromMinutes(5))` and returns
   Ok(new { success = true, url = url.ToString(), fileName }).
   Call a NEW AuditLog.PatientDocumentDownloaded(...) BEFORE returning — minting a SAS for a patient record
   is a read of patient data and belongs on the audit channel.
   Comment the design plainly: the container is private; this five-minute URL is the ONLY way the browser
   reaches the bytes; it is never persisted and never rendered into the page's HTML.

5. CHANGE DeletePatientDocument (~line 1023) to delete the BLOB instead of a file. Rename the output
   parameter to @DeletedBlobName, and when it comes back non-empty call
   `await _documentStorage.DeleteAsync(blobName);` inside its OWN try/catch that logs a WARNING and
   CONTINUES. Comment why a failed blob delete is not fatal: the metadata row is already gone and the
   AuditTrails entry already stands, so failing the request would only confuse the user about what
   happened. Delete every System.IO call and the _env.WebRootPath path arithmetic. Add
   AuditLog.PatientDocumentDeleted(...).

6. PatientController.cs — DeletePatient must now delete the patient's blobs.
   spPatient_DeleteCascade returns a result set of blob keys as of Prompt 2, so switch the call from
   `_db.ExecuteNonQueryAsync` to `_db.ExecuteDataSetAsync`, and after it returns, loop the first table's
   [BlobName] rows calling `await _documentStorage.DeleteAsync(...)` best-effort — each in try/catch,
   logging a warning on failure, never failing the request. Inject IDocumentStorage into
   PatientController the same way, and remove its unused IWebHostEnvironment if nothing else uses it.
   Comment WHY: without this, deleting a patient leaves their documents in storage forever — which costs
   money and, far worse, retains patient data after the patient record was deleted.

7. AuditLog.cs — add four methods in the existing style (static, `_logger.Write(LogEventLevel.…, "AUDIT …
   {Placeholder}", value)`, message starting with "AUDIT "):
       PatientDocumentUploaded(HttpContext, string patientId, int documentId, string docTypeId,
                               string blobName, string fileName, long sizeBytes)   Information
       PatientDocumentDownloaded(HttpContext, string patientId, int documentId, string fileName)  Information
       PatientDocumentDeleted(HttpContext, string patientId, int documentId, string blobName)     Warning
       PatientDocumentsPurged(HttpContext, string patientId, int blobCount)                       Warning
   (the last one for the cascade delete in step 6). Deletions are Warning, matching how HEART treats
   destructive events; uploads and downloads are Information.

8. THE TWO JAVASCRIPT FILES — wwwroot/js/patient/edit-documents.js and wwwroot/js/staffPatient/documents.js.
   Both render the same list item and both currently do, at about line 121-131:

       const safePath = d.filePath || '#';
       ... <a href="${safePath}" target="_blank" rel="noopener noreferrer">${safeName}</a>

   Replace the href with a button-styled anchor carrying the document id, e.g.
   `<a href="#" class="pat-doc-open" data-id="${d.documentId}">${safeName}</a>`, and add a delegated click
   handler in the existing attachHandlers() function that:
     - preventDefault();
     - fetches `/StaffPatient/GetPatientDocumentUrl?id=` + encodeURIComponent(id) with
       headers { 'Accept': 'application/json' };
     - on `result.success`, `window.open(result.url, '_blank', 'noopener,noreferrer')`;
     - otherwise shows the failure through the file's existing showMessage(...) / alert(...) helper —
       match whichever that file already uses; do not introduce a new notification style.
   Add a short comment above the handler: the link is resolved at click time to a 5-minute read SAS, so no
   durable file URL is ever placed in the DOM.
   These two files are near-identical today; keep them near-identical after.
   Do NOT touch the upload or delete code paths in either file — they already post to the right endpoints
   and already send the X-CSRF-Token header.

DO NOT in this prompt: touch StaffController, DocumentsController, js/staff/*, js/documents/*, Program.cs's
pipeline, CRC.Web.csproj, or anything under wwwroot/uploads. Prompts 4 and 5 own those. Do NOT touch
anything in Azure and do NOT edit Nucentra_Azure_Deployment_Guide.md — the owner runs every portal step by
hand, and the storage account already exists because they created it after Prompt 1. You work against the
local Azurite emulator only.

VERIFY BEFORE YOU FINISH:
  • `dotnet build CRC.Web/CRC.Web.csproj` — 0 errors, 0 new warnings.
  • `grep -n "wwwroot\|uploads\|WebRootPath\|FileStream\|Directory.CreateDirectory" CRC.Web/Controllers/StaffPatient/StaffPatientController.cs CRC.Web/Controllers/Patient/PatientController.cs`
    returns NOTHING. Report the command output.
  • `grep -rn "filePath" CRC.Web/wwwroot/js/patient/ CRC.Web/wwwroot/js/staffPatient/` returns NOTHING.
  • `git status --short CRC.Web/wwwroot/uploads` is empty — you created no files there.

WHEN DONE: list every file you changed, paste the three grep results, and report whether
IWebHostEnvironment could be removed from each of the two controllers. Then edit DocumentStoragePlan.md —
tick the Prompt 3 box and set Prompt 3's Status to "✅ Done".
```

---

## Prompt 4 — Staff documents on Blob

**Status:** ✅ Done
**Depends on:** Prompts 1–3

> **The trickier controller.** `StaffController` writes files *inside a database transaction* and cleans them
> up on rollback. That whole mechanism has to be rethought for blobs, not just re-pointed.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 8 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal; the web project is CRC.Web
(net8.0). Data access is ADO.NET through CRC.Data/Database/DatabaseHelper.cs — STORED PROCEDURES ONLY, no
EF, no Dapper, no inline SQL. There are NO architecture brief documents in this repo.

WHAT'S ALREADY DONE:
• Prompt 1 — CRC.Web/Services/IDocumentStorage.cs (UploadAsync / GetReadSasUrl / DeleteAsync) over a
  PRIVATE Azure Blob container, registered as a singleton in Program.cs, bound to the "DocumentStorage"
  config section (Azurite locally, container "nucentra-documents").
  CRC.Web/Infrastructure/DocumentValidation.cs — MaxDocumentBytes (20 MB), allowed extensions
  (.pdf .png .jpg .jpeg .docx) and content types, Validate(IFormFile), SafeFileName(string?),
  BuildBlobName(prefix, ownerId, originalFileName).
• Prompt 2 — dbo.StaffDocument.FilePath was RENAMED to BlobName; spStaffDocument_Insert takes @BlobName;
  spStaffDocument_Delete outputs @DeletedBlobName; spStaffDocument_List and spStaffDocument_GetById select
  [BlobName]; spStaff_Delete's second result set column is now [BlobName].
• Prompt 3 — the same cutover was completed for PATIENT documents in StaffPatientController and
  PatientController, with new AuditLog.PatientDocument* methods and a GetPatientDocumentUrl action that
  mints a 5-minute read SAS. READ StaffPatientController.cs FIRST and mirror its shape — this prompt is
  the staff-side twin of that work and the two should read alike when you are done.

STAFF DOCUMENT SCREENS ARE CURRENTLY BROKEN (the C# still sends @FilePath). This prompt fixes them.

READ FIRST:
  CRC.Web/Controllers/StaffPatient/StaffPatientController.cs   (the finished patient-side pattern)
  CRC.Web/Controllers/Staff/StaffController.cs                 (all of it — it is long)
  CRC.Web/wwwroot/js/staff/edit-staffbasic.js
  CRC.Web/Infrastructure/DocumentValidation.cs, CRC.Web/Infrastructure/AuditLog.cs

THE FOUR PLACES IN StaffController THAT TOUCH FILES:
  ~453-583  SaveStaffWithDocuments — the hard one, see step 2
  ~678-684  GetWebRootPath()       — delete
  ~702-754  CleanupCreatedFiles() and TryDeletePhysicalFile()  — delete/replace, see step 2
  ~759-819  DeleteStaff            — consumes spStaff_Delete's second result set
  ~1041-1114 UploadStaffDocuments  — see step 4
  ~1123-1160 DeleteStaffDocument   — see step 4

YOUR TASK (Prompt 4).

1. Inject IDocumentStorage into StaffController and remove IWebHostEnvironment if nothing else uses it
   (check — GetWebRootPath is the only consumer today, and step 2 deletes it).

2. SaveStaffWithDocuments — the one that needs THINKING, not translating.
   Today it writes each file to disk INSIDE a SQL transaction, tracks the written paths in `createdFiles`,
   and deletes them in `CleanupCreatedFiles` if the transaction throws. Blob uploads are NOT transactional
   and cannot be rolled back by SQL, so that pattern has to be rebuilt rather than re-pointed. Keep the
   same guarantee — no orphaned storage when the save fails — with this ordering:

     a) VALIDATE every incoming file with DocumentValidation.Validate BEFORE the transaction opens, and
        return the existing failure shape on the first bad one. Nothing is written if the batch is bad.
     b) Compute each file's blobName with DocumentValidation.BuildBlobName("staff", staffId, …) and UPLOAD
        every file to Blob BEFORE opening the transaction, collecting the successful keys into a
        `uploadedBlobs` list. NOTE THE ORDERING PROBLEM AND SOLVE IT: for a NEW staff member the Staff_ID
        does not exist until spStaff_Insert has run inside the transaction. Handle it the straightforward
        way — open the transaction, run spStaff_Insert/spStaff_Update first to obtain staffId, and only
        then upload; keep a `uploadedBlobs` list as you go. Write a comment explaining the choice.
     c) Keep the existing "capture the paths of documents being deleted, delete the DB rows inside the
        transaction, remove the storage AFTER commit" pattern — it is already correct, only the storage
        call changes. Rename `deleteFilePaths` to `deleteBlobNames`.
     d) On any exception, roll the transaction back AND delete every key in `uploadedBlobs` best-effort
        (this replaces CleanupCreatedFiles). Each delete in its own try/catch, logged as a warning, never
        masking the original exception — rethrow or return the original failure, not the cleanup's.
     e) After a successful commit, delete the blobs in `deleteBlobNames` best-effort (this replaces the
        TryDeletePhysicalFile loop).
   Replace CleanupCreatedFiles and TryDeletePhysicalFile with ONE private async helper, e.g.
   `private async Task TryDeleteBlobsAsync(IEnumerable<string> blobNames)`, that swallows and logs. DELETE
   GetWebRootPath entirely. Add AuditLog.StaffDocumentUploaded / StaffDocumentDeleted calls mirroring the
   patient ones from Prompt 3.

3. DeleteStaff — spStaff_Delete's second result set now returns [BlobName], not [FilePath]. Change the
   column name in the DataRow read and swap TryDeletePhysicalFile for the new TryDeleteBlobsAsync. The
   overall shape (result set 1 = Status/Message, result set 2 = the keys, only on Success) is unchanged and
   already correct — do not restructure it. Add AuditLog.StaffDocumentsPurged(context, staffId, count).

4. UploadStaffDocuments and DeleteStaffDocument — migrate both.
   IMPORTANT CONTEXT: grepping every view and every .js file finds NO caller for either of them —
   js/staff/edit-staffbasic.js does all its document work through SaveStaffWithDocuments. They are
   nonetheless LIVE, AUTHENTICATED HTTP ENDPOINTS that currently write into wwwroot/uploads/staff, so
   leaving them behind would leave the security hole open behind an unused door. Migrate them to Blob
   exactly like their patient-side equivalents, add the [RequestSizeLimit(120_000_000)] and
   [RequestFormLimits(MultipartBodyLengthLimit = 120_000_000)] attributes to the upload one, and add a
   comment on each recording that no page currently calls it and that it is kept because it is reachable.
   Do NOT delete them in this prompt — deleting a public endpoint is the owner's call, not yours; note the
   observation in your report instead.

5. ADD GetStaffDocumentUrl, the twin of Prompt 3's GetPatientDocumentUrl:

       [Authorize(Policy = "AdminOrSuper")]
       [HttpGet]
       public async Task<IActionResult> GetStaffDocumentUrl(int id)

   calling spStaffDocument_GetById, minting a 5-minute read SAS, returning
   Ok(new { success = true, url, fileName }), and calling a new AuditLog.StaffDocumentDownloaded first.
   Note the policy difference from the patient side ("AdminOrSuper", not "AdminOrSuperOrStaff") — that is
   what every other staff-document action in this controller uses, and this must match it.

6. AuditLog.cs — add the staff counterparts in the same style and at the same log levels as Prompt 3's
   patient methods: StaffDocumentUploaded (Information), StaffDocumentDownloaded (Information),
   StaffDocumentDeleted (Warning), StaffDocumentsPurged (Warning).

7. wwwroot/js/staff/edit-staffbasic.js — at about line 609 it does
   `const safePath = d.filePath || '#';` and renders `<a href="${safePath}" …>`. Replace exactly as Prompt 3
   did on the patient side: an `<a href="#" class="staff-doc-open" data-id="${d.documentId}">` plus a
   delegated click handler that fetches `/Staff/GetStaffDocumentUrl?id=…` and window.open()s the returned
   URL, reporting failure through whichever message helper this file already uses. Do not touch the upload,
   the mandatory-document checks, or the delete-marking logic.

DO NOT in this prompt: touch StaffPatientController, PatientController, DocumentsController,
js/documents/*, js/patient/*, js/staffPatient/*, Program.cs's pipeline, or anything under wwwroot/uploads.
Do NOT touch anything in Azure and do NOT edit Nucentra_Azure_Deployment_Guide.md — the owner performs every
portal action by hand. You work against the local Azurite emulator only.

VERIFY BEFORE YOU FINISH:
  • `dotnet build CRC.Web/CRC.Web.csproj` — 0 errors, 0 new warnings.
  • `grep -n "wwwroot\|uploads\|WebRootPath\|FileStream\|Directory.CreateDirectory\|System.IO.File" CRC.Web/Controllers/Staff/StaffController.cs`
    returns NOTHING. Report the output.
  • `grep -rn "filePath" CRC.Web/wwwroot/js/staff/` returns NOTHING.
  • `git status --short CRC.Web/wwwroot/uploads` is empty.
  • Confirm GetWebRootPath, CleanupCreatedFiles and TryDeletePhysicalFile no longer exist anywhere in the
    repo: `grep -rn "GetWebRootPath\|CleanupCreatedFiles\|TryDeletePhysicalFile" CRC.Web --include=*.cs`.

WHEN DONE: describe how you solved the "Staff_ID does not exist until the transaction has run" ordering
problem in step 2b, paste the four grep results, and note that UploadStaffDocuments / DeleteStaffDocument
have no caller so the owner may want them deleted. Then edit DocumentStoragePlan.md — tick the Prompt 4 box
and set Prompt 4's Status to "✅ Done".
```

---

## Prompt 5 — The Documents search page, and closing the door

**Status:** ✅ Done
**Depends on:** Prompts 1–4

> **This is the prompt that actually shuts the hole.** Everything before it moved new uploads; this one
> removes the old files and makes `/uploads/**` unreachable for good.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 8 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal; the web project is CRC.Web
(net8.0). There are NO architecture brief documents in this repo.

WHAT'S ALREADY DONE (Prompts 1-4): patient and staff documents now go to a PRIVATE Azure Blob container
through CRC.Web/Services/IDocumentStorage.cs; dbo.PatientDocument and dbo.StaffDocument store a BlobName
key instead of a FilePath; StaffPatientController.GetPatientDocumentUrl and StaffController
.GetStaffDocumentUrl mint 5-minute read SAS URLs; the patient, staffPatient and staff document lists all
resolve their links on click. NOTHING in the app writes under wwwroot any more.

TWO THINGS ARE STILL OPEN, and this prompt closes both.

(A) The SUPERUSER Documents search page (/Documents) still renders raw stored values as hrefs.
(B) The FILES THEMSELVES are still there — 31 uploaded PDFs are committed to git under
    CRC.Web/wwwroot/uploads/, they are still on disk locally, and app.UseStaticFiles() will still serve
    anything found under that folder. Moving new uploads elsewhere did nothing about the old ones.

YOUR TASK (Prompt 5).

PART A — the Documents search page.

1. READ CRC.Web/Controllers/Documents/DocumentsController.cs and CRC.Web/wwwroot/js/documents/index.js, and
   read StaffPatientController.GetPatientDocumentUrl as the pattern to mirror.
   The page is [Authorize(Policy = "SuperUserOnly")] at CLASS level. spDocuments_Search returns a unified
   shape for both modes and, as of Prompt 2, its path column is [BlobName].

2. In DocumentsController.Search, DROP `filePath` from the projection entirely — the browser gets no key.
   Keep id, name, documentType, fileName, uploadedOn. (`id` is the Patient_ID / Staff_ID, NOT the document
   id — read the procedure and confirm this before you write the next step.)

3. spDocuments_Search does not return the document's primary key, so the page cannot ask for a SAS by id.
   Fix it in the PROCEDURE, which is the right layer: add `d.[PatientDocument_ID] AS [DocumentId]` to the
   patient branch, `d.[StaffDocument_ID] AS [DocumentId]` to the staff branch, and a matching
   `CAST(NULL AS INT) AS [DocumentId]` to the empty fallback branch so all three shapes still agree. Surface
   it as `documentId` in the controller's projection. Do not change anything else in that procedure.

4. ADD to DocumentsController:

       [HttpGet]
       public async Task<IActionResult> DocumentUrl(string mode, int id)

   'Patient' calls spPatientDocument_GetById, 'Staff' calls spStaffDocument_GetById; anything else is a
   400. Normalise `mode` the same way Search already does. Mint a 5-minute read SAS, return
   Ok(new { success = true, url, fileName }), and audit it with the AuditLog.PatientDocumentDownloaded /
   StaffDocumentDownloaded method that fits the mode. The class-level SuperUserOnly policy already guards
   it; do not weaken it.

5. wwwroot/js/documents/index.js — at about line 218-231 replace `const filePath = row.filePath || '#';`
   and its `<a href="${filePath}">` with an `<a href="#" class="doc-open" data-id="${row.documentId}"
   data-mode="${mode}">`, plus a delegated click handler that fetches
   `/Documents/DocumentUrl?mode=…&id=…` and window.open()s the result. The page already knows its current
   mode (getCurrentMode()); use it. Note that this table is turned into a DataTable after render — attach
   the handler by DELEGATION on a container that survives the DataTable rebuild, not to each row.

PART B — close the door. Do these in order.

6. REMOVE THE COMMITTED FILES. `git ls-files CRC.Web/wwwroot/uploads` currently lists 31 PDFs (19 patient,
   12 staff) — patient PERSONAL IDENTIFICATION, iFOBT RESULTS, PDPA FORM, consent forms, and staff CVs,
   MMC registration certificates and malpractice memberships. Run
   `git rm -r --cached CRC.Web/wwwroot/uploads` and then delete the folder from disk.
   ⚠️ SAY THIS EXPLICITLY IN YOUR REPORT, do not bury it: `git rm --cached` removes the files from the
   working tree and from FUTURE commits, but they REMAIN IN GIT HISTORY and remain on the GitHub remote.
   Purging history needs `git filter-repo` (or BFG) plus a force-push that rewrites every collaborator's
   clone — that is the OWNER'S DECISION, not yours. Report the file list, note that all 31 are ~15 KB and
   appear to be the same placeholder PDF renamed (so this may be a non-issue), and tell the owner to
   confirm that before deciding. DO NOT rewrite history yourself.

7. ADD to .gitignore, with a comment saying why:

       # Uploaded documents never belong in source control. They now live in a private Azure Blob
       # container (see DOCUMENTSTORAGE.md); this rule is a backstop in case a stale build drops files here.
       CRC.Web/wwwroot/uploads/

8. REMOVE the two now-pointless folder registrations from CRC.Web/CRC.Web.csproj:

       <Folder Include="wwwroot\uploads\staff\" />
       <Folder Include="wwwroot\uploads\patient\" />

   If that leaves the <ItemGroup> empty, remove the whole ItemGroup.

9. BLOCK /uploads/** AT THE PIPELINE. In CRC.Web/Program.cs, immediately BEFORE `app.UseStaticFiles();`
   (currently line ~150), add:

       // Nothing under /uploads is ever served again. Patient and staff documents live in a PRIVATE Azure
       // Blob container (Services/AzureBlobDocumentStorage.cs) and are reached only through the
       // authenticated *DocumentUrl endpoints, which mint a 5-minute read SAS.
       //
       // This branch exists because UseStaticFiles() runs BEFORE authentication and performs no
       // authorisation check of its own: any file that ends up under wwwroot is public, permanently, to
       // anyone holding the URL. Files uploaded by earlier versions are STILL PHYSICALLY PRESENT on the
       // Azure App Service disk — a publish never deletes them ("Remove additional files at destination"
       // is deliberately off, to protect logs) — so this 404 is what actually makes them unreachable.
       // Do not remove it, and do not narrow it to a sub-path.
       app.UseWhen(
           ctx => ctx.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase),
           branch => branch.Run(ctx =>
           {
               ctx.Response.StatusCode = StatusCodes.Status404NotFound;
               return Task.CompletedTask;
           }));

   404, not 403: a 403 confirms that something is there.

10. SWEEP the whole repo. `grep -rn "uploads" --include=*.cs --include=*.cshtml --include=*.csproj CRC.Web`
    and `grep -rn "uploads" CRC.Web/wwwroot/js` must both come back empty (ignore CRC.Web/bin and
    CRC.Web/obj, and ignore third-party bundles under wwwroot/lib — tinymce, dropzone and elfinder contain
    the word and are not ours). Also check Export-NucentraPortal.ps1: its $excludeRegex mentions
    wwwroot/uploads — that line becomes harmless but stale; leave the script working and just note it.

DO NOT in this prompt: touch ANYTHING in Azure — not a resource, not an app setting, not a file on the App
Service, and not via Kudu, FTPS, the Azure CLI or Azure PowerShell. The owner deletes the App Service's
stranded files by hand, following Nucentra_Azure_Deployment_Guide.md §10.6, AFTER Prompt 6 and after they
have re-published. Your step 9 middleware is what makes those files unreachable in the meantime — that is
the whole reason it exists. Do NOT edit Nucentra_Azure_Deployment_Guide.md either; it is already written for
this change. And do NOT rewrite git history.

VERIFY BEFORE YOU FINISH:
  • `dotnet build CRC.Web/CRC.Web.csproj` — 0 errors, 0 new warnings.
  • MSBuild on CRC.Database still reports "Build succeeded.", 0 Error(s) and only the two baseline SQL71502
    warnings from spStaffSlots_CreateRange.sql (you changed spDocuments_Search in step 3).
  • `git status --short` shows the 31 deletions, the .gitignore change, the .csproj change and your code
    edits — and NOTHING unexpected.
  • CRC.Web/wwwroot/uploads no longer exists on disk.
  • START THE SITE and prove the block works. Run `dotnet run --project CRC.Web --launch-profile https`,
    wait for it to listen on https://localhost:7276, then request
    `https://localhost:7276/uploads/patient/anything.pdf` with NO authentication cookie and assert the
    status is 404 (PowerShell: `Invoke-WebRequest -SkipCertificateCheck -SkipHttpErrorCheck`). Assert the
    same for `/uploads/staff/anything.pdf`. Report both status codes. Stop the site afterwards.
    (The database is not published yet, so other pages may error — that is expected and irrelevant here;
    the static-file branch runs before anything touches SQL.)

WHEN DONE: report the two 404s, the git file list with the history caveat spelled out, and every file you
changed. Then edit DocumentStoragePlan.md — tick the Prompt 5 box and set Prompt 5's Status to "✅ Done".
```

---

## Prompt 6 — End-to-end proof against Azurite, and `DOCUMENTSTORAGE.md`

**Status:** ✅ Done
**Depends on:** Prompts 1–5

> **The last code prompt, and still entirely local.** It runs the real site against the Azurite emulator and
> a scratch database and proves the whole thing works — including repeating, mechanically, the exact test the
> owner ran by hand to find the bug. Azure itself is untouched: that is 🔵 **AZURE B**, immediately after.

```text
You are an AI coding agent on the CRC Portal, also called "nucentra" (ASP.NET Core 8 MVC + a classic SSDT
database project). Fresh chat — no prior memory. The repo root is CRC_Portal.

WHAT'S ALREADY DONE (Prompts 1-5): patient and staff documents are stored in a PRIVATE Azure Blob container
via CRC.Web/Services/AzureBlobDocumentStorage.cs, bound to the "DocumentStorage" configuration section
(ConnectionString + ContainerName). dbo.PatientDocument.BlobName and dbo.StaffDocument.BlobName hold the
container-relative key. Three endpoints mint 5-minute read SAS URLs on click:
StaffPatientController.GetPatientDocumentUrl, StaffController.GetStaffDocumentUrl and
DocumentsController.DocumentUrl. Uploads are validated (.pdf .png .jpg .jpeg .docx, 20 MB, extension AND
content-type). wwwroot/uploads is deleted from the repo, .gitignore'd, and 404'd by a middleware branch in
Program.cs placed before UseStaticFiles(). Locally everything runs against the Azurite emulator
("UseDevelopmentStorage=true", container "nucentra-documents").

🔵 THE ONE HARD RULE FOR THIS PROMPT: YOU DO NOT TOUCH AZURE.
Not a resource, not an app setting, not a file on the App Service. Not through the portal, not through
Kudu, not through FTPS, not through the Azure CLI, `az`, or Azure PowerShell. The owner performs every
Azure action themselves by hand, following Nucentra_Azure_Deployment_Guide.md §10, and they do §10.5-§10.7
AFTER you finish. Do not edit that guide either — it is already written for this change. Everything below
happens on localhost, against Azurite and a scratch SQL database that you drop when you are done.

YOUR TASK (Prompt 6) — prove it end to end locally, then write the operator document.

PART A — END-TO-END PROOF, locally, against Azurite.

1. Make sure Azurite is listening on 127.0.0.1:10000 (start it if not — it ships with Visual Studio at
   "C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe").

2. Publish a SCRATCH database — never CRC_DB:
       "C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe" CRC.Database/CRC.Database.sqlproj /p:Configuration=Debug /p:VisualStudioVersion=18.0 /nologo /v:minimal
       "C:/Program Files/Microsoft Visual Studio/18/Insiders/Common7/IDE/Extensions/Microsoft/SQLDB/DAC/SqlPackage.exe" /Action:Publish /SourceFile:CRC.Database/bin/Debug/CRC.Database.dacpac /TargetServerName:localhost /TargetDatabaseName:CRC_DB_BlobTest /TargetTrustServerCertificate:True /p:CreateNewDatabase=True
   The post-deployment seed runs automatically and creates the SUPERUSER account (SEEDING.md: username
   SUPERUSER, password ChangeMe!123).

3. Run the site against the scratch database WITHOUT editing any config file — use the environment
   variable, which .NET maps onto ConnectionStrings:CRC_DB:
       $env:ConnectionStrings__CRC_DB = "Server=localhost;Database=CRC_DB_BlobTest;Trusted_Connection=True;TrustServerCertificate=True;"
       dotnet run --project CRC.Web --launch-profile https
   Do NOT edit appsettings.json for this. Wait for https://localhost:7276 to start listening.

4. Drive it over HTTP with a cookie jar (PowerShell Invoke-WebRequest -SessionVariable / -WebSession, or
   curl -c/-b, with -SkipCertificateCheck for the dev certificate). Assert EVERY step and report a
   pass/fail table:
     a) GET /Account/Login, scrape the __RequestVerificationToken, POST the login as SUPERUSER / ChangeMe!123
        and confirm the auth cookie comes back.
     b) A patient must exist. Either create one through POST /Patient/SaveBasic with a valid 12-digit NRIC
        (the controller derives birth date and gender from it), or insert one with sqlcmd — whichever is
        less brittle. Note the Patient_ID.
     c) POST a real PDF to /StaffPatient/UploadPatientDocuments as multipart, with the antiforgery token in
        the X-CSRF-Token header and a valid docTypeId from spLU_PatientDocumentType_List. Assert
        success = true.
     d) 🔴 ASSERT NOTHING WAS WRITTEN TO DISK: CRC.Web/wwwroot/uploads must not exist. This is the
        assertion that proves the whole change.
     e) ASSERT THE BLOB EXISTS in Azurite under key patients/{Patient_ID}/{guid}.pdf, and that
        dbo.PatientDocument.BlobName holds exactly that key (sqlcmd).
     f) GET /StaffPatient/GetPatientDocuments and assert the JSON contains NO path/key field at all.
     g) GET /StaffPatient/GetPatientDocumentUrl?id={documentId}, assert success = true, then fetch the
        returned SAS URL with a CLEAN HttpClient (NO cookies) and assert 200 plus the exact bytes.
     h) 🔴 THE OWNER'S TEST, MECHANISED. Fetch the SAME blob URL with the SAS query string REMOVED, no
        cookies. Assert it FAILS (404 or 409 — the container is private). Then fetch the OLD-STYLE URL
        https://localhost:7276/uploads/patient/{anything}.pdf with no cookies and assert 404. These two
        together are the whole point of this piece of work — report them prominently, at the TOP of your
        report, not buried in the table.
     i) POST /StaffPatient/DeletePatientDocument for that document, assert success, then assert the blob is
        GONE from Azurite and the dbo.PatientDocument row is gone.
     j) Repeat (c)-(i) for STAFF documents through /Staff/SaveStaffWithDocuments and
        /Staff/GetStaffDocumentUrl. You will need a Branch and a Staff record first — SEEDING.md documents
        the setup order.
     k) Upload rejection: POST a .txt file and assert it is refused with the "not an allowed file type"
        message, and that NO blob was created for it.
     l) The cascade: delete the test patient through POST /Patient/DeletePatient and assert every blob
        under patients/{Patient_ID}/ is gone from Azurite. This exercises the spPatient_DeleteCascade
        result set added in Prompt 2.

5. Stop the site, DROP DATABASE CRC_DB_BlobTest (SINGLE_USER WITH ROLLBACK IMMEDIATE first if it refuses),
   and delete any leftover test blobs from Azurite. Confirm CRC_DB was never connected to for writing.

PART B — WRITE THE OPERATOR DOCUMENT.

6. WRITE CRC_Portal/DOCUMENTSTORAGE.md — a short OPERATOR document in the same voice as SEEDING.md,
   present tense, "as built", not a design document. Read SEEDING.md first and match its shape. Cover:
      • Where documents live: the private container nucentra-documents in the storage account
        nucentrastorprod, keys patients/{Patient_ID}/{guid}.ext and staff/{Staff_ID}/{guid}.ext.
      • What the database stores: dbo.PatientDocument.BlobName / dbo.StaffDocument.BlobName — a key, never
        a URL, never the bytes.
      • How a download works: click -> authenticated *DocumentUrl endpoint -> 5-minute read SAS -> browser
        fetches from Blob. State plainly that a minted URL works for five minutes wherever it is pasted,
        and that this is the intended trade-off.
      • What is accepted: .pdf .png .jpg .jpeg .docx, 20 MB per file, extension AND content-type both
        checked, whole batch rejected if any file fails. Where to change it:
        CRC.Web/Infrastructure/DocumentValidation.cs.
      • The two config settings, their local (Azurite) and Azure (App Service) values, and the double
        underscore rule. For the Azure side, do NOT restate the portal steps — point at
        Nucentra_Azure_Deployment_Guide.md §10, which owns them.
      • The file map: Services/{IDocumentStorage,AzureBlobDocumentStorage,DocumentStorageOptions}.cs,
        Infrastructure/DocumentValidation.cs, the three *DocumentUrl actions, the /uploads 404 branch in
        Program.cs — one line each.
      • Local development: start Azurite; the container is created on first upload
        (CreateIfNotExistsAsync), so there is no provisioning step.
      • Housekeeping: deleting a document deletes its blob; deleting a patient (spPatient_DeleteCascade)
        or a staff member (spStaff_Delete) returns the blob keys and the controller deletes them; a failed
        blob delete is logged as a warning and never fails the request, so a rare orphan is possible — say
        how to spot one (compare container keys against the two BlobName columns).
      • Audit: uploads, downloads and deletes all write to the Serilog audit-*.log channel via
        CRC.Web/Infrastructure/AuditLog.cs, and inserts/deletes additionally write a dbo.AuditTrails row
        from inside the stored procedure.
      • 🔴 The migration note, stated plainly: nucentra used to store documents under wwwroot/uploads and
        those URLs were public to anyone holding them, with no login. Any deployment predating this change
        still has those files on its App Service disk, and a publish will NOT remove them. Deleting them is
        a manual step and it is written up in Nucentra_Azure_Deployment_Guide.md §10.6 — link to it rather
        than repeating it.

7. SWEEP for staleness in the CODE and in the docs you own: README, SEEDING.md, and any comment in
   Program.cs or the controllers that still describes files on disk. Fix what you find; list what you
   changed. Do NOT edit Nucentra_Azure_Deployment_Guide.md — if you believe something in it is wrong,
   report it in your final message and leave the file alone.

WHEN DONE: lead your report with the two assertions from steps 4d and 4h, because those are the ones the
owner will check first. Then give the full Part A pass/fail table, confirm the scratch database was dropped
and CRC_DB was never written to, and confirm DOCUMENTSTORAGE.md exists. Finish by reminding the owner, in
one line, that the live site is NOT yet fixed until they complete Nucentra_Azure_Deployment_Guide.md
§10.5-§10.7 by hand. Then edit DocumentStoragePlan.md — tick the Prompt 6 box and set Prompt 6's Status to
"✅ Done".
```

---

## 🔵 AZURE B — Publish, purge the old files, and verify on the live site

**Status:** ⬜ Not started
**Do this after:** Prompt 6 · **In:** [`Nucentra_Azure_Deployment_Guide.md` §10.5 – §10.7](Nucentra_Azure_Deployment_Guide.md)

**This one is yours, and it is the step that actually fixes the live site.** Everything before it changed
code; until you do this, the portal your users are on is unchanged and every document ever uploaded to it is
still public.

| Guide § | What you do | Roughly |
|---|---|---|
| **§10.5** | Re-publish the database (`FilePath` → `BlobName` is a schema change), then publish `CRC.Web` | 20 min |
| **§10.6** | 🔴 Find the old `wwwroot/uploads` files on the App Service and delete them | 20 min |
| **§10.7** | 🔴 Verify on the live site — upload, download, and the two URL tests | 10 min |

**What to expect at each one:**

- **§10.5 — the database.** The guide gives you two options: delete and re-create `CRC_DB` for a clean slate
  (what you said you'd do — it re-seeds a fresh `SUPERUSER` / `ChangeMe!123`, so **change that password
  again**), or publish over the existing database and run two `DELETE` statements to clear the dead document
  rows. Either is fine; the guide spells out what each costs.
- **§10.6 — the purge. This is the one that cannot be skipped.** The Azure Portal has no non-Kudu file
  browser for App Service, so the guide gives two routes: **FTPS with FileZilla** (recommended — it lets you
  *look* at what is there before deleting, and gives you the chance to save anything real), or **one
  deliberate publish with "Remove additional files at destination" ticked** (no new tool, but it also erases
  your `Logs/` folder, including the 365-day `audit-*.log`). Read both before choosing.
  Note the path: **`/site/wwwroot/wwwroot/uploads`** — the doubled `wwwroot` is correct.
- **§10.7 — the proof.** Two tests, and they are the ones that answer your original question: a blob URL with
  its signature stripped must fail, and `https://<your-site>/uploads/patient/anything.pdf` must return 404.

**When §10.7 passes, the work is done.** Come back to the Definition of done below and check it off.

---

## Definition of done

Two halves, and **the work is not finished until both are ticked**. Half of it is not deployable, and half of
it is not testable in Azure — that is why they are listed separately.

### The code half — all six prompts ticked

- **A document uploaded on `Patient/Edit → Documents` is written to the private `nucentra-documents`
  container** under `patients/{Patient_ID}/{guid}.ext`, and **nothing is written under `wwwroot`** —
  `CRC.Web/wwwroot/uploads` does not exist on any developer machine.
- **`/uploads/**` returns 404 from the app itself**, logged in or logged out, because a middleware branch
  answers it before `UseStaticFiles()` ever sees the request. Proven locally in Prompt 5 and again in
  Prompt 6.
- **A blob URL without its SAS query string fails** against Azurite — the container is private. Proven, not
  assumed.
- **Download links are resolved at click time** into a 5-minute read SAS by
  `StaffPatientController.GetPatientDocumentUrl`, `StaffController.GetStaffDocumentUrl` or
  `DocumentsController.DocumentUrl`. No storage key and no durable file URL ever reaches the browser.
- **Staff documents are on exactly the same footing as patient documents** — `/uploads/staff/**` is as dead
  as `/uploads/patient/**`, and the SUPERUSER Documents search page hands out SAS links for both.
- **Uploads are validated server-side**: `.pdf` `.png` `.jpg` `.jpeg` `.docx`, 20 MB per file, extension
  **and** content-type, whole batch rejected before any blob is written.
- **`FilePath` appears nowhere.** `grep -ri "FilePath" CRC.Database CRC.Web` (excluding `bin`/`obj` and
  third-party bundles under `wwwroot/lib`) returns nothing.
- **Deleting a document deletes its blob; deleting a patient or a staff member deletes all of theirs.**
  `spPatient_DeleteCascade` returns its blob keys — a gap that existed before this work and is closed by it.
- **Uploads, downloads and deletes are audited** on both channels: `audit-*.log` via `AuditLog.*`, and
  `dbo.AuditTrails` from inside the insert/delete procedures.
- **Both build gates are clean** — `dotnet build CRC.Web/CRC.Web.csproj` with 0 errors, and MSBuild on
  `CRC.Database.sqlproj` with 0 errors and only the two pre-existing `SQL71502` warnings in
  `spStaffSlots_CreateRange.sql`.
- **`DOCUMENTSTORAGE.md` exists** and leads with where documents live and how a download works.
- **The 31 committed PDFs are out of the working tree**, `.gitignore` prevents a recurrence, and the owner
  has been told explicitly that they remain in git history until someone decides to rewrite it.

### The Azure half — both 🔵 checkpoints done, by hand, in the portal

Follow [`Nucentra_Azure_Deployment_Guide.md` §10](Nucentra_Azure_Deployment_Guide.md).

- **The storage account `nucentrastorprod` exists** in `rg-nucentra-prod` (never HEART's group), with
  *"Allow enabling anonymous access on individual containers"* **off** and *"Enable storage account key
  access"* **on**. — §10.1
- **The container `nucentra-documents` shows `Private`** in the portal's *Public access level* column. — §10.2
- **`DocumentStorage__ConnectionString` and `DocumentStorage__ContainerName`** are set on
  `nucentra-web-prod`, with **two** underscores, under **App settings** (not Connection strings). — §10.4
- **The new database schema and the new build are both published.** — §10.5
- 🔴 **`site/wwwroot/wwwroot/uploads` no longer exists on the App Service.** This is the one that closes the
  hole in reality rather than in the repository; nothing the prompts do can achieve it, and no deployment
  will do it for you. — §10.6
- 🔴 **Both live tests pass**, in a private window with no session: a blob URL stripped of its signature
  fails, and `https://<your-site>/uploads/patient/anything.pdf` returns **404** — including for any real
  filename you saw during §10.6. **This is the test you ran by hand to find the bug, and it is the one that
  says the work is over.** — §10.7
