using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using CRC.Web.Data;

namespace CRC.Web.Controllers.Settings
{
    public class SettingsController : Controller
    {
        private readonly DatabaseHelper _db;

        public SettingsController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Settings
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Settings/GetStaffTypes
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

        // GET: /Settings/GetStaffDocumentSettings?staffTypeId=XXX
        [HttpGet]
        public async Task<IActionResult> GetStaffDocumentSettings(string staffTypeId)
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
                list.Add(new
                {
                    staffDocumentTypeId = row["StaffDocumentType_ID"]?.ToString() ?? "",
                    staffDocumentTypeName = row["StaffDocumentType_Name"]?.ToString() ?? "",
                    isMandatory = row["IsMandatory"] != DBNull.Value && Convert.ToInt32(row["IsMandatory"]) == 1
                });
            }

            return Ok(new { success = true, data = list });
        }

        public class SaveStaffDocumentSettingsRequest
        {
            public string StaffTypeId { get; set; } = string.Empty;
            public string StaffTypeName { get; set; } = string.Empty;
            public List<string> DocumentTypeIds { get; set; } = new();
        }

        // POST: /Settings/SaveStaffDocumentSettings
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
                var delParams = new[]
                {
                    new SqlParameter("@StaffType_ID", model.StaffTypeId)
                };
                await _db.ExecuteNonQueryAsync("spStaffDocumentSettings_DeleteByStaffType", delParams);

                // 2) If no docs selected, we are done
                if (model.DocumentTypeIds == null || model.DocumentTypeIds.Count == 0)
                {
                    return Ok(new { success = true, message = "Settings saved (no mandatory documents)." });
                }

                // 3) Load all doc types so we can map ID -> Name
                var dtDocTypes = await _db.ExecuteDataTableAsync("spLU_STAFFDOCUMENTTYPE_List");
                var docTypeNameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (DataRow row in dtDocTypes.Rows)
                {
                    var id = row["StaffDocumentType_ID"]?.ToString() ?? "";
                    var name = row["StaffDocumentType_Name"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(id))
                    {
                        docTypeNameById[id] = name;
                    }
                }

                // 4) Insert settings for each selected doc type
                foreach (var docId in model.DocumentTypeIds.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!docTypeNameById.TryGetValue(docId, out var docName))
                    {
                        continue; // unknown ID; skip
                    }

                    var insParams = new[]
                    {
                        new SqlParameter("@StaffType_ID",           model.StaffTypeId),
                        new SqlParameter("@StaffType_Name",         (object?)model.StaffTypeName ?? DBNull.Value),
                        new SqlParameter("@StaffDocumentType_ID",   docId),
                        new SqlParameter("@StaffDocumentType_Name", docName)
                    };

                    await _db.ExecuteNonQueryAsync("spStaffDocumentSettings_Insert", insParams);
                }

                return Ok(new { success = true, message = "Settings saved successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error saving staff document settings." });
            }
        }
    }
}