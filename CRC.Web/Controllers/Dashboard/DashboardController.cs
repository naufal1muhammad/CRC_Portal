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

        [HttpGet]
        public async Task<IActionResult> GetPatientStates()
        {
            var dt = await _db.ExecuteDataTableAsync("spDashboard_PatientStates");
            var list = new List<string>();

            foreach (DataRow row in dt.Rows)
            {
                var state = row["Branch_State"]?.ToString();
                if (!string.IsNullOrWhiteSpace(state))
                {
                    list.Add(state);
                }
            }

            return Ok(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetPatientStageCountsByState(string state)
        {
            var parameters = new[]
            {
        new SqlParameter("@Branch_State", (object?)state ?? DBNull.Value)
    };

            var dt = await _db.ExecuteDataTableAsync("spDashboard_PatientStageCountsByState", parameters);

            // Build a dictionary for T2–T5, default 0
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["T2"] = 0,
                ["T3"] = 0,
                ["T4"] = 0,
                ["T5"] = 0
            };

            foreach (DataRow row in dt.Rows)
            {
                var stage = row["Patient_Stage"]?.ToString() ?? "";
                var value = row["PatientCount"] == DBNull.Value ? 0 : Convert.ToInt32(row["PatientCount"]);

                if (counts.ContainsKey(stage))
                {
                    counts[stage] = value;
                }
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    t2 = counts["T2"],
                    t3 = counts["T3"],
                    t4 = counts["T4"],
                    t5 = counts["T5"]
                }
            });
        }
    }
}