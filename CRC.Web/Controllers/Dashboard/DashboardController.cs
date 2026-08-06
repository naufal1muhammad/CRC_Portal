using CRC.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.Dashboard
{
    [Authorize(Policy = "SuperUserOnly")]
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

    }
}
