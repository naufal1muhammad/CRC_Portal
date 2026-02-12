using CRC.Web.Data;
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
        private readonly IWebHostEnvironment _env;

        public DocumentsController(DatabaseHelper db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
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

                var patientDocTypes = dtPatTypes.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["PatientDocumentType_Name"]?.ToString() ?? string.Empty
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

                var staffDocTypes = dtStaffTypes.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["StaffDocumentType_Name"]?.ToString() ?? string.Empty
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .Distinct()
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
            catch (Exception)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error loading document lookups."
                });
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

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = r["Id"]?.ToString() ?? "",
                        name = r["Name"]?.ToString() ?? "",
                        documentType = r["DocumentType"]?.ToString() ?? "",
                        fileName = r["FileName"]?.ToString() ?? "",
                        filePath = r["FilePath"]?.ToString() ?? "",
                        uploadedOn = r["UploadedOn"]?.ToString() ?? ""
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error searching documents." });
            }
        }
    }
}