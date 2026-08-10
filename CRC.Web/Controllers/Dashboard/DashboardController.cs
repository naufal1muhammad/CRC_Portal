using CRC.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.Dashboard
{
    [Authorize(Policy = "SuperUserOnly")]
    public class DashboardController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly IWebHostEnvironment _env;

        public DashboardController(IDatabaseData data, IWebHostEnvironment env)
        {
            _data = data;
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
                var count = await _data.GetActiveBranchCountAsync();

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
                var rows = await _data.GetPatientsByRaceAsync();

                var items = rows
                    .Select(r => new
                    {
                        label = r.Race_Name ?? "Unknown",
                        count = r.PatientCount
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
                var rows = await _data.GetPatientsByAgeGroupAsync();

                var items = rows
                    .Select(r => new
                    {
                        label = r.AgeGroup ?? "Unknown",
                        count = r.PatientCount
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
                var rows = await _data.GetPatientsByDischargeTypeAsync();

                var items = rows
                    .Select(r => new
                    {
                        label = r.DischargeType_Name ?? "Unknown",
                        count = r.PatientCount
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
