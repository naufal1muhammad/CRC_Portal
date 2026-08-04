# Document Storage

Patient and staff documents live in a **private Azure Blob container**. The database stores a blob key and
nothing else — no URL, no bytes. The browser reaches a file only through a **5-minute read SAS** minted on
click by an authenticated endpoint. Nothing in nucentra writes a byte under `wwwroot`.

---

## ⚠️ Deployments that predate this change still have the old files

nucentra used to store documents under `CRC.Web/wwwroot/uploads/`. `app.UseStaticFiles()` runs **before**
authentication and performs no authorisation check, so every one of those URLs was **public to anyone holding
the link, with no login at all**.

Those files are still on the App Service disk of any deployment made before this change, and **a publish will
not remove them** — "Remove additional files at destination" is deliberately left unchecked. Deleting them is
a manual step, written up in
**[`Nucentra_Azure_Deployment_Guide.md` §10.6](Nucentra_Azure_Deployment_Guide.md)**.

`Program.cs` returns **404** for anything under `/uploads` before `UseStaticFiles()` ever sees it, so a
stranded file is unreachable even if a delete is missed. That block is a backstop, not a substitute: until
§10.6 is done the files are still physically there.

---

## Where documents live

| | |
|---|---|
| **Storage account** | `nucentrastorprod` |
| **Container** | `nucentra-documents`, access level **Private** |
| **Patient key** | `patients/{Patient_ID}/{guid}{ext}` — e.g. `patients/PAT-000042/9f1c…d3.pdf` |
| **Staff key** | `staff/{Staff_ID}/{guid}{ext}` — e.g. `staff/END-00003/4b7a…91.pdf` |

One container, two prefixes: one place to check the access policy and one place to set a lifecycle rule. The
file name is a fresh GUID, so nothing the user typed becomes part of the key and two uploads of the same
document never collide. The owner id stays a readable path segment, so files group by owner in Storage
Explorer.

## What the database stores

`dbo.PatientDocument.BlobName` and `dbo.StaffDocument.BlobName`, both `VARCHAR(500)`.

These hold the **key within the container** — never a URL, never a filesystem path, never the bytes. The
longest key this app can produce is about 145 characters. Everything else on those rows is metadata:
`FileName`, `ContentType`, `UploadedOn`, and the document type.

The column was called `FilePath` before this change. It was renamed because it stopped being one.

## How a download works

1. The user clicks a document in a list. The page has **no link to the file** — the rendered HTML never
   contains a storage address.
2. The click calls an authenticated `…DocumentUrl` action, which reads the row, mints
   `GetReadSasUrl(blobName, TimeSpan.FromMinutes(5))` and returns the URL as JSON.
3. The browser fetches the bytes straight from Blob storage with that URL.

The SAS is **read-only**, is never persisted, and never appears in the page source.

**State this plainly, because it is the one consequence to understand:** a minted URL works for **five
minutes wherever it is pasted** — another browser, another machine, no session. That is the intended
trade-off, and it is a different thing from the old permanent public URL: the link dies when the window
closes, and the link in the page is no longer the file's address. If you would rather no URL ever worked
without a cookie, the alternative is to stream the bytes through an authenticated action instead
(`FileStreamResult`), which changes the three `…DocumentUrl` actions and nothing else.

## What is accepted

| | |
|---|---|
| **Extensions** | `.pdf` `.png` `.jpg` `.jpeg` `.docx` |
| **Content types** | `application/pdf`, `image/png`, `image/jpeg`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| **Size** | 20 MB per file |

The extension **and** the reported content-type must both be in their allowed sets, so a renamed or spoofed
file is rejected. Every file in a batch is validated **before any blob is written**, so a bad file at the end
of a selection cannot leave the earlier ones behind — the whole batch is refused.

To change any of this, edit **`CRC.Web/Infrastructure/DocumentValidation.cs`**. It is the only place the rules
live; all three upload endpoints call it.

## Configuration

Two settings, bound to the `DocumentStorage` section:

| Setting | Local (Azurite) | Azure App Service |
|---|---|---|
| `ConnectionString` | `UseDevelopmentStorage=true` | the storage-account connection string |
| `ContainerName` | `nucentra-documents` | `nucentra-documents` |

Locally these come from `CRC.Web/appsettings.json`. In Azure they come from **app settings**, which override
the file at runtime — so `appsettings.json` never carries a production secret.

App Service expresses the section separator as **two underscores**:

```
DocumentStorage__ConnectionString
DocumentStorage__ContainerName
```

A single underscore is silently ignored, and the app then starts against an empty connection string and fails
on the first upload. The portal steps that create the account, the container and these settings are
**[`Nucentra_Azure_Deployment_Guide.md` §10](Nucentra_Azure_Deployment_Guide.md)**, which owns them — they are
not repeated here.

The account is reached by **connection string**, not Managed Identity. Moving to Managed Identity is the top
hardening item and is a real code change, not a settings change: it needs `DefaultAzureCredential` plus a
**user-delegation** SAS instead of the account-key SAS. `GetReadSasUrl` throws a clear error if the client
ever loses its account key, which is the line that will tell you.

## File map

| File | Role |
|---|---|
| `CRC.Web/Services/IDocumentStorage.cs` | The abstraction: `UploadAsync`, `GetReadSasUrl`, `DeleteAsync` |
| `CRC.Web/Services/AzureBlobDocumentStorage.cs` | The Azure implementation; registered as a **singleton** because `BlobServiceClient` is meant to be reused |
| `CRC.Web/Services/DocumentStorageOptions.cs` | The two settings above, bound from the `DocumentStorage` section |
| `CRC.Web/Infrastructure/DocumentValidation.cs` | Allowed types, size cap, safe file name, blob-key builder |
| `CRC.Web/Controllers/StaffPatient/StaffPatientController.cs:1052` | `GetPatientDocumentUrl` — mints the SAS for one patient document |
| `CRC.Web/Controllers/Staff/StaffController.cs:1200` | `GetStaffDocumentUrl` — the same for one staff document |
| `CRC.Web/Controllers/Documents/DocumentsController.cs:240` | `DocumentUrl(mode, id)` — the SUPERUSER search page, which lists both kinds |
| `CRC.Web/Program.cs:171` | The `UseWhen` branch that 404s `/uploads/**` **before** `UseStaticFiles()` |

## Local development

Start Azurite. It ships with Visual Studio:

```bash
"C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe" --blobHost 127.0.0.1 --blobPort 10000 --skipApiVersionCheck
```

`--skipApiVersionCheck` is required, not optional: no Azurite release accepts the API version
`Azure.Storage.Blobs` 12.29.1 sends, and without the flag every local blob call fails with **400**.

That is the whole setup. `UploadAsync` calls `CreateIfNotExistsAsync(PublicAccessType.None)` first, so the
container is created on the first upload — **there is no provisioning step** and no Azure resource is needed
to run or debug nucentra locally. No real patient file leaves your machine during development.

## Housekeeping

| When | What happens to the blob |
|---|---|
| A document is deleted | `spPatientDocument_Delete` / `spStaffDocument_Delete` return the key as an OUTPUT parameter; the controller deletes the blob |
| A patient is deleted | `spPatient_DeleteCascade` returns the patient's keys as a result set; `PatientController.DeletePatient` deletes them |
| A staff member is deleted | `spStaff_Delete` returns the keys as its second result set; `StaffController.DeleteStaff` deletes them |
| A staff save removes documents | `SaveStaffWithDocuments` deletes those blobs **after** the transaction commits |

**A failed blob delete is logged as a warning and never fails the request.** The metadata row is already gone
and the audit entry already stands, so failing the request would only tell the user that a deletion they can
see did not happen. What is actually left behind is an orphaned blob — an operational clean-up job, and a rare
one. Grep `CRC.Web/Logs/app-*.log` for `Failed to delete blob` (patient documents and patient cascade) and
`Failed to delete staff document blob` (everything on the staff side).

To find orphans, compare the keys in the container against the two `BlobName` columns:

```sql
SELECT [BlobName] FROM [dbo].[PatientDocument]
UNION ALL
SELECT [BlobName] FROM [dbo].[StaffDocument];
```

Any key in `nucentra-documents` that is not in that result set belongs to no row and can be removed. List the
container's keys from **Storage browser** on the storage account in the portal.

## Audit

Uploads, downloads and deletes all write to the Serilog **audit channel**, `CRC.Web/Logs/audit-*.log`, through
`CRC.Web/Infrastructure/AuditLog.cs`. Minting a SAS is a read of patient or staff data, so it is logged before
the URL leaves the server — the download itself happens against storage, where the application can no longer
observe it.

```
AUDIT Patient document uploaded. PatientId=PAT-000042 DocumentId=0 DocTypeId=01 BlobName=patients/PAT-000042/9f1c….pdf FileName=consent.pdf SizeBytes=15708
AUDIT Patient document downloaded. PatientId=PAT-000042 DocumentId=17 FileName=consent.pdf
AUDIT Patient document deleted. PatientId=PAT-000042 DocumentId=17 BlobName=patients/PAT-000042/9f1c….pdf
AUDIT Patient documents purged. PatientId=PAT-000042 BlobCount=2
```

`DocumentId=0` on an upload is expected: the insert procedure writes its own audit row but does not hand the
new identity back, so the blob key is what ties the log line to the row it describes.

Inserts and deletes **additionally** write a `dbo.AuditTrails` row from inside the stored procedure, with the
acting user resolved by `DatabaseHelper`'s `@User_ID` injection. That is the database-side record; the log
channel is the security-side one. Downloads appear only in the log channel — the read procedures write no
audit row.
