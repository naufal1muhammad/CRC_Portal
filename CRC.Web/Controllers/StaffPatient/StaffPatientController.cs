using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.StaffPatient
{
    [Authorize(Policy = "StaffOnly")]
    public class StaffPatientController : Controller
    {
        [HttpGet]
        public IActionResult Details(string id)
        {
            ViewData["PatientId"] = id;
            return View();
        }
    }
}