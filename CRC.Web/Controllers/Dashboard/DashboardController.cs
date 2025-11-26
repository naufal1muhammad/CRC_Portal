using Microsoft.Data.SqlClient;
using CRC.Web.Data;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.Dashboard
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly DatabaseHelper _db;

        public DashboardController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Dashboard
        public IActionResult Index()
        {
            // View is static; JS will call GetSummary to load data
            return View();
        }

        // GET: /Dashboard/GetSummary
        [HttpGet]
        public async Task<IActionResult> GetSummary()
        {
            var dt = await _db.ExecuteDataTableAsync("spDashboard_GetSummary");

            if (dt.Rows.Count == 0)
            {
                return Ok(new
                {
                    activeBranchesCount = 0,
                    t2 = 0,
                    t3 = 0,
                    t4 = 0,
                    t5 = 0
                });
            }

            var row = dt.Rows[0];

            int activeBranches = Convert.ToInt32(row["ActiveBranchesCount"]);
            int t2 = Convert.ToInt32(row["T2Count"]);
            int t3 = Convert.ToInt32(row["T3Count"]);
            int t4 = Convert.ToInt32(row["T4Count"]);
            int t5 = Convert.ToInt32(row["T5Count"]);

            return Ok(new
            {
                activeBranchesCount = activeBranches,
                t2,
                t3,
                t4,
                t5
            });
        }
    }
}