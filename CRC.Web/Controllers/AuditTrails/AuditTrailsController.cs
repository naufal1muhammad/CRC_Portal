using CRC.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.AuditTrails
{
    [Authorize(Policy = "SuperUserOnly")]
    public class AuditTrailsController : Controller
    {
        private readonly IDatabaseData _data;

        public AuditTrailsController(IDatabaseData data)
        {
            _data = data;
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
                // The three filter dropdowns are built from dbo.AuditTrails itself, not from any catalogue,
                // so they can only offer values that have actually been recorded. The users lookup INNER
                // JOINs dbo.Users, which means an actor of 0 — the silent failure of CoreFlow.md §0.1 — or
                // a since-deleted user is absent here while its rows still come back from Search below.
                var userRows = await _data.GetAuditTrailUsersAsync();
                var actionRows = await _data.GetAuditTrailActionsAsync();
                var categoryRows = await _data.GetAuditTrailCategoriesAsync();

                var users = userRows
                    .Select(u => new
                    {
                        id = u.Id,
                        name = u.Name
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id) && !string.IsNullOrWhiteSpace(x.name))
                    .OrderBy(x => x.name)
                    .ToList();

                var actions = actionRows
                    .Select(a => new { name = a })
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .OrderBy(x => x.name)
                    .ToList();

                var categories = categoryRows
                    .Select(c => new { name = c })
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
                // 🔴 model.UserId is A FILTER the SUPERUSER picked, not an actor. The procedure's parameter
                // is spelled @UserId (no underscore) precisely because it is not the @User_ID of §0.1, and
                // it is passed straight through: resolving it from the current user's claim would narrow
                // every search to the searcher's own trail. An unparseable date is dropped to null — "no
                // filter" — rather than rejected, which is the behaviour the page has always had.
                var rows = await _data.SearchAuditTrailsAsync(
                    model.UserId,
                    fromDate,
                    toDate,
                    string.IsNullOrWhiteSpace(action) ? null : action,
                    string.IsNullOrWhiteSpace(category) ? null : category);

                var list = rows
                    .Select(r => new
                    {
                        userId = r.User_ID ?? 0,
                        name = r.User_Name ?? string.Empty,
                        dateTime = r.AuditTrail_EventMYT?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty,
                        action = r.AuditTrail_Action ?? string.Empty,
                        category = r.AuditTrail_Category ?? string.Empty,
                        summary = r.AuditTrail_Summary ?? string.Empty
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
