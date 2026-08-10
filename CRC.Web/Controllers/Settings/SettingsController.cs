using CRC.Data.Data;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.Settings
{
    // The document RULES, not the documents. This one SUPERUSER screen owns both mandatory-document
    // tables — dbo.StaffDocumentSettings (which document types a staff type must have on file) and
    // dbo.PatientDocumentSettings (which a patient must have before being discharged under a given
    // reason). Neither table has a "mandatory" flag: the row's existence IS the rule. See CoreFlow.md §8.
    //
    // 🔴 THE TWO HALVES SAVE DIFFERENTLY AND ONLY ONE OF THEM IS ATOMIC. The patient half hands the whole
    // replace to spPatientDocumentSettings_SaveForDischargeType, which deletes and re-inserts in one batch.
    // The staff half has no such procedure, so SaveStaffDocumentSettings below runs a DELETE and then N
    // INSERTs, with no transaction — a failure part-way leaves that staff type with a partial set of
    // mandatory documents and reports an error. That is what this controller has always done and the
    // Dapper migration left the sequencing exactly where it was, in the open, rather than quietly wrapping
    // it. Changing it is a behaviour change and belongs to whoever decides to make one. CoreFlow.md §5.9.
    [Authorize(Policy = "SuperUserOnly")]
    public class SettingsController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(IDatabaseData data, ILogger<SettingsController> logger)
        {
            _data = data;
            _logger = logger;
        }

        // GET: /Settings
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        //------------------------------------------------------
        //STAFF DOCUMENTS
        //------------------------------------------------------

        // GET: /Settings/GetStaffTypes
        [HttpGet]
        public async Task<IActionResult> GetStaffTypes()
        {
            // A bare JSON array, not the { success, data } envelope — the one endpoint on this controller
            // shaped that way, and js/settings/index.js reads it as an array.
            var staffTypes = await _data.GetStaffTypesAsync();

            var list = staffTypes
                .Select(x => new
                {
                    staffTypeId = x.Id,
                    staffTypeName = x.Name
                })
                .ToList();

            return Ok(list);
        }

        // GET: /Settings/GetStaffDocumentSettings?staffTypeId=XXX
        [HttpGet]
        public async Task<IActionResult> GetStaffDocumentSettings(string staffTypeId)
        {
            if (string.IsNullOrWhiteSpace(staffTypeId))
            {
                return Ok(new { success = false, message = "Staff type is required." });
            }

            // 🔴 THIS RETURNS EVERY STAFF DOCUMENT TYPE, NOT ONLY THE MANDATORY ONES — the procedure drives
            // LU_STAFFDOCUMENTTYPE and LEFT JOINs the settings table, so the page gets a full checklist with
            // isMandatory true on the configured ones. The discharge half below does the opposite and
            // returns only the configured rows.
            var settings = await _data.GetStaffDocumentSettingsAsync(staffTypeId);

            var list = settings
                .Select(x => new
                {
                    staffDocumentTypeId = x.StaffDocumentType_ID ?? "",
                    staffDocumentTypeName = x.StaffDocumentType_Name ?? "",
                    // IsMandatory is an INT (`CASE WHEN … THEN 0 ELSE 1 END`), not a BIT, and null-safe here
                    // for the same reason the DataTable code tested for DBNull first.
                    isMandatory = x.IsMandatory == 1
                })
                .ToList();

            return Ok(new { success = true, data = list });
        }

        public class SaveStaffDocumentSettingsRequest
        {
            public string StaffTypeId { get; set; } = string.Empty;
            public string StaffTypeName { get; set; } = string.Empty;
            public List<string> DocumentTypeIds { get; set; } = new();
        }

        // POST: /Settings/SaveStaffDocumentSettings
        //
        // 🔴 DELETE-THEN-INSERT, N+1 ROUND TRIPS, NO TRANSACTION. The sequence below is the whole of the
        // staff-side save: there is no spStaffDocumentSettings_Save procedure to hand it to. Read the class
        // comment before "improving" this.
        [HttpPost]
        public async Task<IActionResult> SaveStaffDocumentSettings([FromBody] SaveStaffDocumentSettingsRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.StaffTypeId))
            {
                return Ok(new { success = false, message = "Staff type is required." });
            }

            try
            {
                // 1) Clear existing settings for this staff type
                await _data.DeleteStaffDocumentSettingsAsync(model.StaffTypeId);

                // 2) If no docs selected, we are done
                if (model.DocumentTypeIds == null || model.DocumentTypeIds.Count == 0)
                {
                    return Ok(new { success = true, message = "Settings saved (no mandatory documents)." });
                }

                // 3) Load all doc types so we can map ID -> Name
                //
                // spLU_STAFFDOCUMENTTYPE_List, not spStaffDocument_LookupDocuments: the settings screen
                // configures a rule for the FUTURE, so it must offer only types that still exist in the
                // lookup — not the ones merely present on an old document.
                var docTypes = await _data.GetStaffDocumentTypesAsync();
                var docTypeNameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var docType in docTypes)
                {
                    var id = docType.Id ?? "";
                    var name = docType.Name ?? "";
                    if (!string.IsNullOrEmpty(id))
                    {
                        docTypeNameById[id] = name;
                    }
                }

                // 4) Insert settings for each selected doc type
                //
                // Distinct() is load-bearing, not tidiness: spStaffDocumentSettings_Insert is a bare INSERT
                // and dbo.StaffDocumentSettings has a composite primary key, so a duplicated id in the
                // posted list would throw part-way through the loop — after the delete has already run.
                foreach (var docId in model.DocumentTypeIds.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!docTypeNameById.TryGetValue(docId, out var docName))
                    {
                        continue; // unknown ID; skip
                    }

                    await _data.AddStaffDocumentSettingAsync(
                        model.StaffTypeId, model.StaffTypeName, docId, docName);
                }

                return Ok(new { success = true, message = "Settings saved successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error saving staff document settings." });
            }
        }

        //------------------------------------------------------
        //PATIENT DOCUMENTS
        //------------------------------------------------------

        public class SaveDischargeDocumentSettingsRequest
        {
            public string DischargeTypeId { get; set; } = string.Empty;
            public List<string> DocumentTypeIds { get; set; } = new();
        }

        [HttpGet]
        public async Task<IActionResult> GetDischargeTypes()
        {
            try
            {
                var dischargeTypes = await _data.GetDischargeTypesAsync();

                var list = dischargeTypes
                    .Select(x => new
                    {
                        dischargeTypeId = x.Id,
                        dischargeTypeName = x.Name
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading discharge types." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDischargeDocumentSettings(string dischargeTypeId)
        {
            if (string.IsNullOrWhiteSpace(dischargeTypeId))
            {
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            try
            {
                // 🔴 ONLY THE MANDATORY TYPES COME BACK — the mirror image of the staff read above, which
                // returns the full checklist. js/settings/index.js ticks these against the separate
                // LU_PATDOCUMENTTYPE list it already holds.
                var settings = await _data.GetDischargeDocumentSettingsAsync(dischargeTypeId);

                var list = settings
                    .Select(x => new
                    {
                        dischargeTypeId = x.DischargeType_ID,
                        dischargeTypeName = x.DischargeType_Name,
                        documentTypeId = x.PatientDocumentType_ID,
                        documentTypeName = x.PatientDocumentType_Name
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading discharge document settings." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveDischargeDocumentSettings([FromBody] SaveDischargeDocumentSettingsRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.DischargeTypeId))
            {
                return BadRequest(new { success = false, message = "Discharge type is required." });
            }

            // The procedure takes the ids as ONE comma-separated string and splits them with STRING_SPLIT,
            // so the list is joined here. A blank CSV is not a no-op: it CLEARS the discharge reason's
            // settings, which is how the screen saves an empty checklist.
            var idsCsv = (model.DocumentTypeIds != null && model.DocumentTypeIds.Count > 0)
                ? string.Join(",", model.DocumentTypeIds.Where(x => !string.IsNullOrWhiteSpace(x)))
                : string.Empty;

            try
            {
                // One call does the whole replace, atomically, inside the procedure — unlike the staff save
                // above.
                await _data.SaveDischargeDocumentSettingsAsync(
                    model.DischargeTypeId,
                    string.IsNullOrWhiteSpace(idsCsv) ? null : idsCsv);

                return Ok(new { success = true, message = "Settings saved successfully." });
            }
            catch (SqlException ex)
            {
                // The procedure RAISERRORs severity 11 on an unrecognised @DischargeType_ID — the only
                // server-side validation in this area, and the reason this catch is kept separate.
                _logger.LogError(ex, "SqlException saving discharge document settings DischargeTypeId={DischargeTypeId}", model.DischargeTypeId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving discharge document settings."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving discharge document settings DischargeTypeId={DischargeTypeId}", model.DischargeTypeId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving discharge document settings."));
            }
        }
    }
}
