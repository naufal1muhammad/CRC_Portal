using CRC.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.PatientTracker
{
    [Authorize(Policy = "AdminOrSuper")]
    public class PatientTrackerController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<PatientTrackerController> _logger;

        public PatientTrackerController(IDatabaseData data, ILogger<PatientTrackerController> logger)
        {
            _data = data;
            _logger = logger;
        }

        // GET: /PatientTracker
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /PatientTracker/GetAppointmentTypes
        [HttpGet]
        public async Task<IActionResult> GetAppointmentTypes()
        {
            try
            {
                var types = await _data.GetTrackerAppointmentTypesAsync();

                var list = types
                    .Select(t => new
                    {
                        pjAppTypeId = t.Id,
                        pjAppTypeName = t.Name
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.pjAppTypeId))
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading PatientTracker appointment types.");
                return Ok(new { success = false, message = "Error loading appointment types." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTrackerData()
        {
            try
            {
                // Five sequential reads, in the order the page's payload is assembled. Nothing here is a
                // unit of work: they are independent parameterless selects, and the tracker tolerates the
                // fact that a row written between the first and the last would be seen by only some of them.
                var typeRows = await _data.GetTrackerAppointmentTypesAsync();
                var patientRows = await _data.GetTrackerPatientsAsync();
                var appointmentRows = await _data.GetTrackerAppointmentsAsync();
                var procedureRows = await _data.GetTrackerProceduresAsync();
                var stalledCount = await _data.GetTrackerStalledCountAsync();

                var appointmentTypes = typeRows
                    .Select(t => new
                    {
                        pjAppTypeId = t.Id,
                        pjAppTypeName = t.Name
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.pjAppTypeId))
                    .ToList();

                var patients = patientRows
                    .Select(r => new
                    {
                        patientId = r.Patient_ID,
                        name = r.Patient_Name,
                        nric = r.Patient_NRIC,
                        phone = r.Patient_Phone,
                        age = r.Patient_Age ?? 0,
                        gender = r.Patient_Gender,
                        dischargeDate = r.Patient_DischargeDate?.ToString("dd/MM/yyyy") ?? "",
                        // IsStalled is a BIT the procedure computes; see PatientTrackerPatientItem for what
                        // "stalled" means and for the fact that the badge below computes it a second time.
                        isStalled = r.IsStalled ?? false
                    })
                    .ToList();

                var appointments = appointmentRows
                    .Select(r => new
                    {
                        patientId = r.Patient_ID,
                        pjAppTypeId = r.PjAppType_ID,
                        status = r.PatientAppointment_Status,
                        date = r.PatientAppointment_Date?.ToString("dd/MM/yyyy") ?? ""
                    })
                    .ToList();

                var procedures = procedureRows
                    .Select(r => new
                    {
                        patientId = r.Patient_ID,
                        pjAppTypeName = r.PjAppType_Name,
                        date = r.PatientJourney_Date?.ToString("dd/MM/yyyy") ?? ""
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    appointmentTypes,
                    patients,
                    appointments,
                    procedures,
                    stalledCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading PatientTracker data.");
                return Ok(new { success = false, message = "Error loading tracker data." });
            }
        }
    }
}
