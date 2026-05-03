using CRC.Data.Database;
using CRC.Web.Infrastructure;
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
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(DatabaseHelper db, IWebHostEnvironment env, ILogger<AppointmentController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
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
                        id = r["PjAppType_ID"]?.ToString(),
                        name = r["PjAppType_Name"]?.ToString()
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .OrderBy(x => x.name)
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
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                using var cmd = await _db.CreateStoredProcedureCommandAsync(
                    conn,
                    null,
                    "spPatientAppointment_UpdateStatus",
                    new[]
                    {
                        new SqlParameter("@PatientAppointment_ID", model.PatientAppointmentId),
                        new SqlParameter("@PatientAppointment_Status", status)
                    });

                var outPatientId = new SqlParameter("@Out_Patient_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                var outStaffId = new SqlParameter("@Out_Staff_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                var outDate = new SqlParameter("@Out_Date", SqlDbType.Date) { Direction = ParameterDirection.Output };
                var outStartTime = new SqlParameter("@Out_StartTime", SqlDbType.Time) { Direction = ParameterDirection.Output };
                var outEndTime = new SqlParameter("@Out_EndTime", SqlDbType.Time) { Direction = ParameterDirection.Output };
                var outTypeId = new SqlParameter("@Out_PjAppType_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                var outBranchId = new SqlParameter("@Out_Branch_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                var outStatus = new SqlParameter("@Out_Status", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(outPatientId);
                cmd.Parameters.Add(outStaffId);
                cmd.Parameters.Add(outDate);
                cmd.Parameters.Add(outStartTime);
                cmd.Parameters.Add(outEndTime);
                cmd.Parameters.Add(outTypeId);
                cmd.Parameters.Add(outBranchId);
                cmd.Parameters.Add(outStatus);

                await cmd.ExecuteNonQueryAsync();

                string persistedPatientId = outPatientId.Value is string pid ? pid : string.Empty;
                string persistedStaffId = outStaffId.Value is string sid ? sid : string.Empty;
                DateTime persistedDate = outDate.Value is DateTime d ? d : default;
                TimeSpan persistedStart = outStartTime.Value is TimeSpan st ? st : default;
                TimeSpan persistedEnd = outEndTime.Value is TimeSpan et ? et : default;
                string persistedTypeId = outTypeId.Value is string tid ? tid : string.Empty;
                string persistedBranchId = outBranchId.Value is string bid ? bid : string.Empty;
                string persistedStatus = outStatus.Value is string ps ? ps : status;

                AuditLog.AppointmentUpdated(HttpContext, model.PatientAppointmentId, persistedPatientId, persistedStaffId,
                    persistedDate, persistedStart, persistedEnd, persistedTypeId, persistedBranchId, persistedStatus);

                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException updating appointment status PatientAppointmentId={Id} Status={Status}", model.PatientAppointmentId, status);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error updating appointment status."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating appointment status PatientAppointmentId={Id} Status={Status}", model.PatientAppointmentId, status);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error updating appointment status."));
            }
        }

    }
}