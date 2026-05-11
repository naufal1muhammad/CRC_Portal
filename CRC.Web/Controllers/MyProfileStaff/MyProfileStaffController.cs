using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CRC.Data.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.MyProfileStaff
{
    // Read-only "My Profile" page for the currently logged-in STAFF user.
    // Admin/Super are also allowed in so they can reach the page if they happen to land on it,
    // but the basic-details endpoint they already use lives in StaffController.
    [Authorize(Policy = "AdminOrSuperOrStaff")]
    public class MyProfileStaffController : Controller
    {
        private readonly DatabaseHelper _db;

        public MyProfileStaffController(DatabaseHelper db)
        {
            _db = db;
        }

        private string GetOwnStaffId()
        {
            return User.FindFirst("StaffId")?.Value?.Trim() ?? string.Empty;
        }

        // GET: /MyProfileStaff
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["StaffId"] = GetOwnStaffId();
            return View();
        }

        // GET: /MyProfileStaff/GetLocationNames?stateId=&cityId=&postcodeId=
        // Returns the human-readable names for the given location IDs so the read-only
        // profile page can display them without exposing the broader admin lookups to staff.
        [HttpGet]
        public async Task<IActionResult> GetLocationNames(int? stateId, int? cityId, int? postcodeId)
        {
            string stateName = string.Empty;
            string cityName = string.Empty;
            string postcodeName = string.Empty;

            try
            {
                if (stateId.HasValue && stateId.Value > 0)
                {
                    var dt = await _db.ExecuteDataTableAsync("spLU_LOCATION_ListStates");
                    stateName = FindNameById(dt, stateId.Value);
                }

                if (cityId.HasValue && cityId.Value > 0 && stateId.HasValue && stateId.Value > 0)
                {
                    var dt = await _db.ExecuteDataTableAsync(
                        "spLU_LOCATION_ListCityByState",
                        new[] { new SqlParameter("@StateId", stateId.Value) });
                    cityName = FindNameById(dt, cityId.Value);
                }

                if (postcodeId.HasValue && postcodeId.Value > 0 && cityId.HasValue && cityId.Value > 0)
                {
                    var dt = await _db.ExecuteDataTableAsync(
                        "spLU_LOCATION_ListPostcodesByCity",
                        new[] { new SqlParameter("@CityId", cityId.Value) });
                    postcodeName = FindNameById(dt, postcodeId.Value);
                }

                return Ok(new
                {
                    success = true,
                    stateName,
                    cityName,
                    postcodeName
                });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading location names." });
            }
        }

        private static string FindNameById(DataTable dt, int id)
        {
            foreach (DataRow row in dt.Rows)
            {
                if (row["LocationId"] == DBNull.Value) continue;
                if (Convert.ToInt32(row["LocationId"]) == id)
                {
                    return row["Name"]?.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
