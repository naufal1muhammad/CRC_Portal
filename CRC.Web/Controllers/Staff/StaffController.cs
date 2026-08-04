using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using CRC.Data.Database;
using CRC.Web.Infrastructure;
using CRC.Web.Services;

namespace CRC.Web.Controllers.Staff
{
    public class StaffController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly IDocumentStorage _documentStorage;
        private readonly ILogger<StaffController> _logger;

        // IWebHostEnvironment is deliberately gone: its only consumer was the web-root helper that resolved
        // the old on-disk document folder. Staff documents now live in the private blob container, and this
        // controller no longer knows anything about the local filesystem.
        public StaffController(DatabaseHelper db, IDocumentStorage documentStorage, ILogger<StaffController> logger)
        {
            _db = db;
            _documentStorage = documentStorage;
            _logger = logger;
        }

        // GET: /Staff
        [HttpGet]
        [Authorize(Policy = "AdminOrSuper")]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Staff/Edit/{id?}
        [HttpGet]
        [Authorize(Policy = "AdminOrSuper")]
        public IActionResult Edit(string? id)
        {
            ViewData["StaffId"] = id ?? string.Empty;
            return View("StaffEdit");
        }

        // GET: /Staff/GetActiveBranches
        [HttpGet]
        [Authorize(Policy = "AdminOrSuper")]
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
        [Authorize(Policy = "AdminOrSuper")]
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
        // Admin/Super can fetch any staff. STAFF role can only fetch their own record
        // (this endpoint is also used by /MyProfileStaff for the read-only profile page).
        [HttpGet]
        [Authorize(Policy = "AdminOrSuperOrStaff")]
        public async Task<IActionResult> GetStaff(string staffId)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                return Ok(new { success = true, data = (object?)null });
            }

            if (!User.CanAccessStaff(staffId))
            {
                return Forbid();
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
        [Authorize(Policy = "AdminOrSuper")]
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

                    AuditLog.StaffCreated(HttpContext, newStaffId, model.Name, model.NRIC,
                        model.Phone, model.Email, model.StaffTypeId, model.StaffBase);

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

                    AuditLog.StaffUpdated(HttpContext, model.StaffId, model.Name, model.NRIC,
                        model.Phone, model.Email, model.StaffTypeId, model.StaffBase);

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
                _logger.LogError(ex, "SqlException saving staff StaffId={StaffId}", model.StaffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving staff StaffId={StaffId}", model.StaffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }

        // POST: /Staff/SaveStaffWithDocuments
        // Saves Staff (insert/update) and stores any selected documents in the same request.
        [HttpPost]
        [Authorize(Policy = "AdminOrSuper")]
        public async Task<IActionResult> SaveStaffWithDocuments(
            [FromForm] SaveStaffRequest model,
            List<IFormFile>? files,
            List<string>? docTypeIds,
            List<string>? docTypeNames,
            List<int>? deleteDocIds)
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

            if (!DateTime.TryParse(model.BirthDate, out var birthDate))
            {
                return Ok(new { success = false, message = "Invalid birth date." });
            }

            // ----- Phase 1: Validate documents BEFORE saving anything -----
            var deleteSet = new HashSet<int>();
            if (deleteDocIds != null)
            {
                foreach (var id in deleteDocIds)
                {
                    if (id > 0) deleteSet.Add(id);
                }
            }

            // Mandatory documents for Staff Type
            var mandatoryDocs = await GetMandatoryDocsByStaffType(model.StaffTypeId);

            // Document types that will exist after this save (existing - pending deletes + new files)
            var resultingDocTypeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!model.IsNew)
            {
                if (string.IsNullOrWhiteSpace(model.StaffId))
                {
                    return Ok(new { success = false, message = "Staff ID is required for update." });
                }

                // Existing docs for this staff (exclude pending deletes)
                var existingParams = new[] { new SqlParameter("@Staff_ID", model.StaffId) };
                var existingDt = await _db.ExecuteDataTableAsync("spStaffDocument_List", existingParams);

                foreach (DataRow row in existingDt.Rows)
                {
                    int docId = row["StaffDocument_ID"] == DBNull.Value ? 0 : Convert.ToInt32(row["StaffDocument_ID"]);
                    if (docId > 0 && deleteSet.Contains(docId)) continue;

                    var typeId = row["StaffDocumentType_ID"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(typeId))
                    {
                        resultingDocTypeIds.Add(typeId);
                    }
                }
            }

            // New files in this request
            if (docTypeIds != null)
            {
                foreach (var t in docTypeIds)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        resultingDocTypeIds.Add(t);
                    }
                }
            }

            var missingDocNames = new List<string>();
            foreach (var doc in mandatoryDocs)
            {
                if (!resultingDocTypeIds.Contains(doc.Id))
                {
                    missingDocNames.Add(doc.Name);
                }
            }

            if (missingDocNames.Count > 0)
            {
                // IMPORTANT: Do not save staff OR documents if mandatory documents are missing.
                return Ok(new
                {
                    success = false,
                    message = "Please upload required documents: " + string.Join(", ", missingDocNames),
                    staffId = model.IsNew ? string.Empty : model.StaffId
                });
            }

            // ----- Phase 2: Commit everything atomically (DB transaction) -----
            docTypeIds ??= new List<string>();
            docTypeNames ??= new List<string>();
            files ??= new List<IFormFile>();

            // Validate the WHOLE batch before anything is written anywhere. A bad file at the end of a
            // selection must not leave the earlier ones already sitting in the container, and a rejection here
            // costs nothing because neither the transaction nor a single upload has started yet.
            foreach (var candidate in files)
            {
                if (candidate is null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "One of the selected files could not be read.",
                        staffId = model.IsNew ? string.Empty : model.StaffId
                    });
                }

                var (ok, validationMessage) = DocumentValidation.Validate(candidate);
                if (!ok)
                {
                    return Ok(new
                    {
                        success = false,
                        message = validationMessage,
                        staffId = model.IsNew ? string.Empty : model.StaffId
                    });
                }
            }

            // Two ledgers, because blob storage has no transaction of its own:
            //   uploadedBlobs   — keys written during THIS request, deleted again if the transaction fails.
            //   deleteBlobNames — keys of documents the user removed, deleted only AFTER the commit succeeds.
            // The asymmetry is the point. Storage is compensated by hand in both directions, and each
            // direction waits for the outcome that makes its removal safe.
            var uploadedBlobs = new List<string>();
            var deleteBlobNames = new List<string>();

            // Audit lines for the documents added and removed, held back until the commit. Writing them inside
            // the transaction would record changes that a rollback then erases.
            var pendingUploadAudits = new List<(string BlobName, string FileName, string DocTypeId, long SizeBytes)>();
            var pendingDeleteAudits = new List<(int DocumentId, string BlobName)>();

            string staffId = string.Empty;

            try
            {
                await using var conn = _db.CreateConnection();
                await conn.OpenAsync();
                await using var tx = conn.BeginTransaction();

                try
                {
                    if (model.IsNew)
                    {
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
                            new SqlParameter("@Staff_Type",        model.StaffTypeId)
                        };

                        var dt = await ExecDataTableAsync(conn, tx, "spStaff_Insert", insertParams);
                        staffId = string.Empty;
                        if (dt.Rows.Count > 0 && dt.Columns.Contains("NewStaff_ID"))
                        {
                            staffId = dt.Rows[0]["NewStaff_ID"]?.ToString() ?? string.Empty;
                        }

                        if (string.IsNullOrWhiteSpace(staffId))
                        {
                            throw new Exception("Failed to generate Staff ID.");
                        }
                    }
                    else
                    {
                        staffId = model.StaffId;

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

                        await ExecNonQueryAsync(conn, tx, "spStaff_Update", updateParams);
                    }

                    // Capture the blob keys of the docs being deleted (and delete the DB rows). Do NOT remove
                    // anything from storage yet — the transaction may still roll the rows back, and a deleted
                    // blob cannot be un-deleted.
                    foreach (var docId in deleteSet)
                    {
                        var dtDoc = await ExecDataTableAsync(conn, tx, "spStaffDocument_GetById",
                            new[] { new SqlParameter("@StaffDocument_ID", docId) });

                        if (dtDoc.Rows.Count > 0)
                        {
                            var blobName = dtDoc.Rows[0]["BlobName"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(blobName))
                            {
                                deleteBlobNames.Add(blobName);
                                pendingDeleteAudits.Add((docId, blobName));
                            }
                        }

                        await ExecNonQueryAsync(conn, tx, "spStaffDocument_Delete",
                            new[] { new SqlParameter("@StaffDocument_ID", docId) });
                    }

                    // Upload new documents (if any).
                    //
                    // THE ORDERING PROBLEM, and why the blob writes sit inside the transaction rather than before
                    // it. The blob key is staff/{Staff_ID}/{guid}{ext}, but for a NEW staff member the
                    // Staff_ID does not exist until spStaff_Insert has run — and spStaff_Insert only runs
                    // inside this transaction. So the upload cannot be hoisted out of the transaction the way
                    // the validation was: the key it needs has not been generated yet.
                    //
                    // The transaction therefore opens first, spStaff_Insert/spStaff_Update runs to obtain
                    // staffId, and only then do the bytes go to the container. What that costs is that a blob
                    // can be written and the transaction can still fail afterwards, so the guarantee is kept
                    // by compensation instead of by ordering: every key lands in uploadedBlobs as it is
                    // written, and the catch block below deletes all of them if anything throws. The
                    // alternative — pre-generating a Staff_ID in the application — would move id generation
                    // out of spStaff_Insert, where it belongs, to buy a rollback that a best-effort delete
                    // already provides.
                    for (int i = 0; i < files.Count; i++)
                    {
                        var file = files[i];

                        string tId = (i < docTypeIds.Count) ? docTypeIds[i] : string.Empty;
                        string tName = (i < docTypeNames.Count) ? docTypeNames[i] : string.Empty;

                        var safeFileName = DocumentValidation.SafeFileName(file.FileName);
                        var contentType = file.ContentType ?? "application/octet-stream";

                        // Server-generated key: staff/{Staff_ID}/{guid}{ext}. Nothing the user typed becomes
                        // part of it, and the bytes are streamed straight to the container — never to disk.
                        var blobName = DocumentValidation.BuildBlobName("staff", staffId, file.FileName);

                        await using (var stream = file.OpenReadStream())
                        {
                            await _documentStorage.UploadAsync(stream, blobName, contentType);
                        }

                        uploadedBlobs.Add(blobName);
                        pendingUploadAudits.Add((blobName, safeFileName, tId, file.Length));

                        var insertDocParams = new[]
                        {
                            new SqlParameter("@Staff_ID",              staffId),
                            new SqlParameter("@Staff_Name",            (object?)model.Name ?? DBNull.Value),
                            new SqlParameter("@StaffDocumentType_ID",  (object?)tId ?? DBNull.Value),
                            new SqlParameter("@StaffDocumentType_Name",(object?)tName ?? DBNull.Value),
                            new SqlParameter("@FileName",              safeFileName),
                            new SqlParameter("@BlobName",              blobName),
                            new SqlParameter("@ContentType",           contentType)
                        };

                        await ExecNonQueryAsync(conn, tx, "spStaffDocument_Insert", insertDocParams);
                    }

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                if (model.IsNew)
                {
                    AuditLog.StaffCreated(HttpContext, staffId, model.Name, model.NRIC,
                        model.Phone, model.Email, model.StaffTypeId, model.StaffBase);
                }
                else
                {
                    AuditLog.StaffUpdated(HttpContext, staffId, model.Name, model.NRIC,
                        model.Phone, model.Email, model.StaffTypeId, model.StaffBase);
                }

                // The document audit lines are written here, not in the loop above, because until the commit
                // returned there was no guarantee any of those rows would survive.
                foreach (var upload in pendingUploadAudits)
                {
                    // DocumentId is 0 because spStaffDocument_Insert writes its own AuditTrails row but does
                    // not hand the new identity back to the caller; the blob key is what ties this audit line
                    // to the row it describes.
                    AuditLog.StaffDocumentUploaded(HttpContext, staffId, 0, upload.DocTypeId,
                        upload.BlobName, upload.FileName, upload.SizeBytes);
                }

                // Remove the storage for deleted documents only AFTER a successful commit.
                await TryDeleteBlobsAsync(deleteBlobNames);

                foreach (var removed in pendingDeleteAudits)
                {
                    AuditLog.StaffDocumentDeleted(HttpContext, staffId, removed.DocumentId, removed.BlobName);
                }

                return Ok(new
                {
                    success = true,
                    message = model.IsNew ? "Staff created successfully." : "Staff updated successfully.",
                    staffId = staffId
                });
            }
            catch (SqlException ex)
            {
                // The transaction is already rolled back, so the DB is clean — but the blobs written above are
                // not, because storage took no part in it. Compensate by deleting exactly the keys this
                // request created. Cleanup failures are logged inside the helper and never rethrown: the
                // caller must see the ORIGINAL failure, not whatever went wrong while tidying up after it.
                await TryDeleteBlobsAsync(uploadedBlobs);
                _logger.LogError(ex, "SqlException saving staff with documents StaffId={StaffId}", staffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
            catch (Exception ex)
            {
                await TryDeleteBlobsAsync(uploadedBlobs);
                _logger.LogError(ex, "Unexpected error saving staff with documents StaffId={StaffId}", staffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }



        // --------------------
        // Helper methods for transactional stored-proc execution & file handling
        // --------------------

        private sealed class MandatoryDocInfo
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private async Task<List<MandatoryDocInfo>> GetMandatoryDocsByStaffType(string staffTypeId)
        {
            var list = new List<MandatoryDocInfo>();

            if (string.IsNullOrWhiteSpace(staffTypeId))
                return list;

            var settingsParams = new[]
            {
                new SqlParameter("@StaffType_ID", staffTypeId)
            };

            var settingsDt = await _db.ExecuteDataTableAsync("spStaffDocumentSettings_GetByStaffType", settingsParams);

            foreach (DataRow row in settingsDt.Rows)
            {
                var isMandatory = row["IsMandatory"] != DBNull.Value && Convert.ToInt32(row["IsMandatory"]) == 1;
                if (!isMandatory) continue;

                var docTypeId = row["StaffDocumentType_ID"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(docTypeId)) continue;

                var docTypeName = row["StaffDocumentType_Name"]?.ToString() ?? docTypeId;

                list.Add(new MandatoryDocInfo
                {
                    Id = docTypeId,
                    Name = docTypeName
                });
            }

            return list;
        }

        private async Task<DataTable> ExecDataTableAsync(SqlConnection conn, SqlTransaction tx, string storedProc, SqlParameter[]? parameters)
        {
            using var cmd = await _db.CreateStoredProcedureCommandAsync(conn, tx, storedProc, parameters);

            using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }

        private async Task<int> ExecNonQueryAsync(SqlConnection conn, SqlTransaction tx, string storedProc, SqlParameter[]? parameters)
        {
            using var cmd = await _db.CreateStoredProcedureCommandAsync(conn, tx, storedProc, parameters);
            return await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Removes blobs best-effort, one at a time, swallowing and logging every failure. This is the single
        /// compensation path for storage, used in both directions: to undo blob writes after a failed transaction,
        /// and to reclaim storage after a successful one.
        /// <para>
        /// Nothing here is allowed to throw. In the rollback case an exception would replace the real failure
        /// with a cleanup failure and the user would be told the wrong thing; in the post-commit case the rows
        /// are already gone and the user has already been told the delete worked. Either way the only casualty
        /// of a failed delete is an orphaned blob, which is an operational clean-up job — hence a warning in
        /// app-*.log rather than a faulted request.
        /// </para>
        /// </summary>
        private async Task TryDeleteBlobsAsync(IEnumerable<string> blobNames)
        {
            if (blobNames == null) return;

            foreach (var blobName in blobNames)
            {
                if (string.IsNullOrWhiteSpace(blobName)) continue;

                try
                {
                    await _documentStorage.DeleteAsync(blobName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete staff document blob {BlobName}", blobName);
                }
            }
        }

        // POST: /Staff/DeleteStaff
        [HttpPost]
        [Authorize(Policy = "AdminOrSuper")]
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

                // spStaff_Delete returns:
                //   Result set 1: Status ('Success' | 'Blocked' | 'NotFound'), Message
                //   Result set 2: BlobName rows for the StaffDocument blobs to remove (only when Success)
                var ds = await _db.ExecuteDataSetAsync("spStaff_Delete", parameters);

                string status = string.Empty;
                string message = string.Empty;

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    var row = ds.Tables[0].Rows[0];
                    status = row.Table.Columns.Contains("Status") ? row["Status"]?.ToString() ?? "" : "";
                    message = row.Table.Columns.Contains("Message") ? row["Message"]?.ToString() ?? "" : "";
                }

                if (!string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new
                    {
                        success = false,
                        message = string.IsNullOrWhiteSpace(message) ? "Failed to delete staff." : message
                    });
                }

                // Remove every StaffDocument blob for this staff member (best effort). The rows are already
                // gone; leaving the blobs would retain personal data after the record it belonged to was
                // deleted, which is the more serious half of an orphan.
                var blobNames = new List<string>();
                if (ds.Tables.Count > 1)
                {
                    foreach (DataRow fileRow in ds.Tables[1].Rows)
                    {
                        var blobName = fileRow["BlobName"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(blobName))
                        {
                            blobNames.Add(blobName);
                        }
                    }
                }

                await TryDeleteBlobsAsync(blobNames);

                AuditLog.StaffDocumentsPurged(HttpContext, model.StaffId, blobNames.Count);
                AuditLog.StaffDeleted(HttpContext, model.StaffId);

                return Ok(new { success = true, message = "Staff deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting staff StaffId={StaffId}", model.StaffId);
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
        [Authorize(Policy = "AdminOrSuper")]
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
        [Authorize(Policy = "AdminOrSuper")]
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
        [Authorize(Policy = "AdminOrSuper")]
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
        [Authorize(Policy = "AdminOrSuper")]
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
        [Authorize(Policy = "AdminOrSuper")]
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

            // Metadata only — the blob key is deliberately NOT projected. The container is private, so a
            // storage key is useless to the browser and is exactly the kind of detail that should never leave
            // the server. The file itself is fetched through GetStaffDocumentUrl, which mints a short-lived
            // read SAS for one document at click time.
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
                    contentType = row["ContentType"]?.ToString() ?? "",
                    uploadedOn = row["UploadedOn"]?.ToString() ?? ""
                });
            }

            return Ok(new { success = true, data = list });
        }

        // POST: /Staff/UploadStaffDocuments
        //
        // NO PAGE CURRENTLY CALLS THIS. Grepping every view and every .js file finds no caller —
        // js/staff/edit-staffbasic.js does all of its document work through SaveStaffWithDocuments. It is kept
        // and migrated because it is nonetheless a LIVE, AUTHENTICATED HTTP ENDPOINT: leaving it on the old
        // disk path would leave the public-file hole open behind an unused door. Deleting it is the owner's
        // call, not this change's.
        //
        // The request size limits are raised on purpose: several 20 MB documents have to fit inside a single
        // multipart body, and the ASP.NET Core default of roughly 30 MB would reject a two-file batch outright,
        // before any of the code below ever runs.
        [HttpPost]
        [Authorize(Policy = "AdminOrSuper")]
        [RequestSizeLimit(120_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 120_000_000)]
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

            // Validate the WHOLE batch first, in a pass of its own. A bad file has to fail BEFORE any blob is
            // written — otherwise the files that happened to be validated earlier are already in the container
            // when the request is rejected, and a refused upload leaves orphaned staff data behind.
            foreach (var candidate in files)
            {
                if (candidate is null)
                {
                    return Ok(new { success = false, message = "One of the selected files could not be read." });
                }

                var (ok, message) = DocumentValidation.Validate(candidate);
                if (!ok)
                {
                    return Ok(new { success = false, message });
                }
            }

            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];

                    string docTypeId = (i < docTypeIds.Count) ? docTypeIds[i] : string.Empty;
                    string docTypeName = (i < docTypeNames.Count) ? docTypeNames[i] : string.Empty;

                    var safeFileName = DocumentValidation.SafeFileName(file.FileName);
                    var contentType = file.ContentType ?? "application/octet-stream";

                    // Server-generated key: staff/{Staff_ID}/{guid}{ext}. Nothing the user typed becomes part
                    // of it, and the bytes are streamed straight to the container — never to disk.
                    var blobName = DocumentValidation.BuildBlobName("staff", staffId, file.FileName);

                    await using var stream = file.OpenReadStream();
                    await _documentStorage.UploadAsync(stream, blobName, contentType);

                    var parameters = new[]
                    {
                new SqlParameter("@Staff_ID",             staffId),
                new SqlParameter("@Staff_Name",           (object?)staffName ?? DBNull.Value),
                new SqlParameter("@StaffDocumentType_ID", (object?)docTypeId ?? DBNull.Value),
                new SqlParameter("@StaffDocumentType_Name",(object?)docTypeName ?? DBNull.Value),
                new SqlParameter("@FileName",             safeFileName),
                new SqlParameter("@BlobName",             blobName),
                new SqlParameter("@ContentType",          contentType)
            };

                    await _db.ExecuteNonQueryAsync("spStaffDocument_Insert", parameters);

                    // DocumentId is 0 because spStaffDocument_Insert writes its own AuditTrails row but does
                    // not hand the new identity back to the caller; the blob key is what ties this audit line
                    // to the row it describes.
                    AuditLog.StaffDocumentUploaded(HttpContext, staffId, 0, docTypeId ?? string.Empty,
                        blobName, safeFileName, file.Length);
                }

                return Ok(new { success = true, message = "Files uploaded successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading staff documents for StaffId={StaffId}", staffId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error uploading staff documents."));
            }
        }

        // GET: /Staff/GetStaffDocumentUrl?id=...
        //
        // Mints a short-lived read SAS URL for ONE document so the browser can fetch it straight from the
        // private container.
        //
        // The design, plainly: the container is private, so this five-minute URL is the ONLY way the browser
        // ever reaches the bytes. It is minted per click by this authenticated action, handed back once, never
        // persisted to the database and never rendered into the page's HTML. That is exactly what the old
        // static-file document links got wrong: static files are served ahead of authentication and get no
        // authorisation check at all, so every one of those links was public, and stayed public.
        //
        // The policy is "AdminOrSuper" — NOT the patient side's "AdminOrSuperOrStaff". Every other staff
        // document action in this controller uses AdminOrSuper, and a download must not be reachable by a role
        // that cannot already list the document.
        [Authorize(Policy = "AdminOrSuper")]
        [HttpGet]
        public async Task<IActionResult> GetStaffDocumentUrl(int id)
        {
            if (id <= 0)
            {
                return Ok(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spStaffDocument_GetById",
                    new[] { new SqlParameter("@StaffDocument_ID", id) }
                );

                if (dt.Rows.Count == 0)
                {
                    return Ok(new { success = false, message = "Document not found." });
                }

                var row = dt.Rows[0];
                var blobName = row["BlobName"]?.ToString();
                var fileName = row["FileName"]?.ToString() ?? string.Empty;
                var staffId = row["Staff_ID"]?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(blobName))
                {
                    return Ok(new { success = false, message = "Document not found." });
                }

                var url = _documentStorage.GetReadSasUrl(blobName, TimeSpan.FromMinutes(5));

                // Minting a SAS for a staff record IS a read of personal data, so it belongs on the audit
                // channel — and it has to be written before the URL leaves the server, because the download
                // itself happens against storage where the application can no longer observe it.
                AuditLog.StaffDocumentDownloaded(HttpContext, staffId, id, fileName);

                return Ok(new { success = true, url = url.ToString(), fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating download URL for StaffDocumentId={StaffDocumentId}", id);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error opening document."));
            }
        }

        public class DeleteStaffDocumentRequest
        {
            public int DocumentId { get; set; }
        }



        // POST: /Staff/DeleteStaffDocument
        //
        // NO PAGE CURRENTLY CALLS THIS either — js/staff/edit-staffbasic.js marks documents for deletion and
        // lets SaveStaffWithDocuments remove them inside its transaction. Migrated rather than deleted for the
        // same reason as UploadStaffDocuments above: it is a live authenticated endpoint, and removing a
        // public endpoint is the owner's decision.
        [HttpPost]
        [Authorize(Policy = "AdminOrSuper")]
        public async Task<IActionResult> DeleteStaffDocument([FromBody] DeleteStaffDocumentRequest model)
        {
            if (model == null || model.DocumentId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                // Read the row first, only so the audit line can name the staff member the document belonged
                // to — spStaffDocument_Delete hands back the blob key and nothing else.
                var lookup = await _db.ExecuteDataTableAsync(
                    "spStaffDocument_GetById",
                    new[] { new SqlParameter("@StaffDocument_ID", model.DocumentId) }
                );

                var staffId = lookup.Rows.Count > 0
                    ? lookup.Rows[0]["Staff_ID"]?.ToString() ?? string.Empty
                    : string.Empty;

                var deletedBlobNameParameter = new SqlParameter("@DeletedBlobName", SqlDbType.VarChar, 500)
                {
                    Direction = ParameterDirection.Output
                };

                var parameters = new[]
                {
                    new SqlParameter("@StaffDocument_ID", model.DocumentId),
                    deletedBlobNameParameter
                };

                await _db.ExecuteNonQueryAsync("spStaffDocument_Delete", parameters);

                // NULL means no row was deleted, so there is nothing in storage to remove.
                var deletedBlobName = deletedBlobNameParameter.Value == DBNull.Value
                    ? null
                    : deletedBlobNameParameter.Value?.ToString();

                if (!string.IsNullOrWhiteSpace(deletedBlobName))
                {
                    await TryDeleteBlobsAsync(new[] { deletedBlobName });

                    AuditLog.StaffDocumentDeleted(HttpContext, staffId, model.DocumentId, deletedBlobName);
                }

                return Ok(new { success = true, message = "Document deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting staff document StaffDocumentId={StaffDocumentId}", model.DocumentId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error deleting staff document."));
            }
        }

        [HttpGet]
        [Authorize(Policy = "AdminOrSuper")]
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
        [Authorize(Policy = "AdminOrSuper")]
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
