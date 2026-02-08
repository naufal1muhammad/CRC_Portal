using CRC.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.Appointment
{
    [Authorize(Policy = "AdminOrSuper")]
    public class AppointmentController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly IWebHostEnvironment _env;

        public AppointmentController(DatabaseHelper db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET: /Appointment
        [HttpGet]
        public IActionResult Index()
        {
            // view will hold filters + empty table
            return View();
        }

        // GET: /Appointment/GetLookups
        [HttpGet]
        public async Task<IActionResult> GetLookups()
        {
            try
            {
                var empty = Array.Empty<SqlParameter>();

                var dtPatients = await _db.ExecuteDataTableAsync(
                    "spPatientAppointment_LookupPatientNames", empty);

                var dtStaff = await _db.ExecuteDataTableAsync(
                    "spPatientAppointment_LookupStaffNames", empty);

                var dtStatuses = await _db.ExecuteDataTableAsync(
                    "spPatientAppointment_LookupStatuses", empty);

                var dtTypes = await _db.ExecuteDataTableAsync(
                    "spLU_PJ_AppType_List", empty);

                var dtBranches = await _db.ExecuteDataTableAsync(
                    "spPatientAppointment_LookupBranches", empty);

                var patients = dtPatients.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["Patient_Name"]?.ToString()
                    })
                    .ToList();

                var staff = dtStaff.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["Staff_Name"]?.ToString()
                    })
                    .ToList();

                var statuses = dtStatuses.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["PatientAppointment_Status"]?.ToString()
                    })
                    .ToList();

                var types = dtTypes.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["PjAppType_Name"]?.ToString()
                    })
                    .ToList();

                var branches = dtBranches.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["Branch_Name"]?.ToString()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    patients,
                    staff,
                    statuses,
                    types,
                    branches
                });
            }
            catch (Exception)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error loading appointment lookups."
                });
            }
        }

        // DTO for search
        public class AppointmentSearchRequest
        {
            public string? PatientName { get; set; }
            public string? StaffName { get; set; }
            public string? Status { get; set; }
            public string? FromDate { get; set; }   // yyyy-MM-dd
            public string? ToDate { get; set; }     // yyyy-MM-dd
            public string? PjAppTypeName { get; set; }
            public string? BranchName { get; set; }
        }

        // POST: /Appointment/Search
        [HttpPost]
        public async Task<IActionResult> Search([FromBody] AppointmentSearchRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            string patientName = model.PatientName?.Trim() ?? string.Empty;
            string staffName = model.StaffName?.Trim() ?? string.Empty;
            string status = model.Status?.Trim() ?? string.Empty;
            string typeName = model.PjAppTypeName?.Trim() ?? string.Empty;
            string branchName = model.BranchName?.Trim() ?? string.Empty;

            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (!string.IsNullOrWhiteSpace(model.FromDate) &&
                DateTime.TryParse(model.FromDate, out var fd))
            {
                fromDate = fd.Date;
            }

            if (!string.IsNullOrWhiteSpace(model.ToDate) &&
                DateTime.TryParse(model.ToDate, out var td))
            {
                toDate = td.Date;
            }

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@PatientName",
                        string.IsNullOrWhiteSpace(patientName) ? (object)DBNull.Value : patientName),
                    new SqlParameter("@StaffName",
                        string.IsNullOrWhiteSpace(staffName) ? (object)DBNull.Value : staffName),
                    new SqlParameter("@Status",
                        string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status),
                    new SqlParameter("@FromDate",
                        fromDate.HasValue ? (object)fromDate.Value : DBNull.Value),
                    new SqlParameter("@ToDate",
                        toDate.HasValue ? (object)toDate.Value : DBNull.Value),
                    new SqlParameter("@PjAppTypeName",
                        string.IsNullOrWhiteSpace(typeName) ? (object)DBNull.Value : typeName),
                    new SqlParameter("@BranchName",
                        string.IsNullOrWhiteSpace(branchName) ? (object)DBNull.Value : branchName)
                };

                // IMPORTANT: the proc name here should match your CREATE PROCEDURE name
                var dt = await _db.ExecuteDataTableAsync("spPatientAppointment_Search", parameters);

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        patientAppointmentId = r["PatientAppointment_ID"] != DBNull.Value
                            ? Convert.ToInt32(r["PatientAppointment_ID"])
                            : 0,
                        patientId = r["Patient_ID"]?.ToString(),
                        patientName = r["Patient_Name"]?.ToString(),
                        patientPhone = r["Patient_Phone"]?.ToString(),
                        patientEmail = r["Patient_Email"]?.ToString(),
                        appointmentType = r["PjAppType_Name"]?.ToString(),
                        status = r["PatientAppointment_Status"]?.ToString(),
                        staffName = r["Staff_Name"]?.ToString(),
                        branchName = r["Branch_Name"]?.ToString(),
                        appointmentDateTime = r["PatientAppointment_Date"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["PatientAppointment_Date"])
                                .ToString("dd/MM/yyyy HH:mm")
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error searching appointments." });
            }
        }

        public class UpdateAppointmentStatusRequest
        {
            public int PatientAppointmentId { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAppointmentStatus([FromBody] UpdateAppointmentStatusRequest model)
        {
            if (model == null || model.PatientAppointmentId <= 0 || string.IsNullOrWhiteSpace(model.Status))
                return BadRequest(new { success = false, message = "Invalid request." });

            var status = model.Status.Trim();

            // safety: only allow known statuses (match your dropdown values)
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Scheduled",
        "Attended",
        "Not Attended"
    };

            if (!allowed.Contains(status))
                return BadRequest(new { success = false, message = "Invalid status value." });

            try
            {
                var parameters = new[]
                {
            new SqlParameter("@PatientAppointment_ID", model.PatientAppointmentId),
            new SqlParameter("@PatientAppointment_Status", status)
        };

                await _db.ExecuteNonQueryAsync("spPatientAppointment_UpdateStatus", parameters);

                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "Error updating appointment status." });
            }
        }

    }
}
