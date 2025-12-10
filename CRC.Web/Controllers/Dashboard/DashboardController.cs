using CRC.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.Dashboard
{
    public class DashboardController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly IWebHostEnvironment _env;

        public DashboardController(DatabaseHelper db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET: /Dashboard
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ---------------------------
        //  Card 1: Active branches
        // ---------------------------
        [HttpGet]
        public async Task<IActionResult> GetActiveBranchCount()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spDashboard_Branch_CountActive",
                    Array.Empty<SqlParameter>());

                int count = 0;
                if (dt.Rows.Count > 0 && dt.Columns.Contains("ActiveBranchCount"))
                {
                    count = Convert.ToInt32(dt.Rows[0]["ActiveBranchCount"]);
                }

                return Ok(new { success = true, count });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading active branch count." });
            }
        }

        // ---------------------------------------------------
        //  Branch list for card 2 & 3 dropdowns
        //  (reuse spBranch_ListActive)
        // ---------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetDashboardBranches()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spBranch_ListActive",
                    Array.Empty<SqlParameter>());

                var branches = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        branchId = r["Branch_ID"]?.ToString(),
                        branchName = r["Branch_Name"]?.ToString()
                    })
                    .Where(b =>
                        !string.IsNullOrWhiteSpace(b.branchId) &&
                        !string.IsNullOrWhiteSpace(b.branchName))
                    .Distinct()
                    .OrderBy(b => b.branchName)
                    .ToList();

                // IMPORTANT: return as "data" because JS expects result.data
                return Ok(new { success = true, data = branches });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading branches." });
            }
        }

        // ---------------------------------------------------
        //  Card 2: Active patients by branch / all branches
        // ---------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetActivePatientsCount(string? branchName)
        {
            try
            {
                string? b = branchName?.Trim();
                var param = new SqlParameter("@Branch_Name", SqlDbType.VarChar, 100)
                {
                    Value = string.IsNullOrWhiteSpace(b) ? (object)DBNull.Value : b
                };

                var dt = await _db.ExecuteDataTableAsync(
                    "spDashboard_Patient_CountActiveByBranch",
                    new[] { param });

                int count = 0;
                if (dt.Rows.Count > 0 && dt.Columns.Contains("ActivePatientCount"))
                {
                    count = Convert.ToInt32(dt.Rows[0]["ActivePatientCount"]);
                }

                return Ok(new { success = true, count });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading active patients count." });
            }
        }

        // ---------------------------------------------------
        //  Card 3: Discharged patients by branch / all
        // ---------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetDischargedPatientsCount(string? branchName)
        {
            try
            {
                string? b = branchName?.Trim();
                var param = new SqlParameter("@Branch_Name", SqlDbType.VarChar, 100)
                {
                    Value = string.IsNullOrWhiteSpace(b) ? (object)DBNull.Value : b
                };

                var dt = await _db.ExecuteDataTableAsync(
                    "spDashboard_Patient_CountDischargedByBranch",
                    new[] { param });

                int count = 0;
                if (dt.Rows.Count > 0 && dt.Columns.Contains("DischargedPatientCount"))
                {
                    count = Convert.ToInt32(dt.Rows[0]["DischargedPatientCount"]);
                }

                return Ok(new { success = true, count });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading discharged patients count." });
            }
        }

        // -----------------------------------
        //  Row 2 – Pie: patients by race
        // -----------------------------------
        [HttpGet]
        public async Task<IActionResult> GetPatientsByRace()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spDashboard_Patient_ByRace",
                    Array.Empty<SqlParameter>());

                var items = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        label = r["Race_Name"]?.ToString() ?? "Unknown",
                        count = Convert.ToInt32(r["PatientCount"])
                    })
                    .ToList();

                return Ok(new { success = true, data = items });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patients by race." });
            }
        }

        // -----------------------------------
        //  Row 2 – Pie: patients by age group
        // -----------------------------------
        [HttpGet]
        public async Task<IActionResult> GetPatientsByAgeGroup()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spDashboard_Patient_ByAgeGroup",
                    Array.Empty<SqlParameter>());

                var items = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        label = r["AgeGroup"]?.ToString() ?? "Unknown",
                        count = Convert.ToInt32(r["PatientCount"])
                    })
                    .ToList();

                return Ok(new { success = true, data = items });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patients by age group." });
            }
        }

        // -----------------------------------------
        //  Row 3 – Bar: patients by discharge type
        // -----------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetPatientsByDischargeType()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spDashboard_Patient_ByDischargeType",
                    Array.Empty<SqlParameter>());

                var items = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        label = r["DischargeType_Name"]?.ToString() ?? "Unknown",
                        count = Convert.ToInt32(r["PatientCount"])
                    })
                    .ToList();

                return Ok(new { success = true, data = items });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patients by discharge type." });
            }
        }

        // ---------------------------------------------------------
        //  Row 4 – Horizontal double bar: admit vs discharge
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAdmitDischargeLast12Months()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spDashboard_Patient_AdmitDischargeLast12Months",
                    Array.Empty<SqlParameter>());

                var items = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        year = Convert.ToInt32(r["Year"]),
                        month = Convert.ToInt32(r["Month"]),
                        label = r["MonthLabel"]?.ToString() ?? "",
                        admittedCount = Convert.ToInt32(r["AdmittedCount"]),
                        dischargedCount = Convert.ToInt32(r["DischargedCount"])
                    })
                    .ToList();

                return Ok(new { success = true, data = items });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading admit/discharge trend." });
            }
        }
    }
}