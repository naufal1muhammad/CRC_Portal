using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRC.Data.Data;
using CRC.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.MyProfileStaff
{
    // Read-only "My Profile" page for the currently logged-in STAFF user.
    // Admin/Super are also allowed in so they can reach the page if they happen to land on it,
    // but the basic-details endpoint they already use lives in StaffController.
    [Authorize(Policy = "AdminOrSuperOrStaff")]
    public class MyProfileStaffController : Controller
    {
        private readonly IDatabaseData _data;

        public MyProfileStaffController(IDatabaseData data)
        {
            _data = data;
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
                // Three whole levels of the LU_LOCATION tree are fetched to resolve three names, because
                // there is no spLU_LOCATION_GetById. That is what this page did before the Dapper layer
                // and it is unchanged here — a lookup that misses simply leaves its name empty.
                if (stateId.HasValue && stateId.Value > 0)
                {
                    var states = await _data.GetStatesAsync();
                    stateName = FindNameById(states, stateId.Value);
                }

                if (cityId.HasValue && cityId.Value > 0 && stateId.HasValue && stateId.Value > 0)
                {
                    var cities = await _data.GetCitiesByStateAsync(stateId.Value);
                    cityName = FindNameById(cities, cityId.Value);
                }

                if (postcodeId.HasValue && postcodeId.Value > 0 && cityId.HasValue && cityId.Value > 0)
                {
                    var postcodes = await _data.GetPostcodesByCityAsync(cityId.Value);
                    postcodeName = FindNameById(postcodes, postcodeId.Value);
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

        private static string FindNameById(List<LocationLookupItem> locations, int id)
        {
            return locations.FirstOrDefault(location => location.LocationId == id)?.Name ?? string.Empty;
        }
    }
}
