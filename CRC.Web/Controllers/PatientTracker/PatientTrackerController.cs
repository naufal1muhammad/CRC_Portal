using CRC.Data.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.PatientTracker
{
    [Authorize(Policy = "AdminOrSuper")]
    public class PatientTrackerController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly ILogger<PatientTrackerController> _logger;

        public PatientTrackerController(DatabaseHelper db, ILogger<PatientTrackerController> logger)
        {
            _db = db;
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientTracker_AppointmentTypes_List",
                    Array.Empty<SqlParameter>()
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        pjAppTypeId = r["PjAppType_ID"]?.ToString() ?? "",
                        pjAppTypeName = r["PjAppType_Name"]?.ToString() ?? ""
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

        // GET: /PatientTracker/GetTrackerData
        // Returns a single payload with everything the page needs:
        //   - appointment types (column headers for APPOINTMENT journey line)
        //   - patient list (with basic info + IsStalled flag)
        //   - latest appointment per (patient, type) for status overlay
        //   - completed procedures per patient
        //   - stalled count for the top "Stalled" card
        [HttpGet]
        public async Task<IActionResult> GetTrackerData()
        {
            try
            {
                var dtTypes = await _db.ExecuteDataTableAsync(
                    "spPatientTracker_AppointmentTypes_List",
                    Array.Empty<SqlParameter>()
                );

                var dtPatients = await _db.ExecuteDataTableAsync(
                    "spPatientTracker_Patients_List",
                    Array.Empty<SqlParameter>()
                );

                var dtAppts = await _db.ExecuteDataTableAsync(
                    "spPatientTracker_Appointments_List",
                    Array.Empty<SqlParameter>()
                );

                var dtProcs = await _db.ExecuteDataTableAsync(
                    "spPatientTracker_Procedures_List",
                    Array.Empty<SqlParameter>()
                );

                var dtStalled = await _db.ExecuteDataTableAsync(
                    "spPatientTracker_StalledCount_Get",
                    Array.Empty<SqlParameter>()
                );

                var appointmentTypes = dtTypes.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        pjAppTypeId = r["PjAppType_ID"]?.ToString() ?? "",
                        pjAppTypeName = r["PjAppType_Name"]?.ToString() ?? ""
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.pjAppTypeId))
                    .ToList();

                var patients = dtPatients.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        patientId = r["Patient_ID"]?.ToString() ?? "",
                        name = r["Patient_Name"]?.ToString() ?? "",
                        nric = r["Patient_NRIC"]?.ToString() ?? "",
                        phone = r["Patient_Phone"]?.ToString() ?? "",
                        age = r["Patient_Age"] == DBNull.Value ? 0 : Convert.ToInt32(r["Patient_Age"]),
                        gender = r["Patient_Gender"]?.ToString() ?? "",
                        dischargeDate = r["Patient_DischargeDate"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["Patient_DischargeDate"]).ToString("dd/MM/yyyy"),
                        isStalled = r["IsStalled"] != DBNull.Value && Convert.ToBoolean(r["IsStalled"])
                    })
                    .ToList();

                var appointments = dtAppts.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        patientId = r["Patient_ID"]?.ToString() ?? "",
                        pjAppTypeId = r["PjAppType_ID"]?.ToString() ?? "",
                        status = r["PatientAppointment_Status"]?.ToString() ?? "",
                        date = r["PatientAppointment_Date"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["PatientAppointment_Date"]).ToString("dd/MM/yyyy")
                    })
                    .ToList();

                var procedures = dtProcs.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        patientId = r["Patient_ID"]?.ToString() ?? "",
                        pjAppTypeName = r["PjAppType_Name"]?.ToString() ?? "",
                        date = r["PatientJourney_Date"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["PatientJourney_Date"]).ToString("dd/MM/yyyy")
                    })
                    .ToList();

                int stalledCount = 0;
                if (dtStalled.Rows.Count > 0 && dtStalled.Rows[0]["StalledCount"] != DBNull.Value)
                {
                    stalledCount = Convert.ToInt32(dtStalled.Rows[0]["StalledCount"]);
                }

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
