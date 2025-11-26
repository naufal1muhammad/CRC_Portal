using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CRC.Web.Data;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace CRC.Web.Controllers.Patient
{
    [Authorize]
    public class PatientController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly IWebHostEnvironment _env;
        public PatientController(DatabaseHelper db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET: /Patient
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Patient/GetActiveBranches
        [HttpGet]
        public async Task<IActionResult> GetActiveBranches()
        {
            var dt = await _db.ExecuteDataTableAsync("spBranch_ListActive");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString()
                });
            }

            return Ok(list);
        }

        // GET: /Patient/GetPatients?stage=T2
        [HttpGet]
        public async Task<IActionResult> GetPatients(string stage)
        {
            if (string.IsNullOrWhiteSpace(stage))
            {
                stage = "T2";
            }

            var parameters = new[]
            {
                new SqlParameter("@Stage", stage)
            };

            var dt = await _db.ExecuteDataTableAsync("spPatient_ListByStage", parameters);
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                DateTime? appointment = row["Appointment_Date"] == DBNull.Value
                    ? (DateTime?)null
                    : (DateTime)row["Appointment_Date"];

                list.Add(new
                {
                    patientId = row["Patient_ID"].ToString(),
                    name = row["Patient_Name"].ToString(),
                    nric = row["Patient_NRIC"].ToString(),
                    phone = row["Patient_Phone"].ToString(),
                    email = row["Patient_Email"].ToString(),
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString(),
                    stage = row["Patient_Stage"].ToString(),
                    remarks = row["Patient_Remarks"] == DBNull.Value ? null : row["Patient_Remarks"].ToString(),
                    appointmentDate = appointment?.ToString("yyyy-MM-dd")
                });
            }

            return Ok(list);
        }

        // GET: /Patient/GetPatient?patientId=...
        [HttpGet]
        public async Task<IActionResult> GetPatient(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest(new { success = false, message = "Patient ID is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Patient_ID", patientId)
            };

            var dt = await _db.ExecuteDataTableAsync("spPatient_GetById", parameters);

            if (dt.Rows.Count == 0)
            {
                return Ok(new { success = false, message = "Patient not found." });
            }

            var row = dt.Rows[0];

            DateTime? appointment = row["Appointment_Date"] == DBNull.Value
                ? (DateTime?)null
                : (DateTime)row["Appointment_Date"];

            return Ok(new
            {
                success = true,
                data = new
                {
                    patientId = row["Patient_ID"].ToString(),
                    name = row["Patient_Name"].ToString(),
                    nric = row["Patient_NRIC"].ToString(),
                    phone = row["Patient_Phone"].ToString(),
                    email = row["Patient_Email"].ToString(),
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString(),
                    stage = row["Patient_Stage"].ToString(),
                    remarks = row["Patient_Remarks"] == DBNull.Value ? null : row["Patient_Remarks"].ToString(),
                    appointmentDate = appointment?.ToString("yyyy-MM-dd")
                }
            });
        }

        public class SavePatientRequest
        {
            public bool IsNew { get; set; }
            public string PatientId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NRIC { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string BranchId { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public string Stage { get; set; } = string.Empty;
            public string? Remarks { get; set; }
            public DateTime? AppointmentDate { get; set; }
        }

        public class DeletePatientRequest
        {
            public string PatientId { get; set; } = string.Empty;
        }

        // POST: /Patient/SavePatient
        [HttpPost]
        public async Task<IActionResult> SavePatient([FromBody] SavePatientRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid data." });
            }

            if (string.IsNullOrWhiteSpace(model.PatientId) ||
                string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.NRIC) ||
                string.IsNullOrWhiteSpace(model.Phone) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.BranchId) ||
                string.IsNullOrWhiteSpace(model.BranchName) ||
                string.IsNullOrWhiteSpace(model.Stage))
            {
                return Ok(new { success = false, message = "Please fill in all required fields." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Patient_ID", model.PatientId),
                new SqlParameter("@Patient_Name", model.Name),
                new SqlParameter("@Patient_NRIC", model.NRIC),
                new SqlParameter("@Patient_Phone", model.Phone),
                new SqlParameter("@Patient_Email", model.Email),
                new SqlParameter("@Branch_ID", model.BranchId),
                new SqlParameter("@Branch_Name", model.BranchName),
                new SqlParameter("@Patient_Stage", model.Stage),
                new SqlParameter("@Patient_Remarks", (object?)model.Remarks ?? DBNull.Value),
                new SqlParameter("@Appointment_Date", (object?)model.AppointmentDate ?? DBNull.Value)
            };

            try
            {
                if (model.IsNew)
                {
                    await _db.ExecuteNonQueryAsync("spPatient_Insert", parameters);
                }
                else
                {
                    await _db.ExecuteNonQueryAsync("spPatient_Update", parameters);
                }

                return Ok(new { success = true, message = "Patient saved successfully." });
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }

        // POST: /Patient/DeletePatient
        [HttpPost]
        public async Task<IActionResult> DeletePatient([FromBody] DeletePatientRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
            {
                return BadRequest(new { success = false, message = "Patient ID is required." });
            }

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@Patient_ID", model.PatientId)
                };

                await _db.ExecuteNonQueryAsync("spPatient_Delete", parameters);

                return Ok(new { success = true, message = "Patient deleted successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }

        // GET: /Patient/GetPatientDocuments?patientId=...
        [HttpGet]
        public async Task<IActionResult> GetPatientDocuments(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest(new { success = false, message = "Patient ID is required." });
            }

            var parameters = new[] { new SqlParameter("@Patient_ID", patientId) };

            var dt = await _db.ExecuteDataTableAsync("spPatientDocument_List", parameters);
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    documentId = Convert.ToInt32(row["PatientDocument_ID"]),
                    patientId = row["Patient_ID"].ToString(),
                    fileName = row["FileName"].ToString(),
                    filePath = row["FilePath"].ToString(),
                    uploadedOn = row["UploadedOn"] == DBNull.Value
                        ? null
                        : ((DateTime)row["UploadedOn"]).ToString("yyyy-MM-dd HH:mm")
                });
            }

            return Ok(new { success = true, data = list });
        }

        // POST: /Patient/UploadPatientDocuments
        [HttpPost]
        [DisableRequestSizeLimit] // optional; configure global limits if needed
        public async Task<IActionResult> UploadPatientDocuments(string patientId, List<IFormFile> files)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest(new { success = false, message = "Patient ID is required." });
            }

            if (files == null || files.Count == 0)
            {
                return Ok(new { success = true, message = "No files to upload." });
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "patient", patientId);

            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            foreach (var file in files)
            {
                if (file.Length <= 0)
                    continue;

                // Only accept PDF for now
                if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    // You can choose to skip or fail the entire request; here we skip non-PDFs
                    continue;
                }

                // Generate a safe file name
                var originalFileName = Path.GetFileName(file.FileName);
                var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var safeFileName = $"{timeStamp}_{originalFileName}";

                var physicalPath = Path.Combine(uploadsRoot, safeFileName);

                // Save to disk
                using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Build relative path for serving
                var relativePath = $"/uploads/patient/{patientId}/{safeFileName}";

                var parameters = new[]
                { new SqlParameter("@Patient_ID", patientId), new SqlParameter("@FileName", originalFileName), new SqlParameter("@FilePath", relativePath), new SqlParameter("@ContentType", file.ContentType) };

                await _db.ExecuteNonQueryAsync("spPatientDocument_Insert", parameters);
            }
            return Ok(new { success = true, message = "Files uploaded successfully." });
        }

        public class DeletePatientDocumentRequest
        {
            public int DocumentId { get; set; }
        }

        // POST: /Patient/DeletePatientDocument
        [HttpPost]
        public async Task<IActionResult> DeletePatientDocument([FromBody] DeletePatientDocumentRequest model)
        {
            if (model == null || model.DocumentId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                // First get the file path
                var paramGet = new[]
                {
            new SqlParameter("@PatientDocument_ID", model.DocumentId)
        };

                var dt = await _db.ExecuteDataTableAsync("spPatientDocument_GetById", paramGet);

                string? filePath = null;

                if (dt.Rows.Count > 0)
                {
                    filePath = dt.Rows[0]["FilePath"].ToString();
                }

                // Delete DB row
                var paramDelete = new[]
                {
            new SqlParameter("@PatientDocument_ID", model.DocumentId)
        };

                await _db.ExecuteNonQueryAsync("spPatientDocument_Delete", paramDelete);

                // Delete file from disk
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    // filePath is like /uploads/patient/{Patient_ID}/filename.pdf
                    var physicalPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                }

                return Ok(new { success = true, message = "Document deleted successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }
    }
}
