using CRC.Data.Data;
using CRC.Web.Infrastructure;
using CRC.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using static CRC.Web.Controllers.Documents.DocumentsController;

namespace CRC.Web.Controllers.Documents
{
    [Authorize(Policy = "SuperUserOnly")]
    public class DocumentsController : Controller
    {
        private readonly DatabaseHelper _db;

        // IWebHostEnvironment is deliberately gone: its only purpose here was resolving the web root so a
        // document could be read back from wwwroot/uploads. Documents live in a private blob container now
        // and this controller never touches the file system.
        private readonly IDocumentStorage _documentStorage;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            DatabaseHelper db,
            IDocumentStorage documentStorage,
            ILogger<DocumentsController> logger)
        {
            _db = db;
            _documentStorage = documentStorage;
            _logger = logger;
        }

        // GET: /Documents
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Documents/GetLookups
        [HttpGet]
        public async Task<IActionResult> GetLookups()
        {
            try
            {
                var empty = Array.Empty<SqlParameter>();

                // 1) Patient names
                var dtPatNames = await _db.ExecuteDataTableAsync(
                    "spPatientDocument_PatientNames",
                    empty
                );

                // 2) Patient doc types
                var dtPatTypes = await _db.ExecuteDataTableAsync(
                    "spPatientDocument_LookupDocuments",
                    empty
                );

                // 3) Staff names
                var dtStaffNames = await _db.ExecuteDataTableAsync(
                    "spStaffDocument_StaffNames",
                    empty
                );

                // 4) Staff doc types
                var dtStaffTypes = await _db.ExecuteDataTableAsync(
                    "spStaffDocument_LookupDocuments",
                    empty
                );

                var patientNames = dtPatNames.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["Patient_Name"]?.ToString() ?? string.Empty
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .Distinct()
                    .OrderBy(x => x.name)
                    .ToList();

                var staffNames = dtStaffNames.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["Staff_Name"]?.ToString() ?? string.Empty
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .Distinct()
                    .OrderBy(x => x.name)
                    .ToList();

                var patientDocTypes = dtPatTypes.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = (r["PatientDocumentType_ID"]?.ToString() ?? string.Empty).Trim(),
                        name = (r["PatientDocumentType_Name"]?.ToString() ?? string.Empty).Trim()
                    })
                    .Where(x => !(string.IsNullOrWhiteSpace(x.id) && string.IsNullOrWhiteSpace(x.name)))
                    .Select(x => new
                    {
                        id = string.IsNullOrWhiteSpace(x.id) ? x.name : x.id,
                        name = string.IsNullOrWhiteSpace(x.name) ? x.id : x.name
                    })
                    .Distinct()
                    .OrderBy(x => x.name)
                    .ToList();

                // Defensive de-duplication:
                // If the lookup SP returns both (ID, Name) and (ID, ID), only keep the "real" name.
                var staffDocTypes = dtStaffTypes.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = (r["StaffDocumentType_ID"]?.ToString() ?? string.Empty).Trim(),
                        name = (r["StaffDocumentType_Name"]?.ToString() ?? string.Empty).Trim()
                    })
                    .Where(x => !(string.IsNullOrWhiteSpace(x.id) && string.IsNullOrWhiteSpace(x.name)))
                    .Select(x => new
                    {
                        id = string.IsNullOrWhiteSpace(x.id) ? x.name : x.id,
                        name = string.IsNullOrWhiteSpace(x.name) ? x.id : x.name
                    })
                    .GroupBy(x => x.id, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        // Prefer a label that isn't just the ID itself.
                        var best = g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.name) && !x.name.Equals(g.Key, StringComparison.OrdinalIgnoreCase))
                                   ?? g.First();

                        return new
                        {
                            id = g.Key,
                            name = best.name
                        };
                    })
                    .OrderBy(x => x.name)
                    .ToList();


                return Ok(new
                {
                    success = true,
                    patientNames,
                    patientDocTypes,
                    staffNames,
                    staffDocTypes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading document lookups");
                return Ok(ErrorResponse.ForUser(HttpContext, "Error loading document lookups."));
            }
        }

        // DTO for search
        public class DocumentsSearchRequest
        {
            // "Patient" or "Staff"
            public string Mode { get; set; } = "Patient";
            public string? IndividualName { get; set; }
            public string? DocumentType { get; set; }
        }

        // POST: /Documents/Search
        [HttpPost]
        public async Task<IActionResult> Search([FromBody] DocumentsSearchRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var mode = (model.Mode ?? "Patient").Trim();

            // Normalise to "Patient" or "Staff"
            if (mode.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                mode = "Staff";
            else
                mode = "Patient";

            var individual = (model.IndividualName ?? string.Empty).Trim();
            var docType = (model.DocumentType ?? string.Empty).Trim();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@Mode", mode),
                    new SqlParameter("@IndividualName", string.IsNullOrWhiteSpace(individual) ? DBNull.Value : individual),
                    new SqlParameter("@DocumentType", string.IsNullOrWhiteSpace(docType) ? DBNull.Value : docType)
                };

                var dt = await _db.ExecuteDataTableAsync("spDocuments_Search", parameters);

                // Metadata only — the blob key is deliberately NOT projected. The container is private, so a
                // storage key is useless to the browser and is exactly the kind of detail that should never
                // leave the server. The file itself is fetched through DocumentUrl, which mints a short-lived
                // read SAS for one document at click time.
                //
                // `id` is the Patient_ID / Staff_ID the row belongs to — the column the page shows in its ID
                // column, NOT the document's key. `documentId` is the PatientDocument_ID / StaffDocument_ID
                // and is the only thing DocumentUrl accepts.
                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        documentId = r["DocumentId"] == DBNull.Value ? 0 : Convert.ToInt32(r["DocumentId"]),
                        id = r["Id"]?.ToString() ?? "",
                        name = r["Name"]?.ToString() ?? "",
                        documentType = r["DocumentType"]?.ToString() ?? "",
                        fileName = r["FileName"]?.ToString() ?? "",
                        uploadedOn = r["UploadedOn"]?.ToString() ?? ""
                    })
                    .ToList();

                AuditLog.DocumentSearched(HttpContext, mode, individual, docType, list.Count);

                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error searching documents Mode={Mode} Individual={Individual} DocType={DocType}", mode, individual, docType);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error searching documents."));
            }
        }

        // Mints a short-lived read SAS URL for ONE document — the search page's counterpart of
        // StaffPatientController.GetPatientDocumentUrl and StaffController.GetStaffDocumentUrl. This page
        // lists both kinds, so it takes the mode alongside the id and dispatches to the matching read.
        //
        // The design, plainly: the container is private, so this five-minute URL is the ONLY way the browser
        // ever reaches the bytes. It is minted per click by this authenticated action, handed back once, never
        // persisted to the database and never rendered into the page's HTML. That is exactly what the old
        // static-file document links got wrong: static files are served ahead of authentication and get no
        // authorisation check at all, so every one of those links was public, and stayed public.
        //
        // No [Authorize] of its own: the class-level SuperUserOnly policy already applies, and this action
        // hands out patient AND staff documents, so it must never be reachable by less than that.
        // GET: /Documents/DocumentUrl?mode=Patient&id=123
        [HttpGet]
        public async Task<IActionResult> DocumentUrl(string mode, int id)
        {
            // Same trim + case-insensitive match Search uses. Search folds anything unrecognised into
            // "Patient" because a search with no hits is harmless; here an unrecognised mode would mean
            // reading the wrong table, so it is rejected outright.
            var normalisedMode = (mode ?? string.Empty).Trim();

            bool isStaff;
            if (normalisedMode.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                isStaff = true;
            else if (normalisedMode.Equals("Patient", StringComparison.OrdinalIgnoreCase))
                isStaff = false;
            else
                return BadRequest(new { success = false, message = "Invalid document mode." });

            if (id <= 0)
            {
                return Ok(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                var dt = isStaff
                    ? await _db.ExecuteDataTableAsync(
                        "spStaffDocument_GetById",
                        new[] { new SqlParameter("@StaffDocument_ID", id) })
                    : await _db.ExecuteDataTableAsync(
                        "spPatientDocument_GetById",
                        new[] { new SqlParameter("@PatientDocument_ID", id) });

                if (dt.Rows.Count == 0)
                {
                    return Ok(new { success = false, message = "Document not found." });
                }

                var row = dt.Rows[0];
                var blobName = row["BlobName"]?.ToString();
                var fileName = row["FileName"]?.ToString() ?? string.Empty;
                var ownerId = (isStaff ? row["Staff_ID"]?.ToString() : row["Patient_ID"]?.ToString()) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(blobName))
                {
                    return Ok(new { success = false, message = "Document not found." });
                }

                var url = _documentStorage.GetReadSasUrl(blobName, TimeSpan.FromMinutes(5));

                // Minting a SAS for a patient or staff record IS a read of personal data, so it belongs on the
                // audit channel — and it has to be written before the URL leaves the server, because the
                // download itself happens against storage where the application can no longer observe it.
                if (isStaff)
                    AuditLog.StaffDocumentDownloaded(HttpContext, ownerId, id, fileName);
                else
                    AuditLog.PatientDocumentDownloaded(HttpContext, ownerId, id, fileName);

                return Ok(new { success = true, url = url.ToString(), fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating download URL for Mode={Mode} DocumentId={DocumentId}", normalisedMode, id);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error opening document."));
            }
        }
    }
}