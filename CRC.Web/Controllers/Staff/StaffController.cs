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
    [Authorize(Policy = "SuperUserOnly")]
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

        // GET: /Staff/Edit/{id?}
        [HttpGet]
        public IActionResult Edit(string? id)
        {
            ViewData["StaffId"] = id ?? string.Empty;
            return View("StaffEdit");
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
                    staffTypeId = row["Staff_Type"]?.ToString() ?? "",
                    staffTypeName = row["StaffType_Name"]?.ToString() ?? ""
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
                return Ok(new { success = true, data = (object?)null });
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
                    birthDate = ToDateInputString(row["Staff_BirthDate"]),
                    age = row["Staff_Age"] == DBNull.Value ? 0 : Convert.ToInt32(row["Staff_Age"]),
                    phone = row["Staff_Phone"].ToString(),
                    email = row["Staff_Email"].ToString(),
                    gender = row["Staff_Gender"]?.ToString() ?? "",
                    resState = row["Staff_ResState"]?.ToString() ?? "",
                    resCity = row["Staff_ResCity"]?.ToString() ?? "",
                    resPostcode = row["Staff_ResPostcode"]?.ToString() ?? "",
                    addLine1 = row["Staff_AddLine1"]?.ToString() ?? "",
                    addLine2 = row["Staff_AddLine2"]?.ToString() ?? "",
                    staffBase = row["Staff_Base"]?.ToString() ?? "",
                    staffTypeId = row["Staff_Type"]?.ToString() ?? "",
                    staffTypeName = row["StaffType_Name"]?.ToString() ?? ""
                }
            });
        }

        private static string? ToDateInputString(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (DateTime.TryParse(value.ToString(), out var dt))
            {
                return dt.ToString("yyyy-MM-dd");
            }
            return null;
        }

        public class SaveStaffRequest
        {
            public bool IsNew { get; set; }

            public string StaffId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NRIC { get; set; } = string.Empty;
            public string BirthDate { get; set; } = string.Empty;
            public int Age { get; set; }
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Gender { get; set; } = string.Empty;
            public string ResState { get; set; } = string.Empty;
            public string ResCity { get; set; } = string.Empty;
            public string ResPostcode { get; set; } = string.Empty;
            public string AddLine1 { get; set; } = string.Empty;
            public string AddLine2 { get; set; } = string.Empty;
            public string StaffBase { get; set; } = string.Empty;

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
                string.IsNullOrWhiteSpace(model.BirthDate) ||
                model.Age <= 0 ||
                string.IsNullOrWhiteSpace(model.Phone) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.Gender) ||
                string.IsNullOrWhiteSpace(model.ResState) ||
                string.IsNullOrWhiteSpace(model.ResCity) ||
                string.IsNullOrWhiteSpace(model.ResPostcode) ||
                string.IsNullOrWhiteSpace(model.AddLine1) ||
                string.IsNullOrWhiteSpace(model.AddLine2) ||
                string.IsNullOrWhiteSpace(model.StaffBase) ||
                string.IsNullOrWhiteSpace(model.StaffTypeId))
            {
                return Ok(new { success = false, message = "Please fill in all required fields." });
            }

            try
            {
                if (!DateTime.TryParse(model.BirthDate, out var birthDate))
                {
                    return Ok(new { success = false, message = "Invalid birth date." });
                }

                if (model.IsNew)
                {
                    // INSERT: Staff_ID is auto-generated in spStaff_Insert
                    var insertParams = new[]
                    {
                new SqlParameter("@Staff_Name",        model.Name),
                new SqlParameter("@Staff_NRIC",        model.NRIC),
                new SqlParameter("@Staff_BirthDate",   birthDate),
                new SqlParameter("@Staff_Age",         model.Age),
                new SqlParameter("@Staff_Phone",       model.Phone),
                new SqlParameter("@Staff_Email",       model.Email),
                new SqlParameter("@Staff_Gender",      model.Gender),
                new SqlParameter("@Staff_ResState",    model.ResState),
                new SqlParameter("@Staff_ResCity",     model.ResCity),
                new SqlParameter("@Staff_ResPostcode", model.ResPostcode),
                new SqlParameter("@Staff_AddLine1",    model.AddLine1),
                new SqlParameter("@Staff_AddLine2",    model.AddLine2),
                new SqlParameter("@Staff_Base",        model.StaffBase),
                new SqlParameter("@Staff_Type",        model.StaffTypeId) // StaffType_ID
            };

                    var dt = await _db.ExecuteDataTableAsync("spStaff_Insert", insertParams);

                    string newStaffId = string.Empty;
                    if (dt.Rows.Count > 0 && dt.Columns.Contains("NewStaff_ID"))
                    {
                        newStaffId = dt.Rows[0]["NewStaff_ID"]?.ToString() ?? "";
                    }

                    // If documents were uploaded before saving Basic Details (using a temporary Staff ID),
                    // move them to the newly generated Staff ID before validating mandatory docs.
                    await ReassignTempStaffDocumentsIfAny(model.StaffId, newStaffId, model.Name);

                    var missingDocs = await GetMissingMandatoryDocuments(model.StaffTypeId, newStaffId);
                    if (missingDocs.Count > 0)
                    {
                        return Ok(new
                        {
                            success = false,
                            message = "Please upload required documents: " + string.Join(", ", missingDocs),
                            staffId = newStaffId
                        });
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
                new SqlParameter("@Staff_ID",          model.StaffId),
                new SqlParameter("@Staff_Name",        model.Name),
                new SqlParameter("@Staff_NRIC",        model.NRIC),
                new SqlParameter("@Staff_BirthDate",   birthDate),
                new SqlParameter("@Staff_Age",         model.Age),
                new SqlParameter("@Staff_Phone",       model.Phone),
                new SqlParameter("@Staff_Email",       model.Email),
                new SqlParameter("@Staff_Gender",      model.Gender),
                new SqlParameter("@Staff_ResState",    model.ResState),
                new SqlParameter("@Staff_ResCity",     model.ResCity),
                new SqlParameter("@Staff_ResPostcode", model.ResPostcode),
                new SqlParameter("@Staff_AddLine1",    model.AddLine1),
                new SqlParameter("@Staff_AddLine2",    model.AddLine2),
                new SqlParameter("@Staff_Base",        model.StaffBase),
                new SqlParameter("@Staff_Type",        model.StaffTypeId)
            };

                    await _db.ExecuteNonQueryAsync("spStaff_Update", updateParams);

                    var missingDocs = await GetMissingMandatoryDocuments(model.StaffTypeId, model.StaffId);
                    if (missingDocs.Count > 0)
                    {
                        return Ok(new
                        {
                            success = false,
                            message = "Please upload required documents: " + string.Join(", ", missingDocs),
                            staffId = model.StaffId
                        });
                    }

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

        private async Task ReassignTempStaffDocumentsIfAny(string tempStaffId, string newStaffId, string staffName)
        {
            if (string.IsNullOrWhiteSpace(tempStaffId) || string.IsNullOrWhiteSpace(newStaffId))
            {
                return;
            }

            if (tempStaffId.Equals(newStaffId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Only allow reassignment from our client-generated temp IDs.
            if (!tempStaffId.StartsWith("TMP-", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var listParams = new[]
            {
                new SqlParameter("@Staff_ID", tempStaffId)
            };

            var dt = await _db.ExecuteDataTableAsync("spStaffDocument_List", listParams);
            if (dt.Rows.Count == 0)
            {
                return;
            }

            foreach (DataRow row in dt.Rows)
            {
                var oldId = row["StaffDocument_ID"] == DBNull.Value ? 0 : Convert.ToInt32(row["StaffDocument_ID"]);

                var docTypeId = row["StaffDocumentType_ID"]?.ToString() ?? "";
                var docTypeName = row["StaffDocumentType_Name"]?.ToString() ?? "";
                var fileName = row["FileName"]?.ToString() ?? "";
                var filePath = row["FilePath"]?.ToString() ?? "";
                var contentType = row["ContentType"]?.ToString() ?? "application/octet-stream";

                var insertParams = new[]
                {
                    new SqlParameter("@Staff_ID", newStaffId),
                    new SqlParameter("@Staff_Name", string.IsNullOrWhiteSpace(staffName) ? (object)DBNull.Value : staffName),
                    new SqlParameter("@StaffDocumentType_ID", string.IsNullOrWhiteSpace(docTypeId) ? (object)DBNull.Value : docTypeId),
                    new SqlParameter("@StaffDocumentType_Name", string.IsNullOrWhiteSpace(docTypeName) ? (object)DBNull.Value : docTypeName),
                    new SqlParameter("@FileName", fileName),
                    new SqlParameter("@FilePath", filePath),
                    new SqlParameter("@ContentType", contentType)
                };

                await _db.ExecuteNonQueryAsync("spStaffDocument_Insert", insertParams);

                if (oldId > 0)
                {
                    var deleteParams = new[]
                    {
                        new SqlParameter("@StaffDocument_ID", oldId)
                    };

                    await _db.ExecuteNonQueryAsync("spStaffDocument_Delete", deleteParams);
                }
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

        private async Task<List<string>> GetMissingMandatoryDocuments(string staffTypeId, string staffId)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(staffTypeId) || string.IsNullOrWhiteSpace(staffId))
            {
                return missing;
            }

            var settingsParams = new[]
            {
                new SqlParameter("@StaffType_ID", staffTypeId)
            };

            var settingsDt = await _db.ExecuteDataTableAsync("spStaffDocumentSettings_GetByStaffType", settingsParams);
            var mandatoryDocs = new List<(string Id, string Name)>();

            foreach (DataRow row in settingsDt.Rows)
            {
                var isMandatory = row["IsMandatory"] != DBNull.Value && Convert.ToInt32(row["IsMandatory"]) == 1;
                if (!isMandatory) continue;

                var docTypeId = row["StaffDocumentType_ID"]?.ToString() ?? "";
                var docTypeName = row["StaffDocumentType_Name"]?.ToString() ?? docTypeId;

                if (!string.IsNullOrWhiteSpace(docTypeId))
                {
                    mandatoryDocs.Add((docTypeId, docTypeName));
                }
            }

            if (mandatoryDocs.Count == 0)
            {
                return missing;
            }

            var docParams = new[]
            {
                new SqlParameter("@Staff_ID", staffId)
            };

            var docDt = await _db.ExecuteDataTableAsync("spStaffDocument_List", docParams);
            var existingDocTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in docDt.Rows)
            {
                var docTypeId = row["StaffDocumentType_ID"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(docTypeId))
                {
                    existingDocTypes.Add(docTypeId);
                }
            }

            foreach (var doc in mandatoryDocs)
            {
                if (!existingDocTypes.Contains(doc.Id))
                {
                    missing.Add(doc.Name);
                }
            }

            return missing;
        }

        // ----- Basic lookups -----

        [HttpGet]
        public async Task<IActionResult> GetStaffLookups()
        {
            try
            {
                var dtStaffTypes = await _db.ExecuteDataTableAsync("spLU_STAFFTYPE_List");
                var dtStates = await _db.ExecuteDataTableAsync("spLU_LOCATION_ListStates");

                var staffTypes = new List<object>();
                foreach (DataRow row in dtStaffTypes.Rows)
                {
                    staffTypes.Add(new
                    {
                        staffTypeId = row["StaffType_ID"]?.ToString() ?? "",
                        staffTypeName = row["StaffType_Name"]?.ToString() ?? ""
                    });
                }

                var states = new List<object>();
                foreach (DataRow row in dtStates.Rows)
                {
                    states.Add(new
                    {
                        id = row["LocationId"]?.ToString() ?? "",
                        name = row["Name"]?.ToString() ?? ""
                    });
                }

                return Ok(new { success = true, staffTypes, states });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading staff lookups." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCitiesByState(int stateId)
        {
            if (stateId <= 0)
            {
                return Ok(new { success = false, message = "State is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@StateId", stateId)
            };

            var dt = await _db.ExecuteDataTableAsync("spLU_LOCATION_ListCityByState", parameters);
            var cities = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                cities.Add(new
                {
                    id = row["LocationId"]?.ToString() ?? "",
                    name = row["Name"]?.ToString() ?? ""
                });
            }

            return Ok(new { success = true, cities });
        }

        [HttpGet]
        public async Task<IActionResult> GetPostcodesByCity(int cityId)
        {
            if (cityId <= 0)
            {
                return Ok(new { success = false, message = "City is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@CityId", cityId)
            };

            var dt = await _db.ExecuteDataTableAsync("spLU_LOCATION_ListPostcodesByCity", parameters);
            var postcodes = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                postcodes.Add(new
                {
                    id = row["LocationId"]?.ToString() ?? "",
                    name = row["Name"]?.ToString() ?? ""
                });
            }

            return Ok(new { success = true, postcodes });
        }

        // ----- Documents -----

        [HttpGet]
        public async Task<IActionResult> GetStaffDocumentTypes()
        {
            var dt = await _db.ExecuteDataTableAsync("spLU_STAFFDOCUMENTTYPE_List");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    documentTypeId = row["StaffDocumentType_ID"]?.ToString() ?? "",
                    documentTypeName = row["StaffDocumentType_Name"]?.ToString() ?? ""
                });
            }

            return Ok(new { success = true, data = list });
        }

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