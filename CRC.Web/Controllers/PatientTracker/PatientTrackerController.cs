using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.PatientTracker
{
    public class PatientTrackerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
