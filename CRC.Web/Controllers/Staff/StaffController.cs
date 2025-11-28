using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using CRC.Web.Data;

namespace CRC.Web.Controllers.Staff
{
    [Authorize]
    public class StaffController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly IWebHostEnvironment _env;

        public StaffController(DatabaseHelper db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET: /Staff
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Staff/GetActiveBranches
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

        // GET: /Staff/GetStaffList
        [HttpGet]
        public async Task<IActionResult> GetStaffList()
        {
            var dt = await _db.ExecuteDataTableAsync("spStaff_List");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    staffId = row["Staff_ID"].ToString(),
                    name = row["Staff_Name"].ToString(),
                    nric = row["Staff_NRIC"].ToString(),
                    phone = row["Staff_Phone"].ToString(),
                    email = row["Staff_Email"].ToString(),
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString(),
                    staffType = row["Staff_Type"] == DBNull.Value ? 0 : Convert.ToInt32(row["Staff_Type"])
                });
            }

            return Ok(list);
        }

        // GET: /Staff/GetStaff?staffId=...
        [HttpGet]
        public async Task<IActionResult> GetStaff(string staffId)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                return BadRequest(new { success = false, message = "Staff ID is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Staff_ID", staffId)
            };

            var dt = await _db.ExecuteDataTableAsync("spStaff_GetById", parameters);

            if (dt.Rows.Count == 0)
            {
                return Ok(new { success = false, message = "Staff not found." });
            }

            var row = dt.Rows[0];

            return Ok(new
            {
                success = true,
                data = new
                {
                    staffId = row["Staff_ID"].ToString(),
                    name = row["Staff_Name"].ToString(),
                    nric = row["Staff_NRIC"].ToString(),
                    phone = row["Staff_Phone"].ToString(),
                    email = row["Staff_Email"].ToString(),
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString(),
                    staffType = row["Staff_Type"] == DBNull.Value ? 0 : Convert.ToInt32(row["Staff_Type"])
                }
            });
        }

        public class SaveStaffRequest
        {
            public bool IsNew { get; set; }
            public string StaffId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NRIC { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string BranchId { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public int StaffType { get; set; }
        }

        public class DeleteStaffRequest
        {
            public string StaffId { get; set; } = string.Empty;
        }

        // POST: /Staff/SaveStaff
        [HttpPost]
        public async Task<IActionResult> SaveStaff([FromBody] SaveStaffRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid data." });
            }

            if (string.IsNullOrWhiteSpace(model.StaffId) ||
                string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.NRIC) ||
                string.IsNullOrWhiteSpace(model.Phone) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.BranchId) ||
                string.IsNullOrWhiteSpace(model.BranchName) ||
                model.StaffType <= 0)
            {
                return Ok(new { success = false, message = "Please fill in all required fields." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Staff_ID", model.StaffId),
                new SqlParameter("@Staff_Name", model.Name),
                new SqlParameter("@Staff_NRIC", model.NRIC),
                new SqlParameter("@Staff_Phone", model.Phone),
                new SqlParameter("@Staff_Email", model.Email),
                new SqlParameter("@Branch_ID", model.BranchId),
                new SqlParameter("@Branch_Name", model.BranchName),
                new SqlParameter("@Staff_Type", model.StaffType)
            };

            try
            {
                if (model.IsNew)
                {
                    await _db.ExecuteNonQueryAsync("spStaff_Insert", parameters);
                }
                else
                {
                    await _db.ExecuteNonQueryAsync("spStaff_Update", parameters);
                }

                return Ok(new { success = true, message = "Staff saved successfully." });
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

        // POST: /Staff/DeleteStaff
        [HttpPost]
        public async Task<IActionResult> DeleteStaff([FromBody] DeleteStaffRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.StaffId))
            {
                return BadRequest(new { success = false, message = "Staff ID is required." });
            }

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@Staff_ID", model.StaffId)
                };

                await _db.ExecuteNonQueryAsync("spStaff_Delete", parameters);

                return Ok(new { success = true, message = "Staff deleted successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }

        // ----- Documents -----

        // GET: /Staff/GetStaffDocuments?staffId=...
        [HttpGet]
        public async Task<IActionResult> GetStaffDocuments(string staffId)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                return BadRequest(new { success = false, message = "Staff ID is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Staff_ID", staffId)
            };

            var dt = await _db.ExecuteDataTableAsync("spStaffDocument_List", parameters);
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    documentId = Convert.ToInt32(row["StaffDocument_ID"]),
                    staffId = row["Staff_ID"].ToString(),
                    staffName = row["Staff_Name"].ToString(),
                    fileName = row["FileName"].ToString(),
                    filePath = row["FilePath"].ToString(),
                    uploadedOn = row["UploadedOn"] == DBNull.Value
                        ? null
                        : ((DateTime)row["UploadedOn"]).ToString("yyyy-MM-dd HH:mm")
                });
            }

            return Ok(new { success = true, data = list });
        }

        // POST: /Staff/UploadStaffDocuments
        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadStaffDocuments(string staffId, string staffName, List<IFormFile> files)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                return BadRequest(new { success = false, message = "Staff ID is required." });
            }

            if (files == null || files.Count == 0)
            {
                return Ok(new { success = true, message = "No files to upload." });
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "staff", staffId);

            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            foreach (var file in files)
            {
                if (file.Length <= 0)
                    continue;

                if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // skip non-PDF
                }

                var originalFileName = Path.GetFileName(file.FileName);
                var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var safeFileName = $"{timeStamp}_{originalFileName}";

                var physicalPath = Path.Combine(uploadsRoot, safeFileName);

                using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/staff/{staffId}/{safeFileName}";

                var parameters = new[]
                {
                    new SqlParameter("@Staff_ID", staffId),
                    new SqlParameter("@Staff_Name", staffName),
                    new SqlParameter("@FileName", originalFileName),
                    new SqlParameter("@FilePath", relativePath),
                    new SqlParameter("@ContentType", file.ContentType)
                };

                await _db.ExecuteNonQueryAsync("spStaffDocument_Insert", parameters);
            }

            return Ok(new { success = true, message = "Files uploaded successfully." });
        }

        public class DeleteStaffDocumentRequest
        {
            public int DocumentId { get; set; }
        }

        // POST: /Staff/DeleteStaffDocument
        [HttpPost]
        public async Task<IActionResult> DeleteStaffDocument([FromBody] DeleteStaffDocumentRequest model)
        {
            if (model == null || model.DocumentId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                // Get file path first
                var paramGet = new[]
                {
                    new SqlParameter("@StaffDocument_ID", model.DocumentId)
                };

                var dt = await _db.ExecuteDataTableAsync("spStaffDocument_GetById", paramGet);

                string? filePath = null;

                if (dt.Rows.Count > 0)
                {
                    filePath = dt.Rows[0]["FilePath"].ToString();
                }

                // Delete DB row
                var paramDelete = new[]
                {
                    new SqlParameter("@StaffDocument_ID", model.DocumentId)
                };

                await _db.ExecuteNonQueryAsync("spStaffDocument_Delete", paramDelete);

                // Delete file from disk
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    var physicalPath = Path.Combine(
                        _env.WebRootPath,
                        filePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

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