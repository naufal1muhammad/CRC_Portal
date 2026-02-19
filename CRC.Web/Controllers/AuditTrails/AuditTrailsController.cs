using CRC.Data.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.AuditTrails
{
    [Authorize(Policy = "SuperUserOnly")]
    public class AuditTrailsController : Controller
    {
        private readonly DatabaseHelper _db;

        public AuditTrailsController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /AuditTrails
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /AuditTrails/GetLookups
        [HttpGet]
        public async Task<IActionResult> GetLookups()
        {
            try
            {
                var empty = Array.Empty<SqlParameter>();

                var dtUsers = await _db.ExecuteDataTableAsync("spAuditTrails_LookupUsers", empty);
                var dtActions = await _db.ExecuteDataTableAsync("spAuditTrails_LookupActions", empty);
                var dtCategories = await _db.ExecuteDataTableAsync("spAuditTrails_LookupCategories", empty);

                var users = dtUsers.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = r["User_ID"] == DBNull.Value ? null : r["User_ID"]?.ToString(),
                        name = r["User_Name"]?.ToString()
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id) && !string.IsNullOrWhiteSpace(x.name))
                    .OrderBy(x => x.name)
                    .ToList();

                var actions = dtActions.Rows.Cast<DataRow>()
                    .Select(r => new { name = r["AuditTrail_Action"]?.ToString() })
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .OrderBy(x => x.name)
                    .ToList();

                var categories = dtCategories.Rows.Cast<DataRow>()
                    .Select(r => new { name = r["AuditTrail_Category"]?.ToString() })
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .OrderBy(x => x.name)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    users,
                    actions,
                    categories
                });
            }
            catch (Exception)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error loading audit trail lookups."
                });
            }
        }

        public class AuditTrailSearchRequest
        {
            public int? UserId { get; set; }
            public string? FromDate { get; set; }   // yyyy-MM-dd
            public string? ToDate { get; set; }     // yyyy-MM-dd
            public string? Action { get; set; }
            public string? Category { get; set; }
        }

        // POST: /AuditTrails/Search
        [HttpPost]
        public async Task<IActionResult> Search([FromBody] AuditTrailSearchRequest model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            var action = model.Action?.Trim() ?? string.Empty;
            var category = model.Category?.Trim() ?? string.Empty;

            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (!string.IsNullOrWhiteSpace(model.FromDate) && DateTime.TryParse(model.FromDate, out var fd))
                fromDate = fd.Date;

            if (!string.IsNullOrWhiteSpace(model.ToDate) && DateTime.TryParse(model.ToDate, out var td))
                toDate = td.Date;

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", model.UserId.HasValue ? (object)model.UserId.Value : DBNull.Value),
                    new SqlParameter("@FromDate", fromDate.HasValue ? (object)fromDate.Value : DBNull.Value),
                    new SqlParameter("@ToDate", toDate.HasValue ? (object)toDate.Value : DBNull.Value),
                    new SqlParameter("@Action", string.IsNullOrWhiteSpace(action) ? (object)DBNull.Value : action),
                    new SqlParameter("@Category", string.IsNullOrWhiteSpace(category) ? (object)DBNull.Value : category)
                };

                var dt = await _db.ExecuteDataTableAsync("spAuditTrails_Search", parameters);

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        userId = r["User_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["User_ID"]),
                        name = r["User_Name"]?.ToString() ?? string.Empty,
                        dateTime = r["AuditTrail_EventMYT"] == DBNull.Value
                            ? string.Empty
                            : Convert.ToDateTime(r["AuditTrail_EventMYT"]).ToString("dd/MM/yyyy HH:mm"),
                        action = r["AuditTrail_Action"]?.ToString() ?? string.Empty,
                        category = r["AuditTrail_Category"]?.ToString() ?? string.Empty,
                        summary = r["AuditTrail_Summary"]?.ToString() ?? string.Empty
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error searching audit trails." });
            }
        }
    }
}