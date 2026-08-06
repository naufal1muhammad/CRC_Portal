using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using CRC.Data.Data;
using CRC.Data.Models;
using CRC.Web.Infrastructure;
using CRC.Web.Services;

namespace CRC.Web.Controllers.Staff
{
    public class StaffController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly IDocumentStorage _documentStorage;
        private readonly ILogger<StaffController> _logger;

        // IWebHostEnvironment is deliberately gone: its only consumer was the web-root helper that resolved
        // the old on-disk document folder. Staff documents now live in the private blob container, and this
        // controller no longer knows anything about the local filesystem.
        //
        // DatabaseHelper is gone for a different reason: every stored-procedure call in this file now goes
        // through IDatabaseData, which is the only type in the solution that names one (CoreFlow.md §6).
        // IDocumentStorage stays — storage is not the database, and CRC.Data must never learn about it.
        public StaffController(IDatabaseData data, IDocumentStorage documentStorage, ILogger<StaffController> logger)
        {
            _data = data;
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
            var branches = await _data.GetActiveBranchesAsync();
            var list = new List<object>();

            // Branch_State is selected by the procedure and deliberately NOT projected: this endpoint feeds
            // the staff form's branch dropdown, which shows the name only.
            foreach (var branch in branches)
            {
                list.Add(new
                {
                    branchId = branch.Branch_ID,
                    branchName = branch.Branch_Name
                });
            }

            return Ok(list);
        }

        // GET: /Staff/GetStaffList
        [HttpGet]
        [Authorize(Policy = "AdminOrSuper")]
        public async Task<IActionResult> GetStaffList()
        {
            var staffList = await _data.GetAllStaffAsync();
            var list = new List<object>();

            // The anonymous object below is a public contract: wwwroot/js/staff/ reads every one of these
            // camelCase names. The model is mapped INTO it and never returned directly.
            foreach (var staff in staffList)
            {
                list.Add(new
                {
                    staffId = staff.Staff_ID,
                    name = staff.Staff_Name,
                    nric = staff.Staff_NRIC,
                    phone = staff.Staff_Phone,
                    email = staff.Staff_Email,
                    staffTypeId = staff.Staff_Type,
                    // StaffType_Name comes from a LEFT JOIN and really can be null — nothing constrains
                    // Staff.Staff_Type to LU_STAFFTYPE. The DataTable code returned "" for that (a DBNull's
                    // ToString() is ""), and the table would render the word "null" without this coalesce.
                    staffTypeName = staff.StaffType_Name ?? ""
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

            var staff = await _data.GetStaffByIdAsync(staffId);

            if (staff == null)
            {
                return Ok(new { success = false, message = "Staff not found." });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    staffId = staff.Staff_ID,
                    name = staff.Staff_Name,
                    nric = staff.Staff_NRIC,
                    birthDate = ToDateInputString(staff.Staff_BirthDate),
                    age = staff.Staff_Age ?? 0,
                    phone = staff.Staff_Phone,
                    email = staff.Staff_Email,
                    gender = staff.Staff_Gender ?? "",
                    resState = staff.Staff_ResState ?? "",
                    resCity = staff.Staff_ResCity ?? "",
                    resPostcode = staff.Staff_ResPostcode ?? "",
                    addLine1 = staff.Staff_AddLine1 ?? "",
                    addLine2 = staff.Staff_AddLine2 ?? "",
                    staffBase = staff.Staff_Base ?? "",
                    staffTypeId = staff.Staff_Type ?? "",
                    staffTypeName = staff.StaffType_Name ?? ""
                }
            });
        }

        // The <input type="date"> the edit form binds to accepts exactly "yyyy-MM-dd" and nothing else, so
        // the date is formatted here rather than left to JSON's default DateTime serialization. A null
        // stays null — the field renders empty — which is what the DataTable version did for a DBNull.
        private static string? ToDateInputString(DateTime? value)
        {
            return value?.ToString("yyyy-MM-dd");
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

        // Maps the form/JSON request onto the data layer's input model. Both save actions build the same
        // thing, so it is built in one place — a mis-ordered NRIC and phone number would be two strings
        // that compile fine and save a staff member whose details are swapped.
        private static StaffSaveInput ToStaffSaveInput(SaveStaffRequest model, DateTime birthDate)
        {
            return new StaffSaveInput
            {
                IsNew = model.IsNew,
                Staff_ID = model.StaffId,
                Staff_Name = model.Name,
                Staff_NRIC = model.NRIC,
                Staff_BirthDate = birthDate,
                Staff_Age = model.Age,
                Staff_Phone = model.Phone,
                Staff_Email = model.Email,
                Staff_Gender = model.Gender,
                Staff_ResState = model.ResState,
                Staff_ResCity = model.ResCity,
                Staff_ResPostcode = model.ResPostcode,
                Staff_AddLine1 = model.AddLine1,
                Staff_AddLine2 = model.AddLine2,
                Staff_Base = model.StaffBase,
                Staff_Type = model.StaffTypeId
            };
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
                    // INSERT: Staff_ID is auto-generated in spStaff_Insert, which returns it.
                    var newStaffId = await _data.CreateStaffAsync(ToStaffSaveInput(model, birthDate));

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

                    await _data.UpdateStaffAsync(ToStaffSaveInput(model, birthDate));

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
                var existingDocs = await _data.GetStaffDocumentsAsync(model.StaffId);

                foreach (var doc in existingDocs)
                {
                    if (doc.StaffDocument_ID > 0 && deleteSet.Contains(doc.StaffDocument_ID)) continue;

                    var typeId = doc.StaffDocumentType_ID ?? "";
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
                // THE TRANSACTION ITSELF LIVES IN SqlData — one named unit of work owning the connection,
                // the SqlTransaction and the procedure sequence (spStaff_Insert or spStaff_Update, then
                // spStaffDocument_GetById + spStaffDocument_Delete per removal, then spStaffDocument_Insert
                // per upload). This controller no longer opens a connection or names a procedure; what it
                // still owns is everything the database cannot roll back.
                //
                // THE ORDERING PROBLEM, and why the blob writes happen in the callback below rather than
                // before the call. The blob key is staff/{Staff_ID}/{guid}{ext}, but for a NEW staff member
                // the Staff_ID does not exist until spStaff_Insert has run — and spStaff_Insert only runs
                // inside that transaction. So the upload cannot be hoisted out of the transaction the way
                // the validation was: the key it needs has not been generated yet. The data layer therefore
                // calls back into this lambda once the staff row is written and the id is known.
                //
                // What that costs is that a blob can be written and the transaction can still fail
                // afterwards, so the guarantee is kept by compensation instead of by ordering: every key
                // lands in uploadedBlobs as it is written, and the catch block below deletes all of them if
                // anything throws. The alternative — pre-generating a Staff_ID in the application — would
                // move id generation out of spStaff_Insert, where it belongs, to buy a rollback that a
                // best-effort delete already provides.
                //
                // IDocumentStorage stays on this side of the boundary throughout: CRC.Data has no reference
                // to CRC.Web and must not gain one, and a Func<> carries no dependency.
                var result = await _data.SaveStaffWithDocumentsAsync(
                    ToStaffSaveInput(model, birthDate),
                    deleteSet.ToList(),
                    async newStaffId =>
                    {
                        // Captured so the catch blocks below can name the staff member in their log line,
                        // exactly as they could when this method held the id in a local.
                        staffId = newStaffId;

                        var documents = new List<StaffDocumentInput>();

                        for (int i = 0; i < files.Count; i++)
                        {
                            var file = files[i];

                            string tId = (i < docTypeIds.Count) ? docTypeIds[i] : string.Empty;
                            string tName = (i < docTypeNames.Count) ? docTypeNames[i] : string.Empty;

                            var safeFileName = DocumentValidation.SafeFileName(file.FileName);
                            var contentType = file.ContentType ?? "application/octet-stream";

                            // Server-generated key: staff/{Staff_ID}/{guid}{ext}. Nothing the user typed becomes
                            // part of it, and the bytes are streamed straight to the container — never to disk.
                            var blobName = DocumentValidation.BuildBlobName("staff", newStaffId, file.FileName);

                            await using (var stream = file.OpenReadStream())
                            {
                                await _documentStorage.UploadAsync(stream, blobName, contentType);
                            }

                            uploadedBlobs.Add(blobName);
                            pendingUploadAudits.Add((blobName, safeFileName, tId, file.Length));

                            documents.Add(new StaffDocumentInput
                            {
                                Staff_Name = model.Name,
                                StaffDocumentType_ID = tId,
                                StaffDocumentType_Name = tName,
                                FileName = safeFileName,
                                BlobName = blobName,
                                ContentType = contentType
                            });
                        }

                        return documents;
                    });

                staffId = result.StaffId;

                // The document rows are gone and the commit has returned, so these keys are now safe to
                // remove from storage and safe to write audit lines about.
                foreach (var removed in result.RemovedDocuments)
                {
                    deleteBlobNames.Add(removed.BlobName);
                    pendingDeleteAudits.Add((removed.StaffDocument_ID, removed.BlobName));
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
        // Helper methods for the mandatory-document rule & file handling
        // --------------------

        private sealed class MandatoryDocInfo
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        // spStaffDocumentSettings_GetByStaffType returns EVERY document type with an IsMandatory flag, not
        // just the mandatory ones, so the filter below is what turns it into "what this staff type must
        // have". IsMandatory is an INT (a CASE over 0/1), and it is compared to 1 rather than treated as a
        // boolean, exactly as the DataTable code did.
        private async Task<List<MandatoryDocInfo>> GetMandatoryDocsByStaffType(string staffTypeId)
        {
            var list = new List<MandatoryDocInfo>();

            if (string.IsNullOrWhiteSpace(staffTypeId))
                return list;

            var settings = await _data.GetStaffDocumentSettingsAsync(staffTypeId);

            foreach (var setting in settings)
            {
                var isMandatory = setting.IsMandatory == 1;
                if (!isMandatory) continue;

                var docTypeId = setting.StaffDocumentType_ID ?? string.Empty;
                if (string.IsNullOrWhiteSpace(docTypeId)) continue;

                var docTypeName = setting.StaffDocumentType_Name ?? docTypeId;

                list.Add(new MandatoryDocInfo
                {
                    Id = docTypeId,
                    Name = docTypeName
                });
            }

            return list;
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
                // spStaff_Delete returns TWO result sets, always:
                //   Result set 1: Status ('Success' | 'Blocked' | 'NotFound'), Message
                //   Result set 2: BlobName rows for the StaffDocument blobs to remove (empty unless Success)
                // Both are carried on StaffDeleteResult — see IDatabaseData.DeleteStaffAsync for what
                // "Blocked" means and what it cascades to when it does not.
                var result = await _data.DeleteStaffAsync(model.StaffId);

                if (!string.Equals(result.Status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new
                    {
                        success = false,
                        message = string.IsNullOrWhiteSpace(result.Message) ? "Failed to delete staff." : result.Message
                    });
                }

                // Remove every StaffDocument blob for this staff member (best effort). The rows are already
                // gone; leaving the blobs would retain personal data after the record it belonged to was
                // deleted, which is the more serious half of an orphan.
                var blobNames = new List<string>();
                foreach (var blobName in result.BlobNames)
                {
                    if (!string.IsNullOrWhiteSpace(blobName))
                    {
                        blobNames.Add(blobName);
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

        // The same rule as GetMandatoryDocsByStaffType, asked the other way round: which mandatory document
        // types does this staff member NOT yet have a row for. Used by SaveStaff, which — unlike
        // SaveStaffWithDocuments — saves first and reports the gap afterwards.
        private async Task<List<string>> GetMissingMandatoryDocuments(string staffTypeId, string staffId)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(staffTypeId) || string.IsNullOrWhiteSpace(staffId))
            {
                return missing;
            }

            var settings = await _data.GetStaffDocumentSettingsAsync(staffTypeId);
            var mandatoryDocs = new List<(string Id, string Name)>();

            foreach (var setting in settings)
            {
                var isMandatory = setting.IsMandatory == 1;
                if (!isMandatory) continue;

                var docTypeId = setting.StaffDocumentType_ID ?? "";
                var docTypeName = setting.StaffDocumentType_Name ?? docTypeId;

                if (!string.IsNullOrWhiteSpace(docTypeId))
                {
                    mandatoryDocs.Add((docTypeId, docTypeName));
                }
            }

            if (mandatoryDocs.Count == 0)
            {
                return missing;
            }

            var documents = await _data.GetStaffDocumentsAsync(staffId);
            var existingDocTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in documents)
            {
                var docTypeId = document.StaffDocumentType_ID ?? "";
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
                var staffTypeItems = await _data.GetStaffTypesAsync();
                var stateItems = await _data.GetStatesAsync();

                var staffTypes = new List<object>();
                foreach (var staffType in staffTypeItems)
                {
                    staffTypes.Add(new
                    {
                        staffTypeId = staffType.Id,
                        staffTypeName = staffType.Name
                    });
                }

                var states = new List<object>();
                foreach (var state in stateItems)
                {
                    states.Add(new
                    {
                        // 🔴 A STRING, not a number, and deliberately so. LU_LOCATION.LocationId is an INT,
                        // and /Branch/GetStates serializes it as a JSON number — but this endpoint has
                        // always gone through DataRow.ToString() and returned "2367". The three staff
                        // endpoints below do the same. Two shapes for one column across two screens is
                        // untidy; changing either one breaks a .js file this plan does not touch.
                        id = state.LocationId.ToString(),
                        name = state.Name
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

            var cityItems = await _data.GetCitiesByStateAsync(stateId);
            var cities = new List<object>();

            foreach (var city in cityItems)
            {
                cities.Add(new
                {
                    id = city.LocationId.ToString(),
                    name = city.Name
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

            var postcodeItems = await _data.GetPostcodesByCityAsync(cityId);
            var postcodes = new List<object>();

            foreach (var postcode in postcodeItems)
            {
                postcodes.Add(new
                {
                    id = postcode.LocationId.ToString(),
                    name = postcode.Name
                });
            }

            return Ok(new { success = true, postcodes });
        }

        // ----- Documents -----

        [HttpGet]
        [Authorize(Policy = "AdminOrSuper")]
        public async Task<IActionResult> GetStaffDocumentTypes()
        {
            var documentTypes = await _data.GetStaffDocumentTypesAsync();
            var list = new List<object>();

            foreach (var documentType in documentTypes)
            {
                list.Add(new
                {
                    documentTypeId = documentType.Id,
                    documentTypeName = documentType.Name
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

            var documents = await _data.GetStaffDocumentsAsync(staffId);

            var list = new List<object>();

            // Metadata only — the blob key is deliberately NOT projected. The container is private, so a
            // storage key is useless to the browser and is exactly the kind of detail that should never leave
            // the server. The file itself is fetched through GetStaffDocumentUrl, which mints a short-lived
            // read SAS for one document at click time. StaffDocumentItem.BlobName is populated here and
            // dropped on purpose; do not add it to this object.
            foreach (var document in documents)
            {
                list.Add(new
                {
                    documentId = document.StaffDocument_ID,
                    staffId = document.Staff_ID,
                    staffName = document.Staff_Name ?? "",
                    staffDocumentTypeId = document.StaffDocumentType_ID ?? "",
                    staffDocumentTypeName = document.StaffDocumentType_Name ?? "",
                    fileName = document.FileName,
                    contentType = document.ContentType,
                    // UploadedOn is rendered with a plain ToString(), i.e. the server's current culture
                    // ("8/6/2026 9:28:03 PM"), and NOT as ISO-8601 — the DataTable code did the same and the
                    // documents table renders the string straight. A null renders "".
                    uploadedOn = document.UploadedOn?.ToString() ?? ""
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

                    // NO TRANSACTION HERE, and that is what this endpoint has always done: one row at a
                    // time, each committing on its own. A failure halfway through a batch leaves the earlier
                    // rows saved. SaveStaffWithDocuments is the atomic path; this one is not, and nothing
                    // calls it.
                    await _data.AddStaffDocumentAsync(staffId, staffName, docTypeId, docTypeName,
                        safeFileName, blobName, contentType);

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
                var document = await _data.GetStaffDocumentByIdAsync(id);

                if (document == null)
                {
                    return Ok(new { success = false, message = "Document not found." });
                }

                var blobName = document.BlobName;
                var fileName = document.FileName ?? string.Empty;
                var staffId = document.Staff_ID ?? string.Empty;

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
                var lookup = await _data.GetStaffDocumentByIdAsync(model.DocumentId);

                var staffId = lookup?.Staff_ID ?? string.Empty;

                // The blob key comes back through the procedure's @DeletedBlobName OUTPUT parameter, which
                // SqlData reads for us. NULL means no row was deleted, so there is nothing in storage to
                // remove.
                var deletedBlobName = await _data.DeleteStaffDocumentAsync(model.DocumentId);

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
            var staffTypes = await _data.GetStaffTypesAsync();
            var list = new List<object>();

            foreach (var staffType in staffTypes)
            {
                list.Add(new
                {
                    staffTypeId = staffType.Id,
                    staffTypeName = staffType.Name
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

            var settings = await _data.GetStaffDocumentSettingsAsync(staffTypeId);
            var list = new List<object>();

            foreach (var setting in settings)
            {
                bool isMandatory = setting.IsMandatory == 1;
                if (!isMandatory)
                    continue;

                list.Add(new
                {
                    staffDocumentTypeId = setting.StaffDocumentType_ID ?? "",
                    staffDocumentTypeName = setting.StaffDocumentType_Name ?? ""
                });
            }

            return Ok(new { success = true, data = list });
        }
    }
}
