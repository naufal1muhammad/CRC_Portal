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
                    staffTypeId = row["Staff_Type"]?.ToString() ?? ""
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
                    staffTypeId = row["Staff_Type"]?.ToString() ?? ""
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

            // This stores StaffType_ID from LU_STAFFTYPE
            public string StaffTypeId { get; set; } = string.Empty;
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

            if (string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.NRIC) ||
                string.IsNullOrWhiteSpace(model.Phone) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.BranchId) ||
                string.IsNullOrWhiteSpace(model.BranchName) ||
                string.IsNullOrWhiteSpace(model.StaffTypeId))
            {
                return Ok(new { success = false, message = "Please fill in all required fields." });
            }

            try
            {
                if (model.IsNew)
                {
                    // INSERT: Staff_ID is auto-generated in spStaff_Insert
                    var insertParams = new[]
                    {
                new SqlParameter("@Staff_Name",  model.Name),
                new SqlParameter("@Staff_NRIC",  model.NRIC),
                new SqlParameter("@Staff_Phone", model.Phone),
                new SqlParameter("@Staff_Email", model.Email),
                new SqlParameter("@Branch_ID",   model.BranchId),
                new SqlParameter("@Branch_Name", (object?)model.BranchName ?? DBNull.Value),
                new SqlParameter("@Staff_Type",  model.StaffTypeId) // StaffType_ID
            };

                    var dt = await _db.ExecuteDataTableAsync("spStaff_Insert", insertParams);

                    string newStaffId = string.Empty;
                    if (dt.Rows.Count > 0 && dt.Columns.Contains("NewStaff_ID"))
                    {
                        newStaffId = dt.Rows[0]["NewStaff_ID"]?.ToString() ?? "";
                    }

                    return Ok(new
                    {
                        success = true,
                        message = "Staff created successfully.",
                        staffId = newStaffId
                    });
                }
                else
                {
                    // UPDATE: Staff_ID must exist
                    if (string.IsNullOrWhiteSpace(model.StaffId))
                    {
                        return Ok(new { success = false, message = "Staff ID is required for update." });
                    }

                    var updateParams = new[]
                    {
                new SqlParameter("@Staff_ID",    model.StaffId),
                new SqlParameter("@Staff_Name",  model.Name),
                new SqlParameter("@Staff_NRIC",  model.NRIC),
                new SqlParameter("@Staff_Phone", model.Phone),
                new SqlParameter("@Staff_Email", model.Email),
                new SqlParameter("@Branch_ID",   model.BranchId),
                new SqlParameter("@Branch_Name", (object?)model.BranchName ?? DBNull.Value),
                new SqlParameter("@Staff_Type",  model.StaffTypeId)
            };

                    await _db.ExecuteNonQueryAsync("spStaff_Update", updateParams);

                    return Ok(new
                    {
                        success = true,
                        message = "Staff updated successfully.",
                        staffId = model.StaffId
                    });
                }
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
                return Ok(new { success = false, message = "Staff ID is required." });
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
                    staffId = row["Staff_ID"]?.ToString() ?? "",
                    staffName = row["Staff_Name"]?.ToString() ?? "",
                    staffDocumentTypeId = row["StaffDocumentType_ID"]?.ToString() ?? "",
                    staffDocumentTypeName = row["StaffDocumentType_Name"]?.ToString() ?? "",
                    fileName = row["FileName"]?.ToString() ?? "",
                    filePath = row["FilePath"]?.ToString() ?? "",
                    contentType = row["ContentType"]?.ToString() ?? "",
                    uploadedOn = row["UploadedOn"]?.ToString() ?? ""
                });
            }

            return Ok(new { success = true, data = list });
        }

        // POST: /Staff/UploadStaffDocuments
        [HttpPost]
        public async Task<IActionResult> UploadStaffDocuments(
            string staffId,
            string staffName,
            List<IFormFile> files,
            List<string>? docTypeIds,
            List<string>? docTypeNames)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                return Ok(new { success = false, message = "Staff ID is required for document upload." });
            }

            if (files == null || files.Count == 0)
            {
                return Ok(new { success = true, message = "No files to upload." });
            }

            docTypeIds ??= new List<string>();
            docTypeNames ??= new List<string>();

            // Adjust path as per your project
            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "staff");
            if (!Directory.Exists(uploadRoot))
            {
                Directory.CreateDirectory(uploadRoot);
            }

            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file == null || file.Length == 0) continue;

                    string docTypeId = (i < docTypeIds.Count) ? docTypeIds[i] : string.Empty;
                    string docTypeName = (i < docTypeNames.Count) ? docTypeNames[i] : string.Empty;

                    var originalFileName = Path.GetFileName(file.FileName);
                    var uniqueFileName = $"{Guid.NewGuid():N}_{originalFileName}";

                    var filePath = Path.Combine(uploadRoot, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var relativePath = $"/uploads/staff/{uniqueFileName}";
                    var contentType = file.ContentType ?? "application/octet-stream";

                    var parameters = new[]
                    {
                new SqlParameter("@Staff_ID",             staffId),
                new SqlParameter("@Staff_Name",           (object?)staffName ?? DBNull.Value),
                new SqlParameter("@StaffDocumentType_ID", (object?)docTypeId ?? DBNull.Value),
                new SqlParameter("@StaffDocumentType_Name",(object?)docTypeName ?? DBNull.Value),
                new SqlParameter("@FileName",             originalFileName),
                new SqlParameter("@FilePath",             relativePath),
                new SqlParameter("@ContentType",          contentType)
            };

                    await _db.ExecuteNonQueryAsync("spStaffDocument_Insert", parameters);
                }

                return Ok(new { success = true, message = "Files uploaded successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error uploading staff documents." });
            }
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

        [HttpGet]
        public async Task<IActionResult> GetStaffTypes()
        {
            var dt = await _db.ExecuteDataTableAsync("spLU_STAFFTYPE_List");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    staffTypeId = row["StaffType_ID"]?.ToString() ?? "",
                    staffTypeName = row["StaffType_Name"]?.ToString() ?? ""
                });
            }

            return Ok(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetMandatoryDocumentsForStaffType(string staffTypeId)
        {
            if (string.IsNullOrWhiteSpace(staffTypeId))
            {
                return Ok(new { success = false, message = "Staff type is required." });
            }

            var parameters = new[]
            {
        new SqlParameter("@StaffType_ID", staffTypeId)
    };

            var dt = await _db.ExecuteDataTableAsync("spStaffDocumentSettings_GetByStaffType", parameters);
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                bool isMandatory = row["IsMandatory"] != DBNull.Value && Convert.ToInt32(row["IsMandatory"]) == 1;
                if (!isMandatory)
                    continue;

                list.Add(new
                {
                    staffDocumentTypeId = row["StaffDocumentType_ID"]?.ToString() ?? "",
                    staffDocumentTypeName = row["StaffDocumentType_Name"]?.ToString() ?? ""
                });
            }

            return Ok(new { success = true, data = list });
        }
    }
}